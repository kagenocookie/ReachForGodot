namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using ReeLib;

public class ChfConverter : ResourceConverter<ColliderHeightFieldResource, CHFFile>, ISynchronousConverter<ColliderHeightFieldResource, CHFFile>
{
    public override CHFFile CreateFile(FileHandler fileHandler) => new CHFFile(fileHandler);

    public override Task<bool> Import(CHFFile file, ColliderHeightFieldResource target)
    {
        return Task.FromResult(ImportSync(file, target));
    }

    public bool ImportSync(CHFFile file, ColliderHeightFieldResource target)
    {
        target.HeightMap ??= new HeightMapShape3D();
        target.HeightMap.MapWidth = file.splitCount + 1;
        target.HeightMap.MapDepth = file.splitCount + 1;
        target.HeightMap.MapData = file.PointHeights;
        target.TileSize = new Vector2(file.tileSizeX, file.tileSizeY);
        target.PointData = file.PointData;
        target.MinRange = file.min.ToGodot();
        target.MaxRange = file.max.ToGodot();
        target.MaskBits = file.MaskBits.Select(m => (int)m).ToArray();
        target.CollisionPresetIDs = file.CollisionPresetIDs;
        target.CollisionPresets = file.Strings.ToArray();
        return true;
    }

    public override Task<bool> Export(ColliderHeightFieldResource source, CHFFile file)
    {
        if (source.HeightMap == null) {
            return Task.FromResult(false);
        }
        if (source.HeightMap.MapWidth != source.HeightMap.MapDepth) {
            GD.PrintErr("Height map width and depth must be identical! " + source.ResourcePath);
            return Task.FromResult(false);
        }

        file.splitCount = source.HeightMap.MapWidth - 1;
        file.splitCount = source.HeightMap.MapDepth - 1;
        file.PointHeights = source.HeightMap.MapData;
        file.tileSizeX = source.TileSize.X;
        file.tileSizeY = source.TileSize.Y;
        file.PointData = source.PointData;
        file.max = source.MaxRange.ToRsz();
        file.min = source.MinRange.ToRsz();
        file.MaskBits = source.MaskBits.Select(m => (uint)m).ToArray();
        file.CollisionPresetIDs = source.CollisionPresetIDs;
        file.Strings.Clear();
        file.Strings.AddRange(source.CollisionPresets);
        return Task.FromResult(true);
    }
}
