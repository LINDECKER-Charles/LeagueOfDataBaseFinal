using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>
/// Types the enums written as names as <c>string</c>: the schema exporter lists their values
/// without a type, and the client generator only exports a typed enum as a runtime array.
/// </summary>
internal sealed class StringEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (schema.Type is null && IsStringEnum(schema.Enum))
        {
            schema.Type = schema.Enum!.Any(static value => value is null)
                ? JsonSchemaType.String | JsonSchemaType.Null
                : JsonSchemaType.String;
        }

        return Task.CompletedTask;
    }

    private static bool IsStringEnum(IList<JsonNode>? values) =>
        values is { Count: > 0 }
        && values.Any(static value => value is not null)
        && values.All(static value =>
            value is null || value.GetValueKind() == JsonValueKind.String);
}
