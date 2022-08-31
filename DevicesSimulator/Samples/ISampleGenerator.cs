using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevicesSimulator.Samples
{
    public interface ISampleGenerator
    {
        Task SendTelemetryAsync(string telemetryName, string telemetryValue, CancellationToken cancellationToken);
    }
}
