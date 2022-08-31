using System;
using DevicesSimulator.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DevicesSimulator
{
    public class SimulateData
    {
        private const string ModelId = "dtmi:com:example:Thermostat;1";
        private const string SdkEventProviderPrefix = "Microsoft-Azure-";

        private IDeviceService deviceService;

        public SimulateData(IDeviceService deviceService)
        {
            this.deviceService = deviceService;
        }

        [FunctionName("SimulateData")]
        public void Run([TimerTrigger("*/5 * * * * *")]TimerInfo myTimer, ILogger log)
        {
            

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
