using Microsoft.AspNetCore.Identity;

namespace MediaGrab.Infrastructure.Database;

/// <summary>
/// Our application's user, extending ASP.NET Core Identity's IdentityUser.
/// We deliberately do NOT create a separate "User" table in
/// MediaGrab.Domain - Identity already owns authentication, password
/// hashing, and the user table (AspNetUsers). DownloadJob.UserId stores
/// this user's Id as a plain foreign key so the Domain project does not
/// need to reference ASP.NET Core Identity at all.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
