using System.Threading.Tasks;
using Coms.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Coms.Web.Controllers
{
    public class AccountController : ComsController
    {
        private readonly SignInManager<IdentityUser> _signIn;
        private readonly ILogger<AccountController> _logger;

        public AccountController(SignInManager<IdentityUser> signIn, ILogger<AccountController> logger)
        {
            _signIn = signIn;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            return View(new LoginModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (string.IsNullOrWhiteSpace(model.UserName) || string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError(string.Empty, "User name and password are required.");
                return View(model);
            }

            Microsoft.AspNetCore.Identity.SignInResult result = await _signIn.PasswordSignInAsync(model.UserName.Trim(), model.Password, isPersistent: false, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {User} signed in", model.UserName);
                return RedirectToLocal(model.ReturnUrl);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "This account is locked. Try again in 15 minutes.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid user name or password.");
            }

            model.Password = string.Empty;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            string? name = User.Identity?.Name;
            await _signIn.SignOutAsync();
            _logger.LogInformation("User {User} signed out", name);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = 403;
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
