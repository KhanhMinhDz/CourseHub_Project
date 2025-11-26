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
        private readonly IWebHostEnvironment _environment;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _environment = environment;
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
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                Gender = user.Gender,
                CurrentAvatar = user.Avatar
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

            // Cập nhật thông tin cơ bản
            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;
            user.Gender = model.Gender;

            // Xử lý upload avatar
            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                // Kiểm tra phần mở rộng file
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(model.AvatarFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("AvatarFile", "Chỉ chấp nhận file ảnh (.jpg, .jpeg, .png, .gif)");
                    return View(model);
                }

                // Kiểm tra kích thước file (giới hạn 5MB)
                if (model.AvatarFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("AvatarFile", "Kích thước file không được vượt quá 5MB");
                    return View(model);
                }

                // Tạo thư mục avatars nếu chưa tồn tại
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
                Directory.CreateDirectory(uploadsFolder);

                // Xóa avatar cũ nếu có
                if (!string.IsNullOrEmpty(user.Avatar))
                {
                    var oldAvatarPath = Path.Combine(_environment.WebRootPath, user.Avatar.TrimStart('/'));
                    if (System.IO.File.Exists(oldAvatarPath))
                    {
                        System.IO.File.Delete(oldAvatarPath);
                    }
                }

                // Lưu file mới
                var fileName = $"{user.Id}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.AvatarFile.CopyToAsync(stream);
                }

                user.Avatar = $"/uploads/avatars/{fileName}";
            }

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
