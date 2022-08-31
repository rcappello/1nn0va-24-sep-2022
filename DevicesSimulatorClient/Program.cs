using DeviceCommon;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevicesSimulatorClient
{
    public class Program
    {
        private const string SdkEventProviderPrefix = "Microsoft-Azure-";

        public static async Task Main(string[] args)
        {
            // Set up logging
            ILogger logger = InitializeConsoleDebugLogger();

            // Instantiating this seems to do all we need for outputting SDK events to our console log.
            using var skdLog = new ConsoleEventListener(SdkEventProviderPrefix, logger);

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

            logger.LogDebug($"Set up the device client.");

            try
            {
                foreach (var deviceSettings in devicesSettings)
                {
                    using DeviceClient deviceClient = await SetupDeviceClientAsync(deviceSettings, logger, cts.Token);
                    var sample = new ValveSample(deviceClient, logger);
                    await sample.PerformOperationsAsync(cts.Token);

                    // PerformOperationsAsync is designed to run until cancellation has been explicitly requested, either through
                    // cancellation token expiration or by Console.CancelKeyPress.
                    // As a result, by the time the control reaches the call to close the device client, the cancellation token source would
                    // have already had cancellation requested.
                    // Hence, if you want to pass a cancellation token to any subsequent calls, a new token needs to be generated.
                    // For device client APIs, you can also call them without a cancellation token, which will set a default
                    // cancellation timeout of 4 minutes: https://github.com/Azure/azure-iot-sdk-csharp/blob/64f6e9f24371bc40ab3ec7a8b8accbfb537f0fe1/iothub/device/src/InternalClient.cs#L1922
                    await deviceClient.CloseAsync(CancellationToken.None);
                }
            }
            catch (OperationCanceledException) { } // User canceled operation

        }


        private static ILogger InitializeConsoleDebugLogger()
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                .AddFilter(level => level >= LogLevel.Debug)
                .AddSystemdConsole(options =>
                {
                    options.TimestampFormat = "[MM/dd/yyyy HH:mm:ss]";
                });
            });

            return loggerFactory.CreateLogger<ValveSample>();
        }

        // Initialize the device client instance using connection string based authentication, over Mqtt protocol (TCP, with fallback over Websocket)
        // and setting the ModelId into ClientOptions.
        private static async Task<DeviceClient> SetupDeviceClientAsync(DevicesSettings deviceSettings, ILogger logger, CancellationToken cancellationToken)
        {
            var options = new ClientOptions
            {
                ModelId = deviceSettings.ModelId,
            };

            logger.LogDebug($"Initializing via DPS");
            DeviceRegistrationResult dpsRegistrationResult = await ProvisionDeviceAsync(deviceSettings, cancellationToken);
            var authMethod = new DeviceAuthenticationWithRegistrySymmetricKey(dpsRegistrationResult.DeviceId, deviceSettings.DeviceSymmetricKey);
            var deviceClient = InitializeDeviceClient(dpsRegistrationResult.AssignedHub, deviceSettings.ModelId, authMethod);
            
            return deviceClient;
        }

        // Provision a device via DPS, by sending the PnP model Id as DPS payload.
        private static async Task<DeviceRegistrationResult> ProvisionDeviceAsync(DevicesSettings deviceSettings, CancellationToken cancellationToken)
        {
            using SecurityProvider symmetricKeyProvider = new SecurityProviderSymmetricKey(deviceSettings.DeviceId, deviceSettings.DeviceSymmetricKey, null);
            using ProvisioningTransportHandler mqttTransportHandler = new ProvisioningTransportHandlerMqtt();
            ProvisioningDeviceClient pdc = ProvisioningDeviceClient.Create(deviceSettings.DpsEndpoint, deviceSettings.DpsIdScope,
                symmetricKeyProvider, mqttTransportHandler);

            var pnpPayload = new ProvisioningRegistrationAdditionalData
            {
                JsonData = $"{{ \"modelId\": \"{deviceSettings.ModelId}\" }}",
            };
            return await pdc.RegisterAsync(pnpPayload, cancellationToken);
        }

        private static DeviceClient InitializeDeviceClient(string hostname, string modelId, IAuthenticationMethod authenticationMethod)
        {
            var options = new ClientOptions
            {
                ModelId = modelId,
            };

            DeviceClient deviceClient = DeviceClient.Create(hostname, authenticationMethod, TransportType.Mqtt, options);

            return deviceClient;
        }
    }
}