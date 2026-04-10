using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HotelBookingSystem.Pages
{
    public class IndexModel : PageModel
    {
        public bool IsLoggedIn { get; set; }

        public void OnGet()
        {
            IsLoggedIn = HttpContext.Session.GetString("UserId") != null;
        }
    }
}
