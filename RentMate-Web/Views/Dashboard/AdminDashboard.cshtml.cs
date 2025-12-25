using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RentMate.Views.Dashboard
{
    /// <summary>
    /// Model for the Admin Dashboard page.
    /// Provides the backend entry point for system-wide statistics and management overviews.
    /// </summary>
    public class AdminDashboardModel : PageModel
    {
        public void OnGet()
        {
            // Logic for the Admin Dashboard is currently handled via the Controller 
            // and the DashboardViewModel. This PageModel remains for routing/future use.
        }
    }
}