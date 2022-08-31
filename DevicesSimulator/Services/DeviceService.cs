using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevicesSimulator.Services
{
    public class DeviceService : IDeviceService
    {
        private DeviceClient deviceClient;

        public DeviceService()
        {
            deviceClient = DeviceClient.CreateFromConnectionString(Environment.GetEnvironmentVariable("IoTHubConnectionString"));
        }
    }
}
