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
                
                // Register all possible session managers
                services.AddSingleton<DummySessionManager>();
                services.AddSingleton<CredentialProviderSessionManager>();
                services.AddSingleton<WindowsSessionManager>();
                
                // Simple conditional registration - resolve at startup, not during build
                services.AddSingleton<ISessionManager>(serviceProvider =>
                {
                    var debugOptions = serviceProvider.GetRequiredService<IOptions<DebugOptions>>().Value;
                    var seatingOptions = serviceProvider.GetRequiredService<IOptions<SeatingOptions>>().Value;
                    
                    if (debugOptions.UseDummySessionManager)
                    {
                        return serviceProvider.GetRequiredService<DummySessionManager>();
                    }
                    else if (seatingOptions.UseCredentialProvider)
                    {
                        return serviceProvider.GetRequiredService<CredentialProviderSessionManager>();
                    }
                    else
                    {
                        return serviceProvider.GetRequiredService<WindowsSessionManager>();
                    }
                });
                services.AddSingleton<Utils>();
                services.AddHostedService<Worker>();
                services.AddSingleton<ReservationManager>();
                
                // Register NamedPipeServerHostedService properly
                services.AddSingleton<NamedPipeServerHostedService>();
                services.AddSingleton<INamedPipeServerService>(sp => sp.GetRequiredService<NamedPipeServerHostedService>());
                services.AddHostedService(sp => sp.GetRequiredService<NamedPipeServerHostedService>());
                
                services.AddSingleton<Timekeeper>();
            })
            .Build();

        host.Run();
    }
}
