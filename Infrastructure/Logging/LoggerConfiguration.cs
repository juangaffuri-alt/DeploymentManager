using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using PasadorScript.Helpers;

namespace PasadorScript.Infrastructure.Logging;

public static class LoggerConfiguration
{
    public static Serilog.ILogger CreateLogger(IConfiguration configuration)
    {
        var sinkOptions = new MSSqlServerSinkOptions
        {
            TableName = "Logs",
            AutoCreateSqlTable = true,
        };

        string minLevelForDbString = configuration
            .GetSection("Serilog:WriteTo:Mssql:restrictedToMinimumLevel")?
            .Value ?? configuration.GetValue<string>("Serilog:MinimumLevel:Default", "Information");

        if (!Enum.TryParse<LogEventLevel>(minLevelForDbString, true, out var minLevelForDb))
        {
            minLevelForDb = LogEventLevel.Information;
        }

        return new Serilog.LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.MSSqlServer(
                connectionString: configuration.GetConnectionString("NuevoP229"),
                sinkOptions: sinkOptions,
                columnOptions: LoggerHelper.GetColumnOptions(),
                restrictedToMinimumLevel: minLevelForDb
            )
            .CreateLogger();
    }
}
