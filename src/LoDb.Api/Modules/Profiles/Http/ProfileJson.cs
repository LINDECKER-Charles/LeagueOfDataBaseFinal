using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Api.Modules.Profiles.Deletion;
using LoDb.Api.Modules.Profiles.Favorites;
using LoDb.Api.Modules.Profiles.Favorites.Views;
using LoDb.Api.Modules.Profiles.Showcase;

namespace LoDb.Api.Modules.Profiles.Http;

/// <summary>
/// The wire form of the profile's enums: camelCase names, never numbers, so the generated
/// client gets them as enums.
/// </summary>
internal static class ProfileJson
{
    public static IServiceCollection AddProfileJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(static options =>
        {
            var converters = options.SerializerOptions.Converters;
            converters.Add(Names<FavoriteSlot>());
            converters.Add(Names<FavoriteStatus>());
            converters.Add(Names<BackdropKind>());
            converters.Add(Names<DeletionConfirmation>());
        });

    private static JsonStringEnumConverter<TEnum> Names<TEnum>()
        where TEnum : struct, Enum =>
        new(JsonNamingPolicy.CamelCase, allowIntegerValues: false);
}
