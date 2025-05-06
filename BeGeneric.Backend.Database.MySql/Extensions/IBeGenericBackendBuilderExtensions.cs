using BeGeneric.Backend.Common;
using BeGeneric.Backend.Common.Builder;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using System.Data;

namespace BeGeneric.Backend.Database.MySql.Extensions;

public static class IBeGenericBackendBuilderExtensions
{
    public static IBeGenericBackendBuilder WithMySqlDatabase(this IBeGenericBackendBuilder builder, string connectionString, string databaseSchema = "dbo")
    {
        IDatabaseStructureService databaseStructureService = new MySqlDatabaseStructureService(connectionString, databaseSchema, builder.Configuration.Metadata);

        builder.Services.AddSingleton(databaseStructureService);

        builder.Services.AddScoped<IDbConnection>((x) =>
        {
            var connection = new MySqlConnection(connectionString);
            connection.Open();
            return connection;
        });

        builder.Services.AddSingleton<ISqlDialect>(new MySqlDialect());

        return builder;
    }
}