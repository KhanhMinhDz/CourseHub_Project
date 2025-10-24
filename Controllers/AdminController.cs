using CourseManagement.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CourseManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // GET: Admin/CreateInstructor
        public IActionResult CreateInstructor() => View();

        [HttpPost]
        public async Task<IActionResult> CreateInstructor(CreateInstructorModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var exists = await _userManager.FindByEmailAsync(model.Email);
            if (exists != null)
            {
                ModelState.AddModelError("", "Email đã tồn tại");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors) ModelState.AddModelError("", err.Description);
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Instructor");
            return RedirectToAction("Index", "Home");
        }
    }

    public class CreateInstructorModel
    {
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
