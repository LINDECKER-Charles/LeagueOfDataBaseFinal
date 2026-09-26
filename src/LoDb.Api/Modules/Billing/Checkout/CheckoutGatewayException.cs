namespace LoDb.Api.Modules.Billing.Checkout;

/// <summary>Stripe refused to open a session, or could not be reached.</summary>
internal sealed class CheckoutGatewayException : Exception
{
    public CheckoutGatewayException()
    {
    }

    public CheckoutGatewayException(string message)
        : base(message)
    {
    }

    public CheckoutGatewayException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
