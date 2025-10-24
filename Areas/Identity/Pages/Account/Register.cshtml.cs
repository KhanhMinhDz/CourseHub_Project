using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using CourseManagement.Data;
using System.Linq;

namespace CourseManagement.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string ReturnUrl { get; set; } = string.Empty;

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string? Email { get; set; }

            [Required]
            public string? FullName { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string? Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string? ConfirmPassword { get; set; }
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            // Diagnostic logging: dump Request.Form keys and mask sensitive values
            try
            {
                var keys = Request.Form.Keys.Cast<string>().ToArray();
                _logger.LogInformation("Request.Form keys: {Keys}", string.Join(", ", keys));
                foreach (var k in keys)
                {
                    var v = Request.Form[k];
                    var display = (k?.IndexOf("password", System.StringComparison.OrdinalIgnoreCase) >= 0 || string.Equals(k, "__RequestVerificationToken"))
                        ? "[MASKED]"
                        : v.ToString();
                    _logger.LogDebug("Form[{Key}] = {Value}", k, display);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to read Request.Form");
            }

            returnUrl ??= Url.Content("~/");
            if (!ModelState.IsValid)
            {
                // Log ModelState errors for diagnosis
                foreach (var kv in ModelState)
                {
                    var key = kv.Key;
                    foreach (var err in kv.Value.Errors)
                    {
                        _logger.LogWarning("ModelState[{Key}] error: {Error}", key, err.ErrorMessage);
                    }
                }
                return Page();
            }

            if (string.IsNullOrEmpty(Input.Email) || string.IsNullOrEmpty(Input.Password) || string.IsNullOrEmpty(Input.FullName))
            {
                ModelState.AddModelError(string.Empty, "Email, full name and password are required.");
                _logger.LogWarning("Registration attempt with missing required fields: Email={Email}, FullNameProvided={HasName}", Input?.Email, !string.IsNullOrEmpty(Input?.FullName));
                return Page();
            }

            var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email, FullName = Input.FullName };
            var result = await _userManager.CreateAsync(user, Input.Password);
            if (result.Succeeded)
            {
                // assign role Student by default
                await _userManager.AddToRoleAsync(user, "Student");
                await _signInManager.SignInAsync(user, isPersistent: false);
                _logger.LogInformation("New user registered: {Email}", user.Email);
                return LocalRedirect(returnUrl);
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                _logger.LogWarning("Registration error for {Email}: {Error}", Input?.Email, error.Description);
            }
            return Page();
        }
    }
}
