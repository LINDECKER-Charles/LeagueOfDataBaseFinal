namespace LoDb.Api.Modules.Billing.Webhooks;

/// <summary>The legacy stack's answer to a recorded event: <c>{"received": true}</c>.</summary>
internal sealed record WebhookReceipt(bool Received);
