using BeGeneric.Backend.Models;
using BeGeneric.Backend.Services.BeGeneric.DatabaseStructure;
using BeGeneric.Backend.Services.BeGeneric.Exceptions;
using BeGeneric.Backend.Settings;
using BeGeneric.Helpers;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Endpoint = BeGeneric.Backend.Models.Endpoint;

namespace BeGeneric.Backend.Services.BeGeneric
{
    public class GenericDataService<T> : IGenericDataService<T>
    {
        protected readonly List<Entity> entities;
        protected readonly ILogger logger;
        protected readonly IDatabaseStructureService dbStructure;
        private readonly IAttachedActionService<T> attachedActionService;
        private readonly IDbConnection connection;

        internal readonly string dbSchema = "dbo";

        private readonly Dictionary<Type, Func<string, object>> stringParsers = new()
        {
            { typeof(int), (x) => int.Parse(x) },
            { typeof(Guid), (x) => new Guid(x) },
            { typeof(string), (x) => x },
        };

        private readonly Dictionary<Type, string> dbTypeParsers = new()
        {
            { typeof(int), "INT" },
            { typeof(Guid), "UNIQUEIDENTIFIER" },
            { typeof(string), "NVARCHAR(100)" },
        };

        public GenericDataService(List<EntityDefinition> entityDefinitions,
            IDatabaseStructureService dbStructure,
            IAttachedActionService<T> attachedActionService,
            IDbConnection connection,
            ILogger logger = null)
        {
            entities = entityDefinitions.ProcessEntities();
            this.logger = logger;
            this.dbStructure = dbStructure;
            this.dbSchema = dbStructure.DataSchema;
            this.connection = connection;
            this.attachedActionService = attachedActionService;
        }

        public async Task<string> Get(ClaimsPrincipal user, string controllerName, T id)
        {
            (Entity entity, string permissionsFilter) = await Authorize(user, controllerName, getOne: true);

            string userName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null;
            string roleName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null;

            var action = this.attachedActionService.GetAttachedAction(controllerName, ActionType.Get, ActionOrderType.Before);
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
            List<SqlParameter> sqlParameters = new();

            string query = GenerateSelectQuery(entity, ref tabCounter, roleName, userName, sqlParameters, null, id);

            List<Tuple<string, object>> permissionsFilterParams = new();

            ComparerObject filterObjectWithPermissions = null;

            if (!string.IsNullOrEmpty(permissionsFilter))
            {
                filterObjectWithPermissions = JsonSerializer.Deserialize<ComparerObject>(permissionsFilter, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            }

            var filters = filterObjectWithPermissions?.ToSQLQuery(user, entity, dbSchema, 0, "tab1", null);

            if (filters != null)
            {
                query += $" AND {filters.Item1}";
            }

            query += $" FOR JSON AUTO, INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER";

            SqlConnection connection = this.connection as SqlConnection;

            using DbCommand command = new SqlCommand(query, connection);
            command.Parameters.Add(new SqlParameter("tab1_val", id));

            command.Parameters.AddRange(sqlParameters.ToArray());

            if (filters != null)
            {
                command.Parameters.AddRange(filters.Item3.Select(x => new SqlParameter(x.Item1, x.Item2)).ToArray());
            }

            if (connection.State != System.Data.ConnectionState.Open)
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
                var afterAction = this.attachedActionService.GetAttachedAction(controllerName, ActionType.Get, ActionOrderType.After);
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

        public async Task<string> Get(ClaimsPrincipal user, string controllerName, int? page = null, int pageSize = 10, string? sortProperty = null, string? sortOrder = "ASC", ComparerObject? filterObject = null, SummaryRequestObject[] summaries = null)
        {
            (Entity entity, string permissionsFilter) = await Authorize(user, controllerName, getAll: true);

            string? userName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null;
            string? roleName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null;

            var action = this.attachedActionService.GetAttachedAction(controllerName, ActionType.GetAll, ActionOrderType.Before);
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

            ComparerObject filterObjectWithPermissions = filterObject;
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

            List<SqlParameter> parameters = new();

            string query = GenerateSelectQuery(entity, ref tabCounter, roleName, userName, parameters);
            var filters = filterObjectWithPermissions?.ToSQLQuery(user, entity, dbSchema, parameters.Count, "tab1", null);

            if (filters != null)
            {
                query += $" AND {filters.Item1}";
            }

            query = AddOrderByToQuery(query, entity, sortProperty, sortOrder);

            if (page != null)
            {
                query = AddPagingToQuery(query, page.Value - 1, pageSize);
            }

            query += " FOR JSON AUTO";

            SqlConnection connection = this.connection as SqlConnection;

            totalCount = (await AggregateEntityAccess(user, entity, null, filterObject: permissionFilterObject))[0];

            string filteredTotalCount = totalCount;

            if (filters != null)
            {
                filteredTotalCount = (await AggregateEntityAccess(user, entity, null, filterObjectWithPermissions))[0];
            }

            using (SqlCommand command = new(query, connection))
            {
                command.Parameters.AddRange(parameters.ToArray());

                if (filters != null)
                {
                    command.Parameters.AddRange(filters.Item3.Select(x => new SqlParameter(x.Item1, x.Item2)).ToArray());
                }

                if (connection.State != System.Data.ConnectionState.Open)
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
                ""summaries"": { (summaries != null ? ("[" + string.Join(", ", await AggregateEntityAccess(user, entity, null, filterObjectWithPermissions, summaries)) + "]") : "[]")}
            }}";

            var afterAction = this.attachedActionService.GetAttachedAction(controllerName, ActionType.GetAll, ActionOrderType.After);
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

        public async Task<string> Get(ClaimsPrincipal user, Endpoint endpoint, int? page = null, int pageSize = 10, string sortProperty = null, string sortOrder = "ASC", ComparerObject filterObject = null)
        {
            (Entity entity, string permissionsFilter) = await Authorize(user, endpoint.StartingEntity.ControllerName, getAll: true);

            var action = this.attachedActionService.GetAttachedAction(endpoint.EndpointPath, ActionType.GetAll, ActionOrderType.Before);
            if (action != null)
            {
                await action(new ActionData<T>()
                {
                    Page = page,
                    FilterObject = filterObject,
                    PageSize = pageSize,
                    SortOrder = sortOrder,
                    SortProperty = sortProperty
                });
            }

            List<string> entities = new();

            List<Tuple<string, object>> permissionsFilterParams = new();

            ComparerObject filterObjectWithPermissions = filterObject;
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

            Dictionary<string, SelectPropertyData> joinData = new();
            string query = GenerateSelectQuery(entity, endpoint.EndpointProperties.Select(x => new Tuple<string, string>(x.PropertyName, x.PropertyPath)).ToList(), joinData);

            var filters = filterObjectWithPermissions?.ToSQLQuery(user, entity, dbSchema, 0, "tab1", joinData);

            if (filters != null)
            {
                query += $" AND {filters.Item1}";
            }

            query = AddOrderByToQuery(query, entity, sortProperty, sortOrder);

            if (page != null)
            {
                query = AddPagingToQuery(query, page.Value - 1, pageSize);
            }

            query += " FOR JSON PATH";

            SqlConnection connection = this.connection as SqlConnection;

            using (SqlCommand command = new(query, connection))
            {
                if (filters != null)
                {
                    command.Parameters.AddRange(filters.Item3.Select(x => new SqlParameter(x.Item1, x.Item2)).ToArray());
                }

                if (connection.State != System.Data.ConnectionState.Open)
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

            string finalResult = entitiyList.Length == 0 ? "[]" : entitiyList;

            var afterAction = this.attachedActionService.GetAttachedAction(endpoint.EndpointPath, ActionType.GetAll, ActionOrderType.After);
            if (afterAction != null)
            {
                await afterAction(new ActionData<T>()
                {
                    Page = page,
                    FilterObject = filterObject,
                    PageSize = pageSize,
                    SortOrder = sortOrder,
                    SortProperty = sortProperty,
                    GetAllResultData = finalResult,
                    UserName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null,
                    Role = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null,
                });
            }

            return finalResult;
        }

        public async Task<string> Post(ClaimsPrincipal user, string controllerName, Dictionary<string, JsonNode> fieldValues)
        {
            Dictionary<string, JsonNode> values = new(fieldValues.Select(x => new KeyValuePair<string, JsonNode>(x.Key.ToLowerInvariant(), x.Value)));

            (Entity entity, string permissionsFilter) = await Authorize(user, controllerName, post: true);

            var action = this.attachedActionService.GetAttachedAction(controllerName, ActionType.Post, ActionOrderType.Before);
            if (action != null)
            {
                await action(new ActionData<T>()
                {
                    InputParameterData = JsonSerializer.Serialize(fieldValues),
                    UserName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null,
                    Role = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null,
                });
            }

            List<Property> properties = entity.Properties.Where(x => !x.IsKey && !x.IsReadOnly).OrderBy(x => x.PropertyName).ToList();

            var usedProperties = properties
                .Where(prop => values.ContainsKey((prop.ModelPropertyName ?? prop.PropertyName).ToLowerInvariant())
                    && (prop.ReferencingEntityId != null ||
                        (values[(prop.ModelPropertyName ?? prop.PropertyName).ToLowerInvariant()] as JsonValue) != null));

            StringBuilder queryBuilder = new();
            queryBuilder.AppendLine(@$"DECLARE @generated_keys table(id {dbTypeParsers[typeof(T)]});");
            queryBuilder.Append($"INSERT INTO {dbSchema}.{dbStructure.ColumnDelimiterLeft}{entity.TableName}{dbStructure.ColumnDelimiterRight} " +
                $"({string.Join(", ", usedProperties.Select(x => $"{dbStructure.ColumnDelimiterLeft}{x.PropertyName}{dbStructure.ColumnDelimiterRight}"))})");
            queryBuilder.Append($"OUTPUT inserted.{dbStructure.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}{dbStructure.ColumnDelimiterRight} INTO @generated_keys ");
            queryBuilder.AppendLine($"VALUES ({string.Join(", ", usedProperties.Select((a, b) => "@PropertyValue" + b.ToString()))})");

            queryBuilder.AppendLine(@"SELECT * FROM @generated_keys");

            SqlConnection connection = this.connection as SqlConnection;

            using DbCommand command = new SqlCommand(queryBuilder.ToString(), connection);
            int i = 0;
            List<Tuple<string, string>> errors = new();

            foreach (Property prop in properties.ToArray())
            {
                object actualValue = DBNull.Value;
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

                    command.Parameters.Add(new SqlParameter("PropertyValue" + i.ToString(), actualValue ?? DBNull.Value));

                    i++;
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
                if (connection.State != System.Data.ConnectionState.Open)
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
                        using DbCommand crossTableCommand = new SqlCommand(crossTableQuery, connection);
                        crossTableCommand.Transaction = transaction;
                        await crossTableCommand.ExecuteNonQueryAsync();
                    }

                    await sr.CloseAsync();
                    await transaction.CommitAsync();

                    var savedEntity = await Get(user, controllerName, newId);

                    var afterAction = this.attachedActionService.GetAttachedAction(controllerName, ActionType.Post, ActionOrderType.After);
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

        public async Task PostRelatedEntity(ClaimsPrincipal user, string controllerName, T id, string relatedEntityName, RelatedEntityObject relatedEntity)
        {
            (Entity entity, string permissionsFilter) = await Authorize(user, controllerName, post: true);

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
            queryBuilder.Append($"INSERT INTO {dbSchema}.{dbStructure.ColumnDelimiterLeft}{crossEntity.CrossTableName}{dbStructure.ColumnDelimiterRight} (");
            if (!string.IsNullOrEmpty(crossEntity.ValidFromColumnName))
            {
                queryBuilder.Append($"{dbStructure.ColumnDelimiterLeft}{crossEntity.ValidFromColumnName}{dbStructure.ColumnDelimiterRight}, ");
            }

            queryBuilder.Append($"{propertyFrom}, {propertyTo})");
            queryBuilder.Append($"VALUES (");

            if (!string.IsNullOrEmpty(crossEntity.ValidFromColumnName))
            {
                queryBuilder.Append($"GETDATE(), ");
            }

            queryBuilder.Append($"'{id}', '{relatedEntity.Id}')");

            SqlConnection connection = this.connection as SqlConnection;

            try
            {
                using DbCommand command = new SqlCommand(queryBuilder.ToString(), connection);
                if (connection.State != System.Data.ConnectionState.Open)
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

            (Entity entity, string permissionsFilter) = await Authorize(user, controllerName, put: true);

            var action = this.attachedActionService.GetAttachedAction(controllerName, ActionType.Patch, ActionOrderType.Before);
            if (action != null)
            {
                await action(new ActionData<T>()
                {
                    Id = id ?? (T)stringParsers[typeof(T)]((values[entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName.ToLowerInvariant() ?? "id"].ToString())),
                    InputParameterData = JsonSerializer.Serialize(fieldValues),
                    UserName = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst("id").Value : null,
                    Role = user.Identity.IsAuthenticated ? (user.Identity as ClaimsIdentity).FindFirst(ClaimsIdentity.DefaultRoleClaimType).Value : null,
                });
            }

            List<Property> properties = entity.Properties.Where(x => !x.IsKey && !x.IsReadOnly).OrderBy(x => x.PropertyName).ToList();

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

                    parameters.Add(new SqlParameter("PropertyValue" + i.ToString(), actualValue ?? DBNull.Value));

                    i++;
                }
            }

            StringBuilder queryBuilder = new();
            queryBuilder.Append($"UPDATE {dbSchema}.{dbStructure.ColumnDelimiterLeft}{entity.TableName}{dbStructure.ColumnDelimiterRight} " +
                $"SET {string.Join($", ", properties.Where(x => values.ContainsKey(x.CamelCaseName().ToLowerInvariant())).Select((x, i) => $"{dbStructure.ColumnDelimiterLeft}{x.PropertyName}{dbStructure.ColumnDelimiterRight}=@PropertyValue" + i.ToString()))}");
            queryBuilder.Append($" WHERE {entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}=@ID");

            if (entity.SoftDeleteColumn != null)
            {
                queryBuilder.Append($" AND ({dbStructure.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{dbStructure.ColumnDelimiterRight} IS NULL OR " +
                    $"{dbStructure.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{dbStructure.ColumnDelimiterRight} = 0)");
            }

            List<Tuple<string, object>> permissionsFilterParams = new();

            ComparerObject filterObjectWithPermissions = null;

            if (!string.IsNullOrEmpty(permissionsFilter))
            {
                filterObjectWithPermissions = JsonSerializer.Deserialize<ComparerObject>(permissionsFilter, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            }

            var filters = filterObjectWithPermissions?.ToSQLQuery(user, entity, dbSchema, 0, $"{dbSchema}.{dbStructure.ColumnDelimiterLeft}{entity.TableName}{dbStructure.ColumnDelimiterRight}", null);

            if (filters != null)
            {
                queryBuilder.Append($" AND {filters.Item1}");
            }

            SqlConnection connection = this.connection as SqlConnection;
            T id1;
            using DbCommand command = new SqlCommand(queryBuilder.ToString(), connection);
            if (id != null)
            {
                id1 = id;
                command.Parameters.Add(new SqlParameter("ID", id));
            }
            else
            {
                id1 = (T)stringParsers[typeof(T)](values[entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName.ToLowerInvariant() ?? "id"].ToString());
                command.Parameters.Add(new SqlParameter("ID", values[entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName.ToLowerInvariant() ?? "id"].ToString()));
            }

            if (filters != null)
            {
                command.Parameters.AddRange(filters.Item3.Select(x => new SqlParameter(x.Item1, x.Item2)).ToArray());
            }

            command.Parameters.AddRange(parameters.ToArray());

            if (errors.Count > 0)
            {
                throw new GenericBackendSecurityException(SecurityStatus.BadRequest, errors.Select(x => new { Error = x.Item1, Property = x.Item2 }));
            }

            if (connection.State != System.Data.ConnectionState.Open)
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
                using DbCommand crossTableCommand = new SqlCommand(crossTableQuery, connection);
                crossTableCommand.Transaction = transaction;
                await crossTableCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            var afterAction = this.attachedActionService.GetAttachedAction(controllerName, ActionType.Patch, ActionOrderType.After);
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
            (Entity entity, string permissionsFilter) = await Authorize(user, controllerName, delete: true);

            var action = this.attachedActionService.GetAttachedAction(controllerName, ActionType.Delete, ActionOrderType.Before);
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
            if ((await AggregateEntityAccess(user, entity, permissionsFilter, new ComparerObject() { Filter = id.ToString(), Property = entityKey }))[0] == "0")
            {
                throw new GenericBackendSecurityException(SecurityStatus.NotFound);
            }

            StringBuilder queryBuilder = new();
            if (entity.SoftDeleteColumn == null)
            {
                queryBuilder.Append($"DELETE FROM {dbSchema}.{dbStructure.ColumnDelimiterLeft}{entity.TableName}{dbStructure.ColumnDelimiterRight} ");
            }
            else
            {
                queryBuilder.Append($"UPDATE {dbSchema}.{dbStructure.ColumnDelimiterLeft}{entity.TableName}{dbStructure.ColumnDelimiterRight} SET {dbStructure.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{dbStructure.ColumnDelimiterRight} = 1 ");
            }

            queryBuilder.Append($"WHERE {dbStructure.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}{dbStructure.ColumnDelimiterRight}=@ID");

            SqlConnection connection = this.connection as SqlConnection;

            using DbCommand command = new SqlCommand(queryBuilder.ToString(), connection);
            command.Parameters.Add(new SqlParameter("ID", id));

            string oneEntry = await Get(user, controllerName, id);

            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            await command.ExecuteNonQueryAsync();

            var afterAction = this.attachedActionService.GetAttachedAction(controllerName, ActionType.Delete, ActionOrderType.After);
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
            (Entity entity, string permissionsFilter) = await Authorize(user, controllerName, delete: true);

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
                queryBuilder.Append($"DELETE FROM {dbSchema}.{dbStructure.ColumnDelimiterLeft}{crossEntity.CrossTableName}{dbStructure.ColumnDelimiterRight} ");
            }

            if (!string.IsNullOrEmpty(crossEntity.ActiveColumnName))
            {
                queryBuilder.Append($"UPDATE {dbSchema}.{dbStructure.ColumnDelimiterLeft}{crossEntity.CrossTableName}{dbStructure.ColumnDelimiterRight} SET {dbStructure.ColumnDelimiterLeft}{crossEntity.ActiveColumnName}{dbStructure.ColumnDelimiterRight} = 0");

                if (!string.IsNullOrEmpty(crossEntity.ValidToColumnName))
                {
                    queryBuilder.Append($", {dbStructure.ColumnDelimiterLeft}{crossEntity.ValidToColumnName}{dbStructure.ColumnDelimiterRight} = GETDATE()");
                }
            }
            else if (!string.IsNullOrEmpty(crossEntity.ValidToColumnName))
            {
                queryBuilder.Append($"UPDATE {dbSchema}.{dbStructure.ColumnDelimiterLeft}{crossEntity.CrossTableName}{dbStructure.ColumnDelimiterRight} SET {dbStructure.ColumnDelimiterLeft}{crossEntity.ValidToColumnName}{dbStructure.ColumnDelimiterRight} = GETDATE()");
            }

            queryBuilder.Append($" WHERE {dbStructure.ColumnDelimiterLeft}{propertyFrom}{dbStructure.ColumnDelimiterRight}='{id}' AND {dbStructure.ColumnDelimiterLeft}{propertyTo}{dbStructure.ColumnDelimiterRight}='{relatedEntityId}'");

            SqlConnection connection = this.connection as SqlConnection;

            using DbCommand command = new SqlCommand(queryBuilder.ToString(), connection);

            if (connection.State != System.Data.ConnectionState.Open)
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
                            qb2.Append($"UPDATE {dbSchema}.{dbStructure.ColumnDelimiterLeft}{crossEntity.CrossTableName}{dbStructure.ColumnDelimiterRight} SET ");
                            qb2.Append($"{dbStructure.ColumnDelimiterLeft}{crossEntity.ValidFromColumnName}{dbStructure.ColumnDelimiterRight} = GETDATE() ");
                        }
                        else if (!string.IsNullOrEmpty(crossEntity.ActiveColumnName))
                        {
                            qb2.Append($"UPDATE {dbSchema}.{dbStructure.ColumnDelimiterLeft}{crossEntity.CrossTableName}{dbStructure.ColumnDelimiterRight} SET ");
                            qb2.Append($"{dbStructure.ColumnDelimiterLeft}{crossEntity.ActiveColumnName}{dbStructure.ColumnDelimiterRight} = 0 ");
                        }
                        else
                        {
                            qb2.Append($"DELETE FROM {dbSchema}.{dbStructure.ColumnDelimiterLeft}{crossEntity.CrossTableName}{dbStructure.ColumnDelimiterRight} ");
                        }

                        qb2.AppendLine($"WHERE {dbStructure.ColumnDelimiterLeft}{entity1ReferencingColumnName}{dbStructure.ColumnDelimiterRight} = '{newId}'");

                        if (tmp1.Any())
                        {
                            qb2.Append($" AND {dbStructure.ColumnDelimiterLeft}{entity2ReferencingColumnName}{dbStructure.ColumnDelimiterRight} NOT IN ('{string.Join("', '", tmp1)}');");
                        }
                        else
                        {
                            qb2.Append(';');
                        }
                    }

                    foreach (Guid refId in tmp1)
                    {
                        qb2.AppendLine($@"IF NOT EXISTS(SELECT * FROM {dbStructure.ColumnDelimiterLeft}{crossEntity.CrossTableName}{dbStructure.ColumnDelimiterRight}
                           WHERE {dbStructure.ColumnDelimiterLeft}{entity1ReferencingColumnName}{dbStructure.ColumnDelimiterRight} = '{newId}'
                           AND {dbStructure.ColumnDelimiterLeft}{entity2ReferencingColumnName}{dbStructure.ColumnDelimiterRight} = '{refId}')");

                        qb2.Append($"INSERT INTO {dbSchema}.{dbStructure.ColumnDelimiterLeft}{crossEntity.CrossTableName}{dbStructure.ColumnDelimiterRight} (");
                        if (!string.IsNullOrEmpty(crossEntity.ValidFromColumnName))
                        {
                            qb2.Append($"{dbStructure.ColumnDelimiterLeft}{crossEntity.ValidFromColumnName}{dbStructure.ColumnDelimiterRight}, ");
                        }

                        qb2.Append($"{dbStructure.ColumnDelimiterLeft}{entity1ReferencingColumnName}{dbStructure.ColumnDelimiterRight}, {dbStructure.ColumnDelimiterLeft}{entity2ReferencingColumnName}{dbStructure.ColumnDelimiterRight})");
                        qb2.Append($"VALUES (");

                        if (!string.IsNullOrEmpty(crossEntity.ValidFromColumnName))
                        {
                            qb2.Append($"GETDATE(), ");
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
            string keyProperty = entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID";
            string orderByColumn = string.IsNullOrEmpty(sortProperty) ?
                null :
                entity.Properties.Where(x => x.CamelCaseName().ToLowerInvariant() == sortProperty.ToLowerInvariant()).Select(x => x.PropertyName).FirstOrDefault();
            orderByColumn ??= keyProperty;
            alteredQuery += $" ORDER BY {dbStructure.ColumnDelimiterLeft}{orderByColumn}{dbStructure.ColumnDelimiterRight} {(string.Equals(sortOrder, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC")}";
            return alteredQuery;
        }

        private static string AddPagingToQuery(string query, int page, int pageSize)
        {
            string alteredQuery = query;

            if (pageSize > 0)
            {
                alteredQuery += $" OFFSET {page * pageSize} ROWS FETCH NEXT {pageSize} ROWS ONLY";
            }

            return alteredQuery;
        }

        private async Task<string[]> AggregateEntityAccess(ClaimsPrincipal user, Entity entity, string permissionsFilter, ComparerObject filterObject = null, SummaryRequestObject[] summaries = null)
        {
            string countQuery = "";
            List<string> summaryNames = new();

            if (summaries == null)
            {
                countQuery = @$"SELECT COUNT(*) FROM {dbSchema}.{dbStructure.ColumnDelimiterLeft}{entity.TableName}{dbStructure.ColumnDelimiterRight} tab1";
            }
            else
            {
                countQuery = @$"SELECT {string.Join(", ", ValidateSummaries(entity, summaries, summaryNames))} FROM {dbSchema}.{dbStructure.ColumnDelimiterLeft}{entity.TableName}{dbStructure.ColumnDelimiterRight} tab1";
            }

            bool countWhereActivated = false;

            if (entity.SoftDeleteColumn != null)
            {
                countQuery += $" WHERE ({dbStructure.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{dbStructure.ColumnDelimiterRight} IS NULL OR {dbStructure.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{dbStructure.ColumnDelimiterRight} = 0)";
                countWhereActivated = true;
            }

            List<Tuple<string, object>> permissionsFilterParams = new();

            var filterObjectWithPermissions = filterObject;
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

            var filters = filterObjectWithPermissions?.ToSQLQuery(user, entity, dbSchema, 0, "tab1", null);

            if (filters != null)
            {
                countQuery += countWhereActivated ? " AND (" : " WHERE (";
                countQuery += filters.Item1;
                countQuery += ")";
            }

            SqlConnection connection = this.connection as SqlConnection;

            using SqlCommand command = new(countQuery, connection);
            if (filterObjectWithPermissions != null)
            {
                command.Parameters.AddRange(filters.Item3.Select(x => new SqlParameter(x.Item1, x.Item2)).ToArray());
            }

            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            if (summaries == null)
            {
                return new string[] { ((await command.ExecuteScalarAsync() as int?) ?? 0).ToString() };
            }
            else
            {
                List<string> result = new();
                using DbDataReader dr = await command.ExecuteReaderAsync();
                if (!(await dr.ReadAsync()))
                {
                    return null;
                }

                for (int i = 0; i < summaryNames.Count; i++)
                {
                    var tmp = summaryNames[i];
                    var res = JsonSerializer.Serialize(dr.GetValue(i));
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
                    comparison = summary.SummaryType switch
                    {
                        SummaryTypes.AVG => $"AVG({property.PropertyName})",
                        SummaryTypes.MIN => $"MIN({property.PropertyName})",
                        SummaryTypes.MAX => $"MAX({property.PropertyName})",
                        SummaryTypes.COUNT => $"COUNT({property.PropertyName})",
                        SummaryTypes.COUNT_DISTINCT => $"COUNT(DISTINCT {property.PropertyName})",
                        SummaryTypes.SUM => $"SUM({property.PropertyName})"
                    };

                    data.Add(comparison);
                    summaryNames.Add($@"{{ ""name"": ""{(property.ModelPropertyName ?? property.PropertyName).CamelCaseName()}"", ""summaryType"": ""{summary.SummaryType}"", ""value"": ");
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

        private string GenerateSelectQuery(Entity entity, List<Tuple<string, string>> properties, Dictionary<string, SelectPropertyData> joinData)
        {
            StringBuilder queryBuilder = new();
            int internalCounter = 1;

            var propertiesInternal = properties.Select(x => new Tuple<string, string>(x.Item1, x.Item2.IndexOf(".") > 0 ? x.Item2.Substring(0, x.Item2.IndexOf(".")) : x.Item2)).ToList();
            queryBuilder.AppendLine($"SELECT tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID"}{dbStructure.ColumnDelimiterRight} AS {entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName.CamelCaseName() ?? "id"} ");

            var filterData = GetJoinsForSelect(entity, null, $"tab{internalCounter--}", properties, joinData, null, ref internalCounter);

            foreach (var data in joinData)
            {
                foreach (var property in data.Value.Properties)
                {
                    queryBuilder.Append($", {dbStructure.ColumnDelimiterLeft}{data.Value.TableName}{dbStructure.ColumnDelimiterRight}.{dbStructure.ColumnDelimiterLeft}{property.Item3}{dbStructure.ColumnDelimiterRight} AS " +
                        $"{dbStructure.ColumnDelimiterLeft}{property.Item1}{dbStructure.ColumnDelimiterRight}");
                }
            }

            queryBuilder.AppendLine($" FROM {dbSchema}.{dbStructure.ColumnDelimiterLeft}{entity.TableName}{dbStructure.ColumnDelimiterRight} AS tab1 ");

            foreach (var data in joinData.Where(x => !string.IsNullOrEmpty(x.Value.JoinPropertyName)))
            {
                queryBuilder.Append($" LEFT JOIN {dbStructure.ColumnDelimiterLeft}{data.Value.OriginalTableName}{dbStructure.ColumnDelimiterRight} AS " +
                    $"{dbStructure.ColumnDelimiterLeft}{data.Value.TableName}{dbStructure.ColumnDelimiterRight} ON " +
                    $"{dbStructure.ColumnDelimiterLeft}{data.Value.TableName}{dbStructure.ColumnDelimiterRight}.{dbStructure.ColumnDelimiterLeft}{data.Value.IdPropertyName}{dbStructure.ColumnDelimiterRight} = " +
                    $"{dbStructure.ColumnDelimiterLeft}{data.Value.JoinTableName}{dbStructure.ColumnDelimiterRight}.{dbStructure.ColumnDelimiterLeft}{data.Value.JoinPropertyName}{dbStructure.ColumnDelimiterRight} ");
            }

            queryBuilder.AppendLine($"WHERE 1=1");

            if (entity.SoftDeleteColumn != null)
            {
                queryBuilder.AppendLine($@" AND (tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{dbStructure.ColumnDelimiterRight} IS NULL OR tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{dbStructure.ColumnDelimiterRight} = 0)");
            }

            return queryBuilder.ToString();
        }

        private string GenerateSelectQuery(Entity entity, ref int counter, string roleName, string userName, List<SqlParameter> parameters, string filterProperty = null, object filterValue = null)
        {
            string filter = AuthorizeSubentity(roleName, userName, entity);
            StringBuilder queryBuilder = new();
            int internalCounter = ++counter;

            queryBuilder.AppendLine($"SELECT tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID" }{dbStructure.ColumnDelimiterRight} AS {dbStructure.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName.CamelCaseName() ?? "id"}{dbStructure.ColumnDelimiterRight} ");

            foreach (Property property in entity.Properties.Where(x => !x.IsKey))
            {
                if (property.ReferencingEntityId == null)
                {
                    queryBuilder.Append($", tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{property.PropertyName}{dbStructure.ColumnDelimiterRight} AS {dbStructure.ColumnDelimiterLeft}{property.CamelCaseName()}{dbStructure.ColumnDelimiterRight}");
                }
                else if (string.IsNullOrEmpty(property.RelatedModelPropertyName))
                {
                    queryBuilder.Append($", (JSON_QUERY(({GenerateSelectQuery(property.ReferencingEntity, ref counter, roleName, userName, parameters, $"tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{property.PropertyName}{dbStructure.ColumnDelimiterRight}")}))) AS {dbStructure.ColumnDelimiterLeft}{property.CamelCaseName()}{dbStructure.ColumnDelimiterRight}");
                }
            }

            foreach (Property property in entity.ReferencingProperties.Where(x => !string.IsNullOrEmpty(x.RelatedModelPropertyName)))
            {
                queryBuilder.Append($", ({GenerateSelectQuery(property.Entity, ref counter, roleName, userName, parameters)} AND {dbStructure.ColumnDelimiterLeft}{property.PropertyName}{dbStructure.ColumnDelimiterRight} = tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID" }{dbStructure.ColumnDelimiterRight} FOR JSON AUTO, INCLUDE_NULL_VALUES) AS {dbStructure.ColumnDelimiterLeft}{property.RelatedModelPropertyName.CamelCaseName()}{dbStructure.ColumnDelimiterRight}");
            }

            foreach (EntityRelation relation in entity.EntityRelations1)
            {
                if (!relation.ShowInEntity1)
                {
                    continue;
                }

                string whereQueryAddition = "";
                if (!string.IsNullOrEmpty(relation.ActiveColumnName))
                {
                    whereQueryAddition = $@" AND {dbStructure.ColumnDelimiterLeft}{relation.ActiveColumnName}{dbStructure.ColumnDelimiterRight} = 1 ";
                }

                if (!string.IsNullOrEmpty(relation.ValidFromColumnName))
                {
                    whereQueryAddition = $@" AND ({dbStructure.ColumnDelimiterLeft}{relation.ValidFromColumnName}{dbStructure.ColumnDelimiterRight} IS NULL OR {dbStructure.ColumnDelimiterLeft}{relation.ValidFromColumnName}{dbStructure.ColumnDelimiterRight} <= GETDATE()) ";
                }

                if (!string.IsNullOrEmpty(relation.ValidToColumnName))
                {
                    whereQueryAddition += $@" AND ({dbStructure.ColumnDelimiterLeft}{relation.ValidToColumnName}{dbStructure.ColumnDelimiterRight} IS NULL OR {dbStructure.ColumnDelimiterLeft}{relation.ValidToColumnName}{dbStructure.ColumnDelimiterRight} >= GETDATE()) ";
                }

                queryBuilder.Append($", ({GenerateSelectQuery(relation.Entity2, ref counter, roleName, userName, parameters)} AND {dbStructure.ColumnDelimiterLeft}{relation.Entity2.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID" }{dbStructure.ColumnDelimiterRight} IN (SELECT {dbStructure.ColumnDelimiterLeft}{relation.Entity2ReferencingColumnName}{dbStructure.ColumnDelimiterRight} FROM " +
                    $"{dbSchema}.{dbStructure.ColumnDelimiterLeft}{relation.CrossTableName}{dbStructure.ColumnDelimiterRight} WHERE {dbStructure.ColumnDelimiterLeft}{relation.Entity1ReferencingColumnName}{dbStructure.ColumnDelimiterRight} = tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID" }{dbStructure.ColumnDelimiterRight} {whereQueryAddition}) " +
                    $"FOR JSON AUTO, INCLUDE_NULL_VALUES) AS {dbStructure.ColumnDelimiterLeft}{relation.Entity1PropertyName.CamelCaseName()}{dbStructure.ColumnDelimiterRight}");
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

                string whereQueryAddition = "";
                if (!string.IsNullOrEmpty(relation.ActiveColumnName))
                {
                    whereQueryAddition = $@" AND {dbStructure.ColumnDelimiterLeft}{relation.ActiveColumnName}{dbStructure.ColumnDelimiterRight} = 1 ";
                }

                if (!string.IsNullOrEmpty(relation.ValidFromColumnName))
                {
                    whereQueryAddition = $@" AND ({dbStructure.ColumnDelimiterLeft}{relation.ValidFromColumnName}{dbStructure.ColumnDelimiterRight} IS NULL OR {dbStructure.ColumnDelimiterLeft}{relation.ValidFromColumnName}{dbStructure.ColumnDelimiterRight} <= GETDATE()) ";
                }

                if (!string.IsNullOrEmpty(relation.ValidToColumnName))
                {
                    whereQueryAddition += $@" AND ({dbStructure.ColumnDelimiterLeft}{relation.ValidToColumnName}{dbStructure.ColumnDelimiterRight} IS NULL OR {dbStructure.ColumnDelimiterLeft}{relation.ValidToColumnName}{dbStructure.ColumnDelimiterRight} >= GETDATE()) ";
                }

                queryBuilder.Append($", ({GenerateSelectQuery(relation.Entity1, ref counter, roleName, userName, parameters)} AND " +
                    $"{dbStructure.ColumnDelimiterLeft}{relation.Entity1.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID" }{dbStructure.ColumnDelimiterRight} IN " +
                    $"(SELECT {dbStructure.ColumnDelimiterLeft}{relation.Entity1ReferencingColumnName}{dbStructure.ColumnDelimiterRight} FROM {dbSchema}.{dbStructure.ColumnDelimiterLeft}{relation.CrossTableName}{dbStructure.ColumnDelimiterRight} " +
                    $"WHERE {dbStructure.ColumnDelimiterLeft}{relation.Entity2ReferencingColumnName}{dbStructure.ColumnDelimiterRight} = tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID" }{dbStructure.ColumnDelimiterRight} {whereQueryAddition}) FOR JSON AUTO, INCLUDE_NULL_VALUES) " +
                    $"AS {dbStructure.ColumnDelimiterLeft}{relation.Entity2PropertyName.CamelCaseName()}{dbStructure.ColumnDelimiterRight}");
            }

            queryBuilder.AppendLine($" FROM {dbSchema}.{dbStructure.ColumnDelimiterLeft}{entity.TableName}{dbStructure.ColumnDelimiterRight} AS tab{internalCounter} ");

            if (filterProperty != null)
            {
                queryBuilder.AppendLine($"WHERE tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID" }{dbStructure.ColumnDelimiterRight}={filterProperty}");
            }
            else if (filterValue != null)
            {
                queryBuilder.AppendLine($"WHERE tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "ID" }{dbStructure.ColumnDelimiterRight}=@tab{internalCounter}_val");
            }
            else
            {
                queryBuilder.AppendLine($"WHERE 1=1");
            }

            if (entity.SoftDeleteColumn != null)
            {
                queryBuilder.AppendLine($@" AND (tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{dbStructure.ColumnDelimiterRight} IS NULL OR tab{internalCounter}.{dbStructure.ColumnDelimiterLeft}{entity.SoftDeleteColumn}{dbStructure.ColumnDelimiterRight} = 0)");
            }

            if (!string.IsNullOrEmpty(filter))
            {
                var permissionFilterObject = JsonSerializer.Deserialize<ComparerObject>(filter, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                var filters = permissionFilterObject?.ToSQLQuery(userName, entity, dbSchema, parameters.Count, $"tab{internalCounter}", null);

                if (filters != null)
                {
                    queryBuilder.Append(" AND (");
                    queryBuilder.Append(filters.Item1);
                    queryBuilder.Append(")");

                    parameters.AddRange(filters.Item3.Select(x => new SqlParameter(x.Item1, x.Item2)).ToArray());
                }
            }

            if (filterProperty != null && filterValue == null)
            {
                queryBuilder.AppendLine($" FOR JSON AUTO, INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER");
            }

            return queryBuilder.ToString();
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

        private async Task<Tuple<Entity, string>> Authorize(
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
                    .OrderBy(x => (getAll || getOne) ? x.ViewFilter : x.EditFilter)
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

            string permissionsFilter = (getAll || getOne) ? er?.ViewFilter : er?.EditFilter;

            if (!string.IsNullOrEmpty(permissionsFilter))
            {
                permissionsFilter = permissionsFilter.Replace("$user", userId.Replace("\"", "\\\""));
                permissionsFilter = permissionsFilter.Replace("$role", roleName.Replace("\"", "\\\""));
            }

            return new Tuple<Entity, string>(entity, permissionsFilter);
        }

        private string AuthorizeSubentity(string roleName, string userId, Entity entity)
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

    public class RelatedEntityObject
    {
        public Guid Id { get; set; }
    }

    public record SelectPropertyData
    {
        public string? JoinTableName { get; set; }
        public string? JoinPropertyName { get; set; }
        public string? OriginalTableName { get; set; }
        public string? TableName { get; set; }
        public string? IdPropertyName { get; set; }
        public string? TableDTOName { get; set; }
        public List<Tuple<string, string, string>>? Properties { get; set; }
    }
}
