﻿/*
 * Solfar - Solfar Skylounge Automation
 * Copyright (C) 2020-2021 - Steve G. Bjorg
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

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadiantPi.Lumagen;
using RadiantPi.Kaleidescape;
using RadiantPi.Sony.Cledis;
using RadiantPi.Trinnov.Altitude;
using TMDbLib.Client;

namespace Solfar {

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

            // initialize services
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
                })
                .AddSingleton(services => new KaleidescapeClientConfig {
                    Host = "192.168.1.147",
                    Port = 10000,
                    DeviceId = kPlayerDeviceId
                })
                .AddSingleton(services => new TMDbClient(movieDbApiKey));
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
                    .AddFilter("RadiantPi.Kaleidescape.KaleidescapeClient", LogLevel.Trace)
                    .AddFilter("RadiantPi.Lumagen.RadianceProClient", LogLevel.Trace)
                    .AddFilter("RadiantPi.Sony.Cledis.SonyCledisClient", LogLevel.Trace)
                    .AddFilter("RadiantPi.Trinnov.Altitude.TrinnovAltitudeClient", LogLevel.Trace)
                    .AddFilter("RadiantPi.Automation.AutomationController", LogLevel.Trace)
                    .AddConsole()
                )
                .Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Trace)
                .AddSingleton<IKaleidescape, KaleidescapeClient>()
                .AddSingleton<IRadiancePro, RadianceProClient>()
                .AddSingleton<ISonyCledis, SonyCledisClient>()
                .AddSingleton<ITrinnovAltitude, TrinnovAltitudeClient>()
                .AddSingleton<SolfarController>();
    }
}
