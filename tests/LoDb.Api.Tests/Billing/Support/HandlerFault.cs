namespace LoDb.Api.Tests.Billing.Support;

/// <summary>An armed failure of the handler of completed sessions, spent once thrown.</summary>
public sealed class HandlerFault
{
    private int _armed;

    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    /// <summary>Throws once armed, and disarms.</summary>
    public void ThrowIfArmed()
    {
        if (Interlocked.Exchange(ref _armed, 0) == 1)
        {
            throw new InvalidOperationException("The handler fails in this test.");
        }
    }
}
