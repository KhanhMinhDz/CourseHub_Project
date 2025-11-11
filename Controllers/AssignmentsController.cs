using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;
using System.IO.Compression;

namespace CourseManagement.Controllers
{
    [Authorize]
    public class AssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AssignmentsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
        public async Task<IActionResult> EditDescription(int id, string? description, string? title, DateTime? dueDate)
        {
            var a = await _context.Assignments.FindAsync(id);
            if (a == null) return NotFound();
            var cls = await _context.ClassRooms.FindAsync(a.ClassRoomId);
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User?.IsInRole("Admin") ?? false;
            if (cls != null && cls.InstructorId != null && cls.InstructorId != userId && !isAdmin) return Forbid();

            if (!string.IsNullOrEmpty(title)) a.Title = title;
            a.Description = description;
            a.DueDate = dueDate?.ToUniversalTime(); // Convert to UTC for database

            _context.Assignments.Update(a);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật bài tập thành công!";
            return RedirectToAction(nameof(Details), new { id = a.Id });
        }

        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> DownloadAllSubmissions(int id)
        {
            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null) return NotFound();

            var cls = await _context.ClassRooms.FindAsync(assignment.ClassRoomId);
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User?.IsInRole("Admin") ?? false;
            if (cls != null && cls.InstructorId != null && cls.InstructorId != userId && !isAdmin) return Forbid();

            var subs = await _context.Submissions
                .Where(s => s.AssignmentId == id && !string.IsNullOrEmpty(s.FilePath))
                .ToListAsync();

            if (!subs.Any())
            {
                TempData["Error"] = "Không có file nào để tải.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var s in subs)
                {
                    var rel = s.FilePath?.TrimStart('/', '\\') ?? string.Empty;
                    if (string.IsNullOrEmpty(rel)) continue;

                    var physical = Path.Combine(_env.WebRootPath ?? "wwwroot", rel);
                    if (!System.IO.File.Exists(physical)) continue;

                    var entryName = $"{s.StudentId}_{Path.GetFileName(physical)}";
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var fileStream = System.IO.File.OpenRead(physical);
                    await fileStream.CopyToAsync(entryStream);
                }
            }

            memoryStream.Position = 0;
            var zipName = $"{(assignment.Title ?? "submissions").Replace(' ', '_')}_{DateTime.Now:yyyyMMddHHmmss}.zip";
            return File(memoryStream, "application/zip", zipName);
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
        [HttpPost]
        [Authorize(Roles = "Instructor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadQuestions(int assignmentId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file CSV hoặc Excel hợp lệ.";
                return RedirectToAction(nameof(Details), new { id = assignmentId });
            }

            var assignment = await _context.Assignments.FindAsync(assignmentId);
            if (assignment == null) return NotFound();

            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var cls = await _context.ClassRooms.FindAsync(assignment.ClassRoomId);
            var isAdmin = User?.IsInRole("Admin") ?? false;
            if (cls != null && cls.InstructorId != null && cls.InstructorId != userId && !isAdmin)
                return Forbid();

            var questions = new List<Question>();

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                stream.Position = 0;

                if (file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(stream);
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var parts = line.Split(',').Select(p => p.Trim()).ToList();
                        if (parts.Count < 3) continue;

                        string content = parts[0];
                        string correct = parts[^2];
                        bool multiple = bool.TryParse(parts[^1], out bool m) && m;

                        var answers = parts.Skip(1).Take(parts.Count - 2).ToList();

                        questions.Add(new Question
                        {
                            AssignmentId = assignmentId,
                            Content = content,
                            Options = answers,
                            CorrectAnswers = correct,
                            AllowMultiple = multiple
                        });
                    }
                }
                else if (file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    using var package = new OfficeOpenXml.ExcelPackage(stream);
                    var ws = package.Workbook.Worksheets.First();
                    int rowCount = ws.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var rowValues = new List<string>();
                        for (int col = 1; col <= ws.Dimension.Columns; col++)
                            rowValues.Add(ws.Cells[row, col].Text.Trim());

                        if (rowValues.Count < 3) continue;

                        string content = rowValues[0];
                        string correct = rowValues[^2];
                        bool multiple = bool.TryParse(rowValues[^1], out bool m) && m;
                        var answers = rowValues.Skip(1).Take(rowValues.Count - 2).ToList();

                        questions.Add(new Question
                        {
                            AssignmentId = assignmentId,
                            Content = content,
                            Options = answers,
                            CorrectAnswers = correct,
                            AllowMultiple = multiple
                        });
                    }
                }
                else
                {
                    TempData["Error"] = "Định dạng file không hợp lệ (chỉ chấp nhận CSV hoặc XLSX).";
                    return RedirectToAction(nameof(Details), new { id = assignmentId });
                }
            }

            _context.Questions.AddRange(questions);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm {questions.Count} câu hỏi từ file {file.FileName}.";
            return RedirectToAction(nameof(Details), new { id = assignmentId });
        }
        public async Task<IActionResult> TakeExam(int id)
        {
            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null) return NotFound();

            var questions = await _context.Questions
            .Where(q => q.AssignmentId == id)
            .OrderBy(q => q.Id)
            .ToListAsync();


            ViewBag.Questions = questions;
            return View(assignment);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitExam(int assignmentId, IFormCollection form, IFormFile? submissionFile, string? studentNote)
        {
            var assignment = await _context.Assignments.FindAsync(assignmentId);
            if (assignment == null) return NotFound();

            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Forbid();

            // Load questions for this assignment
            var questions = await _context.Questions
                .Where(q => q.AssignmentId == assignmentId)
                .ToListAsync();

            // CASE A: no questions -> treat as file-based (tự luận)
            if (questions == null || !questions.Any())
            {
                if (submissionFile == null || submissionFile.Length == 0)
                {
                    TempData["Error"] = "Vui lòng chọn file để nộp.";
                    return RedirectToAction(nameof(Details), new { id = assignmentId });
                }

                // basic validation: size limit (e.g. 20MB) and extensions
                var maxBytes = 20 * 1024 * 1024;
                if (submissionFile.Length > maxBytes)
                {
                    TempData["Error"] = "File quá lớn (giới hạn 20 MB).";
                    return RedirectToAction(nameof(Details), new { id = assignmentId });
                }

                var allowed = new[] { ".pdf", ".docx", ".doc", ".zip", ".rar", ".txt" };
                var ext = Path.GetExtension(submissionFile.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    TempData["Error"] = "Loại file không được hỗ trợ.";
                    return RedirectToAction(nameof(Details), new { id = assignmentId });
                }

                var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
                Directory.CreateDirectory(uploads);

                var fileName = $"{Guid.NewGuid()}{ext}";
                var physical = Path.Combine(uploads, fileName);
                using (var stream = new FileStream(physical, FileMode.Create))
                {
                    await submissionFile.CopyToAsync(stream);
                }

                var submission = new Submission
                {
                    AssignmentId = assignmentId,
                    StudentId = userId,
                    FilePath = $"/uploads/{fileName}",
                    Comments = studentNote,
                    SubmittedAt = DateTime.UtcNow
                };

                _context.Submissions.Add(submission);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Nộp bài thành công.";
                TempData["SuccessMessage"] = "Nộp bài thành công!";
                return RedirectToAction(nameof(Details), new { id = assignmentId });
            }

            // CASE B: xử lý câu hỏi trắc nghiệm như hiện tại
            // ...giữ lại logic hiện có (tính đáp án, lưu điểm, tạo bản ghi Submission/Answer nếu cần)...
            // Ví dụ: xử lý form["answer_{id}"] như code cũ rồi lưu kết quả.

            // Sau khi xử lý, redirect về Details:
            return RedirectToAction(nameof(Details), new { id = assignmentId });
        }

        // POST: /Assignments/SubmitQuiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitQuiz(int assignmentId, IFormCollection form)
        {
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var assignment = await _context.Assignments
                .Include(a => a.ClassRoom)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null) return NotFound();

            // Kiểm tra học viên đã đăng ký lớp chưa
            var enrolled = await _context.Enrollments
                .AnyAsync(e => e.ClassRoomId == assignment.ClassRoomId && e.StudentId == userId);
            if (!enrolled)
            {
                TempData["Error"] = "Bạn chưa đăng ký lớp học này.";
                return RedirectToAction(nameof(Details), new { id = assignmentId });
            }

            // Lấy tất cả câu hỏi
            var questions = await _context.Questions
                .Where(q => q.AssignmentId == assignmentId)
                .OrderBy(q => q.Id)
                .ToListAsync();

            if (!questions.Any())
            {
                TempData["Error"] = "Bài tập này không có câu hỏi.";
                return RedirectToAction(nameof(Details), new { id = assignmentId });
            }

            // Tính điểm mỗi câu: 10 / tổng số câu hỏi
            double pointsPerQuestion = 10.0 / questions.Count;
            int correctCount = 0;

            var questionResults = new List<QuestionResult>();

            foreach (var q in questions)
            {
                var studentAnswers = new List<string>();

                // Lấy đáp án học viên đã chọn từ form
                var answerKey = $"answer_{q.Id}";
                if (form.ContainsKey(answerKey))
                {
                    studentAnswers = form[answerKey].Where(a => a != null).Select(a => a!).ToList();
                }

                // Lấy đáp án đúng
                var correctAnswers = q.CorrectAnswers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .ToList();

                // So sánh đáp án
                bool isCorrect = studentAnswers.Count == correctAnswers.Count &&
                                studentAnswers.All(a => correctAnswers.Contains(a));

                if (isCorrect) correctCount++;

                questionResults.Add(new QuestionResult
                {
                    QuestionId = q.Id,
                    Content = q.Content,
                    Options = q.Options,
                    CorrectAnswers = correctAnswers,
                    StudentAnswers = studentAnswers,
                    IsCorrect = isCorrect,
                    PointsPerQuestion = pointsPerQuestion
                });
            }

            // Tính điểm tổng (làm tròn 2 chữ số)
            double totalScore = Math.Round(correctCount * pointsPerQuestion, 2);

            // Lưu submission vào database
            var submission = new Submission
            {
                AssignmentId = assignmentId,
                StudentId = userId,
                FilePath = "", // Bài trắc nghiệm không có file
                Comments = $"Điểm: {totalScore}/10 - Số câu đúng: {correctCount}/{questions.Count}",
                Score = totalScore, // Lưu điểm số
                SubmittedAt = DateTime.UtcNow
            };

            _context.Submissions.Add(submission);
            await _context.SaveChangesAsync();

            // Tạo ViewModel để hiển thị kết quả
            var viewModel = new QuizResultViewModel
            {
                AssignmentId = assignmentId,
                AssignmentTitle = assignment.Title,
                QuestionResults = questionResults,
                TotalQuestions = questions.Count,
                CorrectAnswers = correctCount,
                Score = totalScore,
                MaxScore = 10.0
            };

            return View("QuizResult", viewModel);
        }

        // POST: /Assignments/Delete/5
        [HttpPost]
        [Authorize(Roles = "Instructor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null) return NotFound();

            var cls = await _context.ClassRooms.FindAsync(assignment.ClassRoomId);
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User?.IsInRole("Admin") ?? false;
            if (cls != null && cls.InstructorId != null && cls.InstructorId != userId && !isAdmin) return Forbid();

            // Remove related questions
            var questions = _context.Questions.Where(q => q.AssignmentId == id);
            _context.Questions.RemoveRange(questions);

            // Remove submissions and their files
            var submissions = await _context.Submissions.Where(s => s.AssignmentId == id).ToListAsync();
            foreach (var s in submissions)
            {
                if (!string.IsNullOrEmpty(s.FilePath))
                {
                    var rel = s.FilePath.TrimStart('/', '\\');
                    var physical = Path.Combine(_env.WebRootPath ?? "wwwroot", rel);
                    try { if (System.IO.File.Exists(physical)) System.IO.File.Delete(physical); } catch { }
                }
            }
            _context.Submissions.RemoveRange(submissions);

            // Finally remove assignment
            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa bài tập thành công.";
            // Redirect back to the classroom edit page
            return RedirectToAction("Edit", "Instructor", new { id = assignment.ClassRoomId });
        }

    }
}
