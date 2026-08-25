using MediaGrab.Domain.Enums;

namespace MediaGrab.Domain.Entities;

/// <summary>
/// A catalog row describing a media source that MediaGrab is (or will be)
/// authorized to work with, e.g. "Direct HTTP file links" or a specific
/// platform once a compliant integration exists. This table drives the
/// "/supported-sites" page and lets an administrator enable/disable a
/// provider without a code deployment.
/// </summary>
public class SupportedProvider : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty; // stable slug used by IMediaProvider.ProviderKey

    public string? Description { get; set; }

    public string? IconUrl { get; set; }

    /// <summary>
    /// Root domain(s) this provider is responsible for, comma separated.
    /// Used by the provider registry as a fast first pass before calling
    /// CanHandleAsync on the matching provider implementation.
    /// </summary>
    public string DomainPatterns { get; set; } = string.Empty;

    public ProviderStatus Status { get; set; } = ProviderStatus.ComingSoon;

    public string? LegalNotes { get; set; }

    public ICollection<DownloadJob> DownloadJobs { get; set; } = new List<DownloadJob>();
}
