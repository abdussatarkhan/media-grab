using System.ComponentModel.DataAnnotations;

namespace MediaGrab.Application.DTOs;

/// <summary>Request body for POST /api/media/analyze.</summary>
public class AnalyzeRequestDto
{
    [Required]
    [StringLength(2048, MinimumLength = 1)]
    public string Url { get; set; } = string.Empty;
}
