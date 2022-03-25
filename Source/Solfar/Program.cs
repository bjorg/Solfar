/*
 * Solfar - Solfar Skylounge Automation
 * Copyright (C) 2020-2022 - Steve G. Bjorg
 *
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 * FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more
 * details.
 *
 * You should have received a copy of the GNU Affero General Public License along
 * with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadiantPi.Lumagen;
using RadiantPi.Kaleidescape;
using RadiantPi.Sony.Cledis;
using RadiantPi.Trinnov.Altitude;
using TMDbLib.Client;

namespace Solfar;

public static class Program {

    //--- Class Methods ---
    public static async Task Main(string[] args) {

        // read sensitive configuration information from environment variables
        var kPlayerDeviceId = Environment.GetEnvironmentVariable("KPLAYER_SERIAL_NUMBER");
        if(string.IsNullOrEmpty(kPlayerDeviceId)) {
            Console.WriteLine("ERROR: environment variable KPLAYER_SERIAL_NUMBER is not set");
            return;
        }
        var movieDbApiKey = Environment.GetEnvironmentVariable("TMDB_APIKEY");
        if(string.IsNullOrEmpty(movieDbApiKey)) {
            Console.WriteLine("ERROR: environment variable TMDB_APIKEY is not set");
            return;
        }

        // initialize services with device configurations
        var services = new ServiceCollection()
            .AddSingleton(_ => new RadianceProClientConfig {
                PortName = "/dev/ttyUSB0",
                BaudRate = 9600
            })
            .AddSingleton(_ => new SonyCledisClientConfig {
                Host = "192.168.1.190",
                Port = 53595
            })
            .AddSingleton(_ => new TrinnovAltitudeClientConfig {
                Host = "192.168.1.180",
                Port = 44100
            })
            .AddSingleton(_ => new KaleidescapeClientConfig {
                Host = "192.168.1.147",
                Port = 10000,
                DeviceId = kPlayerDeviceId
            })
            .AddSingleton(_ => new TMDbClient(movieDbApiKey));

        // launch controller
        ConfigureServices(services);
        using var serviceProvider = services.BuildServiceProvider();
        var controller = serviceProvider.GetRequiredService<SolfarController>();
        _ = Task.Run(() => {
            Console.ReadLine();
            controller.Close();
        });
        await controller.Run();
    }

    private static void ConfigureServices(IServiceCollection services)
        => services
            .AddLogging(configure => configure
                .AddFilter("Default", LogLevel.Trace)
                .AddFilter("RadiantPi.Kaleidescape.KaleidescapeClient", LogLevel.Trace)
                .AddFilter("RadiantPi.Lumagen.RadianceProClient", LogLevel.Trace)
                .AddFilter("RadiantPi.Sony.Cledis.SonyCledisClient", LogLevel.Trace)
                .AddFilter("RadiantPi.Trinnov.Altitude.TrinnovAltitudeClient", LogLevel.Trace)
                .AddFilter("RadiantPi.Automation.AutomationController", LogLevel.Trace)
                .AddConsole()
                .AddFile("Logs/Solfar-{Date}.log", LogLevel.Trace)
            )
            .Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Trace)
            .AddSingleton<IKaleidescape, KaleidescapeClient>()
            .AddSingleton<IRadiancePro, RadianceProClient>()
            .AddSingleton<ISonyCledis, SonyCledisClient>()
            .AddSingleton<ITrinnovAltitude, TrinnovAltitudeClient>()
            .AddSingleton<SolfarController>();
}
