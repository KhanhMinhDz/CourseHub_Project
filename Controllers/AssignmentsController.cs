using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

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
        public async Task<IActionResult> SubmitExam(int assignmentId, IFormCollection form)
        {
            var questions = await _context.Questions
                .Where(q => q.AssignmentId == assignmentId)
                .ToListAsync();

            if (questions.Count == 0)
            {
                ViewBag.Score = 0;
                ViewBag.Total = 10;
                ViewBag.AssignmentId = assignmentId;
                return View("ExamResult");
            }

            double totalScore = 10.0;
            double pointsPerQuestion = totalScore / questions.Count;
            double gainedScore = 0.0;

            string[] NormalizeCorrectAnswers(Question q)
            {
                if (string.IsNullOrWhiteSpace(q.CorrectAnswers)) return Array.Empty<string>();

                var parts = q.CorrectAnswers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim()).ToArray();

                if (parts.All(p => p.All(char.IsDigit)))
                    return parts;

                var letters = parts.Select(p => p.Trim().ToUpper()).ToArray();
                var list = new List<string>();
                var options = JsonConvert.DeserializeObject<List<string>>(q.OptionsJson ?? "[]");
                for (int i = 0; i < options.Count; i++)
                {
                    string letter = ((char)('A' + i)).ToString();
                    if (letters.Contains(letter)) list.Add(i.ToString());
                }
                return list.ToArray();
            }

            foreach (var q in questions)
            {
                var options = JsonConvert.DeserializeObject<List<string>>(q.OptionsJson ?? "[]");

                var fieldName = $"answer_{q.Id}";
                var values = form[fieldName];
                var selected = values.ToArray();

                var correctSet = new HashSet<string>(NormalizeCorrectAnswers(q));
                var selectedSet = new HashSet<string>();

                foreach (var s in selected)
                {
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    var t = s.Trim();
                    if (t.All(char.IsDigit))
                    {
                        selectedSet.Add(t);
                    }
                    else
                    {
                        var up = t.ToUpper();
                        int idx = up[0] - 'A';
                        if (idx >= 0 && idx < options.Count) selectedSet.Add(idx.ToString());
                    }
                }

                bool correct = false;
                if (q.AllowMultiple)
                    correct = selectedSet.SetEquals(correctSet);
                else if (selectedSet.Count > 0)
                    correct = correctSet.Overlaps(selectedSet);

                if (correct)
                    gainedScore += pointsPerQuestion;
            }

            gainedScore = Math.Round(gainedScore, 2);

            ViewBag.Score = gainedScore;
            ViewBag.Total = totalScore;
            ViewBag.AssignmentId = assignmentId;

            return View("ExamResult");
        }

    }
}
