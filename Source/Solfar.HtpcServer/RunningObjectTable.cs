namespace OutOfProcDemo;

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

/// <summary>
/// Wrapper for the COM Running Object Table.
/// </summary>
/// <remarks>
/// See https://docs.microsoft.com/en-us/windows/desktop/api/objidl/nn-objidl-irunningobjecttable.
/// </remarks>
internal class RunningObjectTable : IDisposable {

    //--- Types ---
    private class RevokeRegistration : IDisposable {

        //--- Fields ---
        private readonly RunningObjectTable _rot;
        private readonly int _regCookie;

        //--- Constructors ---
        public RevokeRegistration(RunningObjectTable rot, int regCookie) {
            _rot = rot;
            _regCookie = regCookie;
        }

        //--- Methods ---
        public void Dispose() => _rot.Revoke(_regCookie);
    }

    private static class Ole32 {

        //--- Class Methods ---
        [DllImport(nameof(Ole32))]
        public static extern void CreateItemMoniker(
            [MarshalAs(UnmanagedType.LPWStr)] string lpszDelim,
            [MarshalAs(UnmanagedType.LPWStr)] string lpszItem,
            out IMoniker ppmk
        );

        [DllImport(nameof(Ole32))]
        public static extern void GetRunningObjectTable(
            int reserved,
            out IRunningObjectTable pprot
        );
    }

    //--- Fields ---
    private readonly IRunningObjectTable _rot;
    private bool _isDisposed;

    //--- Constructors ---
    public RunningObjectTable() => Ole32.GetRunningObjectTable(0, out _rot);

    //--- Methods ---
    public void Dispose() {
        if(_isDisposed) {
            return;
        }
        Marshal.ReleaseComObject(_rot);
        _isDisposed = true;
    }

    /// <summary>
    /// Attempts to register an item in the ROT.
    /// </summary>
    public IDisposable Register(string itemName, object obj) {
        const int ROTFLAGS_REGISTRATIONKEEPSALIVE = 1;
        var moniker = CreateMoniker(itemName);
        int regCookie = _rot.Register(ROTFLAGS_REGISTRATIONKEEPSALIVE, obj, moniker);
        return new RevokeRegistration(this, regCookie);
    }

    /// <summary>
    /// Attempts to retrieve an item from the ROT.
    /// </summary>
    public object GetObject(string itemName) {
        var mk = CreateMoniker(itemName);
        int hr = _rot.GetObject(mk, out object result);
        if(hr != 0) {
            Marshal.ThrowExceptionForHR(hr);
        }
        return result;
    }

    private void Revoke(int regCookie) => _rot.Revoke(regCookie);

    private IMoniker CreateMoniker(string itemName) {
        Ole32.CreateItemMoniker("!", itemName, out IMoniker moniker);
        return moniker;
    }
}
