using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using BulkExtensionChanger.Services;

namespace BulkExtensionChanger.Shell;

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;
}

[ComImport]
[Guid("00000122-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDropTarget
{
    [PreserveSig]
    int DragEnter(
        [In, MarshalAs(UnmanagedType.Interface)] IDataObject pDataObj,
        [In] uint grfKeyState,
        [In] POINT pt,
        [In, Out] ref uint pdwEffect);

    [PreserveSig]
    int DragOver(
        [In] uint grfKeyState,
        [In] POINT pt,
        [In, Out] ref uint pdwEffect);

    [PreserveSig]
    int DragLeave();

    [PreserveSig]
    int Drop(
        [In, MarshalAs(UnmanagedType.Interface)] IDataObject pDataObj,
        [In] uint grfKeyState,
        [In] POINT pt,
        [In, Out] ref uint pdwEffect);
}

[ComImport]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(
        [In] IntPtr pUnkOuter,
        [In] ref Guid riid,
        [Out] out IntPtr ppvObject);

    [PreserveSig]
    int LockServer([In, MarshalAs(UnmanagedType.Bool)] bool fLock);
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class ExplorerDropTarget : IDropTarget
{
    private const uint DROPEFFECT_COPY = 1;
    private const short CF_HDROP = 15;
    private readonly Action<IReadOnlyList<string>> _onFilesDropped;

    public ExplorerDropTarget(Action<IReadOnlyList<string>> onFilesDropped)
    {
        _onFilesDropped = onFilesDropped;
    }

    public int DragEnter(IDataObject pDataObj, uint grfKeyState, POINT pt, ref uint pdwEffect)
    {
        pdwEffect = DROPEFFECT_COPY;
        return 0;
    }

    public int DragOver(uint grfKeyState, POINT pt, ref uint pdwEffect)
    {
        pdwEffect = DROPEFFECT_COPY;
        return 0;
    }

    public int DragLeave() => 0;

    public int Drop(IDataObject pDataObj, uint grfKeyState, POINT pt, ref uint pdwEffect)
    {
        pdwEffect = DROPEFFECT_COPY;

        var files = ExtractDropFiles(pDataObj);
        _onFilesDropped(files);
        return 0;
    }

    private static List<string> ExtractDropFiles(IDataObject dataObj)
    {
        var result = new List<string>();
        var format = new FORMATETC
        {
            cfFormat = CF_HDROP,
            ptd = IntPtr.Zero,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = TYMED.TYMED_HGLOBAL
        };

        dataObj.GetData(ref format, out STGMEDIUM medium);
        try
        {
            if (medium.unionmember == IntPtr.Zero)
                return result;

            uint count = DragQueryFileW(medium.unionmember, 0xFFFFFFFF, null, 0);
            for (uint i = 0; i < count; i++)
            {
                uint charLen = DragQueryFileW(medium.unionmember, i, null, 0);
                if (charLen == 0)
                    continue;

                var sb = new StringBuilder((int)charLen + 1);
                if (DragQueryFileW(medium.unionmember, i, sb, (uint)sb.Capacity) > 0)
                    result.Add(sb.ToString());
            }
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }

        return result;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class DropTargetClassFactory : IClassFactory
{
    private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
    private const int E_NOINTERFACE = unchecked((int)0x80004002);
    private readonly Action<IReadOnlyList<string>> _onFilesDropped;

    public DropTargetClassFactory(Action<IReadOnlyList<string>> onFilesDropped)
    {
        _onFilesDropped = onFilesDropped;
    }

    public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;
        if (pUnkOuter != IntPtr.Zero)
            return CLASS_E_NOAGGREGATION;

        var target = new ExplorerDropTarget(_onFilesDropped);
        IntPtr unk = Marshal.GetIUnknownForObject(target);
        try
        {
            int hr = Marshal.QueryInterface(unk, ref riid, out ppvObject);
            return hr == 0 ? 0 : E_NOINTERFACE;
        }
        finally
        {
            Marshal.Release(unk);
        }
    }

    public int LockServer(bool fLock) => 0;
}

public sealed class ComDropServer : IDisposable
{
    private const uint CLSCTX_LOCAL_SERVER = 4;
    private const uint REGCLS_MULTIPLEUSE = 1;

    private uint _cookie;
    private DropTargetClassFactory? _factory;

    public void Start(Action<IReadOnlyList<string>> onFilesDropped)
    {
        var clsid = ShellRegistrationService.DropTargetClsid;
        _factory = new DropTargetClassFactory(onFilesDropped);

        int hr = CoRegisterClassObject(
            ref clsid,
            _factory,
            CLSCTX_LOCAL_SERVER,
            REGCLS_MULTIPLEUSE,
            out _cookie);

        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);
    }

    public void Dispose()
    {
        if (_cookie != 0)
        {
            CoRevokeClassObject(_cookie);
            _cookie = 0;
        }
        _factory = null;
    }

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        ref Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext,
        uint flags,
        out uint lpdwRegister);

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(uint dwRegister);
}
