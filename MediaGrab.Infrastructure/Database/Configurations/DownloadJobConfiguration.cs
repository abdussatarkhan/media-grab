using MediaGrab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaGrab.Infrastructure.Database.Configurations;

public class DownloadJobConfiguration : IEntityTypeConfiguration<DownloadJob>
{
    public void Configure(EntityTypeBuilder<DownloadJob> builder)
    {
        builder.ToTable("DownloadJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.UserId)
            .IsRequired()
            .HasMaxLength(450); // matches Identity's default key length

        builder.Property(j => j.SourceUrl)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(1024);

        builder.Property(j => j.MetadataJson)
            .HasColumnType("text");

        builder.Property(j => j.SelectedFormatId)
            .HasMaxLength(128);

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(j => j.UserId);

        builder.HasIndex(j => j.Status);

        builder.HasIndex(j => j.ExpiresAtUtc);

        builder.HasOne(j => j.SupportedProvider)
            .WithMany(p => p.DownloadJobs)
            .HasForeignKey(j => j.SupportedProviderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(j => j.MediaFiles)
            .WithOne(f => f.DownloadJob)
            .HasForeignKey(f => f.DownloadJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(j => j.History)
            .WithOne(h => h.DownloadJob)
            .HasForeignKey<DownloadHistory>(h => h.DownloadJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
