﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using System.Data;

namespace BoligPletten.Infrastructure.Persistence
{
    /// <summary>
    /// Extension methods for connection pooling services
    /// </summary>
    public static class ConnectionPoolingExtensions
    {
        public static IServiceCollection AddDbConnectionPooling(
            this IServiceCollection services,
            Action<DbConnectionOptions> configureOptions)
        {
            // Configure options
            services.Configure(configureOptions);

            // Register policy
            services.AddSingleton<IPooledObjectPolicy<IDbConnection>, DbConnectionPoolPolicy>();

            // Register pool using fully qualified names to avoid confusion
            services.AddSingleton<ObjectPool<IDbConnection>>(sp =>
            {
                var policy = sp.GetRequiredService<IPooledObjectPolicy<IDbConnection>>();
                var options = sp.GetRequiredService<IOptions<DbConnectionOptions>>();
                return new DefaultObjectPool<IDbConnection>(
                    policy,
                    options.Value.MaxPoolSize);
            });

            return services;
        }
    }
}
