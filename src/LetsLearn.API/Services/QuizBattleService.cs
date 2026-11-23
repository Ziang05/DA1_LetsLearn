using System.Security.Cryptography;
using System.Text.Json;
using LetsLearn.API.Hubs;
using LetsLearn.Core.Entities;
using LetsLearn.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LetsLearn.API.Services;

public sealed record BattleCreateRequest(string CourseId, int QuestionCount = 10, int DurationMinutes = 10, string Difficulty = "mixed");
public sealed record BattleChoiceDto(Guid Id, string Text);
public sealed record BattleQuestionDto(int Index, string Text, IReadOnlyList<BattleChoiceDto> Choices);
public sealed record BattleReviewDto(int QuestionIndex, string Question, string Difficulty, string? YourAnswer, string CorrectAnswer,
    bool? IsCorrect, string? Explanation);
public sealed record BattlePlayerDto(Guid UserId, string Username, string? Avatar, int Score, int AnsweredCount, bool IsHost);
public sealed record BattleAnswerRequest(int QuestionIndex, Guid? ChoiceId);
public sealed record BattleAnswerDto(int QuestionIndex, Guid? ChoiceId, bool Correct, int Points, int Score);
public sealed record BattleResultDto(Guid UserId, string Username, string? Avatar, int Score, int CorrectCount, long TotalAnswerMilliseconds, int Rank);
public sealed record LeaderboardRowDto(Guid UserId, string Username, string? Avatar, long Points, int Rank);
public sealed record BattleStateDto(Guid Id, string JoinCode, string Status, int QuestionCount, int DurationSeconds,
    string Difficulty, DateTime CreatedAt, DateTime? StartedAt, DateTime? CompletedAt, DateTime ServerNow, int CurrentQuestionIndex,
    DateTime? Deadline, DateTime? JoinEndsAt, bool Finished, IReadOnlyList<BattleQuestionDto> Questions,
    IReadOnlyList<BattlePlayerDto> Players, IReadOnlyList<BattleResultDto>? Results,
    IReadOnlyList<BattleReviewDto>? Review, Guid? RematchBattleId);

public sealed class QuizBattleService(LetsLearnContext db, IHubContext<QuizBattleHub> hub,
    TimeProvider clock, ILogger<QuizBattleService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private const int JoinWindowSeconds = 5 * 60;
    // SecondsPerQuestion is retained as the database column name for migration compatibility;
    // new battles store the total battle duration in this column.
    private static DateTime EndAt(QuizBattle b) => b.StartedAt!.Value.AddSeconds(b.SecondsPerQuestion);
    private static DateTime JoinEndsAt(QuizBattle b) => b.StartedAt!.Value.AddSeconds(JoinWindowSeconds);
    private static string Room(Guid id) => $"quiz-battle:{id:N}";

    public async Task<BattleStateDto> CreateAsync(Guid userId, BattleCreateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CourseId)) throw new ArgumentException("CourseId is required.");
        if (request.QuestionCount is < 3 or > 20) throw new ArgumentException("QuestionCount must be between 3 and 20.");
        if (request.DurationMinutes is < 5 or > 60) throw new ArgumentException("DurationMinutes must be between 5 and 60.");
        var difficulty = NormalizeBattleDifficulty(request.Difficulty);
        await EnsureCourseOwnerAsync(request.CourseId, userId, ct);
        var questions = await db.Questions.AsNoTracking().Include(q => q.Choices)
            .Where(q => q.CourseId == request.CourseId && q.DeletedAt == null && !q.Multiple).ToListAsync(ct);
        var usable = questions.Where(q => IsBattleReady(q.Status)
            && (q.Type?.Trim().ToLowerInvariant() is "choice" or "choices answer" or "multiple choice")
            && !string.IsNullOrWhiteSpace(q.QuestionText ?? q.QuestionName)
            && q.Choices.Count >= 2 && q.Choices.All(c => !string.IsNullOrWhiteSpace(c.Text))
            && q.Choices.Count(c => c.GradePercent == 100) == 1 && q.Choices.Count(c => c.GradePercent > 0) == 1).ToList();
        var selectedQuestions = SelectQuestions(usable, request.QuestionCount, difficulty);
        if (selectedQuestions.Count < request.QuestionCount)
        {
            var available = difficulty == "mixed" ? usable.Count : usable.Count(q => NormalizeQuestionDifficulty(q.Difficulty) == difficulty);
            throw new InvalidOperationException(difficulty == "mixed"
                ? $"This course has {available} usable single-answer questions across all difficulties; {request.QuestionCount} are required."
                : $"This course has {available} usable {difficulty} single-answer questions; {request.QuestionCount} are required.");
        }
        var snapshots = selectedQuestions
            .Select(q => new QuestionSnapshot(q.QuestionText ?? q.QuestionName!, q.Choices
                .Select(c => new ChoiceSnapshot(c.Id, c.Text!, c.GradePercent == 100, c.Feedback)).ToList(),
                q.FeedbackOfTrue, q.FeedbackOfFalse, NormalizeQuestionDifficulty(q.Difficulty))).ToList();
        var battle = new QuizBattle
        {
            Id = Guid.NewGuid(), HostId = userId, CourseId = request.CourseId,
            Difficulty = difficulty,
            QuestionCount = request.QuestionCount, SecondsPerQuestion = request.DurationMinutes * 60,
            QuestionsJson = JsonSerializer.Serialize(snapshots, JsonOptions), CreatedAt = Now,
            Participants = new List<QuizBattleParticipant> { new() { Id = Guid.NewGuid(), UserId = userId, JoinedAt = Now } }
        };
        db.QuizBattles.Add(battle);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            battle.JoinCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            try { await db.SaveChangesAsync(ct); return await GetAsync(battle.Id, userId, ct); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_QuizBattles_JoinCode" })
            { /* The unique index reserves the code, including concurrent creates. Retry with a new code. */ }
        }
        throw new InvalidOperationException("Could not reserve a battle code; try again.");
    }

    public async Task<BattleStateDto> JoinAsync(Guid userId, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 6 || !code.Trim().All(char.IsAsciiDigit))
            throw new ArgumentException("Enter a six-digit battle code.");
        var id = await db.QuizBattles.Where(b => b.JoinCode == code.Trim()).Select(b => (Guid?)b.Id).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Battle code not found.");
        await WithBattleLockAsync(id, async battle =>
        {
            if (battle.Participants.Any(p => p.UserId == userId)) return true;
            if (battle.Status == "completed") throw new InvalidOperationException("This battle has ended.");
            if (battle.Status == "active" && (!battle.StartedAt.HasValue || Now >= JoinEndsAt(battle)))
                throw new InvalidOperationException("Joining closed five minutes after the battle started.");
            if (battle.Participants.Count >= 8) throw new InvalidOperationException("This battle is full (8 players maximum).");
            await EnsureCourseAccessAsync(battle.CourseId, userId, ct);
            // Explicitly mark the new participant Added. Discovering a preassigned generated
            // GUID through an existing battle's navigation can mark it Modified instead.
            db.QuizBattleParticipants.Add(new QuizBattleParticipant
            {
                Id = Guid.NewGuid(), BattleId = id, UserId = userId, JoinedAt = Now
            });
            return true;
        }, ct);
        await PublishAsync(id, "PlayerJoined");
        return await GetAsync(id, userId, ct);
    }

    public async Task<BattleStateDto> StartAsync(Guid battleId, Guid userId, CancellationToken ct)
    {
        await WithBattleLockAsync(battleId, battle =>
        {
            if (battle.HostId != userId) throw new UnauthorizedAccessException("Only the host can start the battle.");
            // Retrying a successful start must never reset the clock.
            if (battle.Status != "waiting") return Task.FromResult(false);
            if (battle.Participants.Count < 2) throw new InvalidOperationException("At least two players are required.");
            battle.Status = "active";
            battle.StartedAt = Now.AddSeconds(3);
            return Task.FromResult(true);
        }, ct);
        await PublishAsync(battleId, "BattleStarted");
        return await GetAsync(battleId, userId, ct);
    }

    public async Task<BattleStateDto> EndEarlyAsync(Guid battleId, Guid userId, CancellationToken ct)
    {
        await WithBattleLockAsync(battleId, battle =>
        {
            if (battle.HostId != userId) throw new UnauthorizedAccessException("Only the host can end the battle early.");
            if (battle.Status == "completed") return Task.FromResult(false);
            if (battle.Status != "active") throw new InvalidOperationException("Start the battle before ending it.");
            battle.Status = "completed";
            battle.CompletedAt = Now;
            return Task.FromResult(true);
        }, ct);
        await PublishAsync(battleId, "BattleCompleted");
        return await GetAsync(battleId, userId, ct);
    }

    public async Task<BattleStateDto> RematchAsync(Guid battleId, Guid userId, CancellationToken ct)
    {
        Guid rematchId = Guid.Empty;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                rematchId = await WithBattleLockAsync(battleId, async source =>
                {
                    if (source.HostId != userId) throw new UnauthorizedAccessException("Only the host can start a rematch.");
                    if (source.Status != "completed") throw new InvalidOperationException("A rematch is available after the battle ends.");

                    var existingId = await db.QuizBattles.AsNoTracking()
                        .Where(b => b.RematchOfBattleId == battleId)
                        .Select(b => (Guid?)b.Id).SingleOrDefaultAsync(ct);
                    if (existingId.HasValue) return existingId.Value;

                    var rematch = new QuizBattle
                    {
                        Id = Guid.NewGuid(),
                        JoinCode = await NewJoinCodeAsync(ct),
                        HostId = source.HostId,
                        CourseId = source.CourseId,
                        Difficulty = source.Difficulty,
                        Status = "waiting",
                        QuestionCount = source.QuestionCount,
                        SecondsPerQuestion = source.SecondsPerQuestion,
                        QuestionsJson = source.QuestionsJson,
                        RematchOfBattleId = source.Id,
                        CreatedAt = Now,
                        Participants = source.Participants.Select(p => new QuizBattleParticipant
                        {
                            Id = Guid.NewGuid(), UserId = p.UserId, JoinedAt = Now
                        }).ToList()
                    };
                    db.QuizBattles.Add(rematch);
                    return rematch.Id;
                }, ct);
                break;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_QuizBattles_JoinCode" })
            {
                // Join codes are prechecked, but concurrent rematches can still reserve one
                // between the check and insert. Retry the whole transaction with a fresh code.
                if (attempt == 4) throw new InvalidOperationException("Could not reserve a battle code; try again.", ex);
            }
        }

        await PublishAsync(battleId, "RematchCreated");
        return await GetAsync(rematchId, userId, ct);
    }

    public async Task<object> LeaveAsync(Guid battleId, Guid userId, CancellationToken ct)
    {
        var closed = await WithBattleLockAsync(battleId, battle =>
        {
            var participant = RequireParticipant(battle, userId);
            if (battle.Status != "waiting") throw new InvalidOperationException("Players cannot leave after a battle has started.");
            if (battle.HostId == userId) db.QuizBattles.Remove(battle);
            else db.QuizBattleParticipants.Remove(participant);
            return Task.FromResult(battle.HostId == userId);
        }, ct);
        await PublishAsync(battleId, closed ? "RoomClosed" : "PlayerLeft");
        return new { closed };
    }

    public Task<BattleStateDto> GetAsync(Guid battleId, Guid userId, CancellationToken ct) =>
        WithBattleLockAsync(battleId, async battle =>
        {
            RequireParticipant(battle, userId);
            var now = Now;
            await CompleteIfReadyAsync(battle, now, ct);
            var users = await UsersAsync(battle, ct);
            var answers = await db.QuizBattleAnswers.AsNoTracking().Where(a => a.BattleId == battleId).ToListAsync(ct);
            var myAnswers = answers.Where(a => a.UserId == userId).OrderBy(a => a.QuestionIndex).ToList();
            var finished = myAnswers.Count >= battle.QuestionCount;
            var index = battle.Status == "active" && now >= battle.StartedAt!.Value && !finished
                ? myAnswers.Count : -1;
            IReadOnlyList<BattleQuestionDto> questions = Array.Empty<BattleQuestionDto>();
            if (index >= 0)
            {
                var question = Snapshots(battle)[index];
                questions = new[] { new BattleQuestionDto(index, question.Text, question.Choices.Select(c => new BattleChoiceDto(c.Id, c.Text)).ToList()) };
            }
            var players = battle.Participants.OrderBy(p => p.JoinedAt).ThenBy(p => p.UserId).Select(p =>
                new BattlePlayerDto(p.UserId, users[p.UserId].Username, users[p.UserId].Avatar, p.Score,
                    answers.Count(a => a.UserId == p.UserId), p.UserId == battle.HostId)).ToList();
            var review = battle.Status == "completed" ? BuildReview(battle, myAnswers) : null;
            var rematchBattleId = battle.Status == "completed"
                ? await db.QuizBattles.AsNoTracking().Where(b => b.RematchOfBattleId == battle.Id)
                    .Select(b => (Guid?)b.Id).SingleOrDefaultAsync(ct)
                : null;
            return new BattleStateDto(battle.Id, battle.JoinCode, battle.Status, battle.QuestionCount, battle.SecondsPerQuestion,
                battle.Difficulty,
                battle.CreatedAt, battle.StartedAt, battle.CompletedAt, now, index,
                battle.StartedAt.HasValue ? EndAt(battle) : null,
                battle.StartedAt.HasValue ? JoinEndsAt(battle) : null, finished,
                questions, players,
                battle.Status == "completed" ? RankResults(battle, users, answers) : null,
                review, rematchBattleId);
        }, ct);

    public async Task<BattleAnswerDto> AnswerAsync(Guid battleId, Guid userId, BattleAnswerRequest request, CancellationToken ct)
    {
        var battleCompleted = false;
        var receipt = await WithBattleLockAsync(battleId, async battle =>
        {
            var participant = RequireParticipant(battle, userId);
            var previous = await db.QuizBattleAnswers.SingleOrDefaultAsync(a => a.BattleId == battleId
                && a.UserId == userId && a.QuestionIndex == request.QuestionIndex, ct);
            if (previous != null)
            {
                if (previous.ChoiceId != request.ChoiceId) throw new InvalidOperationException("Your answer is already locked.");
                return Receipt(battle, previous, participant.Score);
            }
            var now = Now;
            if (battle.Status != "active" || !battle.StartedAt.HasValue || now < battle.StartedAt.Value)
                throw new InvalidOperationException("Battle is not active yet.");
            if (now >= EndAt(battle)) throw new InvalidOperationException("The battle time has ended.");
            var elapsedMs = (long)(now - battle.StartedAt.Value).TotalMilliseconds;
            var index = await db.QuizBattleAnswers.CountAsync(a => a.BattleId == battleId && a.UserId == userId, ct);
            if (index >= battle.QuestionCount) throw new InvalidOperationException("You have completed all questions.");
            if (request.QuestionIndex != index) throw new InvalidOperationException("That question is not currently active.");
            var choice = Snapshots(battle)[index].Choices.FirstOrDefault(c => c.Id == request.ChoiceId)
                ?? throw new ArgumentException("Select a choice belonging to this question.");
            var answer = new QuizBattleAnswer
            {
                Id = Guid.NewGuid(), BattleId = battleId, UserId = userId, QuestionIndex = index,
                ChoiceId = choice.Id, IsCorrect = choice.IsCorrect,
                AnswerMilliseconds = elapsedMs, SubmittedAt = now
            };
            db.QuizBattleAnswers.Add(answer);
            participant.Score += Points(battle, answer);
            if (index + 1 == battle.QuestionCount) participant.TotalAnswerMilliseconds = elapsedMs;

            var acceptedAnswers = await db.QuizBattleAnswers.CountAsync(a => a.BattleId == battleId, ct) + 1;
            if (now >= JoinEndsAt(battle) && acceptedAnswers >= battle.Participants.Count * battle.QuestionCount)
            {
                battle.Status = "completed";
                battle.CompletedAt = now;
                battleCompleted = true;
            }
            return Receipt(battle, answer, participant.Score);
        }, ct);
        await PublishAsync(battleId, battleCompleted ? "BattleCompleted" : "AnswerReceived");
        return receipt;
    }

    public Task<IReadOnlyList<BattleResultDto>> ResultsAsync(Guid battleId, Guid userId, CancellationToken ct) =>
        WithBattleLockAsync(battleId, async battle =>
        {
            RequireParticipant(battle, userId);
            await CompleteIfReadyAsync(battle, Now, ct);
            if (battle.Status != "completed") throw new InvalidOperationException("Results are available after the battle ends.");
            return RankResults(battle, await UsersAsync(battle, ct),
                await db.QuizBattleAnswers.AsNoTracking().Where(a => a.BattleId == battleId).ToListAsync(ct));
        }, ct);

    public async Task<IReadOnlyList<LeaderboardRowDto>> LeaderboardAsync(string period, string? courseId, Guid userId, CancellationToken ct)
    {
        courseId = string.IsNullOrWhiteSpace(courseId) ? null : courseId.Trim();
        if (courseId != null) await EnsureCourseAccessAsync(courseId, userId, ct);
        var now = Now;
        var since = period?.ToLowerInvariant() switch
        {
            "week" => now.AddDays(-7), "month" => now.AddDays(-30), "all" => (DateTime?)null,
            _ => throw new ArgumentException("Period must be all, week, or month.")
        };
        // Award an activity only on its first occurrence, then apply the ranking window.
        var activityLogs = db.LearningActivityLogs.AsNoTracking()
            .Where(a => a.TopicId != null && (a.EventType == "topic_completed" || a.EventType == "quiz_submitted" || a.EventType == "assignment_submitted"));
        if (courseId != null) activityLogs = activityLogs.Where(a => a.CourseId == courseId);
        var firstActivities = activityLogs
            .GroupBy(a => new { a.UserId, a.TopicId, a.EventType })
            .Select(g => new { g.Key.UserId, g.Key.EventType, OccurredAt = g.Min(a => a.OccurredAt) })
            .Where(a => a.OccurredAt <= now);
        if (since.HasValue) firstActivities = firstActivities.Where(a => a.OccurredAt >= since.Value);
        var activities = await firstActivities.GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(a => a.EventType == "quiz_submitted" ? 15L : 10L) }).ToListAsync(ct);
        var battles = from p in db.QuizBattleParticipants.AsNoTracking()
                      join b in db.QuizBattles.AsNoTracking() on p.BattleId equals b.Id
                      where b.Status == "completed" && b.CompletedAt <= now
                      select new { p.UserId, p.Score, b.CompletedAt, b.CourseId };
        if (courseId != null) battles = battles.Where(b => b.CourseId == courseId);
        if (since.HasValue) battles = battles.Where(b => b.CompletedAt >= since.Value);
        var battlePoints = await battles.GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(p => (long)p.Score) }).ToListAsync(ct);
        var scores = activities.ToDictionary(a => a.UserId, a => a.Points);
        foreach (var row in battlePoints) scores[row.UserId] = scores.GetValueOrDefault(row.UserId) + row.Points;
        var top = scores.Where(s => s.Value > 0).OrderByDescending(s => s.Value).ThenBy(s => s.Key).Take(50).ToList();
        var ids = top.Select(s => s.Key).ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var rows = new List<LeaderboardRowDto>();
        var rank = 0;
        foreach (var item in top.Where(s => users.ContainsKey(s.Key)))
        {
            if (rows.Count == 0 || rows[^1].Points != item.Value) rank = rows.Count + 1;
            rows.Add(new(item.Key, users[item.Key].Username, users[item.Key].Avatar, item.Value, rank));
        }
        return rows;
    }

    // Finishing a battle must not depend on a browser staying open.
    public async Task CompleteDueBattlesAsync(CancellationToken ct)
    {
        var now = Now;
        var ids = await db.QuizBattles.AsNoTracking().Where(b => b.Status == "active" && b.StartedAt.HasValue
            && b.StartedAt.Value.AddSeconds(JoinWindowSeconds) <= now)
            .OrderBy(b => b.StartedAt).Select(b => b.Id).Take(100).ToListAsync(ct);
        foreach (var id in ids)
        {
            try
            {
                var changed = await WithBattleLockAsync(id, b => CompleteIfReadyAsync(b, Now, ct), ct);
                if (changed) await PublishAsync(id, "BattleCompleted");
            }
            catch (KeyNotFoundException) { /* Host/account deletion raced the scan. */ }
        }
    }

    private async Task<T> WithBattleLockAsync<T>(Guid id, Func<QuizBattle, Task<T>> action, CancellationToken ct)
    {
        // Each operation owns its unit of work. Discard old tracked state before waiting for the database lock.
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var query = db.Database.IsNpgsql()
            ? db.QuizBattles.FromSqlInterpolated($"SELECT * FROM \"QuizBattles\" WHERE \"Id\" = {id} FOR UPDATE")
            : db.QuizBattles.Where(b => b.Id == id);
        var battle = await query.SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Battle not found.");
        await db.Entry(battle).Collection(b => b.Participants).LoadAsync(ct);
        var result = await action(battle);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private async Task EnsureCourseAccessAsync(string courseId, Guid userId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == courseId && c.IsPublished, ct)
            ?? throw new KeyNotFoundException("Published course not found.");
        if (course.CreatorId != userId && !await db.Enrollments.AnyAsync(e => e.CourseId == courseId && e.StudentId == userId, ct))
            throw new UnauthorizedAccessException("Join the course before entering this battle.");
    }

    private async Task EnsureCourseOwnerAsync(string courseId, Guid userId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == courseId && c.IsPublished, ct)
            ?? throw new KeyNotFoundException("Published course not found.");
        if (course.CreatorId != userId)
            throw new UnauthorizedAccessException("Only the course teacher can create a Quiz Battle.");
    }

    private static QuizBattleParticipant RequireParticipant(QuizBattle battle, Guid userId) =>
        battle.Participants.FirstOrDefault(p => p.UserId == userId) ?? throw new UnauthorizedAccessException("You are not in this battle.");

    // Older bank rows may not have a status; the client presents those as Ready.
    private static bool IsBattleReady(string? status) => string.IsNullOrWhiteSpace(status)
        || status.Trim().ToLowerInvariant() is "ready" or "active" or "published";

    private Task<Dictionary<Guid, User>> UsersAsync(QuizBattle battle, CancellationToken ct)
    {
        var ids = battle.Participants.Select(p => p.UserId).ToList();
        return db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
    }

    private static bool CompleteIfDue(QuizBattle battle, DateTime now)
    {
        if (battle.Status != "active" || !battle.StartedAt.HasValue || now < EndAt(battle)) return false;
        battle.Status = "completed";
        battle.CompletedAt = EndAt(battle);
        return true;
    }

    private async Task<bool> CompleteIfReadyAsync(QuizBattle battle, DateTime now, CancellationToken ct)
    {
        if (CompleteIfDue(battle, now)) return true;
        if (battle.Status != "active" || !battle.StartedAt.HasValue || now < JoinEndsAt(battle)) return false;
        var answerCount = await db.QuizBattleAnswers.CountAsync(a => a.BattleId == battle.Id, ct);
        if (answerCount < battle.Participants.Count * battle.QuestionCount) return false;
        battle.Status = "completed";
        battle.CompletedAt = now;
        return true;
    }

    private static int Points(QuizBattle b, QuizBattleAnswer a) => a.IsCorrect ? 100 : 0;
    private static BattleAnswerDto Receipt(QuizBattle b, QuizBattleAnswer a, int score) =>
        new(a.QuestionIndex, a.ChoiceId, a.IsCorrect, Points(b, a), score);

    private static IReadOnlyList<BattleResultDto> RankResults(QuizBattle battle, Dictionary<Guid, User> users, List<QuizBattleAnswer> answers)
    {
        // Equal scores are resolved by completion time measured from the common start.
        var sorted = battle.Participants.Select(p => new
        {
            Player = p,
            Correct = answers.Count(a => a.UserId == p.UserId && a.IsCorrect),
            Time = answers.Count(a => a.UserId == p.UserId) >= battle.QuestionCount
                ? p.TotalAnswerMilliseconds : battle.SecondsPerQuestion * 1000L
        }).OrderByDescending(r => r.Player.Score).ThenBy(r => r.Time).ThenBy(r => r.Player.UserId).ToList();
        var result = new List<BattleResultDto>();
        var rank = 0;
        for (var i = 0; i < sorted.Count; i++)
        {
            var row = sorted[i];
            if (i == 0 || row.Player.Score != sorted[i - 1].Player.Score || row.Time != sorted[i - 1].Time) rank = i + 1;
            var user = users[row.Player.UserId];
            result.Add(new(row.Player.UserId, user.Username, user.Avatar, row.Player.Score, row.Correct, row.Time, rank));
        }
        return result;
    }

    private static IReadOnlyList<BattleReviewDto> BuildReview(QuizBattle battle, List<QuizBattleAnswer> myAnswers)
    {
        var snapshots = Snapshots(battle);
        return snapshots.Select((question, index) =>
        {
            var submitted = myAnswers.FirstOrDefault(a => a.QuestionIndex == index);
            var selected = submitted == null ? null : question.Choices.FirstOrDefault(c => c.Id == submitted.ChoiceId);
            var correct = question.Choices.First(c => c.IsCorrect);
            var explanation = !string.IsNullOrWhiteSpace(selected?.Feedback)
                ? selected.Feedback
                : !string.IsNullOrWhiteSpace(correct.Feedback)
                    ? correct.Feedback
                    : submitted?.IsCorrect == true ? question.FeedbackOfTrue : question.FeedbackOfFalse;
            return new BattleReviewDto(index, question.Text, NormalizeQuestionDifficulty(question.Difficulty), selected?.Text, correct.Text,
                submitted == null ? null : submitted.IsCorrect, explanation);
        }).ToList();
    }

    private async Task<string> NewJoinCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            if (!await db.QuizBattles.AsNoTracking().AnyAsync(b => b.JoinCode == code, ct)) return code;
        }
        throw new InvalidOperationException("Could not reserve a battle code; try again.");
    }

    private async Task PublishAsync(Guid id, string eventName)
    {
        // A failed notification cannot roll back a committed command. Only invalidations go to groups:
        // membership can change while an old connection remains subscribed.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await hub.Clients.Group(Room(id)).SendAsync(eventName, new { battleId = id }, timeout.Token); }
        catch (Exception ex) { logger.LogWarning(ex, "Quiz Battle notification {Event} failed for {BattleId}; REST sync will recover", eventName, id); }
    }

    private static List<QuestionSnapshot> Snapshots(QuizBattle b) => JsonSerializer.Deserialize<List<QuestionSnapshot>>(b.QuestionsJson, JsonOptions)!;
    private static string NormalizeBattleDifficulty(string? difficulty) => difficulty?.Trim().ToLowerInvariant() switch
    {
        null or "" or "mixed" => "mixed",
        "easy" => "easy",
        "medium" => "medium",
        "hard" => "hard",
        _ => throw new ArgumentException("Difficulty must be easy, medium, hard, or mixed.")
    };
    private static string NormalizeQuestionDifficulty(string? difficulty) => difficulty?.Trim().ToLowerInvariant() switch
    {
        "easy" => "easy",
        "hard" => "hard",
        _ => "medium"
    };
    private static List<Question> SelectQuestions(List<Question> questions, int count, string difficulty)
    {
        var random = questions.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
        if (difficulty != "mixed") return random.Where(q => NormalizeQuestionDifficulty(q.Difficulty) == difficulty).Take(count).ToList();

        var selected = new List<Question>(count);
        var buckets = new[] { "easy", "medium", "hard" }
            .Select(level => random.Where(q => NormalizeQuestionDifficulty(q.Difficulty) == level).ToList()).ToList();
        var baseTarget = count / buckets.Count;
        var remainder = count % buckets.Count;
        for (var i = 0; i < buckets.Count; i++)
            selected.AddRange(buckets[i].Take(baseTarget + (i < remainder ? 1 : 0)));
        if (selected.Count < count)
        {
            var selectedIds = selected.Select(q => q.Id).ToHashSet();
            selected.AddRange(random.Where(q => !selectedIds.Contains(q.Id)).Take(count - selected.Count));
        }
        return selected.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
    }
    private sealed record QuestionSnapshot(string Text, List<ChoiceSnapshot> Choices,
        string? FeedbackOfTrue = null, string? FeedbackOfFalse = null, string? Difficulty = "medium");
    private sealed record ChoiceSnapshot(Guid Id, string Text, bool IsCorrect, string? Feedback = null);
}
