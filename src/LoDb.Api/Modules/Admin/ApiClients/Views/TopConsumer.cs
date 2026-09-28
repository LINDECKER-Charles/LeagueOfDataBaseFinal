namespace LoDb.Api.Modules.Admin.ApiClients.Views;

/// <summary>A key among the most used of the last thirty days.</summary>
internal sealed record TopConsumer
{
    public required int Id { get; init; }

    public required string KeyPrefix { get; init; }

    public required string Username { get; init; }

    public required long Requests { get; init; }
}
