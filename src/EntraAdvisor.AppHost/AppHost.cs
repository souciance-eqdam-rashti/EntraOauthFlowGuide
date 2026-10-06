var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.EntraAdvisor_Web>("advisor-web")
    .WithHttpHealthCheck("/");

builder.Build().Run();
