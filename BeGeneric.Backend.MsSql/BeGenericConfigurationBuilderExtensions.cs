using BeGeneric.Backend.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Data.SqlClient;
using System.Data;
using BeGeneric.Backend.Services.GenericBackend.DatabaseStructure;

namespace BeGeneric.Backend.MsSql
{
    public static class BeGenericConfigurationBuilderExtensions
    {
        public static IMvcBuilder UseSqlServer(this IBeGenericConfigurationBuilder builder)
        {
            IDatabaseStructureService databaseStructureService = new MsSqlDatabaseStructureService(
                builder.ConnectionString,
                builder.DatabaseSchema,
                builder.Configuration.Metadata)
            {
                DataSchema = builder.DatabaseSchema
            };

            builder.Services.AddSingleton(databaseStructureService);
            builder.Services.AddSingleton<IBeGenericDatabaseProvider, MsSqlDatabaseProvider>();

            builder.Services.AddScoped<IDbConnection>((x) =>
            {
                var connection = new SqlConnection(builder.ConnectionString);
                connection.Open();
                return connection;
            });

            return builder.MvcBuilder;
        }
    }
}
