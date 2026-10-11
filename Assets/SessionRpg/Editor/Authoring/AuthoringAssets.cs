using System.IO;
using SessionRpg.Editor.Assets;
using SessionRpg.Editor.Performance;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SessionRpg.Editor.Authoring
{
    internal sealed class AuthoringAssets
    {
        public const string ArtFolder = "Assets/SessionRpg/Art";
        public const string MaterialFolder = ArtFolder + "/Materials";
        public const string SettingsFolder = "Assets/SessionRpg/Settings";
        public const string PrefabFolder = "Assets/SessionRpg/Prefabs";
        public const string SceneFolder = "Assets/SessionRpg/Scenes";

        private const string ArenaFlagstoneAlbedoPath =
            "Assets/SessionRpg/Art/Environment/Textures/ArenaFlagstoneAlbedo.png";

        private const string ArenaFloorMacroAlbedoPath =
            "Assets/SessionRpg/Art/Environment/Textures/ArenaFloorMacroAlbedo.png";

        private const string ArenaFloorMacroNormalPath =
            "Assets/SessionRpg/Art/Environment/Textures/ArenaFloorMacroNormal.png";

        private const string ArenaFloorMacroMaskPath =
            "Assets/SessionRpg/Art/Environment/Textures/ArenaFloorMacroMask.png";

        private const string ArenaFloorSurfaceAlbedoPath =
            "Assets/SessionRpg/Art/Environment/Textures/ArenaFloorSurfaceAlbedo.png";

        private const string ArenaFloorSurfaceNormalPath =
            "Assets/SessionRpg/Art/Environment/Textures/ArenaFloorSurfaceNormal.png";

        private const string ArenaFloorSurfaceMaskPath =
            "Assets/SessionRpg/Art/Environment/Textures/ArenaFloorSurfaceMask.png";

        private const float ArenaFloorMacroTiling = 1f;
        private const float ArenaFloorMicroDetailTiling = 4.6875f;
        private const float ArenaFloorMacroNormalStrength = 0.78f;
        private const float ArenaFloorDetailNormalStrength = 0.38f;
        private const float ArenaFloorDetailAlbedoStrength = 0.055f;
        private const float ArenaFloorSmoothness = 1f;

        internal const float ArenaLavaTiling = 1.7f;

        private const string ArenaLavaAlbedoPath =
            "Assets/SessionRpg/Art/Environment/Textures/ArenaLavaAlbedo.png";

        private const string FortressMasonryAlbedoPath =
            "Assets/ThirdParty/PolyHaven/StoneBrickWall001/" +
            "stone_brick_wall_001_diff_2k.png";

        private const string FortressMasonryNormalPath =
            "Assets/ThirdParty/PolyHaven/StoneBrickWall001/" +
            "stone_brick_wall_001_nor_gl_2k.png";

        private const string FortressMasonryMaskPath =
            "Assets/SessionRpg/Art/Environment/Textures/FortressMasonryMask.png";

        private const float FortressMasonryTiling = 2.65f;
        private const float FortressMasonryNormalStrength = 0.92f;
        private const float FortressMasonrySmoothness = 0.72f;

        private const string EnvironmentLookProfilePath =
            "Assets/SessionRpg/Settings/ArenaEnvironmentLook.asset";

        private const string EnvironmentReflectionPath =
            "Assets/SessionRpg/Settings/ArenaEnvironmentReflection.asset";

        private const float AshenCausewayStoneTiling = 2.15f;
        private const float AshenCausewayCinderTiling = 1.38f;
        private const float AshenCausewayGroutTiling = 2.1f;
        private const float AshenCausewayStoneNormalStrength = 0.48f;
        private const float AshenCausewayCinderNormalStrength = 0.34f;
        private const float AshenCausewayGroutNormalStrength = 0.26f;

        private static readonly Vector2 FortressMasonryOffset =
            new Vector2(0.31f, 0.18f);

        private const string ProductFontPath =
            "Assets/ThirdParty/Fonts/AlegreyaSansSC/AlegreyaSansSC-Medium.ttf";

        public Material Stone { get; private set; }

        public Material StoneVariation { get; private set; }

        public Material DarkStone { get; private set; }

        public Material Basalt { get; private set; }

        public Material ArenaFloorStone { get; private set; }

        public Material ArenaFloorTrim { get; private set; }

        public Material ArenaFloorGrout { get; private set; }

        public Material RuinStone { get; private set; }

        public Material AshenCausewayStone { get; private set; }

        public Material AshenCausewayStoneVariation { get; private set; }

        public Material AshenCausewayCinder { get; private set; }

        public Material AshenCausewayMasonry { get; private set; }

        public Material AshenCausewayPaving { get; private set; }

        public Material AshenCausewayDistantHaze { get; private set; }

        public Material AshenCausewayMoss { get; private set; }

        public Material AshenCausewayDryGrass { get; private set; }

        public Material KayKitMaterial { get; private set; }

        public Material SunbreakMeadow { get; private set; }
        public Material SunbreakLimestone { get; private set; }
        public Material SunbreakRock { get; private set; }
        public Material SunbreakPaving { get; private set; }
        public Material SunbreakGrass { get; private set; }
        public Material SunbreakFoliage { get; private set; }
        public Material SunbreakFoliageLight { get; private set; }
        public Material SunbreakBark { get; private set; }
        public Material SunbreakCanvas { get; private set; }
        public Material SunbreakFlowers { get; private set; }

        public Material AshenCausewayGrout { get; private set; }

        public Material AshenCausewaySoot { get; private set; }

        public Material AshenCausewayLavaCrust { get; private set; }

        public Material AshenCausewayLavaCore { get; private set; }

        public Material FloorAccent { get; private set; }

        public Material Abyss { get; private set; }

        public Material ContactShadow { get; private set; }

        public Material Lava { get; private set; }

        public Material LavaSeam { get; private set; }

        public Material Blue { get; private set; }

        public Material Red { get; private set; }

        public Material Gold { get; private set; }

        public Material Health { get; private set; }

        public Material Barrier { get; private set; }

        public Material Cast { get; private set; }

        public Material Telegraph { get; private set; }

        public Material CombatEffect { get; private set; }

        public Material CombatFragment { get; private set; }

        public Material CombatRibbon { get; private set; }

        public Material CombatParticle { get; private set; }

        public UniversalRenderPipelineAsset Pipeline { get; private set; }

        public VolumeProfile EnvironmentLookProfile { get; private set; }

        public Cubemap EnvironmentReflection { get; private set; }

        public MobileQualityAssetSet MobileQuality { get; private set; }

        public Font Font { get; private set; }

        public FortressSourceArtSet FortressSourceArt { get; private set; }

        public GameObject ArenaFloorSourceArt { get; private set; }

        public static AuthoringAssets Build()
        {
            EnsureFolders();
            Texture2D stoneDetail = EnsureAuthoredSurfaceTexture(
                ArenaFlagstoneAlbedoPath);
            Texture2D arenaFloorMacroAlbedo = EnsureAuthoredSurfaceTexture(
                ArenaFloorMacroAlbedoPath);
            Texture2D arenaFloorMacroNormal = EnsureAuthoredSurfaceTexture(
                ArenaFloorMacroNormalPath,
                textureType: TextureImporterType.NormalMap,
                sRgb: false,
                alphaSource: TextureImporterAlphaSource.None);
            Texture2D arenaFloorMacroMask = EnsureAuthoredSurfaceTexture(
                ArenaFloorMacroMaskPath,
                textureType: TextureImporterType.Default,
                sRgb: false,
                alphaSource: TextureImporterAlphaSource.FromInput);
            Texture2D arenaFloorSurfaceAlbedo = EnsureAuthoredSurfaceTexture(
                ArenaFloorSurfaceAlbedoPath,
                textureType: TextureImporterType.Default,
                sRgb: false,
                alphaSource: TextureImporterAlphaSource.None);
            Texture2D arenaFloorNormal = EnsureAuthoredSurfaceTexture(
                ArenaFloorSurfaceNormalPath,
                textureType: TextureImporterType.NormalMap,
                sRgb: false,
                alphaSource: TextureImporterAlphaSource.None);
            EnsureAuthoredSurfaceTexture(
                ArenaFloorSurfaceMaskPath,
                textureType: TextureImporterType.Default,
                sRgb: false,
                alphaSource: TextureImporterAlphaSource.FromInput);
            Texture2D lavaDetail = EnsureAuthoredSurfaceTexture(
                ArenaLavaAlbedoPath);
            Texture2D fortressDetail = EnsureAuthoredSurfaceTexture(
                FortressMasonryAlbedoPath,
                maxTextureSize: 2048);
            Texture2D fortressNormal = EnsureAuthoredSurfaceTexture(
                FortressMasonryNormalPath,
                textureType: TextureImporterType.NormalMap,
                sRgb: false,
                alphaSource: TextureImporterAlphaSource.None,
                maxTextureSize: 2048);
            Texture2D fortressMask = EnsureAuthoredSurfaceTexture(
                FortressMasonryMaskPath,
                textureType: TextureImporterType.Default,
                sRgb: false,
                alphaSource: TextureImporterAlphaSource.FromInput);
            AuthoringAssets assets = new AuthoringAssets
            {
                FortressSourceArt = FortressSourceArtValidator.LoadValidated(),
                ArenaFloorSourceArt = ArenaFloorSourceArtValidator.LoadValidated(),
                Stone = EnsureMaterial(
                    MaterialFolder + "/Stone.mat",
                    new Color(0.72f, 0.78f, 0.9f, 1f),
                    false,
                    false,
                    0.28f),
                StoneVariation = EnsureMaterial(
                    MaterialFolder + "/StoneVariation.mat",
                    new Color(0.76f, 0.8f, 0.87f, 1f),
                    false,
                    false,
                    0.32f),
                DarkStone = EnsureMaterial(
                    MaterialFolder + "/DarkStone.mat",
                    new Color(0.09f, 0.11f, 0.15f, 1f),
                    false,
                    false,
                    0.2f),
                Basalt = EnsureMaterial(
                    MaterialFolder + "/Basalt.mat",
                    new Color(0.075f, 0.09f, 0.125f, 1f),
                    false,
                    false,
                    0.12f),
                ArenaFloorStone = EnsureMaterial(
                    MaterialFolder + "/ArenaFloor_Stone.mat",
                    new Color(0.9f, 0.9f, 0.94f, 1f),
                    false,
                    false,
                    0.18f),
                ArenaFloorTrim = EnsureMaterial(
                    MaterialFolder + "/ArenaFloor_Trim.mat",
                    new Color(0.34f, 0.22f, 0.1f, 1f),
                    false,
                    false,
                    0.34f,
                    0.34f),
                ArenaFloorGrout = EnsureMaterial(
                    MaterialFolder + "/ArenaFloor_Grout.mat",
                    new Color(0.018f, 0.022f, 0.03f, 1f),
                    false,
                    false,
                    0.04f),
                RuinStone = EnsureMaterial(
                    MaterialFolder + "/RuinStone.mat",
                    new Color(0.78f, 0.76f, 0.76f, 1f),
                    false,
                    false,
                    FortressMasonrySmoothness),
                AshenCausewayStone = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_Stone.mat",
                    new Color(0.88f, 0.81f, 0.68f, 1f),
                    false,
                    false,
                    0.13f,
                    0f),
                AshenCausewayStoneVariation = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_StoneVariation.mat",
                    new Color(0.69f, 0.72f, 0.66f, 1f),
                    false,
                    false,
                    0.1f,
                    0f),
                AshenCausewayCinder = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_Cinder.mat",
                    new Color(0.36f, 0.32f, 0.26f, 1f),
                    false,
                    false,
                    0.055f,
                    0f),
                AshenCausewayGrout = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_Grout.mat",
                    new Color(0.23f, 0.22f, 0.2f, 1f),
                    false,
                    false,
                    0.045f,
                    0f),
                AshenCausewayMasonry = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_Masonry.mat",
                    new Color(0.92f, 0.86f, 0.73f, 1f), false, false, 0.36f, 0f),
                AshenCausewayPaving = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_Paving.mat",
                    new Color(0.32f, 0.33f, 0.30f, 1f), false, false, 0.16f, 0f),
                AshenCausewayDistantHaze = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_DistantHaze.mat",
                    new Color(0.14f, 0.185f, 0.2f, 1f), false, false, 0f, 0f, 1f, true),
                AshenCausewayMoss = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_Moss.mat",
                    new Color(0.24f, 0.28f, 0.13f, 1f), false, false, 0.03f, 0f),
                AshenCausewayDryGrass = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_DryGrass.mat",
                    new Color(0.38f, 0.33f, 0.21f, 1f), false, false, 0.02f, 0f),
                KayKitMaterial = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_DetailAtlas.mat",
                    new Color(0.62f, 0.57f, 0.49f, 1f), false, false, 0.12f, 0f),
                SunbreakMeadow = EnsureMaterial(MaterialFolder + "/Sunbreak_Meadow.mat",
                    Color.white, false, false, 0.06f, 0f),
                SunbreakLimestone = EnsureMaterial(MaterialFolder + "/Sunbreak_Limestone.mat",
                    new Color(0.83f, 0.81f, 0.75f), false, false, 0.22f, 0f),
                SunbreakRock = EnsureMaterial(MaterialFolder + "/Sunbreak_Rock.mat",
                    new Color(0.67f, 0.66f, 0.61f), false, false, 0.08f, 0f),
                SunbreakPaving = EnsureMaterial(MaterialFolder + "/Sunbreak_Paving.mat",
                    new Color(0.73f, 0.71f, 0.64f), false, false, 0.12f, 0f),
                SunbreakGrass = EnsureMaterial(MaterialFolder + "/Sunbreak_Grass.mat",
                    new Color(0.23f, 0.29f, 0.13f), false, false, 0.02f, 0f),
                SunbreakFoliage = EnsureMaterial(MaterialFolder + "/Sunbreak_Foliage.mat",
                    new Color(0.82f, 0.98f, 0.83f), false, false, 0.08f, 0f),
                SunbreakFoliageLight = EnsureMaterial(MaterialFolder + "/Sunbreak_FoliageLight.mat",
                    new Color(1.0f, 1.10f, 0.90f), false, false, 0.08f, 0f),
                SunbreakBark = EnsureMaterial(MaterialFolder + "/Sunbreak_Bark.mat",
                    new Color(0.75f, 0.68f, 0.59f), false, false, 0.07f, 0f),
                SunbreakCanvas = EnsureMaterial(MaterialFolder + "/Sunbreak_Canvas.mat",
                    new Color(0.54f, 0.41f, 0.23f), false, false, 0.04f, 0f),
                SunbreakFlowers = EnsureMaterial(MaterialFolder + "/Sunbreak_Flowers.mat",
                    new Color(0.69f, 0.62f, 0.39f), false, false, 0.02f, 0f),
                AshenCausewaySoot = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_Soot.mat",
                    new Color(0.022f, 0.018f, 0.018f, 0.16f),
                    false,
                    true,
                    0f,
                    0f),
                AshenCausewayLavaCrust = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_LavaCrust.mat",
                    new Color(0.42f, 0.075f, 0.018f, 1f),
                    false,
                    false,
                    0f,
                    0f,
                    1f,
                    true),
                AshenCausewayLavaCore = EnsureMaterial(
                    MaterialFolder + "/AshenCauseway_LavaCore.mat",
                    new Color(1f, 0.36f, 0.035f, 1f),
                    false,
                    false,
                    0f,
                    0f,
                    1f,
                    true),
                FloorAccent = EnsureMaterial(
                    MaterialFolder + "/FloorAccent.mat",
                    new Color(0.38f, 0.27f, 0.12f, 1f),
                    true,
                    false,
                    0.2f,
                    0f,
                    0.35f),
                Abyss = EnsureMaterial(
                    MaterialFolder + "/Abyss.mat",
                    new Color(0.018f, 0.012f, 0.014f, 1f),
                    false,
                    false,
                    0f,
                    0f,
                    1f,
                    true),
                ContactShadow = EnsureMaterial(
                    MaterialFolder + "/ContactShadow.mat",
                    new Color(0.005f, 0.008f, 0.015f, 0.58f),
                    false,
                    true),
                Lava = EnsureMaterial(
                    MaterialFolder + "/Lava.mat",
                    new Color(0.86f, 0.48f, 0.2f, 1f),
                    true,
                    false,
                    0.18f,
                    0f,
                    0.9f,
                    false),
                LavaSeam = EnsureMaterial(
                    MaterialFolder + "/LavaSeam.mat",
                    new Color(0.92f, 0.16f, 0.018f, 1f),
                    true,
                    false,
                    0.18f,
                    0f,
                    1.45f,
                    false),
                Blue = EnsureMaterial(
                    MaterialFolder + "/PartyBlue.mat",
                    new Color(0.08f, 0.58f, 1f, 1f),
                    true,
                    false),
                Red = EnsureMaterial(
                    MaterialFolder + "/EnemyRed.mat",
                    new Color(1f, 0.13f, 0.06f, 1f),
                    true,
                    false),
                Gold = EnsureMaterial(
                    MaterialFolder + "/HumanGold.mat",
                    new Color(1f, 0.72f, 0.12f, 1f),
                    true,
                    false),
                Health = EnsureMaterial(
                    MaterialFolder + "/Health.mat",
                    new Color(0.12f, 0.92f, 0.42f, 1f),
                    true,
                    false),
                Barrier = EnsureMaterial(
                    MaterialFolder + "/Barrier.mat",
                    new Color(0.2f, 0.82f, 1f, 1f),
                    true,
                    false),
                Cast = EnsureMaterial(
                    MaterialFolder + "/Cast.mat",
                    new Color(0.76f, 0.32f, 1f, 1f),
                    true,
                    false),
                Telegraph = EnsureMaterial(
                    MaterialFolder + "/Telegraph.mat",
                    new Color(1f, 0.3f, 0.04f, 0.58f),
                    true,
                    true),
                CombatEffect = EnsureMaterial(
                    MaterialFolder + "/CombatEffect.mat",
                    new Color(1f, 1f, 1f, 0.76f),
                    false,
                    true),
                CombatFragment = EnsureMaterial(
                    MaterialFolder + "/CombatFragment.mat",
                    Color.white,
                    true,
                    false,
                    0.28f,
                    0.04f,
                    0.72f),
                CombatRibbon = EnsureRibbonMaterial(
                    MaterialFolder + "/CombatRibbon.mat"),
                CombatParticle = EnsureParticleMaterial(
                    MaterialFolder + "/CombatParticle.mat",
                    ResolveCombatParticleTexture()),
                Font = ResolveProductFont(),
            };

            ConfigureSpellMeshMaterial(assets.CombatEffect);
            ConfigureSpellFragmentMaterial(assets.CombatFragment);

            ConfigureAshenCausewaySurface(
                assets.Stone,
                stoneDetail,
                arenaFloorNormal,
                2.25f,
                new Vector2(0.11f, 0.27f),
                0.55f);
            ConfigureAshenCausewaySurface(
                assets.StoneVariation,
                stoneDetail,
                arenaFloorNormal,
                2.35f,
                new Vector2(0.47f, 0.08f),
                0.48f);
            ConfigureArenaFloorSurface(
                assets.ArenaFloorStone,
                arenaFloorMacroAlbedo,
                arenaFloorMacroNormal,
                arenaFloorMacroMask,
                arenaFloorSurfaceAlbedo,
                arenaFloorNormal);
            ClearSurfaceDetail(assets.DarkStone);
            ClearSurfaceDetail(assets.Basalt);
            ClearSurfaceDetail(assets.ArenaFloorTrim);
            ClearSurfaceDetail(assets.ArenaFloorGrout);
            ConfigureSurfaceDetail(
                assets.Abyss,
                lavaDetail,
                0.85f,
                new Vector2(0.37f, 0.19f));
            ConfigureFortressSurface(
                assets.RuinStone,
                fortressDetail,
                fortressNormal,
                fortressMask,
                FortressMasonryTiling,
                FortressMasonryOffset);
            ConfigureFortressSurface(
                assets.AshenCausewayMasonry,
                fortressDetail, fortressNormal, fortressMask,
                FortressMasonryTiling, FortressMasonryOffset);
            ConfigureAshenCausewaySurface(
                assets.AshenCausewayPaving,
                arenaFloorSurfaceAlbedo, arenaFloorNormal,
                1.1f, new Vector2(0.17f, 0.39f), 0.32f);
            ConfigureSurfaceDetail(assets.KayKitMaterial,
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ThirdParty/KayKit/Dungeon/Environment/dungeon_texture.png"),
                1f, Vector2.zero);
            SunbreakSurfaceAuthoring.Configure(assets);
            SunbreakFortAssets.Configure();
            SunbreakVegetationAssets.Configure();
            SunbreakCanopyAuthoring.BuildParts();
            ConfigureAshenCausewaySurface(
                assets.AshenCausewayStone,
                stoneDetail,
                arenaFloorNormal,
                AshenCausewayStoneTiling,
                new Vector2(0.13f, 0.29f),
                AshenCausewayStoneNormalStrength);
            ConfigureAshenCausewaySurface(
                assets.AshenCausewayStoneVariation,
                stoneDetail,
                arenaFloorNormal,
                AshenCausewayStoneTiling * 1.07f,
                new Vector2(0.57f, 0.08f),
                AshenCausewayStoneNormalStrength * 0.9f);
            ConfigureAshenCausewaySurface(
                assets.AshenCausewayCinder,
                arenaFloorSurfaceAlbedo,
                arenaFloorNormal,
                AshenCausewayCinderTiling,
                new Vector2(0.38f, 0.61f),
                AshenCausewayCinderNormalStrength);
            ConfigureAshenCausewaySurface(
                assets.AshenCausewayGrout,
                arenaFloorSurfaceAlbedo,
                arenaFloorNormal,
                AshenCausewayGroutTiling,
                new Vector2(0.72f, 0.43f),
                AshenCausewayGroutNormalStrength);
            ConfigureSurfaceDetail(
                assets.AshenCausewayLavaCrust,
                lavaDetail,
                1.85f,
                new Vector2(0.09f, 0.42f));
            ConfigureSurfaceDetail(
                assets.AshenCausewayLavaCore,
                lavaDetail,
                2.65f,
                new Vector2(0.63f, 0.17f));
            ConfigureSurfaceDetail(
                assets.Lava,
                lavaDetail,
                ArenaLavaTiling,
                new Vector2(0.16f, 0.38f),
                true);

            assets.MobileQuality = MobileQualityAssetAuthoring.CreateOrUpdateDefaultAssets();
            assets.Pipeline = assets.MobileQuality.HighPipeline;
            assets.EnvironmentLookProfile = EnsureEnvironmentLookProfile();
            assets.EnvironmentReflection = EnsureEnvironmentReflection();
            AssetDatabase.SaveAssets();
            // Creating the profile's sub-assets and the cubemap can refresh the
            // AssetDatabase. Reacquire both UnityEngine.Object references so the
            // scene builder never receives a stale object from before that import.
            assets.EnvironmentLookProfile =
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                    EnvironmentLookProfilePath);
            assets.EnvironmentReflection = AssetDatabase.LoadAssetAtPath<Cubemap>(
                EnvironmentReflectionPath);
            if (assets.EnvironmentLookProfile == null ||
                assets.EnvironmentReflection == null)
            {
                throw new InvalidDataException(
                    "Generated environment look assets could not be reloaded.");
            }

            return assets;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "SessionRpg");
            EnsureFolder("Assets/SessionRpg", "Art");
            EnsureFolder(ArtFolder, "Materials");
            EnsureFolder("Assets/SessionRpg", "Settings");
            EnsureFolder("Assets/SessionRpg", "Prefabs");
            EnsureFolder("Assets/SessionRpg", "Scenes");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static Material EnsureMaterial(
            string path,
            Color color,
            bool emission,
            bool transparent,
            float smoothness = 0.22f,
            float metallic = 0f,
            float emissionMultiplier = 1.35f,
            bool unlit = false)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(transparent || unlit
                ? "Universal Render Pipeline/Unlit"
                : "Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(path),
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            }

            if (emission && material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * Mathf.Max(0f, emissionMultiplier));
                SetEmissionKeyword(material, true);
            }
            else if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
                SetEmissionKeyword(material, false);
            }

            if (transparent)
            {
                ConfigureTransparentBlend(material, false);
            }
            else
            {
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 0f);
                }

                if (material.HasProperty("_ZWrite"))
                {
                    material.SetFloat("_ZWrite", 1f);
                }

                material.renderQueue = -1;
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureRibbonMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(path),
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            ConfigureTransparentBlend(material, true);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureSpellMeshMaterial(Material material)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            ConfigureTransparentBlend(material, false);
            SetEmissionKeyword(material, true);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.white * 1.65f);
            }

            EditorUtility.SetDirty(material);
        }

        private static void ConfigureSpellFragmentMaterial(Material material)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 1f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat(
                    "_Cull",
                    (float)UnityEngine.Rendering.CullMode.Back);
            }

            material.renderQueue = -1;
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            SetEmissionKeyword(material, true);
            EditorUtility.SetDirty(material);
        }

        private static Texture2D EnsureParticleTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            const int size = 32;
            if (texture == null)
            {
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedX = ((x + 0.5f) / size) * 2f - 1f;
                    float normalizedY = ((y + 0.5f) / size) * 2f - 1f;
                    float radius = Mathf.Sqrt(
                        (normalizedX * normalizedX) +
                        (normalizedY * normalizedY));
                    float alpha = 1f - Mathf.SmoothStep(0.08f, 1f, radius);
                    alpha *= alpha;
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Texture2D ResolveCombatParticleTexture()
        {
            const string wispPath =
                "Assets/SessionRpg/Art/Vfx/SpellParticleWisp.png";
            ConfigureCombatParticleTexture(wispPath);
            Texture2D wisp = AssetDatabase.LoadAssetAtPath<Texture2D>(wispPath);
            return wisp != null
                ? wisp
                : EnsureParticleTexture(
                    MaterialFolder + "/CombatParticleTexture.asset");
        }

        private static void ConfigureCombatParticleTexture(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path)
                as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed =
                importer.textureType != TextureImporterType.Default ||
                importer.sRGBTexture ||
                importer.alphaSource != TextureImporterAlphaSource.None ||
                importer.alphaIsTransparency ||
                importer.mipmapEnabled == false ||
                Mathf.Approximately(importer.mipMapBias, -0.25f) == false ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.anisoLevel != 1 ||
                importer.maxTextureSize != 512 ||
                importer.npotScale != TextureImporterNPOTScale.ToNearest ||
                importer.isReadable ||
                importer.textureCompression != TextureImporterCompression.Uncompressed;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.mipMapBias = -0.25f;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.maxTextureSize = 512;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            changed |=
                android.overridden == false ||
                android.maxTextureSize != 512 ||
                android.format != TextureImporterFormat.ASTC_6x6 ||
                android.textureCompression != TextureImporterCompression.Compressed ||
                android.compressionQuality != 50 ||
                android.crunchedCompression;
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = 512;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.textureCompression = TextureImporterCompression.Compressed;
            android.compressionQuality = 50;
            android.crunchedCompression = false;
            importer.SetPlatformTextureSettings(android);

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static Texture2D EnsureAuthoredSurfaceTexture(
            string path,
            TextureImporterType textureType = TextureImporterType.Default,
            bool sRgb = true,
            TextureImporterAlphaSource alphaSource =
                TextureImporterAlphaSource.None,
            bool convertToNormalMap = false,
            float heightMapScale = 0.25f,
            int maxTextureSize = 1024)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path)
                as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path)
                    as TextureImporter;
            }

            if (importer == null)
            {
                throw new InvalidDataException(
                    "Authored arena surface texture is missing: " + path);
            }

            bool changed =
                importer.textureType != textureType ||
                importer.sRGBTexture != sRgb ||
                importer.alphaSource != alphaSource ||
                importer.alphaIsTransparency ||
                importer.convertToNormalmap != convertToNormalMap ||
                Mathf.Approximately(
                    importer.heightmapScale,
                    heightMapScale) == false ||
                importer.flipGreenChannel ||
                importer.mipmapEnabled == false ||
                importer.wrapMode != TextureWrapMode.Repeat ||
                importer.filterMode != FilterMode.Trilinear ||
                importer.anisoLevel != 8 ||
                importer.maxTextureSize != maxTextureSize ||
                importer.npotScale != TextureImporterNPOTScale.ToNearest ||
                importer.isReadable;
            importer.textureType = textureType;
            importer.sRGBTexture = sRgb;
            importer.alphaSource = alphaSource;
            importer.alphaIsTransparency = false;
            importer.convertToNormalmap = convertToNormalMap;
            importer.heightmapScale = heightMapScale;
            importer.flipGreenChannel = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = maxTextureSize;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.isReadable = false;

            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            changed |=
                android.overridden == false ||
                android.maxTextureSize != maxTextureSize ||
                android.format != TextureImporterFormat.ASTC_4x4 ||
                android.textureCompression != TextureImporterCompression.Compressed ||
                android.compressionQuality != 100 ||
                android.crunchedCompression;
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = maxTextureSize;
            android.format = TextureImporterFormat.ASTC_4x4;
            android.textureCompression = TextureImporterCompression.Compressed;
            android.compressionQuality = 100;
            android.crunchedCompression = false;
            importer.SetPlatformTextureSettings(android);
            if (changed)
            {
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                path);
            if (texture == null)
            {
                throw new InvalidDataException(
                    "Unity could not import the arena surface texture: " + path);
            }

            return texture;
        }

        private static void EnsureDerivedTextureCopy(
            string sourcePath,
            string derivativePath)
        {
            string projectRoot = Path.GetDirectoryName(
                UnityEngine.Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidDataException(
                    "Unity project root is unavailable while deriving '" +
                    derivativePath + "'.");
            }

            string sourceAbsolutePath = Path.Combine(projectRoot, sourcePath);
            string derivativeAbsolutePath = Path.Combine(projectRoot, derivativePath);
            if (!File.Exists(sourceAbsolutePath))
            {
                throw new InvalidDataException(
                    "Authored texture source is missing: " + sourcePath);
            }

            byte[] sourceBytes = File.ReadAllBytes(sourceAbsolutePath);
            bool writeDerivative = !File.Exists(derivativeAbsolutePath);
            if (!writeDerivative)
            {
                byte[] derivativeBytes = File.ReadAllBytes(derivativeAbsolutePath);
                writeDerivative = derivativeBytes.Length != sourceBytes.Length;
                for (int index = 0;
                     !writeDerivative && index < sourceBytes.Length;
                     index++)
                {
                    writeDerivative = derivativeBytes[index] != sourceBytes[index];
                }
            }

            if (!writeDerivative)
            {
                return;
            }

            File.WriteAllBytes(derivativeAbsolutePath, sourceBytes);
            AssetDatabase.ImportAsset(
                derivativePath,
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static VolumeProfile EnsureEnvironmentLookProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                EnvironmentLookProfilePath);
            if (profile == null)
            {
                profile = VolumeProfileFactory.CreateVolumeProfileAtPath(
                    EnvironmentLookProfilePath);
                profile.name = "ArenaEnvironmentLook";
            }

            ColorAdjustments color = GetOrAddVolumeComponent<ColorAdjustments>(
                profile);
            color.active = true;
            color.postExposure.Override(0.42f);
            color.contrast.Override(4f);
            color.colorFilter.Override(new Color(1f, 0.99f, 0.975f, 1f));
            color.hueShift.Override(0f);
            color.saturation.Override(-2f);

            WhiteBalance whiteBalance = GetOrAddVolumeComponent<WhiteBalance>(
                profile);
            whiteBalance.active = true;
            whiteBalance.temperature.Override(1f);
            whiteBalance.tint.Override(0f);

            Tonemapping tonemapping = GetOrAddVolumeComponent<Tonemapping>(
                profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            Bloom bloom = GetOrAddVolumeComponent<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.18f);
            bloom.scatter.Override(0.55f);
            bloom.clamp.Override(8f);
            bloom.tint.Override(new Color(1f, 0.82f, 0.64f, 1f));
            bloom.highQualityFiltering.Override(true);
            bloom.downscale.Override(BloomDownscaleMode.Half);
            bloom.maxIterations.Override(5);

            Vignette vignette = GetOrAddVolumeComponent<Vignette>(profile);
            vignette.active = true;
            vignette.color.Override(new Color(0.006f, 0.008f, 0.016f, 1f));
            vignette.center.Override(new Vector2(0.5f, 0.49f));
            vignette.intensity.Override(0.065f);
            vignette.smoothness.Override(0.48f);
            vignette.rounded.Override(false);

            EditorUtility.SetDirty(color);
            EditorUtility.SetDirty(whiteBalance);
            EditorUtility.SetDirty(tonemapping);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(vignette);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                component.SetAllOverridesTo(true);
                return component;
            }

            return VolumeProfileFactory.CreateVolumeComponent<T>(
                profile,
                true,
                false);
        }

        private static Cubemap EnsureEnvironmentReflection()
        {
            const int size = 64;
            Cubemap authored = new Cubemap(
                size,
                TextureFormat.RGBAHalf,
                true)
            {
                name = "ArenaEnvironmentReflection",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };

            foreach (CubemapFace face in System.Enum.GetValues(typeof(CubemapFace)))
            {
                if (face == CubemapFace.Unknown)
                {
                    continue;
                }

                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float u = ((x + 0.5f) / size * 2f) - 1f;
                        float v = ((y + 0.5f) / size * 2f) - 1f;
                        Vector3 direction = GetCubemapDirection(face, u, v);
                        float skyBlend = Mathf.SmoothStep(-0.18f, 0.42f, direction.y);
                        float horizon = Mathf.Pow(
                            Mathf.Clamp01(1f - Mathf.Abs(direction.y)),
                            6f);
                        float emberSide = Mathf.Pow(
                            Mathf.Clamp01((direction.x * 0.5f) + 0.5f),
                            4f) * horizon;
                        Color ground = new Color(0.006f, 0.009f, 0.018f, 1f);
                        Color sky = new Color(0.105f, 0.13f, 0.2f, 1f);
                        Color value = Color.Lerp(ground, sky, skyBlend);
                        value += new Color(0.24f, 0.055f, 0.012f, 0f) *
                            horizon * (0.32f + (emberSide * 0.68f));
                        pixels[(y * size) + x] = value;
                    }
                }

                authored.SetPixels(pixels, face);
            }

            authored.Apply(true, false);
            Cubemap asset = AssetDatabase.LoadAssetAtPath<Cubemap>(
                EnvironmentReflectionPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(authored, EnvironmentReflectionPath);
                return authored;
            }

            EditorUtility.CopySerialized(authored, asset);
            Object.DestroyImmediate(authored);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Vector3 GetCubemapDirection(
            CubemapFace face,
            float u,
            float v)
        {
            switch (face)
            {
                case CubemapFace.PositiveX:
                    return new Vector3(1f, -v, -u).normalized;
                case CubemapFace.NegativeX:
                    return new Vector3(-1f, -v, u).normalized;
                case CubemapFace.PositiveY:
                    return new Vector3(u, 1f, v).normalized;
                case CubemapFace.NegativeY:
                    return new Vector3(u, -1f, -v).normalized;
                case CubemapFace.PositiveZ:
                    return new Vector3(u, -v, 1f).normalized;
                case CubemapFace.NegativeZ:
                    return new Vector3(-u, -v, -1f).normalized;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(face),
                        face,
                        null);
            }
        }

        private static Font ResolveProductFont()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(ProductFontPath);
            if (font != null)
            {
                return font;
            }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                throw new InvalidDataException(
                    "No product font is available. Expected '" +
                    ProductFontPath +
                    "' or Unity's LegacyRuntime.ttf fallback.");
            }

            return font;
        }

        private static void ConfigureSurfaceDetail(
            Material material,
            Texture2D texture,
            float tiling,
            Vector2 offset,
            bool useForEmission = false)
        {
            Vector2 scale = Vector2.one * Mathf.Max(0.01f, tiling);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", scale);
                material.SetTextureOffset("_MainTex", offset);
            }

            if (useForEmission && material.HasProperty("_EmissionMap"))
            {
                material.SetTexture("_EmissionMap", texture);
                material.SetTextureScale("_EmissionMap", scale);
                material.SetTextureOffset("_EmissionMap", offset);
                SetEmissionKeyword(material, true);
            }

            EditorUtility.SetDirty(material);
        }

        private static void SetEmissionKeyword(Material material, bool enabled)
        {
            LocalKeyword emissionKeyword = new LocalKeyword(
                material.shader,
                "_EMISSION");
            if (!emissionKeyword.isValid)
            {
                throw new InvalidDataException(
                    material.shader.name +
                    " does not expose the required _EMISSION local keyword.");
            }

            if (enabled)
            {
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.BakedEmissive;
                material.EnableKeyword(emissionKeyword);
            }
            else
            {
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                material.DisableKeyword(emissionKeyword);
            }
        }

        private static void ConfigureArenaFloorSurface(
            Material material,
            Texture2D macroAlbedo,
            Texture2D macroNormal,
            Texture2D macroMask,
            Texture2D detailAlbedo,
            Texture2D detailNormal)
        {
            ConfigureSurfaceDetail(
                material,
                macroAlbedo,
                ArenaFloorMacroTiling,
                Vector2.zero);
            Vector2 microDetailScale =
                Vector2.one * ArenaFloorMicroDetailTiling;
            // The full-floor FBX uses 30 m planar UVs. These offsets preserve the
            // previous 6.4 m microdetail phase after moving BaseMap to a single
            // non-repeating macro composition.
            var microDetailOffset = new Vector2(0.47125f, 0.64125f);

            // URP Lit samples _BumpMap with _BaseMap_ST, which is the correct UV
            // contract for the full-floor height derivative. Fine grain stays on
            // the independently tiled detail-normal path so joints and surface
            // relief respond to light at their authored scales.
            SetTextureWithTransform(
                material,
                "_BumpMap",
                macroNormal,
                Vector2.one,
                Vector2.zero);
            material.SetFloat("_BumpScale", ArenaFloorMacroNormalStrength);
            material.EnableKeyword("_NORMALMAP");

            SetTextureWithTransform(
                material,
                "_MetallicGlossMap",
                macroMask,
                Vector2.one,
                Vector2.zero);
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", ArenaFloorSmoothness);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");

            SetTextureWithTransform(
                material,
                "_OcclusionMap",
                macroMask,
                Vector2.one,
                Vector2.zero);
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_OCCLUSIONMAP");

            SetTextureWithTransform(
                material,
                "_DetailAlbedoMap",
                detailAlbedo,
                microDetailScale,
                microDetailOffset);
            material.SetFloat(
                "_DetailAlbedoMapScale",
                ArenaFloorDetailAlbedoStrength);
            SetTextureWithTransform(
                material,
                "_DetailNormalMap",
                detailNormal,
                Vector2.one,
                Vector2.zero);
            material.SetFloat(
                "_DetailNormalMapScale",
                ArenaFloorDetailNormalStrength);
            SetTextureWithTransform(
                material,
                "_DetailMask",
                null,
                Vector2.one,
                Vector2.zero);
            material.DisableKeyword("_DETAIL_MULX2");
            material.EnableKeyword("_DETAIL_SCALED");
            EditorUtility.SetDirty(material);
        }

        private static void ConfigureFortressSurface(
            Material material,
            Texture2D albedo,
            Texture2D normal,
            Texture2D mask,
            float tiling,
            Vector2 offset)
        {
            ConfigureSurfaceDetail(material, albedo, tiling, offset);
            Vector2 scale = Vector2.one * Mathf.Max(0.01f, tiling);
            SetTextureWithTransform(
                material,
                "_BumpMap",
                normal,
                scale,
                offset);
            material.SetFloat("_BumpScale", FortressMasonryNormalStrength);
            material.EnableKeyword("_NORMALMAP");

            SetTextureWithTransform(
                material,
                "_MetallicGlossMap",
                mask,
                scale,
                offset);
            SetTextureWithTransform(
                material,
                "_OcclusionMap",
                mask,
                scale,
                offset);
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", FortressMasonrySmoothness);
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (material.HasProperty("_EmissionMap"))
            {
                material.SetTexture("_EmissionMap", null);
                material.SetTextureScale("_EmissionMap", Vector2.one);
                material.SetTextureOffset("_EmissionMap", Vector2.zero);
            }

            EditorUtility.SetDirty(material);
        }

        private static void ConfigureAshenCausewaySurface(
            Material material,
            Texture2D albedo,
            Texture2D normal,
            float tiling,
            Vector2 offset,
            float normalStrength)
        {
            ConfigureSurfaceDetail(material, albedo, tiling, offset);
            Vector2 scale = Vector2.one * Mathf.Max(0.01f, tiling);
            SetTextureWithTransform(
                material,
                "_BumpMap",
                normal,
                scale,
                offset);
            material.SetFloat("_BumpScale", Mathf.Max(0f, normalStrength));
            material.EnableKeyword("_NORMALMAP");

            SetTextureWithTransform(
                material,
                "_MetallicGlossMap",
                null,
                Vector2.one,
                Vector2.zero);
            SetTextureWithTransform(
                material,
                "_OcclusionMap",
                null,
                Vector2.one,
                Vector2.zero);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_OCCLUSIONMAP");
            SetEmissionKeyword(material, false);
            if (material.HasProperty("_EmissionMap"))
            {
                material.SetTexture("_EmissionMap", null);
                material.SetTextureScale("_EmissionMap", Vector2.one);
                material.SetTextureOffset("_EmissionMap", Vector2.zero);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
        }

        private static void SetTextureWithTransform(
            Material material,
            string propertyName,
            Texture texture,
            Vector2 scale,
            Vector2 offset)
        {
            if (material.HasProperty(propertyName) == false)
            {
                throw new InvalidDataException(
                    "Arena floor material shader is missing required property '" +
                    propertyName +
                    "'.");
            }

            material.SetTexture(propertyName, texture);
            material.SetTextureScale(propertyName, scale);
            material.SetTextureOffset(propertyName, offset);
        }

        private static void ClearSurfaceDetail(Material material)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", null);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", null);
            }

            EditorUtility.SetDirty(material);
        }

        private static Material EnsureParticleMaterial(string path, Texture2D texture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(path),
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat(
                    "_Cull",
                    (float)UnityEngine.Rendering.CullMode.Off);
            }

            if (material.HasProperty("_SoftParticlesEnabled"))
            {
                material.SetFloat("_SoftParticlesEnabled", 0f);
            }

            ConfigureTransparentBlend(material, true);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTransparentBlend(
            Material material,
            bool additive)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", additive ? 2f : 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat(
                    "_SrcBlend",
                    (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat(
                    "_DstBlend",
                    (float)(additive
                        ? UnityEngine.Rendering.BlendMode.One
                        : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            }

            if (material.HasProperty("_SrcBlendAlpha"))
            {
                material.SetFloat(
                    "_SrcBlendAlpha",
                    (float)UnityEngine.Rendering.BlendMode.One);
            }

            if (material.HasProperty("_DstBlendAlpha"))
            {
                material.SetFloat(
                    "_DstBlendAlpha",
                    (float)(additive
                        ? UnityEngine.Rendering.BlendMode.One
                        : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0f);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
        }

    }
}
