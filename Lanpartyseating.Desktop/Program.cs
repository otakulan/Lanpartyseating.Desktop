using JetBrains.Annotations;
using Lanpartyseating.Desktop.Business;
using Lanpartyseating.Desktop.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
                    .ValidateDataAnnotations()
                    .BindConfiguration("Seating");
                services.AddOptions<DebugOptions>()
                    .BindConfiguration("Debug");
                services.AddSingleton<PhoenixChannelReactorService>();
                services.AddSingleton<Callbacks>();
                services.AddSingleton<ICredentialProviderService, CredentialProviderService>();
                
                var debugOptions = services.BuildServiceProvider().GetRequiredService<IOptions<DebugOptions>>().Value;
                
                if (debugOptions.UseDummySessionManager)
                {
                    services.AddSingleton<ISessionManager, DummySessionManager>();
                }
                else if (debugOptions.UseCredentialProvider)
                {
                    // Use the new credential provider session manager
                    services.AddSingleton<ISessionManager, CredentialProviderSessionManager>();
                    // Keep the old one available if needed
                    services.AddSingleton<WindowsSessionManager>();
                }
                else
                {
                    // Use the old registry-based approach
                    services.AddSingleton<ISessionManager, WindowsSessionManager>();
                }
                services.AddSingleton<Utils>();
                services.AddHostedService<Worker>();
                services.AddSingleton<ReservationManager>();
                services.AddSingleton<NamedPipeServerHostedService>();
                services.AddSingleton<INamedPipeServerService>(sp => sp.GetRequiredService<NamedPipeServerHostedService>());
                services.AddHostedService<NamedPipeServerHostedService>(sp => sp.GetRequiredService<NamedPipeServerHostedService>());
                services.AddSingleton<Timekeeper>();
            })
            .Build();

        host.Run();
    }
}
