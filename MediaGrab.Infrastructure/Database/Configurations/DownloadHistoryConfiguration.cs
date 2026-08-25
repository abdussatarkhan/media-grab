using MediaGrab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaGrab.Infrastructure.Database.Configurations;

public class DownloadHistoryConfiguration : IEntityTypeConfiguration<DownloadHistory>
{
    public void Configure(EntityTypeBuilder<DownloadHistory> builder)
    {
        builder.ToTable("DownloadHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(h => h.SourceUrl)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(h => h.FinalStatus)
            .IsRequired()
            .HasMaxLength(32);

        builder.HasIndex(h => h.UserId);

        builder.HasIndex(h => h.DownloadJobId).IsUnique();
    }
}
