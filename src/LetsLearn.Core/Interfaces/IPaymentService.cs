using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LetsLearn.Core.Entities;

namespace LetsLearn.Core.Interfaces
{
    public interface IPaymentService
    {
        string CreatePaymentUrl(string ipAddress, Payment payment);
        bool ValidateCallback(IDictionary<string, string> query);
    }
}
