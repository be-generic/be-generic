using BeGeneric.Backend.Services.BeGeneric;
using BeGeneric.Backend.Services.BeGeneric.DatabaseStructure;
using BeGeneric.Backend.Services.Common;
using BeGeneric.Backend.Settings;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Data.SqlClient;

namespace BeGeneric.Backend
{
    public static class BeGenericBackend
    {
        public static void AddGenericBackendServices(this IServiceCollection services,
            string connectionString,
            List<EntityDefinition> entityDefinitions,
            List<ColumnMetadataDefinition> metadataDefinitions,
            Action<IAttachedActionService> configureAttachedActionsAction)
        {
            services.AddSingleton<IDatabaseStructureService>(new MsSqlDatabaseStructureService(connectionString, metadataDefinitions));
            services.AddSingleton<IMemoryCacheService, MemoryCacheService>();
            services.AddSingleton(entityDefinitions);

            services.AddScoped<IDbConnection>((x) =>
            {
                var connection = new SqlConnection(connectionString);
                connection.Open();
                return connection;
            });

            services.AddScoped<IGenericDataService, GenericDataService>();

            AttachedActionService attachedActionService = new();
            configureAttachedActionsAction(attachedActionService);
            services.AddSingleton<IAttachedActionService>(attachedActionService);
        }
    }
}