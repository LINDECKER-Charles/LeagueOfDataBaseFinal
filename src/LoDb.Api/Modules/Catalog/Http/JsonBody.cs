using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>
/// A JSON body as the API writes it, with its strong <c>ETag</c>: the SHA-256 of its bytes.
/// </summary>
/// <remarks>
/// Two instances tag the same content alike, and an answer that gained an image gets a new
/// tag. Immutable, so an endpoint whose answer seldom changes keeps one and serves it again
/// without serializing nor hashing.
/// </remarks>
internal sealed class JsonBody
{
    private JsonBody(byte[] bytes, EntityTagHeaderValue tag)
    {
        Bytes = bytes;
        Tag = tag;
    }

    public ReadOnlyMemory<byte> Bytes { get; }

    public EntityTagHeaderValue Tag { get; }

    public static JsonBody Of<TValue>(TValue value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, options);
        var tag = new EntityTagHeaderValue($"\"{Convert.ToBase64String(SHA256.HashData(bytes))}\"");
        return new JsonBody(bytes, tag);
    }
}
