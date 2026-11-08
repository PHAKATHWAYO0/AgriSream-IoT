// AgriStream IoT - MVC management web app (Azure App Service).
using Microsoft.AspNetCore.Authentication.Cookies;
var b = WebApplication.CreateBuilder(args);
b.Services.AddControllersWithViews();
b.Services.AddApplicationInsightsTelemetry();
b.Services.AddSingleton<Db>();
// Typed client for the Azure Functions API (key comes from App Service settings, never from code)
b.Services.AddHttpClient("fn", c => {
    c.BaseAddress = new Uri((b.Configuration["FunctionBaseUrl"] ?? "http://localhost:7071").TrimEnd('/') + "/");
    c.DefaultRequestHeaders.Add("x-functions-key", b.Configuration["FunctionKey"] ?? "");
});
b.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => { o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/Login"; });
var app = b.Build();
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/health", () => new { status = "ok", instance = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName, time = DateTime.UtcNow });
app.MapDefaultControllerRoute();
app.Run();

public record Reading(string deviceId, double soilMoisture, double temperature, string time);
public record Device(string DeviceId, string Name, string Status, string? LastCommand);
public record AppUser(string Username, string Role);
