using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LetsLearn.Core.Entities;
namespace LetsLearn.Infrastructure.Data
{
    public class LetsLearnContext : DbContext
    {
        #region Ctors
        public LetsLearnContext(DbContextOptions<LetsLearnContext> options) : base(options)
        {
        }
        #endregion

        #region DbSets
        public DbSet<Course> Courses { get; set; }
        public DbSet<Section> Sections { get; set; }
        public DbSet<Topic> Topics { get; set; }
        public DbSet<TopicPage> TopicPages { get; set; }
        public DbSet<TopicFile> TopicFiles { get; set; }
        public DbSet<TopicLink> TopicLinks { get; set; }
        public DbSet<TopicMeeting> TopicMeetings { get; set; }
        public DbSet<TopicMeetingHistory> TopicMeetingHistories { get; set; }
        public DbSet<TopicQuiz> TopicQuizzes { get; set; }
        public DbSet<TopicQuizQuestion> TopicQuizQuestions { get; set; }
        public DbSet<TopicQuizQuestionChoice> TopicQuizQuestionChoices { get; set; }
        public DbSet<TopicAssignment> TopicAssignments { get; set; }
        public DbSet<AssignmentResponse> AssignmentResponses { get; set; }
        public DbSet<CloudinaryFile> CloudinaryFiles { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionChoice> QuestionChoices { get; set; }
        public DbSet<QuizResponse> QuizResponses { get; set; }
        public DbSet<QuizResponseAnswer> QuizResponseAnswers { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<LectureDocument> LectureDocuments { get; set; }
        public DbSet<LectureChunk> LectureChunks { get; set; }
        public DbSet<AiQuestionGenerationJob> AiQuestionGenerationJobs { get; set; }
        public DbSet<AiGeneratedQuestion> AiGeneratedQuestions { get; set; }
        public DbSet<AiEvaluationResult> AiEvaluationResults { get; set; }
        public DbSet<LearningProgress> LearningProgresses { get; set; }
        public DbSet<TopicProgress> TopicProgresses { get; set; }
        public DbSet<LearningActivityLog> LearningActivityLogs { get; set; }
        public DbSet<AssignmentRubric> AssignmentRubrics { get; set; }
        public DbSet<AssignmentRubricCriterion> AssignmentRubricCriteria { get; set; }
        public DbSet<AiAssignmentGradingJob> AiAssignmentGradingJobs { get; set; }
        public DbSet<AiAssignmentGradeDraft> AiAssignmentGradeDrafts { get; set; }
        #endregion

        #region OnModelCreating
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== Enrollment (PK + FK) =====
            modelBuilder.Entity<Enrollment>()
                .HasKey(e => new { e.StudentId, e.CourseId });
                
            modelBuilder.Entity<Enrollment>()
                .HasOne<User>()
                .WithMany(u => u.Enrollments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Enrollment>()
                .HasOne<Course>().WithMany()
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== Course -> Section; Section -> Topic =====
            modelBuilder.Entity<Course>()
                .HasMany(c => c.Sections).WithOne()
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Section>()
                .HasMany(s => s.Topics).WithOne()
                .HasForeignKey(t => t.SectionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== Shared PK = FK for topic_* =====
            modelBuilder.Entity<TopicPage>()
                .HasKey(tp => tp.TopicId);
            modelBuilder.Entity<TopicPage>()
                .Property(tp => tp.TopicId).ValueGeneratedNever();
            modelBuilder.Entity<TopicPage>()
                .HasOne<Topic>().WithOne()
                .HasForeignKey<TopicPage>(tp => tp.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicFile>()
                .HasKey(tf => tf.TopicId);
            modelBuilder.Entity<TopicFile>()
                .Property(tf => tf.TopicId).ValueGeneratedNever();
            modelBuilder.Entity<TopicFile>()
                .HasOne<Topic>().WithOne()
                .HasForeignKey<TopicFile>(tf => tf.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            // TopicFile -> CloudinaryFile (teacher's file)
            modelBuilder.Entity<TopicFile>()
                .HasOne<CloudinaryFile>()
                .WithOne()
                .HasForeignKey<CloudinaryFile>(cf => cf.TopicFileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicLink>()
                .HasKey(tl => tl.TopicId);
            modelBuilder.Entity<TopicLink>()
                .Property(tl => tl.TopicId).ValueGeneratedNever();
            modelBuilder.Entity<TopicLink>()
                .HasOne<Topic>().WithOne()
                .HasForeignKey<TopicLink>(tl => tl.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicMeeting>()
                .HasKey(tm => tm.TopicId);
            modelBuilder.Entity<TopicMeeting>()
                .Property(tm => tm.TopicId).ValueGeneratedNever();
            modelBuilder.Entity<TopicMeeting>()
                .HasOne<Topic>().WithOne()
                .HasForeignKey<TopicMeeting>(tm => tm.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicMeeting>()
                .HasMany(tm => tm.Histories)
                .WithOne()
                .HasForeignKey(h => h.TopicMeetingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicMeetingHistory>()
                .HasKey(h => h.Id);

            modelBuilder.Entity<TopicAssignment>()
                .HasKey(ta => ta.TopicId);
            modelBuilder.Entity<TopicAssignment>()
                .Property(ta => ta.TopicId).ValueGeneratedNever();
            modelBuilder.Entity<TopicAssignment>()
                .HasOne<Topic>().WithOne()
                .HasForeignKey<TopicAssignment>(ta => ta.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicAssignment>()
                .HasMany(ta => ta.Files)
                .WithOne()
                .HasForeignKey(cf => cf.TopicAssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicQuiz>()
                .HasKey(tq => tq.TopicId);
            modelBuilder.Entity<TopicQuiz>()
                .Property(tq => tq.TopicId).ValueGeneratedNever();
            modelBuilder.Entity<TopicQuiz>()
                .HasOne<Topic>().WithOne()
                .HasForeignKey<TopicQuiz>(tq => tq.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicQuiz>()
                .HasMany(tq => tq.Questions).WithOne()
                .HasForeignKey(q => q.TopicQuizId)
                .HasPrincipalKey(tq => tq.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicQuizQuestion>()
                .HasMany(q => q.Choices).WithOne()
                .HasForeignKey(c => c.QuizQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== AssignmentResponse -> CloudinaryFile =====
            modelBuilder.Entity<AssignmentResponse>()
                .HasMany(ar => ar.Files).WithOne()
                .HasForeignKey(f => f.AssignmentResponseId)
                .OnDelete(DeleteBehavior.Cascade); 

            modelBuilder.Entity<AssignmentResponse>()
                .Property(ar => ar.TeacherFeedback)
                .HasMaxLength(4000);

            modelBuilder.Entity<AssignmentResponse>()
                .Property(ar => ar.GradedAt)
                .IsConcurrencyToken();

            modelBuilder.Entity<AssignmentRubric>(entity =>
            {
                entity.HasKey(rubric => rubric.Id);
                entity.Property(rubric => rubric.MaxScore).HasPrecision(5, 2);
                entity.Property(rubric => rubric.TeacherGuidance).HasColumnType("text");
                entity.HasIndex(rubric => new { rubric.TopicId, rubric.Version }).IsUnique();
                entity.HasOne<Topic>()
                    .WithMany()
                    .HasForeignKey(rubric => rubric.TopicId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(rubric => rubric.CreatedBy)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(rubric => rubric.Criteria)
                    .WithOne()
                    .HasForeignKey(criterion => criterion.RubricId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AssignmentRubricCriterion>(entity =>
            {
                entity.HasKey(criterion => criterion.Id);
                entity.Property(criterion => criterion.Name).HasMaxLength(200).IsRequired();
                entity.Property(criterion => criterion.Description).HasMaxLength(2000).IsRequired();
                entity.Property(criterion => criterion.MaxScore).HasPrecision(5, 2);
                entity.HasIndex(criterion => new { criterion.RubricId, criterion.SortOrder }).IsUnique();
            });

            modelBuilder.Entity<AiAssignmentGradingJob>(entity =>
            {
                entity.HasKey(job => job.Id);
                entity.Property(job => job.SubmissionHash).HasMaxLength(64).IsRequired();
                entity.Property(job => job.InputHash).HasMaxLength(64).IsRequired();
                entity.Property(job => job.RequestHash).HasMaxLength(64).IsRequired();
                entity.Property(job => job.SubmissionTextSnapshot).HasColumnType("text").IsRequired();
                entity.Property(job => job.AssignmentDescriptionSnapshot).HasColumnType("text").IsRequired();
                entity.Property(job => job.PromptVersion).HasMaxLength(50).IsRequired();
                entity.Property(job => job.Model).HasMaxLength(200).IsRequired();
                entity.Property(job => job.Status)
                    .HasMaxLength(20)
                    .HasDefaultValue(AiAssignmentGradingJobStatuses.Queued)
                    .IsRequired();
                entity.Property(job => job.ErrorCode).HasMaxLength(100);
                entity.Property(job => job.ErrorMessage).HasMaxLength(1000);
                entity.HasIndex(job => new { job.AssignmentResponseId, job.CreatedAt });
                entity.HasIndex(job => new { job.Status, job.CreatedAt });
                entity.HasIndex(job => new { job.Status, job.AvailableAt });
                entity.HasIndex(job => job.InputHash)
                    .IsUnique()
                    .HasFilter("\"Status\" IN ('queued', 'processing', 'succeeded')");
                entity.HasIndex(job => job.RequestHash)
                    .IsUnique()
                    .HasFilter("\"Status\" IN ('queued', 'processing', 'succeeded')");
                entity.HasOne<AssignmentResponse>()
                    .WithMany()
                    .HasForeignKey(job => job.AssignmentResponseId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<AssignmentRubric>()
                    .WithMany()
                    .HasForeignKey(job => job.RubricId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(job => job.RequestedBy)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(job => job.Draft)
                    .WithOne(draft => draft.Job)
                    .HasForeignKey<AiAssignmentGradeDraft>(draft => draft.JobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AiAssignmentGradeDraft>(entity =>
            {
                entity.HasKey(draft => draft.Id);
                entity.Property(draft => draft.SuggestedScore).HasPrecision(5, 2);
                entity.Property(draft => draft.FinalScore).HasPrecision(5, 2);
                entity.Property(draft => draft.CriterionResultsJson).HasColumnType("jsonb").IsRequired();
                entity.Property(draft => draft.StrengthsJson).HasColumnType("jsonb").IsRequired();
                entity.Property(draft => draft.ImprovementsJson).HasColumnType("jsonb").IsRequired();
                entity.Property(draft => draft.StudySuggestionsJson).HasColumnType("jsonb").IsRequired();
                entity.Property(draft => draft.SummaryFeedback).HasColumnType("text").IsRequired();
                entity.Property(draft => draft.FinalFeedback).HasColumnType("text");
                entity.Property(draft => draft.ReviewComment).HasMaxLength(2000);
                entity.Property(draft => draft.ReviewStatus)
                    .HasMaxLength(20)
                    .HasDefaultValue(AiAssignmentGradeReviewStatuses.Pending)
                    .IsRequired();
                entity.Property(draft => draft.ReviewVersion)
                    .HasDefaultValue(1)
                    .IsConcurrencyToken();
                entity.HasIndex(draft => draft.JobId).IsUnique();
                entity.HasIndex(draft => draft.ReviewStatus);
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(draft => draft.ReviewedBy)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ===== Conversation =====
            modelBuilder.Entity<Conversation>()
                .HasMany(c => c.Messages).WithOne()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== Comments (thread) =====
            modelBuilder.Entity<Comment>()
                .HasMany(c => c.Replies).WithOne()
                .HasForeignKey(r => r.ParentCommentId)
                .OnDelete(DeleteBehavior.Cascade); 

            modelBuilder.Entity<Comment>()
                .HasOne<Comment>().WithMany()
                .HasForeignKey(c => c.RootCommentId)
                .OnDelete(DeleteBehavior.NoAction);

            // ===== Question -> QuestionChoice =====
            modelBuilder.Entity<Question>()
                .HasMany(q => q.Choices)
                .WithOne()
                .HasForeignKey(c => c.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== QuizResponse -> QuizResponseAnswer =====
            modelBuilder.Entity<QuizResponse>()
                .HasMany(qr => qr.Answers)
                .WithOne()
                .HasForeignKey(a => a.QuizResponseId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== Unique constraints =====
            modelBuilder.Entity<Course>()
                .HasIndex(c => c.Title).IsUnique();
            // ===== User =====
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // ===== RefreshToken =====
            modelBuilder.Entity<RefreshToken>()
                .HasKey(rt => rt.Id);
            modelBuilder.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== AI question generation =====
            modelBuilder.Entity<LectureDocument>()
                .HasMany(d => d.Chunks)
                .WithOne()
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LectureChunk>()
                .HasIndex(c => new { c.DocumentId, c.ChunkOrder });

            modelBuilder.Entity<AiQuestionGenerationJob>()
                .HasMany(j => j.Questions)
                .WithOne()
                .HasForeignKey(q => q.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AiQuestionGenerationJob>()
                .Property(j => j.KnowledgePoint)
                .HasMaxLength(500);

            modelBuilder.Entity<AiGeneratedQuestion>()
                .HasIndex(q => new { q.JobId, q.Score });

            modelBuilder.Entity<AiEvaluationResult>()
                .HasOne<AiGeneratedQuestion>()
                .WithMany()
                .HasForeignKey(e => e.GeneratedQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== Learning progress =====
            modelBuilder.Entity<LearningProgress>()
                .HasKey(lp => lp.Id);

            modelBuilder.Entity<LearningProgress>()
                .HasIndex(lp => new { lp.StudentId, lp.CourseId })
                .IsUnique();

            modelBuilder.Entity<LearningProgress>()
                .HasIndex(lp => lp.CourseId);

            modelBuilder.Entity<LearningProgress>()
                .HasIndex(lp => lp.StudentId);

            modelBuilder.Entity<LearningProgress>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(lp => lp.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LearningProgress>()
                .HasOne<Course>()
                .WithMany()
                .HasForeignKey(lp => lp.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicProgress>()
                .HasKey(tp => tp.Id);

            modelBuilder.Entity<TopicProgress>()
                .HasIndex(tp => new { tp.StudentId, tp.TopicId })
                .IsUnique();

            modelBuilder.Entity<TopicProgress>()
                .HasIndex(tp => new { tp.CourseId, tp.StudentId });

            modelBuilder.Entity<TopicProgress>()
                .HasIndex(tp => tp.TopicId);

            modelBuilder.Entity<TopicProgress>()
                .Property(tp => tp.Status)
                .HasMaxLength(50);

            modelBuilder.Entity<TopicProgress>()
                .Property(tp => tp.CompletionSource)
                .HasMaxLength(50);

            modelBuilder.Entity<TopicProgress>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(tp => tp.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicProgress>()
                .HasOne<Course>()
                .WithMany()
                .HasForeignKey(tp => tp.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicProgress>()
                .HasOne<Topic>()
                .WithMany()
                .HasForeignKey(tp => tp.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LearningActivityLog>()
                .HasKey(log => log.Id);

            modelBuilder.Entity<LearningActivityLog>()
                .Property(log => log.EventType)
                .HasMaxLength(50);

            modelBuilder.Entity<LearningActivityLog>()
                .Property(log => log.EventSource)
                .HasMaxLength(50);

            modelBuilder.Entity<LearningActivityLog>()
                .HasIndex(log => new { log.UserId, log.OccurredAt });

            modelBuilder.Entity<LearningActivityLog>()
                .HasIndex(log => new { log.CourseId, log.OccurredAt });

            modelBuilder.Entity<LearningActivityLog>()
                .HasIndex(log => new { log.TopicId, log.OccurredAt });

            modelBuilder.Entity<LearningActivityLog>()
                .HasIndex(log => log.EventType);

            modelBuilder.Entity<LearningActivityLog>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LearningActivityLog>()
                .HasOne<Course>()
                .WithMany()
                .HasForeignKey(log => log.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LearningActivityLog>()
                .HasOne<Topic>()
                .WithMany()
                .HasForeignKey(log => log.TopicId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        #endregion
    }
}
