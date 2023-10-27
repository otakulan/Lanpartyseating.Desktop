using JetBrains.Annotations;
using Lanpartyseating.Desktop.Business;
using Lanpartyseating.Desktop.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace Lanpartyseating.Desktop;

[UsedImplicitly]
internal class Program
{
    static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureHostConfiguration(config =>
            {
                config.AddJsonFile(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Lanparty Seating",
                        "appsettings.json"), true);
            })
            .ConfigureServices(services =>
            {
                services.AddWindowsService(options =>
                {
                    options.ServiceName = "Lanparty Seating";
                });
                services.AddOptions<SeatingOptions>()
                    .BindConfiguration("Seating");
                services.AddSingleton<PhoenixChannelReactorService>();
                services.AddSingleton<Callbacks>();
                services.AddSingleton<Utils>();
                services.AddHostedService<Worker>();
            })
            .Build();

        host.Run();
    }
}
