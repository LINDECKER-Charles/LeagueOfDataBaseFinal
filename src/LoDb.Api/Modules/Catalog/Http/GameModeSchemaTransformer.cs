using System.Text.Json.Nodes;
using LoDb.Domain.Catalog.Modes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>
/// Documents <see cref="GameMode"/> as the enum of its codes, which a custom converter
/// writes and the schema exporter therefore cannot describe on its own.
/// </summary>
internal sealed class GameModeSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        if (context.JsonTypeInfo.Type == typeof(GameMode))
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = null;
            schema.Enum = [.. GameModes.All.Select(static mode =>
                (JsonNode)JsonValue.Create(GameModes.Code(mode)))];
        }

        return Task.CompletedTask;
    }
}
