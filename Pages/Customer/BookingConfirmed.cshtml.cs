using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HotelBookingSystem.Pages.Customer
{
    public class BookingConfirmedModel : PageModel
    {
        public bool   IsPaid      { get; set; }
        public string BookingType { get; set; } = "";

        public void OnGet(string? type, int? id, bool paid = false)
        {
            IsPaid      = paid;
            BookingType = type?.ToUpper() ?? "ROOM";
        }
    }
}
