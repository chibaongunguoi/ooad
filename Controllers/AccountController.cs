using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ooad.Models;

namespace ooad.Controllers
{
    public class AccountController : Controller
    {
        private readonly MainContext _context;

        public AccountController(MainContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("Username") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _context.User.FirstOrDefaultAsync(u => u.Username == username && u.Password == password);
            
            if (user != null)
            {
                HttpContext.Session.SetString("Username", user.Username);
                return RedirectToAction("Index", "Home");
            }
            
            ModelState.AddModelError("", "Invalid username or password");
            return View();
        }

        public IActionResult Register()
        {
            if (HttpContext.Session.GetString("Username") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(User user)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _context.User.FirstOrDefaultAsync(u => u.Username == user.Username);
                if (existingUser != null)
                {
                    ModelState.AddModelError("", "Username already exists");
                    return View(user);
                }

                _context.Add(user);
                await _context.SaveChangesAsync();
                
                HttpContext.Session.SetString("Username", user.Username);
                return RedirectToAction("Index", "Home");
            }
            return View(user);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}