using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetworkHealthCheck;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.Configure<Settings>(builder.Configuration).AddHostedService<HealthCheck>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.MapGet("/health", () => {
	bool healthy = HealthCheck.PublicStatus == HealthStatus.Healthy;

	if (healthy) {
		return Results.Ok("Healthy");
	}

	return Results.Problem("Unhealthy");
});

app.Run();