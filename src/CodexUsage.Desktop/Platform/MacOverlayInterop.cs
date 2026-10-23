using System.Runtime.InteropServices;

namespace CodexUsage.Desktop.Platform;

internal static class MacOverlayInterop
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private static readonly nuint CanJoinAllSpaces = (nuint)1 << 0;
    private static readonly nuint Transient = (nuint)1 << 3;
    private static readonly nuint FullScreenAuxiliary = (nuint)1 << 8;

    private static nint _hudWindowClass;
    // Keep the reverse-P/Invoke delegate alive for the lifetime of the native class.
    private static readonly ConstrainFrameCallback ConstrainHudFrame = (_, _, frame, _) => frame;

    public static void ConfigureHud(nint window)
    {
        if (!OperatingSystem.IsMacOS() || window == nint.Zero)
        {
            return;
        }

        AllowExactTitlebarPlacement(window);
        ConfigureCompanion(window);
        var setHidesOnDeactivate = sel_registerName("setHidesOnDeactivate:");
        var setExcludedFromWindowsMenu = sel_registerName("setExcludedFromWindowsMenu:");
        SendBoolArgument(window, setHidesOnDeactivate, 0);
        SendBoolArgument(window, setExcludedFromWindowsMenu, 1);
        SendBoolArgument(window, sel_registerName("setHasShadow:"), 0);
        SendBoolArgument(window, sel_registerName("setOpaque:"), 0);
    }

    public static void ConfigureCompanion(nint window)
    {
        if (!OperatingSystem.IsMacOS() || window == nint.Zero) return;
        var collectionBehavior = sel_registerName("collectionBehavior");
        var setCollectionBehavior = sel_registerName("setCollectionBehavior:");
        var existingBehavior = SendUnsignedInteger(window, collectionBehavior);
        SendUnsignedIntegerArgument(
            window,
            setCollectionBehavior,
            (existingBehavior & ~((nuint)1 << 7)) | CanJoinAllSpaces | Transient | FullScreenAuxiliary);
    }

    private static void AllowExactTitlebarPlacement(nint window)
    {
        // AppKit otherwise constrains the helper below the menu-bar working area,
        // even while Codex occupies the whole fullscreen display. Apply a native
        // subclass to this helper instance only; Codex and other windows are untouched.
        if (_hudWindowClass == nint.Zero)
        {
            _hudWindowClass = objc_getClass("CodexUsageTitlebarHudWindow");
            if (_hudWindowClass == nint.Zero)
            {
                var subclass = objc_allocateClassPair(object_getClass(window), "CodexUsageTitlebarHudWindow", 0);
                if (subclass == nint.Zero) return;
                var method = sel_registerName("constrainFrameRect:toScreen:");
                if (!class_addMethod(subclass, method,
                    Marshal.GetFunctionPointerForDelegate(ConstrainHudFrame),
                    "{CGRect={CGPoint=dd}{CGSize=dd}}@:{CGRect={CGPoint=dd}{CGSize=dd}}@"))
                {
                    objc_disposeClassPair(subclass);
                    return;
                }
                objc_registerClassPair(subclass);
                _hudWindowClass = subclass;
            }
        }
        if (object_getClass(window) != _hudWindowClass) object_setClass(window, _hudWindowClass);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint { public readonly double X; public readonly double Y; }
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize { public readonly double Width; public readonly double Height; }
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect { public readonly NativePoint Origin; public readonly NativeSize Size; }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NativeRect ConstrainFrameCallback(nint window, nint selector, NativeRect frame, nint screen);

    [DllImport(ObjectiveCLibrary)]
    private static extern nint object_getClass(nint value);
    [DllImport(ObjectiveCLibrary)]
    private static extern nint object_setClass(nint value, nint nativeClass);
    [DllImport(ObjectiveCLibrary)]
    private static extern nint objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(ObjectiveCLibrary)]
    private static extern nint objc_allocateClassPair(nint superclass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nuint extraBytes);
    [DllImport(ObjectiveCLibrary)]
    private static extern void objc_registerClassPair(nint nativeClass);
    [DllImport(ObjectiveCLibrary)]
    private static extern void objc_disposeClassPair(nint nativeClass);
    [DllImport(ObjectiveCLibrary)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool class_addMethod(nint nativeClass, nint selector, nint implementation,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

    [DllImport(ObjectiveCLibrary)]
    private static extern nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern nuint SendUnsignedInteger(nint receiver, nint selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendUnsignedIntegerArgument(nint receiver, nint selector, nuint value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendBoolArgument(nint receiver, nint selector, byte value);
}
