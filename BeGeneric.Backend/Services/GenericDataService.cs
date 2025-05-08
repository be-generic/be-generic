using BeGeneric.Backend.Common;
using BeGeneric.Backend.Common.Exceptions;
using BeGeneric.Backend.Common.Helpers;
using BeGeneric.Backend.Common.Models;
using BeGeneric.Backend.Database;
using BeGeneric.Backend.Services.GenericBackend;
using BeGeneric.Backend.Services.GenericBackend.Helpers;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BeGeneric.Backend.Services;

public class GenericDataService<T> : IGenericDataService<T>
{
    protected readonly List<Entity> entities;
    protected readonly ILogger logger;
    protected readonly IDatabaseStructureService dbStructure;
    private readonly IAttachedActionService<T> attachedActionService;
    private readonly IDbConnection connection;
    private readonly ISqlDialect sqlDialect;

    internal readonly string dbSchema = "dbo";

    private readonly Dictionary<Type, Func<string, object>> stringParsers = new()
    {
        { typeof(int), (x) => int.Parse(x) },
        { typeof(Guid), (x) => new Guid(x) },
        { typeof(string), (x) => x },
    };

    public GenericDataService(List<EntityDefinition> entityDefinitions,
        IDatabaseStructureService dbStructure,
        IAttachedActionService<T> attachedActionService,
        IDbConnection connection,
        ISqlDialect sqlDialect,
        ILogger logger = null)
    {
        this.logger = logger;
        this.dbStructure = dbStructure;
        this.connection = connection;
        this.attachedActionService = attachedActionService;
        this.sqlDialect = sqlDialect;

        entities = entityDefinitions.ProcessEntities();
        dbSchema = dbStructure.DataSchema;
    }

    public async Task<string> Get(ClaimsPrincipal user, string controllerName, T id)
    {
        (Entity entity, string permissionsFilter, string _) = Authorize(user, controllerName, getOne: true);

        string userName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null;
        string roleName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null;

        var action = attachedActionService.GetAttachedAction(controllerName, ActionType.Get, ActionOrderType.Before);
        if (action != null)
        {
            await action(new ActionData<T>()
            {
                Id = id,
                UserName = userName,
                Role = roleName
            });
        }

        int tabCounter = 0;
        List<DbParameter> dbParameters = new();
        List<Guid> entityIds = new();

        string query = GenerateSelectQuery(entity, entityIds, ref tabCounter, roleName, userName, dbParameters, true, "", id);

        List<Tuple<string, object>> permissionsFilterParams = new();

        ComparerObject filterObjectWithPermissions = null;

        if (!string.IsNullOrEmpty(permissionsFilter))
        {
            filterObjectWithPermissions = JsonSerializer.Deserialize<ComparerObject>(permissionsFilter, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
        }

        var filters = filterObjectWithPermissions?.ToSQLQuery(sqlDialect, user, entity, dbSchema, dbParameters.Count, "tab1", null);

        if (filters != null && filters.Item1 != null && filters.Item1.Replace("(", "").Replace(")", "").Length > 0)
        {
            query += $" AND {filters.Item1}";
        }

        query = sqlDialect.WrapIntoJson(query, true, true, true);

        DbConnection connection = this.connection as DbConnection;

        using DbCommand command = dbStructure.GetDbCommand(query, connection);
        command.Parameters.Add(dbStructure.GetDbParameter("tab1_val", id));

        command.Parameters.AddRange(dbParameters.ToArray());

        if (filters != null)
        {
            command.Parameters.AddRange(filters.Item3.Select(x => dbStructure.GetDbParameter(x.Item1, x.Item2)).ToArray());
        }

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using DbDataReader reader = await command.ExecuteReaderAsync();
        StringBuilder response = new();
        bool success = false;

        while (reader.Read())
        {
            response.Append(GetEntryFromReader(reader));
            success = true;
        }

        if (success)
        {
            string finalResponse = response.ToString();
            var afterAction = attachedActionService.GetAttachedAction(controllerName, ActionType.Get, ActionOrderType.After);
            if (afterAction != null)
            {
                await afterAction(new ActionData<T>()
                {
                    Id = id,
                    GetOneResultData = finalResponse,
                    UserName = userName,
                    Role = roleName
                });
            }

            return finalResponse;
        }
        else
        {
            throw new GenericBackendSecurityException(SecurityStatus.NotFound);
        }
    }

    public async Task<string> Get(ClaimsPrincipal user, string controllerName, int? page = null, int pageSize = 10, string? sortProperty = null, string? sortOrder = "ASC", IComparerObject? filterObject = null, SummaryRequestObject[] summaries = null, string[] properties = null)
    {
        (Entity entity, string permissionsFilter, string _) = Authorize(user, controllerName, getAll: true);

        string? userName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null;
        string? roleName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null;

        var action = attachedActionService.GetAttachedAction(controllerName, ActionType.GetAll, ActionOrderType.Before);
        if (action != null)
        {
            await action(new ActionData<T>()
            {
                Page = page,
                FilterObject = filterObject,
                PageSize = pageSize,
                SortOrder = sortOrder,
                SortProperty = sortProperty,
                UserName = userName,
                Role = roleName
            });
        }

        List<string> entities = new();
        int tabCounter = 0;
        string totalCount = "0";

        List<Tuple<string, object>> permissionsFilterParams = new();

        ComparerObject filterObjectWithPermissions = filterObject as ComparerObject;
        ComparerObject permissionFilterObject = null;

        if (!string.IsNullOrEmpty(permissionsFilter))
        {
            permissionFilterObject = JsonSerializer.Deserialize<ComparerObject>(permissionsFilter, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

            if (filterObjectWithPermissions != null)
            {
                filterObjectWithPermissions = new ComparerObject()
                {
                    Comparisons = new[]
                    {
                        filterObjectWithPermissions,
                        permissionFilterObject
                    },
                    Conjunction = "and"
                };
            }
            else
            {
                filterObjectWithPermissions = permissionFilterObject;
            }
        }

        List<DbParameter> parameters = new();

        string query = string.Empty;
        if (!string.IsNullOrEmpty(sortProperty) && sortProperty.Contains('.'))
        {
            query = "SELECT * FROM (";
        }

        List<Guid> entityIds = new();
        query += GenerateSelectQuery(entity, entityIds, ref tabCounter, roleName, userName, parameters, true, "", null, properties);

        var joinData = new Dictionary<string, SelectPropertyData>();
        var filters = filterObjectWithPermissions?.ToSQLQuery(sqlDialect, user, entity, dbSchema, parameters.Count, "tab1", joinData);

        if (filters != null && filters.Item1 != null && filters.Item1.Replace("(", "").Replace(")", "").Length > 0)
        {
            query += $" AND {filters.Item1}";
        }

        if (!string.IsNullOrEmpty(sortProperty) && sortProperty.Contains('.'))
        {
            query += ") t1";
        }

        query = AddOrderByToQuery(query, entity, sortProperty, sortOrder);

        if (page != null)
        {
            query = sqlDialect.AddPagingToQuery(query, page.Value - 1, pageSize);
        }

        query = sqlDialect.WrapIntoJson(query, true);

        DbConnection connection = this.connection as DbConnection;

        totalCount = (await AggregateEntityAccess(user, entity, null, filterObject: permissionFilterObject))[0];

        string filteredTotalCount = totalCount;

        if (filters != null)
        {
            filteredTotalCount = (await AggregateEntityAccess(user, entity, null, filterObjectWithPermissions))[0];
        }

        using (DbCommand command = dbStructure.GetDbCommand(query, connection))
        {
            command.Parameters.AddRange(parameters.ToArray());

            if (filters != null)
            {
                command.Parameters.AddRange(filters.Item3.Select(x => dbStructure.GetDbParameter(x.Item1, x.Item2)).ToArray());
            }

            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            using DbDataReader reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                entities.Add(GetEntryFromReader(reader));
            }
        }

        string entitiyList = string.Join("", entities);
        string finalResponse = $@"{{
                ""recordsTotal"": {totalCount},
                ""recordsFiltered"": {filteredTotalCount},
                ""data"": {(entitiyList.Length == 0 ? "[]" : entitiyList)},
                ""aggregation"": {(summaries != null ? "[" + string.Join(", ", await AggregateEntityAccess(user, entity, null, filterObjectWithPermissions, summaries)) + "]" : "[]")}
            }}";

        var afterAction = attachedActionService.GetAttachedAction(controllerName, ActionType.GetAll, ActionOrderType.After);
        if (afterAction != null)
        {
            await afterAction(new ActionData<T>()
            {
                Page = page,
                FilterObject = filterObject,
                PageSize = pageSize,
                SortOrder = sortOrder,
                SortProperty = sortProperty,
                GetAllResultData = finalResponse,
                UserName = userName,
                Role = roleName
            });
        }

        return finalResponse;
    }

    public async Task<string> Post(ClaimsPrincipal user, string controllerName, Dictionary<string, JsonNode> fieldValues)
    {
        Dictionary<string, JsonNode> values = new(fieldValues.Select(x => new KeyValuePair<string, JsonNode>(x.Key.ToLowerInvariant(), x.Value)));

        (Entity entity, string permissionsFilter, string userId) = Authorize(user, controllerName, post: true);

        var action = attachedActionService.GetAttachedAction(controllerName, ActionType.Post, ActionOrderType.Before);
        if (action != null)
        {
            await action(new ActionData<T>()
            {
                InputParameterData = JsonSerializer.Serialize(fieldValues),
                UserName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null,
                Role = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null,
            });
        }

        List<Property> properties = entity.Properties.Where(x => !x.IsKey && !x.IsReadOnly && !x.IsHidden).OrderBy(x => x.PropertyName).ToList();

        properties = properties.Union(entity.Properties.Where(x => !string.IsNullOrEmpty(x.DefaultValue))).ToList();

        var usedProperties = properties
            .Where(prop => (values.ContainsKey((prop.ModelPropertyName ?? prop.PropertyName).ToLowerInvariant()) || !string.IsNullOrEmpty(prop.DefaultValue))
                && (!string.IsNullOrEmpty(prop.DefaultValue) || prop.ReferencingEntityId != null ||
                    values[(prop.ModelPropertyName ?? prop.PropertyName).ToLowerInvariant()] as JsonValue != null));

        var query = sqlDialect.GetInsertReturningId<T>(entity.TableName,
            dbStructure.DataSchema,
            entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID",
            usedProperties.Select(x => $"{x.PropertyName}"),
            usedProperties.Select((a, b) => "PropertyValue" + b.ToString()));

        DbConnection connection = this.connection as DbConnection;

        using DbCommand command = dbStructure.GetDbCommand(query, connection);
        int i = 0;
        List<Tuple<string, string>> errors = new();

        foreach (Property prop in properties.ToArray())
        {
            object actualValue = DBNull.Value;

            if (string.IsNullOrEmpty(prop.DefaultValue))
            {
                if (values.ContainsKey((prop.ModelPropertyName ?? prop.PropertyName).ToLowerInvariant()))
                {
                    JsonNode value = values[(prop.ModelPropertyName ?? prop.PropertyName).ToLowerInvariant()];
                    if (prop.ReferencingEntity != null)
                    {
                        if (value == null)
                        {
                            actualValue = DBNull.Value;
                        }
                        else if (value is JsonValue value1)
                        {
                            actualValue = (object)value1.ToString() ?? DBNull.Value;
                        }
                        else if (value is JsonObject obj)
                        {
                            var tmp = prop.ReferencingEntity.Properties.FirstOrDefault(x => x.IsKey);
                            string idName = (tmp?.ModelPropertyName ?? tmp?.PropertyName).CamelCaseName() ?? "id";
                            actualValue = obj[idName];

                            if (actualValue is JsonValue actualValueJson)
                            {
                                actualValue = actualValueJson.ToString();
                            }
                            else if (actualValue == null)
                            {
                                actualValue = DBNull.Value;
                            }
                        }
                    }
                    else if (value is JsonValue value1)
                    {
                        actualValue = value1.ToString();
                    }

                    command.Parameters.Add(dbStructure.GetDbParameter("PropertyValue" + i.ToString(), actualValue ?? DBNull.Value));

                    i++;
                }
            }
            else
            {
                if (string.Equals(prop.DefaultValue, "$user", StringComparison.OrdinalIgnoreCase))
                {
                    command.Parameters.Add(dbStructure.GetDbParameter<object>("PropertyValue" + i.ToString(), string.IsNullOrEmpty(userId) ? DBNull.Value : userId));
                    i++;
                }
                else
                {
                    throw new GenericBackendSecurityException(SecurityStatus.BadRequest, new { Error = "Default value not recognised", Property = prop.TitleCaseName() });
                }
            }

            string error = ValidatePropertyValue(prop, actualValue);

            if (error != null)
            {
                errors.Add(new(error, prop.ModelPropertyName ?? prop.PropertyName));
            }
        }

        if (errors.Count > 0)
        {
            throw new GenericBackendSecurityException(SecurityStatus.BadRequest, errors.Select(x => new { Error = x.Item1, Property = x.Item2 }));
        }

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            DbTransaction transaction = connection.BeginTransaction();

            command.Transaction = transaction;
            using DbDataReader sr = await command.ExecuteReaderAsync();
            if (await sr.ReadAsync())
            {
                T newId = sr.GetFieldValue<T>(0);

                StringBuilder qb2 = new();
                foreach (var crossEntity in entity.EntityRelations1)
                {
                    PrepareCrossEntityInsertQuery(values, newId, qb2, crossEntity);
                }

                foreach (var crossEntity in entity.EntityRelations2)
                {
                    PrepareCrossEntityInsertQuery(values, newId, qb2, crossEntity, true);
                }

                string crossTableQuery = qb2.ToString();
                if (!string.IsNullOrEmpty(crossTableQuery))
                {
                    using DbCommand crossTableCommand = dbStructure.GetDbCommand(crossTableQuery, connection);
                    crossTableCommand.Transaction = transaction;
                    await crossTableCommand.ExecuteNonQueryAsync();
                }

                await sr.CloseAsync();
                await transaction.CommitAsync();

                var savedEntity = await Get(user, controllerName, newId);

                var afterAction = attachedActionService.GetAttachedAction(controllerName, ActionType.Post, ActionOrderType.After);
                if (afterAction != null)
                {
                    await afterAction(new ActionData<T>()
                    {
                        Id = newId,
                        SavedParameterData = savedEntity,
                        InputParameterData = JsonSerializer.Serialize(fieldValues),
                        UserName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null,
                        Role = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null,
                    });
                }

                return savedEntity;
            }
            else
            {
                throw new Exception();
            }
        }
        catch
        {
            throw new Exception();
        }
    }

    public async Task PostRelatedEntity(ClaimsPrincipal user, string controllerName, T id, string relatedEntityName, RelatedEntityObject<T> relatedEntity)
    {
        (Entity entity, string permissionsFilter, string _) = Authorize(user, controllerName, post: true);

        EntityRelation crossEntity = entity.EntityRelations1.FirstOrDefault(x => string.Equals(x.Entity1PropertyName, relatedEntityName, StringComparison.OrdinalIgnoreCase));
        Entity entity1 = crossEntity?.Entity2;
        string propertyFrom = crossEntity?.Entity1ReferencingColumnName;
        string propertyTo = crossEntity?.Entity2ReferencingColumnName;

        if (crossEntity == null)
        {
            crossEntity = entity.EntityRelations2.FirstOrDefault(x => string.Equals(x.Entity2PropertyName, relatedEntityName, StringComparison.OrdinalIgnoreCase));
            entity1 = crossEntity?.Entity1;
            propertyFrom = crossEntity?.Entity2ReferencingColumnName;
            propertyTo = crossEntity?.Entity1ReferencingColumnName;
        }

        if (crossEntity == null)
        {
            throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Entity relation not found");
        }

        string entityKey = entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "Id";
        if ((await AggregateEntityAccess(user, entity, permissionsFilter, new ComparerObject() { Filter = id.ToString(), Property = entityKey }))[0] == "0")
        {
            throw new GenericBackendSecurityException(SecurityStatus.NotFound);
        }

        // TODO: ADD PREMISSION FILTER !!! IMPORTANT
        string relatedEntityKey = entity1.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "Id";
        if ((await AggregateEntityAccess(user, entity1, null, new ComparerObject() { Filter = relatedEntity.Id.ToString(), Property = relatedEntityKey }))[0] == "0")
        {
            throw new GenericBackendSecurityException(SecurityStatus.NotFound);
        }

        StringBuilder queryBuilder = new();
        queryBuilder.Append($"INSERT INTO {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{crossEntity.CrossTableName}{sqlDialect.ColumnDelimiterRight} (");
        if (!string.IsNullOrEmpty(crossEntity.ValidFromColumnName))
        {
            queryBuilder.Append($"{sqlDialect.ColumnDelimiterLeft}{crossEntity.ValidFromColumnName}{sqlDialect.ColumnDelimiterRight}, ");
        }

        queryBuilder.Append($"{propertyFrom}, {propertyTo})");
        queryBuilder.Append($"VALUES (");

        if (!string.IsNullOrEmpty(crossEntity.ValidFromColumnName))
        {
            queryBuilder.Append($"{sqlDialect.GetCurrentTimestamp}, ");
        }

        queryBuilder.Append($"'{id}', '{relatedEntity.Id}')");

        DbConnection connection = this.connection as DbConnection;

        try
        {
            using DbCommand command = dbStructure.GetDbCommand(queryBuilder.ToString(), connection);
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            throw new Exception();
        }
    }

    public async Task<T> Patch(ClaimsPrincipal user, string controllerName, T? id, Dictionary<string, JsonNode> fieldValues)
    {
        Dictionary<string, JsonNode> values = new(fieldValues.Select(x => new KeyValuePair<string, JsonNode>(x.Key.ToLowerInvariant(), x.Value)));

        (Entity entity, string permissionsFilter, string _) = Authorize(user, controllerName, put: true);

        var action = attachedActionService.GetAttachedAction(controllerName, ActionType.Patch, ActionOrderType.Before);
        if (action != null)
        {
            await action(new ActionData<T>()
            {
                Id = id ?? (T)stringParsers[typeof(T)](values[entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName.ToLowerInvariant() ?? "id"].ToString()),
                InputParameterData = JsonSerializer.Serialize(fieldValues),
                UserName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null,
                Role = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null,
            });
        }

        List<Property> properties = entity.Properties.Where(x => !x.IsKey && !x.IsReadOnly && !x.IsHidden).OrderBy(x => x.PropertyName).ToList();

        int i = 0;

        List<Tuple<string, string>> errors = new();
        List<DbParameter> parameters = new();

        foreach (Property prop in properties.ToArray())
        {
            object actualValue = DBNull.Value;
            if (values.ContainsKey(prop.CamelCaseName().ToLowerInvariant()))
            {
                JsonNode value = values[prop.CamelCaseName().ToLowerInvariant()];

                if (value == null)
                {
                    actualValue = DBNull.Value;
                }
                else if (value is JsonValue value1)
                {
                    actualValue = (object)value1.ToString() ?? DBNull.Value;
                }
                else if (value is JsonObject obj)
                {
                    var tmp = prop.ReferencingEntity.Properties.FirstOrDefault(x => x.IsKey);
                    string idName = (tmp?.ModelPropertyName ?? tmp?.PropertyName).CamelCaseName() ?? "id";
                    actualValue = obj[idName];

                    if (actualValue is JsonValue actualValueJson)
                    {
                        actualValue = actualValueJson.ToString();
                    }
                    else if (actualValue == null && obj.ContainsKey(idName))
                    {
                        actualValue = DBNull.Value;
                    }
                    else if (actualValue == null)
                    {
                        properties.Remove(prop);
                        continue;
                    }
                }

                string error = ValidatePropertyValue(prop, actualValue);

                if (error != null)
                {
                    errors.Add(new(error, prop.ModelPropertyName ?? prop.PropertyName));
                }

                parameters.Add(dbStructure.GetDbParameter("PropertyValue" + i.ToString(), actualValue ?? DBNull.Value));

                i++;
            }
        }

        StringBuilder queryBuilder = new();
        queryBuilder.Append($"UPDATE {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{entity.TableName}{sqlDialect.ColumnDelimiterRight} " +
            $"SET {string.Join($", ", properties.Where(x => values.ContainsKey(x.CamelCaseName().ToLowerInvariant())).Select((x, i) => $"{sqlDialect.ColumnDelimiterLeft}{x.PropertyName}{sqlDialect.ColumnDelimiterRight}=@PropertyValue" + i.ToString()))}");
        queryBuilder.Append($" WHERE {entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}=@ID");

        if (entity.SoftDeleteColumn != null)
        {
            queryBuilder.Append($" AND ({sqlDialect.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{sqlDialect.ColumnDelimiterRight} IS NULL OR " +
                $"{sqlDialect.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{sqlDialect.ColumnDelimiterRight} = 0)");
        }

        List<Tuple<string, object>> permissionsFilterParams = new();

        ComparerObject filterObjectWithPermissions = null;

        if (!string.IsNullOrEmpty(permissionsFilter))
        {
            filterObjectWithPermissions = JsonSerializer.Deserialize<ComparerObject>(permissionsFilter, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
        }

        var filters = filterObjectWithPermissions?.ToSQLQuery(sqlDialect, user, entity, dbSchema, 0, $"{dbSchema}.{sqlDialect.ColumnDelimiterLeft}{entity.TableName}{sqlDialect.ColumnDelimiterRight}", null);

        if (filters != null && filters.Item1 != null && filters.Item1.Replace("(", "").Replace(")", "").Length > 0)
        {
            queryBuilder.Append($" AND {filters.Item1}");
        }

        DbConnection connection = this.connection as DbConnection;
        T id1;
        using DbCommand command = dbStructure.GetDbCommand(queryBuilder.ToString(), connection);
        if (id != null)
        {
            id1 = id;
            command.Parameters.Add(dbStructure.GetDbParameter("ID", id));
        }
        else
        {
            id1 = (T)stringParsers[typeof(T)](values[entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName.ToLowerInvariant() ?? "id"].ToString());
            command.Parameters.Add(dbStructure.GetDbParameter("ID", values[entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName.ToLowerInvariant() ?? "id"].ToString()));
        }

        if (filters != null)
        {
            command.Parameters.AddRange(filters.Item3.Select(x => dbStructure.GetDbParameter(x.Item1, x.Item2)).ToArray());
        }

        command.Parameters.AddRange(parameters.ToArray());

        if (errors.Count > 0)
        {
            throw new GenericBackendSecurityException(SecurityStatus.BadRequest, errors.Select(x => new { Error = x.Item1, Property = x.Item2 }));
        }

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        DbTransaction transaction = connection.BeginTransaction();
        command.Transaction = transaction;

        int result = await command.ExecuteNonQueryAsync();
        if (result == 0)
        {
            throw new GenericBackendSecurityException(SecurityStatus.NotFound);
        }

        StringBuilder qb2 = new();

        string lastCrossTable = "";
        string lastColumn1 = "";
        string lastColumn2 = "";
        foreach (var crossEntity in entity.EntityRelations1.OrderBy(x => x.CrossTableName).ThenBy(x => x.Entity1ReferencingColumnName))
        {
            if (crossEntity.CrossTableName == lastCrossTable &&
                crossEntity.Entity1ReferencingColumnName == lastColumn1 &&
                crossEntity.Entity2ReferencingColumnName == lastColumn2)
            {
                continue;
            }

            lastCrossTable = crossEntity.CrossTableName;
            lastColumn1 = crossEntity.Entity1ReferencingColumnName;
            lastColumn2 = crossEntity.Entity2ReferencingColumnName;

            PrepareCrossEntityInsertQuery(values, id1, qb2, crossEntity, false, true);
        }

        lastCrossTable = "";
        lastColumn1 = "";
        lastColumn2 = "";
        foreach (var crossEntity in entity.EntityRelations2.OrderBy(x => x.CrossTableName).ThenBy(x => x.Entity1ReferencingColumnName))
        {
            if (crossEntity.CrossTableName == lastCrossTable &&
                crossEntity.Entity1ReferencingColumnName == lastColumn1 &&
                crossEntity.Entity2ReferencingColumnName == lastColumn2)
            {
                continue;
            }

            lastCrossTable = crossEntity.CrossTableName;
            lastColumn1 = crossEntity.Entity1ReferencingColumnName;
            lastColumn2 = crossEntity.Entity2ReferencingColumnName;

            PrepareCrossEntityInsertQuery(values, id1, qb2, crossEntity, true, true);
        }

        string crossTableQuery = qb2.ToString();
        if (!string.IsNullOrEmpty(crossTableQuery))
        {
            using DbCommand crossTableCommand = dbStructure.GetDbCommand(crossTableQuery, connection);
            crossTableCommand.Transaction = transaction;
            await crossTableCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();

        var afterAction = attachedActionService.GetAttachedAction(controllerName, ActionType.Patch, ActionOrderType.After);
        if (afterAction != null)
        {
            await afterAction(new ActionData<T>()
            {
                Id = id1,
                InputParameterData = JsonSerializer.Serialize(values),
                SavedParameterData = await Get(user, controllerName, id1),
                UserName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null,
                Role = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null,
            });
        }

        return id1;
    }

    public async Task Delete(ClaimsPrincipal user, string controllerName, T id)
    {
        (Entity entity, string permissionsFilter, string _) = Authorize(user, controllerName, delete: true);

        var action = attachedActionService.GetAttachedAction(controllerName, ActionType.Delete, ActionOrderType.Before);
        if (action != null)
        {
            await action(new ActionData<T>()
            {
                Id = id,
                UserName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null,
                Role = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null,
            });
        }

        string entityKey = entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "Id";
        if ((await AggregateEntityAccess(user, entity, permissionsFilter, new ComparerObject() { Filter = id, Property = entityKey }))[0] == "0")
        {
            throw new GenericBackendSecurityException(SecurityStatus.NotFound);
        }

        StringBuilder queryBuilder = new();
        if (entity.SoftDeleteColumn == null)
        {
            queryBuilder.Append($"DELETE FROM {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{entity.TableName}{sqlDialect.ColumnDelimiterRight} ");
        }
        else
        {
            queryBuilder.Append($"UPDATE {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{entity.TableName}{sqlDialect.ColumnDelimiterRight} SET {sqlDialect.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{sqlDialect.ColumnDelimiterRight} = 1 ");
        }

        queryBuilder.Append($"WHERE {sqlDialect.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}{sqlDialect.ColumnDelimiterRight}=@ID");

        DbConnection connection = this.connection as DbConnection;

        using DbCommand command = dbStructure.GetDbCommand(queryBuilder.ToString(), connection);
        command.Parameters.Add(dbStructure.GetDbParameter("ID", id));

        string oneEntry = await Get(user, controllerName, id);

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        await command.ExecuteNonQueryAsync();

        var afterAction = attachedActionService.GetAttachedAction(controllerName, ActionType.Delete, ActionOrderType.After);
        if (afterAction != null)
        {
            await afterAction(new ActionData<T>()
            {
                Id = id,
                UserName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null,
                Role = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null,
                InputParameterData = oneEntry
            });
        }
    }

    public async Task DeleteRelatedEntity(ClaimsPrincipal user, string controllerName, T id, string relatedEntityName, T relatedEntityId)
    {
        (Entity entity, string permissionsFilter, string _) = Authorize(user, controllerName, delete: true);

        EntityRelation crossEntity = entity.EntityRelations1.FirstOrDefault(x => string.Equals(x.Entity1PropertyName, relatedEntityName, StringComparison.OrdinalIgnoreCase));
        Entity entity1 = crossEntity?.Entity2;
        string propertyFrom = crossEntity?.Entity1ReferencingColumnName;
        string propertyTo = crossEntity?.Entity2ReferencingColumnName;

        if (crossEntity == null)
        {
            crossEntity = entity.EntityRelations2.FirstOrDefault(x => string.Equals(x.Entity2PropertyName, relatedEntityName, StringComparison.OrdinalIgnoreCase));
            entity1 = crossEntity?.Entity1;
            propertyFrom = crossEntity?.Entity2ReferencingColumnName;
            propertyTo = crossEntity?.Entity1ReferencingColumnName;
        }

        if (crossEntity == null)
        {
            throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Entity relation not found");
        }

        string entityKey = entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "Id";
        if ((await AggregateEntityAccess(user, entity, permissionsFilter, new ComparerObject() { Filter = id.ToString(), Property = entityKey }))[0] == "0")
        {
            throw new GenericBackendSecurityException(SecurityStatus.NotFound);
        }

        // TODO: ADD PREMISSION FILTER !!! IMPORTANT
        string relatedEntityKey = entity1.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "Id";
        if ((await AggregateEntityAccess(user, entity1, null, new ComparerObject() { Filter = relatedEntityId.ToString(), Property = relatedEntityKey }))[0] == "0")
        {
            throw new GenericBackendSecurityException(SecurityStatus.NotFound);
        }

        StringBuilder queryBuilder = new();
        if (string.IsNullOrEmpty(crossEntity.ActiveColumnName) &&
            string.IsNullOrEmpty(crossEntity.ValidToColumnName))
        {
            queryBuilder.Append($"DELETE FROM {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{crossEntity.CrossTableName}{sqlDialect.ColumnDelimiterRight} ");
        }

        if (!string.IsNullOrEmpty(crossEntity.ActiveColumnName))
        {
            queryBuilder.Append($"UPDATE {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{crossEntity.CrossTableName}{sqlDialect.ColumnDelimiterRight} SET {sqlDialect.ColumnDelimiterLeft}{crossEntity.ActiveColumnName}{sqlDialect.ColumnDelimiterRight} = 0");

            if (!string.IsNullOrEmpty(crossEntity.ValidToColumnName))
            {
                queryBuilder.Append($", {sqlDialect.ColumnDelimiterLeft}{crossEntity.ValidToColumnName}{sqlDialect.ColumnDelimiterRight} = {sqlDialect.GetCurrentTimestamp}");
            }
        }
        else if (!string.IsNullOrEmpty(crossEntity.ValidToColumnName))
        {
            queryBuilder.Append($"UPDATE {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{crossEntity.CrossTableName}{sqlDialect.ColumnDelimiterRight} SET {sqlDialect.ColumnDelimiterLeft}{crossEntity.ValidToColumnName}{sqlDialect.ColumnDelimiterRight} = {sqlDialect.GetCurrentTimestamp}");
        }

        queryBuilder.Append($" WHERE {sqlDialect.ColumnDelimiterLeft}{propertyFrom}{sqlDialect.ColumnDelimiterRight}='{id}' AND {sqlDialect.ColumnDelimiterLeft}{propertyTo}{sqlDialect.ColumnDelimiterRight}='{relatedEntityId}'");

        DbConnection connection = this.connection as DbConnection;

        using DbCommand command = dbStructure.GetDbCommand(queryBuilder.ToString(), connection);

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        await command.ExecuteNonQueryAsync();
    }

    private void PrepareCrossEntityInsertQuery(Dictionary<string, JsonNode> values, T newId, StringBuilder qb2, EntityRelation crossEntity, bool reverseProperties = false, bool clearRelations = false)
    {
        string entity1PropertyName = reverseProperties ? crossEntity.Entity2PropertyName : crossEntity.Entity1PropertyName;
        Entity entity = reverseProperties ? crossEntity.Entity1 : crossEntity.Entity2;
        string entity2ReferencingColumnName = reverseProperties ? crossEntity.Entity1ReferencingColumnName : crossEntity.Entity2ReferencingColumnName;
        string entity1ReferencingColumnName = reverseProperties ? crossEntity.Entity2ReferencingColumnName : crossEntity.Entity1ReferencingColumnName;

        // TODO: IMPORTANT!!! ADD SECURITY FOR CROSS-TABLE ENTRY !!!
        if (values.ContainsKey(entity1PropertyName.ToLowerInvariant()))
        {
            dynamic crossValue = values[entity1PropertyName.ToLowerInvariant()];

            if (crossValue is JsonArray crossValueArray && crossValueArray != null)
            {
                var tmp1 = crossValueArray.ToArray().Select(x => (Guid)x[entity.Properties.First(x => x.IsKey).CamelCaseName() ?? "id"]);

                if (clearRelations)
                {
                    if (!string.IsNullOrEmpty(crossEntity.ValidToColumnName))
                    {
                        qb2.Append($"UPDATE {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{crossEntity.CrossTableName}{sqlDialect.ColumnDelimiterRight} SET ");
                        qb2.Append($"{sqlDialect.ColumnDelimiterLeft}{crossEntity.ValidFromColumnName}{sqlDialect.ColumnDelimiterRight} = {sqlDialect.GetCurrentTimestamp} ");
                    }
                    else if (!string.IsNullOrEmpty(crossEntity.ActiveColumnName))
                    {
                        qb2.Append($"UPDATE {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{crossEntity.CrossTableName}{sqlDialect.ColumnDelimiterRight} SET ");
                        qb2.Append($"{sqlDialect.ColumnDelimiterLeft}{crossEntity.ActiveColumnName}{sqlDialect.ColumnDelimiterRight} = 0 ");
                    }
                    else
                    {
                        qb2.Append($"DELETE FROM {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{crossEntity.CrossTableName}{sqlDialect.ColumnDelimiterRight} ");
                    }

                    qb2.AppendLine($"WHERE {sqlDialect.ColumnDelimiterLeft}{entity1ReferencingColumnName}{sqlDialect.ColumnDelimiterRight} = '{newId}'");

                    if (tmp1.Any())
                    {
                        qb2.Append($" AND {sqlDialect.ColumnDelimiterLeft}{entity2ReferencingColumnName}{sqlDialect.ColumnDelimiterRight} NOT IN ('{string.Join("', '", tmp1)}');");
                    }
                    else
                    {
                        qb2.Append(';');
                    }
                }

                foreach (Guid refId in tmp1)
                {
                    qb2.AppendLine($@"IF NOT EXISTS(SELECT * FROM {sqlDialect.ColumnDelimiterLeft}{crossEntity.CrossTableName}{sqlDialect.ColumnDelimiterRight}
                           WHERE {sqlDialect.ColumnDelimiterLeft}{entity1ReferencingColumnName}{sqlDialect.ColumnDelimiterRight} = '{newId}'
                           AND {sqlDialect.ColumnDelimiterLeft}{entity2ReferencingColumnName}{sqlDialect.ColumnDelimiterRight} = '{refId}')");

                    qb2.Append($"INSERT INTO {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{crossEntity.CrossTableName}{sqlDialect.ColumnDelimiterRight} (");
                    if (!string.IsNullOrEmpty(crossEntity.ValidFromColumnName))
                    {
                        qb2.Append($"{sqlDialect.ColumnDelimiterLeft}{crossEntity.ValidFromColumnName}{sqlDialect.ColumnDelimiterRight}, ");
                    }

                    qb2.Append($"{sqlDialect.ColumnDelimiterLeft}{entity1ReferencingColumnName}{sqlDialect.ColumnDelimiterRight}, {sqlDialect.ColumnDelimiterLeft}{entity2ReferencingColumnName}{sqlDialect.ColumnDelimiterRight})");
                    qb2.Append($"VALUES (");

                    if (!string.IsNullOrEmpty(crossEntity.ValidFromColumnName))
                    {
                        qb2.Append($"{sqlDialect.GetCurrentTimestamp}, ");
                    }

                    qb2.AppendLine($"'{newId}', '{refId}')");
                }
            }
        }
    }

    private static string GetEntryFromReader(DbDataReader reader)
    {
        return reader.GetTextReader(0).ReadToEnd();
    }

    private string AddOrderByToQuery(string query, Entity entity, string sortProperty, string sortOrder)
    {
        string alteredQuery = query;
        string keyProperty = entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "Id";

        string[] sortTable = !string.IsNullOrEmpty(sortProperty) ?
            sortProperty.Split('.') :
            Array.Empty<string>();

        StringBuilder sortBuilder = new();

        if (sortTable.Length > 1)
        {
            sortBuilder.Append(sqlDialect.GetJsonPropertyNavigation(sortTable, sortTable[0]));
        }
        else if (!string.IsNullOrEmpty(sortProperty))
        {
            var columnName = entity.Properties
                .Where(x => x.CamelCaseName().ToLowerInvariant() == sortProperty.ToLowerInvariant())
                .Select(x => x.PropertyName)
                .FirstOrDefault();

            sortBuilder.Append($"{sqlDialect.ColumnDelimiterLeft}{columnName}{sqlDialect.ColumnDelimiterRight}");
        }

        string orderByColumn = sortBuilder.Length == 0 ? $"{sqlDialect.ColumnDelimiterLeft}tab1{sqlDialect.ColumnDelimiterRight}.{sqlDialect.ColumnDelimiterLeft}{keyProperty}{sqlDialect.ColumnDelimiterRight}" : sortBuilder.ToString();
        alteredQuery += $" ORDER BY {orderByColumn} {(string.Equals(sortOrder, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC")}";
        return alteredQuery;
    }

    private async Task<string[]> AggregateEntityAccess(ClaimsPrincipal user, Entity entity, string permissionsFilter, IComparerObject filterObject = null, SummaryRequestObject[] summaries = null)
    {
        string countQuery = "";
        List<string> summaryNames = new();

        if (summaries == null)
        {
            countQuery = @$"SELECT COUNT(*) FROM {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{entity.TableName}{sqlDialect.ColumnDelimiterRight} tab1";
        }
        else
        {
            countQuery = @$"SELECT {string.Join(", ", ValidateSummaries(entity, summaries, summaryNames))} FROM {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{entity.TableName}{sqlDialect.ColumnDelimiterRight} tab1";
        }

        bool countWhereActivated = false;

        if (entity.SoftDeleteColumn != null)
        {
            countQuery += $" WHERE ({sqlDialect.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{sqlDialect.ColumnDelimiterRight} IS NULL OR {sqlDialect.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{sqlDialect.ColumnDelimiterRight} = 0)";
            countWhereActivated = true;
        }

        List<Tuple<string, object>> permissionsFilterParams = new();

        ComparerObject filterObjectWithPermissions = filterObject as ComparerObject;
        ComparerObject permissionFilterObject = null;

        if (!string.IsNullOrEmpty(permissionsFilter))
        {
            permissionFilterObject = JsonSerializer.Deserialize<ComparerObject>(permissionsFilter, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

            if (filterObjectWithPermissions != null)
            {
                filterObjectWithPermissions = new ComparerObject()
                {
                    Comparisons = new[]
                    {
                        filterObjectWithPermissions,
                        permissionFilterObject
                    },
                    Conjunction = "and"
                };
            }
        }

        var filters = filterObjectWithPermissions?.ToSQLQuery(sqlDialect, user, entity, dbSchema, 0, "tab1", null);

        if (filters != null && filters.Item1 != null && filters.Item1.Replace("(", "").Replace(")", "").Length > 0)
        {
            countQuery += countWhereActivated ? " AND (" : " WHERE (";
            countQuery += filters.Item1;
            countQuery += ")";
        }

        DbConnection connection = this.connection as DbConnection;

        using DbCommand command = dbStructure.GetDbCommand(countQuery, connection);
        if (filterObjectWithPermissions != null)
        {
            command.Parameters.AddRange(filters.Item3.Select(x => dbStructure.GetDbParameter(x.Item1, x.Item2)).ToArray());
        }

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        if (summaries == null)
        {
            return new string[] { ((await command.ExecuteScalarAsync()) ?? 0).ToString() };
        }
        else
        {
            List<string> result = new();
            using DbDataReader dr = await command.ExecuteReaderAsync();
            if (!await dr.ReadAsync())
            {
                return null;
            }

            for (int i = 0; i < summaryNames.Count; i++)
            {
                var tmp = summaryNames[i];
                var tmpValue = dr.GetValue(i);
                var res = JsonSerializer.Serialize(tmpValue == DBNull.Value ? null : tmpValue);
                result.Add($@"{tmp} {res} }}");
            }

            return result.ToArray();
        }
    }

    private static string[] ValidateSummaries(Entity entity, SummaryRequestObject[] summaries, List<string> summaryNames)
    {
        List<string> data = new();
        foreach (var summary in summaries)
        {
            var property = entity.Properties.Where(x => string.Equals(x.ModelPropertyName ?? x.PropertyName, summary.Property, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (property != null)
            {
                string comparison = string.Empty;
                comparison = summary.AggregationType switch
                {
                    SummaryTypes.AVG => $"AVG({property.PropertyName})",
                    SummaryTypes.MIN => $"MIN({property.PropertyName})",
                    SummaryTypes.MAX => $"MAX({property.PropertyName})",
                    SummaryTypes.COUNT => $"COUNT({property.PropertyName})",
                    SummaryTypes.COUNT_DISTINCT => $"COUNT(DISTINCT {property.PropertyName})",
                    SummaryTypes.SUM => $"SUM({property.PropertyName})"
                };

                data.Add(comparison);
                summaryNames.Add($@"{{ ""property"": ""{(property.ModelPropertyName ?? property.PropertyName).CamelCaseName()}"", ""aggregationType"": ""{summary.AggregationType}"", ""value"": ");
            }
        }

        return data.ToArray();
    }

    private List<Tuple<string, string>> GetJoinsForSelect(Entity entity, string? joinProperty, string joinTableName, List<Tuple<string, string>> properties, Dictionary<string, SelectPropertyData> joinData, string? path, ref int counter)
    {
        string tableName = $"tab{++counter}";
        var data = new SelectPropertyData()
        {
            TableName = tableName,
            TableDTOName = path,
            OriginalTableName = entity.TableName,
            JoinTableName = joinTableName,
            JoinPropertyName = joinProperty,
            IdPropertyName = $"{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}",
            Properties = new List<Tuple<string, string, string>>()
        };

        joinData.Add(tableName, data);

        List<Tuple<string, string>> filterData = new()
        {
            new Tuple<string, string>(path ?? "", tableName)
        };

        string pathExtended = path == null ? "" : path + ".";

        foreach (Property property in entity.Properties.Where(x => !x.IsKey))
        {
            var prop = properties.FirstOrDefault(y => string.Equals(y.Item2.Split(".")[0], property.CamelCaseName(), StringComparison.OrdinalIgnoreCase));

            if (prop == null)
            {
                continue;
            }

            if (!prop.Item2.Contains('.'))
            {
                data.Properties.Add(new Tuple<string, string, string>(prop.Item1, prop.Item2, property.PropertyName));
            }
            else if (property.ReferencingEntity != null && prop != null && string.IsNullOrEmpty(property.RelatedModelPropertyName))
            {
                List<Tuple<string, string>> referencedProperties = properties
                    .Where(x => x.Item2.Contains('.') && string.Equals(x.Item2.Split(".")[0], property.CamelCaseName()))
                    .Select(x => new Tuple<string, string>(x.Item1, x.Item2[(x.Item2.IndexOf(".") + 1)..]))
                    .ToList();

                filterData.AddRange(GetJoinsForSelect(property.ReferencingEntity, property.PropertyName, tableName, referencedProperties, joinData, pathExtended + property.CamelCaseName(), ref counter));
            }
        }

        return filterData;
    }

    public string GenerateSelectQuery(Entity entity, IEnumerable<Guid> entities, ref int counter, string roleName, string userName, List<DbParameter> parameters, bool wrapInJson, string path, object filterValue = null, string[] properties = null)
    {
        string filter = GenericDataService<T>.AuthorizeSubentity(roleName, userName, entity);
        var model = GenerateSelectQueryInternal(entity, entities, ref counter, userName, parameters, path, null, filterValue);
        var queryBuilder = new StringBuilder();

        if (properties != null)
        {
            for (int i = 0; i < model.PropertyNames.Count; i++)
            {
                string property = GetNewPath(model.OutputPaths[i], model.ColumnPaths[i]);
                if (!properties.Contains(property))
                {
                    model.PropertyNames.RemoveAt(i);
                    model.PropertyValues.RemoveAt(i);
                    model.ColumnPaths.RemoveAt(i);
                    model.OutputPaths.RemoveAt(i);
                    i--;
                }
            }
        }

        queryBuilder.AppendLine(sqlDialect.GetBasicSelectQuery(model.PropertyNames, model.PropertyValues, model.ColumnPaths, model.OutputPaths, wrapInJson));
        queryBuilder.AppendLine($" FROM ");
        queryBuilder.AppendLine(model.JoinQueryPart[" LEFT JOIN".Length ..]);

        if (filterValue == null)
        {
            queryBuilder.AppendLine(" WHERE 1=1 ");
        }

        if (!string.IsNullOrEmpty(filter))
        {
            var permissionFilterObject = JsonSerializer.Deserialize<ComparerObject>(filter, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            var filters = permissionFilterObject?.ToSQLQuery(sqlDialect, userName, entity, dbSchema, parameters.Count, $"tab{counter + 1}", null);

            if (filters != null && filters.Item1 != null && filters.Item1.Replace("(", "").Replace(")", "").Length > 0)
            {
                queryBuilder.Append(" AND (");
                queryBuilder.Append(filters.Item1);
                queryBuilder.Append(')');

                parameters.AddRange(filters.Item3.Select(x => dbStructure.GetDbParameter(x.Item1, x.Item2)).ToArray());
            }
        }

        return queryBuilder.ToString();
    }

    private GenerateSelectQueryModel GenerateSelectQueryInternal(Entity entity, IEnumerable<Guid> entities, ref int counter, string userName, List<DbParameter> parameters, string path, string filterProperty = null, object filterValue = null)
    {
        int internalCounter = ++counter;

        var propertyValues = new List<string>(new string[] { $"tab{internalCounter}.{sqlDialect.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "Id"}{sqlDialect.ColumnDelimiterRight}" });
        var propertyNames = new List<string>(new string[] { entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName.CamelCaseName() ?? "Id" });
        var columnPaths = new List<string>(new string[] { entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName.CamelCaseName() ?? "Id" });
        var outputPaths = new List<string>(new string[] { path });
        StringBuilder joinPart = new();
        StringBuilder whereCondition = new();

        if (filterProperty != null)
        {
            joinPart.AppendLine($" LEFT JOIN {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{entity.TableName}{sqlDialect.ColumnDelimiterRight} AS tab{internalCounter} ON ");
            joinPart.Append($" tab{internalCounter}.{sqlDialect.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}{sqlDialect.ColumnDelimiterRight}={filterProperty}");
        }
        else
        {
            joinPart.AppendLine($" LEFT JOIN {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{entity.TableName}{sqlDialect.ColumnDelimiterRight} AS tab{internalCounter} ");
            // queryBuilder.AppendLine($"WHERE 1=1");
        }

        if (entity.SoftDeleteColumn != null)
        {
            joinPart.AppendLine($@" AND (tab{internalCounter}.{sqlDialect.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{sqlDialect.ColumnDelimiterRight} IS NULL OR tab{internalCounter}.{sqlDialect.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{sqlDialect.ColumnDelimiterRight} = 0)");
        }

        foreach (Property property in entity.Properties.Where(x => !x.IsKey && !x.IsHidden))
        {
            if (property.ReferencingEntityId == null)
            {
                propertyValues.Add($"tab{internalCounter}.{sqlDialect.ColumnDelimiterLeft}{property.PropertyName}{sqlDialect.ColumnDelimiterRight}");
                propertyNames.Add($"{sqlDialect.ColumnDelimiterLeft}{property.CamelCaseName()}{sqlDialect.ColumnDelimiterRight}");
                columnPaths.Add(property.CamelCaseName());
                outputPaths.Add(path);
            }
            else if (string.IsNullOrEmpty(property.RelatedModelPropertyName))
            {
                if (!entities.Contains(property.ReferencingEntityId.Value))
                {
                    var model = GenerateSelectQueryInternal(property.ReferencingEntity, entities.Union(new Guid[] { property.ReferencingEntityId.Value }), ref counter, userName, parameters, GetNewPath(path, property.CamelCaseName()), $"tab{internalCounter}.{sqlDialect.ColumnDelimiterLeft}{property.PropertyName}{sqlDialect.ColumnDelimiterRight}");

                    joinPart.Append(model.JoinQueryPart);
                    propertyValues.AddRange(model.PropertyValues);
                    propertyNames.AddRange(model.PropertyNames);
                    columnPaths.AddRange(model.ColumnPaths);
                    outputPaths.AddRange(model.OutputPaths);
                }
            }
        }

        foreach (Property property in entity.ReferencingProperties.Where(x => !string.IsNullOrEmpty(x.RelatedModelPropertyName) && !x.IsHidden))
        {
            if (!entities.Contains(property.EntityId))
            {
                var model = GenerateSelectQueryInternal(property.Entity, entities.Union(new Guid[] { property.EntityId }), ref counter, userName, parameters, GetNewPath(path, property.RelatedModelPropertyName.CamelCaseName()));

                model.JoinQueryPart += $" AND {sqlDialect.ColumnDelimiterLeft}{property.PropertyName}{sqlDialect.ColumnDelimiterRight} = tab{internalCounter}.{sqlDialect.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "Id"}{sqlDialect.ColumnDelimiterRight}";

                joinPart.Append(model.JoinQueryPart);
                propertyValues.AddRange(model.PropertyValues);
                propertyNames.AddRange(model.PropertyNames);
                columnPaths.AddRange(model.ColumnPaths);
                outputPaths.AddRange(model.OutputPaths);
            }
        }

        foreach (EntityRelation relation in entity.EntityRelations1)
        {
            if (!relation.ShowInEntity1)
            {
                continue;
            }

            if (entities.Contains(relation.Entity2Id))
            {
                continue;
            }

            string whereQueryAddition = "";
            if (!string.IsNullOrEmpty(relation.ActiveColumnName))
            {
                whereQueryAddition = $@" AND {sqlDialect.ColumnDelimiterLeft}{relation.ActiveColumnName}{sqlDialect.ColumnDelimiterRight} = 1 ";
            }

            if (!string.IsNullOrEmpty(relation.ValidFromColumnName))
            {
                whereQueryAddition = $@" AND ({sqlDialect.ColumnDelimiterLeft}{relation.ValidFromColumnName}{sqlDialect.ColumnDelimiterRight} IS NULL OR {sqlDialect.ColumnDelimiterLeft}{relation.ValidFromColumnName}{sqlDialect.ColumnDelimiterRight} <= {sqlDialect.GetCurrentTimestamp}) ";
            }

            if (!string.IsNullOrEmpty(relation.ValidToColumnName))
            {
                whereQueryAddition += $@" AND ({sqlDialect.ColumnDelimiterLeft}{relation.ValidToColumnName}{sqlDialect.ColumnDelimiterRight} IS NULL OR {sqlDialect.ColumnDelimiterLeft}{relation.ValidToColumnName}{sqlDialect.ColumnDelimiterRight} >= {sqlDialect.GetCurrentTimestamp}) ";
            }

            var model = GenerateSelectQueryInternal(relation.Entity2, entities.Union(new Guid[] { relation.Entity2Id }), ref counter, userName, parameters, relation.Entity1PropertyName.CamelCaseName());
            model.JoinQueryPart += $" AND {sqlDialect.ColumnDelimiterLeft}{relation.Entity2.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}{sqlDialect.ColumnDelimiterRight} IN (SELECT {sqlDialect.ColumnDelimiterLeft}{relation.Entity2ReferencingColumnName}{sqlDialect.ColumnDelimiterRight} FROM " +
                $"{dbSchema}.{sqlDialect.ColumnDelimiterLeft}{relation.CrossTableName}{sqlDialect.ColumnDelimiterRight} WHERE {sqlDialect.ColumnDelimiterLeft}{relation.Entity1ReferencingColumnName}{sqlDialect.ColumnDelimiterRight} = tab{internalCounter}.{sqlDialect.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}{sqlDialect.ColumnDelimiterRight} {whereQueryAddition}) ";

            joinPart.Append(model.JoinQueryPart);
            propertyValues.AddRange(model.PropertyValues);
            propertyNames.AddRange(model.PropertyNames);
            columnPaths.AddRange(model.ColumnPaths);
            outputPaths.AddRange(model.OutputPaths);
        }

        foreach (EntityRelation relation in entity.EntityRelations2)
        {
            if (relation.Entity1Id == relation.Entity2Id)
            {
                continue;
            }

            if (!relation.ShowInEntity2)
            {
                continue;
            }

            if (entities.Contains(relation.Entity1Id))
            {
                continue;
            }

            string whereQueryAddition = "";
            if (!string.IsNullOrEmpty(relation.ActiveColumnName))
            {
                whereQueryAddition = $@" AND {sqlDialect.ColumnDelimiterLeft}{relation.ActiveColumnName}{sqlDialect.ColumnDelimiterRight} = 1 ";
            }

            if (!string.IsNullOrEmpty(relation.ValidFromColumnName))
            {
                whereQueryAddition = $@" AND ({sqlDialect.ColumnDelimiterLeft}{relation.ValidFromColumnName}{sqlDialect.ColumnDelimiterRight} IS NULL OR {sqlDialect.ColumnDelimiterLeft}{relation.ValidFromColumnName}{sqlDialect.ColumnDelimiterRight} <= {sqlDialect.GetCurrentTimestamp}) ";
            }

            if (!string.IsNullOrEmpty(relation.ValidToColumnName))
            {
                whereQueryAddition += $@" AND ({sqlDialect.ColumnDelimiterLeft}{relation.ValidToColumnName}{sqlDialect.ColumnDelimiterRight} IS NULL OR {sqlDialect.ColumnDelimiterLeft}{relation.ValidToColumnName}{sqlDialect.ColumnDelimiterRight} >= {sqlDialect.GetCurrentTimestamp}) ";
            }

            var model = GenerateSelectQueryInternal(relation.Entity1, entities.Union(new Guid[] { relation.Entity1Id }), ref counter, userName, parameters, relation.Entity2PropertyName.CamelCaseName());
            model.JoinQueryPart += " AND " +
                $"{sqlDialect.ColumnDelimiterLeft}{relation.Entity1.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}{sqlDialect.ColumnDelimiterRight} IN " +
                $"(SELECT {sqlDialect.ColumnDelimiterLeft}{relation.Entity1ReferencingColumnName}{sqlDialect.ColumnDelimiterRight} FROM {dbSchema}.{sqlDialect.ColumnDelimiterLeft}{relation.CrossTableName}{sqlDialect.ColumnDelimiterRight} " +
                $"WHERE {sqlDialect.ColumnDelimiterLeft}{relation.Entity2ReferencingColumnName}{sqlDialect.ColumnDelimiterRight} = tab{internalCounter}.{sqlDialect.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}{sqlDialect.ColumnDelimiterRight} {whereQueryAddition})";

            joinPart.Append(model.JoinQueryPart);
            propertyValues.AddRange(model.PropertyValues);
            propertyNames.AddRange(model.PropertyNames);
            columnPaths.AddRange(model.ColumnPaths);
            outputPaths.AddRange(model.OutputPaths);
        }

        if (string.IsNullOrEmpty(filterProperty) && filterValue != null)
        {
            joinPart.AppendLine($" WHERE tab{internalCounter}.{sqlDialect.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}{sqlDialect.ColumnDelimiterRight}=@tab{internalCounter}_val");
        }

        return new GenerateSelectQueryModel()
        {
            JoinQueryPart = joinPart.ToString(),
            PropertyValues = propertyValues,
            PropertyNames = propertyNames,
            ColumnPaths = columnPaths,
            OutputPaths = outputPaths
        };
    }

    private static string GetNewPath(string originalPath, string pathAddition)
    {
        if (string.IsNullOrEmpty(originalPath))
        {
            return pathAddition;
        }

        return $"{originalPath}.{pathAddition}";
    }

    private string ValidatePropertyValue(Property property, object value)
    {
        Type propType = dbStructure.GetFieldType(property.PropertyName, property.Entity.TableName);
        bool isFieldNullable = dbStructure.GetFieldNullable(property.PropertyName, property.Entity.TableName);

        if (!isFieldNullable && (value == DBNull.Value || value == null) && !property.IsKey)
        {
            return FieldValueErrorEnum.Required.ToString();
        }

        if (propType == typeof(string))
        {
            DatabaseFieldSizeLimitation limits = dbStructure.GetFieldSizeLimitation(property.PropertyName, property.Entity.TableName);

            if (limits.Min.HasValue && limits.Min.Value > value.ToString().Length)
            {
                return FieldValueErrorEnum.ValueToShort.ToString();
            }

            if (value != null && limits.Max.HasValue && limits.Max.Value > -1 && limits.Max.Value < value.ToString().Length)
            {
                return FieldValueErrorEnum.ValueToLong.ToString();
            }

            string[] values = dbStructure.GetEnumValues(property.PropertyName, property.Entity.TableName);
            if (!(values?.Contains(value) ?? true))
            {
                return FieldValueErrorEnum.UnknownValue.ToString();
            }

            string regex = dbStructure.GetRegexValues(property.PropertyName, property.Entity.TableName);
            if (regex != null && new Regex(regex).Match(value?.ToString()).Success)
            {
                return FieldValueErrorEnum.UnknownValue.ToString();
            }
        }

        return null;
    }

    private Tuple<Entity, string, string?> Authorize(
        ClaimsPrincipal user,
        string controllerName,
        bool getOne = false,
        bool getAll = false,
        bool post = false,
        bool put = false,
        bool delete = false)
    {
        List<Entity> entityList = entities
            .Where(x => controllerName != null && x.ControllerName != null && x.ControllerName.ToLower() == controllerName.ToLower()).ToList();

        Entity entity = entityList.FirstOrDefault(e => !e.EntityRoles.Any());
        EntityRole er = null;
        string roleName = null;
        string userId = null;

        if (user.Identity.IsAuthenticated)
        {
            ClaimsIdentity userData = user.Identity as ClaimsIdentity;
            roleName = userData.FindFirst(userData.RoleClaimType).Value;
            userId = userData.FindFirst("id").Value;

            er = entityList.SelectMany(x => x.EntityRoles)
                .Where(r =>
                    r.Role.RoleName == roleName &&
                    (r.GetAll || !getAll) &&
                    (r.GetOne || !getOne) &&
                    (r.Post || !post) &&
                    (r.Put || !put) &&
                    (r.Delete || !delete))
                .OrderBy(x => getAll || getOne ? x.ViewFilter : x.EditFilter)
                .FirstOrDefault();

            entity = er?.Entity ?? entity;
        }

        if (entity == null)
        {
            if (entityList.Count == 0)
            {
                throw new GenericBackendSecurityException(SecurityStatus.NotFound);
            }
            else if (user.Identity.IsAuthenticated)
            {
                throw new GenericBackendSecurityException(SecurityStatus.Forbidden);
            }
            else
            {
                throw new GenericBackendSecurityException(SecurityStatus.Unauthorised);
            }
        }

        string permissionsFilter = getAll || getOne ? er?.ViewFilter : er?.EditFilter;

        if (!string.IsNullOrEmpty(permissionsFilter))
        {
            permissionsFilter = permissionsFilter.Replace("$user", userId.Replace("\"", "\\\""));
            permissionsFilter = permissionsFilter.Replace("$role", roleName.Replace("\"", "\\\""));
        }

        return new Tuple<Entity, string, string?>(entity, permissionsFilter, userId);
    }

    private static string AuthorizeSubentity(string roleName, string userId, Entity entity)
    {
        EntityRole er = null;

        if (!string.IsNullOrEmpty(userId))
        {
            er = entity.EntityRoles
                .Where(r => r.Role.RoleName == roleName &&
                           (r.GetAll || r.GetOne))
                .FirstOrDefault();

            entity = er?.Entity ?? entity;
        }

        if (entity == null)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                throw new GenericBackendSecurityException(SecurityStatus.Forbidden);
            }
            else
            {
                throw new GenericBackendSecurityException(SecurityStatus.Unauthorised);
            }
        }

        string permissionsFilter = er?.ViewFilter;

        if (!string.IsNullOrEmpty(permissionsFilter))
        {
            permissionsFilter = permissionsFilter.Replace("$user", userId.Replace("\"", "\\\""));
            permissionsFilter = permissionsFilter.Replace("$role", roleName.Replace("\"", "\\\""));
        }

        return permissionsFilter;
    }
}
