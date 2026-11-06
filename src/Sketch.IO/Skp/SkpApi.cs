using System.Reflection;
using System.Runtime.InteropServices;

namespace Sketch.IO.Skp;

/// <summary>
/// Calls into the SketchUp C API (SketchUpAPI.dll) using the function signatures from Trimble's public
/// "SketchUp C API" documentation. All *Ref types are opaque handles (a struct with one pointer), passed as IntPtr.
/// The DLL is not bundled: it comes from the official SDK or from the user's SketchUp installation. It is only
/// needed for files older than SketchUp 2021 (newer files are read by <see cref="SkpNativeReader"/>).
/// </summary>
internal static class SkpApi
{
    private const string Lib = "SketchUpAPI";
    public const int Ok = 0; // SU_ERROR_NONE

    [StructLayout(LayoutKind.Sequential)]
    public struct Point3D
    {
        public double X, Y, Z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Transformation
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public double[] Values;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Color
    {
        public byte Red, Green, Blue, Alpha;
    }

    private static string? _dllPath;
    private static bool _resolverSet;

    /// <summary>Points the DllImports below at a specific SketchUpAPI.dll (its folder holds its dependencies).</summary>
    public static void UseLibrary(string path)
    {
        _dllPath = path;
        if (_resolverSet) return;
        NativeLibrary.SetDllImportResolver(typeof(SkpApi).Assembly, Resolve);
        _resolverSet = true;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string? path);

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Lib || _dllPath is null) return IntPtr.Zero;
        // Dependencies of SketchUpAPI.dll sit next to it.
        SetDllDirectory(Path.GetDirectoryName(_dllPath));
        try
        {
            return NativeLibrary.Load(_dllPath);
        }
        finally
        {
            SetDllDirectory(null);
        }
    }

    [DllImport(Lib)] public static extern void SUInitialize();
    [DllImport(Lib)] public static extern void SUTerminate();

    [DllImport(Lib)] public static extern int SUModelCreateFromFile(ref IntPtr model, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
    [DllImport(Lib)] public static extern int SUModelRelease(ref IntPtr model);
    [DllImport(Lib)] public static extern int SUModelGetEntities(IntPtr model, ref IntPtr entities);
    [DllImport(Lib)] public static extern int SUModelGetNumLayers(IntPtr model, out nuint count);
    [DllImport(Lib)] public static extern int SUModelGetLayers(IntPtr model, nuint len, [Out] IntPtr[] layers, out nuint count);

    [DllImport(Lib)] public static extern int SUEntitiesGetNumFaces(IntPtr entities, out nuint count);
    [DllImport(Lib)] public static extern int SUEntitiesGetFaces(IntPtr entities, nuint len, [Out] IntPtr[] faces, out nuint count);
    [DllImport(Lib)] public static extern int SUEntitiesGetNumEdges(IntPtr entities, [MarshalAs(UnmanagedType.U1)] bool standaloneOnly, out nuint count);
    [DllImport(Lib)] public static extern int SUEntitiesGetEdges(IntPtr entities, [MarshalAs(UnmanagedType.U1)] bool standaloneOnly, nuint len, [Out] IntPtr[] edges, out nuint count);
    [DllImport(Lib)] public static extern int SUEntitiesGetNumGroups(IntPtr entities, out nuint count);
    [DllImport(Lib)] public static extern int SUEntitiesGetGroups(IntPtr entities, nuint len, [Out] IntPtr[] groups, out nuint count);
    [DllImport(Lib)] public static extern int SUEntitiesGetNumInstances(IntPtr entities, out nuint count);
    [DllImport(Lib)] public static extern int SUEntitiesGetInstances(IntPtr entities, nuint len, [Out] IntPtr[] instances, out nuint count);

    [DllImport(Lib)] public static extern int SUEntitiesGetNumImages(IntPtr entities, out nuint count);
    [DllImport(Lib)] public static extern int SUEntitiesGetImages(IntPtr entities, nuint len, [Out] IntPtr[] images, out nuint count);

    [DllImport(Lib)] public static extern int SUFaceGetOuterLoop(IntPtr face, ref IntPtr loop);
    [DllImport(Lib)] public static extern int SUFaceGetNumInnerLoops(IntPtr face, out nuint count);
    [DllImport(Lib)] public static extern int SUFaceGetInnerLoops(IntPtr face, nuint len, [Out] IntPtr[] loops, out nuint count);
    [DllImport(Lib)] public static extern int SUFaceGetNormal(IntPtr face, out Point3D normal);
    [DllImport(Lib)] public static extern int SUFaceGetFrontMaterial(IntPtr face, ref IntPtr material);
    [DllImport(Lib)] public static extern int SUFaceGetBackMaterial(IntPtr face, ref IntPtr material);
    [DllImport(Lib)] public static extern IntPtr SUFaceToDrawingElement(IntPtr face);

    [DllImport(Lib)] public static extern int SULoopGetNumVertices(IntPtr loop, out nuint count);
    [DllImport(Lib)] public static extern int SULoopGetVertices(IntPtr loop, nuint len, [Out] IntPtr[] vertices, out nuint count);
    [DllImport(Lib)] public static extern int SUVertexGetPosition(IntPtr vertex, out Point3D position);

    [DllImport(Lib)] public static extern int SUEdgeGetStartVertex(IntPtr edge, ref IntPtr vertex);
    [DllImport(Lib)] public static extern int SUEdgeGetEndVertex(IntPtr edge, ref IntPtr vertex);
    [DllImport(Lib)] public static extern int SUEdgeGetSoft(IntPtr edge, [MarshalAs(UnmanagedType.U1)] out bool soft);
    [DllImport(Lib)] public static extern int SUEdgeGetSmooth(IntPtr edge, [MarshalAs(UnmanagedType.U1)] out bool smooth);
    [DllImport(Lib)] public static extern IntPtr SUEdgeToDrawingElement(IntPtr edge);

    [DllImport(Lib)] public static extern int SUDrawingElementGetLayer(IntPtr element, ref IntPtr layer);
    [DllImport(Lib)] public static extern int SUDrawingElementGetHidden(IntPtr element, [MarshalAs(UnmanagedType.U1)] out bool hidden);
    [DllImport(Lib)] public static extern int SUDrawingElementGetMaterial(IntPtr element, ref IntPtr material);

    [DllImport(Lib)] public static extern int SULayerGetName(IntPtr layer, ref IntPtr name);
    [DllImport(Lib)] public static extern int SULayerGetVisibility(IntPtr layer, [MarshalAs(UnmanagedType.U1)] out bool visible);

    [DllImport(Lib)] public static extern int SUMaterialGetName(IntPtr material, ref IntPtr name);
    [DllImport(Lib)] public static extern int SUMaterialGetColor(IntPtr material, out Color color);
    [DllImport(Lib)] public static extern int SUMaterialGetOpacity(IntPtr material, out double alpha);

    [DllImport(Lib)] public static extern int SUGroupGetEntities(IntPtr group, ref IntPtr entities);
    [DllImport(Lib)] public static extern int SUGroupGetTransform(IntPtr group, out Transformation transform);
    [DllImport(Lib)] public static extern int SUGroupGetName(IntPtr group, ref IntPtr name);
    [DllImport(Lib)] public static extern IntPtr SUGroupToDrawingElement(IntPtr group);

    [DllImport(Lib)] public static extern int SUComponentInstanceGetDefinition(IntPtr instance, ref IntPtr definition);
    [DllImport(Lib)] public static extern int SUComponentInstanceGetTransform(IntPtr instance, out Transformation transform);
    [DllImport(Lib)] public static extern int SUComponentInstanceGetName(IntPtr instance, ref IntPtr name);
    [DllImport(Lib)] public static extern IntPtr SUComponentInstanceToDrawingElement(IntPtr instance);
    [DllImport(Lib)] public static extern int SUComponentDefinitionGetEntities(IntPtr definition, ref IntPtr entities);
    [DllImport(Lib)] public static extern int SUComponentDefinitionGetName(IntPtr definition, ref IntPtr name);

    [DllImport(Lib)] public static extern int SUImageGetName(IntPtr image, ref IntPtr name);
    [DllImport(Lib)] public static extern int SUImageGetTransform(IntPtr image, out Transformation transform);
    [DllImport(Lib)] public static extern int SUImageGetDimensions(IntPtr image, out double width, out double height);
    [DllImport(Lib)] public static extern int SUImageGetPixelDimensions(IntPtr image, out nuint width, out nuint height);
    [DllImport(Lib)] public static extern int SUImageGetImageRep(IntPtr image, ref IntPtr imageRep);
    [DllImport(Lib)] public static extern IntPtr SUImageToDrawingElement(IntPtr image);
    [DllImport(Lib)] public static extern int SUImageRepCreate(ref IntPtr imageRep);
    [DllImport(Lib)] public static extern int SUImageRepRelease(ref IntPtr imageRep);
    [DllImport(Lib)] public static extern int SUImageRepGetPixelDimensions(IntPtr imageRep, out nuint width, out nuint height);
    [DllImport(Lib)] public static extern int SUImageRepResize(IntPtr imageRep, nuint width, nuint height);
    [DllImport(Lib)] public static extern int SUImageRepSaveToFile(IntPtr imageRep, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Lib)] public static extern int SUStringCreate(ref IntPtr str);
    [DllImport(Lib)] public static extern int SUStringRelease(ref IntPtr str);
    [DllImport(Lib)] public static extern int SUStringGetUTF8Length(IntPtr str, out nuint length);
    [DllImport(Lib)] public static extern int SUStringGetUTF8(IntPtr str, nuint length, [Out] byte[] buffer, out nuint count);

    public delegate int HandleGetter(IntPtr handle, ref IntPtr result);

    /// <summary>
    /// Reads a handle-valued property. The C API refuses to write into an output that already holds a valid
    /// handle (SU_ERROR_OVERWRITE_VALID), so the output always starts as null; a reused stack slot made every
    /// read after the first one fail.
    /// </summary>
    public static IntPtr Get(IntPtr handle, HandleGetter getter)
    {
        var result = IntPtr.Zero;
        return getter(handle, ref result) == Ok ? result : IntPtr.Zero;
    }

    public delegate int StringGetter(IntPtr handle, ref IntPtr str);

    /// <summary>Reads a string property through the SUStringRef protocol (create, fill, read, release).</summary>
    public static string GetString(IntPtr handle, StringGetter getter)
    {
        var str = IntPtr.Zero;
        if (SUStringCreate(ref str) != Ok) return string.Empty;
        try
        {
            if (getter(handle, ref str) != Ok) return string.Empty;
            if (SUStringGetUTF8Length(str, out var len) != Ok || len == 0) return string.Empty;
            var buffer = new byte[(int)len + 1];
            if (SUStringGetUTF8(str, len + 1, buffer, out var count) != Ok) return string.Empty;
            return System.Text.Encoding.UTF8.GetString(buffer, 0, (int)Math.Min(count, len));
        }
        finally
        {
            SUStringRelease(ref str);
        }
    }

    public delegate int CountGetter(IntPtr handle, out nuint count);
    public delegate int ArrayGetter(IntPtr handle, nuint len, IntPtr[] items, out nuint count);

    /// <summary>Reads a list through the usual GetNumX / GetX pair.</summary>
    public static IntPtr[] GetList(IntPtr handle, CountGetter countGetter, ArrayGetter arrayGetter)
    {
        if (countGetter(handle, out var n) != Ok || n == 0) return Array.Empty<IntPtr>();
        var items = new IntPtr[(int)n];
        return arrayGetter(handle, n, items, out var count) == Ok ? items.Take((int)count).ToArray() : Array.Empty<IntPtr>();
    }
}
