using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace TeilOne.Threading.DistributedCancellationToken.Redis
{
    public sealed class RedisDistributedCancellationTokenSourceFactory : IDisposable
    {
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokenSources = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly SemaphoreSlim _channelSubscribtionSemaphore = new SemaphoreSlim(1, 1);

        private readonly RedisChannel _channel;
        private readonly ConnectionMultiplexer _connectionMultiplexer;

        private volatile ISubscriber _channelSubscriber;

        public RedisDistributedCancellationTokenSourceFactory(string configuration, string channelName)
        {
            _connectionMultiplexer = ConnectionMultiplexer.Connect(configuration);
            _channel = RedisChannel.Literal(channelName);
        }

        public async Task<CancellationTokenSource> Create(string cancellationName)
        {
            await SubscribeToChannel();

            return _cancellationTokenSources.GetOrAdd(cancellationName, key =>
            {
                var cts = new CancellationTokenSource();

                cts.Token.Register(async () =>
                {
                    if (_cancellationTokenSources.TryRemove(cancellationName, out _))
                    {
                        await _channelSubscriber.PublishAsync(_channel, cancellationName);
                    }
                });

                return cts;
            });
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

                await _channelSubscriber.SubscribeAsync(_channel, (_, message) =>
                {
                    var cancellationName = (string)message;

                    // Remove before cancelling so that the message is not published to Redis
                    if (_cancellationTokenSources.TryRemove(cancellationName, out var cts))
                    {
                        cts.Cancel();
                    }
                });
            }
            finally
            {
                _channelSubscribtionSemaphore.Release();
            }
        }
    }
}