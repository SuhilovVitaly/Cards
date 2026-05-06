namespace Cards.Web.Services;

public sealed class DataFileLock : IAsyncDisposable
{
    private const int RetryDelayMs = 50;

    private readonly FileStream _stream;

    private DataFileLock(FileStream stream)
    {
        _stream = stream;
    }

    public static async Task<DataFileLock> AcquireAsync(string dataDirectory, CancellationToken ct)
    {
        var lockPath = Path.Combine(dataDirectory, ".cards.lock");

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.DeleteOnClose);

                return new DataFileLock(stream);
            }
            catch (IOException)
            {
                await Task.Delay(RetryDelayMs, ct);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        return _stream.DisposeAsync();
    }
}
