using BeGeneric.Backend.Services.Common;
using BeGeneric.Backend.Services.GenericBackend;
using BeGeneric.Backend.Services.GenericBackend.DatabaseStructure;
using BeGeneric.Backend.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Data;
using System.Text.Json;

namespace BeGeneric.Backend;

public static class BeGenericBackend
{
    private static readonly string getEntitiesCommand = @$"SELECT 
	EntityId as EntityKey, 
	TableName,
	ObjectName,
	ControllerName,
	SoftDeleteColumn,
	(SELECT 
		p.PropertyName,
		p.ModelPropertyName,
		p.IsKey,
		p.IsReadOnly,
		p.RelatedModelPropertyName,
		p.ReferencingEntityId as ReferencingEntityKey
	FROM [SCHEMA].Properties p 
	WHERE p.EntityId = e.EntityId
	FOR JSON AUTO, INCLUDE_NULL_VALUES) AS Properties,
	ISNULL((SELECT 
		er.GetOne,
		er.GetAll,
		er.Post,
		er.Put,
		er.[Delete],
		er.ViewFilter,
		er.EditFilter,
		(SELECT 
			r.Id,
			r.RoleName as [Name]
		FROM Roles r
		FOR JSON AUTO, INCLUDE_NULL_VALUES) Role
	FROM [SCHEMA].EntityRole er
	WHERE er.EntitiesEntityId = e.EntityId
	FOR JSON AUTO, INCLUDE_NULL_VALUES
	),'[]') EntityRoles,
	ISNULL((SELECT
			er.ActiveColumnName,
			er.CrossTableName,
			er.Entity1PropertyName as EntityPropertyName,
			er.Entity1ReferencingColumnName as EntityReferencingColumnName,
			er.Entity2PropertyName as RelatedEntityPropertyName,
			er.Entity2ReferencingColumnName as RelatedEntityReferencingColumnName,
			er.EntityRelationId,
			er.ValidFromColumnName,
			er.ValidToColumnName,
			er.Entity2Id as RelatedEntityKey,
			er.ShowInEntity1 as ShowInEntity,
			er.ShowInEntity2 as ShowInRelatedEntity
		FROM [SCHEMA].EntityRelation er
		WHERE er.Entity1Id = e.EntityId
		FOR JSON AUTO, INCLUDE_NULL_VALUES), '[]') EntityRelations
FROM [SCHEMA].Entities e
FOR JSON AUTO, INCLUDE_NULL_VALUES";

		private static readonly string getMetadataCommand = @$"SELECT 
	*
FROM [SCHEMA].ColumnMetadata
FOR JSON AUTO, INCLUDE_NULL_VALUES";

    public static IMvcBuilder AddControllersWithBeGeneric<T>(this IServiceCollection services,
        string connectionString,
        BeConfiguration configuration,
        Action<IAttachedActionService<T>> configureAttachedActionsAction = null,
        string databaseSchema = "dbo")
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.DisableImplicitFromServicesParameters = true;
        });

        IDatabaseStructureService databaseStructureService = new MsSqlDatabaseStructureService(connectionString, databaseSchema, configuration.Metadata)
        {
            DataSchema = databaseSchema
        };

        services.AddSingleton(databaseStructureService);
        services.AddSingleton<IMemoryCacheService, MemoryCacheService>();
        services.AddSingleton(configuration.Entities);
        services.TryAddEnumerable(ServiceDescriptor.Transient<IApplicationModelProvider, ServiceControllerDynamicRouteProvider>());

        services.AddScoped<IDbConnection>((x) =>
        {
            var connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        });

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

        return result;
    }

    public static IMvcBuilder AddControllersWithBeGenericSql<T>(this IServiceCollection services,
        string connectionString,
        string configDatabaseConnectionString,
        Action<IAttachedActionService<T>> configureAttachedActionsAction = null,
        string databaseSchema = "dbo",
        string configSchema = "gba")
    {
        return AddControllersWithBeGeneric(services, 
            connectionString, 
            new BeConfiguration() {
                Entities = GetEntityDefinitions(configDatabaseConnectionString, configSchema),
                Metadata = GetMetadataDefinitions(configDatabaseConnectionString, configSchema),
            },
            configureAttachedActionsAction,
            databaseSchema);
    }

    public static IMvcBuilder AddControllersWithBeGeneric<T>(this IServiceCollection services,
        string connectionString,
        string configurationPath = "./be-generic.config.json",
        Action<IAttachedActionService<T>> configureAttachedActionsAction = null,
        string databaseSchema = "dbo")
    {
        using StreamReader metadataReader = new(configurationPath);
        BeConfiguration configuration = JsonSerializer.Deserialize<BeConfiguration>(metadataReader.ReadToEnd(), new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });

        return AddControllersWithBeGeneric(services, connectionString, configuration, configureAttachedActionsAction, databaseSchema);
    }

    private static List<EntityDefinition> GetEntityDefinitions(string connectionString, string schema)
    {
        using SqlConnection conn = new(connectionString);
        using SqlCommand sqlCommand = conn.CreateCommand();
        sqlCommand.CommandType = CommandType.Text;
        sqlCommand.CommandText = getEntitiesCommand.Replace("[SCHEMA]", schema);
        conn.Open();
        using var reader = sqlCommand.ExecuteReader();

        if (reader.Read())
        {
            if (reader[0] == null)
            {
                return new List<EntityDefinition>();
            }

            return JsonSerializer.Deserialize<List<EntityDefinition>>(reader[0].ToString());
        }
        else
        {
            throw new ArgumentException("Failed to get entities from provided connection string", nameof(connectionString));
        }
    }

    private static List<ColumnMetadataDefinition> GetMetadataDefinitions(string connectionString, string schema)
    {
        using SqlConnection conn = new(connectionString);
        using SqlCommand sqlCommand = conn.CreateCommand();
        sqlCommand.CommandType = CommandType.Text;
        sqlCommand.CommandText = getMetadataCommand.Replace("[SCHEMA]", schema);
        conn.Open();
        using var reader = sqlCommand.ExecuteReader();

        if (reader.Read() && reader[0] != null)
        {
            return JsonSerializer.Deserialize<List<ColumnMetadataDefinition>>(reader[0].ToString());
        }
        else
        {
            return new List<ColumnMetadataDefinition>();
        }
    }
}