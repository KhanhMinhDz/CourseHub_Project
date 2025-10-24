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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enroll(int classId, string password)
        {
            var c = await _context.ClassRooms.FindAsync(classId);
            if (c == null) return NotFound();

            // password check using Identity password hasher: verify against instructor's stored hash
            if (!string.IsNullOrEmpty(c.EnrollmentPasswordHash))
            {
                if (string.IsNullOrEmpty(c.InstructorId))
                {
                    TempData["EnrollError"] = "Lớp không có giảng viên, không thể ghi danh.";
                    return RedirectToAction("Details", "ClassRooms", new { id = classId });
                }
                var instr = await _userManager.FindByIdAsync(c.InstructorId);
                if (instr == null)
                {
                    TempData["EnrollError"] = "Giảng viên không tồn tại.";
                    return RedirectToAction("Details", "ClassRooms", new { id = classId });
                }
                var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<CourseManagement.Data.ApplicationUser>();
                var verify = hasher.VerifyHashedPassword(instr, c.EnrollmentPasswordHash, password);
                if (verify == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
                {
                    TempData["EnrollError"] = "Mật khẩu ghi danh không đúng.";
                    return RedirectToAction("Details", "ClassRooms", new { id = classId });
                }
            }

            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Forbid();

            var exists = await _context.Enrollments.AnyAsync(e => e.ClassRoomId == classId && e.StudentId == userId);
            if (!exists)
            {
                _context.Enrollments.Add(new Enrollment { ClassRoomId = classId, StudentId = userId });
                await _context.SaveChangesAsync();
                TempData["EnrollSuccess"] = "Bạn đã ghi danh thành công.";
            }
            else
            {
                TempData["EnrollSuccess"] = "Bạn đã ghi danh vào lớp này trước đó.";
            }
            return RedirectToAction("Details", "ClassRooms", new { id = classId });
        }
    }
}
