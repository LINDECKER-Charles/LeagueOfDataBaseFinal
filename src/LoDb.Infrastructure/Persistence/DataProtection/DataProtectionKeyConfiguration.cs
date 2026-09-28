using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.DataProtection;

/// <summary>
/// The key ring shared by every instance, so that a deployment signs nobody out.
/// </summary>
/// <remarks>
/// <c>xml</c> holds each key encrypted by the Data Protection certificate, except in
/// Development.
/// </remarks>
internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.ToTable("data_protection_keys");
        builder.HasKey(key => key.Id);
    }
}
