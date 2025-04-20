namespace ReaGE;

public enum SupportedGame
{
    Unknown = 0,
    DragonsDogma2 = 1,
    DevilMayCry5 = 2,
    ResidentEvil2 = 3,
    ResidentEvil2RT = 4,
    ResidentEvil3 = 5,
    ResidentEvil3RT = 6,
    ResidentEvil4 = 7,
    ResidentEvil7 = 8,
    ResidentEvil7RT = 9,
    ResidentEvil8 = 10,
    MonsterHunterRise = 11,
    StreetFighter6 = 12,
    MonsterHunterWilds = 13,
}

public enum SupportedFileFormats
{
    Unknown,
    Mesh,
    Texture,
    Scene,
    Prefab,
    Userdata,
    MaterialDefinition,
    Rcol,
    Uvar,
    MotionList,
    MotionBank,
    CollisionFilter,
    CollisionDefinition,
    CollisionMaterial,
    Foliage,
    MasterMaterial,
    GpuBuffer,
    RenderTexture,
    Efx,
    Timeline,
    UVSequence,
    Gui,
    MeshCollider,
    MotionFsm,
    MotionFsm2,
    Chain,
    Chain2,
    LightProbe,
    Probe,
    AiMap,
}

public static class EnumExtensions
{
    public static bool UsesEmbeddedUserdata(this SupportedGame game) => game switch {
        SupportedGame.DevilMayCry5 => true,
        SupportedGame.ResidentEvil2 => true,
        SupportedGame.ResidentEvil3 => true,
        SupportedGame.ResidentEvil7 => true,
        _ => false
    };

    public static string ToShortName(this SupportedGame game) => game switch {
        SupportedGame.DragonsDogma2 => "dd2",
        SupportedGame.DevilMayCry5 => "dmc5",
        SupportedGame.ResidentEvil2 => "re2",
        SupportedGame.ResidentEvil2RT => "re2rt",
        SupportedGame.ResidentEvil3 => "re3",
        SupportedGame.ResidentEvil3RT => "re3rt",
        SupportedGame.ResidentEvil4 => "re4",
        SupportedGame.ResidentEvil7 => "re7",
        SupportedGame.ResidentEvil7RT => "re7rt",
        SupportedGame.ResidentEvil8 => "re8",
        SupportedGame.MonsterHunterRise => "mhrise",
        SupportedGame.StreetFighter6 => "sf6",
        SupportedGame.MonsterHunterWilds => "mhwilds",
        _ => game.ToString(),
    };
}