using StackExchange.Redis;
using System.Collections.Concurrent;

namespace TeilOne.Threading.DistributedCancellationToken.Redis.Tests;

[TestClass]
public class RedisDistributedCancellationTokenSourceFactoryTest
{
    [TestMethod]
    public async Task WhenCancelled_ThenCancelledAllWithTheSameName()
    {
        // Arrange
        var redisConfiguration = "localhost";
        var channelName = "test-channel";

        var factory1 = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);
        var f1Cts = await factory1.Create("Cancellation1");

        var factory2 = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);
        var f2Cts = await factory2.Create("Cancellation1");

        var factory3 = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);
        var f3Cts = await factory3.Create("Cancellation1");

        // Act
        f1Cts.Cancel();

        // Assert
        await Task.Delay(10);

        Assert.IsTrue(f2Cts.IsCancellationRequested);
        Assert.IsTrue(f3Cts.IsCancellationRequested);
    }

    [TestMethod]
    public async Task WhenCancelled_ThenCancelledOnlyWithTheSameName()
    {
        // Arrange
        var redisConfiguration = "localhost";
        var channelName = "test-channel";

        var factory1 = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);
        var f1Cts = await factory1.Create("Cancellation1");

        var factory2 = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);
        var f2Cts1 = await factory2.Create("Cancellation1");
        var f2Cts2 = await factory2.Create("Cancellation2");

        var factory3 = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);
        var f3Cts = await factory3.Create("Cancellation2");

        // Act
        f1Cts.Cancel();

        // Assert
        await Task.Delay(10);

        Assert.IsTrue(f2Cts1.IsCancellationRequested);

        Assert.IsFalse(f2Cts2.IsCancellationRequested);
        Assert.IsFalse(f3Cts.IsCancellationRequested);
    }

    [TestMethod]
    public async Task GivenMultipleCancellationsWithSameNameInDifferentFactories_WhenCancelled_ThenOnlyOneMessageIsPublished()
    {
        // Given 3 cancellations with the same name in different factories
        var redisConfiguration = "localhost";
        var channelName = "test-channel";

        var factory1 = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);
        var f1Cts = await factory1.Create("Cancellation1");

        var factory2 = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);
        var f2Cts = await factory2.Create("Cancellation1");

        var factory3 = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);
        var f3Cts = await factory3.Create("Cancellation1");

        using var connectionMultiplexer = ConnectionMultiplexer.Connect(redisConfiguration);
        var channelSubscriber = connectionMultiplexer.GetSubscriber();

        var cancellationCalls = new ConcurrentDictionary<string, int>();
        await channelSubscriber.SubscribeAsync(RedisChannel.Literal(channelName), (_, message) =>
        {
            cancellationCalls.AddOrUpdate(message.ToString(), 1, (cancellationName, calls) => ++calls);
        });

        // When one token is cancelled
        f1Cts.Cancel();

        await Task.Delay(10);

        // Then only one Redis cancellation message is sent
        var calls = cancellationCalls["Cancellation1"];
        Assert.AreEqual(1, calls);

        Assert.IsTrue(f2Cts.IsCancellationRequested);
        Assert.IsTrue(f3Cts.IsCancellationRequested);
    }

    [TestMethod]
    public async Task GivenMultipleCancellationsWithSameNameInSameFactory_WhenCancelled_ThenOnlyOneMessageIsPublished()
    {
        // Given 3 cancellations with the same name in the same factory
        var redisConfiguration = "localhost";
        var channelName = "test-channel";

        var factory = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);
        var cts1 = await factory.Create("Cancellation1");
        var cts2 = await factory.Create("Cancellation1");
        var cts3 = await factory.Create("Cancellation1");

        using var connectionMultiplexer = ConnectionMultiplexer.Connect(redisConfiguration);
        var channelSubscriber = connectionMultiplexer.GetSubscriber();

        var cancellationCalls = new ConcurrentDictionary<string, int>();
        await channelSubscriber.SubscribeAsync(RedisChannel.Literal(channelName), (_, message) =>
        {
            cancellationCalls.AddOrUpdate(message.ToString(), 1, (cancellationName, calls) => ++calls);
        });

        // When one token is cancelled
        cts1.Cancel();

        await Task.Delay(10);

        // Then only one Redis cancellation message is sent
        var calls = cancellationCalls["Cancellation1"];
        Assert.AreEqual(1, calls);

        Assert.IsTrue(cts2.IsCancellationRequested);
        Assert.IsTrue(cts3.IsCancellationRequested);
    }
}
