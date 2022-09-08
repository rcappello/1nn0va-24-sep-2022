using DeviceCommon;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevicesSimulatorClient
{
    internal class DeviceClientEngine
    {
        List<DevicesSettings> devicesSettings;
        ILogger logger;
        CancellationTokenSource cts;

        public DeviceClientEngine(List<DevicesSettings>  devicesSettings,
            ILogger logger,
            CancellationTokenSource cts
            )
        {
            this.devicesSettings = devicesSettings;
            this.logger = logger;
            this.cts = cts;
        }

        public async Task RunDevicesClientAsync()
        {
            var deviceClients = new List<DeviceClient>();
            var deviceTasks = new List<Task>();
            try
            {
                foreach (var deviceSettings in devicesSettings)
                {
                    // PerformOperationsAsync is designed to run until cancellation has been explicitly requested, either through
                    // cancellation token expiration or by Console.CancelKeyPress.
                    // As a result, by the time the control reaches the call to close the device client, the cancellation token source would
                    // have already had cancellation requested.
                    // Hence, if you want to pass a cancellation token to any subsequent calls, a new token needs to be generated.
                    // For device client APIs, you can also call them without a cancellation token, which will set a default
                    // cancellation timeout of 4 minutes: https://github.com/Azure/azure-iot-sdk-csharp/blob/64f6e9f24371bc40ab3ec7a8b8accbfb537f0fe1/iothub/device/src/InternalClient.cs#L1922
                    var deviceClient = await SetupDeviceClientAsync(deviceSettings, logger, cts.Token);
                    deviceClients.Add(deviceClient);

                    ISample sample;
                    if (deviceSettings.DeviceType == DeviceTypeEnum.Valve)
                        sample = new ValveSample(deviceClient, logger);
                    else
                        sample = new ValveSample(deviceClient, logger);

                    var task = sample.PerformOperationsAsync(cts.Token);
                    deviceTasks.Add(task);

                }

                await Task.WhenAll(deviceTasks.ToArray());

            }
            catch (OperationCanceledException) {    // User canceled operation

                foreach (var deviceClient in deviceClients)
                    await deviceClient.CloseAsync(CancellationToken.None);

            }
        }

        // Initialize the device client instance using connection string based authentication, over Mqtt protocol (TCP, with fallback over Websocket)
        // and setting the ModelId into ClientOptions.
        private static async Task<DeviceClient> SetupDeviceClientAsync(DevicesSettings deviceSettings, ILogger logger, CancellationToken cancellationToken)
        {
            logger.LogDebug($"Initializing via DPS");

            using var security = new SecurityProviderSymmetricKey(
                deviceSettings.DeviceId,
                deviceSettings.DeviceSymmetricKey,
                null);

            using ProvisioningTransportHandler transportHandler = new ProvisioningTransportHandlerMqtt();

            var provClient = ProvisioningDeviceClient.Create(
                deviceSettings.DpsEndpoint,
                deviceSettings.DpsIdScope,
                security,
                transportHandler);

            logger.LogDebug($"Initialized for registration Id {security.GetRegistrationID()}.");

            var pnpPayload = new ProvisioningRegistrationAdditionalData
            {
                JsonData = $"{{ \"modelId\": \"{deviceSettings.ModelId}\" }}",
            };
            logger.LogDebug("Registering with the device provisioning service...");
            var dpsRegistrationResult = await provClient.RegisterAsync(pnpPayload, cancellationToken);

            var authMethod = new DeviceAuthenticationWithRegistrySymmetricKey(dpsRegistrationResult.DeviceId, deviceSettings.DeviceSymmetricKey);

            var options = new ClientOptions
            {
                ModelId = deviceSettings.ModelId,
            };

            var deviceClient = DeviceClient.Create(dpsRegistrationResult.AssignedHub, authMethod, TransportType.Mqtt, options);

            return deviceClient;
        }

    }
}
