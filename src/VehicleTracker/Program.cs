using Microsoft.AspNetCore.HttpOverrides;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<VehicleTracker.Data.ApplicationDbContext>("vehicletracker");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

// TODO: add PersistKeysToAzureBlobStorage here once ASP.NET Core Identity is implemented

var app = builder.Build();

app.UseForwardedHeaders();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "VehicleTracker API v1");
    });
}
else
{
    // In production the compiled Angular SPA is served from wwwroot/.
    // wwwroot/ is populated by PublishWithContainerFiles in AppHost at publish time.
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapHealthChecks("/api/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});
app.MapHealthChecks("/api/health/ready");

app.MapControllers();

if (!app.Environment.IsDevelopment())
{
    // SPA fallback: return index.html for any non-API route so Angular router can handle it.
    app.MapFallbackToFile("index.html");
}

app.Run();
