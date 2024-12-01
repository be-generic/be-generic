using System.Data.Common;

namespace BeGeneric.Backend.Builder
{
    public interface IBeGenericDatabaseProvider
    {
        DbCommand GetDbCommand(string command, DbConnection connection);
        DbParameter GetDbParameter(string key, object value);
    }
}
