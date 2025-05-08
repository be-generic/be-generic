using BeGeneric.Backend.Common.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BeGeneric.Backend.Common.Builder;

public class BeGenericBackendBuilder: IBeGenericBackendBuilder
{
    public BeGenericBackendBuilder(IServiceCollection services, 
        BeConfiguration configuration,
        Type primaryKeyType)
    {
        Services = services;
        Configuration = configuration;
        PrimaryKeyType = primaryKeyType;
    }

    public IServiceCollection Services { get; }
    public BeConfiguration Configuration { get; }
    public Type PrimaryKeyType { get; }
}