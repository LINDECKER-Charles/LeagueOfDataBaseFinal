using System.Runtime.InteropServices;

namespace LoDb.Api.Cli;

/// <summary>
/// Cancellation raised by SIGINT or SIGTERM, so that Ctrl+C or <c>docker stop</c> ends a
/// command at its next cancellation check instead of killing it mid-write.
/// </summary>
internal sealed class ShutdownSignal : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private readonly PosixSignalRegistration[] _registrations;

    public ShutdownSignal() =>
        _registrations =
        [
            PosixSignalRegistration.Create(PosixSignal.SIGINT, Cancel),
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, Cancel),
        ];

    public CancellationToken Token => _source.Token;

    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _source.Dispose();
    }

    private void Cancel(PosixSignalContext context)
    {
        // The default handling would end the process at once; the command stops on its own.
        context.Cancel = true;
        _source.Cancel();
    }
}
