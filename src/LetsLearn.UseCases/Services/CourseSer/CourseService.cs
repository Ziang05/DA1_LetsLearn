using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Repository;
using LetsLearn.Infrastructure.UnitOfWork;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services.CourseSer
{
    public class CourseService : ICourseService
    {
        private readonly IUnitOfWork _uow;
        private readonly ITopicService _topicService;

        private static readonly DateTime MIN = DateTime.SpecifyKind(DateTime.MinValue.AddYears(1), DateTimeKind.Utc);
        private static readonly DateTime MAX = DateTime.SpecifyKind(DateTime.MaxValue.AddYears(-1), DateTimeKind.Utc);

        public CourseService(IUnitOfWork uow, ITopicService topicService)
        {
            _uow = uow;
            _topicService = topicService;
        }

        // =============== CREATE / UPDATE ===============
        // Test Case Estimation:
        // Decision points (D):
        // - if Title is null/whitespace → throw: +1
        // - if titleExists → throw: +1
        // - if idExists → throw: +1
        // - DbUpdateException on Commit → +1 (error branch)
        // D = 4 => Minimum Test Cases = D + 1 = 5
        public async Task<CreateCourseResponse> CreateCourseAsync(CreateCourseRequest dto, Guid userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new ArgumentException("Title is required.");

            var titleExists = await _uow.Course.ExistByTitle(dto.Title!);
            if (titleExists)
                throw new InvalidOperationException("A course with this title already exists. Please choose a different name");

            var idExists = await _uow.Course.ExistsAsync(c => c.Id == dto.Id, ct);
            if (idExists)
                throw new InvalidOperationException($"Course ID '{dto.Id}' already exists.");

            var course = new Course
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                Category = dto.Category,
                Level = dto.Level,
                Price = dto.Price,
                IsPublished = dto.IsPublished ?? false,
                CreatorId = userId,
                TotalJoined = 1
            };

            await _uow.Course.AddAsync(course);

            DateTime utcNow = DateTime.UtcNow;

            TimeZoneInfo gmtPlus7 = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

            DateTime gmtPlus7Time = TimeZoneInfo.ConvertTimeFromUtc(utcNow, gmtPlus7);

            var enrollment = new Enrollment
            {
                StudentId = userId,
                CourseId = course.Id,
                JoinDate = DateTime.SpecifyKind(gmtPlus7Time, DateTimeKind.Utc)
            };

            await _uow.Enrollments.AddAsync(enrollment);

            try
            {
                await _uow.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to create course.", ex);
            }

            return new CreateCourseResponse
            {
                Id = course.Id,
                CreatorId = course.CreatorId,
                Title = course.Title,
                Description = course.Description,
                TotalJoined = course.TotalJoined,
                ImageUrl = course.ImageUrl,
                Price = course.Price,
                Category = course.Category,
                Level = course.Level,
                IsPublished = course.IsPublished
            };
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Null-coalesce throw when course not found: +1
        // - if Title provided: +1
        // - if titleExists: +1
        // - DbUpdateException on Commit → +1
        // D = 4 => Minimum Test Cases = D + 1 = 5
        public async Task<UpdateCourseResponse> UpdateCourseAsync(UpdateCourseRequest dto, CancellationToken ct = default)
        {
            var course = await _uow.Course.GetByIdAsync(dto.Id, ct)
                         ?? throw new KeyNotFoundException("Course not found.");

            if (!string.IsNullOrWhiteSpace(dto.Title))
                course.Title = dto.Title;

            course.Description = dto.Description;
            course.ImageUrl = dto.ImageUrl;
            course.Category = dto.Category;
            course.Level = dto.Level;
            course.IsPublished = dto.IsPublished ?? course.IsPublished;

            try
            {
                await _uow.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to update course.", ex);
            }

            return new UpdateCourseResponse
            {
                Id = course.Id,
                CreatorId = course.CreatorId,
                Title = course.Title,
                Description = course.Description,
                TotalJoined = course.TotalJoined,
                ImageUrl = course.ImageUrl,
                Price = course.Price,
                Category = course.Category,
                Level = course.Level,
                IsPublished = course.IsPublished,
            };
        }

        // ================= READ =================
        // Test Case Estimation:
        // Decision points (D):
        // - Pure retrieval/filtering in repository: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task<IEnumerable<GetCourseResponse>> GetAllCoursesAsync(CancellationToken ct = default)
        {
            var courses = await _uow.Course.GetAllCoursesByIsPublishedTrue();
            return courses.Where(c => c != null).Select(c => MapToResponse(c!)).ToList();
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Pure retrieval: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task<IEnumerable<GetCourseResponse>> GetAllCoursesForAdminAsync(CancellationToken ct = default)
        {
            var courses = await _uow.Course.GetAllAsync(ct);
            return courses.Where(c => c != null).Select(c => MapToResponse(c!)).ToList();
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if userExists is false: +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task<IEnumerable<GetCourseResponse>> GetAllCoursesByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            var userExists = await _uow.Users.ExistsAsync(u => u.Id == userId, ct);
            if (!userExists) throw new KeyNotFoundException("User not found.");

            var enrollments = await _uow.Enrollments.GetByStudentId(userId, ct);

            var courseIds = enrollments.Select(e => e.CourseId).Distinct();

            var courses = await _uow.Course.GetByIdsAsync(courseIds);

            return courses.Where(c => c != null).Select(c => MapToResponse(c!)).ToList();
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Null-coalesce throw when course not found: +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task<GetCourseResponse> GetCourseByIdAsync(String id, CancellationToken ct = default)
        {
            // Query course như cũ - KHÔNG thay đổi
            var course = await _uow.Course.GetByIdAsync(id, ct)
                         ?? throw new KeyNotFoundException("Course not found.");

            // Query riêng creator
            var creator = await _uow.Users.GetByIdAsync(course.CreatorId, ct);

            // Query riêng enrollments
            var enrollments = await _uow.Enrollments.FindAsync(e => e.CourseId == id, ct);
            var studentIds = enrollments.Where(e => e.StudentId != course.CreatorId)
                                      .Select(e => e.StudentId)
                                      .ToList();

            // Query riêng students
            var students = new List<UserBasicInfo>();
            if (studentIds.Any())
            {
                foreach (var studentId in studentIds)
                {
                    var student = await _uow.Users.GetByIdAsync(studentId, ct);
                    if (student != null)
                    {
                        students.Add(new UserBasicInfo
                        {
                            Id = student.Id,
                            Username = student.Username,
                            Avatar = student.Avatar
                        });
                    }
                }
            }

            return MapToResponseWithUsers(course, creator, students);
        }
        // Helper method mapping đơn giản
        private GetCourseResponse MapToResponseWithUsers(Course c, User? creator, List<UserBasicInfo> students)
        {
            var response = MapToResponse(c); // Dùng method cũ

            // Thêm creator và students
            response.Creator = creator != null ? new UserBasicInfo
            {
                Id = creator.Id,
                Username = creator.Username,
                Avatar = creator.Avatar
            } : null;

            response.Students = students;

            return response;
        }
        // Test Case Estimation:
        // Decision points (D):
        // - if course not found → +1
        // - if user not found → +1
        // - if enrollmentExists → +1
        // - Commit error → +1
        // D = 4 => Minimum Test Cases = D + 1 = 5
        public async Task AddUserToCourseAsync(String courseId, Guid userId, CancellationToken ct = default)
        {
            var course = await _uow.Course.GetByIdAsync(courseId, ct)
                             ?? throw new KeyNotFoundException("Course not found.");

            var user = await _uow.Users.GetByIdAsync(userId, ct)
                            ?? throw new KeyNotFoundException("User not found.");

            var enrollmentExists = await _uow.Enrollments.ExistsAsync(e => e.CourseId == courseId && e.StudentId == userId, ct);
            if (enrollmentExists)
                throw new InvalidOperationException("User is already enrolled in this course.");

            var enrollment = new Enrollment
            {
                StudentId = userId,
                CourseId = courseId,
                JoinDate = DateTime.UtcNow
            };

            await _uow.Enrollments.AddAsync(enrollment);

            course.TotalJoined += 1;

            try
            {
                await _uow.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to add user to course.", ex);
            }
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if only start or end provided (XOR) → throw: +1
        // - if start > end → throw: +1
        // - if course not found → +1
        // - switch: quiz/assignment/meeting/page/file/link/topic → +6
        // D = 9 => Minimum Test Cases = D + 1 = 10
        public async Task<IEnumerable<TopicDTO>> GetAllWorksOfCourseAndUserAsync(String courseId, Guid userId, string type, DateTime? start, DateTime? end, CancellationToken ct = default)
        {
            if (start.HasValue ^ end.HasValue)
            {
                throw new ArgumentException("Provide start and end time!");
            }

            if (start.HasValue && end.HasValue && start > end)
            {
                throw new ArgumentException("Start time must be after end time");
            }

            var course = await _uow.Course.GetByIdAsync(courseId, ct);
            if (course == null)
            {
                throw new KeyNotFoundException("Course not found");
            }

            var result = new List<TopicDTO>();

            foreach (var courseSection in course.Sections)
            {
                foreach (var topicSection in courseSection.Topics)
                {
                    if (string.IsNullOrEmpty(type) || type.Equals(topicSection.Type, StringComparison.OrdinalIgnoreCase))
                    {
                        // Get the direct topic data without wrapping
                        var topicData = await GetTopicDataDirectAsync(topicSection.Id, userId, start, end, ct);

                        var topicDTO = new TopicDTO
                        {
                            Id = topicSection.Id,
                            Title = topicSection.Title ?? "No Title",
                            Type = topicSection.Type,
                            SectionId = topicSection.SectionId,
                            Data = topicData,  // Direct assignment without .Item wrapper
                            Response = null,   // Handle response separately if needed
                            Course = null,     // Will be populated if needed
                            StudentCount = null
                        };

                        result.Add(topicDTO);
                    }
                }
            }

            return result;
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
        // D = 7 => Minimum Test Cases = D + 1 = 8
        public async Task<object?> GetTopicDataDirectAsync(Guid topicId, Guid userId, DateTime? start, DateTime? end, CancellationToken ct = default)
        {
            var topic = await _uow.Topics.GetByIdAsync(topicId, ct);
            if (topic == null)
            {
                return null;
            }

            switch (topic.Type!.ToLower())
            {
                case "quiz":
                    var quiz = await _uow.TopicQuizzes.GetWithQuestionsAsync(topicId);
                    return quiz; // Return quiz directly, not wrapped in TopicDataDTO

                case "assignment":
                    var assignment = (await _uow.TopicAssignments.FindAsync(a => a.TopicId == topicId, ct)).FirstOrDefault();
                    return assignment; // Return assignment directly

                case "meeting":
                    var meeting = (await _uow.TopicMeetings.FindAsync(m => m.TopicId == topicId, ct)).FirstOrDefault();
                    return meeting; // Return meeting directly

                case "page":
                    var page = (await _uow.TopicPages.FindAsync(p => p.TopicId == topicId, ct)).FirstOrDefault();
                    return page; // Return page directly

                case "file":
                    var file = (await _uow.TopicFiles.FindAsync(f => f.TopicId == topicId, ct)).FirstOrDefault();
                    return file; // Return file directly

                case "link":
                    var link = (await _uow.TopicLinks.FindAsync(l => l.TopicId == topicId, ct)).FirstOrDefault();
                    return link; // Return link directly

                default:
                    return topic; // Return basic topic for unknown types
            }
        }

        public async Task<AllAssignmentsReportDTO> GetAssignmentsReportAsync(String courseId, DateTime? startTime, DateTime? endTime, CancellationToken ct = default)
        {
            if (!startTime.HasValue)
            {
                startTime = DateTime.MinValue; // Default start to minimum value if not provided
            }
            if (!endTime.HasValue)
            {
                endTime = DateTime.MaxValue; // Default end to maximum value if not provided
            }

            // Lấy thông tin khóa học
            var course = await _uow.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found");

            var singleAssignmentReportDTOs = new List<SingleAssignmentReportDTO>();

            // Lấy thời gian hiện tại theo UTC và tính toán thời gian tháng hiện tại
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1).AddSeconds(-1);

            var assignmentsEndingThisMonth = 0;
            var assignmentsInProgressCounter = 0;
            DateTime? nextClosestEndTime = null;

            // Duyệt qua tất cả các sections và topics trong khóa học
            foreach (var courseSection in course.Sections)
            {
                foreach (var topic in courseSection.Topics)
                {
                    if (topic.Type == "assignment") // Kiểm tra xem loại topic có phải là "assignment" không
                    {
                        var topicAssignment = await _uow.TopicAssignments.GetByIdAsync(topic.Id)
                             ?? throw new InvalidOperationException("Report to dev please");


                        // Tính toán thời gian bắt đầu và kết thúc của bài tập
                        var topicStart = topicAssignment.Open ?? DateTime.MinValue;
                        var topicEnd = topicAssignment.Close ?? DateTime.MaxValue;

                        if (topicStart < endTime && topicEnd > startTime)
                        {
                            singleAssignmentReportDTOs.Add(await _topicService.GetSingleAssignmentReportAsync(courseId, topic.Id));

                            // Đếm số bài tập sẽ kết thúc trong tháng này
                            if (topicEnd > monthStart && topicEnd < monthEnd && topicEnd > now)
                            {
                                assignmentsEndingThisMonth++;
                            }

                            if (topicEnd > now)
                            {
                                assignmentsInProgressCounter++;
                            }

                            // Lưu thời gian kết thúc bài tập gần nhất
                            if (topicEnd > now)
                            {
                                if (nextClosestEndTime == null || topicEnd < nextClosestEndTime)
                                {
                                    nextClosestEndTime = topicEnd;
                                }
                            }
                        }
                    }
                }
            }

            var markDistributionCounts = singleAssignmentReportDTOs
                .Where(report => report.MarkDistributionCount != null)  // Kiểm tra null
                .Select(report => report.MarkDistributionCount) // Lấy MarkDistributionCount từ từng báo cáo
                .ToList();

            var studentInfoWithAverageMarks = CalculateAverageStudentScoreForAssignments(singleAssignmentReportDTOs);

            var reportDTO = new AllAssignmentsReportDTO
            {
                AssignmentsCountInProgress = assignmentsInProgressCounter,
                AssignmentCount = singleAssignmentReportDTOs.Count,
                // Add safety checks for empty collections
                AvgMark = singleAssignmentReportDTOs.Any() ? singleAssignmentReportDTOs.Average(r => r.AvgMark) : 0,
                AvgCompletionRate = singleAssignmentReportDTOs.Any() ? singleAssignmentReportDTOs.Average(r => r.CompletionRate) : 0,
                NumberOfAssignmentEndsAtThisMonth = assignmentsEndingThisMonth,
                ClosestNextEndAssignment = nextClosestEndTime,
                MarkDistributionCount = MergeMarkDistributionCount(markDistributionCounts),
                StudentInfoWithMarkAverage = studentInfoWithAverageMarks,
                StudentWithMarkOver8 = studentInfoWithAverageMarks.Where(info => info.AverageMark >= 8.0).ToList(),
                StudentWithMarkOver5 = studentInfoWithAverageMarks.Where(info => info.AverageMark >= 5.0 && info.AverageMark < 8.0).ToList(),
                StudentWithMarkOver2 = studentInfoWithAverageMarks.Where(info => info.AverageMark >= 2.0 && info.AverageMark < 5.0).ToList(),
                StudentWithMarkOver0 = studentInfoWithAverageMarks.Where(info => info.AverageMark < 2.0).ToList(),
                StudentWithNoResponse = studentInfoWithAverageMarks.Where(info => !info.Submitted).ToList(),
                FileTypeCount = GetFileTypeCount(singleAssignmentReportDTOs),
                SingleAssignmentReports = singleAssignmentReportDTOs
            };

            return reportDTO;
        }

        public async Task<AllQuizzesReportDTO> GetQuizzesReportAsync(String courseId, DateTime? startTime, DateTime? endTime, CancellationToken ct = default)
        {
            if (!startTime.HasValue)
            {
                startTime = DateTime.MinValue; // Default start to minimum value if not provided
            }
            if (!endTime.HasValue)
            {
                endTime = DateTime.MaxValue; // Default end to maximum value if not provided
            }

            // Lấy thông tin khóa học
            var course = await _uow.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found");

            List<SingleQuizReportDTO> singleQuizReportDTOs = new List<SingleQuizReportDTO>();

            // Duyệt qua tất cả các section và topic trong khóa học
            foreach (var courseSection in course.Sections)
            {
                foreach (var topic in courseSection.Topics)
                {
                    if (topic.Type == "quiz")
                    {
                        // Lấy thông tin bài quiz từ topic
                        var topicQuiz = await _uow.TopicQuizzes.GetByIdAsync(topic.Id, ct)
                            ?? throw new InvalidOperationException("Report to dev please");


                        // Lấy thời gian mở và đóng của bài quiz
                        var topicStart = topicQuiz.Open ?? DateTime.MinValue;
                        var topicEnd = topicQuiz.Close ?? DateTime.MaxValue;

                        // Kiểm tra xem bài quiz có trong khoảng thời gian yêu cầu không
                        if (topicStart < endTime && topicEnd > startTime)
                        {
                            var quizReport = await _topicService.GetSingleQuizReportAsync(courseId, topic.Id, ct);
                            singleQuizReportDTOs.Add(quizReport);
                        }
                    }
                }
            }

            // Tính toán điểm trung bình của học sinh từ báo cáo quiz
            var studentInfoAndMarks = CalculateAverageStudentScoreForQuizzes(singleQuizReportDTOs);

            var reportDTO = new AllQuizzesReportDTO
            {
                QuizCount = singleQuizReportDTOs.Count,
                // Add safety checks for empty collections
                AvgCompletionPercentage = singleQuizReportDTOs.Any() ? singleQuizReportDTOs.Average(rep => rep.CompletionRate) : 0,
                MinQuestionCount = singleQuizReportDTOs.Any() ? singleQuizReportDTOs.Min(rep => rep.QuestionCount) : 0,
                MaxQuestionCount = singleQuizReportDTOs.Any() ? singleQuizReportDTOs.Max(rep => rep.QuestionCount) : 0,
                MinStudentScoreBase10 = singleQuizReportDTOs.Any() ? singleQuizReportDTOs.Min(rep => rep.MinStudentMarkBase10) : 0,
                MaxStudentScoreBase10 = singleQuizReportDTOs.Any() ? singleQuizReportDTOs.Max(rep => rep.MaxStudentMarkBase10) : 0,
                StudentInfoWithMarkAverage = studentInfoAndMarks,
                StudentWithMarkOver8 = studentInfoAndMarks.Where(info => info.Submitted && info.Mark >= 8.0).ToList(),
                StudentWithMarkOver5 = studentInfoAndMarks.Where(info => info.Submitted && info.Mark >= 5.0 && info.Mark < 8.0).ToList(),
                StudentWithMarkOver2 = studentInfoAndMarks.Where(info => info.Submitted && info.Mark >= 2.0 && info.Mark < 5.0).ToList(),
                StudentWithMarkOver0 = studentInfoAndMarks.Where(info => info.Submitted && info.Mark < 2.0).ToList(),
                StudentWithNoResponse = studentInfoAndMarks.Where(info => !info.Submitted).ToList(),
                MarkDistributionCount = MergeMarkDistributionCount(singleQuizReportDTOs.Select(rep => rep.MarkDistributionCount).ToList()),
                SingleQuizReports = singleQuizReportDTOs,
                TrueFalseQuestionCount = singleQuizReportDTOs.Sum(rep => rep.TrueFalseQuestionCount),
                MultipleChoiceQuestionCount = singleQuizReportDTOs.Sum(rep => rep.MultipleChoiceQuestionCount),
                ShortAnswerQuestionCount = singleQuizReportDTOs.Sum(rep => rep.ShortAnswerQuestionCount)
            };

            return reportDTO;
        }

        public Dictionary<int, int> MergeMarkDistributionCount(List<Dictionary<int, int>> markDistributionCounts)
        {
            // Khởi tạo một từ điển để lưu trữ kết quả phân phối điểm tổng hợp
            var mergedMarkDistribution = new Dictionary<int, int>
            {
                { -1, 0 },
                { 0, 0 },
                { 2, 0 },
                { 5, 0 },
                { 8, 0 }
            };

            // Duyệt qua các kết quả phân phối điểm và cộng dồn giá trị của từng nhóm điểm
            foreach (var distribution in markDistributionCounts)
            {
                foreach (var entry in distribution)
                {
                    // Nếu nhóm điểm đã tồn tại trong mergedMarkDistribution, cộng dồn giá trị
                    if (mergedMarkDistribution.ContainsKey(entry.Key))
                    {
                        mergedMarkDistribution[entry.Key] += entry.Value;
                    }
                }
            }

            return mergedMarkDistribution;
        }

        public Dictionary<string, long> GetFileTypeCount(List<SingleAssignmentReportDTO> singleAssignmentReports)
        {
            // Khởi tạo một từ điển để lưu trữ số lượng các loại tệp
            var fileTypeCount = new Dictionary<string, long>();

            // Duyệt qua tất cả các báo cáo bài tập
            foreach (var report in singleAssignmentReports)
            {
                // Kiểm tra nếu báo cáo có FileTypeCount không null và có giá trị
                if (report.FileTypeCount != null && report.FileTypeCount.Any())
                {
                    // Duyệt qua từng loại tệp và cộng dồn số lượng
                    foreach (var fileType in report.FileTypeCount)
                    {
                        if (fileTypeCount.ContainsKey(fileType.Key))
                        {
                            fileTypeCount[fileType.Key] += fileType.Value;
                        }
                        else
                        {
                            fileTypeCount[fileType.Key] = fileType.Value;
                        }
                    }
                }
            }

            return fileTypeCount;
        }

        public List<AllAssignmentsReportDTO.StudentInfoWithAverageMark> CalculateAverageStudentScoreForAssignments(List<SingleAssignmentReportDTO> singleAssignmentReports)
        {
            // Khởi tạo các cấu trúc dữ liệu để lưu trữ điểm của sinh viên
            var studentScoresMap = new Dictionary<Guid, List<double>>(); // Lưu điểm của từng sinh viên
            var latestStudentInfo = new Dictionary<Guid, SingleAssignmentReportDTO.StudentInfoAndMarkAssignment>(); // Lưu thông tin mới nhất của sinh viên

            // Duyệt qua tất cả các báo cáo bài tập
            foreach (var report in singleAssignmentReports)
            {
                // Kiểm tra nếu không có thông tin điểm của sinh viên
                if (report.StudentMarks == null) continue;

                // Duyệt qua các sinh viên trong báo cáo bài tập
                foreach (var infoAndMark in report.StudentMarks)
                {
                    if (infoAndMark.Student != null && infoAndMark.Submitted)
                    {
                        var studentId = infoAndMark.Student.Id;
                        var mark = infoAndMark.Mark ?? 0;

                        // Lưu điểm của sinh viên
                        if (!studentScoresMap.ContainsKey(studentId))
                        {
                            studentScoresMap[studentId] = new List<double>();
                        }
                        studentScoresMap[studentId].Add(mark);

                        // Cập nhật hoặc lưu lại thông tin mới nhất của sinh viên
                        latestStudentInfo[studentId] = infoAndMark;
                    }
                }
            }

            // Tạo danh sách các sinh viên với điểm trung bình và thông tin
            return studentScoresMap.Select(entry =>
            {
                var studentId = entry.Key;
                var scores = entry.Value;

                // Add safety check for empty scores collection
                var averageMark = scores.Any() ? scores.Average() : 0;

                // Lấy thông tin sinh viên mới nhất
                var existingInfo = latestStudentInfo[studentId];

                // Tạo đối tượng StudentInfoWithAverageMark
                return new AllAssignmentsReportDTO.StudentInfoWithAverageMark
                {
                    User = existingInfo.Student,
                    AverageMark = averageMark,
                    Submitted = existingInfo.Submitted
                };
            }).ToList();
        }

        public List<SingleQuizReportDTO.StudentInfoAndMarkQuiz> CalculateAverageStudentScoreForQuizzes(List<SingleQuizReportDTO> singleQuizReports)
        {
            // Tạo Dictionary lưu trữ điểm của học sinh qua các quiz
            var studentScoresMap = new Dictionary<Guid, List<double>>();

            // Tạo Dictionary lưu trữ thông tin mới nhất của từng học sinh
            var latestStudentInfo = new Dictionary<Guid, SingleQuizReportDTO.StudentInfoAndMarkQuiz>();

            // Lặp qua tất cả các báo cáo quiz
            foreach (var report in singleQuizReports)
            {
                if (report.StudentWithMark == null) continue;

                // Lặp qua từng học sinh trong báo cáo quiz
                foreach (var infoAndMark in report.StudentWithMark)
                {
                    if (infoAndMark.Student != null && infoAndMark.Submitted && report.MaxDefaultMark != null)
                    {
                        var studentId = infoAndMark.Student.Id;

                        // Lưu trữ điểm của học sinh vào studentScoresMap
                        if (!studentScoresMap.ContainsKey(studentId))
                        {
                            studentScoresMap[studentId] = new List<double>();
                        }
                        studentScoresMap[studentId].Add(infoAndMark.Mark ?? 0);

                        // Cập nhật thông tin học sinh mới nhất
                        latestStudentInfo[studentId] = infoAndMark;
                    }
                }
            }

            // Tạo danh sách kết quả với điểm trung bình cho mỗi học sinh
            var result = studentScoresMap.Select(entry =>
            {
                var studentId = entry.Key;
                var scores = entry.Value;

                // Lấy thông tin học sinh mới nhất
                var existingInfo = latestStudentInfo[studentId];

                // Tạo đối tượng mới để tránh sửa đổi thông tin gốc
                var avgInfo = new SingleQuizReportDTO.StudentInfoAndMarkQuiz
                {
                    Student = existingInfo.Student,
                    Submitted = existingInfo.Submitted,
                    ResponseId = existingInfo.ResponseId
                };

                // Add safety check for empty scores collection
                double averageMark = scores.Any() ? scores.Average() : 0;
                avgInfo.Mark = averageMark;

                return avgInfo;
            }).ToList();

            return result;
        }


        public async Task<TopicDataDTO?> GetTopicDataByTypeAsync(Guid topicId, Guid userId, DateTime? start, DateTime? end, CancellationToken ct = default) //TopicDataDTO
        {
            // Lấy thông tin topic theo loại
            var topic = await _uow.Topics.GetByIdAsync(topicId, ct);
            if (topic == null)
            {
                throw new KeyNotFoundException("Topic not found");
            }

            TopicDataDTO? topicData = null;

            switch (topic.Type.ToLower())
            {
                case "quiz":
                    var quiz = await _uow.TopicQuizzes.GetWithQuestionsAsync(topicId);
                    if (quiz != null && (end == null || quiz.Close < end))
                    {
                        var responses = await _uow.QuizResponses.FindByTopicIdAndStudentIdWithAnswersAsync(quiz.TopicId, userId, ct);
                        topicData = new TopicDataDTO
                        {
                            Item = quiz,
                            Response = responses
                        };
                    }
                    break;

                case "assignment":
                    var assignment = (await _uow.TopicAssignments.FindAsync(a => a.TopicId == topicId, ct)).FirstOrDefault();
                    if (assignment != null && (end == null || assignment.Close < end))
                    {
                        var response = await _uow.AssignmentResponses.GetByTopicIdAndStudentIdWithFilesAsync(assignment.TopicId, userId);
                        topicData = new TopicDataDTO
                        {
                            Item = assignment,
                            Response = response
                        };
                    }
                    break;

                case "meeting":
                case "file":
                case "link":
                case "page":
                    // Không xử lý các loại này
                    break;

                default:
                    throw new NotSupportedException($"Topic type {topic.Type} is not supported.");
            }

            return topicData;
        }

        // =============== Helpers (mapping & utils) ===============
        // Test Case Estimation:
        // Decision points (D):
        // - Pure mapping, no branching: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1 (basic mapping)
        private async Task<GetCourseResponse> MapToResponseAsync(Course c)
        {
            return new GetCourseResponse
            {
                Id = c.Id,
                CreatorId = c.CreatorId,
                Title = c.Title,
                Description = c.Description,
                TotalJoined = c.TotalJoined,
                ImageUrl = c.ImageUrl,
                Price = c.Price,
                Category = c.Category,
                Level = c.Level,
                IsPublished = c.IsPublished,

                Sections = (await Task.WhenAll(
                    c.Sections.Select(async s => new SectionResponse
                    {
                        Id = s.Id,
                        CourseId = c.Id,
                        Position = s.Position,
                        Title = s.Title,
                        Description = s.Description,

                        Topics = (await Task.WhenAll(
                            s.Topics.Select(async t =>
                            {
                                // Dùng TopicService để lấy DATA đầy đủ
                                return await _topicService.GetTopicByIdAsync(t.Id);
                            })
                        )).ToList()
                    })
                ))
                .ToList()
            };
        }

        private static GetCourseResponse MapToResponse(Course c)
        {
            return new GetCourseResponse
            {
                Id = c.Id,
                CreatorId = c.CreatorId,
                Title = c.Title,
                Description = c.Description,
                TotalJoined = c.TotalJoined,
                ImageUrl = c.ImageUrl,
                Price = c.Price,
                Category = c.Category,
                Level = c.Level,
                IsPublished = c.IsPublished,
                Sections = c.Sections?.Select(s => new SectionResponse
                {
                    Id = s.Id,
                    CourseId = c.Id,
                    Position = s.Position,
                    Title = s.Title,
                    Description = s.Description,
                    Topics = s.Topics?.Select(t => new TopicResponse
                    {
                        Id = t.Id,
                        Title = t.Title,
                        SectionId = t.SectionId,
                        Type = t.Type
                    }).ToList() ?? new List<TopicResponse>()
                }).ToList()
            };
        }

        public static TopicDTO ToDTO(Topic topic)
        {
            return new TopicDTO
            {
                Id = topic.Id,
                Title = topic.Title ?? "No Title",
                Type = topic.Type,
                SectionId = topic.SectionId,
            };
        }
    }
}