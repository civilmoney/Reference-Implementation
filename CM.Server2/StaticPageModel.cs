using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CM.Server {
    public class StaticPageModel : PageModel {

        public IActionResult OnGet() {
            return Page();
        }
    }
}
