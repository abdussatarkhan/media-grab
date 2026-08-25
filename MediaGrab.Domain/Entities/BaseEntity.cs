namespace MediaGrab.Domain.Entities;

/// <summary>
/// Common fields shared by every persisted entity. Centralizing these
/// means every table gets consistent auditing columns for free.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
