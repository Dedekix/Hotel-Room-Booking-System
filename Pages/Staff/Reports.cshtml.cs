using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HotelBookingSystem.Pages.Staff
{
    public class ReportsModel : PageModel
    {
        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "ADMIN")
                return Redirect("/Login?returnUrl=/Staff/Reports");
            return Page();
        }
    }
}
