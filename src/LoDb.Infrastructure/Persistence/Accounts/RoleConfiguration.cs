using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Accounts;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("identity_roles");
        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name).HasMaxLength(256).IsRequired();
        builder.Property(role => role.NormalizedName).HasMaxLength(256).IsRequired();
        builder.Property(role => role.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(role => role.NormalizedName).IsUnique();
    }
}
