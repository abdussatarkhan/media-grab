namespace MediaGrab.Domain.Enums;

/// <summary>
/// Whether a provider is currently active, disabled by an administrator,
/// or still under development / not yet enabled for end users.
/// </summary>
public enum ProviderStatus
{
    Disabled = 0,
    Active = 1,
    ComingSoon = 2
}
