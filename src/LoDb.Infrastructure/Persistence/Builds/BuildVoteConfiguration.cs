using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Builds;

internal sealed class BuildVoteConfiguration : IEntityTypeConfiguration<BuildVote>
{
    public void Configure(EntityTypeBuilder<BuildVote> builder)
    {
        builder.ToTable("build_votes");
        builder.HasKey(vote => vote.Id).HasName("build_votes_pkey");

        builder.HasIndex(vote => new { vote.BuildId, vote.VoterId })
            .IsUnique()
            .HasDatabaseName("uniq_build_votes_build_voter");
        // Doctrine indexes each join column, even the one the unique pair already leads.
        builder.HasIndex(vote => vote.BuildId).HasDatabaseName("idx_11530dc417c13f8b");
        builder.HasIndex(vote => vote.VoterId).HasDatabaseName("idx_11530dc4ebb4b8ad");
        builder.HasOne(vote => vote.Build)
            .WithMany()
            .HasForeignKey(vote => vote.BuildId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_11530dc417c13f8b");
        builder.HasOne(vote => vote.Voter)
            .WithMany()
            .HasForeignKey(vote => vote.VoterId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_11530dc4ebb4b8ad");
    }
}
