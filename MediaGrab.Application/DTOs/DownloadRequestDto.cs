using System.ComponentModel.DataAnnotations;

namespace MediaGrab.Application.DTOs;

/// <summary>Request body for POST /api/media/download - starts background processing for a previously analyzed job.</summary>
public class DownloadRequestDto
{
    [Required]
    public Guid JobId { get; set; }

    /// <summary>Format/quality identifier returned by a prior call to /api/media/analyze.</summary>
    [StringLength(128)]
    public string? FormatId { get; set; }
}
