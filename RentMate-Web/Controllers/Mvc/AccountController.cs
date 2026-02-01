using Microsoft.AspNetCore.Mvc;

namespace RentMate.Controllers.Mvc
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

