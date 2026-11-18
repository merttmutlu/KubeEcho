var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var healthLock = new object();
var ready = true;
var live = true;

app.MapGet("/", () => new
{
    service = app.Configuration["SERVICE_TYPE"] ?? app.Environment.ApplicationName,
    hostname = Environment.MachineName
});

app.MapGet("/health/ready", () =>
{
    lock (healthLock)
    {
        return Results.Text(ready ? "ready" : "not ready", statusCode: ready ? 200 : 503);
    }
});

app.MapGet("/health/live", () =>
{
    lock (healthLock)
    {
        return Results.Text(live ? "live" : "not live", statusCode: live ? 200 : 503);
    }
});

app.MapPatch("/health", (Dictionary<string, bool> update) =>
{
    if (update.Count == 0 || update.Keys.Any(key => key is not ("ready" or "live")))
    {
        return Results.BadRequest("Supply ready, live, or both as JSON booleans.");
    }

    lock (healthLock)
    {
        ready = update.GetValueOrDefault("ready", ready);
        live = update.GetValueOrDefault("live", live);
        return Results.Ok(new { ready, live });
    }
});

app.Run();
