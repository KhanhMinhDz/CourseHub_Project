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
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public ClassRoomsController(ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
            var c = await _context.ClassRooms
                .Include(x => x.ContentBlocks)
                .Include(x => x.Assignments)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (c == null) return NotFound();

            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            ViewBag.IsInstructor = (userId != null && userId == c.InstructorId);

            if (userId != null)
            {
                ViewBag.IsEnrolled = await _context.Enrollments
                    .AnyAsync(e => e.ClassRoomId == id && e.StudentId == userId);

                // Lấy phiên điểm danh đang active
                var activeSession = await _context.AttendanceSessions
                    .Where(s => s.ClassRoomId == id && s.IsActive && DateTime.Now < s.CloseAt)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                ViewBag.ActiveAttendanceSession = activeSession;

                // Kiểm tra xem học viên đã điểm danh chưa
                if (activeSession != null)
                {
                    var hasAttended = await _context.AttendanceRecords
                        .AnyAsync(r => r.AttendanceSessionId == activeSession.Id
                                    && r.StudentId == userId
                                    && r.IsPresent);
                    ViewBag.HasAttended = hasAttended;
                }
                else
                {
                    ViewBag.HasAttended = false;
                }
            }
            else
            {
                ViewBag.IsEnrolled = false;
                ViewBag.ActiveAttendanceSession = null;
                ViewBag.HasAttended = false;
            }

            return View(c);
        }

        // Học viên điểm danh
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> MarkAttendance(int sessionId)
        {
            var userId = _userManager.GetUserId(User);

            var session = await _context.AttendanceSessions.FindAsync(sessionId);
            if (session == null)
                return Json(new { success = false, message = "Không tìm thấy phiên điểm danh" });

            // Kiểm tra phiên còn mở không
            if (!session.IsActive || DateTime.Now > session.CloseAt)
                return Json(new { success = false, message = "Phiên điểm danh đã đóng" });

            // Kiểm tra học viên có trong lớp không
            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.ClassRoomId == session.ClassRoomId && e.StudentId == userId);

            if (!isEnrolled)
                return Json(new { success = false, message = "Bạn chưa ghi danh vào lớp này" });

            // Tìm bản ghi điểm danh
            var record = await _context.AttendanceRecords
                .FirstOrDefaultAsync(r => r.AttendanceSessionId == sessionId && r.StudentId == userId);

            if (record == null)
            {
                // Tạo mới nếu chưa có
                record = new AttendanceRecord
                {
                    AttendanceSessionId = sessionId,
                    StudentId = userId!,
                    IsPresent = true,
                    AttendedAt = DateTime.Now
                };
                _context.AttendanceRecords.Add(record);
            }
            else if (record.IsPresent)
            {
                return Json(new { success = false, message = "Bạn đã điểm danh rồi" });
            }
            else
            {
                // Cập nhật nếu đã có nhưng chưa điểm danh
                record.IsPresent = true;
                record.AttendedAt = DateTime.Now;
                _context.AttendanceRecords.Update(record);
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Điểm danh thành công",
                attendedAt = DateTime.Now.ToString("HH:mm dd/MM/yyyy")
            });
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
