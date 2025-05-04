using System.Data.Common;

namespace BeGeneric.Backend.Common;

public interface IDatabaseStructureService
{
    Type GetFieldType(string fieldName, string tableName);

    string[] GetEnumValues(string fieldName, string tableName);

    DatabaseFieldSizeLimitation GetFieldSizeLimitation(string fieldName, string tableName);
    bool GetFieldNullable(string propertyName, string tableName);

    string GetRegexValues(string fieldName, string tableName);

    string ColumnDelimiterLeft { get; }

    string ColumnDelimiterRight { get; }

    string StringDelimiter { get; }

    string DataSchema { get; }

    DbCommand GetDbCommand(string commandText, DbConnection connection);

    DbCommand GetDbCommand(string commandText, DbConnection connection, DbTransaction transaction);

    DbParameter GetDbParameter<T>(string parameterName, T value);
}

public class DatabaseFieldSizeLimitation
{
    public int? Min { get; set; }
    public int? Max { get; set; }
}
