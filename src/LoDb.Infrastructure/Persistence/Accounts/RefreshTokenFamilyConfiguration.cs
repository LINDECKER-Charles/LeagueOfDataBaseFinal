using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Accounts;

internal sealed class RefreshTokenFamilyConfiguration
    : IEntityTypeConfiguration<RefreshTokenFamily>
{
    public void Configure(EntityTypeBuilder<RefreshTokenFamily> builder)
    {
        builder.ToTable("refresh_token_families");
        builder.HasKey(family => family.Id);
        builder.Property(family => family.Id).ValueGeneratedNever();

        // A deleted account takes its sessions along, the legacy stack's deletions included.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(family => family.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(family => family.UserId);
        builder.HasIndex(family => family.ExpiresAt);
    }
}
