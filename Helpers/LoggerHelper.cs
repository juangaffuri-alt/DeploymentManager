using Serilog.Sinks.MSSqlServer;
using System.Data;

namespace PasadorScript.Helpers
{
    public static class LoggerHelper
    {
        public static ColumnOptions GetColumnOptions()
        {
            var columnOptions = new ColumnOptions();
            columnOptions.Store.Remove(StandardColumn.Properties);
            columnOptions.Store.Remove(StandardColumn.MessageTemplate);

            columnOptions.AdditionalColumns = new List<SqlColumn>
            {
                new SqlColumn { ColumnName = "Service", DataType = SqlDbType.NVarChar, DataLength = 100 },
                new SqlColumn { ColumnName = "UserId", DataType = SqlDbType.NVarChar, DataLength = 100 },
                new SqlColumn { ColumnName = "RequestId", DataType = SqlDbType.NVarChar, DataLength = 100 }
            };

            return columnOptions;
        }
    }
}
