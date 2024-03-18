namespace TeilOne.Threading.DistributedCancellationToken.Base.Tests
{
    [TestClass]
    public class BaseDistributedCancellationTokenSourceFactoryTest
    {
        [TestMethod]
        public async Task WhenCreate2CtsWithSameName_ThenTheyAreDifferent()
        {
            // Arrange
            var factory = new BaseDistributedCancellationTokenSourceFactory();

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
            var factory = new BaseDistributedCancellationTokenSourceFactory();

            // Act
            await factory.Create("Cancellation1");

            // Assert
            Assert.IsTrue(factory.ActiveCancellations.Contains("Cancellation1"));
        }

        [TestMethod]
        public async Task WhenCtsIsDisposed_ThenItIsRemovedAfterCleanup()
        {
            // Arrange
            var cleanupInterval = TimeSpan.FromMilliseconds(50);

            var factory = new BaseDistributedCancellationTokenSourceFactory(cleanupInterval);
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
            var cleanupInterval = TimeSpan.FromMilliseconds(500);

            var factory = new BaseDistributedCancellationTokenSourceFactory(cleanupInterval);
            var cts = await factory.Create("Cancellation1");

            // Act
            cts.Dispose();

            // Assert
            await Task.Delay(50);
            await factory.Create("Cancellation2");

            Assert.IsTrue(factory.ActiveCancellations.Contains("Cancellation1"));
        }
        [TestMethod]
        public async Task GivenSameFactory_WhenCancelled_ThenCancelledAllWithTheSameName()
        {
            // Arrange
            var factory = new BaseDistributedCancellationTokenSourceFactory();
            var cts1 = await factory.Create("Cancellation1");

            var cts2 = await factory.Create("Cancellation1");

            var cts3 = await factory.Create("Cancellation1");

            // Act
            cts1.Cancel();

            // Assert
            await Task.Delay(10);

            Assert.IsTrue(cts2.IsCancellationRequested);
            Assert.IsTrue(cts3.IsCancellationRequested);
        }

        [TestMethod]
        public async Task GivenSameFactory_WhenCancelled_ThenCancelledOnlyWithTheSameName()
        {
            // Arrange
            var factory = new BaseDistributedCancellationTokenSourceFactory();
            var cts1 = await factory.Create("Cancellation1");
            var cts2 = await factory.Create("Cancellation1");

            var cts3 = await factory.Create("Cancellation2");

            // Act
            cts1.Cancel();

            // Assert
            await Task.Delay(10);

            Assert.IsTrue(cts2.IsCancellationRequested);

            Assert.IsFalse(cts3.IsCancellationRequested);
        }
    }
}