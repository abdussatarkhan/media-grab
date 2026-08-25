using MediaGrab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaGrab.Infrastructure.Database.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(s => s.Value)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(s => s.Description)
            .HasMaxLength(512);

        builder.HasIndex(s => s.Key).IsUnique();
    }
}
