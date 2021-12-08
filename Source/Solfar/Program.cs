using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadiantPi.Lumagen;
using RadiantPi.Sony.Cledis;
using RadiantPi.Trinnov.Altitude;

namespace Solfar {

    public static class Program {

        //--- Class Methods ---
        public static async Task Main(string[] args) {
            var services = new ServiceCollection()
                .AddSingleton(services => new RadianceProClientConfig {
                    PortName = "/dev/ttyUSB0",
                    BaudRate = 9600
                })
                .AddSingleton(services => new SonyCledisClientConfig {
                    Host = "192.168.1.190",
                    Port = 53595
                })
                .AddSingleton(services => new TrinnovAltitudeClientConfig {
                    Host = "192.168.1.180",
                    Port = 44100
                });
            ConfigureServices(services);
            using(var serviceProvider = services.BuildServiceProvider()) {
                var controller = serviceProvider.GetRequiredService<SolfarController>();
                _ = Task.Run(() => {
                    Console.ReadLine();
                    controller.Stop();
                });
                await controller.Start();

                // wait until orchestrator finishes
                await controller.WaitAsync();
            }
        }

        private static void ConfigureServices(IServiceCollection services)
            => services
                .AddLogging(configure => configure
                    .AddFilter("Default", LogLevel.Trace)
                    .AddFilter("RadiantPi.Lumagen.RadianceProClient", LogLevel.Trace)
                    .AddFilter("RadiantPi.Sony.Cledis.SonyCledisClient", LogLevel.Trace)
                    .AddFilter("RadiantPi.Automation.AutomationController", LogLevel.Trace)
                    .AddConsole()
                )
                .Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Trace)
                .AddSingleton<IRadiancePro, RadianceProClient>()
                .AddSingleton<ISonyCledis, SonyCledisClient>()
                .AddSingleton<ITrinnovAltitude, TrinnovAltitudeClient>()
                .AddSingleton<SolfarController>();
    }
}
