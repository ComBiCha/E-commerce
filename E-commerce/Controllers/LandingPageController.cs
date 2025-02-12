using Microsoft.AspNetCore.Mvc;

namespace E_commerce.Controllers
{
    public class LandingPageController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
