using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Helpers;
using Microsoft.Extensions.Configuration;

namespace LetsLearn.UseCases.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IConfiguration _configuration;

        public PaymentService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreatePaymentUrl(string ipAddress, Payment payment)
        {
            var vnpay = new VnpayLibrary();
            var vnpayConfig = _configuration.GetSection("Vnpay");

            vnpay.AddRequestData("vnp_Version", vnpayConfig["Version"]!);
            vnpay.AddRequestData("vnp_Command", vnpayConfig["Command"]!);
            vnpay.AddRequestData("vnp_TmnCode", vnpayConfig["TmnCode"]!);
            vnpay.AddRequestData("vnp_Amount", ((long)(payment.Amount * 100)).ToString());
            vnpay.AddRequestData("vnp_CreateDate", payment.CreatedAt.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", vnpayConfig["CurrCode"]!);
            vnpay.AddRequestData("vnp_IpAddr", ipAddress);
            vnpay.AddRequestData("vnp_Locale", vnpayConfig["Locale"]!);
            vnpay.AddRequestData("vnp_OrderInfo", $"Payment for course {payment.CourseId}");
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", vnpayConfig["ReturnUrl"]!);
            vnpay.AddRequestData("vnp_TxnRef", payment.OrderId!);

            return vnpay.CreateRequestUrl(vnpayConfig["BaseUrl"]!, vnpayConfig["HashSecret"]!);
        }

        public bool ValidateCallback(IDictionary<string, string> query)
        {
            var vnpay = new VnpayLibrary();
            var vnpayConfig = _configuration.GetSection("Vnpay");

            foreach (var kv in query)
            {
                if (!string.IsNullOrEmpty(kv.Key) && kv.Key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(kv.Key, kv.Value);
                }
            }

            var vnp_SecureHash = query.ContainsKey("vnp_SecureHash") ? query["vnp_SecureHash"] : string.Empty;
            return vnpay.ValidateSignature(vnp_SecureHash, vnpayConfig["HashSecret"]!);
        }
    }
}
