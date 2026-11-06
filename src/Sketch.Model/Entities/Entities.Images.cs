using Sketch.Geometry;

namespace Sketch.Model;

public sealed partial class Entities
{
    private readonly List<ImageEntity> _images = new();

    public IReadOnlyList<ImageEntity> Images => _images;

    public IEnumerable<ImageEntity> AllImages() => _images.Concat(_groups.SelectMany(g => g.Entities.AllImages()));

    /// <summary>Places an image whose pixels are already in the model's image store.</summary>
    public ImageEntity AddImage(string imageKey, Vector3d origin, Vector3d u, Vector3d v, string name = "")
    {
        var img = new ImageEntity
        {
            Id = Context.NextId(), ImageKey = imageKey, Origin = origin, U = u, V = v, Name = name, Parent = this, Tag = NewEntityTag(),
        };
        _images.Add(img);
        RaiseChanged();
        return img;
    }

    private bool RemoveImage(Entity e)
    {
        if (e is not ImageEntity img || !_images.Remove(img)) return false;
        img.IsDeleted = true;
        return true;
    }

    private void ClearImages()
    {
        foreach (var i in _images) i.IsDeleted = true;
        _images.Clear();
    }

    private IEnumerable<EntitiesState.ImageData> CaptureImages() =>
        _images.Select(i => new EntitiesState.ImageData(i.Id, i.ImageKey, i.Origin, i.U, i.V, i.Name, i.Tag?.Name, i.Hidden));

    private void LoadImages(EntitiesState state, Func<int, int> id, Vector3d offset, List<Entity> added)
    {
        foreach (var d in state.Images ?? Array.Empty<EntitiesState.ImageData>())
        {
            var img = new ImageEntity
            {
                Id = id(d.Id), ImageKey = d.Key, Origin = d.Origin + offset, U = d.U, V = d.V, Name = d.Name, Parent = this,
                Tag = Context.Tags.Find(d.Tag), Hidden = d.Hidden,
            };
            _images.Add(img);
            added.Add(img);
        }
    }

    private static void TransformImage(ImageEntity img, Matrix4d transform)
    {
        img.Origin = transform.TransformPoint(img.Origin);
        img.U = transform.TransformVector(img.U);
        img.V = transform.TransformVector(img.V);
    }
}

/// <summary>Raw bytes of imported images, keyed by content hash (shared by every image entity using them).</summary>
public sealed class ImageStore
{
    private readonly Dictionary<string, byte[]> _data = new();

    public IReadOnlyDictionary<string, byte[]> Items => _data;

    /// <summary>Stores image bytes (deduplicated by content) and returns their key.</summary>
    public string Add(byte[] bytes)
    {
        var key = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(bytes));
        _data.TryAdd(key, bytes);
        return key;
    }

    public void Set(string key, byte[] bytes) => _data[key] = bytes;

    public byte[]? Get(string key) => _data.GetValueOrDefault(key);

    public void Clear() => _data.Clear();
}
