using AAALogistics.Host.Hosting;

namespace AAALogistics.Integration.Tests;

public sealed class SingleInstanceLockTests
{
    [Fact]
    public void Second_Acquire_Fails_Until_The_First_Lock_Is_Released()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "aaa-lock-" + Guid.NewGuid().ToString("N"));

        using (var first = SingleInstanceLock.Acquire(directory))
        {
            var error = Assert.Throws<InvalidOperationException>(
                () => SingleInstanceLock.Acquire(directory));
            Assert.Contains(
                SingleInstanceLock.FileName,
                error.Message,
                StringComparison.Ordinal);
            Assert.True(File.Exists(first.LockPath));
        }

        using var second = SingleInstanceLock.Acquire(directory);
        Assert.Equal(
            Path.Combine(directory, SingleInstanceLock.FileName),
            second.LockPath);
    }
}
