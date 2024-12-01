using BeGeneric.Backend.Builder;
using BeGeneric.Backend.Services.BeGeneric.DatabaseStructure;
using Microsoft.Extensions.DependencyInjection;
using System.Data.SqlClient;
using System.Data;

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
            builder.Services.AddSingleton<MsSqlDatabaseProvider>();

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
