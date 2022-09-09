using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommon.Enums
{
    internal enum StatusCode
    {
        Completed = 200,
        InProgress = 202,
        ReportDeviceInitialProperty = 203,
        AlreadyOpened = 204,
        AlreadyClosed = 205,
        BadRequest = 400,
        NotFound = 404
    }
}
