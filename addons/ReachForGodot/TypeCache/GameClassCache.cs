namespace ReaGE;

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Godot;
using RszTool;

public static partial class TypeCache
{
    private sealed class GameClassCache
    {
        public SupportedGame game;
        public readonly Dictionary<string, ClassInfo> serializationCache = new();
        public readonly Dictionary<string, List<REFieldAccessor>> fieldOverrides = new();

        private Dictionary<string, Dictionary<string, PrefabGameObjectRefProperty>>? gameObjectRefProps;
        public Dictionary<string, Dictionary<string, PrefabGameObjectRefProperty>> GameObjectRefProps
            => gameObjectRefProps ??= DeserializeOrNull(ReachForGodot.GetPaths(game)?.PfbGameObjectRefPropsPath, gameObjectRefProps) ?? new(0);

        private RszParser? parser;
        public RszParser Parser => parser ??= LoadRszParser();

        private Il2cppCache? il2CppCache;
        public Il2cppCache Il2cppCache => il2CppCache ??= LoadIl2cppData(ReachForGodot.GetAssetConfig(game).Paths);

        private Dictionary<string, EnhancedRszClassPatch> rszTypePatches = new();

        public GameClassCache(SupportedGame game)
        {
            this.game = game;
        }

        public Dictionary<string, PrefabGameObjectRefProperty>? GetClassProps(string classname)
        {
            if (GameObjectRefProps.TryGetValue(classname, out var result)) {
                return result;
            }

            return null;
        }

        public EnhancedRszClassPatch FindOrCreateClassPatch(string classname)
        {
            if (!rszTypePatches.TryGetValue(classname, out var props)) {
                rszTypePatches[classname] = props = new();
            }
            return props;
        }

        public bool TryFindClassPatch(string classname, [MaybeNullWhen(false)] out EnhancedRszClassPatch data)
        {
            if (rszTypePatches.TryGetValue(classname, out var props)) {
                data = props;
                return true;
            }
            data = null;
            return false;
        }

        public string GetResourceType(string classname, string field)
        {
            if (rszTypePatches.TryGetValue(classname, out var props) && props.FieldPatches != null) {
                var patch = props.FieldPatches.FirstOrDefault(f => f.ReplaceName == field) ?? props.FieldPatches.FirstOrDefault(f => f.Name == field);
                return patch != null && patch.FileFormat != SupportedFileFormats.Unknown ? PathUtils.GetResourceTypeFromFormat(patch.FileFormat).Name : nameof(REResource);
            }
            return nameof(REResource);
        }

        public void UpdateRszPatches(AssetConfig config)
        {
            Directory.CreateDirectory(config.Paths.RszPatchPath.GetBaseDir());
            using var file = File.Create(config.Paths.RszPatchPath);
            JsonSerializer.Serialize(file, rszTypePatches, jsonOptions);
        }

        public void UpdateClassProps(string classname, Dictionary<string, PrefabGameObjectRefProperty> propInfoDict)
        {
            var reflist = GameObjectRefProps;
            reflist[classname] = propInfoDict;
            var fn = ReachForGodot.GetPaths(game)?.PfbGameObjectRefPropsPath ?? throw new Exception("Missing pfb cache filepath for " + game);
            using var fs = File.Create(fn);
            JsonSerializer.Serialize<Dictionary<string, Dictionary<string, PrefabGameObjectRefProperty>>>(fs, reflist, jsonOptions);
        }

        private RszParser LoadRszParser()
        {
            var paths = ReachForGodot.GetPaths(game);
            var jsonPath = paths?.RszJsonPath;
            if (jsonPath == null || paths == null) {
                GD.PrintErr("No rsz json defined for game " + game);
                return null!;
            }

            var time = new Stopwatch();
            time.Start();
            var parser = RszParser.GetInstance(jsonPath);
            parser.ReadPatch(GamePaths.RszPatchGlobalPath);
            rszTypePatches = DeserializeOrNull(paths.RszPatchPath, rszTypePatches) ?? new();
            parser.ReadPatch(paths.RszPatchPath);
            foreach (var (cn, accessors) in fieldOverrides) {
                foreach (var acc in accessors) {
                    var cls = parser.GetRSZClass(cn);
                    if (cls != null) {
                        GenerateObjectCache(this, cls);
                    }
                }
            }
            time.Stop();
            GD.Print($"Loaded {game} RSZ data in {time.Elapsed}");
            return parser;
        }

        private Il2cppCache LoadIl2cppData(GamePaths paths)
        {
            var time = new Stopwatch();
            time.Start();
            il2CppCache = new Il2cppCache();
            var baseCacheFile = paths.EnumCacheFilename;
            if (!File.Exists(baseCacheFile)) {
                RegenerateIl2cppCache(paths);
                GD.Print("Regenerated source il2cpp data in " + time.Elapsed);
            } else {
                var cacheLastUpdate = File.GetLastWriteTimeUtc(baseCacheFile);
                var il2cppLastUpdate = paths.Il2cppPath == null ? DateTime.MinValue : File.GetLastWriteTimeUtc(paths.Il2cppPath!);
                if (il2cppLastUpdate > cacheLastUpdate) {
                    RegenerateIl2cppCache(paths);
                    GD.Print("Regenerated source il2cpp data in " + time.Elapsed);
                }
            }
            time.Restart();

            var success = TryApplyIl2cppCache(il2CppCache, baseCacheFile);
            if (!success) {
                RegenerateIl2cppCache(paths);
                GD.Print("Regenerated source il2cpp data in " + time.Elapsed);
                success = TryApplyIl2cppCache(il2CppCache, baseCacheFile);
            }
            TryApplyEnumOverrides(il2CppCache, paths.EnumOverridesDir);
            if (success) {
                GD.Print("Loaded cached il2cpp data in " + time.Elapsed);
            } else {
                GD.PrintErr("Failed to load il2cpp cache data from " + baseCacheFile);
            }
            TryApplyTypePatches(Il2cppCache, paths.TypePatchFilepath);
            return il2CppCache;
        }

        private void RegenerateIl2cppCache(GamePaths paths)
        {
            if (!File.Exists(paths.Il2cppPath)) {
                GD.PrintErr($"Il2cpp file does not exist, nor do we have a valid cache file for {paths.Game}. Enums and class names won't resolve properly.");
                return;
            }

            il2CppCache ??= new();
            var entries = DeserializeOrNull<REFDumpFormatter.SourceDumpRoot>(paths.Il2cppPath)
                ?? throw new Exception("File is not a valid il2cpp dump json file");
            il2CppCache.ApplyIl2cppData(entries);
            TryApplyTypePatches(Il2cppCache, paths.TypePatchFilepath);

            var baseCacheFile = paths.EnumCacheFilename;
            GD.Print("Updating il2cpp cache... " + baseCacheFile);
            Directory.CreateDirectory(baseCacheFile.GetBaseDir());
            using var outfs = File.Create(baseCacheFile);
            JsonSerializer.Serialize(outfs, il2CppCache.ToCacheData(), jsonOptions);
            outfs.Close();
        }

        private bool TryApplyTypePatches(Il2cppCache target, string patchFilename)
        {
            if (!File.Exists(patchFilename)) return false;

            if (TryDeserialize<Dictionary<string, TypePatch>>(patchFilename, out var patches)) {
                target.ApplyPatches(patches);
                return true;
            }
            return false;
        }

        private bool TryApplyIl2cppCache(Il2cppCache target, string cacheFilename)
        {
            if (TryDeserialize<Il2cppCacheData>(cacheFilename, out var data)) {
                if (data.CacheVersion < Il2cppCacheData.CurrentCacheVersion) {
                    GD.PrintErr("Il2cpp cache data is out of date, needs a rebuild.");
                    return false;
                }
                target.ApplyCacheData(data);
                return true;
            }
            return false;
        }

        private bool TryApplyEnumOverrides(Il2cppCache target, string sourceDir)
        {
            if (!Directory.Exists(sourceDir)) return false;

            var files = Directory.EnumerateFiles(sourceDir, "*.json");
            foreach (var file in files) {
                if (TryDeserialize<EnumOverrideRoot>(file, out var root)) {
                    var classname = Path.GetFileNameWithoutExtension(file);
                    if (target.enums.TryGetValue(classname, out var enumDesc)) {
                        if (root.DisplayLabels != null) {
                            enumDesc.ParseCacheData(root.DisplayLabels);
                        }
                    }
                }
            }
            return true;
        }

        private T? DeserializeOrNull<T>(string? filepath) where T : class => DeserializeOrNull<T>(filepath, default);
        private T? DeserializeOrNull<T>(string? filepath, T? _) where T : class
        {
            if (File.Exists(filepath)) {
                using var fs = File.OpenRead(filepath);
                return JsonSerializer.Deserialize<T>(fs, jsonOptions);
            }
            return null;
        }

        private bool TryDeserialize<T>(string? filepath, [MaybeNullWhen(false)] out T result)
        {
            if (File.Exists(filepath)) {
                using var fs = File.OpenRead(filepath);
                result = JsonSerializer.Deserialize<T>(fs, jsonOptions);
                return result != null;
            }
            result = default;
            return false;
        }
    }
}
