using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface ISectionService
    {
        Task<SectionResponse> CreateSectionAsync(CreateSectionRequest request, CancellationToken ct = default);
        Task<SectionResponse> GetSectionByIdAsync(Guid sectionId, CancellationToken ct = default);
        Task<SectionResponse> UpdateSectionAsync(UpdateSectionRequest request, CancellationToken ct = default);
    }
}
