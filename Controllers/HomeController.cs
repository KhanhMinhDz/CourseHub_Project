using System.Diagnostics;
using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string searchQuery)
        {
            var classesQuery = _context.ClassRooms.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                classesQuery = classesQuery.Where(c =>
                    c.Title.Contains(searchQuery) ||
                    (c.Description != null && c.Description.Contains(searchQuery)));
            }

            var classes = await classesQuery
                .OrderByDescending(c => c.Id)
                .Take(12)
                .ToListAsync();

            // Get instructor names and student counts
            var classViewModels = new List<ClassDisplayViewModel>();
            foreach (var cls in classes)
            {
                var instructor = await _userManager.FindByIdAsync(cls.InstructorId);
                var studentCount = await _context.Enrollments.CountAsync(e => e.ClassRoomId == cls.Id);

                classViewModels.Add(new ClassDisplayViewModel
                {
                    Id = cls.Id,
                    Title = cls.Title,
                    Description = cls.Description ?? "",
                    InstructorName = instructor?.FullName ?? instructor?.UserName ?? "Unknown",
                    StudentCount = studentCount
                });
            }

            ViewBag.SearchQuery = searchQuery;
            return View(classViewModels);
        }

        public IActionResult Privacy()
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
