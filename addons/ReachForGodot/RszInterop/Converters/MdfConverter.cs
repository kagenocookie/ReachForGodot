namespace ReaGE;

using System.Threading.Tasks;
using Godot;
using RszTool;
using RszTool.Common;

public class MdfConverter : RszAssetConverter<MaterialDefinitionResource, MdfFile, MaterialDefinitionResource>
{
    public override MaterialDefinitionResource CreateOrReplaceResourcePlaceholder(AssetReference reference)
        => SetupResource(new MaterialDefinitionResource(), reference);

    public override MdfFile CreateFile(FileHandler fileHandler) => new MdfFile(FileOption, fileHandler);

    public override Task<bool> Import(MdfFile file, MaterialDefinitionResource target)
    {
        target.Materials = new MaterialResource[file.MatDatas.Count];
        int m = 0;
        foreach (var matData in file.MatDatas) {
            var flags1 = (matData.Header.alphaFlags & 0x03ff);
            var tess = (int)(((matData.Header.alphaFlags & 0xfc00) >> 10) & 0xf3);
            var phong = (int)(((matData.Header.alphaFlags & 0x00ff0000) >> 16) & 0xff);
            var flags2 = ((matData.Header.alphaFlags & 0xff000000) >> 24) & 0xff;
            var mat = new MaterialResource() {
                Game = Game,
                AlphaFlags = (MaterialFlags1)flags1,
                TesselationFactor = tess,
                PhongFactor = phong,
                AlphaFlags2 = (MaterialFlags2)flags2,
                MasterMaterial = Importer.FindOrImportResource<MasterMaterialResource>(matData.Header.mmtrPath, Config, WritesEnabled),
                MaterialName = matData.Header.matName,
                ResourceName = matData.Header.matName,
                ShaderType = (int)matData.Header.shaderType,
                Textures = new Godot.Collections.Dictionary<string, TextureResource>(),
                GpuBuffers = new Godot.Collections.Dictionary<string, GpuBufferResource>(),
            };

            for (int i = 0; i < matData.Header.texCount; ++i) {
                var srcTex = matData.TexHeaders[i];
                Debug.Assert(!string.IsNullOrEmpty(srcTex.texType) && !string.IsNullOrEmpty(srcTex.texPath));
                var tex = Importer.FindOrImportResource<TextureResource>(srcTex.texPath, Config, WritesEnabled)
                    ?? new TextureResource() { Game = Game, Asset = new AssetReference(srcTex.texPath) };
                mat.Textures[srcTex.texType] = tex;
            }

            mat.Params = new MaterialPropertyList(matData.Header.paramCount) { ResourceName = "MaterialProperties" };
            for (int i = 0; i < matData.Header.paramCount; ++i) {
                var param = matData.ParamHeaders[i];
                Variant value = param.componentCount switch {
                    1 => (int)param.parameter.X,
                    4 => new Color(param.parameter.X, param.parameter.Y, param.parameter.Z, param.parameter.W),
                    _ => throw new ArgumentException($"Unexpected MDF2 param [{i}] '{param.paramName}' size " + param.Size),
                };
                mat.Params.SetParam(i, value, param.paramName ?? string.Empty);
            }

            for (int i = 0; i < matData.Header.gpbfNameCount; ++i) {
                var (name, data) = matData.GpbfHeaders[i];
                Debug.Assert(!string.IsNullOrEmpty(name.name) && !string.IsNullOrEmpty(data.name));
                mat.GpuBuffers[name.name] = Importer.FindOrImportResource<GpuBufferResource>(data.name, Config, WritesEnabled)
                    ?? new GpuBufferResource() { Game = Game, Asset = new AssetReference(data.name) };
            }

            target.Materials[m++] = mat;
        }
        return Task.FromResult(true);
    }

    public override Task<bool> Export(MaterialDefinitionResource source, MdfFile file)
    {
        file.MatDatas = new List<MdfFile.MatData>(source.Materials?.Length ?? 0);
        if (source.Materials == null) return Task.FromResult(true);

        file.Header.Data = new MdfFile.HeaderStruct() {
            magic = MdfFile.Magic,
            matCount = (short)source.Materials.Length,
            mdfVersion = 1,
        };

        for (int i = 0; i < source.Materials.Length; ++i) {
            var mat = source.Materials[i];
            var matData = new MdfFile.MatData(new MdfFile.MatHeader(FileOption.Version) {
                matName = mat.MaterialName,
                mmtrPath = mat.MasterMaterial?.Asset?.AssetFilename,
                paramCount = mat.Params?.Values?.Count ?? 0,
                texCount = mat.Textures?.Count ?? 0,
                alphaFlags = (
                    (uint)mat.AlphaFlags +
                    (uint)((mat.TesselationFactor & 0xf3) << 10) +
                    (uint)((mat.PhongFactor & 0xff) << 16) +
                    (uint)(((uint)mat.AlphaFlags2 & 0xff) << 24)
                ),
                shaderType = (uint)mat.ShaderType,
            });
            file.MatDatas.Add(matData);

            if (mat.Textures != null) {
                foreach (var (name, tex) in mat.Textures) {
                    matData.TexHeaders.Add(new MdfFile.TexHeader(FileOption.Version) {
                        texType = name,
                        asciiHash = MurMur3HashUtils.GetAsciiHash(name),
                        hash = MurMur3HashUtils.GetHash(name),
                        texPath = tex.Asset?.AssetFilename
                    });
                }
            }

            if (mat.Params?.Values != null) {
                matData.ParamHeaders = new List<MdfFile.ParamHeader>(mat.Params.Values.Count);
                for (var p = 0; p < mat.Params.Values.Count; p++) {
                    var paramValue = mat.Params.Values[p];
                    System.Numerics.Vector4 vec;
                    if (paramValue.VariantType is Variant.Type.Float or Variant.Type.Int) {
                        vec = new System.Numerics.Vector4(paramValue.AsSingle(), 0, 0, 0);
                    } else if (paramValue.VariantType == Variant.Type.Color) {
                        var col = paramValue.AsColor();
                        vec = new System.Numerics.Vector4(col.R, col.G, col.B, col.A);
                    } else {
                        throw new Exception($"Invalid param [{p}] '{mat.Params.Names![p]}': {paramValue}");
                    }

                    matData.ParamHeaders.Add(new MdfFile.ParamHeader(FileOption.Version) {
                        componentCount = mat.Params.ValueCounts![p],
                        paramName = mat.Params.Names![p],
                        parameter = vec,
                    });
                }
            }

            matData.Header.gpbfDataCount = matData.Header.gpbfNameCount = mat.GpuBuffers?.Count ?? 0;
            if (mat.GpuBuffers != null) {
                foreach (var (name, resource) in mat.GpuBuffers) {
                    if (resource.Asset?.AssetFilename == null) {
                        GD.PrintErr("Missing Gpu buffer resource path!");
                        continue;
                    }

                    matData.GpbfHeaders.Add((new MdfFile.GpbfHeader(name), new MdfFile.GpbfHeader() { name = resource.Asset.AssetFilename, asciiHash = 1 }));
                }
            }
        }

        return Task.FromResult(true);
    }
}
