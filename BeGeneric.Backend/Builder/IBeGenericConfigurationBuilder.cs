using BeGeneric.Backend.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace BeGeneric.Backend.Builder
{
    public interface IBeGenericConfigurationBuilder
    {
        public IServiceCollection Services { get; }
        public IMvcBuilder MvcBuilder { get; }
        public string ConnectionString { get; }
        public BeConfiguration Configuration { get; }
        public string DatabaseSchema { get; }
    }
}
