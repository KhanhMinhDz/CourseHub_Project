using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Controllers
{
    [Authorize]
    public class AssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AssignmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int classId)
        {
            var items = await _context.Assignments.Where(a => a.ClassRoomId == classId).ToListAsync();
            ViewBag.ClassId = classId;
            return View(items);
        }

        [Authorize(Roles = "Instructor")]
        public IActionResult Create(int classId)
        {
            var model = new Assignment { ClassRoomId = classId };
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> Create(Assignment model)
        {
            if (!ModelState.IsValid) return View(model);
            // verify instructor owns the class
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var cls = await _context.ClassRooms.FindAsync(model.ClassRoomId);
            if (cls == null) return NotFound();
            var isAdmin = User?.IsInRole("Admin") ?? false;
            if (cls.InstructorId != null)
            {
                if (cls.InstructorId != userId && !isAdmin) return Forbid();
            }
            else
            {
                // if no instructor assigned, only admin can create
                if (!isAdmin) return Forbid();
            }

            _context.Assignments.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { classId = model.ClassRoomId });
        }

        // GET: /Assignments/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var a = await _context.Assignments.FindAsync(id);
            if (a == null) return NotFound();
            var cls = await _context.ClassRooms.FindAsync(a.ClassRoomId);
            ViewBag.Class = cls;
            return View(a);
        }

        // POST: /Assignments/EditDescription
        [HttpPost]
        [Authorize(Roles = "Instructor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDescription(int id, string? description, string? title)
        {
            var a = await _context.Assignments.FindAsync(id);
            if (a == null) return NotFound();
            var cls = await _context.ClassRooms.FindAsync(a.ClassRoomId);
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User?.IsInRole("Admin") ?? false;
            if (cls != null && cls.InstructorId != null && cls.InstructorId != userId && !isAdmin) return Forbid();

            if (!string.IsNullOrEmpty(title)) a.Title = title;
            a.Description = description;
            _context.Assignments.Update(a);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = a.Id });
        }

        // POST: /Assignments/CreateFromEditor
        [HttpPost]
        [Authorize(Roles = "Instructor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromEditor(int classId, string title, string? description)
        {
            var cls = await _context.ClassRooms.FindAsync(classId);
            if (cls == null) return NotFound();
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User?.IsInRole("Admin") ?? false;
            if (cls.InstructorId != null && cls.InstructorId != userId && !isAdmin) return Forbid();

            var model = new Assignment { ClassRoomId = classId, Title = title, Description = description };
            _context.Assignments.Add(model);
            await _context.SaveChangesAsync();

            // If the request is AJAX (from the instructor editor), return the partial markup so the client can insert it.
            if (Request.Headers != null && Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                // Allow the partial to know that the current user can edit this assignment (creator/instructor)
                ViewBag.CanEdit = true;
                return PartialView("~/Views/Shared/_AssignmentBlock.cshtml", model);
            }

            return RedirectToAction("Index", new { classId });
        }
    }
}
