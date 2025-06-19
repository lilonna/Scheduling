using Microsoft.AspNetCore.Mvc;

namespace Scheduling.Controllers
{
    public class PubController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
