using ImmersiHome_API.Infrastructure.Mappers;
using ImmersiHome_API.Infrastructure.Persistence;
using ImmersiHome_API.Infrastructure.Persistence.Repositories;
using ImmersiHome_API.Infrastructure.Persistence.Repositories.Common;
using ImmersiHome_API.Models.Domain;
using ImmersiHome_API.Models.Entities;
using ImmersiHome_API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using Npgsql;
using System.Data;

namespace ImmersiHome_API
{
    /// <summary>
    /// Initializes services for dependency injection
    /// </summary>
    public static class ServiceInitializer
    {
        /// <summary>
        /// Adds high-performance services
        /// </summary>
        public static IServiceCollection AddHighPerformanceServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add connection pooling with configuration
            services.AddDbConnectionPooling(options =>
            {
                options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? "GG";
                options.MaxPoolSize = configuration.GetValue<int>("Database:MaxPoolSize", 100);
                options.MinPoolSize = configuration.GetValue<int>("Database:MinPoolSize", 10);
                options.CommandTimeout = TimeSpan.FromSeconds(configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30));
            });

            // Register high-performance mapper
            services.AddSingleton<IGenericMapper<HouseModel, HouseEntity, int>, HighPerformanceMapper<HouseModel, HouseEntity, int>>();

            // Register repository factories
            services.AddSingleton<Func<IDbConnection, IDbTransaction, IHouseRepository>>(sp =>
                (conn, trans) => {
                    var mapper = sp.GetRequiredService<IGenericMapper<HouseModel, HouseEntity, int>>();
                    return new HouseRepository(conn, trans, mapper);
                });

            // Register Unit of Work
            services.AddScoped<IUnitOfWork>(sp =>
            {
                var connectionPool = sp.GetRequiredService<Microsoft.Extensions.ObjectPool.ObjectPool<IDbConnection>>();
                var houseRepoFactory = sp.GetRequiredService<Func<IDbConnection, IDbTransaction, IHouseRepository>>();

                return new PooledUnitOfWork(
                    connectionPool,
                    houseRepoFactory);
            });

            // Register services
            services.AddScoped<IHouseService, HouseService>();

            return services;
        }
    }
}