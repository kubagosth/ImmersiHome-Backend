using Microsoft.AspNetCore.Mvc;

namespace ImmersiHome_API.Controllers
{
    [Route("api/[controller]")]
    public class HomeController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return Ok("Welcome to ImmersiHome API");
        }
    }
}
