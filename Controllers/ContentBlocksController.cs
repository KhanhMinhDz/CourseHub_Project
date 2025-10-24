using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Controllers
{
    [Authorize(Roles = "Instructor")]
    public class ContentBlocksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<CourseManagement.Data.ApplicationUser> _userManager;

        public ContentBlocksController(ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<CourseManagement.Data.ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int classId, string content)
        {
            var cls = await _context.ClassRooms.FindAsync(classId);
            if (cls == null) return NotFound();
            var userId = _userManager.GetUserId(User);
            if (cls.InstructorId != null && cls.InstructorId != userId && !(User.IsInRole("Admin"))) return Forbid();

            var maxOrder = await _context.ContentBlocks.Where(cb => cb.ClassRoomId == classId).MaxAsync(cb => (int?)cb.Order) ?? 0;
            var cbNew = new ContentBlock { ClassRoomId = classId, Content = content ?? string.Empty, Order = maxOrder + 1 };
            _context.ContentBlocks.Add(cbNew);
            await _context.SaveChangesAsync();
            return PartialView("_ContentBlock", cbNew);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, string content)
        {
            var cb = await _context.ContentBlocks.FindAsync(id);
            if (cb == null) return NotFound();
            var cls = await _context.ClassRooms.FindAsync(cb.ClassRoomId);
            var userId = _userManager.GetUserId(User);
            if (cls != null && cls.InstructorId != null && cls.InstructorId != userId && !(User.IsInRole("Admin"))) return Forbid();
            cb.Content = content ?? string.Empty;
            cb.UpdatedAt = DateTime.UtcNow;
            _context.ContentBlocks.Update(cb);
            await _context.SaveChangesAsync();
            return PartialView("_ContentBlock", cb);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cb = await _context.ContentBlocks.FindAsync(id);
            if (cb == null) return NotFound();
            var cls = await _context.ClassRooms.FindAsync(cb.ClassRoomId);
            var userId = _userManager.GetUserId(User);
            if (cls != null && cls.InstructorId != null && cls.InstructorId != userId && !(User.IsInRole("Admin"))) return Forbid();
            _context.ContentBlocks.Remove(cb);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
