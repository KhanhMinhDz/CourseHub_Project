using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Controllers
{
    [Authorize]
    public class SubmissionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SubmissionsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index(int assignmentId)
        {
            var items = await _context.Submissions.Where(s => s.AssignmentId == assignmentId).ToListAsync();
            ViewBag.AssignmentId = assignmentId;
            return View(items);
        }

        [HttpGet]
        public IActionResult Create(int assignmentId)
        {
            ViewBag.AssignmentId = assignmentId;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(int assignmentId, IFormFile? file, string? comments)
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Forbid();

            string? path = null;
            if (file != null && file.Length > 0)
            {
                var uploads = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                path = Path.Combine("uploads", fileName);
                var physical = Path.Combine(_env.ContentRootPath, "wwwroot", path);
                using var fs = System.IO.File.Create(physical);
                await file.CopyToAsync(fs);
            }

            var submission = new Submission
            {
                AssignmentId = assignmentId,
                StudentId = userId,
                FilePath = path,
                Comments = comments
            };
            _context.Submissions.Add(submission);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { assignmentId });
        }
    }
}
