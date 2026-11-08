using Azure.Data.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
[Authorize]
public class DashboardController(IHttpClientFactory f, Db db, IConfiguration cfg) : Controller
{
    public async Task<IActionResult> Index()
    {
        var readings = new List<Reading>();
        // Calls the HTTP GET Azure Function, which reads the latest telemetry from Table Storage
        try { readings = await f.CreateClient("fn").GetFromJsonAsync<List<Reading>>("api/telemetry") ?? new(); }
        catch (Exception ex) { ViewBag.Error = "Telemetry service unavailable: " + ex.Message; }
        try { ViewBag.Devices = await db.DevicesAsync(); } catch { ViewBag.Devices = new List<Device>(); }
        return View(readings);
    }
    [HttpPost]
    public async Task<IActionResult> Simulate(int count = 20)
    {   // Simulated soil sensors write high-frequency readings into Azure Table Storage
        var t = new TableClient(cfg["StorageConn"], "SensorReadings"); await t.CreateIfNotExistsAsync();
        var rnd = new Random();
        for (int i = 0; i < Math.Clamp(count, 1, 100); i++)
            await t.AddEntityAsync(new TableEntity("farm1", $"{DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks:D19}-{i:D3}") {
                { "DeviceId", "SENSOR-" + rnd.Next(1, 6) }, { "SoilMoisture", Math.Round(rnd.NextDouble() * 60 + 10, 1) },
                { "Temperature", Math.Round(rnd.NextDouble() * 20 + 10, 1) }, { "Time", DateTime.UtcNow.ToString("u") } });
        TempData["Msg"] = $"{Math.Clamp(count, 1, 100)} sensor readings stored."; return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Irrigate(string deviceId)
    {   // Calls the HTTP POST Azure Function, which places an Irrigation Command on the storage queue
        var r = await f.CreateClient("fn").PostAsJsonAsync("api/irrigation", new { deviceId, action = "START" });
        TempData["Msg"] = r.IsSuccessStatusCode ? $"Irrigation command queued for {deviceId}." : "Could not queue the command.";
        return RedirectToAction(nameof(Index));
    }
}
