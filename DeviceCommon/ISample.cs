using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommon
{
    public interface ISample
    {
        Task PerformOperationsAsync(CancellationToken cancellationToken);
    }
}
