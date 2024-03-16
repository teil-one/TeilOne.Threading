using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeilOne.Threading.DistributedCancellationToken.Redis
{
    public sealed class RedisDistributedCancellationTokenSourceFactory : IDisposable
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<CancellationTokenSource, bool>> _cancellationTokenSources = new ConcurrentDictionary<string, ConcurrentDictionary<CancellationTokenSource, bool>>();
        private readonly SemaphoreSlim _channelSubscribtionSemaphore = new SemaphoreSlim(1, 1);

        private readonly RedisChannel _channel;
        private readonly ConnectionMultiplexer _connectionMultiplexer;

        private readonly DateTime _lastCleanup = DateTime.Now;
        private readonly TimeSpan _cleanupInterval;
        private readonly SemaphoreSlim _cleanupSemaphore = new SemaphoreSlim(1, 1);

        private volatile ISubscriber _channelSubscriber;

        public ICollection<string> ActiveCancellations => _cancellationTokenSources.Keys;

        public RedisDistributedCancellationTokenSourceFactory(string configuration, string channelName, TimeSpan? cleanupInterval = null)
        {
            _connectionMultiplexer = ConnectionMultiplexer.Connect(configuration);
            _channel = RedisChannel.Literal(channelName);

            _cleanupInterval = cleanupInterval ?? TimeSpan.FromSeconds(60);
        }

        public async Task<CancellationTokenSource> Create(string cancellationName)
        {
            await CleanupIfNeeded();

            await SubscribeToChannel();

            var tokensForCancellation = _cancellationTokenSources.GetOrAdd(cancellationName, key => new ConcurrentDictionary<CancellationTokenSource, bool>());

            var cts = new CancellationTokenSource();

            cts.Token.Register(async () =>
            {
                if (tokensForCancellation.TryRemove(cts, out _))
                {
                    await _channelSubscriber.PublishAsync(_channel, cancellationName);
                }
            });

            tokensForCancellation.TryAdd(cts, true);

            return cts;
        }

        public void Dispose()
        {
            _connectionMultiplexer.Dispose();
        }

        private async Task SubscribeToChannel()
        {
            if (_channelSubscriber != null)
            {
                return;
            }

            await _channelSubscribtionSemaphore.WaitAsync();

            try
            {
                if (_channelSubscriber != null)
                {
                    return;
                }

                _channelSubscriber = _connectionMultiplexer.GetSubscriber();

                await _channelSubscriber.SubscribeAsync(_channel, async (_, message) =>
                {
                    var cancellationName = (string)message;

                    if (_cancellationTokenSources.TryRemove(cancellationName, out var tokensForCancellation))
                    {
                        await _channelSubscriber.PublishAsync(_channel, cancellationName);

                        foreach (var ctsItem in tokensForCancellation)
                        {
                            var cts = ctsItem.Key;

                            // Remove before cancelling so that the message is not published to Redis
                            if (tokensForCancellation.TryRemove(cts, out var x))
                            {
                                cts.Cancel();
                            }
                        }
                    }
                });
            }
            finally
            {
                _channelSubscribtionSemaphore.Release();
            }
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
                    catch(ObjectDisposedException)
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
    }
}