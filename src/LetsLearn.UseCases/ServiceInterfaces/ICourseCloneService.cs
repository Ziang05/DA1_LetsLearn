using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface ICourseCloneService
    {
        Task<CloneCourseResponse> CloneAsync(
            string sourceCourseId,
            CloneCourseRequest request,
            Guid userId,
            CancellationToken ct = default);
    }
}
