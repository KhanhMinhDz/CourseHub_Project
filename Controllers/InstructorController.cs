using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Controllers
{
    [Authorize(Roles = "Instructor")]
    public class InstructorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public InstructorController(ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var classes = await _context.ClassRooms.Where(c => c.InstructorId == userId).ToListAsync();
            return View(classes);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var cls = await _context.ClassRooms.FindAsync(id);
            if (cls == null) return NotFound();
            var userId = _userManager.GetUserId(User);
            if (cls.InstructorId != userId) return Forbid();
            return View(cls);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, string? enrollmentPassword)
        {
            var cls = await _context.ClassRooms.FindAsync(id);
            if (cls == null) return NotFound();
            var userId = _userManager.GetUserId(User);
            if (cls.InstructorId != userId) return Forbid();

            if (!string.IsNullOrEmpty(enrollmentPassword))
            {
                var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<ApplicationUser>();
                var instr = await _userManager.GetUserAsync(User);
                cls.EnrollmentPasswordHash = hasher.HashPassword(instr!, enrollmentPassword);
            }
            else
            {
                cls.EnrollmentPasswordHash = null;
            }

            _context.ClassRooms.Update(cls);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Enrollments(int id)
        {
            var cls = await _context.ClassRooms.FindAsync(id);
            if (cls == null) return NotFound();
            var userId = _userManager.GetUserId(User);
            if (cls.InstructorId != userId) return Forbid();

            var list = await _context.Enrollments.Where(e => e.ClassRoomId == id).ToListAsync();
            var vm = new List<CourseManagement.Models.EnrollmentViewModel>();
            foreach (var e in list)
            {
                var user = await _userManager.FindByIdAsync(e.StudentId);
                vm.Add(new CourseManagement.Models.EnrollmentViewModel
                {
                    Id = e.Id,
                    StudentId = e.StudentId,
                    StudentName = user?.FullName ?? user?.UserName,
                    EnrolledAt = e.EnrolledAt
                });
            }
            return View(vm);
        }
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClassRoom model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = _userManager.GetUserId(User);

            model.InstructorId = userId;

            _context.ClassRooms.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

    }
}
