using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
[Authorize]
public class MediaController(IConfiguration cfg) : Controller
{
    BlobContainerClient Box() => new(cfg["StorageConn"], "drone-images");
    public async Task<IActionResult> Index()
    {
        var items = new List<(string Name, long? Size)>();
        try { var c = Box(); await c.CreateIfNotExistsAsync(); await foreach (var b in c.GetBlobsAsync()) items.Add((b.Name, b.Properties.ContentLength)); }
        catch (Exception ex) { ViewBag.Error = "Storage error: " + ex.Message; }
        return View(items);
    }
    [HttpPost, RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(IFormFile file)
    {   // Drone image goes to Blob Storage; the Blob-triggered Function then logs an audit row
        if (file is null || file.Length == 0 || !file.ContentType.StartsWith("image/")) { TempData["Msg"] = "Choose an image file."; return RedirectToAction(nameof(Index)); }
        var c = Box(); await c.CreateIfNotExistsAsync();
        await using var s = file.OpenReadStream();
        await c.UploadBlobAsync($"{DateTime.UtcNow:yyyyMMddHHmmss}-{Path.GetFileName(file.FileName)}", s);
        TempData["Msg"] = "Image uploaded."; return RedirectToAction(nameof(Index));
    }
}
