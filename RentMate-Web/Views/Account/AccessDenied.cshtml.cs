using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RentMate.Views.Account
{
    /// <summary>
    /// Model for the Access Denied page.
    /// Handles requests when a user lacks permission for a resource.
    /// </summary>
    public class AccessDeniedModel : PageModel
    {
        public void OnGet()
        {
            // No backend logic required for the initial display of this page.
        }
    }
}