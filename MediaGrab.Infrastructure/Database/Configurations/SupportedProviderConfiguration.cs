using MediaGrab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaGrab.Infrastructure.Database.Configurations;

public class SupportedProviderConfiguration : IEntityTypeConfiguration<SupportedProvider>
{
    public void Configure(EntityTypeBuilder<SupportedProvider> builder)
    {
        builder.ToTable("SupportedProviders");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(p => p.Key)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(p => p.Description)
            .HasMaxLength(512);

        builder.Property(p => p.IconUrl)
            .HasMaxLength(512);

        builder.Property(p => p.DomainPatterns)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(p => p.LegalNotes)
            .HasMaxLength(1024);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(p => p.Key).IsUnique();
    }
}
