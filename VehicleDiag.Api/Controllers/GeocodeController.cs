using Microsoft.AspNetCore.Mvc;

namespace VehicleDiag.Api.Controllers
{
    public class GeocodeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
