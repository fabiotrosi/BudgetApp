using System.Security.Claims;
using System.Security.Cryptography;
using BudgetApp.Data.Repositories;
using BudgetApp.Enums;
using BudgetApp.Extensions;
using BudgetApp.Models;
using BudgetApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BudgetApp.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly IUserRepository<UserModel> _userRepo;
        private readonly IPasswordHasher<UserModel> _passwordHasher;
        private readonly IEmailService _emailService;

        public AccountController(
            ILogger<AccountController> logger,
            IUserRepository<UserModel> userRepo,
            IPasswordHasher<UserModel> passwordHasher,
            IEmailService emailService
        )
        {
            _logger = logger;
            _userRepo = userRepo;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(vm);

            var user = await _userRepo.GetByEmail(vm.Email);

            if (
                user == null
                || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, vm.Password)
                    == PasswordVerificationResult.Failed
            )
            {
                ModelState.AddModelError(string.Empty, "E-Mail oder Passwort ungültig.");
                return View(vm);
            }

            await SignInUser(user);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var existing = await _userRepo.GetByEmail(vm.Email);
            if (existing != null)
            {
                ModelState.AddModelError(
                    nameof(vm.Email),
                    "Diese E-Mail-Adresse ist bereits registriert."
                );
                return View(vm);
            }

            try
            {
                var user = new UserModel
                {
                    Email = vm.Email.Trim().ToLowerInvariant(),
                    DisplayName = vm.DisplayName.Trim(),
                    PasswordHash = string.Empty,
                };
                user.PasswordHash = _passwordHasher.HashPassword(user, vm.Password);

                int newId = await _userRepo.Create(user);
                user.Id = newId;

                user.EmailConfirmationToken = Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(32)
                );
                user.EmailConfirmationTokenExpiry = DateTime.UtcNow.AddHours(24);
                await _userRepo.Update(user);

                try
                {
                    var confirmUrl = Url.Action(
                        "ConfirmEmail",
                        "Account",
                        new { token = user.EmailConfirmationToken },
                        Request.Scheme
                    )!;
                    await _emailService.SendConfirmationEmailAsync(
                        user.Email,
                        user.DisplayName,
                        confirmUrl
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error sending confirmation email to user {UserId}",
                        user.Id
                    );
                }

                await SignInUser(user);

                var toast = new ToastMessageViewModel
                {
                    Title = "Willkommen",
                    Message = $"Konto für {user.DisplayName} erstellt.",
                    Type = ToastType.Success,
                };
                TempData.Put("ToastMsg", toast);

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Register");
                ModelState.AddModelError(string.Empty, "Ein unerwarteter Fehler ist aufgetreten.");
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string token)
        {
            var user = await _userRepo.GetByEmailConfirmationToken(token);

            bool valid =
                user != null
                && user.EmailConfirmationTokenExpiry.HasValue
                && user.EmailConfirmationTokenExpiry.Value > DateTime.UtcNow;

            bool authenticated = User.Identity?.IsAuthenticated == true;

            if (!valid)
            {
                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Der Bestätigungslink ist ungültig oder abgelaufen.",
                        Type = ToastType.Error,
                    }
                );
                return authenticated
                    ? RedirectToAction("Index", "Home")
                    : RedirectToAction(nameof(Login));
            }

            user!.IsEmailConfirmed = true;
            user.EmailConfirmationToken = null;
            user.EmailConfirmationTokenExpiry = null;
            await _userRepo.Update(user);

            TempData.Put(
                "ToastMsg",
                new ToastMessageViewModel
                {
                    Title = "Bestätigt",
                    Message = "E-Mail-Adresse erfolgreich bestätigt.",
                    Type = ToastType.Success,
                }
            );

            return authenticated
                ? RedirectToAction("Index", "Home")
                : RedirectToAction(nameof(Login));
        }

        private async Task SignInUser(UserModel user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.DisplayName),
            };
            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal
            );
        }
    }
}
