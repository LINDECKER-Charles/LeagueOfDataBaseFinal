using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Api.Hosting.OpenApi;
using LoDb.Api.Modules.Catalog.WarmUp.Progress;
using LoDb.Domain.Catalog;
using LoDb.Domain.Derived.Items;
using LoDb.Domain.Derived.Ranges;
using LoDb.Domain.Derived.Stats;
using LoDb.Domain.Editions;
using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog.Hotlinks;
using LoDb.Ingestion.Catalog.Images;
using Microsoft.AspNetCore.OpenApi;

namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>
/// The wire form of the domain enums under <c>/api</c>: names, never numbers, so the OpenAPI
/// document lists their values and the generated client gets them as enums.
/// </summary>
/// <remarks>
/// Registered on the API's JSON options rather than per property: the other modules write
/// the same enums (a build's mode, an item's edition) and must write them alike. Each enum
/// keeps the case its domain documents: kebab-case for the locales, which is their URL
/// segment; snake_case for the stats, the vocabulary of the translation keys; the persisted
/// codes for the game modes; camelCase otherwise.
/// Numbers are strict: the web defaults also read them from strings, which the OpenAPI
/// document then declares as <c>[integer, string]</c> and the client as <c>number | string</c>,
/// although the API only ever writes numbers.
/// </remarks>
internal static class CatalogJson
{
    public static IServiceCollection AddCatalogJson(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            AddConverters(options.SerializerOptions.Converters);
        });
        services.Configure<OpenApiOptions>(
            OpenApiDocuments.App,
            static options => options
                .AddSchemaTransformer<GameModeSchemaTransformer>()
                .AddSchemaTransformer<StringEnumSchemaTransformer>());
        return services;
    }

    private static void AddConverters(IList<JsonConverter> converters)
    {
        var camel = JsonNamingPolicy.CamelCase;
        converters.Add(new JsonStringEnumConverter<Edition>(camel, allowIntegerValues: false));
        converters.Add(new JsonStringEnumConverter<ResourceType>(camel, allowIntegerValues: false));
        converters.Add(new JsonStringEnumConverter<ItemTier>(camel, allowIntegerValues: false));
        converters.Add(
            new JsonStringEnumConverter<AttackRangeClass>(camel, allowIntegerValues: false));
        converters.Add(new JsonStringEnumConverter<ImageStatus>(camel, allowIntegerValues: false));
        converters.Add(new JsonStringEnumConverter<AbilitySlot>(camel, allowIntegerValues: false));
        converters.Add(new JsonStringEnumConverter<WarmUpStage>(camel, allowIntegerValues: false));
        converters.Add(new JsonStringEnumConverter<GameStat>(
            JsonNamingPolicy.SnakeCaseLower,
            allowIntegerValues: false));
        converters.Add(new JsonStringEnumConverter<UiLocale>(
            JsonNamingPolicy.KebabCaseLower,
            allowIntegerValues: false));
        converters.Add(new GameModeJsonConverter());
    }
}
