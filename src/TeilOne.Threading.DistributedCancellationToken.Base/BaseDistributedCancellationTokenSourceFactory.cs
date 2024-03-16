using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeilOne.Threading.DistributedCancellationToken.Base
{
    public class BaseDistributedCancellationTokenSourceFactory : IDistributedCancellationTokenSourceFactory
    {
        protected readonly ConcurrentDictionary<string, ConcurrentDictionary<CancellationTokenSource, bool>> _cancellationTokenSources = new ConcurrentDictionary<string, ConcurrentDictionary<CancellationTokenSource, bool>>();

        private readonly DateTime _lastCleanup = DateTime.Now;
        private readonly TimeSpan _cleanupInterval;
        private readonly SemaphoreSlim _cleanupSemaphore = new SemaphoreSlim(1, 1);

        public ICollection<string> ActiveCancellations => _cancellationTokenSources.Keys;

        public BaseDistributedCancellationTokenSourceFactory(TimeSpan? cleanupInterval = null)
        {
            _cleanupInterval = cleanupInterval ?? TimeSpan.FromSeconds(60);
        }

        public virtual async Task<CancellationTokenSource> Create(string cancellationName)
        {
            await CleanupIfNeeded();

            var tokensForCancellation = _cancellationTokenSources.GetOrAdd(cancellationName, key => new ConcurrentDictionary<CancellationTokenSource, bool>());

            var cts = new CancellationTokenSource();

            tokensForCancellation.TryAdd(cts, true);

            return cts;
        }

        private void RemoveDisposedCancellationTokenSources()
        {
            foreach (var tokensForCancellationItem in _cancellationTokenSources)
            {
                var tokensForCancellation = tokensForCancellationItem.Value;
                foreach (var ctsItem in tokensForCancellation)
                {
                    var cts = ctsItem.Key;
                    try
                    {
                        _ = cts.Token;
                    }
                    catch (ObjectDisposedException)
                    {
                        tokensForCancellation.TryRemove(cts, out _);
                        if (tokensForCancellation.IsEmpty)
                        {
                            _cancellationTokenSources.TryRemove(tokensForCancellationItem.Key, out _);
                        }
                    }
                }
            }
        }

        private async Task CleanupIfNeeded()
        {
            await _cleanupSemaphore.WaitAsync();

            try
            {
                if (DateTime.Now.Subtract(_lastCleanup) >= _cleanupInterval)
                {
                    RemoveDisposedCancellationTokenSources();
                }
            }
            finally
            {
                _cleanupSemaphore.Release();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
