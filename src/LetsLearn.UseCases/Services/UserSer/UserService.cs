using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;


namespace LetsLearn.UseCases.Services.UserSer
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITopicService _topicService;
        private readonly ICourseService _courseService;
        public UserService(IUnitOfWork unitOfWork, ITopicService topicService, ICourseService courseService)
        {
            _unitOfWork = unitOfWork;
            _topicService = topicService;
            _courseService = courseService;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Null-coalesce throw when user not found: +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task<GetUserResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var user = await _unitOfWork.Users.GetByIdWithEnrollmentsAsync(id, ct)
                ?? throw new KeyNotFoundException("User not found.");

            return new GetUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Avatar = user.Avatar,
                Role = user.Role,
                Enrollments = user.Enrollments?
                                              .Select(e => new EnrollmentDTO
                                              {
                                                  StudentId = e.StudentId,
                                                  CourseId = e.CourseId,
                                                  JoinDate = e.JoinDate
                                              })
                                              .ToList()
            };
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Null-coalesce throw when user not found: +1
        // - if Username provided: +1
        // - if Avatar provided: +1
        // - Commit DbUpdateException: +1
        // D = 4 => Minimum Test Cases = D + 1 = 5
        public async Task<UpdateUserResponse> UpdateAsync(Guid id, UpdateUserDTO dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("User not found.");

            if (!string.IsNullOrWhiteSpace(dto.Username))
                user.Username = dto.Username.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Avatar))
                user.Avatar = dto.Avatar.Trim();

            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to persist user changes.", ex);
            }

            return new UpdateUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Avatar = user.Avatar,
                Role = user.Role,
            };
        }

        // Test Case Estimation:
        // Decision points (D):
        // - No branching here: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task<IEnumerable<GetUserResponse>> GetAllAsync(Guid requesterId)
        {
            var users = await _unitOfWork.Users.FindAsync(u => u.Id != requesterId);

            return users
                .Select(u => new GetUserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    Username = u.Username,
                    Avatar = u.Avatar,
                    Role = u.Role,
                })
                .ToList();
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if start > end: +1
        // - if user not found: +1
        // - if user is TEACHER or STUDENT: +1
        // - if no courses found: +1
        // - topic type filter (if type != null): +1
        // D = 5 => Minimum Test Cases = D + 1 = 6
        public async Task<IEnumerable<TopicDTO>> GetAllWorksOfUserAsync(Guid userId, string? type, DateTime? start, DateTime? end, CancellationToken ct = default)
        {
            if (start.HasValue && end.HasValue && start > end)
                throw new ArgumentException("Start time must be after end time");

            start ??= DateTime.MinValue;
            end ??= DateTime.MaxValue;

            var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
            if (user == null)
                throw new ArgumentException("User not found");

            var courses = new List<Course>();

            if (user.Role.Equals("TEACHER", StringComparison.OrdinalIgnoreCase))
            {
                courses = (await _unitOfWork.Course.GetByCreatorId(user.Id, ct)).Where(c => c != null).Select(c => c!).ToList();
            }
            else
            {
                var enrollments = await _unitOfWork.Enrollments.GetAllByStudentIdAsync(user.Id, ct);
                foreach (var enrollment in enrollments)
                {
                    var course = await _unitOfWork.Course.GetByIdAsync(enrollment.CourseId, ct);
                    if (course != null)
                    {
                        courses.Add(course);
                    }
                }
            }

            if (courses == null || !courses.Any())
                throw new Exception("No courses found for user");

            var result = new List<TopicDTO>();

            foreach (var course in courses)
            {
                // Get course creator and students for full course object
                var creator = await _unitOfWork.Users.GetByIdAsync(course.CreatorId, ct);
                var enrollments = await _unitOfWork.Enrollments.GetAllByCourseIdAsync(course.Id, ct);
                var students = new List<User>();

                foreach (var enrollment in enrollments)
                {
                    var student = await _unitOfWork.Users.GetByIdAsync(enrollment.StudentId, ct);
                    if (student != null) students.Add(student);
                }

                // Create full course response
                var courseResponse = MapToEnhancedCourseResponse(course, creator, students);

                foreach (var section in course.Sections)
                {
                    foreach (var topic in section.Topics)
                    {
                        if (string.IsNullOrEmpty(type) || type.Equals(topic.Type, StringComparison.OrdinalIgnoreCase))
                        {
                            // Use the direct method that doesn't wrap in TopicDataDTO
                            object? topicData = await GetTopicDataDirectAsync(topic.Id, userId, start, end, ct);

                            var topicDTO = new TopicDTO
                            {
                                Id = topic.Id,
                                Title = topic.Title,
                                Type = topic.Type,
                                SectionId = topic.SectionId,
                                Data = topicData,  // Direct assignment without wrapper
                                Response = null,   // Will be populated if needed
                                Course = courseResponse, // ✅ Include full course object
                                StudentCount = students.Count // ✅ Include student count
                            };

                            result.Add(topicDTO);
                        }
                    }
                }
            }

            return result;
        }

        // Helper method to create enhanced course response
        private static GetCourseResponse MapToEnhancedCourseResponse(Course course, User? creator, List<User> students)
        {
            return new GetCourseResponse
            {
                Id = course.Id,
                CreatorId = creator?.Id ?? course.CreatorId,
                Title = course.Title,
                Description = course.Description,
                TotalJoined = students.Count,
                ImageUrl = course.ImageUrl,
                Price = course.Price,
                Category = course.Category,
                Level = course.Level,
                IsPublished = course.IsPublished,
            };
        }

        // Updated method to ensure assignment data includes open/close dates
        private async Task<object?> GetTopicDataDirectAsync(Guid topicId, Guid userId, DateTime? start, DateTime? end, CancellationToken ct = default)
        {
            var topic = await _unitOfWork.Topics.GetByIdAsync(topicId, ct);
            if (topic == null)
            {
                return null;
            }

            switch (topic.Type.ToLower())
            {
                case "quiz":
                    var quiz = await _unitOfWork.TopicQuizzes.GetWithQuestionsAsync(topicId);
                    if (quiz != null && ShouldIncludeInDateRange(quiz.Open, quiz.Close, start, end))
                    {
                        return quiz; // ✅ Quiz includes open/close dates
                    }
                    break;

                case "assignment":
                    var assignment = (await _unitOfWork.TopicAssignments.FindAsync(a => a.TopicId == topicId, ct)).FirstOrDefault();
                    if (assignment != null && ShouldIncludeInDateRange(assignment.Open, assignment.Close, start, end))
                    {
                        return assignment; // ✅ Assignment includes open/close dates
                    }
                    break;

                case "meeting":
                    var meeting = (await _unitOfWork.TopicMeetings.FindAsync(m => m.TopicId == topicId, ct)).FirstOrDefault();
                    if (meeting != null && ShouldIncludeInDateRange(meeting.Open, meeting.Close, start, end))
                    {
                        return meeting; // ✅ Meeting includes open/close dates
                    }
                    break;

                case "page":
                    var page = (await _unitOfWork.TopicPages.FindAsync(p => p.TopicId == topicId, ct)).FirstOrDefault();
                    return page; // ✅ Pages don't need date filtering

                case "file":
                    var file = (await _unitOfWork.TopicFiles.FindAsync(f => f.TopicId == topicId, ct)).FirstOrDefault();
                    return file; // ✅ Files don't need date filtering

                case "link":
                    var link = (await _unitOfWork.TopicLinks.FindAsync(l => l.TopicId == topicId, ct)).FirstOrDefault();
                    return link; // ✅ Links don't need date filtering

                default:
                    return topic;
            }

            return null;
        }

        // Update ToDTO to accept course and student count
        public static TopicDTO ToDTO(Topic topic, GetCourseResponse? course = null, int? studentCount = null)
        {
            return new TopicDTO
            {
                Id = topic.Id,
                Title = topic.Title,
                Type = topic.Type,
                SectionId = topic.SectionId,
                Course = course,
                StudentCount = studentCount
            };
        }

        private static bool ShouldIncludeInDateRange(DateTime? itemStart, DateTime? itemEnd, DateTime? filterStart, DateTime? filterEnd)
        {
            // If no date filter provided, include all items
            if (filterStart == null && filterEnd == null)
                return true;

            // If date filter provided, check for overlap
            if (filterStart.HasValue && filterEnd.HasValue)
            {
                var start = itemStart ?? DateTime.MinValue;
                var end = itemEnd ?? DateTime.MaxValue;

                // Check if there's any overlap between item date range and filter date range
                return start <= filterEnd && end >= filterStart;
            }

            return true;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if course not found: +1
        // - if no topic quizzes found: +1
        // - if no assignments found: +1
        // - quiz mark calculation: +1
        // - assignment mark calculation: +1
        // D = 5 => Minimum Test Cases = D + 1 = 6
        public async Task<StudentReportDTO> GetStudentReportAsync(
            Guid userId,
            String courseId,
            DateTime? start,
            DateTime? end,
            CancellationToken ct = default)
        {
            // 1. Lấy course
            var course = await _unitOfWork.Course.GetByIdAsync(courseId, ct);
            if (course == null)
                throw new KeyNotFoundException("Course not found");

            // 2. Lấy tất cả topic của course
            var sectionIds = course.Sections.Select(s => s.Id).ToList();
            var topics = await _unitOfWork.Topics.GetAllBySectionIdsAsync(sectionIds, ct);

            // 3. Xử lý start/end null
            var startTime = start ?? DateTime.MinValue;
            var endTime = end ?? DateTime.MaxValue;

            // 4. Lấy danh sách quiz trong thời gian [start, end]
            var quizTopicIds = topics.Where(t => t.Type == "quiz").Select(t => t.Id).ToList();
            var topicQuizzes = await _unitOfWork.TopicQuizzes.FindByTopicsAndOpenCloseAsync(
                quizTopicIds, startTime, endTime, ct);

            // 5. Lấy quiz responses của user
            var quizResponses = await _unitOfWork.QuizResponses.FindByTopicIdsAndStudentIdAsync(
                topicQuizzes.Select(q => q.TopicId).ToList(), userId, ct);

            // 6. Lấy assignment
            var assignmentTopicIds = topics.Where(t => t.Type == "assignment").Select(t => t.Id).ToList();
            var topicAssignments = await _unitOfWork.TopicAssignments.FindByTopicsAndOpenCloseAsync(
                assignmentTopicIds, startTime, endTime, ct);

            var assignmentResponses = await _unitOfWork.AssignmentResponses.FindByTopicIdsAndStudentIdAsync(
                topicAssignments.Select(a => a.TopicId).ToList(), userId, ct);

            // 7. Tính điểm quiz base 10
            var quizTopicIdWithMarkBase10 =
                CalculateTopicQuizMarkBase10(quizResponses, topicQuizzes.ToDictionary(q => q.TopicId, q => q.GradingMethod));

            // 8. Tính điểm assignment
            var assignmentTopicIdWithMark =
                CalculateTopicAssignmentMark(assignmentResponses);

            // 9. Build report DTO
            var report = new StudentReportDTO
            {
                TotalQuizCount = topicQuizzes.Count,
                TotalAssignmentCount = topicAssignments.Count,
                QuizToDoCount = topicQuizzes.Count - quizResponses.Select(r => r.TopicId).Distinct().Count(),
                AssignmentToDoCount = topicAssignments.Count - assignmentResponses.Select(r => r.TopicId).Distinct().Count(),
                AvgQuizMark = quizTopicIdWithMarkBase10.Any() ? quizTopicIdWithMarkBase10.Values.Average() : 0,
                AvgAssignmentMark = assignmentResponses.Where(r => r.Mark.HasValue).Select(r => (double)r.Mark!.Value).DefaultIfEmpty(0).Average()
            };

            // Top topic quiz
            report.TopTopicQuiz = quizTopicIdWithMarkBase10.Keys.Select(tId =>
            {
                var latest = quizResponses
                    .Where(r => r.TopicId == tId)
                    .OrderByDescending(r => r.CompletedAt)
                    .FirstOrDefault();

                return new StudentReportDTO.TopicInfo
                {
                    Topic = topics.First(t => t.Id == tId),               // Topic entity luôn
                    ResponseId = latest?.Id,
                    Mark = quizTopicIdWithMarkBase10[tId],
                    DoneTime = latest?.CompletedAt
                };
            }).ToList();

            // Top topic assignment
            report.TopTopicAssignment = assignmentResponses.Select(resp =>
            {
                return new StudentReportDTO.TopicInfo
                {
                    Topic = topics.First(t => t.Id == resp.TopicId),
                    ResponseId = resp.Id,
                    Mark = assignmentTopicIdWithMark[resp.TopicId],
                    DoneTime = resp.SubmittedAt
                };
            }).ToList();

            return report;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if enrollment not found → throw: +1
        // - if course exists and TotalJoined > 0 → decrease: +1
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public async Task LeaveCourseAsync(Guid userId, string courseId, CancellationToken ct = default)
        {
            // Kiểm tra xem enrollment có tồn tại
            var enrollment = await _unitOfWork.Enrollments.GetByIdsAsync(userId, courseId, ct);
            if (enrollment == null)
                throw new KeyNotFoundException("Enrollment not found for this user and course.");

            // Xóa enrollment
            await _unitOfWork.Enrollments.DeleteByStudentIdAndCourseIdAsync(userId, courseId, ct);

            // Cập nhật tổng số học viên của khóa học
            var course = await _unitOfWork.Course.GetByIdAsync(courseId, ct);
            if (course != null && course.TotalJoined > 0)
            {
                course.TotalJoined -= 1;
            }

            // Commit transaction
            await _unitOfWork.CommitAsync();
        }

        public static TopicDTO ToDTO(Topic topic)
        {
            return new TopicDTO
            {
                Id = topic.Id,
                Title = topic.Title,
                Type = topic.Type,
                SectionId = topic.SectionId,
                // Add any other properties of Topic to map to TopicDTO
            };
        }

        private Dictionary<Guid, double> CalculateTopicQuizMarkBase10(
            List<QuizResponse> quizResponses,
            Dictionary<Guid, string> topicGradingMethod)
        {
            var result = new Dictionary<Guid, double>();

            // Group theo topicId → list<mark>
            var grouped = quizResponses
                .SelectMany(resp =>
                    resp.Answers.Select(ans =>
                    {
                        double mark = Convert.ToDouble(ans.Mark ?? 0);

                        Console.WriteLine($"MARK = {ans.Mark}");

                        double defaultMark = 1;

                        Console.WriteLine($"RAW QUESTION = {ans.Question}");

                        try
                        {
                            var qObj = System.Text.Json.JsonSerializer.Deserialize<Question>(
                                ans.Question,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                            );

                            Console.WriteLine($"Parsed DefaultMark = {qObj?.DefaultMark}");

                            defaultMark = Convert.ToDouble(qObj?.DefaultMark ?? 1);
                        }
                        catch { defaultMark = 1; }

                        double normalizedMark = defaultMark == 0
                            ? 0
                            : (mark / defaultMark) * 10;

                        Console.WriteLine($"TopicId from Response = {resp.TopicId}");

                        return new
                        {
                            resp.TopicId,
                            EarnedMark = mark,
                            Default = defaultMark
                        };
                    })
                )
                .GroupBy(x => x.TopicId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        double totalEarned = g.Sum(x => x.EarnedMark);
                        double totalDefault = g.Sum(x => x.Default);

                        if (totalDefault == 0) return 0; // tránh chia 0

                        return (totalEarned / totalDefault) * 10;  // score base 10
                    }
                );

            return grouped;
        }

        private Dictionary<Guid, double> CalculateTopicAssignmentMark(IReadOnlyList<AssignmentResponse> responses)
        {
            return responses
                .Where(r => r.Mark.HasValue)
                .ToDictionary(r => r.TopicId, r => (double)r.Mark!.Value);
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
                "first grade" => marks.First(),
                "last grade" => marks.Last(),
                _ => marks.Max()
            };
        }
    }
}
