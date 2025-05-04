using System.Text;
using System.Text.Json;
using BeGeneric.Backend.Common;
using BeGeneric.Backend.Common.Exceptions;
using BeGeneric.Backend.Common.Helpers;
using BeGeneric.Backend.Common.Models;

namespace BeGeneric.Backend.Database;

public class ComparerObject : ComparerObjectGroup, IComparerObject
{
    public string? Operator { get; set; }
    public string? Property { get; set; }
    public object? Filter { get; set; }

    public override Tuple<string, int, List<Tuple<string, object>>> ToSQLQuery(string userName, Entity entity, string dbSchema, int counter, string originTableAlias, Dictionary<string, SelectPropertyData> joinData)
    {
        if (Comparisons != null)
        {
            return base.ToSQLQuery(userName, entity, dbSchema, counter, originTableAlias, joinData);
        }

        List<Tuple<string, object>> parameters = new();
        bool includeAny = Property.IndexOf(".") > -1;
        string operation = ResolveOperator(includeAny);

        if (includeAny && !operation.StartsWith("$filterParam"))
        {
            operation = "EXISTS (" + ResolvePropertyName(entity, originTableAlias, operation) + ")";
        }
        else
        {
            operation = operation.Replace("$property", ResolvePropertyName(entity, dbSchema, originTableAlias));
        }

        if (operation.Contains("$filterParam"))
        {
            operation = operation.Replace("$filterParam", $@"@Filter_Int{counter}");

            if (Filter is JsonElement jsonFilter)
            {
                Filter = jsonFilter.ToString();
            }

            if (Filter is string strFilter && string.Equals(strFilter, "$user", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(userName))
                {
                    Filter = userName;
                }
                else
                {
                    Filter = DBNull.Value;
                }
            }

            parameters.Add(new Tuple<string, object>($@"Filter_Int{counter++}", Filter));
        }

        return new Tuple<string, int, List<Tuple<string, object>>>(operation, counter, parameters);
    }

    public static Tuple<string, int, List<Tuple<string, object>>> ToGroupSQLQuery(List<IComparerObject> comparers, Entity entity, string dbSchema, int counter, string originTableAlias)
    {
        List<Tuple<string, object>> parameters = new();

        string operation = null;

        StringBuilder sb = new("(");
        bool isFirst = true;
        int internal_counter = 0;
        foreach (string word in comparers[0].Filter.ToString().Split(" ").Where(x => !string.IsNullOrEmpty(x)))
        {
            if (!isFirst)
            {
                sb.Append(" AND ");
            }
            else
            {
                isFirst = false;
            }

            bool isFirstInBatch = true;
            sb.Append('(');

            foreach (var comparer in comparers)
            {
                string propertyName = comparer.ResolvePropertyName(entity, dbSchema, originTableAlias);
                if (!isFirstInBatch)
                {
                    sb.Append($@" OR ");
                }
                else
                {
                    isFirstInBatch = false;
                }

                sb.Append($@"{propertyName} LIKE @Filter_Int{counter}_{parameters.Count} ");
                parameters.Add(new Tuple<string, object>($@"Filter_Int{counter++}_{parameters.Count}", "%" + word.Trim() + "%"));
            }

            sb.Append(')');
        }

        sb.Append(')');
        operation = sb.ToString();

        return new Tuple<string, int, List<Tuple<string, object>>>(operation, counter, parameters);
    }

    public string ResolvePropertyName(Entity entity, string dbSchema, string originTableAlias, string includedFilter = null, Dictionary<string, SelectPropertyData> joinData = null)
    {
        var properties = Property.Split(".");

        if (properties.Length == 1)
        {
            Property property = entity.Properties.FirstOrDefault(x => x.CamelCaseName().ToLowerInvariant() == properties[0].ToLowerInvariant());
            return property == null
                ? throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Search filter is not valid")
                : property.PropertyName;
        }
        else if (joinData != null && joinData.Any(x => string.Equals(x.Value.TableDTOName, string.Join(".", properties[..^1]), StringComparison.OrdinalIgnoreCase)))
        {
            var data = joinData.First(x => string.Equals(x.Value.TableDTOName, string.Join(".", properties[..^1]), StringComparison.OrdinalIgnoreCase)).Value;
            return $"[{data.TableName}].[{data.Properties.First(x => string.Equals(properties[^1], x.Item2, StringComparison.OrdinalIgnoreCase)).Item3}]";
        }
        else
        {
            StringBuilder sb = new();
            sb.Append($@"(SELECT fil_tab{properties.Length - 2}.{properties[^1]} FROM ");

            Entity iterationEntity = entity;
            string firstKey = "";
            string secondKey = "";
            string secondKeyTabName = "fil_tab0";

            for (int i = 0; i < properties.Length - 1; i++)
            {
                string prop = properties[i].ToLowerInvariant();
                Property property = iterationEntity.Properties.FirstOrDefault(x => x.CamelCaseName().ToLowerInvariant() == prop);
                bool referencingProperty = false;

                if (property == null)
                {
                    property = iterationEntity.ReferencingProperties.Where(x => string.Equals(x.RelatedModelPropertyName ??
                        x.Entity.CamelCaseName() + "_" + x.CamelCaseName()
                        , prop, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    referencingProperty = property != null;
                }

                if (property == null)
                {
                    EntityRelation crossEntity = iterationEntity.EntityRelations1.FirstOrDefault(x => x.Entity1PropertyName.ToLowerInvariant() == prop);
                    Entity entity1 = crossEntity?.Entity1;
                    string propertyFrom = crossEntity?.Entity1ReferencingColumnName;
                    string propertyTo = crossEntity?.Entity2ReferencingColumnName;

                    if (crossEntity == null)
                    {
                        crossEntity = iterationEntity.EntityRelations2.FirstOrDefault(x => x.Entity2PropertyName.ToLowerInvariant() == prop);
                        entity1 = crossEntity?.Entity2;
                        propertyFrom = crossEntity?.Entity2ReferencingColumnName;
                        propertyTo = crossEntity?.Entity1ReferencingColumnName;
                    }

                    if (crossEntity == null)
                    {
                        throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Search filter is not valid");
                    }

                    if (i > 0)
                    {
                        sb.Append(" INNER JOIN ");
                    }
                    else
                    {
                        secondKeyTabName = "fil_cross_tab0";
                        secondKey = propertyFrom;
                    }

                    sb.Append($@"{dbSchema}.[{crossEntity.CrossTableName}] AS fil_cross_tab{i} ");

                    if (i > 0)
                    {
                        string originalKey = iterationEntity.Properties.FirstOrDefault(x => x.IsKey).PropertyName ?? "Id";
                        sb.Append($@" ON fil_tab{i - 1}.[{originalKey}]=fil_cross_tab{i}.[{propertyFrom}]");
                    }
                    else
                    {
                        firstKey = entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "Id";
                    }

                    iterationEntity = crossEntity.Entity1.EntityId != entity1.EntityId ? crossEntity.Entity1 : crossEntity.Entity2;

                    sb.Append($@" INNER JOIN ");
                    sb.Append($@"{dbSchema}.[{iterationEntity.TableName}] AS fil_tab{i} ");

                    string key = iterationEntity.Properties.FirstOrDefault(x => x.IsKey).PropertyName ?? "Id";
                    sb.Append($@" ON fil_cross_tab{i}.[{propertyTo}]=fil_tab{i}.{key}");

                    continue;
                }

                if (i > 0)
                {
                    sb.Append(" INNER JOIN ");
                }
                else
                {
                    firstKey = entity.Properties.FirstOrDefault(x => x.IsKey)?.PropertyName ?? "Id";
                }

                if (!referencingProperty && property.ReferencingEntity != null)
                {
                    iterationEntity = property.ReferencingEntity;
                    sb.Append($@"{dbSchema}.[{iterationEntity.TableName}]");
                    sb.Append($@" AS fil_tab{i}");

                    if (i > 0)
                    {
                        string key = iterationEntity.Properties.FirstOrDefault(x => x.IsKey).PropertyName ?? "Id";
                        sb.Append($@" ON fil_tab{i - 1}.{property.PropertyName}=fil_tab{i}.{key}");
                    }
                    else
                    {
                        secondKey = iterationEntity.Properties.FirstOrDefault(x => x.IsKey).PropertyName ?? "Id";
                    }
                }
                else if (referencingProperty)
                {
                    iterationEntity = property.Entity;
                    sb.Append($@"{dbSchema}.[{iterationEntity.TableName}]");
                    sb.Append($@" AS fil_tab{i}");

                    if (i > 0)
                    {
                        string key = iterationEntity.Properties.FirstOrDefault(x => x.IsKey).PropertyName ?? "Id";
                        sb.Append($@" ON fil_tab{i - 1}.{property.PropertyName}=fil_tab{i}.{key}");
                    }
                    else
                    {
                        secondKey = property.PropertyName;
                    }
                }
                else
                {
                    throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Search filter is not valid");
                }
            }

            sb.Append(" WHERE ");

            // IMPORTAINT!!! tab1 is always the table belonging to target entity !!!IMPORTANT
            sb.Append($"{secondKeyTabName}.{secondKey} = {originTableAlias}.{firstKey}");

            if (!string.IsNullOrEmpty(includedFilter))
            {
                sb.Append($" AND {includedFilter.Replace("$property", $"fil_tab{properties.Length - 2}.[{properties[^1]}]")})");
            }
            else
            {
                sb.Append($")");
            }

            return sb.ToString();
        }
    }

    private string ResolveOperator(bool includeAny)
    {
        string any = includeAny ? "ANY " : string.Empty;
        return Operator?.ToLowerInvariant() switch
        {
            null => $"$filterParam = {any}$property",
            "eq" => $"$filterParam = {any}$property",
            "neq" => $"$filterParam != {any}$property",
            "lte" => $"$filterParam >= {any}$property",
            "gte" => $"$filterParam <= {any}$property",
            "lt" => $"$filterParam > {any}$property",
            "gt" => $"$filterParam < {any}$property",
            "not null" => $"$property IS NOT NULL",
            "null" => $"$property IS NULL",
            "contains" => "$property LIKE '%' + $filterParam + '%'",
            "startswith" => "$property LIKE $filterParam + '%'",
            "endswith" => "$property LIKE '%' + $filterParam",
            _ => "1 = 1",
        };
    }
}
