using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace Spotify2Media.Core;

public class ConversionLogger : IDisposable
{
    private readonly Action<string, bool> _uiCallback;
    private readonly ILogger _logger;

    public ConversionLogger(string outputDir, Action<string, bool> uiCallback)
    {
        _uiCallback = uiCallback;
        var logPath = Path.Combine(outputDir, "conversion.log");
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logPath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u4}] {Message:lj}{NewLine}"
            )
            .CreateLogger();
    }

    public void Log(string message, bool isError = false)
    {
        if (isError)
            _logger.Error(message);
        else
            _logger.Information(message);

        _uiCallback(message, isError);
    }

    public void Dispose()
    {
        (_logger as IDisposable)?.Dispose();
    }
}
