namespace ReaGE;

using System.Threading.Tasks;
using ReeLib;

public class CdefConverter : ResourceConverter<CollisionDefinitionResource, CdefFile>
{
    public override CdefFile CreateFile(FileHandler fileHandler) => new CdefFile(fileHandler);

    public override Task<bool> Import(CdefFile file, CollisionDefinitionResource target)
    {
        target.Layers = new CollisionLayerDefinition[file.Header.layerCount];

        for (var i = 0; i < file.Header.layerCount; i++) {
            var src = file.Layers[i];
            target.Layers[i] = new CollisionLayerDefinition() {
                Name = src.name,
                Guid = src.guid,
                Color = src.color.ToGodot(),
                Value1 = src.ukn1,
                Value2 = src.ukn2,
                Value3 = src.ukn3,
                Value4 = src.ukn4,
                ResourceName = src.name,
                Bits1 = (uint)(file.Bits[i] & 0xffffffff),
                Bits2 = (uint)((file.Bits[i] >> 32) & 0xffffffff),
            };
        }

        target.Masks = file.Masks.Select(src => new CollisonMaskDefinition() {
            Guid = src.guid,
            LayerId = src.layerId,
            MaskId = src.maskId,
            Name = src.name,
            Value1 = src.ukn1,
            ResourceName = src.name,
        }).ToArray();

        target.Materials = file.Materials.Select(src => new CollisionMaterialDefinition() {
            Guid = src.guid,
            Name = src.name,
            ResourceName = src.name,
            Color = src.color.ToGodot(),
        }).ToArray();

        target.Attributes = file.Attributes.Select(src => new CollisionAttributeDefinition() {
            Guid = src.guid,
            Name = src.name,
            ResourceName = src.name,
        }).ToArray();

        target.Presets = file.Presets.Select(src => new CollisionPresetDefinition() {
            Guid = src.guid,
            Name = src.name,
            ResourceName = src.name,
            Color = src.color.ToGodot(),
            Description = src.description,
            MaskBits = src.maskBits,
            Value1 = src.ukn1,
            Value2 = src.ukn2,
            Value3 = src.ukn3,
        }).ToArray();
        return Task.FromResult(true);
    }

    public override Task<bool> Export(CollisionDefinitionResource source, CdefFile file)
    {
        return Task.FromResult(false);
    }
}
