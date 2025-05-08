using BeGeneric.Backend.Common;
using BeGeneric.Backend.Common.Builder;
using BeGeneric.Backend.Common.Models;
using BeGeneric.Backend.Services;
using BeGeneric.Backend.Services.Common;
using BeGeneric.Backend.Services.GenericBackend;
using BeGeneric.Backend.Settings;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;

namespace BeGeneric.Backend;

public static class BeGenericBackend
{
    public static IBeGenericBackendBuilder AddBeGeneric<T>(this IServiceCollection services,
        BeConfiguration configuration,
        Action<IAttachedActionService<T>> configureAttachedActionsAction = null)
    {
        services.AddSingleton<IMemoryCacheService, MemoryCacheService>();
        services.AddSingleton(configuration.Entities);
        services.AddScoped<IGenericDataService<T>, GenericDataService<T>>();

        AttachedActionService<T> attachedActionService = new();
        configureAttachedActionsAction?.Invoke(attachedActionService);

        services.AddSingleton<IAttachedActionService<T>>(attachedActionService);

        return new BeGenericBackendBuilder(services, configuration, typeof(T));
    }

    public static IBeGenericBackendBuilder AddBeGeneric<T>(this IServiceCollection services,
        string configurationPath = "./be-generic.config.json",
        Action<IAttachedActionService<T>> configureAttachedActionsAction = null)
    {
        using StreamReader metadataReader = new(configurationPath);
        BeConfiguration configuration = JsonSerializer.Deserialize<BeConfiguration>(metadataReader.ReadToEnd(), new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });

        return AddBeGeneric(services, configuration, configureAttachedActionsAction);
    }

    public static IMvcBuilder WithControllers(this IBeGenericBackendBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IApplicationModelProvider, ServiceControllerDynamicRouteProvider>());

        Dictionary<string, string> featureConfigutation = new()
        {
            { builder.PrimaryKeyType.Name, "true" }
        };

        var configurationBuild = new ConfigurationBuilder()
            .AddInMemoryCollection(featureConfigutation)
            .Build();

        var result = builder.Services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                manager.FeatureProviders.Clear();
                manager.FeatureProviders.Add(new GenericControllerFeatureProvider(configurationBuild));
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressMapClientErrors = true;
            });

        return result;
    }
}