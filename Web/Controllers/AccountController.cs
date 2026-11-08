using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
public class AccountController(Db db) : Controller
{
    static readonly string[] Roles = { "Farmer", "Agronomist", "Admin" };
    public IActionResult Register() => View();
    [HttpPost]
    public async Task<IActionResult> Register(string username, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(username) || (password?.Length ?? 0) < 6 || !Roles.Contains(role)) { ViewBag.Error = "Enter a username, a password of 6+ characters and a valid role."; return View(); }
        try { if (!await db.CreateUserAsync(username.Trim(), password!, role)) { ViewBag.Error = "That username already exists."; return View(); } }
        catch (Exception ex) { ViewBag.Error = "Database error: " + ex.Message; return View(); }
        TempData["Msg"] = "Account created. Please log in."; return RedirectToAction(nameof(Login));
    }
    public IActionResult Login() => View();
    [HttpPost]
    public async Task<IActionResult> Login(string username, string password)
    {
        AppUser? u;
        try { u = await db.LoginAsync(username ?? "", password ?? ""); } catch (Exception ex) { ViewBag.Error = "Database error: " + ex.Message; return View(); }
        if (u is null) { ViewBag.Error = "Invalid username or password."; return View(); }
        var id = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, u.Username), new Claim(ClaimTypes.Role, u.Role) }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(id));
        return RedirectToAction("Index", "Dashboard");
    }
    public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(); return Redirect("/"); }
}
