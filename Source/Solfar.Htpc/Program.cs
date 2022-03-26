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
app.MapPost("/Go4K", SwitchTo4KAsync);
app.MapPost("/Go3D", SwitchTo3DAsync);
app.Run();

async Task<HtmlDoc> GetStatusAsync(ISonyCledis cledisClient, ILogger<Program>? logger) {
    logger?.LogInformation($"{nameof(GetStatusAsync)} called");
    var power = await cledisClient.GetPowerStatusAsync();
    var input = (power == SonyCledisPowerStatus.On)
        ? await cledisClient.GetInputAsync()
        : SonyCledisInput.Undefined;
    var mode3d = (power == SonyCledisPowerStatus.On)
        ? await cledisClient.Get2D3DModeAsync()
        : SonyCledis2D3DMode.Undefined;
    return new HtmlDoc()
        .Attr("lang", "en")
        .Head()
            .Title("HTPC Status")
        .End()
        .Body()
            .Elem("h1", "HTPC Status")
            .Elem("h2", "Sony C-LED")
            .Begin("ul")
                .Elem("li", $"Power: {power}")
                .Elem("li", $"Input: {input}")
                .Elem("li", $"3D Mode: {mode3d}")
            .End()
        .End();
}

async Task<string> SwitchTo4KAsync(HttpContext contenxt, ISonyCledis cledisClient, ILogger<Program>? logger) {
    logger?.LogInformation($"{nameof(SwitchTo4KAsync)} called");

    // check if Sony C-LED is turned on
    var power = await cledisClient.GetPowerStatusAsync();
    if(power != SonyCledisPowerStatus.On) {
        var response = $"Sony C-LED is not on (current: {power})";
        logger?.LogInformation($"RESPONSE: {response}");
        return response;
    }

    // check if Dual-DisplayPort is the active input
    var input = await cledisClient.GetInputAsync();
    if(input != SonyCledisInput.DisplayPortBoth) {
        var response = $"Sony C-LED active input is not Dual-DispayPort (current: {input})";
        logger?.LogInformation($"RESPONSE: {response}");
        return response;
    }

    // check if Sony C-LED 3D mode is enabled
    var mode2D3D = await cledisClient.Get2D3DModeAsync();
    if(mode2D3D == SonyCledis2D3DMode.Mode3D) {

        // NOTE: need to switch NVidia surround mode off before toggling Sony C-LED 3D mode
        await SwitchSurroundProfile("configs/individual-3D.cfg", logger);
        await Task.Delay(TimeSpan.FromSeconds(5));

        // switch Sony C-LED to 2D mode
        await cledisClient.Set2D3DModeAsync(SonyCledis2D3DMode.Mode2D);
        await Task.Delay(TimeSpan.FromSeconds(10));
    }

    // activate NVidia 4K surround mode
    await SwitchSurroundProfile("configs/surround-4K.cfg", logger);
    logger?.LogInformation($"{nameof(SwitchTo4KAsync)} finished");
    return "Ok";
}

async Task<string> SwitchTo3DAsync(HttpContext contenxt, ISonyCledis cledisClient, ILogger<Program>? logger) {
    logger?.LogInformation($"{nameof(SwitchTo3DAsync)} called");

    // check if Sony C-LED is turned on
    var power = await cledisClient.GetPowerStatusAsync();
    if(power != SonyCledisPowerStatus.On) {
        var response = $"Sony C-LED is not on (current: {power})";
        logger?.LogInformation($"RESPONSE: {response}");
        return response;
    }

    // check if Dual-DisplayPort is the active input
    var input = await cledisClient.GetInputAsync();
    if(input != SonyCledisInput.DisplayPortBoth) {
        var response = $"Sony C-LED active input is not Dual-DispayPort (current: {input})";
        logger?.LogInformation($"RESPONSE: {response}");
        return response;
    }

    // check if Sony C-LED 2D mode is enabled
    var mode2D3D = await cledisClient.Get2D3DModeAsync();
    if(mode2D3D == SonyCledis2D3DMode.Mode2D) {

        // NOTE: need to switch NVidia surround mode off before toggling Sony C-LED 3D mode
        await SwitchSurroundProfile("configs/individual-4xHD.cfg", logger);
        await Task.Delay(TimeSpan.FromSeconds(5));

        // switch Sony C-LED to 3D mode
        await cledisClient.Set2D3DModeAsync(SonyCledis2D3DMode.Mode3D);
        await Task.Delay(TimeSpan.FromSeconds(10));
    }

    // activate NVidia 3D surround mode
    await SwitchSurroundProfile("configs/surround-3D.cfg",  logger);
    logger?.LogInformation($"{nameof(SwitchTo3DAsync)} finished");
    return "Ok";
}

async Task SwitchSurroundProfile(string profile, ILogger? logger) {
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
            logger?.LogWarning($"{nameof(SwitchSurroundProfile)}: exited with code: {process.ExitCode}");
        }
    } else {
        logger?.LogError($"{nameof(SwitchSurroundProfile)}: unable to start process");
    }
}