namespace MediaGrab.Web.Models;

/// <summary>Trivial shared view model used by a few static/informational pages.</summary>
public class PageMessageViewModel
{
    public string Title { get; set; } = string.Empty;

    public string? Message { get; set; }
}
