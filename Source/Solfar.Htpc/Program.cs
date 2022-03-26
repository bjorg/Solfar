using System.Diagnostics;
using HtmlDocument;
using RadiantPi.Sony.Cledis;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddSingleton(_ => new SonyCledisClientConfig {
        Host = "192.168.1.190",
        Port = 53595
    })
    .AddSingleton<ISonyCledis, SonyCledisClient>();

var app = builder.Build();
app.MapGet("/", GetStatusAsync);
app.MapPost("/Go4K", SwitchTo4KAsync);
app.MapPost("/Go3D", SwitchTo3DAsync);
app.Run();

async Task<HtmlDoc> GetStatusAsync(ISonyCledis cledisClient) {
    var power = await cledisClient.GetPowerStatusAsync();
    var input = (power == SonyCledisPowerStatus.On)
        ? await cledisClient.GetInputAsync()
        : SonyCledisInput.Undefined;
    var mode4k3d = (power == SonyCledisPowerStatus.On)
        ? await cledisClient.GetDualDisplayPort3D4KModeAsync()
        : SonyCledisDualDisplayPort3D4KMode.Undefined;
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
                .Elem("li", $"4K-3D Mode: {mode4k3d}")
            .End()
            .Elem("h2", "NVidia Surround Status")
            .Elem("p", "TODO")
            .Elem("h2", "JRiver Status")
            .Elem("p", "TODO")
        .End();
}

async Task<string> SwitchTo4KAsync(HttpContext contenxt, ISonyCledis cledisClient) {

    // check if Sony C-LED is turned on
    var power = await cledisClient.GetPowerStatusAsync();
    if(power != SonyCledisPowerStatus.On) {
        return $"Sony C-LED is not on (current: {power})";
    }

    // check if Dual-DisplayPort is the active input
    var input = await cledisClient.GetInputAsync();
    if(input != SonyCledisInput.DisplayPortBoth) {
        return $"Sony C-LED active input is not Dual-DispayPort (current: {input})";
    }

    // check if Sony C-LED 3D mode is enabled
    var mode2D3D = await cledisClient.Get2D3DModeAsync();
    if(mode2D3D == SonyCledis2D3DMode.Select3D) {

        // NOTE: need to switch NVidia surround mode off before toggling Sony C-LED 3D mode
        await SwitchSurroundProfile("configs/individual-3D.cfg");

        // switch Sony C-LED to 2D mode
        await cledisClient.Set2D3DModeAsync(SonyCledis2D3DMode.Select2D);
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    // activate NVidia 4K surround mode
    await SwitchSurroundProfile("configs/surround-4K.cfg");
    return "Ok";
}

async Task<string> SwitchTo3DAsync(HttpContext contenxt, ISonyCledis cledisClient) {

    // check if Sony C-LED is turned on
    var power = await cledisClient.GetPowerStatusAsync();
    if(power != SonyCledisPowerStatus.On) {
        return $"Sony C-LED is not on (current: {power})";
    }

    // check if Dual-DisplayPort is the active input
    var input = await cledisClient.GetInputAsync();
    if(input != SonyCledisInput.DisplayPortBoth) {
        return $"Sony C-LED active input is not Dual-DispayPort (current: {input})";
    }

    // check if Sony C-LED 2D mode is enabled
    var mode2D3D = await cledisClient.Get2D3DModeAsync();
    if(mode2D3D == SonyCledis2D3DMode.Select2D) {

        // NOTE: need to switch NVidia surround mode off before toggling Sony C-LED 3D mode
        await SwitchSurroundProfile("configs/individual-4xHD.cfg");

        // switch Sony C-LED to 3D mode
        await cledisClient.Set2D3DModeAsync(SonyCledis2D3DMode.Select3D);
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    // activate NVidia 3D surround mode
    await SwitchSurroundProfile("configs/surround-3D.cfg");
    return "Ok";
}

async Task SwitchSurroundProfile(string profile) {
    using var process = Process.Start(new ProcessStartInfo() {
        FileName = @"C:\Projects\NVIDIAInfo-v1.7.0\NVIDIAInfo.exe",
        Arguments = $"load \"{profile}\"",
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = true
    });
    if(process is not null) {
        await process.WaitForExitAsync();
        if(process.ExitCode != 0) {
            Console.WriteLine($"WARNING: {nameof(SwitchSurroundProfile)} - exited with code: {process.ExitCode}");
        }
    } else {
        Console.WriteLine($"ERROR: {nameof(SwitchSurroundProfile)} - unable to start process");
    }
}