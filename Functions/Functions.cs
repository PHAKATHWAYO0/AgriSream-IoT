// The four functions required by the brief: Blob trigger, HTTP GET, HTTP POST, Queue trigger.
using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

public record IrrigationCommand(string DeviceId, string Action);

public class AuditEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "drone";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Status { get; set; } = "";
    public DateTime ProcessedAt { get; set; }
}

// Output type that writes to the queue (output binding) AND returns the HTTP response
public class IrrigationOutput
{
    [QueueOutput("irrigation-commands", Connection = "StorageConn")] public string? QueueMessage { get; set; }
    [HttpResult] public HttpResponseData? Response { get; set; }
}

public class Functions(ILogger<Functions> log)
{
    static readonly JsonSerializerOptions Opt = new(JsonSerializerDefaults.Web);

    // 1) BLOB TRIGGER: a drone image lands in Blob Storage -> write an audit row to Table Storage (output binding)
    [Function("BlobUploadAudit")]
    [TableOutput("ProcessingAudit", Connection = "StorageConn")]
    public async Task<AuditEntity> BlobUploadAudit(
        [BlobTrigger("drone-images/{name}", Connection = "StorageConn")] BlobClient blob, string name)
    {
        var props = (await blob.GetPropertiesAsync()).Value;
        log.LogInformation("Drone image {Name} uploaded ({Size} bytes)", name, props.ContentLength);
        return new AuditEntity { RowKey = $"{DateTime.UtcNow.Ticks}-{Guid.NewGuid():N}", FileName = name, SizeBytes = props.ContentLength, Status = "Processed", ProcessedAt = DateTime.UtcNow };
    }

    // 2) HTTP GET: the MVC dashboard asks for the latest telemetry (Table input binding, newest 20 rows)
    [Function("GetTelemetry")]
    public async Task<HttpResponseData> GetTelemetry(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry")] HttpRequestData req,
        [TableInput("SensorReadings", "farm1", 20, Connection = "StorageConn")] IEnumerable<TableEntity> rows)
    {
        var data = rows.Select(r => new { deviceId = r.GetString("DeviceId"), soilMoisture = r.GetDouble("SoilMoisture"), temperature = r.GetDouble("Temperature"), time = r.GetString("Time") });
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(data);
        return res;
    }

    // 3) HTTP POST: "Trigger Irrigation" -> place an Irrigation Command on the storage queue (Queue output binding)
    [Function("TriggerIrrigation")]
    public async Task<IrrigationOutput> TriggerIrrigation(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "irrigation")] HttpRequestData req)
    {
        var cmd = JsonSerializer.Deserialize<IrrigationCommand>(await new StreamReader(req.Body).ReadToEndAsync(), Opt);
        if (cmd is null || string.IsNullOrWhiteSpace(cmd.DeviceId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest); await bad.WriteStringAsync("deviceId is required");
            return new IrrigationOutput { Response = bad };
        }
        var ok = req.CreateResponse(HttpStatusCode.Accepted); await ok.WriteStringAsync("Irrigation command queued");
        return new IrrigationOutput { QueueMessage = JsonSerializer.Serialize(cmd, Opt), Response = ok };
    }

    // 4) QUEUE TRIGGER: process the command and update the device's status in the Azure SQL master registry
    [Function("ProcessIrrigationCommand")]
    public async Task ProcessIrrigationCommand([QueueTrigger("irrigation-commands", Connection = "StorageConn")] string message)
    {
        var cmd = JsonSerializer.Deserialize<IrrigationCommand>(message, Opt) ?? throw new InvalidOperationException("Bad queue message");
        var status = cmd.Action == "START" ? "Irrigating" : "Idle";
        await using var c = new SqlConnection(Environment.GetEnvironmentVariable("SqlConn")); await c.OpenAsync();
        await using var sql = new SqlCommand(@"UPDATE Devices SET Status=@s, LastCommand=@a, UpdatedAt=SYSUTCDATETIME() WHERE DeviceId=@d;
            IF @@ROWCOUNT=0 INSERT INTO Devices(DeviceId,Name,Status,LastCommand,UpdatedAt) VALUES(@d,@d,@s,@a,SYSUTCDATETIME())", c);
        sql.Parameters.AddWithValue("@s", status); sql.Parameters.AddWithValue("@a", cmd.Action); sql.Parameters.AddWithValue("@d", cmd.DeviceId);
        await sql.ExecuteNonQueryAsync();
        log.LogInformation("Device {Id} set to {Status}", cmd.DeviceId, status);
    }
}
