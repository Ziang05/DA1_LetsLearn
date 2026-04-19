using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LetsLearn.Core.Entities;
namespace LetsLearn.Core.Interfaces
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        Task<int> CommitAsync();
    }
}
