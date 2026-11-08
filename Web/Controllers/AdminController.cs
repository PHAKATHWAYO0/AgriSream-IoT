using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
[Authorize(Roles = "Admin")]
public class AdminController(Db db) : Controller
{
    public async Task<IActionResult> Index() { ViewBag.Devices = await db.DevicesAsync(); return View(await db.UsersAsync()); }
}
