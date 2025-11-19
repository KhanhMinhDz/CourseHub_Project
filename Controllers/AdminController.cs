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

            // Kiểm tra xem giảng viên có lớp học nào không
            var hasCourses = await _context.ClassRooms.AnyAsync(c => c.InstructorId == id);
            if (hasCourses)
            {
                TempData["Error"] = "Không thể xóa giảng viên đang có lớp học!";
                return RedirectToAction(nameof(Instructors));
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Xóa tài khoản thành công!";
            }
            else
            {
                TempData["Error"] = "Không thể xóa tài khoản!";
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
