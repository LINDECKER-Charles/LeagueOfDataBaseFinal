using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Accounts;

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<int>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<int>> builder)
    {
        builder.ToTable("identity_user_tokens");
        builder.HasKey(token => new { token.UserId, token.LoginProvider, token.Name });

        builder.Property(token => token.LoginProvider).HasMaxLength(128);
        builder.Property(token => token.Name).HasMaxLength(128);

        // The legacy stack deletes accounts without knowing this table.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
