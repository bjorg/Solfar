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

    // check 3D-4K mode is disabled
    var dualDisplayPort3DMode = await cledisClient.GetDualDisplayPort3D4KModeAsync();
    if(dualDisplayPort3DMode != SonyCledisDualDisplayPort3D4KMode.Off) {

        // TODO: disable NVidia surround mode

        // disable Sony C-LED 4K-3D mode
        await cledisClient.SetDualDisplayPort3D4KModeAsync(SonyCledisDualDisplayPort3D4KMode.Off);

        // TODO: reactivate HDMI audio output
    }

    // 4. Is NVidia Surround 4K profile active?
    //  NO => Activate NVidia Surround 4K profile
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

    // check 3D-4K mode is enabled
    var dualDisplayPort3DMode = await cledisClient.GetDualDisplayPort3D4KModeAsync();
    if(dualDisplayPort3DMode != SonyCledisDualDisplayPort3D4KMode.On) {

        // TODO: disable NVidia surround mode

        // enable Sony C-LED 4K-3D mode
        await cledisClient.SetDualDisplayPort3D4KModeAsync(SonyCledisDualDisplayPort3D4KMode.Off);

        // TODO: reactivate HDMI audio output
    }

    // 4. Is NVidia Surround 3D profile active?
    //  NO => Activate NVidia Surround 3D profile
    return "Ok";
}