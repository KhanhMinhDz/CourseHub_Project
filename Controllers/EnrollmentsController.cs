using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Controllers
{
    [Authorize]
    public class EnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<CourseManagement.Data.ApplicationUser> _userManager;

        public EnrollmentsController(ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<CourseManagement.Data.ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // POST: Enrollments/Enroll
        [Authorize]
        public async Task<IActionResult> Access(int classId)
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Forbid();

            // 👇 DEBUG
            Console.WriteLine($"[Access] userId={userId}, classId={classId}");

            var enrolled = await _context.Enrollments
                .AnyAsync(e => e.ClassRoomId == classId && e.StudentId == userId);

            Console.WriteLine($"[Access] enrolled={enrolled}");

            if (enrolled)
            {
                Console.WriteLine($"[Access] Redirect to Details for class {classId}");
                return RedirectToAction("Details", "ClassRooms", new { id = classId });
            }

            var classroom = await _context.ClassRooms.FindAsync(classId);
            if (classroom == null) return NotFound();

            ViewBag.ClassId = classId;
            ViewBag.ClassTitle = classroom.Title;

            return View("EnrollPage");
        }



        [HttpGet]
        public async Task<IActionResult> EnrollPage(int classId)
        {
            var c = await _context.ClassRooms.FindAsync(classId);
            if (c == null) return NotFound();

            ViewBag.ClassName = c.Title;
            ViewBag.ClassId = classId;
            return View("EnrollPage");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enroll(int classId, string password)
        {
            var c = await _context.ClassRooms.FindAsync(classId);
            if (c == null) return NotFound();

            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Forbid();

            var already = await _context.Enrollments.AnyAsync(e => e.ClassRoomId == classId && e.StudentId == userId);
            if (already)
                return RedirectToAction("Details", "ClassRooms", new { id = classId });

            // kiểm tra mật khẩu nếu có
            if (!string.IsNullOrEmpty(c.EnrollmentPasswordHash))
            {
                var instr = await _userManager.FindByIdAsync(c.InstructorId);
                var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<CourseManagement.Data.ApplicationUser>();
                var verify = hasher.VerifyHashedPassword(instr, c.EnrollmentPasswordHash, password);
                if (verify == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
                {
                    TempData["EnrollError"] = "Mật khẩu ghi danh không đúng.";
                    return RedirectToAction("EnrollPage", new { classId = classId });
                }
            }

            _context.Enrollments.Add(new Enrollment { ClassRoomId = classId, StudentId = userId });
            await _context.SaveChangesAsync();

            TempData["EnrollSuccess"] = "Ghi danh thành công!";
            return RedirectToAction("Details", "ClassRooms", new { id = classId });
        }

        [Authorize]
        public async Task<IActionResult> Details(int id)
        {
            var classRoom = await _context.ClassRooms
                .Include(c => c.Assignments)
                .Include(c => c.ContentBlocks)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (classRoom == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);

            bool isInstructor = classRoom.InstructorId == userId;
            bool isEnrolled = await _context.Enrollments.AnyAsync(e => e.ClassRoomId == id && e.StudentId == userId);

            if (!isInstructor && !isEnrolled)
            {
                TempData["EnrollError"] = "Bạn cần ghi danh trước khi vào lớp.";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.IsInstructor = isInstructor;
            return View(classRoom);
        }

    }
}
