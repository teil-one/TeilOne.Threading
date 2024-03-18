using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TeilOne.Threading.DistributedCancellationToken.Base;

namespace TeilOne.Threading.DistributedCancellationToken.Redis
{
    public class RedisDistributedCancellationTokenSourceFactory : BaseDistributedCancellationTokenSourceFactory
    {
        private readonly RedisChannel _channel;
        private readonly SemaphoreSlim _channelSubscribtionSemaphore = new SemaphoreSlim(1, 1);
        private readonly ConnectionMultiplexer _connectionMultiplexer;

        private volatile ISubscriber _channelSubscriber;

        private bool _isDisposed;

        public RedisDistributedCancellationTokenSourceFactory(string configuration, string channelName, TimeSpan? cleanupInterval = null)
            : base(cleanupInterval)
        {
            _connectionMultiplexer = ConnectionMultiplexer.Connect(configuration);
            _channel = RedisChannel.Literal(channelName);
        }

        public override async Task<CancellationTokenSource> Create(string cancellationName)
        {
            await SubscribeToChannel();

            var cts = await base.Create(cancellationName);

            cts.Token.Register(async () =>
            {
                if (_cancellationTokenSources.TryGetValue(cancellationName, out var tokensForCancellation))
                {
                    if (tokensForCancellation.TryRemove(cts, out _))
                    {
                        await _channelSubscriber.PublishAsync(_channel, cancellationName);
                    }
                }
            });

            return cts;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _connectionMultiplexer.Dispose();
                }

                _isDisposed = true;
            }

            base.Dispose(disposing);
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
    }
}