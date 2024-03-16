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
    public async Task WhenCreate2CtsWithSameName_ThenTheyAreDifferent()
    {
        // Arrange
        var redisConfiguration = "localhost";
        var channelName = "test-channel";

        var factory = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);

        // Act
        var cts1 = await factory.Create("Cancellation1");
        var cts2 = await factory.Create("Cancellation1");

        // Assert
        Assert.IsFalse(ReferenceEquals(cts1, cts2));
    }

    [TestMethod]
    public async Task WhenCtsIsCreated_ThenItIsActive()
    {
        // Arrange
        var redisConfiguration = "localhost";
        var channelName = "test-channel";

        var factory = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName);

        // Act
        await factory.Create("Cancellation1");

        // Assert
        Assert.IsTrue(factory.ActiveCancellations.Contains("Cancellation1"));
    }

    [TestMethod]
    public async Task WhenCtsIsDisposed_ThenItIsRemovedAfterCleanup()
    {
        // Arrange
        var redisConfiguration = "localhost";
        var channelName = "test-channel";

        var cleanupInterval = TimeSpan.FromMilliseconds(50);

        var factory = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName, cleanupInterval);
        var cts = await factory.Create("Cancellation1");

        // Act
        cts.Dispose();

        // Assert
        await Task.Delay((int)Math.Ceiling(cleanupInterval.TotalMilliseconds));
        await factory.Create("Cancellation2");

        Assert.IsFalse(factory.ActiveCancellations.Contains("Cancellation1"));
    }

    [TestMethod]
    public async Task WhenCtsIsDisposed_ThenItIsNotRemovedBeforeCleanup()
    {
        // Arrange
        var redisConfiguration = "localhost";
        var channelName = "test-channel";

        var cleanupInterval = TimeSpan.FromMilliseconds(500);

        var factory = new RedisDistributedCancellationTokenSourceFactory(redisConfiguration, channelName, cleanupInterval);
        var cts = await factory.Create("Cancellation1");

        // Act
        cts.Dispose();

        // Assert
        await Task.Delay(50);
        await factory.Create("Cancellation2");

        Assert.IsTrue(factory.ActiveCancellations.Contains("Cancellation1"));
    }
}
