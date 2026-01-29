using LicenseWatch.Web.Models.Account;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LicenseWatch.Web.Controllers;

[AllowAnonymous]
[Route("account")]
public class AccountController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    ISecurityEventStore securityEventStore,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (signInManager.IsSignedIn(User))
        {
            return RedirectToLocal(returnUrl);
        }

        var reason = Request.Query["reason"].ToString();
        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            AlertMessage = string.Equals(reason, "expired", StringComparison.OrdinalIgnoreCase)
                ? "Your session has expired. Please sign in again."
                : null
        });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            securityEventStore.Add(new SecurityEvent(
                DateTime.UtcNow,
                "Login.LockedOut",
                $"Account locked for {model.Email}.",
                HttpContext.Request.Path,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                model.Email));
            ModelState.AddModelError(string.Empty, "Your account is temporarily locked due to repeated failed attempts. Try again in 15 minutes.");
            return View(model);
        }

        securityEventStore.Add(new SecurityEvent(
            DateTime.UtcNow,
            "Login.Failed",
            $"Failed login for {model.Email}.",
            HttpContext.Request.Path,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            model.Email));

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpGet("register")]
    public IActionResult Register(string? returnUrl = null)
    {
        if (signInManager.IsSignedIn(User))
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new IdentityUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await signInManager.SignInAsync(user, isPersistent: true);
            logger.LogInformation("User registered: {Email}", model.Email);
            return RedirectToLocal(model.ReturnUrl);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
