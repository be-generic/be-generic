using BeGeneric.Backend.Common;
using BeGeneric.Backend.Common.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace BeGeneric.Backend.Database.MsSql.Extensions;

public static class IBeGenericBackendBuilderExtensions
{
    public static IBeGenericBackendBuilder WithMsSqlDatabase(this IBeGenericBackendBuilder builder, string connectionString, string databaseSchema = "dbo")
    {
        IDatabaseStructureService databaseStructureService = new MsSqlDatabaseStructureService(connectionString, databaseSchema, builder.Configuration.Metadata);

        builder.Services.AddSingleton(databaseStructureService);

        builder.Services.AddScoped<IDbConnection>((x) =>
        {
            var connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        });

        return builder;
    }
}