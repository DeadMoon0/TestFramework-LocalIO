using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestFramework.LocalIO;

/// <summary>
/// Reads a redirected pipe as it is written, so what arrived so far can be taken without waiting for its end.
/// </summary>
/// <remarks>
/// <para>
/// <c>ReadToEndAsync</c> hands over nothing until the pipe closes, and a child the command left running can hold it
/// open indefinitely. Reading in chunks keeps the output a step already received when it stops waiting.
/// </para>
/// <para>
/// Bounded, and the end is what is kept. A command that writes without stopping would otherwise grow the capture -
/// and the step result, the variables bound from it and the debugger signals carrying them - without limit, and
/// what explains a failure is almost always the last thing a command wrote.
/// </para>
/// </remarks>
internal sealed class PipeCapture
{
    /// <summary>
    /// The most of one stream a command result keeps: 1 MiB of text. Anything earlier is dropped and counted.
    /// </summary>
    internal const int Limit = 1024 * 1024;

    private readonly StringBuilder _text = new();
    private readonly object _gate = new();
    private readonly int _limit;
    private long _dropped;

    private PipeCapture(StreamReader reader, int limit, CancellationToken cancellationToken)
    {
        _limit = limit;
        Completion = ReadAsync(reader, cancellationToken);
    }

    /// <summary>Completes when the pipe closes, or faults when reading is cancelled.</summary>
    public Task Completion { get; }

    /// <summary>
    /// Everything kept so far: the last <see cref="Limit"/> characters, led by a line saying how many came
    /// before them when any were dropped.
    /// </summary>
    public string Text
    {
        get
        {
            lock (_gate)
            {
                long dropped = _dropped + System.Math.Max(0, _text.Length - _limit);
                string kept = _text.Length > _limit ? _text.ToString(_text.Length - _limit, _limit) : _text.ToString();

                return dropped == 0
                    ? kept
                    : $"[{dropped} earlier characters were dropped; the last {_limit} are kept.]\n{kept}";
            }
        }
    }

    public static PipeCapture Start(StreamReader reader, CancellationToken cancellationToken) => new(reader, Limit, cancellationToken);

    internal static PipeCapture Start(StreamReader reader, int limit, CancellationToken cancellationToken) => new(reader, limit, cancellationToken);

    private async Task ReadAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        char[] buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            lock (_gate)
            {
                _text.Append(buffer, 0, read);

                // Trimmed in batches rather than on every chunk, so a long-running writer costs one copy per
                // limit's worth of output instead of one per read.
                if (_text.Length > 2 * _limit)
                {
                    int excess = _text.Length - _limit;
                    _text.Remove(0, excess);
                    _dropped += excess;
                }
            }
        }
    }
}
