namespace Solfar.HtpcServer;

// \\.\DISPLAY1: (570, 1728)-(1664, 1109)
// \\.\DISPLAY2: (0, 0)-(3072, 1728)

// X = 1843, Y = 401, Screen = 1
// X = 1843, Y = 401, Screen = 1
// X = 1843, Y = 401, Screen = 1
// X = 2710, Y = 560, Screen = 1
// X = 3124, Y = 638, Screen = -1
// X = 3310, Y = 673, Screen = -1

using System.Runtime.InteropServices;

public static class Program {

    //--- Class Methods ---

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main(string[] args) {
        AllocConsole();

        foreach(var screen in Screen.AllScreens) {
            Console.WriteLine($"{screen.DeviceName}: ({screen.Bounds.Left}, {screen.Bounds.Top})-({screen.Bounds.Width}, {screen.Bounds.Height})");
        }

        // launch web API
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://*:5158");
        builder.Services
            .AddLogging(configure => configure
                .AddFilter("Default", LogLevel.Trace)
                .AddConsole()
                .AddFile("Logs/Solfar-Server-{Date}.log", LogLevel.Trace)
            )
            .Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Trace);
        var app = builder.Build();
        app.MapGet("/", () => "Hello World!");
        Task.Run(() => app.Run());


        // track mouse position
        //Task.Run(async () => {
        //    while(true) {
        //        // var mousePoint = new Point();
        //        // GetCursorPos(ref mousePoint);

        //        var mousePoint = Cursor.Position;

        //        // Now after calling the function, defPnt contains the coordinates which we can read
        //        Console.WriteLine($"X = {mousePoint.X}, Y = {mousePoint.Y}, Screen = {ConvertMousePointToScreenIndex(mousePoint)}");
        //        var s = Screen.FromPoint(mousePoint);
        //        Console.WriteLine($"DeviceName = {s.DeviceName}");
        //        await Task.Delay(1000);
        //    }
        //});

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }

    private static int ConvertMousePointToScreenIndex(Point mousePoint)
        => Screen.AllScreens.ToList().FindIndex(screen => screen.Bounds.Contains(mousePoint));
}