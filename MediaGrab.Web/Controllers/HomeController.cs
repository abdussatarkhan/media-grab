using Microsoft.AspNetCore.Mvc;

namespace MediaGrab.Web.Controllers;

/// <summary>
/// Public marketing/informational pages. None of these require
/// authentication. The Index view contains the URL submission form,
/// which posts to the API (Controllers/Api/MediaController) via
/// JavaScript, not to an action on this controller.
/// </summary>
public class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Download() => View();

    public IActionResult SupportedSites() => View();

    public IActionResult About() => View();

    public IActionResult Faq() => View();

    public IActionResult Contact() => View();

    public IActionResult Privacy() => View();

    public IActionResult Terms() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
