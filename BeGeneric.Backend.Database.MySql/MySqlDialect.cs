namespace BeGeneric.Backend.Database.MySql;

public class MySqlDialect : ISqlDialect
{
    public string AddPagingToQuery(string query, int page, int pageSize)
    {
        if (page < 0 || pageSize < 1)
            return query;

        int offset = page * pageSize;
        return $"{query} LIMIT {pageSize} OFFSET {offset}";
    }

    public string GetJsonPropertyNavigation(string[] sortTable, string dbColumnName)
    {
        if (sortTable.Length < 2)
            throw new ArgumentException("JSON property navigation requires at least two elements", nameof(sortTable));

        var jsonPath = "$." + string.Join(".", sortTable.Skip(1));
        return $"JSON_UNQUOTE(JSON_EXTRACT({sortTable[0]}, '{jsonPath}'))";
    }

    public string GetInsertReturningId<T>(string tableName, string schemaName, string keyColumn, IEnumerable<string> insertColumns, IEnumerable<string> valuePlaceholders)
    {
        return $@"
INSERT INTO `{schemaName}`.`{tableName}` ({string.Join(", ", insertColumns.Select(col => $"`{col}`"))})
VALUES ({string.Join(", ", valuePlaceholders.Select(x => $"@{x}"))})
RETURNING `{keyColumn}`;";
    }

    public string GetInsertIfNotExists(string tableName, string column1, string value1, string column2, string value2, string? validFromColumn = null)
    {
        var columns = new List<string>();
        var values = new List<string>();

        if (!string.IsNullOrEmpty(validFromColumn))
        {
            columns.Add($"`{validFromColumn}`");
            values.Add(GetCurrentTimestamp);
        }

        columns.Add($"`{column1}`");
        values.Add($"'{value1}'");

        columns.Add($"`{column2}`");
        values.Add($"'{value2}'");

        return $@"
INSERT INTO `{tableName}` ({string.Join(", ", columns)})
SELECT {string.Join(", ", values)}
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM `{tableName}`
    WHERE `{column1}` = '{value1}' AND `{column2}` = '{value2}'
);";
    }

    public string GetBasicSelectQuery(IList<string> columnNames, IList<string> columnValues, bool wrapInJson = false)
    {
        if (!wrapInJson)
        {
            return $"SELECT {string.Join(", ", columnValues.Select((x, i) => $"{x} AS {columnNames[i]}"))} ";
        }

        return @$"SELECT JSON_ARRAYAGG(
    JSON_OBJECT(
        {string.Join(", ", columnValues.Select((x, i) => $"{x}, {columnNames[i]}"))}
    )
)";
    }

    public string GetBasicSelectQuery(IList<string> columnNames, IList<string> columnValues, IList<string> columnPaths, IList<string> outputPaths, bool wrapInJson = false)
    {
        /*
        var columnNames = new List<string> { "Id", "Title", "CategoryId", "CategoryName" };
        var columnValues = new List<string> { "t.Id", "t.Title", "c.Id", "c.Name" };
        var columnPaths = new List<string> { "id", "title", "id", "name" };
        var outputPaths = new List<string> { "", "", "category", "category" };
         */

        if (columnNames.Count != columnValues.Count ||
            columnNames.Count != columnPaths.Count ||
            columnNames.Count != outputPaths.Count)
        {
            throw new ArgumentException("All input lists must have the same length.");
        }

        if (!wrapInJson)
        {
            var selectClauses = new List<string>();

            for (int i = 0; i < columnNames.Count; i++)
            {
                string alias = string.IsNullOrWhiteSpace(outputPaths[i])
                    ? columnPaths[i]
                    : $"{outputPaths[i]}_{columnPaths[i]}"; // Flattened alias for SQL column naming
                selectClauses.Add($"{columnValues[i]} AS {alias}");
            }

            return $"SELECT {string.Join(", ", selectClauses)}";
        }
        else
        {
            var rootFields = new Dictionary<string, List<(string Key, string Value)>>();

            for (int i = 0; i < columnNames.Count; i++)
            {
                string parent = outputPaths[i]?.Trim() ?? "";
                if (!rootFields.ContainsKey(parent))
                {
                    rootFields[parent] = new List<(string, string)>();
                }

                rootFields[parent].Add((columnPaths[i], columnValues[i]));
            }

            var jsonFields = new List<string>();

            foreach (var group in rootFields)
            {
                if (string.IsNullOrEmpty(group.Key))
                {
                    foreach (var (key, val) in group.Value)
                    {
                        jsonFields.Add($"'{key}', {val}");
                    }
                }
                else
                {
                    var nestedFields = string.Join(", ", group.Value.Select(f => $"'{f.Key}', {f.Value}"));
                    jsonFields.Add($"'{group.Key}', JSON_OBJECT({nestedFields})");
                }
            }

            string jsonObject = $"JSON_OBJECT({string.Join(", ", jsonFields)})";
            return $"SELECT JSON_ARRAYAGG({jsonObject})";
        }
    }

    public string ColumnDelimiterLeft => "`";
    public string ColumnDelimiterRight => "`";
    public string StringDelimiter => "'";
    public string GetCurrentTimestamp => "UTC_TIMESTAMP()";

    public string GetJsonColumn(string columnAlias) => $"JSON_OBJECT({columnAlias})";

    public string WrapIntoJson(string baseQuery, bool auto, bool includeNulls = false, bool withoutWrapper = false)
    {
        // MySQL does not support FOR JSON, use JSON_ARRAYAGG or JSON_OBJECT functions in SELECT query instead
        return baseQuery;
    }
}
