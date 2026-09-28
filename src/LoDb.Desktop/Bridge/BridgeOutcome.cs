using System.Text.Json.Nodes;

namespace LoDb.Desktop.Bridge;

/// <summary>What a command answers: a result object, or an error code.</summary>
internal sealed record BridgeOutcome
{
    private const string IdMember = "id";
    private const string OkMember = "ok";
    private const string ResultMember = "result";
    private const string ErrorMember = "error";

    public JsonObject? Result { get; private init; }

    /// <summary>One of <see cref="BridgeErrors"/>; null on success.</summary>
    public string? Error { get; private init; }

    public static BridgeOutcome Success(JsonObject result) => new() { Result = result };

    public static BridgeOutcome Success() => Success([]);

    public static BridgeOutcome Failure(string error) => new() { Error = error };

    /// <summary>The reply message: <c>{id, ok, result}</c> or <c>{id, ok, error}</c>.</summary>
    public string ToReply(string id)
    {
        var reply = new JsonObject { [IdMember] = id, [OkMember] = Error is null };
        if (Error is null)
        {
            reply[ResultMember] = Result?.DeepClone() ?? new JsonObject();
        }
        else
        {
            reply[ErrorMember] = Error;
        }

        return reply.ToJsonString();
    }
}
