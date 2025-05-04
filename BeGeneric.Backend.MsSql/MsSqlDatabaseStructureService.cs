using BeGeneric.Backend.Common;
using BeGeneric.Backend.Common.Models;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace BeGeneric.Backend.Database.MsSql;

public class MsSqlDatabaseStructureService : IDatabaseStructureService
{
    private readonly Dictionary<string, Dictionary<string, DatabaseFieldData>> _fieldData = new();

    public MsSqlDatabaseStructureService(string connectionString, string dbSchema, List<ColumnMetadataDefinition> metadata)
    {
        DataSchema = dbSchema;

        using SqlConnection conn = new(connectionString);
        using SqlCommand command = new(@$"
SELECT c.COLUMN_NAME, c.TABLE_NAME, c.DATA_TYPE, c.IS_NULLABLE, c.CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE TABLE_SCHEMA = @Schema
", conn);

        command.Parameters.AddWithValue("@Schema", dbSchema);

        conn.Open();

        var reader = command.ExecuteReader();

        while (reader.Read())
        {
            string colName = reader.GetFieldValue<string>(0);
            string tabName = reader.GetFieldValue<string>(1);
            string type = reader.GetFieldValue<string>(2);
            int? maxLength = reader.IsDBNull(4) ? null : reader.GetFieldValue<int>(4);

            var meta = metadata.FirstOrDefault(x => string.Equals(x.TableName, tabName, StringComparison.OrdinalIgnoreCase) &&
                                                    string.Equals(x.ColumnName, colName, StringComparison.OrdinalIgnoreCase));

            bool nullable = reader.GetFieldValue<string>(3) == "YES" && !(meta?.IsRequired ?? false);

            string allowedValueList = meta?.AllowedValues;
            string regex = meta?.Regex;

            if (!_fieldData.ContainsKey(tabName))
            {
                _fieldData.Add(tabName, new Dictionary<string, DatabaseFieldData>());
            }

            _fieldData[tabName].Add(colName, new DatabaseFieldData()
            {
                IsNullable = nullable,
                MaxLength = maxLength,
                MinLength = null,
                AllowedValues = allowedValueList?.Split(','),
                Regex = regex,
                FieldType = type
            });
        }
    }

    public string[] GetEnumValues(string fieldName, string tableName)
    {
        DatabaseFieldData field = GetField(fieldName, tableName);
        return field?.AllowedValues;
    }

    public bool GetFieldNullable(string fieldName, string tableName)
    {
        DatabaseFieldData field = GetField(fieldName, tableName);
        return field?.IsNullable ?? true;
    }

    public DatabaseFieldSizeLimitation GetFieldSizeLimitation(string fieldName, string tableName)
    {
        DatabaseFieldData field = GetField(fieldName, tableName);
        return new()
        {
            Min = field?.MinLength,
            Max = field?.MaxLength,
        };
    }

    public Type GetFieldType(string fieldName, string tableName)
    {
        DatabaseFieldData field = GetField(fieldName, tableName);

        return field.FieldType switch
        {
            "nvarchar" or "varchar" or "nchar" or "char" => typeof(string),
            "uniqueidentifier" => typeof(Guid),
            "bit" => typeof(bool),
            "int" => typeof(int),
            "decimal" => typeof(decimal),
            "double" => typeof(double),
            "float" or "single" => typeof(float),
            "date" or "datetime" or "datetime2" => typeof(DateTime),
            _ => typeof(object),
        };
    }

    public string GetRegexValues(string fieldName, string tableName)
    {
        DatabaseFieldData field = GetField(fieldName, tableName);
        return field.Regex;
    }

    public string DataSchema { get; internal set; }

    public string ColumnDelimiterLeft => "[";

    public string ColumnDelimiterRight => "]";

    public string StringDelimiter => "'";

    private DatabaseFieldData GetField(string fieldName, string tableName)
    {
        if (_fieldData.TryGetValue(tableName, out Dictionary<string, DatabaseFieldData> fieldData))
        {
            if (fieldData.TryGetValue(fieldName, out DatabaseFieldData actualFieldData))
            {
                return actualFieldData;
            }
        }

        return null;
    }

    public DbCommand GetDbCommand(string commandText, DbConnection connection)
    {
        return new SqlCommand(commandText, connection as SqlConnection);
    }

    public DbCommand GetDbCommand(string commandText, DbConnection connection, DbTransaction transaction)
    {
        return new SqlCommand(commandText, connection as SqlConnection, transaction as SqlTransaction);
    }

    public DbParameter GetDbParameter<T>(string parameterName, T value)
    {
        return new SqlParameter(parameterName, value);
    }
}
