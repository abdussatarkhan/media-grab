namespace MediaGrab.Domain.Entities;

/// <summary>
/// Simple key/value store for admin-configurable settings that don't
/// warrant their own table (feature flags, rate-limit thresholds, etc.).
/// </summary>
public class SystemSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }
}
