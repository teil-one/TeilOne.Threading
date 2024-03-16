using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeilOne.Threading.DistributedCancellationToken.Base
{
    public interface IDistributedCancellationTokenSourceFactory : IDisposable
    {
        Task<CancellationTokenSource> Create(string cancellationName);
    }
}
