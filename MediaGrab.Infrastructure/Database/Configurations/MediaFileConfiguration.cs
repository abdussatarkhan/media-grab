using MediaGrab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaGrab.Infrastructure.Database.Configurations;

public class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.ToTable("MediaFiles");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FileName)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(f => f.Format)
            .HasMaxLength(32);

        builder.Property(f => f.StoragePath)
            .HasMaxLength(1024);

        builder.Property(f => f.ContentType)
            .HasMaxLength(256);

        builder.Property(f => f.FileType)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(f => f.DownloadJobId);
    }
}
