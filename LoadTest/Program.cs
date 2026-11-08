// Usage: dotnet run -- https://YOURAPP.azurewebsites.net/health 20000 100   (url, total requests, concurrent users)
var url = args[0]; int total = int.Parse(args[1]), conc = int.Parse(args[2]), ok = 0, fail = 0; long ms = 0;
using var http = new HttpClient(); var sw = System.Diagnostics.Stopwatch.StartNew();
await Parallel.ForEachAsync(Enumerable.Range(0, total), new ParallelOptions { MaxDegreeOfParallelism = conc }, async (_, ct) =>
{
    var t = System.Diagnostics.Stopwatch.StartNew();
    try { var r = await http.GetAsync(url, ct); if (r.IsSuccessStatusCode) Interlocked.Increment(ref ok); else Interlocked.Increment(ref fail); }
    catch { Interlocked.Increment(ref fail); }
    Interlocked.Add(ref ms, t.ElapsedMilliseconds);
});
Console.WriteLine($"OK={ok} Failed={fail} AvgResponse={ms / Math.Max(1, ok + fail)}ms Total={sw.Elapsed.TotalSeconds:F1}s");
