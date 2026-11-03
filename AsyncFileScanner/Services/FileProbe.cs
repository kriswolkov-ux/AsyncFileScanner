using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncFileScanner.Services;

public sealed class FileProbe
{
    public async Task<int> ReadHeaderAsync(
        string path,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1024];

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
    }
}