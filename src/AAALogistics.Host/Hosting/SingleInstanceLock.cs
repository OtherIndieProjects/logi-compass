namespace AAALogistics.Host.Hosting;

/// <summary>
/// Exclusive file lock so only one server process uses a data directory.
/// </summary>
public sealed class SingleInstanceLock : IDisposable
{
    public const string FileName = "aaa-host.lock";

    public const string DisableEnvironmentVariable = "AAA_DISABLE_INSTANCE_LOCK";

    private readonly FileStream _stream;
    private bool _disposed;

    private SingleInstanceLock(FileStream stream, string lockPath)
    {
        _stream = stream;
        LockPath = lockPath;
    }

    public string LockPath { get; }

    public static bool IsDisabledByEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(DisableEnvironmentVariable);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    public static SingleInstanceLock Acquire(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        var lockPath = Path.Combine(dataDirectory, FileName);

        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            stream.SetLength(0);
            var payload = System.Text.Encoding.UTF8.GetBytes(
                Environment.ProcessId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            stream.Write(payload);
            stream.Flush(flushToDisk: true);

            var acquired = new SingleInstanceLock(stream, lockPath);
            stream = null;
            return acquired;
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                "No se pudo obtener el bloqueo de instancia en "
                + lockPath
                + ". Otra instancia de AAA Logistics ya está en ejecución "
                + "o el fichero no se puede bloquear.",
                exception);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
    }
}
