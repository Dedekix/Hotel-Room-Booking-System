using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HotelBookingSystem.Pages.Staff
{
    public class DashboardModel : PageModel
    {
        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "STAFF" && role != "ADMIN")
                return Redirect("/Login?returnUrl=/Staff/Dashboard");
            return Page();
        }
    }
}
