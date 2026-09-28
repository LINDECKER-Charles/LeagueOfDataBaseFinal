using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace LoDb.Api.Modules.PublicApi.Http;

/// <summary>
/// How <c>/v1</c> writes JSON: go-api's media type, and names set by an attribute on every
/// property.
/// </summary>
/// <remarks>
/// Kept apart from the options of <c>/api</c>: their naming policy and converters must
/// never reach the public contract.
/// </remarks>
internal static class V1Json
{
    /// <summary>The <c>Content-Type</c> of every JSON answer.</summary>
    public const string ContentType = "application/json; charset=utf-8";

    /// <summary>The media type the OpenAPI document declares.</summary>
    public const string MediaType = "application/json";

    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static V1JsonResult<T> Ok<T>(T value) => new(StatusCodes.Status200OK, value);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            // Letters outside ASCII as they are, as Go writes them; markup stays escaped.
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
