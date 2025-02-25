using BoligPletten.Application.Repositories;
using BoligPletten.Application.Repositories.Common;
using BoligPletten.Application.Services;
using BoligPletten.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace BoligPletten.Application
{
    /// <summary>
    /// Initializes services for dependency injection
    /// </summary>
    public static class ServiceInitializer
    {
        /// <summary>
        /// Adds high-performance services
        /// </summary>
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services)
        {
            // Register services
            services.AddScoped<IHouseService, HouseService>();

            return services;
        }
    }
}