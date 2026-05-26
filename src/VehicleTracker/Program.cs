using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

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

app.UseHttpsRedirection();

app.MapControllers();

if (!app.Environment.IsDevelopment())
{
    // SPA fallback: return index.html for any non-API route so Angular router can handle it.
    app.MapFallbackToFile("index.html");
}

app.Run();
