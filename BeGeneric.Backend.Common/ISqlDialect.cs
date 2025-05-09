namespace BeGeneric.Backend.Common;

public interface ISqlDialect
{
    string ColumnDelimiterLeft { get; }
    string ColumnDelimiterRight { get; }
    string StringDelimiter { get; }
    string GetCurrentTimestamp { get; }

    string AddPagingToQuery(string query, int page, int pageSize);
    string GetJsonColumn(string columnAlias);
    string WrapIntoJson(string baseQuery, bool auto, bool includeNulls = false, bool withoutWrapper = false);
    string GetJsonPropertyNavigation(string[] sortTable, string dbColumnName);
    string GetInsertReturningId<T>(string tableName, string schemaName, string keyColumn, IEnumerable<string> insertColumns, IEnumerable<string> valuePlaceholders);
    string GetInsertIfNotExists(string tableName, string column1, string value1, string column2, string value2, string? validFromColumn = null);

    string GetBasicSelectQuery(IList<string> columnNames, IList<string> columnValues, IList<string> columnPaths, IList<string> outputPaths, bool wrapInJson = false);
}