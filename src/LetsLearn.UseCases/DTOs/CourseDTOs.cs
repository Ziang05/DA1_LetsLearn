using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class AddStudentRequest
    {
        [Required]
        public string StudentId { get; set; } = string.Empty;
    }

    public class CreateCourseRequest
    {
        public required String Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? Category { get; set; }
        public string? Level { get; set; }
        public decimal? Price { get; set; }
        public bool? IsPublished { get; set; }
    }

    public class UpdateCourseRequest
    {
        public required String Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? Category { get; set; }
        public string? Level { get; set; }
        public decimal? Price { get; set; }
        public bool? IsPublished { get; set; }
    }

    public class CreateCourseResponse
    {
        public required String Id { get; set; }
        public Guid CreatorId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int TotalJoined { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? Price { get; set; }
        public string? Category { get; set; }
        public string? Level { get; set; }
        public bool? IsPublished { get; set; }
    }

    public class UpdateCourseResponse
    {
        public required String Id { get; set; }
        public Guid CreatorId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int TotalJoined { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? Price { get; set; }
        public string? Category { get; set; }
        public string? Level { get; set; }
        public bool? IsPublished { get; set; }

        public List<SectionResponse>? Sections { get; set; }
    }

    public class GetCourseResponse
    {
        public required String Id { get; set; }
        public Guid CreatorId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int TotalJoined { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? Price { get; set; }
        public string? Category { get; set; }
        public string? Level { get; set; }
        public bool IsPublished { get; set; }

        public List<SectionResponse>? Sections { get; set; }
        public UserBasicInfo? Creator { get; set; }
        public List<UserBasicInfo>? Students { get; set; }
    }
    public class UserBasicInfo
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = null!;
        public string? Avatar { get; set; }
    }
    public class AllAssignmentsReportDTO
    {
        public class StudentInfoWithAverageMark
        {
            public GetUserResponse User { get; set; }
            public double? AverageMark { get; set; }
            public bool Submitted { get; set; }
        }

        public int AssignmentsCountInProgress { get; set; }
        public int AssignmentCount { get; set; }
        public double AvgMark { get; set; }
        public double AvgCompletionRate { get; set; }
        public int NumberOfAssignmentEndsAtThisMonth { get; set; }
        public DateTime? ClosestNextEndAssignment { get; set; }
        public Dictionary<int, int> MarkDistributionCount { get; set; } = new Dictionary<int, int>();
        public List<StudentInfoWithAverageMark> StudentInfoWithMarkAverage { get; set; } = new List<StudentInfoWithAverageMark>();
        public List<StudentInfoWithAverageMark> StudentWithMarkOver8 { get; set; } = new List<StudentInfoWithAverageMark>();
        public List<StudentInfoWithAverageMark> StudentWithMarkOver5 { get; set; } = new List<StudentInfoWithAverageMark>();
        public List<StudentInfoWithAverageMark> StudentWithMarkOver2 { get; set; } = new List<StudentInfoWithAverageMark>();
        public List<StudentInfoWithAverageMark> StudentWithMarkOver0 { get; set; } = new List<StudentInfoWithAverageMark>();
        public List<StudentInfoWithAverageMark> StudentWithNoResponse { get; set; } = new List<StudentInfoWithAverageMark>();
        public Dictionary<string, long> FileTypeCount { get; set; } = new Dictionary<string, long>();
        public List<SingleAssignmentReportDTO> SingleAssignmentReports { get; set; } = new List<SingleAssignmentReportDTO>();
    }

    public class AllQuizzesReportDTO
    {
        public double QuizCount { get; set; }
        public double AvgCompletionPercentage { get; set; }

        public double MinQuestionCount { get; set; }
        public double MaxQuestionCount { get; set; }
        public double MinStudentScoreBase10 { get; set; }
        public double MaxStudentScoreBase10 { get; set; }

        public List<SingleQuizReportDTO.StudentInfoAndMarkQuiz> StudentInfoWithMarkAverage { get; set; } = new List<SingleQuizReportDTO.StudentInfoAndMarkQuiz>();

        public List<SingleQuizReportDTO.StudentInfoAndMarkQuiz> StudentWithMarkOver8 { get; set; } = new List<SingleQuizReportDTO.StudentInfoAndMarkQuiz>();
        public List<SingleQuizReportDTO.StudentInfoAndMarkQuiz> StudentWithMarkOver5 { get; set; } = new List<SingleQuizReportDTO.StudentInfoAndMarkQuiz>();
        public List<SingleQuizReportDTO.StudentInfoAndMarkQuiz> StudentWithMarkOver2 { get; set; } = new List<SingleQuizReportDTO.StudentInfoAndMarkQuiz>();
        public List<SingleQuizReportDTO.StudentInfoAndMarkQuiz> StudentWithMarkOver0 { get; set; } = new List<SingleQuizReportDTO.StudentInfoAndMarkQuiz>();
        public List<SingleQuizReportDTO.StudentInfoAndMarkQuiz> StudentWithNoResponse { get; set; } = new List<SingleQuizReportDTO.StudentInfoAndMarkQuiz>();

        public Dictionary<int, int> MarkDistributionCount { get; set; } = new Dictionary<int, int>();

        public List<SingleQuizReportDTO> SingleQuizReports { get; set; } = new List<SingleQuizReportDTO>();

        public int TrueFalseQuestionCount { get; set; }
        public int MultipleChoiceQuestionCount { get; set; }
        public int ShortAnswerQuestionCount { get; set; }
    }

    public class CloneCourseRequest
    {
        public required string NewCourseId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? Category { get; set; }
        public string? Level { get; set; }
        public decimal? Price { get; set; }
        public bool? IsPublished { get; set; }
    }

    public class CloneCourseResponse
    {
        public string Id { get; set; }
        public string SourceCourseId { get; set; }
        public int SectionCount { get; set; }
        public int TopicCount { get; set; }
    }

    public class CourseCloneResult
    {
        public Course Course { get; set; }
        public Dictionary<Guid, Guid> SectionIdMap { get; set; } = new();
        public Dictionary<Guid, Guid> TopicIdMap { get; set; } = new();
        public Dictionary<Guid, Guid> QuestionIdMap { get; set; } = new();
        public int SectionCount { get; set; }
        public int TopicCount { get; set; }
        public int QuestionCount { get; set; }
    }
}
