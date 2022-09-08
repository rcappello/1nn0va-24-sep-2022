using DeviceCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DevicesSimulatorClient
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Set up logging
            ILogger logger = InitializeConsoleDebugLogger();

            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();
            var devicesSettings = new List<DevicesSettings>();
            configuration.GetSection("Devices").Bind(devicesSettings);

            logger.LogInformation("Press Control+C to quit the Device Simulator Client.");
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
                logger.LogInformation("Execution cancellation requested; will exit.");
            };

            logger.LogDebug($"Set up the devices client.");

            var deviceClientEngine = new DeviceClientEngine(devicesSettings, logger, cts);
            await deviceClientEngine.RunDevicesClientAsync();
        }

        private static ILogger InitializeConsoleDebugLogger()
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                .AddFilter(level => level >= LogLevel.Debug)
                .AddSystemdConsole(options =>
                {
                    options.TimestampFormat = "[yyyy/MM/dd HH:mm:ss]";
                });
            });

            return loggerFactory.CreateLogger<ISample>();
        }
    }
}