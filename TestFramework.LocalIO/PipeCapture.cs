using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestFramework.LocalIO;

/// <summary>
/// Reads a redirected pipe as it is written, so what arrived so far can be taken without waiting for its end.
/// </summary>
/// <remarks>
/// <c>ReadToEndAsync</c> hands over nothing until the pipe closes, and a child the command left running can hold it
/// open indefinitely. Reading in chunks keeps the output a step already received when it stops waiting.
/// </remarks>
internal sealed class PipeCapture
{
    private readonly StringBuilder _text = new();
    private readonly object _gate = new();

    private PipeCapture(StreamReader reader, CancellationToken cancellationToken)
    {
        Completion = ReadAsync(reader, cancellationToken);
    }

    /// <summary>Completes when the pipe closes, or faults when reading is cancelled.</summary>
    public Task Completion { get; }

    /// <summary>Everything read so far.</summary>
    public string Text
    {
        get
        {
            lock (_gate)
                return _text.ToString();
        }
    }

    public static PipeCapture Start(StreamReader reader, CancellationToken cancellationToken) => new(reader, cancellationToken);

    private async Task ReadAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        char[] buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            lock (_gate)
                _text.Append(buffer, 0, read);
        }
    }
}
