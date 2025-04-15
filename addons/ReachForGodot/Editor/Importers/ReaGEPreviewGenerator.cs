namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using Godot.Collections;

/// <summary>
/// Proxy placeholder resource for resources that are imported directly from the godot filesystem
/// </summary>
public partial class ReaGEPreviewGenerator : EditorResourcePreviewGenerator
{
    public override bool _CanGenerateSmallPreview() => true;
    public override bool _Handles(string type)
    {
        return type == "Resource";
    }

    public override Texture2D? _Generate(Resource resource, Vector2I size, Dictionary metadata)
    {
        if (resource is not REResource res) return null;

        var img = TryLoadImage("res://addons/ReachForGodot/icons/" + res.ResourceType + ".svg", size)
            ?? TryLoadImage("res://addons/ReachForGodot/icons/" + res.ResourceType + ".png", size);

        if (img == null) return null;

        if (img.GetSize() != size) img.Resize(size.X, size.Y, Image.Interpolation.Trilinear);
        return ImageTexture.CreateFromImage(img);
    }

    private static Image? TryLoadImage(string path, Vector2I size)
    {
        if (!ResourceLoader.Exists(path)) return null;

        if (Path.GetExtension(path) == ".svg") {
            var bytes = File.ReadAllBytes(ProjectSettings.GlobalizePath(path));
            var svg = new Image();
            // we don't really know what the original default size was
            // godot default icons are 16x16 so using that as a reference
            svg.LoadSvgFromBuffer(bytes, size.X / 16);
            return svg;
        }

        var res = ResourceLoader.Load<Resource>(path);
        if (res is Image img) return img;
        if (res is Texture2D tex) return tex.GetImage();
        return null;
    }
}
