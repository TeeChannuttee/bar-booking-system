using BarBookingSystem.Data;
using BarBookingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BarBookingSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Branches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
            ViewBag.PromoCodes = await _context.PromoCodes
                .Where(p => p.IsActive && p.ValidTo >= DateTime.UtcNow)
                .Take(4)
                .ToListAsync();

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}