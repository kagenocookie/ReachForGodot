namespace ReaGE;

using System;
using Godot;
using Godot.Collections;
using ReaGE.EFX;
using RszTool.Efx;
using RszTool.Efx.Structs.Common;

[GlobalClass, Tool]
public partial class EfxClipAnimationPlayer : AnimationPlayer
{
    private Array<Dictionary>? _propertyList;
    private static Lazy<PackedScene> ClipFieldScene = new Lazy<PackedScene>(() => ResourceLoader.Load<PackedScene>("res://addons/ReachForGodot/Editor/Inspectors/Efx/ClipField.tscn"));

    private EfxClipList? cliplist;
    private IClipAttribute? attr;
    private EfxAttributeNode? node;
    private EfxInspectorPlugin? ui;

    public void Setup(EfxClipList cliplist, IClipAttribute attr, EfxAttributeNode? node, EfxInspectorPlugin? ui)
    {
        var isNewlySetup = this.cliplist == null;
        this.cliplist = cliplist;
        this.attr = attr;
        this.node = node;
        this.ui = ui;
        _propertyList = new Array<Dictionary>();
        if (!attr.Clip.IsParsed) {
            // AssetConverter.Instance.Efx.ExportObject(attr, target);
            attr.Clip.ParseClip();
        }
        var shouldSetupClipTracks = string.IsNullOrEmpty(CurrentAnimation);

        var anim = GetClipAnim();
        CurrentAnimation = "default";
        anim.Length = cliplist.Duration / EfxObject.ClipFps;

        ReimportAllTracks(true, isNewlySetup);
    }

    private Animation GetClipAnim()
    {
        Animation anim;
        AnimationLibrary animlib;
        if (!HasAnimationLibrary("")) {
            animlib = new AnimationLibrary();
            AddAnimationLibrary("", animlib);
        } else {
            animlib = GetAnimationLibrary("");
        }
        if (!animlib.HasAnimation("default")) {
            anim = new Animation();
            animlib.AddAnimation("default", anim);
        } else {
            anim = animlib.GetAnimation("default");
        }
        return anim;
    }

    private void ReimportUpdatedTracks()
    {
        ReimportAllTracks(false, true);
        TryUpdateSourceData();
    }

    private void ReimportRegenerateUI()
    {
        ReimportAllTracks(true, true);
        TryUpdateSourceData();
    }

    private void TryUpdateSourceData()
    {
        if (node?.Data != null && attr != null && cliplist != null) {
            attr.Clip.SetFromClipList(cliplist);
            AssetConverter.Instance.Game = node.Data.Version.GetGameForEfxVersion();
            AssetConverter.Instance.Efx.ImportObject(node.Data.Get("clipData").As<EfxObject>(), attr.Clip);
        }
    }

    private readonly List<ClipField> clipFields = new();

    private void ReimportAllTracks(bool doUI, bool forceRegenTracks)
    {
        if ((attr == null || cliplist == null) && !FindTargetObjects(out cliplist, out attr)) return;

        _propertyList ??= new();
        _propertyList.Clear();
        if (clipFields.Count != 0 && (!IsInstanceValid(clipFields[0]) || !clipFields[0].IsInsideTree())) {
            clipFields.Clear();
        }
        var anim = GetClipAnim();
        var set = 0;
        for (int i = 0; i < attr.ClipBits.BitCount; ++i) {
            int myBitIndex = i;
            var enabled = attr.ClipBits.HasBit(myBitIndex);

            var bitName = attr.ClipBits.GetBitName(myBitIndex);
            var uiName = bitName ?? ("Field " + (i + 1));
            // TODO set correct default value type for empty int-clips
            var valueType = cliplist.clips.FirstOrDefault()?.valueType ?? ClipValueType.Float;
            var clip = enabled ? cliplist.clips[set++] : null;

            if (doUI && ui != null) {
                var ctrl = ClipFieldScene.Value.Instantiate<ClipField>();
                clipFields.Add(ctrl);
                ctrl.Setup(enabled, uiName, valueType, clip);
                ui.AddCustomControl(ctrl);
                ctrl.ValuesChanged += ReimportUpdatedTracks;
                ctrl.StructureChanged += () => {
                    ctrl.Setup(true, uiName, valueType, clip);
                    ReimportUpdatedTracks();
                };
                ctrl.Toggled += (toggled) => {
                    var index = attr.ClipBits.GetBitInsertIndex(myBitIndex);
                    attr.ClipBits.SetBit(myBitIndex, toggled);
                    if (toggled) {
                        clip = cliplist.AddClip(index, myBitIndex);
                    } else {
                        cliplist.RemoveClip(index);
                    }
                    attr.Clip.SetFromClipList(cliplist);
                    TryUpdateSourceData();
                    if (toggled && clip != null) {
                        ctrl.Setup(toggled, uiName, clip.valueType, clip);
                    } else {
                        ctrl.Setup(toggled, uiName, cliplist.clips.FirstOrDefault()?.valueType ?? ClipValueType.Float, null);
                    }
                    ReimportUpdatedTracks();
                };
            } else if (ui != null) {
                var cf = clipFields[i];
                cf.Setup(enabled, uiName, valueType, clip);
            }

            _propertyList.Add(new Dictionary() {
                ["name"] = uiName,
                ["type"] = (int)(valueType == ClipValueType.Float ? Variant.Type.Float : Variant.Type.Int),
                // ["usage"] = (uint)(PropertyUsageFlags.Editor|PropertyUsageFlags.ScriptVariable),
                ["usage"] = (uint)(PropertyUsageFlags.ScriptVariable),
            });

            var trackPath = ".:" + uiName;
            var trackId = anim.FindTrack(trackPath, Animation.TrackType.Bezier);
            if (trackId == -1) {
                if (enabled) {
                    trackId = anim.AddTrack(Animation.TrackType.Bezier);
                    anim.TrackSetPath(trackId, trackPath);
                    if (clip != null) {
                        ImportTrack(anim, trackId, clip);
                    }
                }
            } else {
                if (!enabled) {
                    anim.RemoveTrack(trackId);
                } else if (forceRegenTracks && clip != null) {
                    ImportTrack(anim, trackId, clip);
                }
            }
        }
    }

    private void ImportTrack(Animation anim, int trackId, EfxParsedClip clip)
    {
        var existingKeys = anim.TrackGetKeyCount(trackId);
        for (int i = existingKeys - 1; i >= 0; i--) anim.TrackRemoveKey(trackId, i);

        foreach (var frame in clip.frames) {
            var key = anim.BezierTrackInsertKey(trackId, frame.data.frameTime / EfxObject.ClipFps, frame.data.AsFloat(clip.valueType));
            if (frame.data.type == FrameInterpolationType.Bezier) {
                anim.BezierTrackSetKeyInHandle(trackId, key, new Vector2(frame.tangents.in_x,frame.tangents.in_y));
                anim.BezierTrackSetKeyOutHandle(trackId, key, new Vector2(frame.tangents.out_x,frame.tangents.out_y));
            }
        }
    }

    private bool FindTargetObjects(out EfxClipList cliplist, out IClipAttribute attr)
    {
        var parent = this.GetParentOrNull<EfxAttributeNode>();
        if (parent != null && parent.Data is EfxObject efxObj && efxObj.ClassTags.Contains(EfxObject.ClassTagClipContainer) && efxObj.RuntimeObject is IClipAttribute clipAttr) {
            cliplist = clipAttr.Clip.ParsedClip;
            attr = clipAttr;
            return true;
        }
        cliplist = null!;
        attr = null!;
        return false;
    }

    public override Array<Dictionary> _GetPropertyList()
    {
        if (_propertyList == null) {
            var errorProp = new Dictionary() {
                { "name", "Select the parent node for full data editing functionality" },
                { "type", (int)Variant.Type.Nil },
                { "usage", (int)(PropertyUsageFlags.Category|PropertyUsageFlags.ScriptVariable) }
            };
            if (FindTargetObjects(out var cliplist, out var attr)) {
                Setup(cliplist, attr, null, null);
            } else {
                _propertyList = new Array<Dictionary>() { errorProp };
            }
            if (_propertyList == null) {
                _propertyList = new Array<Dictionary>() { errorProp };
            } else {
                _propertyList.Insert(0, errorProp);
            }
            return base._GetPropertyList();
        }
        return _propertyList;
    }
}