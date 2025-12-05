using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Dashboard
        public async Task<IActionResult> Index()
        {
            var instructors = await _userManager.GetUsersInRoleAsync("Instructor");
            var students = await _userManager.GetUsersInRoleAsync("Student");
            var courses = await _context.ClassRooms.ToListAsync();
            var enrollments = await _context.Enrollments.CountAsync();

            var instructorStats = new List<InstructorStatViewModel>();
            foreach (var instructor in instructors)
            {
                var instructorCourses = await _context.ClassRooms
                    .Where(c => c.InstructorId == instructor.Id)
                    .ToListAsync();

                var totalStudents = 0;
                foreach (var course in instructorCourses)
                {
                    totalStudents += await _context.Enrollments.CountAsync(e => e.ClassRoomId == course.Id);
                }

                instructorStats.Add(new InstructorStatViewModel
                {
                    InstructorId = instructor.Id,
                    InstructorName = instructor.FullName ?? instructor.UserName ?? "",
                    Email = instructor.Email ?? "",
                    CourseCount = instructorCourses.Count,
                    TotalStudents = totalStudents
                });
            }

            var viewModel = new AdminDashboardViewModel
            {
                TotalInstructors = instructors.Count,
                TotalStudents = students.Count,
                TotalCourses = courses.Count,
                TotalEnrollments = enrollments,
                InstructorStats = instructorStats.OrderByDescending(s => s.CourseCount).ToList()
            };

            return View(viewModel);
        }

        // === QUẢN LÝ TÀI KHOẢN HỌC VIÊN ===
        public async Task<IActionResult> Students()
        {
            var students = await _userManager.GetUsersInRoleAsync("Student");
            var viewModels = students.Select(s => new UserManagementViewModel
            {
                Id = s.Id,
                UserName = s.UserName ?? "",
                Email = s.Email ?? "",
                FullName = s.FullName ?? "",
                Role = "Student"
            }).OrderBy(s => s.FullName).ToList();

            return View(viewModels);
        }

        [HttpGet]
        public IActionResult CreateStudent()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStudent(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Student");
                TempData["Success"] = $"Tạo tài khoản học viên {model.FullName} thành công!";
                return RedirectToAction(nameof(Students));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditStudent(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? "",
                FullName = user.FullName ?? "",
                Role = "Student"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStudent(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
                return NotFound();

            user.Email = model.Email;
            user.UserName = model.Email;
            user.FullName = model.FullName;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(model.NewPassword))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                }

                TempData["Success"] = "Cập nhật tài khoản thành công!";
                return RedirectToAction(nameof(Students));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStudent(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Xóa tài khoản thành công!";
            }
            else
            {
                TempData["Error"] = "Không thể xóa tài khoản!";
            }

            return RedirectToAction(nameof(Students));
        }

        // === QUẢN LÝ TÀI KHOẢN GIẢNG VIÊN ===
        public async Task<IActionResult> Instructors()
        {
            var instructors = await _userManager.GetUsersInRoleAsync("Instructor");
            var viewModels = new List<UserManagementViewModel>();

            foreach (var instructor in instructors)
            {
                var courseCount = await _context.ClassRooms.CountAsync(c => c.InstructorId == instructor.Id);
                viewModels.Add(new UserManagementViewModel
                {
                    Id = instructor.Id,
                    UserName = instructor.UserName ?? "",
                    Email = instructor.Email ?? "",
                    FullName = instructor.FullName ?? "",
                    Role = "Instructor"
                });
            }

            return View(viewModels.OrderBy(i => i.FullName).ToList());
        }

        [HttpGet]
        public IActionResult CreateInstructor()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInstructor(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Instructor");
                TempData["Success"] = $"Tạo tài khoản giảng viên {model.FullName} thành công!";
                return RedirectToAction(nameof(Instructors));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditInstructor(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? "",
                FullName = user.FullName ?? "",
                Role = "Instructor"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditInstructor(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
                return NotFound();

            user.Email = model.Email;
            user.UserName = model.Email;
            user.FullName = model.FullName;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(model.NewPassword))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                }

                TempData["Success"] = "Cập nhật tài khoản thành công!";
                return RedirectToAction(nameof(Instructors));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteInstructor(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            try
            {
                // Lấy tất cả các lớp học của giảng viên
                var classRooms = await _context.ClassRooms
                    .Where(c => c.InstructorId == id)
                    .ToListAsync();

                // Xóa tất cả dữ liệu liên quan đến từng lớp học
                foreach (var classRoom in classRooms)
                {
                    // Xóa các câu hỏi trong assignments
                    var assignments = await _context.Assignments
                        .Where(a => a.ClassRoomId == classRoom.Id)
                        .ToListAsync();

                    var assignmentIds = assignments.Select(a => a.Id).ToList();

                    foreach (var assignment in assignments)
                    {
                        var questions = await _context.Questions
                            .Where(q => q.AssignmentId == assignment.Id)
                            .ToListAsync();
                        _context.Questions.RemoveRange(questions);
                    }

                    // Xóa submissions thông qua AssignmentId
                    var submissions = await _context.Submissions
                        .Where(s => assignmentIds.Contains(s.AssignmentId))
                        .ToListAsync();
                    _context.Submissions.RemoveRange(submissions);

                    // Xóa assignments
                    _context.Assignments.RemoveRange(assignments);

                    // Xóa content blocks
                    var contentBlocks = await _context.ContentBlocks
                        .Where(cb => cb.ClassRoomId == classRoom.Id)
                        .ToListAsync();
                    _context.ContentBlocks.RemoveRange(contentBlocks);

                    // Xóa attendance sessions và records
                    var attendanceSessions = await _context.AttendanceSessions
                        .Where(asn => asn.ClassRoomId == classRoom.Id)
                        .ToListAsync();

                    var sessionIds = attendanceSessions.Select(s => s.Id).ToList();

                    var attendanceRecords = await _context.AttendanceRecords
                        .Where(ar => sessionIds.Contains(ar.AttendanceSessionId))
                        .ToListAsync();
                    _context.AttendanceRecords.RemoveRange(attendanceRecords);
                    _context.AttendanceSessions.RemoveRange(attendanceSessions);

                    // Xóa enrollments
                    var enrollments = await _context.Enrollments
                        .Where(e => e.ClassRoomId == classRoom.Id)
                        .ToListAsync();
                    _context.Enrollments.RemoveRange(enrollments);
                }

                // Xóa tất cả các lớp học
                _context.ClassRooms.RemoveRange(classRooms);

                // Lưu thay đổi database
                await _context.SaveChangesAsync();

                // Xóa tài khoản giảng viên
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    TempData["Success"] = $"Đã xóa giảng viên {user.FullName} và {classRooms.Count} khóa học thành công!";
                }
                else
                {
                    TempData["Error"] = "Không thể xóa tài khoản giảng viên!";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction(nameof(Instructors));
        }

        // === QUẢN LÝ KHÓA HỌC ===
        public async Task<IActionResult> Courses()
        {
            var courses = await _context.ClassRooms.ToListAsync();
            var viewModels = new List<CourseManagementViewModel>();

            foreach (var course in courses)
            {
                var instructor = await _userManager.FindByIdAsync(course.InstructorId);
                var studentCount = await _context.Enrollments.CountAsync(e => e.ClassRoomId == course.Id);
                var assignmentCount = await _context.Assignments.CountAsync(a => a.ClassRoomId == course.Id);

                viewModels.Add(new CourseManagementViewModel
                {
                    ClassId = course.Id,
                    ClassName = course.Title,
                    Description = course.Description ?? "",
                    InstructorName = instructor?.FullName ?? instructor?.UserName ?? "Unknown",
                    InstructorEmail = instructor?.Email ?? "",
                    StudentCount = studentCount,
                    AssignmentCount = assignmentCount
                });
            }

            return View(viewModels.OrderBy(c => c.ClassName).ToList());
        }

        // Xem chi tiết khóa học (giống Instructor/Edit)
        public async Task<IActionResult> CourseDetails(int id)
        {
            var course = await _context.ClassRooms.FindAsync(id);
            if (course == null)
                return NotFound();

            return RedirectToAction("Students", "Instructor", new { classRoomId = id });
        }

        // Xóa khóa học
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _context.ClassRooms.FindAsync(id);
            if (course == null)
            {
                TempData["Error"] = "Không tìm thấy khóa học!";
                return RedirectToAction(nameof(Courses));
            }

            try
            {
                // Xóa tất cả dữ liệu liên quan
                // 1. Xóa submissions
                var assignments = await _context.Assignments.Where(a => a.ClassRoomId == id).ToListAsync();
                foreach (var assignment in assignments)
                {
                    var submissions = await _context.Submissions.Where(s => s.AssignmentId == assignment.Id).ToListAsync();
                    _context.Submissions.RemoveRange(submissions);
                }

                // 2. Xóa assignments
                _context.Assignments.RemoveRange(assignments);

                // 3. Xóa attendance records
                var attendanceSessions = await _context.AttendanceSessions.Where(s => s.ClassRoomId == id).ToListAsync();
                foreach (var session in attendanceSessions)
                {
                    var records = await _context.AttendanceRecords.Where(r => r.AttendanceSessionId == session.Id).ToListAsync();
                    _context.AttendanceRecords.RemoveRange(records);
                }

                // 4. Xóa attendance sessions
                _context.AttendanceSessions.RemoveRange(attendanceSessions);

                // 5. Xóa enrollments
                var enrollments = await _context.Enrollments.Where(e => e.ClassRoomId == id).ToListAsync();
                _context.Enrollments.RemoveRange(enrollments);

                // 6. Xóa content blocks
                var contentBlocks = await _context.ContentBlocks.Where(c => c.ClassRoomId == id).ToListAsync();
                _context.ContentBlocks.RemoveRange(contentBlocks);

                // 7. Xóa khóa học
                _context.ClassRooms.Remove(course);

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa khóa học '{course.Title}' thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi xóa khóa học: {ex.Message}";
            }

            return RedirectToAction(nameof(Courses));
        }

        // === BÁO CÁO & THỐNG KÊ ===
        public async Task<IActionResult> Reports()
        {
            var instructors = await _userManager.GetUsersInRoleAsync("Instructor");
            var viewModels = new List<InstructorStatViewModel>();

            foreach (var instructor in instructors)
            {
                var courses = await _context.ClassRooms
                    .Where(c => c.InstructorId == instructor.Id)
                    .ToListAsync();

                var coursesViewModel = new List<CourseManagementViewModel>();
                int totalStudents = 0;

                foreach (var course in courses)
                {
                    var studentCount = await _context.Enrollments.CountAsync(e => e.ClassRoomId == course.Id);
                    var assignmentCount = await _context.Assignments.CountAsync(a => a.ClassRoomId == course.Id);
                    totalStudents += studentCount;

                    coursesViewModel.Add(new CourseManagementViewModel
                    {
                        ClassId = course.Id,
                        ClassName = course.Title,
                        Description = course.Description ?? "",
                        InstructorName = instructor.FullName ?? instructor.UserName ?? "",
                        InstructorEmail = instructor.Email ?? "",
                        StudentCount = studentCount,
                        AssignmentCount = assignmentCount
                    });
                }

                viewModels.Add(new InstructorStatViewModel
                {
                    InstructorId = instructor.Id,
                    InstructorName = instructor.FullName ?? instructor.UserName ?? "",
                    Email = instructor.Email ?? "",
                    CourseCount = courses.Count,
                    TotalStudents = totalStudents,
                    Courses = coursesViewModel
                });
            }

            return View(viewModels.OrderByDescending(v => v.CourseCount).ToList());
        }
    }
}
