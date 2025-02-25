using BoligPletten.Application;
using BoligPletten.Infrastructure;
using ImmersiHome_API;
using Npgsql;
using System.Data;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Register ìnfrastructure and application services
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices();

        // Register default services
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