var builder = DistributedApplication.CreateBuilder(args);

var acr = builder.AddAzureContainerRegistry("registry");

builder.AddAzureContainerAppEnvironment("aca-env")
    .WithAzureContainerRegistry(acr);

var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
    .RunAsContainer(c => c.WithDataVolume())
    .AddDatabase("vehicletracker");

var api = builder.AddProject<Projects.VehicleTracker>("api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.Template.Scale.MinReplicas = 1;
        app.Template.Scale.MaxReplicas = 3;
    });

var apiMigrations = api.AddEFMigrations("api-migrations")
    .WithMigrationsProject<Projects.VehicleTracker_Data>()
    .WithReference(postgres)
    .WaitFor(postgres)
    .RunDatabaseUpdateOnStart();

api.WaitForCompletion(apiMigrations);

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
