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

        public QuizResponseService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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
                // Load câu hỏi từ TopicQuizQuestion
                var topicQuestionEntity = await _unitOfWork.TopicQuizQuestions.GetByIdAsync(a.TopicQuizQuestionId);
                if (topicQuestionEntity == null)
                    throw new Exception($"TopicQuizQuestion {a.TopicQuizQuestionId} not found");

                // 🔥 THÊM: Load từ Question Bank để có feedback đầy đủ
                Question fullQuestion = null;

                // Try to find the original question in Question Bank by matching content
                var allQuestions = await _unitOfWork.Questions.FindAsync(q =>
                    q.QuestionText == topicQuestionEntity.QuestionText &&
                    q.Type == topicQuestionEntity.Type);

                var originalQuestion = allQuestions.FirstOrDefault();

                if (originalQuestion != null)
                {
                    // Use data from Question Bank (has feedback)
                    fullQuestion = new Question
                    {
                        Id = topicQuestionEntity.Id,  // Keep TopicQuizQuestion ID for reference
                        QuestionName = originalQuestion.QuestionName,
                        QuestionText = originalQuestion.QuestionText,
                        Status = originalQuestion.Status,
                        Type = originalQuestion.Type,
                        DefaultMark = originalQuestion.DefaultMark,
                        Usage = originalQuestion.Usage ?? 0,
                        FeedbackOfTrue = originalQuestion.FeedbackOfTrue,      // ✅ From Question Bank
                        FeedbackOfFalse = originalQuestion.FeedbackOfFalse,    // ✅ From Question Bank
                        CorrectAnswer = originalQuestion.CorrectAnswer,
                        Multiple = originalQuestion.Multiple,
                        CreatedById = originalQuestion.CreatedById,
                        ModifiedById = originalQuestion.ModifiedById,
                        CourseId = originalQuestion.CourseId,
                        Choices = originalQuestion.Choices.Select(c => new QuestionChoice
                        {
                            Id = c.Id,
                            QuestionId = topicQuestionEntity.Id,
                            Text = c.Text,
                            GradePercent = c.GradePercent,
                            Feedback = c.Feedback                              // ✅ From Question Bank
                        }).ToList()
                    };
                }
                else
                {
                    // Fallback to TopicQuizQuestion data if no match found
                    fullQuestion = new Question
                    {
                        Id = topicQuestionEntity.Id,
                        QuestionName = topicQuestionEntity.QuestionName,
                        QuestionText = topicQuestionEntity.QuestionText,
                        Status = null,
                        Type = topicQuestionEntity.Type,
                        DefaultMark = topicQuestionEntity.DefaultMark,
                        Usage = 0,
                        FeedbackOfTrue = topicQuestionEntity.FeedbackOfTrue,
                        FeedbackOfFalse = topicQuestionEntity.FeedbackOfFalse,
                        CorrectAnswer = topicQuestionEntity.CorrectAnswer ?? false,
                        Multiple = topicQuestionEntity.Multiple ?? false,
                        CreatedById = Guid.Empty,
                        ModifiedById = null,
                        CourseId = "",
                        Choices = topicQuestionEntity.Choices.Select(c => new QuestionChoice
                        {
                            Id = c.Id,
                            QuestionId = topicQuestionEntity.Id,
                            Text = c.Text,
                            GradePercent = c.GradePercent,
                            Feedback = c.Feedback
                        }).ToList()
                    };
                }

                entity.Answers.Add(new QuizResponseAnswer
                {
                    Id = Guid.NewGuid(),
                    QuizResponseId = entity.Id,
                    Question = JsonSerializer.Serialize(fullQuestion),
                    Answer = a.Answer,
                    Mark = a.Mark
                });
            }

            await _unitOfWork.QuizResponses.AddAsync(entity);
            await _unitOfWork.CommitAsync();

            return ToDto(entity);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if quizResponse not found: +1
        // - if TopicQuizQuestion not found during update: +1
        // - if old answers exist → DeleteRangeAsync: +1
        // - foreach new answer → add item: +1
        // - DbUpdateException when CommitAsync: +1
        // D = 5 => Minimum Test Cases = D + 1 = 6
        public async Task<QuizResponseDTO> UpdateQuizResponseByIdAsync(Guid id, QuizResponseRequest dto, CancellationToken ct = default)
        {
            var entity = await _unitOfWork.QuizResponses.GetByIdTrackedWithAnswersAsync(id,ct);
            if (entity == null)
                throw new Exception("Quiz response not found");

            entity.Status = dto.Data.Status;
            entity.StartedAt = dto.Data.StartedAt;
            entity.CompletedAt = dto.Data.CompletedAt;

            await _unitOfWork.QuizResponseAnswers.DeleteRangeAsync(entity.Answers);
            entity.Answers.Clear();

            foreach (var a in dto.Data.Answers)
            {
                var questionEntity = await _unitOfWork.TopicQuizQuestions.GetByIdAsync(a.TopicQuizQuestionId)
                    ?? throw new Exception($"TopicQuizQuestion {a.TopicQuizQuestionId} not found");

                var fullQuestion = new Question
                {
                    Id = questionEntity.Id,
                    QuestionName = questionEntity.QuestionName,
                    QuestionText = questionEntity.QuestionText,
                    Status = null,
                    Type = questionEntity.Type,
                    DefaultMark = questionEntity.DefaultMark,
                    Usage = 0,
                    FeedbackOfTrue = questionEntity.FeedbackOfTrue,
                    FeedbackOfFalse = questionEntity.FeedbackOfFalse,
                    CorrectAnswer = questionEntity.CorrectAnswer ?? false,
                    Multiple = questionEntity.Multiple ?? false,
                    CreatedById = Guid.Empty,
                    ModifiedById = null,
                    CourseId = "",
                    Choices = questionEntity.Choices.Select(c => new QuestionChoice
                    {
                        Id = c.Id,
                        QuestionId = questionEntity.Id,
                        Text = c.Text,
                        GradePercent = c.GradePercent,
                        Feedback = c.Feedback
                    }).ToList()
                };

                entity.Answers.Add(new QuizResponseAnswer
                {
                    Id = Guid.NewGuid(),
                    QuizResponseId = entity.Id,
                    Question = JsonSerializer.Serialize(fullQuestion),
                    Answer = a.Answer,
                    Mark = a.Mark
                });
            }

            await _unitOfWork.CommitAsync();

            return ToDto(entity);
        }

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