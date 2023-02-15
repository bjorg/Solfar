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

using System.Runtime.InteropServices;

[Flags]
public enum DISPLAY_DEVICE_STATE_FLAGS : int {

    /// <summary>The device is part of the desktop.</summary>
    AttachedToDesktop = 0x1,
    MultiDriver = 0x2,

    /// <summary>The device is part of the desktop.</summary>
    PrimaryDevice = 0x4,

    /// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
    MirroringDriver = 0x8,

    /// <summary>The device is VGA compatible.</summary>
    VGACompatible = 0x10,

    /// <summary>The device is removable; it cannot be the primary display.</summary>
    Removable = 0x20,

    /// <summary>The device has more display modes than its output devices support.</summary>
    ModesPruned = 0x8000000,
    Remote = 0x4000000,
    Disconnect = 0x2000000
}

[StructLayout(LayoutKind.Sequential)]
public struct POINTL {

    //--- Fields ---

    [MarshalAs(UnmanagedType.I4)]
    public int x;

    [MarshalAs(UnmanagedType.I4)]
    public int y;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct DEVMODE {

    //--- Fields ---

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string dmDeviceName;

    [MarshalAs(UnmanagedType.U2)]
    public UInt16 dmSpecVersion;

    [MarshalAs(UnmanagedType.U2)]
    public UInt16 dmDriverVersion;

    [MarshalAs(UnmanagedType.U2)]
    public UInt16 dmSize;

    [MarshalAs(UnmanagedType.U2)]
    public UInt16 dmDriverExtra;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmFields;

    public POINTL dmPosition;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmDisplayOrientation;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmDisplayFixedOutput;

    [MarshalAs(UnmanagedType.I2)]
    public Int16 dmColor;

    [MarshalAs(UnmanagedType.I2)]
    public Int16 dmDuplex;

    [MarshalAs(UnmanagedType.I2)]
    public Int16 dmYResolution;

    [MarshalAs(UnmanagedType.I2)]
    public Int16 dmTTOption;

    [MarshalAs(UnmanagedType.I2)]
    public Int16 dmCollate;

    // CCHDEVICENAME = 32 = 0x50
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string dmFormName;
    // Also can be defined as
    //[MarshalAs(UnmanagedType.ByValArray,
    // SizeConst = 32, ArraySubType = UnmanagedType.U1)]
    //public Byte[] dmFormName;

    [MarshalAs(UnmanagedType.U2)]
    public UInt16 dmLogPixels;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmBitsPerPel;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmPelsWidth;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmPelsHeight;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmDisplayFlags;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmDisplayFrequency;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmICMMethod;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmICMIntent;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmMediaType;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmDitherType;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmReserved1;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmReserved2;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmPanningWidth;

    [MarshalAs(UnmanagedType.U4)]
    public UInt32 dmPanningHeight;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct DISPLAY_DEVICE {

    //--- Fields ---
    [MarshalAs(UnmanagedType.U4)]
    public int cb;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;

    [MarshalAs(UnmanagedType.U4)]
    public DISPLAY_DEVICE_STATE_FLAGS StateFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceID;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;
}
