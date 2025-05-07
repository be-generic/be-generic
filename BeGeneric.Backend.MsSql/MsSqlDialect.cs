namespace BeGeneric.Backend.Database.MsSql;

public class MsSqlDialect: ISqlDialect
{
    private readonly Dictionary<Type, string> dbTypeParsers = new()
    {
        { typeof(int), "INT" },
        { typeof(Guid), "UNIQUEIDENTIFIER" },
        { typeof(string), "NVARCHAR(100)" },
    };

    public string AddPagingToQuery(string query, int page, int pageSize)
    {
        if (page < 0 || pageSize < 1)
            return query;

        return $"{query} OFFSET {page * pageSize} ROWS FETCH NEXT {pageSize} ROWS ONLY";
    }

    public string GetJsonPropertyNavigation(string[] sortTable, string dbColumnName)
    {
        if (sortTable.Length < 2)
            throw new ArgumentException("JSON property navigation requires at least two elements", nameof(sortTable));

        var jsonPath = string.Join(".", sortTable.Skip(1).Select(x => x));
        return $"JSON_VALUE([{sortTable[0]}], '$.{jsonPath}')";
    }

    public string GetInsertReturningId<T>(string tableName, string schemaName, string keyColumn, IEnumerable<string> insertColumns, IEnumerable<string> valuePlaceholders)
    {
        return $@"
DECLARE @generated_keys TABLE({keyColumn} {dbTypeParsers[typeof(T)]});
INSERT INTO [{schemaName}].[{tableName}] ({string.Join(", ", insertColumns)})
OUTPUT inserted.[{keyColumn}] INTO @generated_keys
VALUES ({string.Join(", ", valuePlaceholders.Select(x => $"@{x}"))});
SELECT * FROM @generated_keys;";
    }

    public string GetInsertIfNotExists(string tableName, string column1, string value1, string column2, string value2, string? validFromColumn = null)
    {
        var columns = new List<string>();
        var values = new List<string>();

        if (!string.IsNullOrEmpty(validFromColumn))
        {
            columns.Add($"[{validFromColumn}]");
            values.Add(GetCurrentTimestamp);
        }

        columns.Add($"[{column1}]");
        values.Add($"'{value1}'");

        columns.Add($"[{column2}]");
        values.Add($"'{value2}'");

        return $@"
IF NOT EXISTS (
    SELECT 1 FROM [{tableName}]
    WHERE [{column1}] = '{value1}' AND [{column2}] = '{value2}'
)
INSERT INTO [{tableName}] ({string.Join(", ", columns)})
VALUES ({string.Join(", ", values)});
";
    }

    public string GetBasicSelectQuery(IList<string> columnNames, IList<string> columnValues, bool wrapInJson = false)
    {
        return $"SELECT {string.Join(", ", columnValues.Select((x, i) => $"{x} AS {columnNames[i]}"))} ";
    }

    public string GetBasicSelectQuery(IList<string> columnNames, IList<string> columnValues, IList<string> columnPaths, IList<string> outputPaths, bool wrapInJson = false)
    {
        if (columnNames.Count != columnValues.Count ||
            columnNames.Count != columnPaths.Count ||
            columnNames.Count != outputPaths.Count)
        {
            throw new ArgumentException("All input lists must have the same length.");
        }

        var selectClauses = new List<string>();

        for (int i = 0; i < columnNames.Count; i++)
        {
            string alias = !wrapInJson
                ? columnPaths[i]  // flat key for plain SELECT
                : string.IsNullOrWhiteSpace(outputPaths[i])
                    ? columnPaths[i]
                    : $"{outputPaths[i]}.{columnPaths[i]}";  // nested JSON path for FOR JSON PATH

            selectClauses.Add($"{columnValues[i]} AS [{alias}]");
        }

        string selectClause = string.Join(", ", selectClauses);

        string query = $@"SELECT {selectClause} ";

        return query;
    }


    public string ColumnDelimiterLeft => "[";
    public string ColumnDelimiterRight => "]";
    public string StringDelimiter => "'";
    public string GetCurrentTimestamp => "GETUTCDATE()";

    public string GetJsonColumn(string columnAlias) => $"JSON_QUERY({columnAlias})";
    public string WrapIntoJson(string baseQuery, bool auto, bool includeNulls = false, bool withoutWrapper = false) => $"{baseQuery} FOR JSON PATH {(includeNulls ? ", INCLUDE_NULL_VALUES" : "")}{(withoutWrapper ? ", WITHOUT_ARRAY_WRAPPER" : "")}";
}
