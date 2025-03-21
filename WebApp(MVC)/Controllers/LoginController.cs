using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp_MVC_.Models;

namespace WebApp_MVC_.Controllers
{
    public class LoginController : Controller
    {
        private readonly AppDbContext _context;

        public LoginController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

            if (user != null)
            {
                // Logic for successful login (e.g., sign in the user, set up cookies, etc.)
                return RedirectToAction("Index", "Validation");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View();
        }

    }
}
