using Microsoft.AspNetCore.Mvc;

namespace RentMate.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet("/AccessDenied")]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
