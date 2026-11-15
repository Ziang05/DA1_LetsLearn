namespace LetsLearn.Core.Entities;

public class QuizBattle
{
    public Guid Id { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public Guid HostId { get; set; }
    public string CourseId { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "mixed";
    public string Status { get; set; } = "waiting";
    public int QuestionCount { get; set; }
    public int SecondsPerQuestion { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public Guid? RematchOfBattleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<QuizBattleParticipant> Participants { get; set; } = new List<QuizBattleParticipant>();
    public ICollection<QuizBattleAnswer> Answers { get; set; } = new List<QuizBattleAnswer>();
}

public class QuizBattleParticipant
{
    public Guid Id { get; set; }
    public Guid BattleId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; }
    public int Score { get; set; }
    public long TotalAnswerMilliseconds { get; set; }
}

public class QuizBattleAnswer
{
    public Guid Id { get; set; }
    public Guid BattleId { get; set; }
    public Guid UserId { get; set; }
    public int QuestionIndex { get; set; }
    public Guid? ChoiceId { get; set; }
    public bool IsCorrect { get; set; }
    public long AnswerMilliseconds { get; set; }
    public DateTime SubmittedAt { get; set; }
}
