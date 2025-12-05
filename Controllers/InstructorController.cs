using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace CourseManagement.Controllers
{
    [Authorize(Roles = "Instructor,Admin")]
    public class InstructorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public InstructorController(ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var classes = await _context.ClassRooms.Where(c => c.InstructorId == userId).ToListAsync();
            return View(classes);
        }

        [Authorize(Roles = "Instructor")]
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

        // Quản lý tất cả học viên từ tất cả các lớp của giảng viên
        public async Task<IActionResult> Students(int? classRoomId = null)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            // Lấy tất cả lớp học của giảng viên hoặc lớp cụ thể
            // Admin có thể xem tất cả lớp
            var classQuery = isAdmin ? _context.ClassRooms : _context.ClassRooms.Where(c => c.InstructorId == userId);

            if (classRoomId.HasValue)
            {
                classQuery = classQuery.Where(c => c.Id == classRoomId.Value);
                var selectedClass = await classQuery.FirstOrDefaultAsync();
                if (selectedClass != null)
                {
                    ViewBag.ClassName = selectedClass.Title;
                    ViewBag.ClassRoomId = classRoomId.Value;
                }
            }

            var classIds = await classQuery.Select(c => c.Id).ToListAsync();

            // Lấy tất cả phiên điểm danh
            var allSessions = await _context.AttendanceSessions
                .Where(s => classIds.Contains(s.ClassRoomId))
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            ViewBag.AttendanceSessions = allSessions;

            // Lấy tất cả enrollments từ các lớp đó
            var enrollments = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .Where(e => classIds.Contains(e.ClassRoomId))
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();

            // Tạo danh sách học viên với thông tin chi tiết và điểm danh
            var studentsList = new List<StudentAttendanceViewModel>();

            foreach (var enrollment in enrollments)
            {
                var user = await _userManager.FindByIdAsync(enrollment.StudentId);
                if (user != null)
                {
                    // Lấy các phiên điểm danh của lớp này
                    var classSessions = allSessions
                        .Where(s => s.ClassRoomId == enrollment.ClassRoomId)
                        .ToList();

                    // Lấy bản ghi điểm danh của học viên này
                    var studentRecords = await _context.AttendanceRecords
                        .Where(r => classSessions.Select(s => s.Id).Contains(r.AttendanceSessionId)
                                 && r.StudentId == user.Id)
                        .ToListAsync();

                    var attendanceStatuses = new List<AttendanceStatus>();
                    foreach (var session in classSessions)
                    {
                        var record = studentRecords.FirstOrDefault(r => r.AttendanceSessionId == session.Id);
                        attendanceStatuses.Add(new AttendanceStatus
                        {
                            SessionId = session.Id,
                            IsPresent = record?.IsPresent ?? false,
                            AttendedAt = record?.AttendedAt,
                            SessionDate = session.CreatedAt
                        });
                    }

                    studentsList.Add(new StudentAttendanceViewModel
                    {
                        StudentId = user.Id,
                        StudentName = user.FullName ?? user.UserName ?? "",
                        Email = user.Email ?? "",
                        AttendanceStatuses = attendanceStatuses
                    });
                }
            }

            return View(studentsList);
        }

        // Báo cáo & Thống kê điểm
        public async Task<IActionResult> Reports(int? classRoomId = null)
        {
            var userId = _userManager.GetUserId(User) ?? "";
            var isAdmin = User.IsInRole("Admin");

            // Nếu có classRoomId - hiển thị 1 lớp như cũ (cho chức năng chấm điểm trong lớp)
            if (classRoomId.HasValue)
            {
                return await ShowSingleClassReport(classRoomId.Value, userId, isAdmin);
            }

            // Không có classRoomId - hiển thị tất cả lớp (cho Báo cáo & Thống kê)
            return await ShowAllClassesReport(userId, isAdmin);
        }

        // Action phụ để hiển thị báo cáo 1 lớp (chấm điểm)
        private async Task<IActionResult> ShowSingleClassReport(int classRoomId, string userId, bool isAdmin = false)
        {
            var classQuery = isAdmin ? _context.ClassRooms.Where(c => c.Id == classRoomId) : _context.ClassRooms.Where(c => c.InstructorId == userId && c.Id == classRoomId);
            var selectedClass = await classQuery.FirstOrDefaultAsync();

            if (selectedClass != null)
            {
                ViewBag.ClassName = selectedClass.Title;
                ViewBag.ClassRoomId = classRoomId;
            }

            var classes = await classQuery.ToListAsync();
            var classIds = classes.Select(c => c.Id).ToList();

            // Lấy tất cả phiên điểm danh
            var allSessions = await _context.AttendanceSessions
                .Where(s => classIds.Contains(s.ClassRoomId))
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            ViewBag.AttendanceSessions = allSessions;

            // Lấy tất cả bài tập
            var assignments = await _context.Assignments
                .Where(a => classIds.Contains(a.ClassRoomId))
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();

            // Lấy tất cả học viên đã ghi danh
            var enrollments = await _context.Enrollments
                .Where(e => classIds.Contains(e.ClassRoomId))
                .ToListAsync();

            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

            // Lấy tất cả submissions
            var submissions = await _context.Submissions
                .Where(s => assignments.Select(a => a.Id).Contains(s.AssignmentId))
                .ToListAsync();

            // Lấy tất cả questions để xác định bài tập trắc nghiệm
            var assignmentIds = assignments.Select(a => a.Id).ToList();
            var questionsGrouped = await _context.Questions
                .Where(q => assignmentIds.Contains(q.AssignmentId))
                .GroupBy(q => q.AssignmentId)
                .Select(g => new { AssignmentId = g.Key, HasQuestions = g.Any() })
                .ToListAsync();

            var quizAssignmentIds = questionsGrouped.Where(q => q.HasQuestions).Select(q => q.AssignmentId).ToList();

            // Tạo danh sách học viên với điểm
            var studentReports = await BuildStudentReports(studentIds, allSessions, assignments, submissions, quizAssignmentIds);

            var viewModel = new GradeReportViewModel
            {
                Students = studentReports.OrderBy(s => s.StudentName).ToList(),
                Assignments = assignments
            };

            return View(viewModel);
        }

        // Action phụ để hiển thị báo cáo tất cả các lớp
        private async Task<IActionResult> ShowAllClassesReport(string userId, bool isAdmin = false)
        {
            var classes = isAdmin
                ? await _context.ClassRooms.OrderBy(c => c.Title).ToListAsync()
                : await _context.ClassRooms.Where(c => c.InstructorId == userId).OrderBy(c => c.Title).ToListAsync();

            var classReports = new List<ClassGradeReportViewModel>();

            foreach (var classRoom in classes)
            {
                // Lấy phiên điểm danh của lớp
                var sessions = await _context.AttendanceSessions
                    .Where(s => s.ClassRoomId == classRoom.Id)
                    .OrderBy(s => s.CreatedAt)
                    .ToListAsync();

                // Lấy bài tập của lớp
                var assignments = await _context.Assignments
                    .Where(a => a.ClassRoomId == classRoom.Id)
                    .OrderBy(a => a.CreatedAt)
                    .ToListAsync();

                // Lấy học viên đã ghi danh
                var enrollments = await _context.Enrollments
                    .Where(e => e.ClassRoomId == classRoom.Id)
                    .ToListAsync();

                var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

                // Lấy submissions
                var assignmentIds = assignments.Select(a => a.Id).ToList();
                var submissions = await _context.Submissions
                    .Where(s => assignmentIds.Contains(s.AssignmentId))
                    .ToListAsync();

                // Xác định quiz assignments
                var questionsGrouped = await _context.Questions
                    .Where(q => assignmentIds.Contains(q.AssignmentId))
                    .GroupBy(q => q.AssignmentId)
                    .Select(g => new { AssignmentId = g.Key, HasQuestions = g.Any() })
                    .ToListAsync();

                var quizAssignmentIds = questionsGrouped.Where(q => q.HasQuestions).Select(q => q.AssignmentId).ToList();

                // Tạo danh sách học viên với điểm
                var studentReports = await BuildStudentReports(studentIds, sessions, assignments, submissions, quizAssignmentIds);

                classReports.Add(new ClassGradeReportViewModel
                {
                    ClassId = classRoom.Id,
                    ClassName = classRoom.Title,
                    Students = studentReports.OrderBy(s => s.StudentName).ToList(),
                    Assignments = assignments,
                    AttendanceSessions = sessions
                });
            }

            var viewModel = new ReportsByClassViewModel
            {
                ClassReports = classReports
            };

            return View("ReportsByClass", viewModel);
        }

        // Helper method để tạo danh sách StudentGradeReportViewModel
        private async Task<List<StudentGradeReportViewModel>> BuildStudentReports(
            List<string> studentIds,
            List<AttendanceSession> sessions,
            List<Assignment> assignments,
            List<Submission> submissions,
            List<int> quizAssignmentIds)
        {
            var studentReports = new List<StudentGradeReportViewModel>();

            foreach (var studentId in studentIds)
            {
                var user = await _userManager.FindByIdAsync(studentId);
                if (user == null) continue;

                // Lấy thông tin điểm danh của học viên
                var studentRecords = await _context.AttendanceRecords
                    .Where(r => sessions.Select(s => s.Id).Contains(r.AttendanceSessionId)
                             && r.StudentId == user.Id)
                    .ToListAsync();

                var attendanceStatuses = new List<AttendanceStatus>();
                foreach (var session in sessions)
                {
                    var record = studentRecords.FirstOrDefault(r => r.AttendanceSessionId == session.Id);
                    attendanceStatuses.Add(new AttendanceStatus
                    {
                        SessionId = session.Id,
                        IsPresent = record?.IsPresent ?? false,
                        AttendedAt = record?.AttendedAt,
                        SessionDate = session.CreatedAt
                    });
                }

                var studentReport = new StudentGradeReportViewModel
                {
                    StudentId = user.Id,
                    StudentName = user.FullName ?? user.UserName ?? "",
                    Email = user.Email ?? "",
                    AssignmentGrades = new Dictionary<int, AssignmentGrade>(),
                    AttendanceStatuses = attendanceStatuses
                };

                // Tạo grade cho mỗi bài tập
                foreach (var assignment in assignments)
                {
                    var submission = submissions.FirstOrDefault(s =>
                        s.AssignmentId == assignment.Id && s.StudentId == studentId);

                    var isQuiz = quizAssignmentIds.Contains(assignment.Id);

                    studentReport.AssignmentGrades[assignment.Id] = new AssignmentGrade
                    {
                        AssignmentId = assignment.Id,
                        AssignmentTitle = assignment.Title,
                        IsQuiz = isQuiz,
                        Score = submission?.Score,
                        SubmissionId = submission?.Id,
                        HasSubmitted = submission != null
                    };
                }

                // Tính điểm trung bình
                var scores = studentReport.AssignmentGrades.Values
                    .Where(g => g.Score.HasValue)
                    .Select(g => g.Score!.Value)
                    .ToList();

                studentReport.AverageScore = scores.Any() ? Math.Round(scores.Average(), 2) : 0;

                studentReports.Add(studentReport);
            }

            return studentReports;
        }

        // Cập nhật điểm cho bài tập tự luận
        [HttpPost]
        public async Task<IActionResult> UpdateGrade(int submissionId, double score)
        {
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission == null)
                return Json(new { success = false, message = "Không tìm thấy bài nộp" });

            // Kiểm tra quyền
            var assignment = await _context.Assignments.FindAsync(submission.AssignmentId);
            if (assignment == null)
                return Json(new { success = false, message = "Không tìm thấy bài tập" });

            var classRoom = await _context.ClassRooms.FindAsync(assignment.ClassRoomId);
            var userId = _userManager.GetUserId(User);

            if (classRoom?.InstructorId != userId)
                return Json(new { success = false, message = "Không có quyền" });

            // Cập nhật điểm
            submission.Score = Math.Round(score, 2);
            _context.Submissions.Update(submission);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật điểm thành công" });
        }

        // Quản lý điểm danh
        public async Task<IActionResult> Attendance()
        {
            var userId = _userManager.GetUserId(User);

            // Lấy tất cả lớp học của giảng viên
            var classes = await _context.ClassRooms
                .Where(c => c.InstructorId == userId)
                .ToListAsync();

            ViewBag.Classes = classes;

            return View();
        }

        // Tạo phiên điểm danh mới
        [HttpPost]
        public async Task<IActionResult> CreateAttendanceSession(int classRoomId, int durationMinutes, string password)
        {
            var userId = _userManager.GetUserId(User);

            // Kiểm tra quyền
            var classRoom = await _context.ClassRooms.FindAsync(classRoomId);
            if (classRoom == null || classRoom.InstructorId != userId)
                return Json(new { success = false, message = "Không có quyền" });

            // Kiểm tra mật khẩu
            if (string.IsNullOrWhiteSpace(password))
                return Json(new { success = false, message = "Vui lòng nhập mật khẩu điểm danh" });

            // Tạo phiên điểm danh
            var session = new AttendanceSession
            {
                ClassRoomId = classRoomId,
                CreatedAt = DateTime.Now,
                CloseAt = DateTime.Now.AddMinutes(durationMinutes),
                IsActive = true,
                Password = password.Trim()
            };

            _context.AttendanceSessions.Add(session);
            await _context.SaveChangesAsync();

            // Tạo bản ghi điểm danh cho tất cả học viên (mặc định vắng)
            var enrollments = await _context.Enrollments
                .Where(e => e.ClassRoomId == classRoomId)
                .ToListAsync();

            foreach (var enrollment in enrollments)
            {
                var record = new AttendanceRecord
                {
                    AttendanceSessionId = session.Id,
                    StudentId = enrollment.StudentId,
                    IsPresent = false,
                    AttendedAt = null
                };
                _context.AttendanceRecords.Add(record);
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã mở điểm danh thành công",
                sessionId = session.Id,
                closeAt = session.CloseAt.ToString("HH:mm dd/MM/yyyy")
            });
        }

        // Xem chi tiết các phiên điểm danh
        public async Task<IActionResult> AttendanceSessions(int classRoomId)
        {
            var userId = _userManager.GetUserId(User);

            var classRoom = await _context.ClassRooms.FindAsync(classRoomId);
            if (classRoom == null || classRoom.InstructorId != userId)
                return Forbid();

            var sessions = await _context.AttendanceSessions
                .Where(s => s.ClassRoomId == classRoomId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var viewModels = new List<AttendanceViewModel>();

            foreach (var session in sessions)
            {
                var records = await _context.AttendanceRecords
                    .Where(r => r.AttendanceSessionId == session.Id)
                    .ToListAsync();

                viewModels.Add(new AttendanceViewModel
                {
                    SessionId = session.Id,
                    CreatedAt = session.CreatedAt,
                    CloseAt = session.CloseAt,
                    IsActive = session.IsActive && DateTime.Now < session.CloseAt,
                    TotalStudents = records.Count,
                    PresentCount = records.Count(r => r.IsPresent),
                    AbsentCount = records.Count(r => !r.IsPresent)
                });
            }

            ViewBag.ClassName = classRoom.Title;
            return View(viewModels);
        }

        // Đóng phiên điểm danh
        [HttpPost]
        public async Task<IActionResult> CloseAttendanceSession(int sessionId)
        {
            var session = await _context.AttendanceSessions.FindAsync(sessionId);
            if (session == null)
                return Json(new { success = false, message = "Không tìm thấy phiên điểm danh" });

            var userId = _userManager.GetUserId(User);
            var classRoom = await _context.ClassRooms.FindAsync(session.ClassRoomId);

            if (classRoom?.InstructorId != userId)
                return Json(new { success = false, message = "Không có quyền" });

            session.IsActive = false;
            _context.AttendanceSessions.Update(session);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã đóng điểm danh" });
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

        // Xuất báo cáo Excel
        public async Task<IActionResult> ExportToExcel(int? classRoomId = null)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var userId = _userManager.GetUserId(User) ?? "";
            var isAdmin = User.IsInRole("Admin");

            using var package = new ExcelPackage();

            // Nếu có classRoomId - xuất 1 lớp cụ thể (chức năng cũ)
            if (classRoomId.HasValue)
            {
                await ExportSingleClass(package, userId, classRoomId.Value, isAdmin);
            }
            else
            {
                // Xuất tất cả các lớp - mỗi lớp 1 sheet
                await ExportAllClasses(package, userId, isAdmin);
            }

            // Return file
            var stream = new MemoryStream();
            package.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"BaoCaoDiem_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // Helper method để xuất 1 lớp cụ thể
        private async Task ExportSingleClass(ExcelPackage package, string userId, int classRoomId, bool isAdmin = false)
        {
            var classQuery = isAdmin
                ? _context.ClassRooms.Where(c => c.Id == classRoomId)
                : _context.ClassRooms.Where(c => c.InstructorId == userId && c.Id == classRoomId);
            var classes = await classQuery.ToListAsync();

            if (!classes.Any()) return;

            var classRoom = classes.First();
            await ExportClassToSheet(package, classRoom, classRoom.Title);
        }

        // Helper method để xuất tất cả các lớp
        private async Task ExportAllClasses(ExcelPackage package, string userId, bool isAdmin = false)
        {
            var classes = isAdmin
                ? await _context.ClassRooms.OrderBy(c => c.Title).ToListAsync()
                : await _context.ClassRooms.Where(c => c.InstructorId == userId).OrderBy(c => c.Title).ToListAsync();

            foreach (var classRoom in classes)
            {
                // Tạo tên sheet hợp lệ (Excel giới hạn 31 ký tự)
                var sheetName = classRoom.Title.Length > 31
                    ? classRoom.Title.Substring(0, 31)
                    : classRoom.Title;

                // Thay thế các ký tự không hợp lệ
                sheetName = sheetName.Replace(":", "").Replace("/", "-").Replace("\\", "-")
                    .Replace("?", "").Replace("*", "").Replace("[", "").Replace("]", "");

                await ExportClassToSheet(package, classRoom, sheetName);
            }
        }

        // Helper method để xuất 1 lớp vào 1 sheet
        private async Task ExportClassToSheet(ExcelPackage package, ClassRoom classRoom, string sheetName)
        {
            var worksheet = package.Workbook.Worksheets.Add(sheetName);

            // Lấy phiên điểm danh
            var allSessions = await _context.AttendanceSessions
                .Where(s => s.ClassRoomId == classRoom.Id)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            // Lấy bài tập
            var assignments = await _context.Assignments
                .Where(a => a.ClassRoomId == classRoom.Id)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();

            // Lấy học viên
            var enrollments = await _context.Enrollments
                .Where(e => e.ClassRoomId == classRoom.Id)
                .ToListAsync();

            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

            // Lấy submissions
            var assignmentIds = assignments.Select(a => a.Id).ToList();
            var submissions = await _context.Submissions
                .Where(s => assignmentIds.Contains(s.AssignmentId))
                .ToListAsync();

            // Xác định quiz assignments
            var questionsGrouped = await _context.Questions
                .Where(q => assignmentIds.Contains(q.AssignmentId))
                .GroupBy(q => q.AssignmentId)
                .Select(g => new { AssignmentId = g.Key, HasQuestions = g.Any() })
                .ToListAsync();

            var quizAssignmentIds = questionsGrouped.Where(q => q.HasQuestions).Select(q => q.AssignmentId).ToList();

            // Header row 1
            int col = 1;
            worksheet.Cells[1, col++].Value = "STT";
            worksheet.Cells[1, col++].Value = "Họ tên";

            // Merge cells cho Điểm danh
            int attendanceStartCol = col;
            if (allSessions.Any())
            {
                worksheet.Cells[1, col, 1, col + allSessions.Count - 1].Merge = true;
                worksheet.Cells[1, col].Value = "Điểm danh";
                col += allSessions.Count;
            }

            // Merge cells cho Bài tập
            int assignmentStartCol = col;
            if (assignments.Any())
            {
                worksheet.Cells[1, col, 1, col + assignments.Count - 1].Merge = true;
                worksheet.Cells[1, col].Value = "Bài tập";
                col += assignments.Count;
            }

            worksheet.Cells[1, col].Value = "Điểm TB";

            // Header row 2
            col = 1;
            worksheet.Cells[2, col++].Value = "";
            worksheet.Cells[2, col++].Value = "";

            // Điểm danh columns
            foreach (var session in allSessions)
            {
                worksheet.Cells[2, col++].Value = session.CreatedAt.ToString("dd/MM HH:mm");
            }

            // Assignment columns
            foreach (var assignment in assignments)
            {
                var isQuiz = quizAssignmentIds.Contains(assignment.Id);
                worksheet.Cells[2, col++].Value = $"{assignment.Title} ({(isQuiz ? "TN" : "TL")})";
            }

            worksheet.Cells[2, col].Value = "";

            // Data rows
            int row = 3;
            int index = 1;

            foreach (var studentId in studentIds)
            {
                var user = await _userManager.FindByIdAsync(studentId);
                if (user == null) continue;

                col = 1;
                worksheet.Cells[row, col++].Value = index++;
                worksheet.Cells[row, col++].Value = user.FullName ?? user.UserName;

                // Điểm danh data
                var studentRecords = await _context.AttendanceRecords
                    .Where(r => allSessions.Select(s => s.Id).Contains(r.AttendanceSessionId)
                             && r.StudentId == studentId)
                    .ToListAsync();

                foreach (var session in allSessions)
                {
                    var record = studentRecords.FirstOrDefault(r => r.AttendanceSessionId == session.Id);
                    worksheet.Cells[row, col].Value = record?.IsPresent == true ? "✓" : "✗";

                    // Color coding
                    if (record?.IsPresent == true)
                    {
                        worksheet.Cells[row, col].Style.Font.Color.SetColor(Color.Green);
                    }
                    else
                    {
                        worksheet.Cells[row, col].Style.Font.Color.SetColor(Color.Red);
                    }
                    col++;
                }

                // Assignment scores
                double totalScore = 0;
                int scoreCount = 0;

                foreach (var assignment in assignments)
                {
                    var submission = submissions.FirstOrDefault(s =>
                        s.AssignmentId == assignment.Id && s.StudentId == studentId);

                    if (submission != null && submission.Score.HasValue)
                    {
                        worksheet.Cells[row, col].Value = submission.Score.Value;
                        totalScore += submission.Score.Value;
                        scoreCount++;
                    }
                    else if (submission != null)
                    {
                        worksheet.Cells[row, col].Value = "Chưa chấm";
                    }
                    else
                    {
                        worksheet.Cells[row, col].Value = "Chưa nộp";
                    }
                    col++;
                }

                // Average score
                var avgScore = scoreCount > 0 ? Math.Round(totalScore / scoreCount, 2) : 0;
                worksheet.Cells[row, col].Value = avgScore;

                row++;
            }

            // Styling nếu có dữ liệu
            if (row > 3)
            {
                // Header styling
                using (var range = worksheet.Cells[1, 1, 2, col])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                // Auto fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Borders
                using (var range = worksheet.Cells[1, 1, row - 1, col])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                // Center align all cells
                using (var range = worksheet.Cells[1, 1, row - 1, col])
                {
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
            }
            else
            {
                // Không có học viên - hiển thị thông báo
                worksheet.Cells[3, 1].Value = "Chưa có học viên nào ghi danh vào lớp này";
                worksheet.Cells[3, 1, 3, col].Merge = true;
                worksheet.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[3, 1].Style.Font.Italic = true;
            }
        }

        // Xóa khóa học
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cls = await _context.ClassRooms.FindAsync(id);
            if (cls == null)
            {
                TempData["Error"] = "Không tìm thấy khóa học";
                return RedirectToAction(nameof(Index));
            }

            var userId = _userManager.GetUserId(User);
            if (cls.InstructorId != userId)
            {
                TempData["Error"] = "Bạn không có quyền xóa khóa học này";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Xóa tất cả dữ liệu liên quan

                // 1. Xóa các bái tập và dữ liệu liên quan
                var assignments = await _context.Assignments
                    .Where(a => a.ClassRoomId == id)
                    .ToListAsync();

                foreach (var assignment in assignments)
                {
                    // Xóa câu hỏi của bài tập (nếu có)
                    var questions = await _context.Questions
                        .Where(q => q.AssignmentId == assignment.Id)
                        .ToListAsync();
                    _context.Questions.RemoveRange(questions);

                    // Xóa các bài nộp
                    var submissions = await _context.Submissions
                        .Where(s => s.AssignmentId == assignment.Id)
                        .ToListAsync();
                    _context.Submissions.RemoveRange(submissions);
                }

                _context.Assignments.RemoveRange(assignments);

                // 2. Xóa nội dung lớp học
                var contentBlocks = await _context.ContentBlocks
                    .Where(c => c.ClassRoomId == id)
                    .ToListAsync();
                _context.ContentBlocks.RemoveRange(contentBlocks);

                // 3. Xóa các phiên điểm danh và bản ghi điểm danh
                var attendanceSessions = await _context.AttendanceSessions
                    .Where(s => s.ClassRoomId == id)
                    .ToListAsync();

                foreach (var session in attendanceSessions)
                {
                    var attendanceRecords = await _context.AttendanceRecords
                        .Where(r => r.AttendanceSessionId == session.Id)
                        .ToListAsync();
                    _context.AttendanceRecords.RemoveRange(attendanceRecords);
                }

                _context.AttendanceSessions.RemoveRange(attendanceSessions);

                // 4. Xóa các ghi danh
                var enrollments = await _context.Enrollments
                    .Where(e => e.ClassRoomId == id)
                    .ToListAsync();
                _context.Enrollments.RemoveRange(enrollments);

                // 5. Cuối cùng xóa khóa học
                _context.ClassRooms.Remove(cls);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Xóa khóa học '{cls.Title}' thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Có lỗi xảy ra khi xóa khóa học: {ex.Message}";
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        // Xóa học viên khỏi khóa học
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveStudent(int classRoomId, string studentId)
        {
            var classRoom = await _context.ClassRooms.FindAsync(classRoomId);
            if (classRoom == null)
            {
                return Json(new { success = false, message = "Không tìm thấy khóa học" });
            }

            var userId = _userManager.GetUserId(User);
            if (classRoom.InstructorId != userId)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa học viên khỏi khóa học này" });
            }

            try
            {
                // Tìm enrollment của học viên
                var enrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.ClassRoomId == classRoomId && e.StudentId == studentId);

                if (enrollment == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin ghi danh" });
                }

                // Xóa enrollment
                _context.Enrollments.Remove(enrollment);

                // Xóa các bản ghi điểm danh của học viên trong lớp này
                var attendanceSessions = await _context.AttendanceSessions
                    .Where(s => s.ClassRoomId == classRoomId)
                    .Select(s => s.Id)
                    .ToListAsync();

                var attendanceRecords = await _context.AttendanceRecords
                    .Where(r => attendanceSessions.Contains(r.AttendanceSessionId) && r.StudentId == studentId)
                    .ToListAsync();

                _context.AttendanceRecords.RemoveRange(attendanceRecords);

                // Xóa các bài nộp của học viên trong lớp này
                var assignments = await _context.Assignments
                    .Where(a => a.ClassRoomId == classRoomId)
                    .Select(a => a.Id)
                    .ToListAsync();

                var submissions = await _context.Submissions
                    .Where(s => assignments.Contains(s.AssignmentId) && s.StudentId == studentId)
                    .ToListAsync();

                _context.Submissions.RemoveRange(submissions);

                await _context.SaveChangesAsync();

                var student = await _userManager.FindByIdAsync(studentId);
                var studentName = student?.FullName ?? student?.UserName ?? "Học viên";

                return Json(new
                {
                    success = true,
                    message = $"Đã xóa {studentName} khỏi khóa học thành công"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
            }
        }

    }
}
