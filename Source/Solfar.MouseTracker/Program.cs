using System.Linq;
using System.Runtime.InteropServices;

namespace Solfar.MouseTracker;

static class Program {
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main() {
        // ApplicationConfiguration.Initialize();
        // Application.Run(new Form1());

        AllocConsole();
        while(true) {
            // var mousePoint = new Point();
            // GetCursorPos(ref mousePoint);

            var mousePoint = Cursor.Position;

            // Now after calling the function, defPnt contains the coordinates which we can read
            Console.WriteLine($"X = {mousePoint.X}, Y = {mousePoint.Y}, Screen = {ConvertMousePointToScreenIndex(mousePoint)}");
            Thread.Sleep(200);
        }
    }

    private static int ConvertMousePointToScreenIndex(Point mousePoint)
        => Screen.AllScreens.ToList().FindIndex(screen => screen.Bounds.Contains(mousePoint));

    // We need to use unmanaged code
    [DllImport("user32.dll")]

    // GetCursorPos() makes everything possible
    static extern bool GetCursorPos(ref Point lpPoint);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();
}