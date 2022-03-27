using System.Runtime.InteropServices;

public record ResolutionInfo(uint Width, uint Height, uint RefreshRate);
public record DisplayInfo(uint Id, string Name, string Description, ResolutionInfo Resolution, string RegistryKey);

public enum NVidiaMode {
    Undefined,
    Disabled,
    Individual3DTiles,
    Surround3D,
    IndividualHdTiles,
    Surround4K
}

public static class DisplayApi {

    //--- Constants ---
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const uint EDD_GET_DEVICE_INTERFACE_NAME = 1;

    //--- Methods ---
    public static IEnumerable<DisplayInfo> GetDisplays(bool onlyAttachedDisplays = true) {

        // enumerate display adapters until EnumDisplayDevices returns false
        DISPLAY_DEVICE displayDevice = new();
        displayDevice.cb = Marshal.SizeOf(displayDevice);
        for(uint adapterId = 0; EnumDisplayDevices(null, adapterId, ref displayDevice, EDD_GET_DEVICE_INTERFACE_NAME); ++adapterId) {

            // check if this display adapter is attached to the desktop
            DEVMODE deviceMode = new();
            deviceMode.dmSize = (ushort)Marshal.SizeOf(deviceMode);
            if(displayDevice.StateFlags.HasFlag(DISPLAY_DEVICE_STATE_FLAGS.AttachedToDesktop)) {
                EnumDisplaySettings(displayDevice.DeviceName, ENUM_CURRENT_SETTINGS, ref deviceMode);
                yield return new(adapterId, displayDevice.DeviceName, displayDevice.DeviceString, new(deviceMode.dmPelsWidth, deviceMode.dmPelsHeight, deviceMode.dmDisplayFrequency), displayDevice.DeviceKey);
            } else if(!onlyAttachedDisplays) {
                yield return new(adapterId, displayDevice.DeviceName, displayDevice.DeviceString, new(0, 0, 0), displayDevice.DeviceKey);
            }

            // reset data structure for next iteration
            displayDevice = new();
            displayDevice.cb = Marshal.SizeOf(displayDevice);
        }
    }

    public static (NVidiaMode NVidiaMode, bool AudioEnabled) GetCurrentDisplayStatus() {
        var displays = GetDisplays();

        // audio is based on a display being attached to the built-in Intel graphics adapter
        var audioEnabled = displays.Any(displayInfo => displayInfo.Description == "Intel(R) UHD Graphics 770");

        // video is based on the configuraiton of the attached NVidia displays
        var nvidiaMode = NVidiaMode.Undefined;
        var nvidiaDisplays = displays.Where(displayInfo => displayInfo.Description == "NVIDIA GeForce RTX 3080 Ti").ToList();
        switch(nvidiaDisplays.Count) {
        case 0:
            nvidiaMode = NVidiaMode.Disabled;
            break;
        case 1:

            // there are 2 defined NVidia surround modes, which appear as a single display
            nvidiaMode = nvidiaDisplays[0].Resolution switch {
                (Width: 7680, Height: 2160, RefreshRate: 60) => NVidiaMode.Surround3D,
                (Width: 3840, Height: 2160, RefreshRate: 120) => NVidiaMode.Surround4K,
                _ => NVidiaMode.Undefined
            };
            break;
        case 4:

            // when NVidia surround mode is disabled, the individuals displays are visibile in the enumeration
            if(nvidiaDisplays.All(display => display.Resolution is (Width: 1920, Height: 2160, RefreshRate: 60))) {
                nvidiaMode = NVidiaMode.Individual3DTiles;
            } else if(nvidiaDisplays.All(display => display.Resolution is (Width: 1920, Height: 1080, RefreshRate: 120))) {
                nvidiaMode = NVidiaMode.IndividualHdTiles;
            }
            break;
        }
        return (nvidiaMode, audioEnabled);
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("User32.dll", CharSet = CharSet.Ansi)]
    private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);
}
