using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.Services.QuizResponseService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services
{
    public class TopicService : ITopicService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TopicService> _logger;
        public TopicService(IUnitOfWork unitOfWork, ILogger<TopicService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if request == null: +1
        // - switch(request.Type):
        //      case "page": +1
        //      case "file": +1
        //      case "link": +1
        //      case "assignment": +1
        //      case "quiz": +1
        //      case "meeting": +1
        //      default (unsupported type): +1
        // - commit fails (DbUpdateException): +1
        // D = 9 => Minimum Test Cases = 10
        public async Task<TopicResponse> CreateTopicAsync(CreateTopicRequest request, CancellationToken ct = default)
        {
            try
            {
                if (request == null)
                    throw new ArgumentNullException(nameof(request));

                _logger.LogInformation("Creating topic of type {Type} for section {SectionId}", request.Type, request.SectionId);

                // Bước 1: Tạo Topic cha
                var topic = new Topic
                {
                    Id = Guid.NewGuid(),
                    Title = request.Title,
                    Type = request.Type,
                    SectionId = request.SectionId
                };

                await _unitOfWork.Topics.AddAsync(topic);

                object? topicData = null;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var raw = request.Data ?? "null";
                // Bước 2: Deserialize Data và xử lý theo Type
                switch (request.Type.ToLower())
                {
                    case "page":
                        {
                            var pageReq = JsonSerializer.Deserialize<CreateTopicPageRequest>(raw, options);

                            var page = new TopicPage
                            {
                                TopicId = topic.Id,
                                Description = pageReq?.Description,
                                Content = pageReq?.Content
                            };

                            await _unitOfWork.TopicPages.AddAsync(page);
                            topicData = page;
                            break;
                        }

                    case "file":
                        {
                            var fileReq = JsonSerializer.Deserialize<CreateTopicFileRequest>(raw, options);

                            var file = new TopicFile
                            {
                                TopicId = topic.Id,
                                Description = fileReq?.Description,
                                File = null  // Will be set below if fileReq.File exists
                            };

                            // Handle the file data if provided
                            if (fileReq?.File != null)
                            {
                                var cloudinaryFile = new CloudinaryFile
                                {
                                    Id = fileReq.File.Id ?? Guid.NewGuid(),
                                    Name = fileReq.File.Name,
                                    DisplayUrl = fileReq.File.DisplayUrl,
                                    DownloadUrl = fileReq.File.DownloadUrl,
                                    TopicFileId = topic.Id  // Set the relationship
                                };

                                file.File = cloudinaryFile;  // Set single file
                                await _unitOfWork.CloudinaryFiles.AddAsync(cloudinaryFile);
                            }

                            await _unitOfWork.TopicFiles.AddAsync(file);
                            topicData = file;
                            break;
                        }

                    case "link":
                        {
                            var linkReq = JsonSerializer.Deserialize<CreateTopicLinkRequest>(raw, options);

                            var link = new TopicLink
                            {
                                TopicId = topic.Id,
                                Description = linkReq?.Description,
                                Url = linkReq?.Url
                            };

                            await _unitOfWork.TopicLinks.AddAsync(link);
                            topicData = link;
                            break;
                        }

                    case "assignment":
                        {
                            var assignReq = JsonSerializer.Deserialize<CreateTopicAssignmentRequest>(raw, options);

                            var assignment = new TopicAssignment
                            {
                                TopicId = topic.Id,
                                Description = assignReq?.Description,
                                Open = assignReq?.Open,
                                Close = assignReq?.Close,
                                MaximumFile = assignReq?.MaximumFile,
                                MaximumFileSize = assignReq?.MaximumFileSize,
                                RemindToGrade = assignReq?.RemindToGrade,
                                Files = new List<CloudinaryFile>()
                            };

                            // Handle files if provided
                            if (assignReq?.CloudinaryFiles != null && assignReq.CloudinaryFiles.Any())
                            {
                                foreach (var fileData in assignReq.CloudinaryFiles)
                                {
                                    var cloudinaryFile = new CloudinaryFile
                                    {
                                        Id = fileData.Id ?? Guid.NewGuid(),
                                        Name = fileData.Name,
                                        DisplayUrl = fileData.DisplayUrl,
                                        DownloadUrl = fileData.DownloadUrl,
                                        TopicAssignmentId = topic.Id
                                    };

                                    assignment.Files.Add(cloudinaryFile);
                                    await _unitOfWork.CloudinaryFiles.AddAsync(cloudinaryFile);
                                }
                            }

                            await _unitOfWork.TopicAssignments.AddAsync(assignment);
                            topicData = assignment;
                            break;
                        }

                    case "quiz":
                        {
                            var quizReq = JsonSerializer.Deserialize<CreateTopicQuizRequest>(raw, options);

                            var quiz = new TopicQuiz
                            {
                                TopicId = topic.Id,
                                Description = quizReq?.Description,
                                Open = quizReq?.Open,
                                Close = quizReq?.Close,
                                TimeLimit = quizReq?.TimeLimit,
                                TimeLimitUnit = quizReq?.TimeLimitUnit,
                                GradeToPass = quizReq?.GradeToPass,
                                GradingMethod = quizReq?.GradingMethod,
                                AttemptAllowed = quizReq?.AttemptAllowed,
                                Questions = quizReq?.Questions?.Select(q => new TopicQuizQuestion
                                {
                                    QuestionName = q.QuestionName,
                                    QuestionText = q.QuestionText,
                                    Type = q.Type,
                                    DefaultMark = q.DefaultMark,
                                    FeedbackOfTrue = q.FeedbackOfTrue,
                                    FeedbackOfFalse = q.FeedbackOfFalse,
                                    CorrectAnswer = q.CorrectAnswer,
                                    Multiple = q.Multiple,
                                    Choices = q.Choices.Select(c => new TopicQuizQuestionChoice
                                    {
                                        Text = c.Text,
                                        GradePercent = c.GradePercent,
                                        Feedback = c.Feedback
                                    }).ToList() ?? new List<TopicQuizQuestionChoice>()
                                }).ToList() ?? new List<TopicQuizQuestion>()
                            };

                            await _unitOfWork.TopicQuizzes.AddAsync(quiz);
                            topicData = quiz;
                            break;
                        }

                    case "meeting":
                        {
                            var meetingReq = JsonSerializer.Deserialize<CreateTopicMeetingRequest>(raw, options);
                            var meeting = new TopicMeeting
                            {
                                TopicId = topic.Id,
                                Description = meetingReq?.Description,
                                Open = meetingReq?.Open,
                                Close = meetingReq?.Close
                            };
                            await _unitOfWork.TopicMeetings.AddAsync(meeting);
                            topicData = meeting;
                            break;
                        }

                    default:
                        _logger.LogWarning("Unsupported topic type: {Type}", request.Type);
                        throw new NotSupportedException($"Unsupported topic type: {request.Type}");
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully created topic {TopicId} ({Type})", topic.Id, request.Type);

                return new TopicResponse
                {
                    Id = topic.Id,
                    Title = topic.Title,
                    Type = topic.Type,
                    SectionId = topic.SectionId,
                    Data = topicData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating topic of type {Type}", request?.Type);
                throw;
            }
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if request == null: +1
        // - if topic not found: +1
        // - switch(request.Type):
        //      page exists: +1
        //      file exists: +1
        //      link exists: +1
        //      assignment exists: +1
        //      quiz exists: +1
        //      meeting exists: +1
        //      default (unsupported type): +1
        // - commit fails: +1
        // - choices update logic (new choice or update or remove): +1
        // D = 11 => Minimum Test Cases = 12
        public async Task<TopicResponse> UpdateTopicAsync(UpdateTopicRequest request, CancellationToken ct = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Updating topic {TopicId}", request.Id);

            var topic = await _unitOfWork.Topics.GetByIdAsync(request.Id!.Value);
            if (topic == null)
            {
                _logger.LogWarning("Topic {TopicId} not found.", request.Id);
                throw new KeyNotFoundException("Topic not found.");
            }

            // Cập nhật thông tin chung
            topic.Title = request.Title ?? topic.Title;
            await _unitOfWork.Topics.UpdateAsync(topic);

            object? topicData = null;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var raw = request.Data ?? "null";
            // Chuyển đổi dựa vào type do người dùng truyền
            switch (request.Type?.ToLower())
            {
                case "page":
                    {
                        var pageReq = JsonSerializer.Deserialize<UpdateTopicPageRequest>(raw, options);
                        var page = (await _unitOfWork.TopicPages.FindAsync(p => p.TopicId == topic.Id)).FirstOrDefault()
                                   ?? throw new KeyNotFoundException("TopicPage not found.");
                        page.Description = pageReq.Description ?? page.Description;
                        page.Content = pageReq.Content ?? page.Content;
                        await _unitOfWork.TopicPages.UpdateAsync(page);
                        topicData = page;
                        break;
                    }

                case "file":
                    {
                        _logger.LogInformation("Processing file update. Raw data: {RawData}", raw);

                        var fileReq = JsonSerializer.Deserialize<UpdateTopicFileRequest>(raw, options);

                        _logger.LogInformation("Deserialized file request - Description: {Description}, File: {HasFile}",
                            fileReq?.Description, fileReq?.File != null);

                        var file = (await _unitOfWork.TopicFiles.FindAsync(f => f.TopicId == topic.Id, ct)).FirstOrDefault()
                                   ?? throw new KeyNotFoundException("TopicFile not found.");

                        file.Description = fileReq?.Description ?? file.Description;

                        // Update file data
                        if (fileReq?.File != null)
                        {
                            _logger.LogInformation("Updating file data - ID: {FileId}, Name: {FileName}",
                                fileReq.File.Id, fileReq.File.Name);

                            // Remove existing file for this topic (if any)
                            var existingFiles = await _unitOfWork.CloudinaryFiles.FindAsync(cf => cf.TopicFileId == topic.Id, ct);
                            if (existingFiles.Any())
                            {
                                await _unitOfWork.CloudinaryFiles.DeleteRangeAsync(existingFiles);
                            }

                            // Add new file
                            var cloudinaryFile = new CloudinaryFile
                            {
                                Id = fileReq.File.Id ?? Guid.NewGuid(),
                                Name = fileReq.File.Name,
                                DisplayUrl = fileReq.File.DisplayUrl,
                                DownloadUrl = fileReq.File.DownloadUrl,
                                TopicFileId = topic.Id
                            };

                            file.File = cloudinaryFile;  // Set single file
                            await _unitOfWork.CloudinaryFiles.AddAsync(cloudinaryFile);

                            _logger.LogInformation("Added new CloudinaryFile with ID: {CloudinaryFileId}", cloudinaryFile.Id);
                        }

                        await _unitOfWork.TopicFiles.UpdateAsync(file);
                        topicData = file;

                        _logger.LogInformation("File update completed successfully for TopicFile ID: {TopicId}", topic.Id);
                        break;
                    }

                case "link":
                    {
                        var linkReq = JsonSerializer.Deserialize<UpdateTopicLinkRequest>(raw, options);
                        var link = (await _unitOfWork.TopicLinks.FindAsync(l => l.TopicId == topic.Id, ct)).FirstOrDefault()
                                   ?? throw new KeyNotFoundException("TopicLink not found.");
                        link.Description = linkReq.Description ?? link.Description;
                        link.Url = linkReq.Url ?? link.Url;
                        await _unitOfWork.TopicLinks.UpdateAsync(link);
                        topicData = link;
                        break;
                    }

                case "assignment":
                    {
                        var assignmentReq = JsonSerializer.Deserialize<UpdateTopicAssignmentRequest>(raw, options);
                        var assignment = (await _unitOfWork.TopicAssignments.FindAsync(a => a.TopicId == topic.Id, ct)).FirstOrDefault()
                                         ?? throw new KeyNotFoundException("TopicAssignment not found.");

                        assignment.Description = assignmentReq.Description ?? assignment.Description;
                        assignment.Open = assignmentReq.Open ?? assignment.Open;
                        assignment.Close = assignmentReq.Close ?? assignment.Close;
                        assignment.MaximumFile = assignmentReq.MaximumFile ?? assignment.MaximumFile;
                        assignment.MaximumFileSize = assignmentReq.MaximumFileSize ?? assignment.MaximumFileSize;
                        assignment.RemindToGrade = assignmentReq.RemindToGrade ?? assignment.RemindToGrade;

                        // Update files
                        if (assignmentReq.CloudinaryFiles != null)
                        {
                            // Remove existing files
                            var existingFiles = await _unitOfWork.CloudinaryFiles.FindAsync(cf => cf.TopicAssignmentId == topic.Id, ct);
                            if (existingFiles.Any())
                            {
                                assignment.Files.Clear();
                                await _unitOfWork.CloudinaryFiles.DeleteRangeAsync(existingFiles);
                            }

                            // Add new files
                            foreach (var fileData in assignmentReq.CloudinaryFiles)
                            {
                                var cloudinaryFile = new CloudinaryFile
                                {
                                    Id = fileData.Id ?? Guid.NewGuid(),
                                    Name = fileData.Name,
                                    DisplayUrl = fileData.DisplayUrl,
                                    DownloadUrl = fileData.DownloadUrl,
                                    TopicAssignmentId = topic.Id
                                };

                                assignment.Files.Add(cloudinaryFile);
                                await _unitOfWork.CloudinaryFiles.AddAsync(cloudinaryFile);
                            }
                        }

                        await _unitOfWork.TopicAssignments.UpdateAsync(assignment);
                        topicData = assignment;
                        break;
                    }

                case "quiz":
                    {
                        // LOG: Raw data received
                        _logger.LogInformation("Raw quiz data received: {RawData}", raw);

                        var quizReq = JsonSerializer.Deserialize<UpdateTopicQuizRequest>(raw, options);

                        // LOG: Deserialized quiz request
                        _logger.LogInformation("Deserialized quiz request - QuestionCount: {QuestionCount}",
                            quizReq?.Questions?.Count ?? 0);

                        var quiz = await _unitOfWork.TopicQuizzes.GetWithQuestionsAsync(topic.Id)
                                   ?? throw new KeyNotFoundException("TopicQuiz not found.");

                        quiz.Description = quizReq.Description ?? quiz.Description;
                        quiz.Open = quizReq.Open ?? quiz.Open;
                        quiz.Close = quizReq.Close ?? quiz.Close;
                        quiz.TimeLimit = quizReq.TimeLimit ?? quiz.TimeLimit;
                        quiz.TimeLimitUnit = quizReq.TimeLimitUnit ?? quiz.TimeLimitUnit;
                        quiz.GradeToPass = quizReq.GradeToPass ?? quiz.GradeToPass;
                        quiz.GradingMethod = quizReq.GradingMethod ?? quiz.GradingMethod;
                        quiz.AttemptAllowed = quizReq.AttemptAllowed ?? quiz.AttemptAllowed;

                        // 1. Build dictionary cho update nhanh
                        var existingQuestions = quiz.Questions.ToDictionary(q => q.Id);

                        // Danh sách Id mới từ request
                        var incomingIds = quizReq.Questions.Where(q => q.Id != null)
                                       .Select(q => q.Id!.Value)
                                       .ToHashSet();

                        // LOG: Existing vs incoming questions
                        _logger.LogInformation("Existing questions: {ExistingCount}, Incoming questions: {IncomingCount}",
                            existingQuestions.Count, quizReq.Questions.Count);

                        // 3. Xử lý từng câu hỏi trong request
                        foreach (var q in quizReq.Questions)
                        {
                            // LOG: Processing each question
                            _logger.LogInformation("Processing question ID: {QuestionId}, Name: '{QuestionName}', Type: '{Type}'",
                                q.Id, q.QuestionName, q.Type);

                            // LOG: Feedback data for each question
                            _logger.LogInformation("Question {QuestionId} - FeedbackOfTrue: '{FeedbackTrue}', FeedbackOfFalse: '{FeedbackFalse}'",
                                q.Id, q.FeedbackOfTrue, q.FeedbackOfFalse);

                            // LOG: Choices data
                            _logger.LogInformation("Question {QuestionId} - ChoiceCount: {ChoiceCount}",
                                q.Id, q.Choices?.Count ?? 0);

                            foreach (var choice in q.Choices ?? new List<UpdateTopicQuizQuestionChoiceRequest>())
                            {
                                _logger.LogInformation("Choice ID: {ChoiceId}, Text: '{Text}', Feedback: '{Feedback}', GradePercent: {GradePercent}",
                                    choice.Id, choice.Text, choice.Feedback, choice.GradePercent);
                            }

                            TopicQuizQuestion question;

                            // 3.1. Update existing question
                            if (q.Id != null && existingQuestions.TryGetValue(q.Id.Value, out question))
                            {
                                _logger.LogInformation("Updating existing question {QuestionId}", q.Id);

                                // LOG: Before update values
                                _logger.LogInformation("BEFORE UPDATE - Question {QuestionId}: FeedbackOfTrue='{OldFeedbackTrue}', FeedbackOfFalse='{OldFeedbackFalse}'",
                                    question.Id, question.FeedbackOfTrue, question.FeedbackOfFalse);

                                question.QuestionName = q.QuestionName;
                                question.QuestionText = q.QuestionText;
                                question.Type = q.Type;
                                question.DefaultMark = q.DefaultMark;
                                question.FeedbackOfTrue = q.FeedbackOfTrue;
                                question.FeedbackOfFalse = q.FeedbackOfFalse;
                                question.CorrectAnswer = q.CorrectAnswer;
                                question.Multiple = q.Multiple;

                                // LOG: After update values
                                _logger.LogInformation("AFTER UPDATE - Question {QuestionId}: FeedbackOfTrue='{NewFeedbackTrue}', FeedbackOfFalse='{NewFeedbackFalse}'",
                                    question.Id, question.FeedbackOfTrue, question.FeedbackOfFalse);

                                // Update choices
                                UpdateTopicQuizQuestionChoices(question, q);
                            }
                            else
                            {
                                _logger.LogInformation("Creating new question with ID: {QuestionId}", q.Id ?? Guid.Empty);

                                // 3.2. CREATE NEW question
                                question = new TopicQuizQuestion
                                {
                                    Id = Guid.NewGuid(),
                                    TopicQuizId = quiz.TopicId,
                                    QuestionName = q.QuestionName,
                                    QuestionText = q.QuestionText,
                                    Type = q.Type,
                                    DefaultMark = q.DefaultMark,
                                    FeedbackOfTrue = q.FeedbackOfTrue,
                                    FeedbackOfFalse = q.FeedbackOfFalse,
                                    CorrectAnswer = q.CorrectAnswer,
                                    Multiple = q.Multiple,
                                    Choices = new List<TopicQuizQuestionChoice>()
                                };

                                // LOG: New question feedback
                                _logger.LogInformation("NEW QUESTION {QuestionId}: FeedbackOfTrue='{FeedbackTrue}', FeedbackOfFalse='{FeedbackFalse}'",
                                    question.Id, question.FeedbackOfTrue, question.FeedbackOfFalse);

                                // Tạo choices
                                foreach (var c in q.Choices)
                                {
                                    var newChoice = new TopicQuizQuestionChoice
                                    {
                                        Id = Guid.NewGuid(),
                                        QuizQuestionId = question.Id,
                                        Text = c.Text,
                                        GradePercent = c.GradePercent,
                                        Feedback = c.Feedback
                                    };

                                    // LOG: New choice feedback
                                    _logger.LogInformation("NEW CHOICE {ChoiceId}: Text='{Text}', Feedback='{Feedback}'",
                                        newChoice.Id, newChoice.Text, newChoice.Feedback);

                                    question.Choices.Add(newChoice);
                                }

                                await _unitOfWork.TopicQuizQuestions.AddAsync(question);
                            }
                        }

                        await _unitOfWork.TopicQuizzes.UpdateAsync(quiz);
                        topicData = quiz;

                        // LOG: Final quiz data
                        _logger.LogInformation("Quiz update completed. Final question count: {QuestionCount}",
                            quiz.Questions.Count);

                        break;
                    }

                case "meeting":
                    {
                        var meetingReq = JsonSerializer.Deserialize<UpdateTopicMeetingRequest>(raw, options);

                        var meeting = (await _unitOfWork.TopicMeetings
                            .FindAsync(m => m.TopicId == topic.Id, ct))
                            .FirstOrDefault()
                            ?? throw new KeyNotFoundException("TopicMeeting not found.");

                        meeting.Description = meetingReq.Description ?? meeting.Description;
                        meeting.Open = meetingReq.Open ?? meeting.Open;
                        meeting.Close = meetingReq.Close ?? meeting.Close;

                        await _unitOfWork.TopicMeetings.UpdateAsync(meeting);
                        topicData = meeting;
                        break;
                    }

                default:
                    _logger.LogError("Unsupported topic type for update: {Type}", request.Type);
                    throw new NotSupportedException($"Unsupported topic type: {request.Type}");
            }

            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Successfully updated topic {TopicId}", topic.Id);

            return new TopicResponse
            {
                Id = topic.Id,
                Title = topic.Title,
                Type = topic.Type,
                SectionId = topic.SectionId,
                Data = topicData
            };
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if topic == null: +1
        // D = 1 => Minimum Test Cases = 2
        private void UpdateTopicQuizQuestionChoices(TopicQuizQuestion question, UpdateTopicQuizQuestionRequest q)
        {
            _logger.LogInformation("Updating choices for question {QuestionId}. Current choice count: {CurrentCount}, Incoming choice count: {IncomingCount}",
                question.Id, question.Choices.Count, q.Choices.Count);

            var existing = question.Choices.ToDictionary(c => c.Id);
            var incoming = q.Choices.Where(c => c.Id != null)
                                    .Select(c => c.Id!.Value)
                                    .ToHashSet();

            // LOG: Choice operations
            _logger.LogInformation("Existing choice IDs: [{ExistingIds}]",
                string.Join(", ", existing.Keys));
            _logger.LogInformation("Incoming choice IDs: [{IncomingIds}]",
                string.Join(", ", incoming));

            // Xóa choice cũ
            var removed = question.Choices.Where(c => !incoming.Contains(c.Id)).ToList();
            foreach (var r in removed)
            {
                _logger.LogInformation("Removing choice {ChoiceId}: '{Text}'", r.Id, r.Text);
                question.Choices.Remove(r);
            }

            // xử lý từng choice
            foreach (var c in q.Choices)
            {
                TopicQuizQuestionChoice choice;

                if (c.Id != null && existing.TryGetValue(c.Id.Value, out choice))
                {
                    // LOG: Before update choice
                    _logger.LogInformation("BEFORE UPDATE CHOICE {ChoiceId}: Text='{OldText}', Feedback='{OldFeedback}', GradePercent={OldGrade}",
                        choice.Id, choice.Text, choice.Feedback, choice.GradePercent);

                    // Update
                    choice.Text = c.Text;
                    choice.GradePercent = c.GradePercent;
                    choice.Feedback = c.Feedback;

                    // LOG: After update choice
                    _logger.LogInformation("AFTER UPDATE CHOICE {ChoiceId}: Text='{NewText}', Feedback='{NewFeedback}', GradePercent={NewGrade}",
                        choice.Id, choice.Text, choice.Feedback, choice.GradePercent);
                }
                else
                {
                    _logger.LogInformation("Adding new choice: Text='{Text}', Feedback='{Feedback}', GradePercent={GradePercent}",
                        c.Text, c.Feedback, c.GradePercent);

                    // Add new
                    var newChoice = new TopicQuizQuestionChoice
                    {
                        Id = Guid.NewGuid(),
                        QuizQuestionId = question.Id,
                        Text = c.Text,
                        GradePercent = c.GradePercent,
                        Feedback = c.Feedback
                    };

                    _logger.LogInformation("NEW CHOICE CREATED {ChoiceId}: Text='{Text}', Feedback='{Feedback}'",
                        newChoice.Id, newChoice.Text, newChoice.Feedback);

                    question.Choices.Add(newChoice);
                }
            }

            _logger.LogInformation("Choice update completed for question {QuestionId}. Final choice count: {FinalCount}",
                question.Id, question.Choices.Count);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if topic == null: +1
        // - quiz exists: +1
        // - assignment exists: +1
        // - meeting exists: +1
        // - page exists: +1
        // - file exists: +1
        // - link exists: +1
        // - default (unsupported type): +1
        // D = 8 => Minimum Test Cases = 9
        public async Task<bool> DeleteTopicAsync(Guid id, CancellationToken ct = default)
        {
            var topic = await _unitOfWork.Topics.GetByIdAsync(id);
            if (topic == null)
            {
                return false;
            }

            await _unitOfWork.Topics.DeleteAsync(topic);
            await _unitOfWork.CommitAsync();
            return true;
        }

        public async Task<bool> SaveMeetingHistoryAsync(Guid topicId, SaveMeetingHistoryRequest request, CancellationToken ct = default)
        {
            try 
            {
                var meeting = await _unitOfWork.TopicMeetings.FindAsync(m => m.TopicId == topicId, ct);
                if (!meeting.Any())
                {
                    _logger.LogWarning("Topic meeting not found for topic {TopicId}", topicId);
                    return false;
                }

                var history = new TopicMeetingHistory
                {
                    Id = Guid.NewGuid(),
                    TopicMeetingId = topicId,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    AttendeeCount = request.AttendeeCount,
                    AttendanceCsvUrl = request.AttendanceCsvUrl
                };

                await _unitOfWork.TopicMeetingHistories.AddAsync(history);
                await _unitOfWork.CommitAsync();
                
                _logger.LogInformation("Saved meeting history for topic {TopicId}", topicId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving meeting history for topic {TopicId}", topicId);
                throw;
            }
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if topic == null: +1
        // - quiz type: +1
        // - assignment type: +1
        // - meeting type: +1
        // - page type: +1
        // - file type: +1
        // - link type: +1
        // - default (unsupported type): +1
        // D = 8 => Minimum Test Cases = D + 1 = 9
        public async Task<TopicResponse> GetTopicByIdAsync(Guid id, CancellationToken ct = default)
        {
            try
            {
                var topic = await _unitOfWork.Topics.GetByIdAsync(id);
                if (topic == null)
                {
                    _logger.LogWarning("Topic {TopicId} not found.", id);
                    throw new KeyNotFoundException("Topic not found.");
                }

                _logger.LogInformation("Fetching topic {TopicId} of type {Type}", id, topic.Type);

                object? topicData = null;

                switch (topic.Type!.ToLower())
                {
                    case "quiz":
                        var quiz = await _unitOfWork.TopicQuizzes.GetWithQuestionsAsync(topic.Id);
                        if (quiz != null)
                            topicData = quiz;
                        break;

                    case "assignment":
                        var assignment = (await _unitOfWork.TopicAssignments.FindAsync(a => a.TopicId == topic.Id, ct)).FirstOrDefault();
                        if (assignment != null)
                        {
                            // Load associated files
                            var files = await _unitOfWork.CloudinaryFiles.FindAsync(cf => cf.TopicAssignmentId == topic.Id, ct);
                            assignment.Files = files.ToList();
                            topicData = assignment;
                        }
                        break;

                    case "file":
                        var file = (await _unitOfWork.TopicFiles.FindAsync(f => f.TopicId == topic.Id, ct)).FirstOrDefault();
                        if (file != null)
                        {
                            // Load the associated CloudinaryFile if it exists and not already loaded
                            if (file.File == null)
                            {
                                var cloudinaryFiles = await _unitOfWork.CloudinaryFiles.FindAsync(cf => cf.TopicFileId == topic.Id, ct);
                                file.File = cloudinaryFiles.FirstOrDefault();
                            }
                            topicData = file;
                        }
                        break;

                    case "link":
                        var link = (await _unitOfWork.TopicLinks.FindAsync(l => l.TopicId == topic.Id, ct)).FirstOrDefault();
                        if (link != null)
                            topicData = link;
                        break;

                    case "page":
                        var page = (await _unitOfWork.TopicPages.FindAsync(p => p.TopicId == topic.Id, ct)).FirstOrDefault();
                        if (page != null)
                            topicData = page;
                        break;

                    case "meeting":
                        var meeting = (await _unitOfWork.TopicMeetings.FindAsync(p => p.TopicId == topic.Id, ct)).FirstOrDefault();
                        if (meeting != null)
                        {
                            // Load history
                            var histories = await _unitOfWork.TopicMeetingHistories.FindAsync(h => h.TopicMeetingId == topic.Id, ct);
                            meeting.Histories = histories.OrderByDescending(h => h.StartTime).ToList();
                            topicData = meeting;
                        }
                        break;

                    default:
                        _logger.LogWarning("Invalid topic type {Type} for topic {TopicId}", topic.Type, id);
                        throw new NotSupportedException($"Invalid topic type: {topic.Type}");
                }

                _logger.LogInformation("Successfully fetched topic {TopicId}", id);

                return new TopicResponse
                {
                    Id = topic.Id,
                    Title = topic.Title,
                    Type = topic.Type,
                    SectionId = topic.SectionId,
                    Data = topicData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching topic {TopicId}", id);
                throw new Exception("Error fetching topic.", ex);
            }
        }

        public async Task<SingleQuizReportDTO> GetSingleQuizReportAsync(String courseId, Guid topicId, CancellationToken ct = default)
        {
            // Fetch Course
            var course = await _unitOfWork.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found");

            var creatorId = course.CreatorId;

            // Fetch Topic
            var topic = await _unitOfWork.Topics.GetByIdAsync(topicId, ct)
                ?? throw new KeyNotFoundException("Topic not found");

            var reportDTO = new SingleQuizReportDTO();

            // Fetch TopicQuiz and its questions
            var topicQuiz = await _unitOfWork.TopicQuizzes.GetWithQuestionsAsync(topicId)
                ?? throw new KeyNotFoundException("No topic quiz found");

            // Use topicQuiz.Questions to get the list of quiz questions
            var quizQuestions = topicQuiz.Questions;

            // Fetch quiz responses of students
            var quizResponses = await _unitOfWork.QuizResponses.FindAllByTopicIdWithAnswersAsync(topicId, ct);

            // Get students who participated in the quiz
            var topicEndTime = topicQuiz.Close ?? DateTime.MaxValue;

            var studentsThatTookPartIn = (await _unitOfWork.Enrollments.GetAllByCourseIdAsync(courseId, ct))
                                            .Where(e => e.StudentId != creatorId)
                                            .ToList(); ;

            int studentCount = studentsThatTookPartIn.Count;

            // Create a dictionary for all students with default mark 0 for those who didn't participate
            var marksWithStudentId = studentsThatTookPartIn.ToDictionary(
                student => student.StudentId,
                student =>
                {
                    // Check if the student has a quiz response, if not set mark as 0
                    var quizResponse = quizResponses.FirstOrDefault(qr => qr.StudentId == student.StudentId);
                    if (quizResponse == null)
                    {
                        return 0.0; // Student did not participate in the quiz
                    }

                    // If the student has a response, calculate their mark based on their answers
                    return (double)quizResponse.Answers.Average(answer =>
                    {
                        var question = JsonSerializer.Deserialize<Question>(answer.Question);
                        return (answer.Mark / question!.DefaultMark ?? 1) * 10;
                    });
                });

            // Apply the grading method to calculate final marks for each student
            var finalMarksWithStudentId = marksWithStudentId.ToDictionary(
                entry => entry.Key,
                entry => CalculateMark(new List<double> { entry.Value }, topicQuiz.GradingMethod!) // Apply grading method
            );

            // Calculate average, max, and min marks
            double avgMark = marksWithStudentId.Values.Average();
            double maxMark = marksWithStudentId.Values.Max();
            double minMark = marksWithStudentId.Values.Min();

            // Categorize students by marks
            var studentInfoAndMarks = await GetStudentInfoWithMarkAndResponseIdForQuiz(studentsThatTookPartIn, finalMarksWithStudentId, ct);

            reportDTO.StudentWithMark = studentInfoAndMarks;
            reportDTO.StudentWithMarkOver8 = studentInfoAndMarks.Where(info => info.Mark >= 8).ToList();
            reportDTO.StudentWithMarkOver5 = studentInfoAndMarks.Where(info => info.Mark >= 5 && info.Mark < 8).ToList();
            reportDTO.StudentWithMarkOver2 = studentInfoAndMarks.Where(info => info.Mark >= 2 && info.Mark < 5).ToList();
            reportDTO.StudentWithMarkOver0 = studentInfoAndMarks.Where(info => info.Mark < 2).ToList();
            reportDTO.StudentWithNoResponse = studentInfoAndMarks.Where(info => !info.Submitted).ToList();

            var quizResponseDTOs = await MapQuizResponsesToDTO(quizResponses, ct);

            // Calculate completion rate excluding students who did not participate
            var studentsWithResponse = studentInfoAndMarks.Count(info => info.Submitted); // Count only students who submitted their response
            double completionRate = studentCount > 0 ? (double)studentsWithResponse / studentCount : 0;

            // Update additional metrics
            reportDTO.MarkDistributionCount = CalculateMarkDistribution(marksWithStudentId, studentCount);
            reportDTO.QuestionCount = quizQuestions.Count;
            reportDTO.MaxDefaultMark = quizQuestions.Sum(q => (double)q.DefaultMark!);
            reportDTO.AvgStudentMarkBase10 = avgMark;
            reportDTO.MaxStudentMarkBase10 = maxMark;
            reportDTO.MinStudentMarkBase10 = minMark;
            reportDTO.AttemptCount = quizResponses.Count;
            reportDTO.AvgTimeSpend = CalculateAvgTimeSpend(quizResponseDTOs);
            reportDTO.CompletionRate = completionRate;
            reportDTO.TrueFalseQuestionCount = quizQuestions.Count(q => q.Type == "True/False");
            reportDTO.MultipleChoiceQuestionCount = quizQuestions.Count(q => q.Type == "Multiple Choice");
            reportDTO.ShortAnswerQuestionCount = quizQuestions.Count(q => q.Type == "Short Answer");

            return reportDTO;
        }

        public async Task<SingleAssignmentReportDTO> GetSingleAssignmentReportAsync(String courseId, Guid topicId, CancellationToken ct = default)
        {
            // Fetch Course
            var course = await _unitOfWork.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found");

            var creatorId = course.CreatorId;

            // Fetch Topic
            var topic = await _unitOfWork.Topics.GetByIdAsync(topicId, ct)
                ?? throw new KeyNotFoundException("Topic not found");

            var reportDTO = new SingleAssignmentReportDTO();

            // Fetch TopicAssignment
            var topicAssignment = await _unitOfWork.TopicAssignments.GetByIdAsync(topicId, ct)
                ?? throw new KeyNotFoundException("Topic Assignment not found");

            // Fetch Assignment responses
            var assignmentResponses = await _unitOfWork.AssignmentResponses.GetAllByTopicIdAsync(topicId);

            // Get students who participated in the assignment
            var topicEndTime = topicAssignment.Close ?? DateTime.MaxValue;

            var studentsThatTookPartIn = (await _unitOfWork.Enrollments.GetAllByCourseIdAsync(courseId, ct))
                                                        .Where(e => e.StudentId != creatorId)
                                                        .ToList(); ;
            int studentCount = studentsThatTookPartIn.Count();

            // Map students to their marks (if any)
            var marksWithStudentId = studentsThatTookPartIn.ToDictionary(
                student => student.StudentId,
                student =>
                {
                    // Get assignment response for each student
                    var assignmentResponse = assignmentResponses.FirstOrDefault(res => res.StudentId == student.StudentId);
                    if (assignmentResponse == null)
                    {
                        return 0.0; // No response, mark is 0
                    }

                    // Calculate mark for the student based on their answers
                    return (double)(assignmentResponse.Mark ?? 0m);
                });

            // Calculate average, max, and min marks
            double avgMark = marksWithStudentId.Values.Average();
            double maxMark = marksWithStudentId.Values.Max();
            double minMark = marksWithStudentId.Values.Min();

            // Categorize students based on their marks
            var studentInfoAndMarks = await GetStudentInfoWithMarkAndResponseIdForAssignment(studentsThatTookPartIn, marksWithStudentId, ct);

            var fileTypeCount = new Dictionary<string, long>();
            int fileCount = 0;

            foreach (var response in assignmentResponses)
            {
                if (response.Files != null)
                {
                    foreach (var file in response.Files)
                    {
                        fileCount++; // Increment total file count
                        Console.WriteLine($"Processing file: {file.Name}, extension: {Path.GetExtension(file.Name)}");
                        string fileExtension = Path.GetExtension(file.Name)?.ToLower() ?? "unknown"; // Get file extension

                        if (fileTypeCount.ContainsKey(fileExtension))
                        {
                            fileTypeCount[fileExtension]++;
                        }
                        else
                        {
                            fileTypeCount[fileExtension] = 1;
                        }
                    }
                }
            }

            // Setting up the report DTO
            reportDTO.Name = topic.Title ?? "No Title";
            reportDTO.StudentMarks = studentInfoAndMarks;
            reportDTO.StudentWithMarkOver8 = studentInfoAndMarks.Where(info => info.Mark >= 8).ToList();
            reportDTO.StudentWithMarkOver5 = studentInfoAndMarks.Where(info => info.Mark >= 5 && info.Mark < 8).ToList();
            reportDTO.StudentWithMarkOver2 = studentInfoAndMarks.Where(info => info.Mark >= 2 && info.Mark < 5).ToList();
            reportDTO.StudentWithMarkOver0 = studentInfoAndMarks.Where(info => info.Mark < 2).ToList();
            reportDTO.StudentWithNoResponse = studentInfoAndMarks.Where(info => !info.Submitted).ToList();

            reportDTO.MarkDistributionCount = CalculateMarkDistribution(marksWithStudentId, studentCount);
            reportDTO.AvgMark = avgMark;
            reportDTO.MaxMark = maxMark;
            reportDTO.MinMark = minMark;
            reportDTO.CompletionRate = (double)assignmentResponses.Count() / studentCount;

            // Set the file counts and file types
            reportDTO.FileCount = fileCount;
            reportDTO.FileTypeCount = fileTypeCount;

            return reportDTO;
        }

        private double CalculateMark(List<double> marks, string method)
        {
            if (marks == null || marks.Count == 0)
                return 0;

            method = method?.Trim().ToLowerInvariant();

            return method switch
            {
                "highest grade" => marks.Max(),
                "average grade" => marks.Average(),
                "first grade" => marks.FirstOrDefault(),
                "last grade" => marks.LastOrDefault(),
                _ => marks.Max()
            };
        }

        private double CalculateAvgTimeSpend(List<QuizResponseDTO> quizResponses)
        {
            var validResponses = quizResponses
                .Where(res => res.Data.StartedAt.HasValue && res.Data.CompletedAt.HasValue);

            if (!validResponses.Any()) return 0.0;

            return validResponses
                .Where(res => res.Data.StartedAt.HasValue && res.Data.CompletedAt.HasValue)
                .Select(res =>
                {
                    var startedAt = res.Data.StartedAt.Value;
                    var completedAt = res.Data.CompletedAt.Value;

                    return (completedAt - startedAt).TotalSeconds;
                })
                .Average();
        }

        private int CountQuestionType(List<TopicQuizQuestion> questions, string questionType)
        {
            return questions.Count(q => q.Type!.Equals(questionType, StringComparison.OrdinalIgnoreCase));
        }

        public Dictionary<int, int> CalculateMarkDistribution(Dictionary<Guid, double> studentsWithMark, int studentCount)
        {
            var count8OrMore = studentsWithMark.Values.Count(mark => mark >= 8);
            var count5To7 = studentsWithMark.Values.Count(mark => mark >= 5 && mark < 8);
            var count2To4 = studentsWithMark.Values.Count(mark => mark >= 2 && mark < 5);
            var count0To1 = studentsWithMark.Values.Count(mark => mark >= 0 && mark < 2);

            var distribution = new Dictionary<int, int>
            {
                { 8, count8OrMore },
                { 5, count5To7 },
                { 2, count2To4 },
                { 0, count0To1 },
                { -1, studentCount - count8OrMore - count5To7 - count2To4 - count0To1 }
            };

            return distribution;
        }

        public async Task<List<SingleQuizReportDTO.StudentInfoAndMarkQuiz>> GetStudentInfoWithMarkAndResponseIdForQuiz(
            List<Enrollment> studentsThatTookPartIn,
            Dictionary<Guid, double> studentIdWithMark,
            CancellationToken ct = default)
        {
            var enrollmentByStudentId = studentsThatTookPartIn.ToDictionary(detail => detail.StudentId);
            var result = new List<SingleQuizReportDTO.StudentInfoAndMarkQuiz>();

            // FIXED: Change parallel operations to sequential to avoid DbContext threading issues
            // Fetch students with marks sequentially
            foreach (var entry in studentIdWithMark)
            {
                var studentId = entry.Key;
                var mark = entry.Value;
                var enrollment = enrollmentByStudentId[studentId];

                var user = await _unitOfWork.Users.GetByIdAsync(studentId, ct);
                if (user == null)
                {
                    throw new KeyNotFoundException("User not found.");
                }

                result.Add(new SingleQuizReportDTO.StudentInfoAndMarkQuiz
                {
                    Student = MapToDTO(user),
                    Submitted = true,
                    Mark = mark,
                    ResponseId = null
                });
            }

            // Fetch students with no response sequentially
            foreach (var entry in enrollmentByStudentId.Where(entry => !studentIdWithMark.ContainsKey(entry.Key)))
            {
                var studentId = entry.Value.StudentId;

                var user = await _unitOfWork.Users.GetByIdAsync(studentId, ct);

                if (user == null)
                {
                    throw new KeyNotFoundException("User not found.");
                }

                result.Add(new SingleQuizReportDTO.StudentInfoAndMarkQuiz
                {
                    Student = MapToDTO(user),
                    Submitted = false,
                    Mark = 0.0,
                    ResponseId = null
                });
            }

            return result;
        }

        public async Task<List<SingleAssignmentReportDTO.StudentInfoAndMarkAssignment>> GetStudentInfoWithMarkAndResponseIdForAssignment(
            List<Enrollment> studentsThatTookPartIn,
            Dictionary<Guid, double> studentIdWithMark,
            CancellationToken ct = default)
        {
            var enrollmentByStudentId = studentsThatTookPartIn.ToDictionary(detail => detail.StudentId);
            var result = new List<SingleAssignmentReportDTO.StudentInfoAndMarkAssignment>();

            // FIXED: Change parallel operations to sequential to avoid DbContext threading issues
            // Fetch students with marks sequentially
            foreach (var entry in studentIdWithMark)
            {
                var studentId = entry.Key;
                var mark = entry.Value;
                var enrollment = enrollmentByStudentId[studentId];

                // Get user info asynchronously
                var user = await _unitOfWork.Users.GetByIdAsync(studentId, ct);
                if (user == null)
                {
                    throw new KeyNotFoundException("User not found.");
                }

                result.Add(new SingleAssignmentReportDTO.StudentInfoAndMarkAssignment
                {
                    Student = MapToDTO(user),
                    Mark = mark,
                    ResponseId = null
                });
            }

            // Fetch students with no response sequentially
            foreach (var entry in enrollmentByStudentId.Where(entry => !studentIdWithMark.ContainsKey(entry.Key)))
            {
                var studentId = entry.Value.StudentId;
                var user = await _unitOfWork.Users.GetByIdAsync(studentId, ct);

                if (user == null)
                {
                    throw new KeyNotFoundException("User not found.");
                }

                result.Add(new SingleAssignmentReportDTO.StudentInfoAndMarkAssignment
                {
                    Student = MapToDTO(user),
                    Submitted = false,
                    Mark = 0.0,
                    ResponseId = null
                });
            }

            return result;
        }

        public static GetUserResponse MapToDTO(LetsLearn.Core.Entities.User user)
        {
            return new GetUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Avatar = user.Avatar,
                Role = user.Role,
                Enrollments = user.Enrollments?.Select(e => new EnrollmentDTO
                {
                    StudentId = e.StudentId,
                    CourseId = e.CourseId,
                    JoinDate = e.JoinDate
                }).ToList()
            };
        }

        public async Task<List<QuizResponseDTO>> MapQuizResponsesToDTO(List<QuizResponse>? quizResponses, CancellationToken ct = default)
        {
            // Nếu quizResponses là null hoặc không có dữ liệu, trả về danh sách rỗng
            if (quizResponses == null || !quizResponses.Any())
            {
                return new List<QuizResponseDTO>();
            }

            // FIXED: Change parallel operation to sequential to avoid DbContext threading issues
            // Chuyển đổi từng phần tử trong danh sách quizResponses sang DTO sequentially
            var quizResponseDTOs = new List<QuizResponseDTO>();
            foreach (var quizResponse in quizResponses)
            {
                quizResponseDTOs.Add(MapQuizResponseToDTO(quizResponse)); // This method doesn't use async operations
            }

            return quizResponseDTOs;
        }

        public QuizResponseDTO MapQuizResponseToDTO(QuizResponse quizResponse)
        {
            if (quizResponse == null)
            {
                throw new ArgumentNullException(nameof(quizResponse));
            }

            var quizResponseDTO = new QuizResponseDTO
            {
                Id = quizResponse.Id,
                StudentId = quizResponse.StudentId,
                TopicId = quizResponse.TopicId,
                Data = new QuizResponseData
                {
                    Status = quizResponse.Status,
                    StartedAt = quizResponse.StartedAt,
                    CompletedAt = quizResponse.CompletedAt,
                    Answers = quizResponse.Answers
                        .Select(answer => new QuizResponseAnswerDTO
                        {
                            // Chuyển đổi Answer thành DTO
                            Answer = answer.Answer,
                            Mark = answer.Mark,
                            TopicQuizQuestionId = (JsonSerializer.Deserialize<Question>(answer.Question!)!).Id
                        }).ToList()
                }
            };

            return quizResponseDTO;
        }
    }
}