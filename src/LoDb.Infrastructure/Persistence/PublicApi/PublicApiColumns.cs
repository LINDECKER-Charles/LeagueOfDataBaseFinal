using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.PublicApi;

/// <summary>Stored spelling of the grant sources, shared by the model and the raw SQL.</summary>
public static class PublicApiColumns
{
    internal static readonly ValueConverter<ApiCreditGrantSource, string> SourceConverter = new(
        source => ToText(source),
        value => ParseSource(value));

    public static string ToText(ApiCreditGrantSource source) => source switch
    {
        ApiCreditGrantSource.Purchase => "purchase",
        ApiCreditGrantSource.Migration => "migration",
        ApiCreditGrantSource.Reconciliation => "reconciliation",
        ApiCreditGrantSource.Admin => "admin",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };

    public static ApiCreditGrantSource ParseSource(string value) => value switch
    {
        "purchase" => ApiCreditGrantSource.Purchase,
        "migration" => ApiCreditGrantSource.Migration,
        "reconciliation" => ApiCreditGrantSource.Reconciliation,
        "admin" => ApiCreditGrantSource.Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
