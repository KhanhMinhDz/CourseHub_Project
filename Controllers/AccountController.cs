using CourseManagement.Data;
using CourseManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CourseManagement.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET: Account/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            var model = new EditProfileViewModel
            {
                FullName = user.FullName ?? "",
                Email = user.Email ?? ""
            };

            return View(model);
        }

        // POST: Account/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            user.FullName = model.FullName;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Cập nhật thông tin thành công!";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // GET: Account/ChangePassword
        [HttpGet]
        public IActionResult ChangePassword()
        {
            var model = new ChangePasswordViewModel
            {
                Email = User.Identity?.Name ?? ""
            };
            return View(model);
        }

        // POST: Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Kiểm tra email có khớp với tài khoản đang đăng nhập không
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return NotFound();

            if (currentUser.Email?.ToLower() != model.Email.ToLower())
            {
                ModelState.AddModelError("Email", "Email không khớp với tài khoản đang đăng nhập");
                return View(model);
            }

            // Kiểm tra mật khẩu cũ
            var passwordCheck = await _userManager.CheckPasswordAsync(currentUser, model.OldPassword);
            if (!passwordCheck)
            {
                ModelState.AddModelError("OldPassword", "Mật khẩu cũ không đúng");
                return View(model);
            }

            // Đổi mật khẩu
            var result = await _userManager.ChangePasswordAsync(currentUser, model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                // Đăng nhập lại với mật khẩu mới
                await _signInManager.RefreshSignInAsync(currentUser);
                TempData["Success"] = "Đổi mật khẩu thành công!";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }
    }
}
