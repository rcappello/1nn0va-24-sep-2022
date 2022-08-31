using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevicesSimulatorClient
{
    public class DevicesSettings
    {
        public DeviceTypeEnum DeviceType { get; set; }
        public string DeviceId { get; set; }
        public string DeviceSymmetricKey { get; set; }
        public string DpsEndpoint { get; set; }
        public string DpsIdScope { get; set; }
        public string ModelId { get; set; }
    }

    public enum DeviceTypeEnum
    {
        Valve,
        Flow
    }
}
