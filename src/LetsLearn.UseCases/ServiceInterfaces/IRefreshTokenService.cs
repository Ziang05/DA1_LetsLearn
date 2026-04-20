using LetsLearn.UseCases.DTOs;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IRefreshTokenService
    {
        Task<string> CreatRefreshTokenAsync(Guid userId, string role);
        Task RefreshTokenAsync(HttpContext context);
    }
}
