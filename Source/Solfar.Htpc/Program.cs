using System.Diagnostics;
using HtmlDocument;
using RadiantPi.Sony.Cledis;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddSingleton(_ => new SonyCledisClientConfig {
        Host = "192.168.1.190",
        Port = 53595
    })
    .AddSingleton<ISonyCledis, SonyCledisClient>()
    .Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Trace);

var app = builder.Build();
app.MapGet("/", GetStatusAsync);
app.MapPost("/Go2D", SwitchTo2DAsync);
app.MapPost("/Go3D", SwitchTo3DAsync);
app.Run();

async Task<HtmlDoc> GetStatusAsync(ISonyCledis cledisClient, ILogger<Program>? logger) {
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

async Task<string> SwitchTo2DAsync(HttpContext contenxt, ISonyCledis cledisClient, ILogger<Program>? logger) {
    logger?.LogInformation($"{nameof(SwitchTo2DAsync)} called");
    var response = "Ok";

    // check if Sony C-LED is turned on
    var power = await cledisClient.GetPowerStatusAsync();
    if(power != SonyCledisPowerStatus.On) {
        response = $"Sony C-LED is not on (current: {power})";
        goto done;
    }

    // check if Dual-DisplayPort is the active input
    var input = await cledisClient.GetInputAsync();
    if(input != SonyCledisInput.DisplayPortBoth) {
        response = $"Sony C-LED active input is not Dual-DispayPort (current: {input})";
        goto done;
    }

    // check current display status
    var displayStatus = DisplayApi.GetCurrentDisplayStatus();
    switch(displayStatus.NVidiaMode) {
    default:
    case NVidiaMode.Undefined:
        response = $"HTPC NVidia display mode could not be identified (mode: {displayStatus.NVidiaMode})";
        break;
    case NVidiaMode.Disabled:
        response = $"HTPC NVidia adapter has not displays attached";
        break;
    case NVidiaMode.Surround3D:
        logger?.LogInformation($"Disabling NVIDIA 3D surround mode");

        // NOTE: need to switch NVidia surround mode off before toggling Sony C-LED 3D mode
        await SwitchNVidiaSurroundProfileAsync("configs/individual-3D.cfg", logger);
        await Task.Delay(TimeSpan.FromSeconds(5));
        goto case NVidiaMode.Individual3DTiles;
    case NVidiaMode.Individual3DTiles:
        logger?.LogInformation($"Disabling Sony C-LED 3D mode");

        // switch Sony C-LED to 2D mode
        await cledisClient.Set2D3DModeAsync(SonyCledis2D3DMode.Mode2D);
        await Task.Delay(TimeSpan.FromSeconds(15));
        goto case NVidiaMode.IndividualHdTiles;
    case NVidiaMode.IndividualHdTiles:
        logger?.LogInformation($"Enabling NVIDIA 4K surround mode");

        // activate NVidia 4K surround mode
        await SwitchNVidiaSurroundProfileAsync("configs/surround-4K.cfg", logger);
        await Task.Delay(TimeSpan.FromSeconds(5));
        goto case NVidiaMode.Surround4K;
    case NVidiaMode.Surround4K:

        // nothing to do
        break;
    }

    // check if audio needs to be enabled
    displayStatus = DisplayApi.GetCurrentDisplayStatus();
    if(!displayStatus.AudioEnabled) {
        logger?.LogInformation($"Enabling 4K display profile");
        await SwitchMonitorProfileAsync("4K", logger);
    }
done:
    logger?.LogInformation($"RESPONSE: {response}");
    return response;
}

async Task<string> SwitchTo3DAsync(HttpContext contenxt, ISonyCledis cledisClient, ILogger<Program>? logger) {
    logger?.LogInformation($"{nameof(SwitchTo3DAsync)} called");
    var response = "Ok";

    // check if Sony C-LED is turned on
    var power = await cledisClient.GetPowerStatusAsync();
    if(power != SonyCledisPowerStatus.On) {
        response = $"Sony C-LED is not on (current: {power})";
        goto done;
    }

    // check if Dual-DisplayPort is the active input
    var input = await cledisClient.GetInputAsync();
    if(input != SonyCledisInput.DisplayPortBoth) {
        response = $"Sony C-LED active input is not Dual-DispayPort (current: {input})";
        goto done;
    }

    // check current display status
    var displayStatus = DisplayApi.GetCurrentDisplayStatus();
    switch(displayStatus.NVidiaMode) {
    default:
    case NVidiaMode.Undefined:
        response = $"HTPC NVidia display mode could not be identified (mode: {displayStatus.NVidiaMode})";
        break;
    case NVidiaMode.Disabled:
        response = $"HTPC NVidia adapter has not displays attached";
        break;
    case NVidiaMode.Surround4K:
        logger?.LogInformation($"Disabling NVIDIA 4K surround mode");

        // NOTE: need to switch NVidia surround mode off before toggling Sony C-LED 3D mode
        await SwitchNVidiaSurroundProfileAsync("configs/individual-4xHD.cfg", logger);
        await Task.Delay(TimeSpan.FromSeconds(5));
        goto case NVidiaMode.IndividualHdTiles;
    case NVidiaMode.IndividualHdTiles:
        logger?.LogInformation($"Enabling Sony C-LED 3D mode");

        // switch Sony C-LED to 3D mode
        await cledisClient.Set2D3DModeAsync(SonyCledis2D3DMode.Mode3D);
        await Task.Delay(TimeSpan.FromSeconds(15));
        goto case NVidiaMode.Individual3DTiles;
    case NVidiaMode.Individual3DTiles:
        logger?.LogInformation($"Enabling NVIDIA 3D surround mode");

        // activate NVidia 3D surround mode
        await SwitchNVidiaSurroundProfileAsync("configs/surround-3D.cfg",  logger);
        await Task.Delay(TimeSpan.FromSeconds(5));
        goto case NVidiaMode.Surround3D;
    case NVidiaMode.Surround3D:

        // nothing to do
        break;
    }

    // check if audio needs to be enabled
    displayStatus = DisplayApi.GetCurrentDisplayStatus();
    if(!displayStatus.AudioEnabled) {
        logger?.LogInformation($"Enabling 3D display profile");
        await SwitchMonitorProfileAsync("3D", logger);
    }
done:
    logger?.LogInformation($"RESPONSE: {response}");
    return response;
}

async Task SwitchNVidiaSurroundProfileAsync(string profile, ILogger? logger) {
    logger?.LogInformation($"Switch NVidia surround profile: {profile}");
    using var process = Process.Start(new ProcessStartInfo() {
        FileName = @"C:\Projects\NVIDIAInfo-v1.7.0\NVIDIAInfo.exe",
        Arguments = $"load \"{profile}\"",
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = true
    });
    if(process is not null) {
        await process.WaitForExitAsync();
        if(process.ExitCode != 0) {
            logger?.LogWarning($"{nameof(SwitchNVidiaSurroundProfileAsync)}: exited with code: {process.ExitCode}");
        }
    } else {
        logger?.LogError($"{nameof(SwitchNVidiaSurroundProfileAsync)}: unable to start process");
    }
}

async Task SwitchMonitorProfileAsync(string profile, ILogger? logger) {
    logger?.LogInformation($"Switch monitor profile: {profile}");
    using var process = Process.Start(new ProcessStartInfo() {
        FileName = @"C:\Program Files (x86)\DisplayFusion\DisplayFusionCommand.exe",
        Arguments = $"-monitorloadprofile \"{profile}\"",
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = true
    });
    if(process is not null) {
        await process.WaitForExitAsync();
        if(process.ExitCode != 0) {
            logger?.LogWarning($"{nameof(SwitchMonitorProfileAsync)}: exited with code: {process.ExitCode}");
        }
    } else {
        logger?.LogError($"{nameof(SwitchMonitorProfileAsync)}: unable to start process");
    }
}