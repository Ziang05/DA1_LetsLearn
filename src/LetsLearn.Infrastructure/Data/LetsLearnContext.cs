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

            // ===== Conversation =====
            modelBuilder.Entity<Conversation>()
                .HasMany(c => c.Messages).WithOne()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Conversation>()
                .HasIndex(c => new { c.User1Id, c.User2Id }).IsUnique();

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
        }

        #endregion
    }
}
