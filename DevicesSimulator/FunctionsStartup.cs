using DevicesSimulator.Services;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(DevicesSimulator.Startup))]
namespace DevicesSimulator
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<IDeviceService>((s) => {
                return new DeviceService();
            });
        }
    }
}
