using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Accounts;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private static readonly ValueConverter<IReadOnlyList<string>, string> RolesConverter = new(
        roles => JsonSerializer.Serialize(roles, (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<string[]>(json, (JsonSerializerOptions?)null)
            ?? Array.Empty<string>());

    private static readonly ValueComparer<IReadOnlyList<string>> RolesComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        roles => roles.Aggregate(
            0,
            (hash, role) => HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(role))),
        roles => roles.ToArray());

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id).HasName("users_pkey");

        builder.Property(user => user.Email).HasMaxLength(180);
        builder.Property(user => user.Username).HasMaxLength(24);
        builder.Property(user => user.Roles)
            .HasColumnType("json")
            .HasConversion(RolesConverter, RolesComparer);
        builder.Property(user => user.Password).HasMaxLength(255);
        builder.Property(user => user.IsPublicProfile).HasLegacyDefault(false);
        builder.Property(user => user.FavoriteChampionId).HasMaxLength(64).HasNullDefault();
        builder.Property(user => user.FavoriteItemId).HasMaxLength(16).HasNullDefault();
        builder.Property(user => user.FavoriteRuneId).HasMaxLength(16).HasNullDefault();
        builder.Property(user => user.FavoriteSummonerId).HasMaxLength(64).HasNullDefault();
        builder.Property(user => user.GoogleId).HasMaxLength(30).HasNullDefault();
        builder.Property(user => user.RiotTagline).HasMaxLength(5).HasNullDefault();
        builder.Property(user => user.IsSupporter).HasLegacyDefault(false);
        builder.Property(user => user.IsBanned).HasLegacyDefault(false);
        builder.Property(user => user.BannedAt).HasPrecision(0).HasNullDefault();
        builder.Property(user => user.BanReason).HasMaxLength(255).HasNullDefault();
        builder.Property(user => user.IsVerified).HasLegacyDefault(false);
        builder.Property(user => user.FavoriteSkinId).HasMaxLength(64).HasNullDefault();
        builder.Property(user => user.PreferredVersion).HasMaxLength(24).HasNullDefault();

        builder.HasIndex(user => user.GoogleId)
            .IsUnique()
            .HasDatabaseName("uniq_1483a5e976f5c865");
    }
}
