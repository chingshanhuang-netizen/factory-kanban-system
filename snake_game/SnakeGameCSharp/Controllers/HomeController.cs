using Microsoft.AspNetCore.Mvc;

namespace SnakeGameCSharp.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
