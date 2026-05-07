using System.Security.Claims;
using JioCxRcsWrapper.Application.Auth;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Web.Models.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JioCxRcsWrapper.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly ICurrentUser _currentUser;

    public AccountController(IAuthService authService, ICurrentUser currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.LoginAsync(new LoginRequest(model.Email, model.Password), cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Invalid email or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId!.Value.ToString()),
            new(ClaimTypes.Name, result.Name!),
            new(ClaimTypes.Email, model.Email),
            new(ClaimTypes.Role, result.Role!),
            new("is_developer", result.IsDeveloper.ToString())
        };

        if (result.ClientId.HasValue)
        {
            claims.Add(new Claim("client_id", result.ClientId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        Response.Cookies.Append("access_token", result.JwtToken!, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict
        });

        return RedirectToAction("Index", "Dashboard");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserByIdAsync(_currentUser.UserId, cancellationToken);
        if (user == null) return NotFound();
        return View(new ProfileViewModel { Email = user.Email, Name = user.Name });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ProfileViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Profile", model);
        }

        var result = await _authService.ChangePasswordAsync(_currentUser.UserId, model.NewPassword!, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Failed to change password.");
            return View("Profile", model);
        }

        // Force logout after password change
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("access_token");
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("access_token");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
