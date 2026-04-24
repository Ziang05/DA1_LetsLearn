using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IUnitOfWork _uow;

        public PaymentController(IPaymentService paymentService, IUnitOfWork uow)
        {
            _paymentService = paymentService;
            _uow = uow;
        }

        [HttpGet("create-url")]
        [Authorize]
        public async Task<IActionResult> CreatePaymentUrl([FromQuery] string courseId)
        {
            var userIdStr = User.FindFirst("userID")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var course = await _uow.Course.GetByIdAsync(courseId);
            if (course == null) return NotFound("Course not found");

            if (!course.Price.HasValue || course.Price.Value <= 0)
                return BadRequest("This course is free and does not require payment.");

            // Check if already paid
            var existingPayment = (await _uow.Payments.FindAsync(p => 
                p.UserId == userId && 
                p.CourseId == courseId && 
                p.Status == "Success")).FirstOrDefault();
            
            if (existingPayment != null)
                return BadRequest("You have already paid for this course.");

            var orderId = DateTime.Now.Ticks.ToString();
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CourseId = courseId,
                Amount = course.Price.Value,
                OrderId = orderId,
                Status = "Pending",
                Description = $"Payment for course {course.Title}",
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Payments.AddAsync(payment);
            await _uow.CommitAsync();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var paymentUrl = _paymentService.CreatePaymentUrl(ipAddress, payment);
            return Ok(new { url = paymentUrl });
        }

        [HttpGet("vnpay-return")]
        public async Task<IActionResult> VnpayReturn()
        {
            if (Request.Query.Count > 0)
            {
                var vnpayData = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());
                var orderId = vnpayData.GetValueOrDefault("vnp_TxnRef");
                var vnp_TransactionNo = vnpayData.GetValueOrDefault("vnp_TransactionNo");
                var vnp_ResponseCode = vnpayData.GetValueOrDefault("vnp_ResponseCode");
                var vnp_TransactionStatus = vnpayData.GetValueOrDefault("vnp_TransactionStatus");

                bool isValidSignature = _paymentService.ValidateCallback(vnpayData);

                if (isValidSignature)
                {
                    var payment = (await _uow.Payments.FindAsync(p => p.OrderId == orderId)).FirstOrDefault();
                    if (payment != null && payment.Status == "Pending")
                    {
                        if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
                        {
                            payment.Status = "Success";
                            payment.TransactionId = vnp_TransactionNo;
                            payment.PaidAt = DateTime.UtcNow;
                            
                            // Auto enroll after successful payment
                            try 
                            {
                                var enrollmentExists = await _uow.Enrollments.ExistsAsync(e => e.CourseId == payment.CourseId && e.StudentId == payment.UserId);
                                if (!enrollmentExists)
                                {
                                    var enrollment = new Enrollment
                                    {
                                        StudentId = payment.UserId,
                                        CourseId = payment.CourseId,
                                        JoinDate = DateTime.UtcNow
                                    };
                                    await _uow.Enrollments.AddAsync(enrollment);
                                    
                                    var course = await _uow.Course.GetByIdAsync(payment.CourseId);
                                    if (course != null) course.TotalJoined += 1;
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log error but payment is still success
                                Console.WriteLine($"Enrollment failed after payment: {ex.Message}");
                            }
                        }
                        else
                        {
                            payment.Status = "Failed";
                        }

                        await _uow.CommitAsync();
                        
                        // Redirect to home page with status for notification
                        var frontendUrl = "http://localhost:3000/"; 
                        return Redirect($"{frontendUrl}?paymentStatus={payment.Status}&courseId={payment.CourseId}");
                    }
                }
            }

            return BadRequest("Invalid payment callback");
        }
    }
}
