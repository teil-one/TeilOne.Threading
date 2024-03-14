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
}