using ImmersiHome_API.Models.Domain;
using ImmersiHome_API.Models.Entities;
using ImmersiHome_API.Persistence.Mappers;
using ImmersiHome_API.Persistence.Repositories;
using ImmersiHome_API.Persistence.Repositories.Common;
using ImmersiHome_API.Services;
using Npgsql;
using System.Data;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var configuration = builder.Configuration;

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var databaseProvider = configuration.GetValue<string>("DatabaseProvider");

        // Register memory cache
        builder.Services.AddMemoryCache();

        // Register database connection
        builder.Services.AddScoped<IDbConnection>(_ =>
        {
            var conn = new NpgsqlConnection(connectionString);
            conn.Open();
            return conn;
        });

        // Register services
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IGenericMapper<HouseModel, HouseEntity, int>, DefaultGenericMapper<HouseModel, HouseEntity, int>>();
        builder.Services.AddScoped<IHouseService, HouseService>();
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