using BoligPletten.Application.Repositories.Common;
using BoligPletten.Application.Repositories;
using BoligPletten.Domain.Models;
using BoligPletten.Infrastructure.Mappers;
using BoligPletten.Infrastructure.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using BoligPletten.Infrastructure.Persistence;
using BoligPletten.Infrastructure.Repositories.Common;
using BoligPletten.Infrastructure.Repositories;
using Microsoft.Extensions.ObjectPool;

namespace BoligPletten.Infrastructure
{
    /// <summary>
    /// Initializes services for dependency injection
    /// </summary>
    public static class ServiceInitializer
    {
        /// <summary>
        /// Adds high-performance services
        /// </summary>
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add connection pooling with configuration
            services.AddDbConnectionPooling(options =>
            {
                options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? "GG";
                options.MaxPoolSize = configuration.GetValue<int>("Database:MaxPoolSize", 15000);
                options.MinPoolSize = configuration.GetValue<int>("Database:MinPoolSize", 10);
                options.CommandTimeout = TimeSpan.FromSeconds(configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30));
            });

            // Register high-performance mapper
            services.AddSingleton<IGenericMapper<HouseModel, HouseEntity, int>, HighPerformanceMapper<HouseModel, HouseEntity, int>>();

            // Register repository factories
            services.AddSingleton<Func<IDbConnection, IDbTransaction, IHouseRepository>>(sp =>
                (conn, trans) =>
                {
                    var mapper = sp.GetRequiredService<IGenericMapper<HouseModel, HouseEntity, int>>();
                    return new HouseRepository(conn, trans, mapper);
                });

            // Register Unit of Work
            services.AddScoped<IUnitOfWork>(sp =>
            {
                var connectionPool = sp.GetRequiredService<ObjectPool<IDbConnection>>();
                var houseRepoFactory = sp.GetRequiredService<Func<IDbConnection, IDbTransaction, IHouseRepository>>();

                return new PooledUnitOfWork(
                    connectionPool,
                    houseRepoFactory);
            });

            return services;
        }
    }
}
