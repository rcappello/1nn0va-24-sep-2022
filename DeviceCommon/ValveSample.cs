using DeviceCommon.Enums;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceCommon
{
    public class ValveSample : ISample
    {
        // The default reported "value" and "av" for each "Thermostat" component on the client initial startup.
        // See https://docs.microsoft.com/azure/iot-develop/concepts-convention#writable-properties for more details in acknowledgment responses.
        private const double DefaultPropertyValue = 0d;
        private const long DefaultAckVersion = 0L;
        private readonly string DeviceId;

        private const string ActuatorPositionProperty = "ActuatorPosition";
        private const string ActuatorStateProperty = "ActuatorState";
        private const string BatteryLevelProperty = "BatteryLevel";
        private const string LocationProperty = "LocationPosition";
        private const string RotationSpeedProperty = "RotationSpeed";
        private const string SerialNumberProperty = "SerialNumber";
        private const string TimeRemainingProperty = "TimeRemaining";
        private const string ValveNumberProperty = "ValveNumber";
        private const string ValvePositionProperty = "ValvePosition";

        private readonly Random _random = new Random();

        private double _temperature;
        private double _pressure = 1.0d;
        private double _maxTemp;
        private double _maxPressure;

        // Dictionary to hold the Temperature and Pressure updates sent over.
        // NOTE: Memory constrained devices should leverage storage capabilities of an external service to store this information and perform computation.
        // See https://docs.microsoft.com/en-us/azure/event-grid/compare-messaging-services for more details.
        private readonly Dictionary<DateTimeOffset, double> _temperatureReadingsDateTimeOffset = new Dictionary<DateTimeOffset, double>();
        private readonly Dictionary<DateTimeOffset, double> _pressureReadingsDateTimeOffset = new Dictionary<DateTimeOffset, double>();

        private readonly DeviceClient _deviceClient;
        private readonly ILogger _logger;

        // A safe initial value for caching the writable properties version is 1, so the client
        // will process all previous property change requests and initialize the device application
        // after which this version will be updated to that, so we have a high water mark of which version number
        // has been processed.
        private static long s_localWritablePropertiesVersion = 1;

        public ValveSample(DeviceClient deviceClient, string deviceId, ILogger logger)
        {
            _deviceClient = deviceClient ?? throw new ArgumentNullException(nameof(deviceClient));
            DeviceId = deviceId;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task PerformOperationsAsync(CancellationToken cancellationToken)
        {
            // This sample follows the following workflow:
            // -> Set handler to receive and respond to connection status changes.
            // -> Set handler to receive "OpenValve" command, and send the valve new pressure as command response.
            // -> Set handler to receive "CloseValve" command, and send the valve new pressure as command response.
            // -> Check if the device properties are empty on the initial startup. If so, report the default values with ACK to the hub.
            // -> Periodically send "Temperature" over telemetry.
            // -> Periodically send "Pressure" over telemetry.

            _deviceClient.SetConnectionStatusChangesHandler(async (status, reason) =>
            {
                _logger.LogDebug($"{DeviceId} Connection status change registered - status={status}, reason={reason}.");

                // Call GetWritablePropertiesAndHandleChangesAsync() to get writable properties from the server once the connection status changes into Connected.
                // This can get back "lost" property updates in a device reconnection from status Disconnected_Retrying or Disconnected.
                if (status == ConnectionStatus.Connected)
                {
                    await GetWritablePropertiesAndHandleChangesAsync();
                }
            });

            //_logger.LogDebug($"Set handler to receive \"TargetTemperature\" updates.");
            //await _deviceClient.SetDesiredPropertyUpdateCallbackAsync(TargetTemperatureUpdateCallbackAsync, _deviceClient, cancellationToken);

            //_logger.LogDebug($"Set handler for \"getMaxMinReport\" command.");
            //await _deviceClient.SetMethodHandlerAsync("getMaxMinReport", HandleMaxMinReportCommand, _deviceClient, cancellationToken);

            _logger.LogDebug($"Set handler for \"OpenValve\" command.");
            await _deviceClient.SetMethodHandlerAsync("OpenValve", HandleOpenValveCommand, _deviceClient, cancellationToken);

            _logger.LogDebug($"Set handler for \"CloseValve\" command.");
            await _deviceClient.SetMethodHandlerAsync("CloseValve", HandleCloseValveCommand, _deviceClient, cancellationToken);

            _logger.LogDebug("Check if the device properties are empty on the initial startup.");
            await CheckEmptyPropertiesAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                // Generate a random value between 25.0°C and 27.0°C for the current temperature reading.
                _temperature = Math.Round(_random.NextDouble() * 2.0 + 25.0, 1);

                if (_pressure != 0.0d)
                {
                    // Generate a random value between 10 and 11 for the current pressure reading.
                    _pressure = Math.Round(_random.NextDouble() * 1.0 + 10.0, 1);
                }

                await SendTemperatureAsync(cancellationToken);
                await SendPressureAsync(cancellationToken);
                await Task.Delay(5 * 1000, cancellationToken);
            }
        }

        private async Task GetWritablePropertiesAndHandleChangesAsync()
        {
            Twin twin = await _deviceClient.GetTwinAsync();
            _logger.LogInformation($"{DeviceId} Device retrieving twin values on CONNECT: {twin.ToJson()}");

            TwinCollection twinCollection = twin.Properties.Desired;
            long serverWritablePropertiesVersion = twinCollection.Version;

            // Check if the writable property version is outdated on the local side.
            // For the purpose of this sample, we'll only check the writable property versions between local and server
            // side without comparing the property values.
            if (serverWritablePropertiesVersion > s_localWritablePropertiesVersion)
            {
                _logger.LogInformation($"The writable property version cached on local is changing " +
                    $"from {s_localWritablePropertiesVersion} to {serverWritablePropertiesVersion}.");

                foreach (KeyValuePair<string, object> propertyUpdate in twinCollection)
                {
                    string propertyName = propertyUpdate.Key;
                    if (propertyName == ActuatorPositionProperty)
                    {
                        //await TargetTemperatureUpdateCallbackAsync(twinCollection, propertyName);
                    }
                    else
                    {
                        _logger.LogWarning($"Property: Received an unrecognized property update from service:" +
                            $"\n[ {propertyUpdate.Key}: {propertyUpdate.Value} ].");
                    }
                }

                _logger.LogInformation($"The writable property version on local is currently {s_localWritablePropertiesVersion}.");
            }
        }

        // The desired property update callback, which receives the target temperature as a desired property update,
        // and updates the current temperature value over telemetry and reported property update.
        private async Task TargetTemperatureUpdateCallbackAsync(TwinCollection desiredProperties, object userContext)
        { /*
            (bool targetTempUpdateReceived, double targetTemperature) = GetPropertyFromTwin<double>(desiredProperties, TargetTemperatureProperty);
            if (targetTempUpdateReceived)
            {
                _logger.LogDebug($"Property: Received - {{ \"{TargetTemperatureProperty}\": {targetTemperature}°C }}.");

                s_localWritablePropertiesVersion = desiredProperties.Version;

                string jsonPropertyPending = $"{{ \"{TargetTemperatureProperty}\": {{ \"value\": {targetTemperature}, \"ac\": {(int)StatusCode.InProgress}, " +
                    $"\"av\": {desiredProperties.Version}, \"ad\": \"In progress - reporting current temperature\" }} }}";
                var reportedPropertyPending = new TwinCollection(jsonPropertyPending);
                await _deviceClient.UpdateReportedPropertiesAsync(reportedPropertyPending);
                _logger.LogDebug($"Property: Update - {{\"{TargetTemperatureProperty}\": {targetTemperature}°C }} is {StatusCode.InProgress}.");

                // Update Temperature in 2 steps
                double step = (targetTemperature - _temperature) / 2d;
                for (int i = 1; i <= 2; i++)
                {
                    _temperature = Math.Round(_temperature + step, 1);
                    await Task.Delay(6 * 1000);
                }

                string jsonProperty = $"{{ \"{TargetTemperatureProperty}\": {{ \"value\": {targetTemperature}, \"ac\": {(int)StatusCode.Completed}, " +
                    $"\"av\": {desiredProperties.Version}, \"ad\": \"Successfully updated target temperature\" }} }}";
                var reportedProperty = new TwinCollection(jsonProperty);
                await _deviceClient.UpdateReportedPropertiesAsync(reportedProperty);
                _logger.LogDebug($"Property: Update - {{\"{TargetTemperatureProperty}\": {targetTemperature}°C }} is {StatusCode.Completed}.");
            }
            else
            {
                _logger.LogDebug($"Property: Received an unrecognized property update from service:\n{desiredProperties.ToJson()}");
            }
          */
        }

        private static (bool, T) GetPropertyFromTwin<T>(TwinCollection collection, string propertyName)
        {
            return collection.Contains(propertyName) ? (true, (T)collection[propertyName]) : (false, default);
        }

        private Task<MethodResponse> HandleOpenValveCommand(MethodRequest request, object userContext)
        {
            try
            {
                _logger.LogDebug($"{DeviceId} Command: Received - OpenValve " +
                    $"{request.DataAsJson}.");

                if(_pressure == 0.0d)
                {
                    _pressure = Math.Round(_random.NextDouble() * 1.0 + 10.0, 1);

                    var report = new
                    {
                        Pressure = _pressure,
                    };

                    _logger.LogDebug($"{DeviceId} Command: OpenValve:" +
                            $" New Pressure={report.Pressure}");

                    byte[] responsePayload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(report));
                    return Task.FromResult(new MethodResponse(responsePayload, (int)StatusCode.Completed));
                }

                _logger.LogDebug($"Command: Valve already opened ");
                return Task.FromResult(new MethodResponse((int)StatusCode.AlreadyOpened));
            }
            catch (JsonReaderException ex)
            {
                _logger.LogDebug($"Command input is invalid: {ex.Message}.");
                return Task.FromResult(new MethodResponse((int)StatusCode.BadRequest));
            }
        }

        private Task<MethodResponse> HandleCloseValveCommand(MethodRequest request, object userContext)
        {
            try
            {
                _logger.LogDebug($"{DeviceId} Command: Received - CloseValve " +
                    $"{request.DataAsJson}.");

                if (_pressure > 0.0d)
                {
                    _pressure = 0.0d;

                    var report = new
                    {
                        Pressure = _pressure,
                    };

                    _logger.LogDebug($"{DeviceId} Command: CloseValve:" +
                            $" New Pressure={report.Pressure}");

                    byte[] responsePayload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(report));
                    return Task.FromResult(new MethodResponse(responsePayload, (int)StatusCode.Completed));
                }

                _logger.LogDebug($"Command: Valve already closed");
                return Task.FromResult(new MethodResponse((int)StatusCode.AlreadyOpened));
            }
            catch (JsonReaderException ex)
            {
                _logger.LogDebug($"Command input is invalid: {ex.Message}.");
                return Task.FromResult(new MethodResponse((int)StatusCode.BadRequest));
            }
        }

        // Send temperature updates over telemetry. The sample also sends the value of max temperature since last reboot over reported property update.
        private async Task SendTemperatureAsync(CancellationToken cancellationToken)
        {
            await SendTemperatureTelemetryAsync(cancellationToken);

            double maxTemp = _temperatureReadingsDateTimeOffset.Values.Max<double>();
            if (maxTemp > _maxTemp)
            {
                _maxTemp = maxTemp;
                //Sent this Telemetry if you want to monitor it
            }
        }
        private async Task SendPressureAsync(CancellationToken cancellationToken)
        {
            await SendPressureTelemetryAsync(cancellationToken);

            double maxPressure = _pressureReadingsDateTimeOffset.Values.Max<double>();
            if (maxPressure > _maxPressure)
            {
                _maxPressure = maxPressure;
                //Sent this Telemetry if you want to monitor it
            }
        }

        // Send temperature update over telemetry.
        private async Task SendTemperatureTelemetryAsync(CancellationToken cancellationToken)
        {
            const string telemetryName = "Temperature";

            string telemetryPayload = $"{{ \"{telemetryName}\": {_temperature} }}";
            using var message = new Message(Encoding.UTF8.GetBytes(telemetryPayload))
            {
                ContentEncoding = "utf-8",
                ContentType = "application/json",
            };

            await _deviceClient.SendEventAsync(message, cancellationToken);
            _logger.LogDebug($"{DeviceId} Telemetry: Sent - {{ \"{telemetryName}\": {_temperature}°C }}.");

            _temperatureReadingsDateTimeOffset.Add(DateTimeOffset.Now, _temperature);
        }

        // Send pressure update over telemetry.
        private async Task SendPressureTelemetryAsync(CancellationToken cancellationToken)
        {
            const string telemetryName = "Pressure";

            string telemetryPayload = $"{{ \"{telemetryName}\": {_pressure} }}";
            using var message = new Message(Encoding.UTF8.GetBytes(telemetryPayload))
            {
                ContentEncoding = "utf-8",
                ContentType = "application/json",
            };

            await _deviceClient.SendEventAsync(message, cancellationToken);
            _logger.LogDebug($"{DeviceId} Telemetry: Sent - {{ \"{telemetryName}\": {_pressure} bar }}.");

            _pressureReadingsDateTimeOffset.Add(DateTimeOffset.Now, _pressure);
        }

        private async Task CheckEmptyPropertiesAsync(CancellationToken cancellationToken)
        {
            Twin twin = await _deviceClient.GetTwinAsync(cancellationToken);
            TwinCollection writableProperty = twin.Properties.Desired;
            TwinCollection reportedProperty = twin.Properties.Reported;

            // Check if the device properties (both writable and reported) are empty.
            if (!writableProperty.Contains(ActuatorPositionProperty) && !reportedProperty.Contains(ActuatorPositionProperty))
            {
                await ReportInitialPropertyAsync(ActuatorPositionProperty, cancellationToken);
            }
            // Check if the device properties (both writable and reported) are empty.
            if (!writableProperty.Contains(ActuatorStateProperty) && !reportedProperty.Contains(ActuatorStateProperty))
            {
                await ReportInitialPropertyAsync(ActuatorStateProperty, cancellationToken);
            }
            // Check if the device properties (both writable and reported) are empty.
            if (!writableProperty.Contains(BatteryLevelProperty) && !reportedProperty.Contains(BatteryLevelProperty))
            {
                await ReportInitialPropertyAsync(BatteryLevelProperty, cancellationToken);
            }
            // Check if the device properties (both writable and reported) are empty.
            if (!writableProperty.Contains(LocationProperty) && !reportedProperty.Contains(LocationProperty))
            {
                await ReportInitialPropertyAsync(LocationProperty, cancellationToken);
            }
            // Check if the device properties (both writable and reported) are empty.
            if (!writableProperty.Contains(RotationSpeedProperty) && !reportedProperty.Contains(RotationSpeedProperty))
            {
                await ReportInitialPropertyAsync(RotationSpeedProperty, cancellationToken);
            }
            // Check if the device properties (both writable and reported) are empty.
            if (!writableProperty.Contains(SerialNumberProperty) && !reportedProperty.Contains(SerialNumberProperty))
            {
                await ReportInitialPropertyAsync(SerialNumberProperty, cancellationToken);
            }
            // Check if the device properties (both writable and reported) are empty.
            if (!writableProperty.Contains(TimeRemainingProperty) && !reportedProperty.Contains(TimeRemainingProperty))
            {
                await ReportInitialPropertyAsync(TimeRemainingProperty, cancellationToken);
            }
            // Check if the device properties (both writable and reported) are empty.
            if (!writableProperty.Contains(ValveNumberProperty) && !reportedProperty.Contains(ValveNumberProperty))
            {
                await ReportInitialPropertyAsync(ValveNumberProperty, cancellationToken);
            }
            // Check if the device properties (both writable and reported) are empty.
            if (!writableProperty.Contains(ValvePositionProperty) && !reportedProperty.Contains(ValvePositionProperty))
            {
                await ReportInitialPropertyAsync(ValvePositionProperty, cancellationToken);
            }
        }

        private async Task ReportInitialPropertyAsync(string propertyName, CancellationToken cancellationToken)
        {
            // If the device properties are empty, report the default value with ACK(ac=203, av=0) as part of the PnP convention.
            // "DefaultPropertyValue" is set from the device when the desired property is not set via the hub.
            string jsonProperty = $"{{ \"{propertyName}\": {{ \"value\": {DefaultPropertyValue}, \"ac\": {(int)StatusCode.ReportDeviceInitialProperty}, " +
                    $"\"av\": {DefaultAckVersion}, \"ad\": \"Initialized with default value\"}} }}";

            var reportedProperty = new TwinCollection(jsonProperty);
            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperty, cancellationToken);
            _logger.LogDebug($"Report the default values.\nProperty: Update - {jsonProperty} is {StatusCode.Completed}.");
        }
    }
}