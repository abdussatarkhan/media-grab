using MediaGrab.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MediaGrab.Infrastructure.Database;

/// <summary>
/// The single EF Core context for MediaGrab. Inherits from
/// IdentityDbContext so authentication tables (AspNetUsers, AspNetRoles,
/// etc.) live alongside our own domain tables in the same PostgreSQL
/// database.
/// </summary>
public class MediaGrabDbContext : IdentityDbContext<ApplicationUser>
{
    public MediaGrabDbContext(DbContextOptions<MediaGrabDbContext> options)
        : base(options)
    {
    }

    public DbSet<DownloadJob> DownloadJobs => Set<DownloadJob>();

    public DbSet<DownloadHistory> DownloadHistories => Set<DownloadHistory>();

    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    public DbSet<SupportedProvider> SupportedProviders => Set<SupportedProvider>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(MediaGrabDbContext).Assembly);
    }
}
