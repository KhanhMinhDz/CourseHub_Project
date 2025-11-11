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
        private readonly ILogger<SubmissionsController> _logger;

        public SubmissionsController(ApplicationDbContext context, IWebHostEnvironment env, ILogger<SubmissionsController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int assignmentId)
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isInstructor = User?.IsInRole("Instructor") ?? false;

            IQueryable<Submission> query = _context.Submissions
                .Include(s => s.Assignment)
                .Where(s => s.AssignmentId == assignmentId);

            // Nếu không phải giảng viên, chỉ xem bài nộp của mình
            if (!isInstructor && userId != null)
            {
                query = query.Where(s => s.StudentId == userId);
            }

            var items = await query.OrderByDescending(s => s.SubmittedAt).ToListAsync();
            ViewBag.AssignmentId = assignmentId;
            ViewBag.IsInstructor = isInstructor;

            // Lấy thông tin assignment để hiển thị tiêu đề
            var assignment = await _context.Assignments.FindAsync(assignmentId);
            ViewBag.AssignmentTitle = assignment?.Title ?? "Bài tập";

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
            try
            {
                _logger.LogInformation("=== START: SubmissionsController.Create ===");
                _logger.LogInformation("AssignmentId: {AssignmentId}", assignmentId);
                _logger.LogInformation("File: {FileName}, Length: {Length}", file?.FileName ?? "null", file?.Length ?? 0);
                _logger.LogInformation("Comments length: {Length}", comments?.Length ?? 0);

                var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation("Current user id: {UserId}", userId);

                if (userId == null)
                {
                    _logger.LogWarning("Unauthenticated user attempted to submit for assignment {AssignmentId}", assignmentId);
                    TempData["ErrorMessage"] = "Bạn cần đăng nhập để nộp bài.";
                    return RedirectToAction("Create", new { assignmentId });
                }

                string? path = null;
                if (file != null && file.Length > 0)
                {
                    _logger.LogInformation("Processing file upload: {FileName}", file.FileName);
                    var uploads = Path.Combine(_env.WebRootPath, "uploads");
                    _logger.LogInformation("Upload directory: {Uploads}", uploads);

                    if (!Directory.Exists(uploads))
                    {
                        Directory.CreateDirectory(uploads);
                        _logger.LogInformation("Created upload directory");
                    }

                    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    path = "/uploads/" + fileName;
                    var physical = Path.Combine(uploads, fileName);

                    _logger.LogInformation("Saving file to: {Physical}", physical);
                    using var fs = System.IO.File.Create(physical);
                    await file.CopyToAsync(fs);
                    _logger.LogInformation("File saved successfully");
                }

                var submission = new Submission
                {
                    AssignmentId = assignmentId,
                    StudentId = userId,
                    FilePath = path,
                    Comments = comments,
                    SubmittedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Creating submission entity");
                _context.Submissions.Add(submission);

                _logger.LogInformation("Calling SaveChangesAsync...");
                var saveResult = await _context.SaveChangesAsync();
                _logger.LogInformation("SaveChangesAsync completed. Rows affected: {Rows}", saveResult);
                _logger.LogInformation("Submission ID: {Id}", submission.Id);

                // Verify it was saved
                var verify = await _context.Submissions.FindAsync(submission.Id);
                _logger.LogInformation("Verification: Submission exists in DB: {Exists}", verify != null);

                _logger.LogInformation("=== END: SubmissionsController.Create (SUCCESS) ===");

                // Redirect học viên về trang chi tiết bài tập với thông báo thành công
                TempData["SuccessMessage"] = "Nộp bài thành công!";
                return RedirectToAction("Details", "Assignments", new { id = assignmentId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR in SubmissionsController.Create ===");
                _logger.LogError("Exception Type: {Type}", ex.GetType().Name);
                _logger.LogError("Exception Message: {Message}", ex.Message);
                _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);

                // Show a friendly error page or return status code with message in development
                TempData["ErrorMessage"] = "Lỗi khi nộp bài: " + ex.Message;
                return RedirectToAction("Create", new { assignmentId });
            }
        }
    }
}
