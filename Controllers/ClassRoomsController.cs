using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Controllers
{
    public class ClassRoomsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClassRoomsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /
        public async Task<IActionResult> Index()
        {
            var classes = await _context.ClassRooms.ToListAsync();
            return View(classes);
        }

        // GET: /ClassRooms/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var c = await _context.ClassRooms.FindAsync(id);
            if (c == null) return NotFound();
            return View(c);
        }

        // Admin creates class
        [Authorize(Roles = "Admin")]
        public IActionResult Create() => View();

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(ClassRoom model)
        {
            if (!ModelState.IsValid) return View(model);
            _context.ClassRooms.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
