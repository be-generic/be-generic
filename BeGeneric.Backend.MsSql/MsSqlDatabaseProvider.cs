using BeGeneric.Backend.Builder;
using System.Data.Common;
using System.Data.SqlClient;

namespace BeGeneric.Backend.MsSql
{
    public class MsSqlDatabaseProvider: IBeGenericDatabaseProvider
    {
        public DbCommand GetDbCommand(string command, DbConnection connection) => new SqlCommand(command, (SqlConnection)connection);

        public DbParameter GetDbParameter(string key, object value) => new SqlParameter(key, value);
    }
}
