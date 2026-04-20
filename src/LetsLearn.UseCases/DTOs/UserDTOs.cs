using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class UserResponse
    {
        [Required]
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Email cannot be empty")]
        [EmailAddress(ErrorMessage = "Email is not valid")]
        public string Email { get; set; } = null!;

        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
        public string? Password { get; set; }

        [Required(ErrorMessage = "Username cannot be empty")]
        [MinLength(6, ErrorMessage = "Username must be at least 6 characters long")]
        public string Username { get; set; } = null!;

        public string? Role { get; set; }
        public string? Avatar { get; set; }

        public List<GetCourseResponse>? Courses { get; set; }
    }

    public class EnrollmentDTO
    {
        public Guid StudentId { get; set; }
        public string CourseId { get; set; } = null!;
        public DateTime JoinDate { get; set; }
    }

    public class GetUserResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string? Password { get; set; }
        public string Username { get; set; } = null!;
        public string? Role { get; set; }
        public string? Avatar { get; set; }
        public List<EnrollmentDTO>? Enrollments { get; set; }
    }

    public class UpdateUserResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string? Password { get; set; }
        public string Username { get; set; } = null!;
        public string? Role { get; set; }
        public string? Avatar { get; set; }
        public List<Enrollment>? Enrollments { get; set; }
    }

    public class UpdateUserDTO
    {
        [MinLength(6, ErrorMessage = "Username must be at least 6 characters long")]
        public string? Username { get; set; }

        public string? Avatar { get; set; }
    }

    public class UpdatePasswordDTO
    {
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
        public string OldPassword { get; set; } = null!;

        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
        public string NewPassword { get; set; } = null!;
    }

    public class StudentReportDTO
    {
        public class TopicInfo
        {
            // public TopicDTO Topic { get; set; }
            public Topic Topic { get; set; }
            public Guid? ResponseId { get; set; }
            public double? Mark { get; set; }
            public DateTime? DoneTime { get; set; }

            public TopicInfo() { }

            public TopicInfo(Topic topic, Guid? responseId, double? mark, DateTime? doneTime)
            {
                Topic = topic;
                ResponseId = responseId;
                Mark = mark;
                DoneTime = doneTime;
            }
        }

        public double TotalQuizCount { get; set; }
        public double TotalAssignmentCount { get; set; }

        public double QuizToDoCount { get; set; }
        public double AssignmentToDoCount { get; set; }

        public double AvgQuizMark { get; set; }
        public double AvgAssignmentMark { get; set; }

        public List<TopicInfo> TopTopicQuiz { get; set; } = new List<TopicInfo>();
        public List<TopicInfo> TopTopicAssignment { get; set; } = new List<TopicInfo>();
    }
}
