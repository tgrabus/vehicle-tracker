var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("vehicletracker");

var api = builder.AddProject<Projects.VehicleTracker>("api")
    .WithReference(postgres)
    .WaitFor(postgres);

var frontend = builder
    .AddViteApp("frontend", "../VehicleTracker.Frontend", "start")
    .WithHttpsEndpoint(port: 4200, env: "DEV_SERVER_PORT")
    .WithReference(api)
    .WaitFor(api);

// PublishWithContainerFiles copies the Angular build output into the ASP.NET Core
// container's wwwroot/ directory at publish time.
// In run (dev) mode this has no effect — Angular dev server handles the frontend.
api.PublishWithContainerFiles(frontend, "./wwwroot");

builder.Build().Run();
