/*
 * Solfar - Solfar Skylounge Automation
 * Copyright (C) 2020-2023 - Steve G. Bjorg
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

using HtmlDocument;
using RadiantPi.Kaleidescape;
using RadiantPi.Lumagen;
using RadiantPi.Sony.Cledis;
using RadiantPi.Trinnov.Altitude;
using Solfar;
using TMDbLib.Client;

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

// create and configure services
var builder = WebApplication.CreateBuilder(args);
builder.Services

    // configure logging
    .AddLogging(configure => configure
        .AddFilter("Default", LogLevel.Trace)
        .AddFilter("RadiantPi.Kaleidescape.KaleidescapeClient", LogLevel.Trace)
        .AddFilter("RadiantPi.Lumagen.RadianceProClient", LogLevel.Trace)
        .AddFilter("RadiantPi.Sony.Cledis.SonyCledisClient", LogLevel.Trace)
        .AddFilter("RadiantPi.Trinnov.Altitude.TrinnovAltitudeClient", LogLevel.Trace)
        .AddFilter("RadiantPi.Automation.AutomationController", LogLevel.Trace)
        // .AddFilter("MediaCenterClient", LogLevel.Trace)
        .AddConsole()
        .AddFile("Logs/Solfar-{Date}.log", LogLevel.Trace)
    )
    .Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Trace)

    // Sony Cledis client configuration
    .AddSingleton(_ => new SonyCledisClientConfig {
        Host = "192.168.1.190",
        Port = 53595
    })
    .AddSingleton<ISonyCledis, SonyCledisClient>()

    // Lumagen RadiancePro client configuration
    .AddSingleton(_ => new RadianceProClientConfig {
        PortName = "COM3",
        BaudRate = 9600
    })
    .AddSingleton<IRadiancePro, RadianceProClient>()

    // Trinnov Altitude client configuration
    .AddSingleton(_ => new TrinnovAltitudeClientConfig {
        Host = "192.168.1.180",
        Port = 44100
    })
    .AddSingleton<ITrinnovAltitude, TrinnovAltitudeClient>()

    // Kaleidescape client configuration
    .AddSingleton(_ => new KaleidescapeClientConfig {
        Host = "192.168.1.147",
        Port = 10000,
        DeviceId = kPlayerDeviceId
    })
    .AddSingleton<IKaleidescape, KaleidescapeClient>()

    // TheMovieDatabase client configuration
    .AddSingleton(_ => new TMDbClient(movieDbApiKey))

    // HTTP client configuration
    .AddSingleton(_ => new HttpClient())

    // JRiver MediaCenter client
    .AddSingleton(_ => new MediaCenterClientConfig {
        Url = "http://192.168.0.236:52199",
        Interval = TimeSpan.FromSeconds(3)
    })
    .AddSingleton<MediaCenterClient>()

    // add Solfar controller
    .AddSingleton<SolfarController>();

// launch web API endpoint
var app = builder.Build();
app.MapGet("/", GetStatusAsync);
app.MapPost("/cledis/light-output/{mode}", async (string mode, HttpContext _, ISonyCledis cledisClient, ILogger<Program>? logger) => SwitchLightOutputAsync(logger, cledisClient, Enum.Parse<SonyCledisLightOutputMode>(mode, ignoreCase: true)));
var appTask = app.RunAsync();

// launch Solfar controller
var controller = app.Services.GetRequiredService<SolfarController>();
var controllerTask = controller.RunAsync();
var mediaCenter = app.Services.GetRequiredService<MediaCenterClient>();
var mediaCenterTask = mediaCenter.RunAsync();

// close web API and Solfar controller when ENTER key is pressed
_ = Task.Run(() => {
    Console.ReadLine();
    controller.Close();
    mediaCenter.Stop();
    app.StopAsync();
});

// run until the web API and Solfar controller both exit
await Task.WhenAll(new[] {
    controllerTask,
    appTask,
    mediaCenterTask
});

// local functions
async Task<HtmlDoc> GetStatusAsync(ILogger<Program>? logger, ISonyCledis cledisClient) {
    logger?.LogInformation($"{nameof(GetStatusAsync)} called");

    // read settings from Sony C-LED controller
    var power = await cledisClient.GetPowerStatusAsync();
    var input = SonyCledisInput.Undefined;
    var mode2D3D = SonyCledis2D3DMode.Undefined;
    float controllerTemperature = 0;
    float maxModuleTemperature = 0;
    if(power == SonyCledisPowerStatus.On) {
        input = await cledisClient.GetInputAsync();
        mode2D3D = await cledisClient.Get2D3DModeAsync();
        var temperatures = await cledisClient.GetTemperatureAsync();
        controllerTemperature = temperatures?.ControllerTemperature ?? 0;
        maxModuleTemperature = temperatures?.Modules
            .Cast<SonyCledisModuleTemperature>()
            .SelectMany(module => module.CellTemperatures.Append(module.AmbientTemperature).Append(module.BoardTemperature))
            .Max() ?? 0;
    }
    var displays = DisplayApi.GetDisplays();
    var displayStatus = DisplayApi.GetCurrentDisplayStatus();
    return new HtmlDoc()
        .Attr("lang", "en")
        .Head()
            .Title("HTPC Status")
            .Elem("style", @"
body {
    background-color: #121826;
    color: #FFFFFF
}")
        .End()
        .Body()
            .Elem("h1", "Solfar HTPC Status")

            // Sony C-LED Controller Information
            .Elem("h2", "Sony C-LED")
            .Begin("ul")
                .Elem("li", $"Controller Power: {power}")
                .Elem("li", $"Active Input: {input}")
                .Elem("li", $"2D/3D Mode: {mode2D3D}")
                .Elem("li", $"Controller Temperature: {controllerTemperature:#0.00}°C")
                .Elem("li", $"Modules Temperature (Max): {maxModuleTemperature:#0.00}°C")
            .End()

            // Display Information
            .Elem("h2", "Displays")
            .Begin("ul")
                .Elem("li", $"HDMI Audio Active: {displayStatus.AudioEnabled}")
                .Elem("li", $"NVidia Mode: {displayStatus.NVidiaMode}")
                .Begin("li")
                    .Value("Attached")
                    .Begin("ol")
                        .Build(parent => {
                            var current = parent;
                            foreach(var display in displays) {
                                current = current.Elem("li", $"{display.Description}: {display.Resolution.Width}x{display.Resolution.Height} ({display.Resolution.RefreshRate}Hz)");
                            }
                            return current;
                        })
                    .End()
                .End()
            .End()
        .End();
}

async Task SwitchLightOutputAsync(ILogger<Program>? logger, ISonyCledis cledisClient, SonyCledisLightOutputMode mode) {
    logger?.LogInformation($"{nameof(SwitchLightOutputAsync)}({mode}) called");

    // check if power is on
    var power = await cledisClient.GetPowerStatusAsync();
    if(power != SonyCledisPowerStatus.On) {
        logger?.LogInformation($"Sony Cledis controller is not turned on (expected: {SonyCledisPowerStatus.On}, found: {power})");
        return;
    }

    // check if the HTPC picture mode is selected
    var currentPictureMode = await cledisClient.GetPictureModeAsync();
    if(currentPictureMode != SonyCledisPictureMode.Mode10) {
        logger?.LogWarning($"Sony Cledis controller uses wrong picture mode for switching light output (expected: {SonyCledisPictureMode.Mode10}, current: {currentPictureMode})");
        return;
    }
    await cledisClient.SetLightOutputMode(mode);
}