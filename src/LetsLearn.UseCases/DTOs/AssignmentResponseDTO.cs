using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class UpdateAssignmentResponseRequest
    {
        public Guid TopicId { get; set; }
        public Guid StudentId { get; set; } 
        public AssignmentResponseData Data { get; set; }
    }

    //public class CreateAssignmentResponseRequest
    //{
    //    public Guid TopicId { get; set; }
    //    public AssignmentResponseData Data { get; set; }
    //}

    public class AssignmentResponseDTO
    {
        public Guid Id { get; set; }
        public Guid TopicId { get; set; }
        public Guid StudentId { get; set; }

        public AssignmentResponseData Data { get; set; }
    }

    public class AssignmentResponseData
    {
        public DateTime? SubmittedAt { get; set; }
        public List<CloudinaryFile> Files { get; set; } = new();
        public decimal? Mark { get; set; }
        public string? Note { get; set; }
    }

    public class CreateAssignmentResponseRequest
    {
        public Guid TopicId { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public List<CreateCloudinaryFileRequest> CloudinaryFiles { get; set; } = new();
        public decimal? Mark { get; set; }
        public string? Note { get; set; }
    }

    public class CreateCloudinaryFileRequest
    {
        public string? Name { get; set; }
        public string? DisplayUrl { get; set; }
        public string? DownloadUrl { get; set; }
    }
}

