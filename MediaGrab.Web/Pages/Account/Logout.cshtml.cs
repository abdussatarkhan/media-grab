using MediaGrab.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MediaGrab.Web.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public LogoutModel(SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        await _signInManager.SignOutAsync();

        return returnUrl is not null ? LocalRedirect(returnUrl) : RedirectToPage("/Account/Login");
    }
}
