using LetsLearn.UseCases.DTOs;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IAuthService
    {
        Task RegisterAsync(SignUpRequest request, HttpContext context);
        Task<JwtTokenResponse> LoginAsync(AuthRequest request, HttpContext context);
        Task RefreshAsync(HttpContext httpContext);
        Task UpdatePasswordAsync(UpdatePassword request, Guid userId);
        void Logout(HttpContext context);
    }
}
