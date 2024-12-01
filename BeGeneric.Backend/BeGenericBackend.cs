using BeGeneric.Backend.Builder;
using BeGeneric.Backend.Services.Common;
using BeGeneric.Backend.Services.GenericBackend;
using BeGeneric.Backend.Settings;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;

namespace BeGeneric.Backend
{
    public static class BeGenericBackend
    {
        public static IBeGenericConfigurationBuilder AddControllersWithBeGeneric<T>(this IServiceCollection services,
            string connectionString,
            BeConfiguration configuration,
            Action<IAttachedActionService<T>> configureAttachedActionsAction = null,
            string databaseSchema = "dbo")
        {
            services.AddSingleton<IMemoryCacheService, MemoryCacheService>();
            services.AddSingleton(configuration.Entities);
            services.TryAddEnumerable(ServiceDescriptor.Transient<IApplicationModelProvider, ServiceControllerDynamicRouteProvider>());

            services.AddScoped<IGenericDataService<T>, GenericDataService<T>>();

            Dictionary<string, string> featureConfigutation = new()
            {
                { typeof(T).Name, "true" }
            };

            var configurationBuild = new ConfigurationBuilder()
                .AddInMemoryCollection(featureConfigutation)
                .Build();

            var result = services.AddControllers()
                .ConfigureApplicationPartManager(manager =>
                {
                    manager.FeatureProviders.Clear();
                    manager.FeatureProviders.Add(new GenericControllerFeatureProvider(configurationBuild));
                })
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.SuppressMapClientErrors = true;
                });

            AttachedActionService<T> attachedActionService = new();
            if (configureAttachedActionsAction != null)
            {
                configureAttachedActionsAction(attachedActionService);
            }

            services.AddSingleton<IAttachedActionService<T>>(attachedActionService);

            return new BeGenericConfigurationBuilder(services, result, connectionString, configuration, databaseSchema);
        }

        public static IBeGenericConfigurationBuilder AddControllersWithBeGeneric<T>(this IServiceCollection services,
            string connectionString,
            string configurationPath = "./be-generic.config.json",
            Action<IAttachedActionService<T>> configureAttachedActionsAction = null,
            string databaseSchema = "dbo")
        {
            using StreamReader metadataReader = new(configurationPath);
            BeConfiguration? configuration = JsonSerializer.Deserialize<BeConfiguration>(metadataReader.ReadToEnd(), new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });

            return configuration == null
                ? throw new ArgumentException("Configuration path is invalid or the file cannot be parsed.", nameof(configurationPath))
                : AddControllersWithBeGeneric(services, connectionString, configuration, configureAttachedActionsAction, databaseSchema);
        }
    }
}