using BeGeneric.Backend.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace BeGeneric.Backend.Builder
{
    public class BeGenericConfigurationBuilder: IBeGenericConfigurationBuilder
    {
        public BeGenericConfigurationBuilder(IServiceCollection services,
            IMvcBuilder mvcBuilder, 
            string connectionString,
            BeConfiguration configuration,
            string databaseSchema = "dbo")
        { 
            Services = services;
            MvcBuilder = mvcBuilder;
            ConnectionString = connectionString;
            Configuration = configuration;
            DatabaseSchema = databaseSchema;
        }

        public IServiceCollection Services { get; }
        public IMvcBuilder MvcBuilder { get; }
        public string ConnectionString { get; }
        public BeConfiguration Configuration { get; }
        public string DatabaseSchema { get; }
    }
}
