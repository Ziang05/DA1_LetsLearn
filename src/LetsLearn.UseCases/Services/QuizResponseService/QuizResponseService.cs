using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services.QuizResponseService
{
    public class QuizResponseService : IQuizResponseService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILearningProgressService _learningProgressService;

        public QuizResponseService(
            IUnitOfWork unitOfWork,
            ILearningProgressService learningProgressService)
        {
            _unitOfWork = unitOfWork;
            _learningProgressService = learningProgressService;
        }

        private QuizResponseDTO ToDto(QuizResponse entity)
        {
            return new QuizResponseDTO
            {
                Id = entity.Id,
                TopicId = entity.TopicId,
                StudentId = entity.StudentId,
                Data = new QuizResponseData
                {
                    Status = entity.Status,
                    StartedAt = entity.StartedAt,
                    CompletedAt = entity.CompletedAt,
                    Answers = entity.Answers.Select(a =>
                    {
                        var questionEntity = JsonSerializer.Deserialize<Question>(a.Question!)!;
                        return new QuizResponseAnswerDTO
                        {
                            TopicQuizQuestionId = questionEntity.Id,
                            Question = CreateQuestionDTO(questionEntity),
                            Answer = a.Answer,
                            Mark = a.Mark
                        };
                    }).ToList()
                }
            };
        }

        private QuestionDTO CreateQuestionDTO(Question questionEntity)
        {
            var questionDto = new QuestionDTO
            {
                Id = questionEntity.Id,
                Type = questionEntity.Type,
                QuestionText = questionEntity.QuestionText,
                DefaultMark = questionEntity.DefaultMark
            };

            // Handle different question types with specific data structures
            switch (questionEntity.Type?.ToLower()?.Trim())
            {
                case "choices answer":
                case "choice":
                case "multiple choice":
                    questionDto.Data = new QuestionDataDTO
                    {
                        Multiple = questionEntity.Multiple,
                        Choices = questionEntity.Choices?.Select(c => new QuestionChoiceDTO
                        {
                            Id = c.Id.ToString(),
                            Text = c.Text,
                            GradePercent = c.GradePercent,
                            Feedback = c.Feedback
                        }).ToList() ?? new List<QuestionChoiceDTO>()
                    };
                    break;

                case "true/false":
                case "truefalse":
                case "boolean":
                    questionDto.Data = new QuestionDataDTO
                    {
                        CorrectAnswer = questionEntity.CorrectAnswer,
                        FeedbackOfTrue = questionEntity.FeedbackOfTrue,
                        FeedbackOfFalse = questionEntity.FeedbackOfFalse
                    };
                    break;

                case "short answer":
                case "shortanswer":
                case "text":
                    questionDto.Data = new QuestionDataDTO
                    {
                        Choices = questionEntity.Choices?.Select(c => new QuestionChoiceDTO
                        {
                            Id = c.Id.ToString(),
                            Text = c.Text,
                            GradePercent = c.GradePercent,
                            Feedback = c.Feedback
                        }).ToList() ?? new List<QuestionChoiceDTO>()
                    };
                    break;

                default:
                    questionDto.Data = new QuestionDataDTO
                    {
                        Multiple = questionEntity.Multiple,
                        Choices = questionEntity.Choices?.Select(c => new QuestionChoiceDTO
                        {
                            Id = c.Id.ToString(),
                            Text = c.Text,
                            GradePercent = c.GradePercent,
                            Feedback = c.Feedback
                        }).ToList() ?? new List<QuestionChoiceDTO>()
                    };
                    break;
            }

            return questionDto;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if TopicQuizQuestion not found: +1
        // - foreach answer → serialization / mapping per item: +1
        // - DbUpdateException when CommitAsync: +1
        // D = 3 => Minimum Test Cases = D + 1 = 4
        public async Task<QuizResponseDTO> CreateQuizResponseAsync(QuizResponseRequest dto, Guid studentId, CancellationToken ct = default)
        {
            var quiz = await _unitOfWork.TopicQuizzes.GetWithQuestionsAsync(dto.TopicId)
                ?? throw new KeyNotFoundException($"Quiz topic {dto.TopicId} not found.");

            var entity = new QuizResponse
            {
                Id = Guid.NewGuid(),
                TopicId = dto.TopicId,
                StudentId = studentId,
                Status = dto.Data.Status,
                StartedAt = dto.Data.StartedAt,
                CompletedAt = dto.Data.CompletedAt,
                Answers = new List<QuizResponseAnswer>()
            };

            foreach (var a in dto.Data.Answers)
            {
                var topicQuestionEntity = quiz.Questions.FirstOrDefault(question => question.Id == a.TopicQuizQuestionId)
                    ?? throw new ArgumentException($"Question {a.TopicQuizQuestionId} does not belong to quiz {dto.TopicId}.");
                var fullQuestion = CreateQuizQuestionSnapshot(topicQuestionEntity);

                entity.Answers.Add(new QuizResponseAnswer
                {
                    Id = Guid.NewGuid(),
                    QuizResponseId = entity.Id,
                    Question = JsonSerializer.Serialize(fullQuestion),
                    Answer = a.Answer,
                    Mark = QuizAnswerScorer.CalculateMark(fullQuestion, a.Answer) ?? 0m
                });
            }

            await _unitOfWork.QuizResponses.AddAsync(entity);
            await _unitOfWork.LearningActivityLogs.AddAsync(new LearningActivityLog
            {
                Id = Guid.NewGuid(),
                UserId = studentId,
                CourseId = await ResolveCourseIdByTopicAsync(dto.TopicId, ct),
                TopicId = dto.TopicId,
                EventType = "quiz_submitted",
                EventSource = "quiz_response",
                Metadata = JsonSerializer.Serialize(new
                {
                    quizResponseId = entity.Id,
                    status = entity.Status,
                    answerCount = entity.Answers.Count,
                    completedAt = entity.CompletedAt
                }),
                OccurredAt = DateTime.UtcNow
            });
            await _unitOfWork.CommitAsync();
            await _learningProgressService.MarkTopicCompletedAsync(dto.TopicId, studentId, "submit_quiz", ct);

            return ToDto(entity);
        }

        private async Task<string?> ResolveCourseIdByTopicAsync(Guid topicId, CancellationToken ct)
        {
            var topic = await _unitOfWork.Topics.GetByIdAsync(topicId, ct);
            if (topic == null)
            {
                return null;
            }

            var section = await _unitOfWork.Sections.GetByIdAsync(topic.SectionId, ct);
            return section?.CourseId;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if quizResponse not found: +1
        // - if TopicQuizQuestion not found during update: +1
        // - if old answers exist → DeleteRangeAsync: +1
        // - foreach new answer → add item: +1
        // - DbUpdateException when CommitAsync: +1
        // D = 5 => Minimum Test Cases = D + 1 = 6
        public async Task<QuizResponseDTO> UpdateQuizResponseByIdAsync(Guid id, QuizResponseRequest dto, Guid studentId, CancellationToken ct = default)
        {
            var entity = await _unitOfWork.QuizResponses.GetByIdTrackedWithAnswersAsync(id,ct);
            if (entity == null)
                throw new Exception("Quiz response not found");
            if (entity.StudentId != studentId)
                throw new UnauthorizedAccessException("You can only update your own quiz attempt.");
            if (entity.TopicId != dto.TopicId)
                throw new ArgumentException("The quiz topic cannot be changed for an existing attempt.");

            var quiz = await _unitOfWork.TopicQuizzes.GetWithQuestionsAsync(entity.TopicId)
                ?? throw new KeyNotFoundException($"Quiz topic {entity.TopicId} not found.");

            entity.Status = dto.Data.Status;
            entity.StartedAt = dto.Data.StartedAt;
            entity.CompletedAt = dto.Data.CompletedAt;

            await _unitOfWork.QuizResponseAnswers.DeleteRangeAsync(entity.Answers);
            entity.Answers.Clear();

            foreach (var a in dto.Data.Answers)
            {
                var questionEntity = quiz.Questions.FirstOrDefault(question => question.Id == a.TopicQuizQuestionId)
                    ?? throw new ArgumentException($"Question {a.TopicQuizQuestionId} does not belong to quiz {entity.TopicId}.");
                var fullQuestion = CreateQuizQuestionSnapshot(questionEntity);

                entity.Answers.Add(new QuizResponseAnswer
                {
                    Id = Guid.NewGuid(),
                    QuizResponseId = entity.Id,
                    Question = JsonSerializer.Serialize(fullQuestion),
                    Answer = a.Answer,
                    Mark = QuizAnswerScorer.CalculateMark(fullQuestion, a.Answer) ?? 0m
                });
            }

            await _unitOfWork.CommitAsync();

            return ToDto(entity);
        }

        private static Question CreateQuizQuestionSnapshot(TopicQuizQuestion topicQuestion) => new()
        {
            Id = topicQuestion.Id,
            QuestionName = topicQuestion.QuestionName,
            QuestionText = topicQuestion.QuestionText,
            Type = topicQuestion.Type,
            DefaultMark = topicQuestion.DefaultMark,
            FeedbackOfTrue = topicQuestion.FeedbackOfTrue,
            FeedbackOfFalse = topicQuestion.FeedbackOfFalse,
            CorrectAnswer = topicQuestion.CorrectAnswer ?? false,
            Multiple = topicQuestion.Multiple ?? false,
            Choices = topicQuestion.Choices.Select(choice => new QuestionChoice
            {
                Id = choice.Id,
                QuestionId = topicQuestion.Id,
                Text = choice.Text,
                GradePercent = choice.GradePercent,
                Feedback = choice.Feedback
            }).ToList()
        };

        // Test Case Estimation:
        // Decision points (D):
        // - if quizResponse not found: +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task<QuizResponseDTO> GetQuizResponseByIdAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await _unitOfWork.QuizResponses.GetByIdTrackedWithAnswersAsync(id,ct);
            if (entity == null)
            {
                throw new Exception("Quiz response not found");
            }

            return ToDto(entity);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - No branching logic: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task<IEnumerable<QuizResponseDTO>> GetAllQuizResponsesByTopicIdAsync(Guid topicId, CancellationToken ct = default)
        {
            var entities = await _unitOfWork.QuizResponses.FindAllByTopicIdWithAnswersAsync(topicId, ct);
            return entities.Select(ToDto).ToList();
        }

        // Test Case Estimation:
        // Decision points (D):
        // - No branching logic: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task<IEnumerable<QuizResponseDTO>> GetAllQuizResponsesByTopicIdOfStudentAsync(Guid topicId, Guid studentId, CancellationToken ct = default)
        {
            var entities = await _unitOfWork.QuizResponses.FindByTopicIdAndStudentIdWithAnswersAsync(topicId, studentId, ct);
            return entities.Select(ToDto).ToList();
        }

        //public async Task DeleteQuizResponseByIdAsync(Guid id)
        //{
        //    var entity = await _unitOfWork.QuizResponses.GetByIdAsync(id);
        //    if (entity == null) throw new Exception("Quiz response not found");

        //    await _unitOfWork.QuizResponses.DeleteAsync(entity);
        //    await _unitOfWork.CommitAsync();
        //}
    }
}
