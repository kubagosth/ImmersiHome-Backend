using ImmersiHome_API;
using ImmersiHome_API.Models.Domain;
using ImmersiHome_API.Models.Entities;
using ImmersiHome_API.Services;
using Npgsql;
using System.Data;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Register services
        builder.Services.AddHighPerformanceServices(builder.Configuration);
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Environment configurations
        if (builder.Environment.IsProduction())
        {
            var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            if (!string.IsNullOrEmpty(urls))
            {
                builder.WebHost.UseUrls(urls);
            }
        }
        else if (builder.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        // TODO - Remove this
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        });

        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}