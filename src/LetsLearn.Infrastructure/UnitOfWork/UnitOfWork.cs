using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Repository;
using LetsLearn.Infrastructure.Data;
using Microsoft.Extensions.Logging;
namespace LetsLearn.Infrastructure.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly LetsLearnContext _context;

        public IRepository<WeatherForecast> WeatherForecasts { get; private set; }

        public ICourseRepository Course { get; private set; }

        public IRepository<CloudinaryFile> CloudinaryFiles { get; private set; }
        public IUserRepository Users { get; private set; }
        public IRefreshTokenRepository RefreshTokens { get; private set; }
        public IMessageRepository Messages { get; private set; }
        public IConversationRepository Conversations { get; private set; }
        public IQuestionRepository Questions { get; }
        public IQuestionChoiceRepository QuestionChoices { get; }
        public ICommentRepository Comments { get; private set; }
        public IAssignmentResponseRepository AssignmentResponses { get; private set; }
        public IQuizResponseRepository QuizResponses { get; private set; }
        public IQuizResponseAnswerRepository QuizResponseAnswers { get; private set; }

        public ISectionRepository Sections { get; private set; }
        public ITopicRepository Topics { get; private set; }
        public ITopicPageRepository TopicPages { get; private set; }
        public ITopicFileRepository TopicFiles { get; private set; }
        public ITopicLinkRepository TopicLinks { get; private set; }
        public ITopicQuizRepository TopicQuizzes { get; private set; }
        public ITopicAssignmentRepository TopicAssignments { get; private set; }
        public ITopicMeetingRepository TopicMeetings { get; private set; }
        public IRepository<TopicMeetingHistory> TopicMeetingHistories { get; private set; }
        public IRepository<TopicQuizQuestion> TopicQuizQuestions { get; private set; }
        public IRepository<TopicQuizQuestionChoice> TopicQuizQuestionChoices { get; private set; }
        public IEnrollmentRepository Enrollments { get; private set; }
        public INotificationRepository Notifications { get; }
        public IRepository<Payment> Payments { get; private set; }
        public UnitOfWork(LetsLearnContext context, ILogger<QuestionRepository> questionLogger)
        {
            _context = context;
            WeatherForecasts = new GenericRepository<WeatherForecast>(_context);
            Course = new CourseRepository(_context);
            CloudinaryFiles = new GenericRepository<CloudinaryFile>(_context);
            Users = new UserRepository(_context);
            RefreshTokens = new RefreshTokenRepository(_context);
            Messages = new MessageRepository(_context);
            Conversations = new ConversationRepository(_context);
            Sections = new SectionRepository(_context);
            Topics = new TopicRepository(_context);
            TopicPages = new TopicPageRepository(_context);
            TopicFiles = new TopicFileRepository(_context);
            TopicLinks = new TopicLinkRepository(_context);
            TopicQuizzes = new TopicQuizRepository(_context);
            TopicAssignments = new TopicAssignmentRepository(_context);
            TopicMeetings = new TopicMeetingRepository(_context);
            TopicMeetingHistories = new GenericRepository<TopicMeetingHistory>(_context);
            Questions = new QuestionRepository(_context, questionLogger);
            QuestionChoices = new QuestionChoiceRepository(_context);
            Comments = new CommentRepository(context);
            AssignmentResponses = new AssignmentResponseRepository(_context);
            QuizResponses = new QuizResponseRepository(_context);
            QuizResponseAnswers = new QuizResponseAnswerRepository(_context);
            Enrollments = new EnrollmentRepository(_context);
            TopicQuizQuestions = new TopicQuizQuestionRepository(_context);
            TopicQuizQuestionChoices = new TopicQuizQuestionChoiceRepository(_context);
            Notifications = new NotificationRepository(_context);
            Payments = new GenericRepository<Payment>(_context);
        }

        public async Task<int> CommitAsync() =>
            await _context.SaveChangesAsync();

        public async ValueTask DisposeAsync() =>
            await _context.DisposeAsync();
    }
}
