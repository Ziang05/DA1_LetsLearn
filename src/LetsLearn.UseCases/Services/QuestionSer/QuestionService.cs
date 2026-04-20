using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services.QuestionSer
{
    public class QuestionService : IQuestionService
    {
        private readonly IUnitOfWork _uow;

        public QuestionService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if Choices provided (req.Choices Count > 0): +1
        // - if choicesToAdd.Count > 0: +1
        // - Null-coalesce throw on reload created question: +1
        // D = 3 => Minimum Test Cases = D + 1 = 4
        public async Task<GetQuestionResponse> CreateAsync(CreateQuestionRequest req, Guid userId, CancellationToken ct = default)
        {
            var courseId = req.CourseId ?? req.Course?.Id ?? string.Empty;
            var question = new Question
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DeletedAt = null,

                QuestionName = req.QuestionName,
                QuestionText = req.QuestionText,
                Status = req.Status,
                Type = req.Type,
                DefaultMark = (decimal?)req.DefaultMark,
                Usage = req.Usage,
                FeedbackOfTrue = req.FeedbackOfTrue,
                FeedbackOfFalse = req.FeedbackOfFalse,
                CorrectAnswer = req.CorrectAnswer ?? false,
                Multiple = req.Multiple,

                CreatedById = userId,
                ModifiedById = userId,

                CourseId = courseId
            };

            await _uow.Questions.AddAsync(question);

            if (req.Choices is { Count: > 0 })
            {
                var choicesToAdd = req.Choices.Select(c => new QuestionChoice
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    Text = c.Text,
                    Feedback = c.Feedback,
                    GradePercent = c.GradePercent
                }).ToList();

                    if (choicesToAdd.Count > 0)
                    {
                        await _uow.QuestionChoices.AddRangeAsync(choicesToAdd);
                        try
                        {
                            await _uow.CommitAsync();
                        }
                        catch (DbUpdateException ex)
                        {
                            throw new InvalidOperationException("Failed to save question choices.", ex);
                        }
                    }
            }

            try
            {
                await _uow.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to create question.", ex);
            }

            var created = await _uow.Questions.GetWithChoicesAsync(question.Id, ct)
                          ?? throw new InvalidOperationException("Failed to reload created question.");
            return MapToResponse(created);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Null-coalesce throw when question not found: +1
        // - if CorrectAnswer.HasValue: +1
        // - if CourseId provided: +1
        // - if Choices provided (req.Choices Count > 0): +1
        // - if toAdd.Count > 0: +1
        // - Null-coalesce throw on reload updated question: +1
        // D = 6 => Minimum Test Cases = D + 1 = 7
        public async Task<GetQuestionResponse> UpdateAsync(UpdateQuestionRequest req, Guid userId, CancellationToken ct = default)
        {

            var question = await _uow.Questions.GetWithChoicesAsync(req.Id, ct);
            if (question == null)
            {
                throw new KeyNotFoundException("Question not found.");
            }

           
            // Store original values for logging
            var originalCourseId = question.CourseId;
            var originalDeletedAt = question.DeletedAt;
            var originalChoiceCount = question.Choices?.Count ?? 0;

            question.QuestionName = req.QuestionName;
            question.QuestionText = req.QuestionText;
            question.Status = req.Status;
            question.Type = req.Type;
            question.DefaultMark = (decimal?)req.DefaultMark;
            question.Usage = req.Usage;
            question.FeedbackOfTrue = req.FeedbackOfTrue;
            question.FeedbackOfFalse = req.FeedbackOfFalse;
            if (req.CorrectAnswer.HasValue) question.CorrectAnswer = req.CorrectAnswer.Value;
            if (!string.IsNullOrWhiteSpace(req.CourseId)) question.CourseId = req.CourseId;
            question.ModifiedById = userId;
            question.UpdatedAt = DateTime.UtcNow;
            // Make sure DeletedAt remains null
            question.DeletedAt = null;

            
            // Handle choices updates
            if (req.Choices is { Count: > 0 })
            {
              
                var existingById = question.Choices.ToDictionary(x => x.Id, x => x);
                var incomingIds = req.Choices.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();

               
                // Mark choices for deletion (don't delete immediately)
                var choicesToRemove = question.Choices.Where(c => !incomingIds.Contains(c.Id)).ToList();
                if (choicesToRemove.Any())
                {
                   
                    await _uow.QuestionChoices.DeleteRangeAsync(choicesToRemove);
                }

                // Update existing choices
                var choicesToUpdate = req.Choices.Where(x => x.Id.HasValue && existingById.ContainsKey(x.Id.Value)).ToList();
                if (choicesToUpdate.Any())
                {
                   
                    foreach (var c in choicesToUpdate)
                    {
                        var ex = existingById[c.Id!.Value];
                        ex.Text = c.Text;
                        ex.Feedback = c.Feedback;
                        ex.GradePercent = c.GradePercent;
                    }
                }

                // Add new choices
                var toAdd = req.Choices
                    .Where(x => !x.Id.HasValue || !existingById.ContainsKey(x.Id.Value))
                    .Select(x => new QuestionChoice
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = question.Id,
                        Text = x.Text,
                        Feedback = x.Feedback,
                        GradePercent = x.GradePercent
                    })
                    .ToList();

                if (toAdd.Count > 0)
                {
                    await _uow.QuestionChoices.AddRangeAsync(toAdd);
                }
            }
            else
            {
                // If no choices provided, remove all existing choices
                if (question.Choices.Any())
                {

                    await _uow.QuestionChoices.DeleteRangeAsync(question.Choices);
                }
            }

            try
            {
                await _uow.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to update question.", ex);
            }

            // Verify the question is still findable
            var updated = await _uow.Questions.GetWithChoicesAsync(req.Id, ct);

            if (updated == null)
            {
                // Try to find the question without the DeletedAt filter to see if it exists but is marked deleted
                var questionInDb = await _uow.Questions.FirstOrDefaultAsync(q => q.Id == req.Id, ct);

                throw new InvalidOperationException("Failed to reload updated question.");
            }

          
            return MapToResponse(updated);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Null-coalesce throw when question not found: +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task<GetQuestionResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var q = await _uow.Questions.GetWithChoicesAsync(id, ct)
                    ?? throw new KeyNotFoundException("Question not found.");
            return MapToResponse(q);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - No branching here: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task<IEnumerable<GetQuestionResponse>> GetByCourseIdAsync(string courseId, CancellationToken ct = default)
        {
            var questions = await _uow.Questions.GetAllByCourseIdAsync(courseId, ct);
            return questions.Select(MapToResponse).ToList();
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Pure mapping, no branching: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1 (basic happy-path mapping)
        private static GetQuestionResponse MapToResponse(Question q)
        {
            return new GetQuestionResponse
            {
                Id = q.Id,
                CourseId = q.CourseId,
                QuestionName = q.QuestionName,
                QuestionText = q.QuestionText,
                Status = q.Status,
                Type = q.Type,
                DefaultMark = (double?)q.DefaultMark,
                Usage = q.Usage,
                FeedbackOfTrue = q.FeedbackOfTrue,
                FeedbackOfFalse = q.FeedbackOfFalse,
                CorrectAnswer = q.CorrectAnswer,
                Multiple = q.Multiple,
                CreatedById = q.CreatedById,
                ModifiedById = q.ModifiedById,
                CreatedAt = q.CreatedAt,
                UpdatedAt = q.UpdatedAt,
                Choices = q.Choices?.Select(c => new GetQuestionChoiceResponse
                {
                    Id = c.Id,
                    QuestionId = c.QuestionId,
                    Text = c.Text,
                    Feedback = c.Feedback,
                    GradePercent = c.GradePercent
                }).ToList()
            };
        }

        // Test Case Estimation:
        // Decision points (D):
        // - foreach loop": +1
        // - null-coalescing cho Choices (req.Choices?.Select... ?? new List<QuestionChoice>()): +1
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public async Task<int> BulkCreateAsync(List<CreateQuestionRequest> requests, Guid userId, CancellationToken ct = default)
        {
            var questionsToAdd = new List<Question>();

            // Group by CourseId to fetch existing questions efficiently
            var requestsByCourse = requests.GroupBy(r => r.CourseId ?? string.Empty);

            foreach (var group in requestsByCourse)
            {
                var courseId = group.Key;
                
                // Get existing questions to check for duplicates
                // Using a HashSet for faster lookup of QuestionText
                var existingQuestions = await _uow.Questions.GetAllByCourseIdAsync(courseId, ct);
                var existingTexts = existingQuestions
                    .Select(q => q.QuestionText?.Trim().ToLower() ?? string.Empty)
                    .ToHashSet();

                foreach (var req in group)
                {
                    // Skip if duplicate found (case-insensitive check)
                    var reqTextNormalized = req.QuestionText?.Trim().ToLower() ?? string.Empty;
                    if (string.IsNullOrEmpty(reqTextNormalized) || existingTexts.Contains(reqTextNormalized))
                    {
                        continue;
                    }

                    var questionId = Guid.NewGuid();
                    var question = new Question
                    {
                        Id = questionId,
                        QuestionName = req.QuestionName,
                        QuestionText = req.QuestionText,
                        Type = req.Type,
                        CourseId = req.CourseId ?? string.Empty,
                        CreatedById = userId,
                        CreatedAt = DateTime.UtcNow,
                        Status = "active",
                        Multiple = req.Type == "choice" ? true : req.Multiple,
                        DefaultMark = (req.DefaultMark == null || req.DefaultMark == 0) ? 1 : (decimal)req.DefaultMark,
                        Choices = req.Choices?.Select(c => new QuestionChoice
                        {
                            Id = Guid.NewGuid(),
                            QuestionId = questionId, 
                            Text = c.Text,
                            GradePercent = c.GradePercent,
                            Feedback = c.Feedback
                        }).ToList() ?? new List<QuestionChoice>()
                    };
                    questionsToAdd.Add(question);
                    
                    // Add to the set so we also detect duplicates within the upload batch
                    existingTexts.Add(reqTextNormalized);
                }
            }

            if (questionsToAdd.Count > 0)
            {
                await _uow.Questions.AddRangeAsync(questionsToAdd);
                await _uow.CommitAsync();
            }

            // Return the count of questions added, not the database rows affected
            return questionsToAdd.Count;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Check response (!response.IsSuccessStatusCode): +1
        // - Deserialize JSON (parsedQuestions == null): +1
        // - BulkCreateAsync: (D has been calculated separately)
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public async Task<int> ImportBulkQuestionsAsync(IFormFile file, string courseId, Guid userId, CancellationToken ct)
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(3);
            using var multipartContent = new MultipartFormDataContent();
            using var fileStream = file.OpenReadStream();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            multipartContent.Add(fileContent, "data", file.FileName);

            var response = await httpClient.PostAsync("https://plastics-discover-message-judicial.trycloudflare.com/webhook/parse-gemini", multipartContent);
            if (!response.IsSuccessStatusCode) throw new Exception("Fail API Parser call.");

            var jsonResult = await response.Content.ReadAsStringAsync();
            var parsedQuestions = JsonConvert.DeserializeObject<List<ListQuestionParserResponse>>(jsonResult);

            var requests = parsedQuestions.Select(item => new CreateQuestionRequest
            {
                CourseId = courseId,
                QuestionName = item.QuestionName ?? $"Q_{Guid.NewGuid().ToString().Substring(0, 8)}",
                QuestionText = item.QuestionText,
                Type = item.Type switch
                {
                    "Multiple Choice" => "Choices Answer",
                    "Choice" => "Choices Answer",
                    _ => item.Type
                },
                DefaultMark = item.DefaultMark,
                CorrectAnswer = item.CorrectAnswer,
                Multiple = item.Multiple,
                Choices = item.Choices?.Select(c => new CreateQuestionChoiceRequest
                {
                    Text = c.Text,
                    GradePercent = c.GradePercent
                }).ToList()
            }).ToList();

            return await BulkCreateAsync(requests, userId, ct);
        }
    }
}
