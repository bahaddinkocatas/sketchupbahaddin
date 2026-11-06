using System.Buffers.Binary;
using System.Text;
using Sketch.Geometry;

namespace Sketch.IO.Skp;

/// <summary>
/// One record of the tag-length-value tree that SketchUp 2021+ files keep in <c>model.dat</c>: a 2-byte
/// little-endian tag, a 4-byte little-endian payload length and the payload. Container records hold a sequence of
/// child records; leaf records hold a number, a string, an identifier or a block of doubles. The layout was learned
/// by inspecting the user's own files (see <c>docs/skp-bicimi.md</c>); no SketchUp software was decoded.
/// </summary>
internal readonly struct SkpRecord
{
    private readonly byte[] _buf;

    public SkpRecord(byte[] buf, ushort tag, int start, int end)
    {
        _buf = buf;
        Tag = tag;
        Start = start;
        End = end;
    }

    public ushort Tag { get; }
    public int Start { get; }
    public int End { get; }
    public int Length => End - Start;
    public ReadOnlySpan<byte> Bytes => _buf.AsSpan(Start, Length);

    /// <summary>Parses the whole file: its single root record.</summary>
    public static SkpRecord Root(byte[] buf)
    {
        if (buf.Length < 6) throw new InvalidDataException("model.dat is too short.");
        var tag = BinaryPrimitives.ReadUInt16LittleEndian(buf);
        var len = BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(2));
        if (len < 0 || 6L + len > buf.Length) throw new InvalidDataException("model.dat has an invalid root record.");
        return new SkpRecord(buf, tag, 6, 6 + len);
    }

    /// <summary>The child records in order (stops at the first malformed one).</summary>
    public IEnumerable<SkpRecord> Children()
    {
        var p = Start;
        while (p + 6 <= End)
        {
            var tag = BinaryPrimitives.ReadUInt16LittleEndian(_buf.AsSpan(p));
            var len = BinaryPrimitives.ReadInt32LittleEndian(_buf.AsSpan(p + 2));
            var vs = p + 6;
            if (len < 0 || (long)vs + len > End) yield break;
            yield return new SkpRecord(_buf, tag, vs, vs + len);
            p = vs + len;
        }
    }

    public IEnumerable<SkpRecord> Children(ushort tag)
    {
        foreach (var c in Children())
            if (c.Tag == tag) yield return c;
    }

    /// <summary>The first child with this tag, or null.</summary>
    public SkpRecord? Child(ushort tag)
    {
        foreach (var c in Children())
            if (c.Tag == tag) return c;
        return null;
    }

    /// <summary>Follows a path of tags, first child at each step.</summary>
    public SkpRecord? Path(params ushort[] tags)
    {
        SkpRecord? cur = this;
        foreach (var t in tags)
        {
            cur = cur.Value.Child(t);
            if (cur is null) return null;
        }
        return cur;
    }

    public string Text() => Encoding.UTF8.GetString(_buf, Start, Length);

    /// <summary>An identifier: an unsigned little-endian integer written with as few bytes as needed (1–8).</summary>
    public long Id()
    {
        long v = 0;
        for (var i = Math.Min(Length, 8) - 1; i >= 0; i--) v = (v << 8) | _buf[Start + i];
        return v;
    }

    public byte Byte() => Length >= 1 ? _buf[Start] : (byte)0;
    public int Int32() => Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(Bytes) : (int)Id();
    public double Double() => Length >= 8 ? BinaryPrimitives.ReadDoubleLittleEndian(Bytes) : 0;

    public double[] Doubles()
    {
        var n = Length / 8;
        var r = new double[n];
        for (var i = 0; i < n; i++) r[i] = BinaryPrimitives.ReadDoubleLittleEndian(_buf.AsSpan(Start + 8 * i));
        return r;
    }

    public Vector3d Point()
    {
        var d = Doubles();
        return d.Length >= 3 ? new Vector3d(d[0], d[1], d[2]) : Vector3d.Zero;
    }
}

/// <summary>Record tags of model.dat as observed in SketchUp 2021–2026 files (names are ours).</summary>
internal static class SkpTag
{
    // Top-level sections
    public const ushort Root = 0x01f4;
    public const ushort RootEntities = 0x01f6; // holds one Entities container: the model's own geometry
    public const ushort MaterialList = 0x01f7; // 30d4 > 30d5 > Material*
    public const ushort LayerList = 0x01f8; // 3a98 > 3a99 > Layer*
    public const ushort DefinitionList = 0x01f9; // 1770 > 1771 > Definition*
    public const ushort Options = 0x0200; // 61a8 > 61a9 > 61aa (name 61ab, values 61ac)

    // Entity header (07d0) and base (05dc)
    public const ushort Base = 0x05dc; // > Id
    public const ushort Id = 0x05de;
    public const ushort Header = 0x07d0; // > Base, Material?, Layer?, Flags
    public const ushort HeaderMaterial = 0x07d1; // material id (faces: front material; texts: colour)
    public const ushort HeaderLayer = 0x07d2; // layer id; absent = Layer0 (untagged)
    public const ushort HeaderFlags = 0x07d3; // bit 6 (64) = hidden; bits 1,2 = shadow options

    // Entities container (1388)
    public const ushort Entities = 0x1388;
    public const ushort VertexList = 0x1389;
    public const ushort EdgeList = 0x138a;
    public const ushort FaceList = 0x138b;
    public const ushort ComponentInstanceList = 0x138c; // > Instance (component definitions, type 0)
    public const ushort InstanceList = 0x138d; // > 1d4c > Instance (groups, type 1)
    public const ushort ImageInstanceList = 0x1390; // > 1f40 > Instance (definition type 2)
    public const ushort GuideList = 0x1392; // > 426c
    public const ushort SectionList = 0x1393; // > 445c
    public const ushort ActiveSection = 0x1394; // id of the active section plane
    public const ushort CurveList = 0x1396; // > 4a38 (arcs/polylines: first/last edge, edge count)
    public const ushort TextList = 0x1398; // > 55f0
    public const ushort DimensionList = 0x1399; // > 5bcc
    public const ushort Bounds = 0x13a0; // 6 doubles: min xyz, max xyz

    public const ushort Guide = 0x426c; // > 4268 (header wrapper), two points, flag
    public const ushort GuideHeaderWrap = 0x4268;
    public const ushort GuideStart = 0x426d;
    public const ushort GuideEnd = 0x426e;
    public const ushort GuideIsLine = 0x426f;

    public const ushort Section = 0x445c; // > header, plane, name, symbol
    public const ushort SectionPlaneEq = 0x445d; // a b c d; the normal points at the side that stays visible
    public const ushort SectionName = 0x445e;

    public const ushort Dimension = 0x5bcc; // > 59d8 (header wrapper), two attachments, normal, offset
    public const ushort DimensionHeaderWrap = 0x59d8;
    public const ushort DimensionStart = 0x5bcd; // > 5208 attachment
    public const ushort DimensionEnd = 0x5bce; // > 5208 attachment
    public const ushort DimensionNormal = 0x5bcf;
    public const ushort DimensionOffset = 0x5bd2; // signed distance of the dimension line from the measured line

    // Attachment (text leaders, dimension ends): a point, or a vertex of the same container
    public const ushort AttachmentKind = 0x5209; // 1 = point in 520a, 2 = vertex in 520b
    public const ushort AttachmentVertexWrap = 0x520b; // > 53fc > 53fd vertex id
    public const ushort AttachmentVertexInner = 0x53fc;
    public const ushort AttachmentVertexId = 0x53fd;

    // Scenes (0207 > 6d60 > 6d61 > 7148)
    public const ushort SceneList = 0x0207;
    public const ushort Scene = 0x7148;
    public const ushort SceneName = 0x7153;
    public const ushort SceneCameraWrap = 0x714a; // > 34bc
    public const ushort SceneHiddenLayers = 0x7150; // list of (1-byte length, id) hidden layer ids; present = tags saved
    public const ushort SceneStyleId = 0x714c; // present = style saved
    public const ushort CameraRecord = 0x34bc;
    public const ushort CameraEye = 0x34bd;
    public const ushort CameraTarget = 0x34be;
    public const ushort CameraUp = 0x34bf;
    public const ushort CameraOrthoHeight = 0x34c3;
    public const ushort CameraFov = 0x34c4;
    public const ushort CameraPerspective = 0x34c6;

    public const ushort Vertex = 0x09c4;
    public const ushort VertexPosition = 0x09c5;

    public const ushort Edge = 0x0bb8;
    public const ushort EdgeStart = 0x0bb9;
    public const ushort EdgeEnd = 0x0bba;
    public const ushort EdgeCurve = 0x0bbb;

    public const ushort Face = 0x0dac;
    public const ushort FacePlane = 0x0dad; // a b c d
    public const ushort FaceLoops = 0x0dae; // > Loop*
    public const ushort FaceFrontMaterial = 0x0daf;
    public const ushort Loop = 0x1194; // > 1195 > EdgeUse*
    public const ushort LoopEdgeUses = 0x1195;
    public const ushort EdgeUse = 0x0fa0;
    public const ushort EdgeUseEdge = 0x0fa1;
    public const ushort EdgeUseReversed = 0x0fa2;

    public const ushort InstanceWrap = 0x1d4c;
    public const ushort ImageInstanceWrap = 0x1f40;
    public const ushort Instance = 0x1964;
    public const ushort InstanceName = 0x1965;
    public const ushort InstanceTransform = 0x1966; // 13 doubles: 3×3 matrix row by row, origin, scale divisor
    public const ushort InstanceDefinition = 0x1967;

    public const ushort Text = 0x55f0;
    public const ushort TextString = 0x55f1;
    public const ushort TextAttachment = 0x55f4; // > 5208 > 520a point
    public const ushort TextAttachmentInner = 0x5208;
    public const ushort TextPoint = 0x520a;

    // Definitions (157c)
    public const ushort Definition = 0x157c; // > Entities, name, type
    public const ushort DefinitionName = 0x157e;
    public const ushort DefinitionType = 0x1583; // 0 component, 1 group, 2 image

    // Layers
    public const ushort Layer = 0x3c8c;
    public const ushort LayerName = 0x3c8d;
    public const ushort LayerHidden = 0x3c8e;

    // Materials
    public const ushort Material = 0x32c8;
    public const ushort MaterialName = 0x32cc;

    // Options
    public const ushort OptionGroup = 0x61aa;
    public const ushort OptionGroupName = 0x61ab;
    public const ushort OptionValues = 0x61ac;
    public const ushort OptionName = 0x61ad;
    public const ushort OptionValue = 0x38a4;
    public const ushort OptionInt = 0x38a7;
}
