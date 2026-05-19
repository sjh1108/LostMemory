using System;
using System.Collections.Generic;
using System.IO;
using LostMemory.Combat;
using LostMemory.Enemies.Boss.Rena;
using LostMemory.Stage;
using LostMemory.UI;
using MoreMountains.TopDownEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Editor.Enemies
{
    public static class RenaBossPrototypeBuilder
    {
        private const string RenaRootMenu = "Lost Memory/Enemies/Build Rena Boss Prototype";
        private const string Install2FBossTestMenu = "Lost Memory/Enemies/Install Rena Into 2F Boss Test Scene";
        private const string FixMagicShieldImportSettingsMenu =
            "Lost Memory/Enemies/Fix Rena Magic Shield Import Settings";
        private const string RenaArtRoot = "Assets/_Project/Art/Enemies/Boss/Rena";
        private const string IdleFramesFolder = RenaArtRoot + "/Idle";
        private const string MoveFramesFolder = RenaArtRoot + "/Move/Moving";
        private const string HurtFramesFolder = RenaArtRoot + "/Hurt";
        private const string DeathFramesFolder = RenaArtRoot + "/Death";
        private const string EntryFramesFolder = RenaArtRoot + "/Attacks/CometDive/Full/Glow";
        private const string FastCastFramesFolder = RenaArtRoot + "/Casts/FastCast/NoGlow";
        private const string HeavyCastFramesFolder = RenaArtRoot + "/Casts/HeavyCast/Full";
        private const string ThunderStrikeCastFramesFolder = RenaArtRoot + "/Casts/UpCast";
        private const string ThunderboltCastFramesFolder = RenaArtRoot + "/Casts/ThunderBolt/Full";
        private const string FireballCastedFramesFolder = RenaArtRoot + "/Spells/Fire/Fireball/Sprites/Casted/NoGlow";
        private const string FireballCastedGlowFramesFolder = RenaArtRoot + "/Spells/Fire/Fireball/Sprites/Casted";
        private const string FireballFramesFolder = RenaArtRoot + "/Spells/Fire/Fireball/Sprites/Loop/NoGlow";
        private const string FireballGlowFramesFolder = RenaArtRoot + "/Spells/Fire/Fireball/Sprites/Loop";
        private const string FireballHitFramesFolder = RenaArtRoot + "/Spells/Fire/Fireball/Sprites/Hit/NoGlow";
        private const string FireballHitGlowFramesFolder = RenaArtRoot + "/Spells/Fire/Fireball/Sprites/Hit";
        private const string InfernoFramesFolder = RenaArtRoot + "/Spells/Fire/Infierno/NoGlow";
        private const string InfernoGlowFramesFolder = RenaArtRoot + "/Spells/Fire/Infierno/Glow";
        private const string IcePillarFramesFolder = RenaArtRoot + "/Spells/Ice/IcePillar/Full";
        private const string MagicShieldFramesFolder = RenaArtRoot + "/MagicShield/Full/FX";
        private const string PhaseTwoIntroFramesFolder = RenaArtRoot + "/Rest/Full";
        private const string PhaseTwoInfernoPoseFramesFolder = RenaArtRoot + "/Attacks/CometDive/Impact/Glow/Pose2";
        private const string PhaseTwoSprintFramesFolder = RenaArtRoot + "/Sprint/Full/FX";
        private const string ThunderStrikeFramesFolder = RenaArtRoot + "/Spells/Thunder/ThunderStrike/Full/Yellow/NoGlow";
        private const string ThunderStrikeGlowFramesFolder = RenaArtRoot + "/Spells/Thunder/ThunderStrike/Full/Yellow/Glow";
        private const string PhaseTwoThunderStrikeFramesFolder = RenaArtRoot + "/Spells/Thunder/ThunderStrike/Full/Blue/NoGlow";
        private const string PhaseTwoThunderStrikeGlowFramesFolder = RenaArtRoot + "/Spells/Thunder/ThunderStrike/Full/Blue/Glow";
        private const string ThunderboltFramesFolder = RenaArtRoot + "/Spells/Thunder/Eletricity/Horizontal/NoGlow";
        private const string ThunderboltGlowFramesFolder = RenaArtRoot + "/Spells/Thunder/Eletricity/Horizontal";
        private const string OutputFolder = "Assets/_Project/Art/Animations/Enemies/Boss/Rena";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Enemies/Boss";
        private const string StageDataFolder = "Assets/_Project/ScriptableObjects/Stage";
        private const string EnemyDataFolder = "Assets/_Project/ScriptableObjects/Enemies";
        private const string SceneFolder = "Assets/_Project/Scenes/Dungeon";
        private const string IdleClipPath = OutputFolder + "/Rena_Idle.anim";
        private const string MoveClipPath = OutputFolder + "/Rena_Move.anim";
        private const string HurtClipPath = OutputFolder + "/Rena_Hurt.anim";
        private const string DeathClipPath = OutputFolder + "/Rena_Death.anim";
        private const string EntryClipPath = OutputFolder + "/Rena_Entry_CometDive.anim";
        private const string RestClipPath = OutputFolder + "/Rena_Rest.anim";
        private const string PhaseTwoInfernoPoseClipPath = OutputFolder + "/Rena_Inferno_Pose2.anim";
        private const string PhaseTwoSprintClipPath = OutputFolder + "/Rena_SprintFull.anim";
        private const string PhaseTwoSprintBreakClipPath = OutputFolder + "/Rena_SprintBreak.anim";
        private const string FastCastClipPath = OutputFolder + "/Rena_fastCast.anim";
        private const string HeavyCastClipPath = OutputFolder + "/Rena_HeavyCast.anim";
        private const string ThunderStrikeClipPath = OutputFolder + "/Rena_thunderStrike.anim";
        private const string ThunderboltClipPath = OutputFolder + "/Rena_thunderbolt.anim";
        private const string ControllerPath = OutputFolder + "/Rena.controller";
        private const string IntroDataPath = StageDataFolder + "/RenaBossIntroSequenceData.asset";
        private const string RenaBossBalanceDataPath = EnemyDataFolder + "/Rena_Boss_Balance.asset";
        private const string PrefabPath = PrefabFolder + "/RenaRoot.prefab";
        private const string BossTestScenePath = SceneFolder + "/2F_BossTest.unity";
        private const string BossHealthBarPrefabPath = "Assets/_Project/Prefabs/UI/BossHealthBar.prefab";
        private const int SpritePixelsPerUnit = 64;
        private const int DefaultFrameRate = 12;
        private const int EntryFrameRate = 15;
        private const int PhaseTwoInfernoPoseFrameRate = 18;
        private const int PhaseTwoSprintFrameRate = 14;
        private const int PhaseTwoSprintMoveFrameCount = 7;
        private const int ThunderboltCastFrameRate = 10;
        private const int InfernoCastedFrameCount = 9;
        private const int InfernoLoopFrameCount = 7;
        private const float RenaVisualScale = 2.2f;
        private const float RenaInitialHealth = 800f;
        private const float IntroStartDelaySeconds = 0.5f;
        private const string RequestFileName = "RenaBossPrototypeBuilder.request";
        private const string Install2FBossTestRequestFileName = "RenaBossInstall2FBossTest.request";
        private const string FixMagicShieldImportSettingsRequestFileName =
            "RenaBossFixMagicShieldImportSettings.request";

        private static double _nextRequestPollTime;

        static RenaBossPrototypeBuilder()
        {
            RunWhenRequested();
            EditorApplication.update -= PollRequestFiles;
            EditorApplication.update += PollRequestFiles;
        }

        [InitializeOnLoadMethod]
        private static void RunWhenRequested()
        {
            TryRunRequestedOperations();
        }

        private static void PollRequestFiles()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRequestPollTime)
            {
                return;
            }

            _nextRequestPollTime = now + 0.5d;
            TryRunRequestedOperations();
        }

        [MenuItem(RenaRootMenu)]
        public static void BuildAll()
        {
            try
            {
                EnsureFolder(OutputFolder);
                EnsureFolder(PrefabFolder);
                EnsureFolder(StageDataFolder);
                EnsureFolder(EnemyDataFolder);
                EnsureFolder(SceneFolder);

                List<Sprite> idleSprites = ImportSprites(IdleFramesFolder, recursive: false);
                List<Sprite> moveSprites = ImportSprites(MoveFramesFolder);
                List<Sprite> hurtSprites = ImportSprites(HurtFramesFolder);
                List<Sprite> deathSprites = ImportSprites(DeathFramesFolder);
                List<Sprite> entrySprites = ImportSprites(EntryFramesFolder);
                List<Sprite> fastCastSprites = ImportSprites(FastCastFramesFolder);
                List<Sprite> heavyCastSprites = ImportSprites(HeavyCastFramesFolder);
                List<Sprite> thunderStrikeCastSprites = ImportSprites(ThunderStrikeCastFramesFolder);
                List<Sprite> thunderboltCastSprites = ImportSprites(ThunderboltCastFramesFolder);
                List<Sprite> fireballCastedSprites = ImportSprites(FireballCastedFramesFolder);
                List<Sprite> fireballCastedGlowSprites = ImportSprites(FireballCastedGlowFramesFolder, recursive: false);
                List<Sprite> fireballSprites = ImportSprites(FireballFramesFolder);
                List<Sprite> fireballGlowSprites = ImportSprites(FireballGlowFramesFolder, recursive: false);
                List<Sprite> fireballHitSprites = ImportSprites(FireballHitFramesFolder);
                List<Sprite> fireballHitGlowSprites = ImportSprites(FireballHitGlowFramesFolder, recursive: false);
                List<Sprite> infernoSourceSprites = ImportSprites(InfernoFramesFolder);
                List<Sprite> infernoGlowSourceSprites = ImportSprites(InfernoGlowFramesFolder);
                List<Sprite> infernoCastedSprites = SliceFrames(infernoSourceSprites, 0, InfernoCastedFrameCount);
                List<Sprite> infernoSprites = SliceFrames(infernoSourceSprites, InfernoCastedFrameCount, InfernoLoopFrameCount);
                List<Sprite> infernoHitSprites = SliceFrames(
                    infernoSourceSprites,
                    InfernoCastedFrameCount + InfernoLoopFrameCount,
                    int.MaxValue);
                List<Sprite> infernoCastedGlowSprites = SliceFrames(infernoGlowSourceSprites, 0, InfernoCastedFrameCount);
                List<Sprite> infernoGlowSprites = SliceFrames(
                    infernoGlowSourceSprites,
                    InfernoCastedFrameCount,
                    InfernoLoopFrameCount);
                List<Sprite> infernoHitGlowSprites = SliceFrames(
                    infernoGlowSourceSprites,
                    InfernoCastedFrameCount + InfernoLoopFrameCount,
                    int.MaxValue);
                List<Sprite> icePillarSprites = ImportSprites(IcePillarFramesFolder, recursive: false);
                List<Sprite> magicShieldSprites = ImportSprites(MagicShieldFramesFolder, recursive: false);
                List<Sprite> phaseTwoIntroSprites = ImportSprites(PhaseTwoIntroFramesFolder);
                List<Sprite> phaseTwoInfernoPoseSprites =
                    ImportSprites(PhaseTwoInfernoPoseFramesFolder, recursive: false);
                List<Sprite> phaseTwoSprintSourceSprites = ImportSprites(PhaseTwoSprintFramesFolder, recursive: false);
                List<Sprite> phaseTwoSprintSprites =
                    SliceFrames(phaseTwoSprintSourceSprites, 0, PhaseTwoSprintMoveFrameCount);
                List<Sprite> phaseTwoSprintBreakSprites =
                    SliceFrames(phaseTwoSprintSourceSprites, PhaseTwoSprintMoveFrameCount, int.MaxValue);
                List<Sprite> thunderStrikeSprites = ImportSprites(ThunderStrikeFramesFolder, recursive: false);
                List<Sprite> thunderStrikeGlowSprites = ImportSprites(ThunderStrikeGlowFramesFolder, recursive: false);
                List<Sprite> phaseTwoThunderStrikeSprites =
                    ImportSprites(PhaseTwoThunderStrikeFramesFolder, recursive: false);
                List<Sprite> phaseTwoThunderStrikeGlowSprites =
                    ImportSprites(PhaseTwoThunderStrikeGlowFramesFolder, recursive: false);
                List<Sprite> thunderboltSprites = ImportSprites(ThunderboltFramesFolder);
                List<Sprite> thunderboltGlowSprites = ImportSprites(ThunderboltGlowFramesFolder, recursive: false);

                if (!ValidateFrames("Idle", idleSprites) ||
                    !ValidateFrames("Move", moveSprites) ||
                    !ValidateFrames("Hurt", hurtSprites) ||
                    !ValidateFrames("Death", deathSprites) ||
                    !ValidateFrames("Entry CometDive", entrySprites) ||
                    !ValidateFrames("FastCast", fastCastSprites) ||
                    !ValidateFrames("HeavyCast", heavyCastSprites) ||
                    !ValidateFrames("Thunder Strike Cast", thunderStrikeCastSprites) ||
                    !ValidateFrames("Thunderbolt Cast", thunderboltCastSprites) ||
                    !ValidateFrames("Fireball Casted", fireballCastedSprites) ||
                    !ValidateFrames("Fireball Casted Glow", fireballCastedGlowSprites) ||
                    !ValidateFrames("Fireball", fireballSprites) ||
                    !ValidateFrames("Fireball Glow", fireballGlowSprites) ||
                    !ValidateFrames("Fireball Hit", fireballHitSprites) ||
                    !ValidateFrames("Fireball Hit Glow", fireballHitGlowSprites) ||
                    !ValidateFrames("Inferno Casted", infernoCastedSprites) ||
                    !ValidateFrames("Inferno", infernoSprites) ||
                    !ValidateFrames("Inferno Hit", infernoHitSprites) ||
                    !ValidateFrames("Inferno Casted Glow", infernoCastedGlowSprites) ||
                    !ValidateFrames("Inferno Glow", infernoGlowSprites) ||
                    !ValidateFrames("Inferno Hit Glow", infernoHitGlowSprites) ||
                    !ValidateFrames("Ice Pillar", icePillarSprites) ||
                    !ValidateFrames("Magic Shield", magicShieldSprites) ||
                    !ValidateFrames("Phase 2 Rest", phaseTwoIntroSprites) ||
                    !ValidateFrames("Phase 2 Inferno Pose", phaseTwoInfernoPoseSprites) ||
                    !ValidateFrames("Phase 2 Sprint", phaseTwoSprintSprites) ||
                    !ValidateFrames("Phase 2 Sprint Break", phaseTwoSprintBreakSprites) ||
                    !ValidateFrames("Phase 1 Thunder Strike", thunderStrikeSprites) ||
                    !ValidateFrames("Phase 1 Thunder Strike Glow", thunderStrikeGlowSprites) ||
                    !ValidateFrames("Phase 2 Thunder Strike", phaseTwoThunderStrikeSprites) ||
                    !ValidateFrames("Phase 2 Thunder Strike Glow", phaseTwoThunderStrikeGlowSprites) ||
                    !ValidateFrames("Thunderbolt", thunderboltSprites) ||
                    !ValidateFrames("Thunderbolt Glow", thunderboltGlowSprites))
                {
                    return;
                }

                AnimationClip idleClip = CreateOrUpdateClip(IdleClipPath, idleSprites, true, DefaultFrameRate);
                AnimationClip moveClip = CreateOrUpdateClip(MoveClipPath, moveSprites, true, DefaultFrameRate);
                AnimationClip hurtClip = CreateOrUpdateClip(HurtClipPath, hurtSprites, false, DefaultFrameRate);
                AnimationClip deathClip = CreateOrUpdateClip(DeathClipPath, deathSprites, false, DefaultFrameRate);
                AnimationClip entryClip = CreateOrUpdateClip(EntryClipPath, entrySprites, false, EntryFrameRate);
                AnimationClip restClip = CreateOrUpdateClip(RestClipPath, phaseTwoIntroSprites, false, DefaultFrameRate);
                AnimationClip phaseTwoInfernoPoseClip =
                    CreateOrUpdateClip(
                        PhaseTwoInfernoPoseClipPath,
                        phaseTwoInfernoPoseSprites,
                        false,
                        PhaseTwoInfernoPoseFrameRate);
                AnimationClip phaseTwoSprintClip =
                    CreateOrUpdateClip(PhaseTwoSprintClipPath, phaseTwoSprintSprites, true, PhaseTwoSprintFrameRate);
                AnimationClip phaseTwoSprintBreakClip =
                    CreateOrUpdateClip(
                        PhaseTwoSprintBreakClipPath,
                        phaseTwoSprintBreakSprites,
                        false,
                        PhaseTwoSprintFrameRate);
                AnimationClip fastCastClip = CreateOrUpdateClip(FastCastClipPath, fastCastSprites, false, DefaultFrameRate);
                AnimationClip heavyCastClip = CreateOrUpdateClip(HeavyCastClipPath, heavyCastSprites, false, DefaultFrameRate);
                AnimationClip thunderStrikeClip =
                    CreateOrUpdateClip(ThunderStrikeClipPath, thunderStrikeCastSprites, false, DefaultFrameRate);
                AnimationClip thunderboltClip =
                    CreateOrUpdateClip(ThunderboltClipPath, thunderboltCastSprites, false, ThunderboltCastFrameRate);
                AnimatorController controller = CreateOrUpdateController(
                    idleClip,
                    moveClip,
                    hurtClip,
                    deathClip,
                    entryClip,
                    restClip,
                    phaseTwoInfernoPoseClip,
                    phaseTwoSprintClip,
                    phaseTwoSprintBreakClip,
                    fastCastClip,
                    heavyCastClip,
                    thunderStrikeClip,
                    thunderboltClip);
                BossIntroSequenceData introData = CreateOrUpdateIntroData(entrySprites.Count / (float)EntryFrameRate);
                RenaBossBalanceData balanceData = CreateOrUpdateRenaBossBalanceData();
                GameObject prefab = CreateOrUpdatePrefab(
                    controller,
                    introData,
                    balanceData,
                    idleSprites[0],
                    fireballCastedSprites,
                    fireballSprites,
                    fireballHitSprites,
                    fireballCastedGlowSprites,
                    fireballGlowSprites,
                    fireballHitGlowSprites,
                    infernoCastedSprites,
                    infernoSprites,
                    infernoHitSprites,
                    infernoCastedGlowSprites,
                    infernoGlowSprites,
                    infernoHitGlowSprites,
                    icePillarSprites,
                    magicShieldSprites,
                    phaseTwoIntroSprites,
                    thunderStrikeSprites,
                    thunderStrikeGlowSprites,
                    phaseTwoThunderStrikeSprites,
                    phaseTwoThunderStrikeGlowSprites,
                    thunderboltSprites,
                    thunderboltGlowSprites);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[RenaBossPrototypeBuilder] Built Rena boss prefab, animator and intro data.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        public static void BuildAllBatch()
        {
            BuildAll();
            EditorApplication.Exit(0);
        }

        [MenuItem(FixMagicShieldImportSettingsMenu)]
        public static void FixMagicShieldImportSettings()
        {
            List<Sprite> magicShieldSprites = ImportSprites(MagicShieldFramesFolder, recursive: false);
            if (!ValidateFrames("Magic Shield", magicShieldSprites))
            {
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RenaBossPrototypeBuilder] Fixed Rena magic shield import settings.");
        }

        public static void FixMagicShieldImportSettingsBatch()
        {
            FixMagicShieldImportSettings();
            EditorApplication.Exit(0);
        }

        public static void InstallRenaInto2FBossTestSceneBatch()
        {
            InstallRenaInto2FBossTestScene();
            EditorApplication.Exit(0);
        }

        private static string RequestFilePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", RequestFileName));

        private static string Install2FBossTestRequestFilePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", Install2FBossTestRequestFileName));

        private static string FixMagicShieldImportSettingsRequestFilePath =>
            Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Temp", FixMagicShieldImportSettingsRequestFileName));

        private static void TryRunRequestedOperations()
        {
            bool shouldBuild = false;
            bool shouldInstall2FBossTest = false;
            bool shouldFixMagicShieldImportSettings = false;

            if (File.Exists(RequestFilePath))
            {
                File.Delete(RequestFilePath);
                shouldBuild = true;
            }

            if (File.Exists(Install2FBossTestRequestFilePath))
            {
                File.Delete(Install2FBossTestRequestFilePath);
                shouldInstall2FBossTest = true;
            }

            if (File.Exists(FixMagicShieldImportSettingsRequestFilePath))
            {
                File.Delete(FixMagicShieldImportSettingsRequestFilePath);
                shouldFixMagicShieldImportSettings = true;
            }

            if (shouldBuild)
            {
                TryRunRequestedBuild();
            }

            if (shouldInstall2FBossTest)
            {
                TryRunRequestedInstall2FBossTest();
            }

            if (shouldFixMagicShieldImportSettings)
            {
                TryRunRequestedFixMagicShieldImportSettings();
            }
        }

        private static void TryRunRequestedBuild()
        {
            BuildAll();
        }

        private static void TryRunRequestedInstall2FBossTest()
        {
            InstallRenaInto2FBossTestScene();
        }

        private static void TryRunRequestedFixMagicShieldImportSettings()
        {
            FixMagicShieldImportSettings();
        }

        private static bool ValidateFrames(string label, IReadOnlyCollection<Sprite> sprites)
        {
            if (sprites.Count > 0)
            {
                return true;
            }

            Debug.LogError("[RenaBossPrototypeBuilder] " + label + " frames were not found.");
            return false;
        }

        private static List<Sprite> ImportSprites(string folderPath, bool recursive = true)
        {
            List<string> paths = new List<string>();
            if (recursive)
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                for (int i = 0; i < guids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        paths.Add(assetPath);
                    }
                }
            }
            else if (Directory.Exists(folderPath))
            {
                string[] files = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    paths.Add(ToAssetPath(files[i]));
                }
            }

            paths.Sort(CompareAssetPathsNaturally);

            List<Sprite> sprites = new List<Sprite>(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                ConfigureTextureAsSprite(paths[i]);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }

            return sprites;
        }

        private static string ToAssetPath(string path)
        {
            string normalizedPath = path.Replace('\\', '/');
            string projectRoot = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            if (normalizedPath.StartsWith(projectRoot, StringComparison.Ordinal))
            {
                return normalizedPath.Substring(projectRoot.Length);
            }

            return normalizedPath;
        }

        private static List<Sprite> SliceFrames(IReadOnlyList<Sprite> sprites, int startIndex, int count)
        {
            List<Sprite> sliced = new List<Sprite>();
            if (sprites == null || startIndex >= sprites.Count || count <= 0)
            {
                return sliced;
            }

            int start = Mathf.Max(0, startIndex);
            int end = count == int.MaxValue
                ? sprites.Count
                : Mathf.Min(sprites.Count, start + count);
            for (int i = start; i < end; i++)
            {
                sliced.Add(sprites[i]);
            }

            return sliced;
        }

        private static void ConfigureTextureAsSprite(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (Math.Abs(importer.spritePixelsPerUnit - SpritePixelsPerUnit) > 0.01f)
            {
                importer.spritePixelsPerUnit = SpritePixelsPerUnit;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip CreateOrUpdateClip(
            string clipPath,
            IReadOnlyList<Sprite> sprites,
            bool loop,
            int frameRate)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = frameRate;
            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] frames = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                frames[i] = new ObjectReferenceKeyframe
                {
                    time = i / (float)frameRate,
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController CreateOrUpdateController(
            AnimationClip idleClip,
            AnimationClip moveClip,
            AnimationClip hurtClip,
            AnimationClip deathClip,
            AnimationClip entryClip,
            AnimationClip restClip,
            AnimationClip phaseTwoInfernoPoseClip,
            AnimationClip phaseTwoSprintClip,
            AnimationClip phaseTwoSprintBreakClip,
            AnimationClip fastCastClip,
            AnimationClip heavyCastClip,
            AnimationClip thunderStrikeClip,
            AnimationClip thunderboltClip)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            AnimatorState idle = AddState(stateMachine, "Idle", idleClip, new Vector3(260f, 0f, 0f));
            AnimatorState move = AddState(stateMachine, "Move", moveClip, new Vector3(260f, 90f, 0f));
            AnimatorState entry = AddState(stateMachine, "Entry", entryClip, new Vector3(20f, 0f, 0f));
            AnimatorState hurt = AddState(stateMachine, "Hurt", hurtClip, new Vector3(260f, 180f, 0f));
            AnimatorState death = AddState(stateMachine, "Death", deathClip, new Vector3(260f, 270f, 0f));
            AnimatorState rest = AddState(stateMachine, "Rest", restClip, new Vector3(780f, 0f, 0f));
            AnimatorState phaseTwoInfernoPose =
                AddState(stateMachine, "Pose2", phaseTwoInfernoPoseClip, new Vector3(780f, 90f, 0f));
            AddState(stateMachine, "SprintMove", phaseTwoSprintClip, new Vector3(780f, 180f, 0f));
            AnimatorState phaseTwoSprintBreak =
                AddState(stateMachine, "SprintBreak", phaseTwoSprintBreakClip, new Vector3(780f, 270f, 0f));
            AnimatorState fastCast = AddState(stateMachine, "fastCast", fastCastClip, new Vector3(520f, 0f, 0f));
            AnimatorState heavyCast = AddState(stateMachine, "HeavyCast", heavyCastClip, new Vector3(520f, 90f, 0f));
            AnimatorState thunderStrike =
                AddState(stateMachine, "thunderStrike", thunderStrikeClip, new Vector3(520f, 180f, 0f));
            AnimatorState thunderbolt =
                AddState(stateMachine, "thunderbolt", thunderboltClip, new Vector3(520f, 270f, 0f));

            stateMachine.defaultState = idle;
            AddExitTransition(entry, idle);
            AddExitTransition(hurt, idle);
            AddExitTransition(rest, idle);
            AddExitTransition(phaseTwoInfernoPose, idle);
            AddExitTransition(phaseTwoSprintBreak, idle);
            AddExitTransition(fastCast, idle);
            AddExitTransition(heavyCast, idle);
            AddExitTransition(thunderStrike, idle);
            AddExitTransition(thunderbolt, idle);

            controller.AddParameter("Entry", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Rest", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Pose2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("fastCast", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyCast", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("thunderStrike", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("thunderbolt", AnimatorControllerParameterType.Trigger);

            AddAnyStateTriggerTransition(stateMachine, entry, "Entry");
            AddAnyStateTriggerTransition(stateMachine, hurt, "Hurt");
            AddAnyStateTriggerTransition(stateMachine, death, "Death");
            AddAnyStateTriggerTransition(stateMachine, rest, "Rest");
            AddAnyStateTriggerTransition(stateMachine, phaseTwoInfernoPose, "Pose2");
            AddAnyStateTriggerTransition(stateMachine, fastCast, "fastCast");
            AddAnyStateTriggerTransition(stateMachine, heavyCast, "HeavyCast");
            AddAnyStateTriggerTransition(stateMachine, thunderStrike, "thunderStrike");
            AddAnyStateTriggerTransition(stateMachine, thunderbolt, "thunderbolt");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState AddState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Motion motion,
            Vector3 position)
        {
            AnimatorState state = stateMachine.AddState(stateName, position);
            state.motion = motion;
            return state;
        }

        private static void AddExitTransition(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
        }

        private static void AddAnyStateTriggerTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState to,
            string triggerName)
        {
            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        }

        private static BossIntroSequenceData CreateOrUpdateIntroData(float entryDuration)
        {
            BossIntroSequenceData data = AssetDatabase.LoadAssetAtPath<BossIntroSequenceData>(IntroDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BossIntroSequenceData>();
                AssetDatabase.CreateAsset(data, IntroDataPath);
            }

            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("lockPlayersDuringIntro").boolValue = true;
            serialized.FindProperty("unlockPlayersOnComplete").boolValue = true;
            serialized.FindProperty("hideBossBeforeEntry").boolValue = true;
            serialized.FindProperty("delayBeforeEntry").floatValue = 0.2f;
            serialized.FindProperty("playEntryAnimation").boolValue = true;
            serialized.FindProperty("entryDuration").floatValue = entryDuration;
            serialized.FindProperty("delayBeforeDialogue").floatValue = 0f;
            serialized.FindProperty("delayAfterDialogue").floatValue = 0f;
            serialized.FindProperty("entryLandingSfx").objectReferenceValue = null;
            serialized.FindProperty("entryLandingSfxVolume").floatValue = 1f;
            serialized.FindProperty("entryLandingSfxDelay").floatValue = Mathf.Max(0f, entryDuration - 0.35f);
            serialized.FindProperty("playDialogue").boolValue = false;
            serialized.FindProperty("dialogueCueIds").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(data);
            return data;
        }

        private static RenaBossBalanceData CreateOrUpdateRenaBossBalanceData()
        {
            RenaBossBalanceData data = AssetDatabase.LoadAssetAtPath<RenaBossBalanceData>(RenaBossBalanceDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<RenaBossBalanceData>();
                AssetDatabase.CreateAsset(data, RenaBossBalanceDataPath);
            }

            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("_displayName").stringValue = "Rena";
            serialized.FindProperty("_maxHealth").floatValue = RenaInitialHealth;
            serialized.FindProperty("_moveSpeed").floatValue = 0f;
            serialized.FindProperty("_attackDamages").arraySize = 0;
            serialized.FindProperty("_expReward").intValue = 0;
            serialized.FindProperty("_dropWeight").floatValue = 1f;
            serialized.FindProperty("_phase2ThresholdNormalized").floatValue = 0.5f;
            serialized.FindProperty("_phase3ThresholdNormalized").floatValue = 0.1f;
            serialized.FindProperty("fireballDamage").floatValue = 10f;
            serialized.FindProperty("infernoDamage").floatValue = 16f;
            serialized.FindProperty("iceSweepDamage").floatValue = 18f;
            serialized.FindProperty("thunderStrikeDamage").floatValue = 13f;
            serialized.FindProperty("thunderboltDamage").floatValue = 6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(data);
            return data;
        }

        private static GameObject CreateOrUpdatePrefab(
            RuntimeAnimatorController controller,
            BossIntroSequenceData introData,
            RenaBossBalanceData balanceData,
            Sprite idleSprite,
            IReadOnlyList<Sprite> fireballCastedSprites,
            IReadOnlyList<Sprite> fireballSprites,
            IReadOnlyList<Sprite> fireballHitSprites,
            IReadOnlyList<Sprite> fireballCastedGlowSprites,
            IReadOnlyList<Sprite> fireballGlowSprites,
            IReadOnlyList<Sprite> fireballHitGlowSprites,
            IReadOnlyList<Sprite> infernoCastedSprites,
            IReadOnlyList<Sprite> infernoSprites,
            IReadOnlyList<Sprite> infernoHitSprites,
            IReadOnlyList<Sprite> infernoCastedGlowSprites,
            IReadOnlyList<Sprite> infernoGlowSprites,
            IReadOnlyList<Sprite> infernoHitGlowSprites,
            IReadOnlyList<Sprite> icePillarSprites,
            IReadOnlyList<Sprite> magicShieldSprites,
            IReadOnlyList<Sprite> phaseTwoIntroSprites,
            IReadOnlyList<Sprite> thunderStrikeSprites,
            IReadOnlyList<Sprite> thunderStrikeGlowSprites,
            IReadOnlyList<Sprite> phaseTwoThunderStrikeSprites,
            IReadOnlyList<Sprite> phaseTwoThunderStrikeGlowSprites,
            IReadOnlyList<Sprite> thunderboltSprites,
            IReadOnlyList<Sprite> thunderboltGlowSprites)
        {
            GameObject root = new GameObject("RenaRoot");
            RenaBossEncounterController encounterController = root.AddComponent<RenaBossEncounterController>();
            BossIntroSequenceController introController = root.AddComponent<BossIntroSequenceController>();
            RenaBossWanderController wanderController = root.AddComponent<RenaBossWanderController>();
            wanderController.enabled = false;
            RenaBossSpellCombatController spellCombatController = root.AddComponent<RenaBossSpellCombatController>();
            spellCombatController.enabled = false;
            Health health = root.AddComponent<Health>();
            CombatTargetable targetable = root.AddComponent<CombatTargetable>();
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            BoxCollider2D hurtbox = root.AddComponent<BoxCollider2D>();
            EnemyDeathAnimationLock deathAnimationLock = root.AddComponent<EnemyDeathAnimationLock>();

            root.tag = "Boss";

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * RenaVisualScale;

            SpriteRenderer spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = idleSprite;
            spriteRenderer.sortingOrder = 20;

            Animator animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            ConfigureHealth(health, animator, visual);
            ConfigurePhysics(body, hurtbox);
            deathAnimationLock.Configure(health, animator);

            SerializedObject introSerialized = new SerializedObject(introController);
            introSerialized.FindProperty("sequenceData").objectReferenceValue = introData;
            introSerialized.FindProperty("bossAnimator").objectReferenceValue = animator;
            introSerialized.FindProperty("entryTriggerName").stringValue = string.Empty;
            introSerialized.FindProperty("entryStateName").stringValue = "Entry";
            introSerialized.FindProperty("entryStateLayer").intValue = 0;
            introSerialized.FindProperty("entryMotionRoot").objectReferenceValue = visual.transform;
            introSerialized.FindProperty("moveEntryFromOffset").boolValue = true;
            introSerialized.FindProperty("entryStartLocalOffset").vector3Value = new Vector3(0f, 7f, 0f);
            introSerialized.FindProperty("entryMoveDuration").floatValue = 1.1f;
            SerializedProperty visibilityTargets = introSerialized.FindProperty("introVisibilityTargets");
            visibilityTargets.arraySize = 1;
            visibilityTargets.GetArrayElementAtIndex(0).objectReferenceValue = visual;
            introSerialized.FindProperty("dialoguePlayerOwner").objectReferenceValue = root;
            introSerialized.FindProperty("bossHealth").objectReferenceValue = health;
            introSerialized.FindProperty("makeBossInvulnerableDuringIntro").boolValue = true;
            introSerialized.FindProperty("bossTargetable").objectReferenceValue = targetable;
            introSerialized.FindProperty("makeBossUntargetableDuringIntro").boolValue = true;
            introSerialized.FindProperty("protectBossBeforeIntroStarts").boolValue = true;
            introSerialized.FindProperty("debugLogging").boolValue = true;
            introSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject wanderSerialized = new SerializedObject(wanderController);
            wanderSerialized.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            wanderSerialized.FindProperty("animator").objectReferenceValue = animator;
            wanderSerialized.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
            wanderSerialized.FindProperty("phaseController").objectReferenceValue = spellCombatController;
            wanderSerialized.FindProperty("idleStateName").stringValue = "Idle";
            wanderSerialized.FindProperty("moveStateName").stringValue = "Move";
            wanderSerialized.FindProperty("phaseTwoMoveStateName").stringValue = "SprintMove";
            wanderSerialized.FindProperty("phaseTwoStopStateName").stringValue = "SprintBreak";
            wanderSerialized.FindProperty("moveSpeed").floatValue = 3.3f;
            wanderSerialized.FindProperty("phaseTwoMoveSpeed").floatValue = 4.9f;
            wanderSerialized.FindProperty("phaseTwoStopMinDuration").floatValue = 0.5f;
            wanderSerialized.FindProperty("phaseTwoStopAnimationMinMoveDuration").floatValue = 3f;
            wanderSerialized.FindProperty("phaseTwoStopAnimationCooldown").floatValue = 3f;
            wanderSerialized.FindProperty("phaseTwoStopAnimationChance").floatValue = 0.35f;
            wanderSerialized.FindProperty("wanderRadius").floatValue = 5.25f;
            wanderSerialized.FindProperty("moveDurationRange").vector2Value = new Vector2(3.5f, 5.2f);
            wanderSerialized.FindProperty("pauseDurationRange").vector2Value = new Vector2(2.4f, 4.2f);
            wanderSerialized.FindProperty("moveStartChanceAfterPause").floatValue = 0.35f;
            wanderSerialized.FindProperty("moveSkippedPauseDurationRange").vector2Value = new Vector2(2.5f, 5f);
            wanderSerialized.FindProperty("pauseExtensionChance").floatValue = 0.2f;
            wanderSerialized.FindProperty("pauseExtensionDurationRange").vector2Value = new Vector2(1.2f, 2.4f);
            wanderSerialized.FindProperty("maxPauseExtensionsBeforeMove").intValue = 1;
            wanderSerialized.FindProperty("directionAttempts").intValue = 10;
            wanderSerialized.FindProperty("centerPullWhenOutsideRadius").floatValue = 0.75f;
            wanderSerialized.FindProperty("keepDistanceFromTargets").boolValue = true;
            wanderSerialized.FindProperty("targetLayerMask").intValue = ResolveLayerMask("Player", 1 << 10);
            wanderSerialized.FindProperty("targetDetectionRadius").floatValue = 16f;
            wanderSerialized.FindProperty("preferredTargetDistance").floatValue = 9f;
            wanderSerialized.FindProperty("targetDistanceTolerance").floatValue = 2.2f;
            wanderSerialized.FindProperty("targetSpacingRefreshInterval").floatValue = 0.6f;
            wanderSerialized.FindProperty("targetSpacingBlockedPauseDuration").floatValue = 0.45f;
            wanderSerialized.FindProperty("targetSpacingCooldown").floatValue = 10f;
            wanderSerialized.FindProperty("obstacleMask").intValue =
                ResolveLayerMask("Obstacles", "ObstaclesDoors", "DungeonWall", (1 << 8) | (1 << 11) | (1 << 24));
            wanderSerialized.FindProperty("obstacleProbeRadius").floatValue = 0.35f;
            wanderSerialized.FindProperty("obstacleProbeDistance").floatValue = 0.3f;
            wanderSerialized.FindProperty("debugLogging").boolValue = false;
            wanderSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject spellSerialized = new SerializedObject(spellCombatController);
            spellSerialized.FindProperty("animator").objectReferenceValue = animator;
            spellSerialized.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
            spellSerialized.FindProperty("health").objectReferenceValue = health;
            spellSerialized.FindProperty("wanderController").objectReferenceValue = wanderController;
            spellSerialized.FindProperty("projectileSpawnOrigin").objectReferenceValue = root.transform;
            spellSerialized.FindProperty("balanceData").objectReferenceValue = balanceData;
            spellSerialized.FindProperty("idleStateName").stringValue = "Idle";
            spellSerialized.FindProperty("fastCastStateName").stringValue = "fastCast";
            spellSerialized.FindProperty("heavyCastStateName").stringValue = "HeavyCast";
            spellSerialized.FindProperty("thunderStrikeStateName").stringValue = "thunderStrike";
            spellSerialized.FindProperty("thunderboltStateName").stringValue = "thunderbolt";
            spellSerialized.FindProperty("detectionRadius").floatValue = 16f;
            spellSerialized.FindProperty("targetLayerMask").intValue = ResolveLayerMask("Player", 1 << 10);
            spellSerialized.FindProperty("obstacleLayerMask").intValue =
                ResolveLayerMask("Obstacles", "ObstaclesDoors", "DungeonWall", (1 << 8) | (1 << 11) | (1 << 24));
            spellSerialized.FindProperty("attackRecoveryDuration").floatValue = 0.85f;
            spellSerialized.FindProperty("phaseTwoAttackRecoveryDuration").floatValue = 1.05f;
            spellSerialized.FindProperty("firstFireballDelay").floatValue = 1.2f;
            spellSerialized.FindProperty("fireballCooldown").floatValue = 2.85f;
            spellSerialized.FindProperty("fireballRange").floatValue = 14f;
            spellSerialized.FindProperty("fireballCastDuration").floatValue = 0.85f;
            spellSerialized.FindProperty("fireballReleaseTime").floatValue = 0.32f;
            spellSerialized.FindProperty("fireballDamage").floatValue = 10f;
            spellSerialized.FindProperty("fireballSpeed").floatValue = 7.5f;
            spellSerialized.FindProperty("fireballLifetime").floatValue = 3.2f;
            spellSerialized.FindProperty("fireballHitRadius").floatValue = 0.32f;
            spellSerialized.FindProperty("fireballVisualScale").floatValue = 1.3f;
            spellSerialized.FindProperty("fireballMaximumHits").intValue = 1;
            spellSerialized.FindProperty("fireballDestroyOnHit").boolValue = true;
            spellSerialized.FindProperty("fireballProjectileCount").intValue = 5;
            spellSerialized.FindProperty("fireballTotalSpreadAngle").floatValue = 40f;
            spellSerialized.FindProperty("fireballHomingEnabled").boolValue = true;
            spellSerialized.FindProperty("fireballHomingTurnRateDegrees").floatValue = 120f;
            spellSerialized.FindProperty("fireballHomingStartDelay").floatValue = 0.12f;
            spellSerialized.FindProperty("fireballHomingEndDistance").floatValue = 0.65f;
            spellSerialized.FindProperty("fireballHomingLaneSpacing").floatValue = 0.65f;
            spellSerialized.FindProperty("fireballHomingForwardSpacing").floatValue = 0.18f;
            SetSpriteArray(spellSerialized.FindProperty("fireballCastedFrames"), fireballCastedSprites);
            SetSpriteArray(spellSerialized.FindProperty("fireballFrames"), fireballSprites);
            SetSpriteArray(spellSerialized.FindProperty("fireballHitFrames"), fireballHitSprites);
            SetSpriteArray(spellSerialized.FindProperty("fireballCastedGlowFrames"), fireballCastedGlowSprites);
            SetSpriteArray(spellSerialized.FindProperty("fireballGlowFrames"), fireballGlowSprites);
            SetSpriteArray(spellSerialized.FindProperty("fireballHitGlowFrames"), fireballHitGlowSprites);
            spellSerialized.FindProperty("firstInfernoDelay").floatValue = 4f;
            spellSerialized.FindProperty("infernoCooldown").floatValue = 9f;
            spellSerialized.FindProperty("infernoRange").floatValue = 13f;
            spellSerialized.FindProperty("infernoCastDuration").floatValue = 1.65f;
            spellSerialized.FindProperty("infernoReleaseTime").floatValue = 0.95f;
            spellSerialized.FindProperty("infernoDamage").floatValue = 16f;
            spellSerialized.FindProperty("infernoSpeed").floatValue = 5.8f;
            spellSerialized.FindProperty("infernoLifetime").floatValue = 2.8f;
            spellSerialized.FindProperty("infernoHitRadius").floatValue = 0.48f;
            spellSerialized.FindProperty("infernoVisualScale").floatValue = 1.35f;
            spellSerialized.FindProperty("infernoMaximumHits").intValue = 4;
            spellSerialized.FindProperty("infernoDestroyOnHit").boolValue = false;
            spellSerialized.FindProperty("infernoProjectileCount").intValue = 3;
            spellSerialized.FindProperty("infernoTotalSpreadAngle").floatValue = 36f;
            SetSpriteArray(spellSerialized.FindProperty("infernoCastedFrames"), infernoCastedSprites);
            SetSpriteArray(spellSerialized.FindProperty("infernoFrames"), infernoSprites);
            SetSpriteArray(spellSerialized.FindProperty("infernoHitFrames"), infernoHitSprites);
            SetSpriteArray(spellSerialized.FindProperty("infernoCastedGlowFrames"), infernoCastedGlowSprites);
            SetSpriteArray(spellSerialized.FindProperty("infernoGlowFrames"), infernoGlowSprites);
            SetSpriteArray(spellSerialized.FindProperty("infernoHitGlowFrames"), infernoHitGlowSprites);
            spellSerialized.FindProperty("firstIceSweepDelay").floatValue = 6f;
            spellSerialized.FindProperty("iceSweepCooldown").floatValue = 21f;
            spellSerialized.FindProperty("iceSweepUseHealthThresholds").boolValue = true;
            spellSerialized.FindProperty("iceSweepFirstHealthThreshold").floatValue = 0.7f;
            spellSerialized.FindProperty("iceSweepSecondHealthThreshold").floatValue = 0.4f;
            spellSerialized.FindProperty("iceSweepThirdHealthThreshold").floatValue = 0.1f;
            spellSerialized.FindProperty("iceSweepFirstThresholdAttackCount").intValue = 4;
            spellSerialized.FindProperty("iceSweepSecondThresholdAttackCount").intValue = 6;
            spellSerialized.FindProperty("iceSweepThirdThresholdAttackCount").intValue = 6;
            spellSerialized.FindProperty("iceSweepRange").floatValue = 16f;
            spellSerialized.FindProperty("iceSweepCastDuration").floatValue = 1.55f;
            spellSerialized.FindProperty("iceSweepReleaseTime").floatValue = 1.55f;
            spellSerialized.FindProperty("iceSweepDamage").floatValue = 18f;
            spellSerialized.FindProperty("iceSweepCenterFollowsBoss").boolValue = true;
            spellSerialized.FindProperty("iceSweepAreaCenter").vector2Value = Vector2.zero;
            spellSerialized.FindProperty("iceSweepAreaSize").vector2Value = new Vector2(24f, 12f);
            spellSerialized.FindProperty("iceSweepAttackCount").intValue = 6;
            spellSerialized.FindProperty("phaseTwoIceSweepAttackCount").intValue = 6;
            spellSerialized.FindProperty("iceSweepColumnCount").intValue = 28;
            spellSerialized.FindProperty("iceSweepRowCount").intValue = 4;
            spellSerialized.FindProperty("iceSweepCellOverlap").floatValue = 0.25f;
            spellSerialized.FindProperty("iceSweepRowGap").floatValue = 0f;
            spellSerialized.FindProperty("iceSweepWarningDuration").floatValue = 0.85f;
            spellSerialized.FindProperty("iceSweepFinalThresholdThunderStrikeEnabled").boolValue = true;
            spellSerialized.FindProperty("iceSweepFinalThresholdThunderStrikeCount").intValue = 24;
            spellSerialized.FindProperty("iceSweepFinalThresholdThunderStrikeWarningDuration").floatValue = 0.45f;
            spellSerialized.FindProperty("iceSweepFinalThresholdThunderStrikeStunDuration").floatValue = 0.35f;
            spellSerialized.FindProperty("iceSweepPillarSpawnInterval").floatValue = 0.02f;
            spellSerialized.FindProperty("iceSweepWaveInterval").floatValue = 0.45f;
            spellSerialized.FindProperty("iceSweepCellDamageDelay").floatValue = 0.08f;
            spellSerialized.FindProperty("iceSweepSkipBlockedCells").boolValue = true;
            spellSerialized.FindProperty("iceSweepVisualScale").floatValue = 1.4f;
            spellSerialized.FindProperty("iceSweepPillarVisualJitter").vector2Value = new Vector2(0.22f, 0.32f);
            spellSerialized.FindProperty("iceSweepPillarVisualRows").intValue = 2;
            spellSerialized.FindProperty("iceSweepAnimationFrameRate").floatValue = 18f;
            spellSerialized.FindProperty("iceSweepSortingLayerName").stringValue = "Foreground";
            spellSerialized.FindProperty("iceSweepSortingOrder").intValue = 18;
            spellSerialized.FindProperty("iceSweepShieldRoot").objectReferenceValue = visual.transform;
            SetSpriteArray(spellSerialized.FindProperty("iceSweepShieldFrames"), magicShieldSprites);
            spellSerialized.FindProperty("iceSweepShieldLoopStartFrame").intValue = 10;
            spellSerialized.FindProperty("iceSweepShieldLoopEndFrame").intValue = 12;
            spellSerialized.FindProperty("iceSweepShieldFrameRate").floatValue = 18f;
            spellSerialized.FindProperty("iceSweepShieldVisualScale").floatValue = 1f;
            spellSerialized.FindProperty("iceSweepShieldSortingLayerName").stringValue = "Foreground";
            spellSerialized.FindProperty("iceSweepShieldSortingOrder").intValue = 22;
            SetSpriteArray(spellSerialized.FindProperty("iceSweepFrames"), icePillarSprites);
            spellSerialized.FindProperty("phaseTwoHealthThreshold").floatValue = 0.5f;
            spellSerialized.FindProperty("phaseTwoIntroStateName").stringValue = "Rest";
            spellSerialized.FindProperty("phaseTwoFallbackIntroStateName").stringValue = "Entry";
            spellSerialized.FindProperty("phaseTwoAnimatorIntroDuration").floatValue =
                phaseTwoIntroSprites.Count / (float)DefaultFrameRate;
            spellSerialized.FindProperty("makeInvulnerableDuringPhaseTransition").boolValue = true;
            spellSerialized.FindProperty("phaseTwoOpeningInfernoDelay").floatValue = 0.9f;
            spellSerialized.FindProperty("phaseTwoInfernoProjectileCount").intValue = 6;
            spellSerialized.FindProperty("phaseTwoInfernoTotalSpreadAngle").floatValue = 72f;
            spellSerialized.FindProperty("phaseTwoInfernoStateName").stringValue = "Pose2";
            spellSerialized.FindProperty("firstThunderStrikeDelay").floatValue = 5f;
            spellSerialized.FindProperty("thunderStrikeCooldown").floatValue = 13f;
            spellSerialized.FindProperty("thunderStrikeRange").floatValue = 14f;
            spellSerialized.FindProperty("thunderStrikeCastDuration").floatValue = 1.55f;
            spellSerialized.FindProperty("thunderStrikeReleaseTime").floatValue = 0.6f;
            spellSerialized.FindProperty("thunderStrikeDamage").floatValue = 13f;
            spellSerialized.FindProperty("thunderStrikeAreaCount").intValue = 10;
            spellSerialized.FindProperty("thunderStrikeSpawnInterval").floatValue = 0.12f;
            spellSerialized.FindProperty("thunderStrikeSpawnRadiusAroundTarget").floatValue = 3.5f;
            spellSerialized.FindProperty("thunderStrikeRadius").floatValue = 0.85f;
            spellSerialized.FindProperty("thunderStrikeWarningDuration").floatValue = 0.85f;
            spellSerialized.FindProperty("thunderStrikeVisualScale").floatValue = 1.2f;
            spellSerialized.FindProperty("thunderStrikeAnimationFrameRate").floatValue = 18f;
            SetSpriteArray(spellSerialized.FindProperty("thunderStrikeFrames"), thunderStrikeSprites);
            SetSpriteArray(spellSerialized.FindProperty("thunderStrikeGlowFrames"), thunderStrikeGlowSprites);
            SetSpriteArray(spellSerialized.FindProperty("phaseTwoThunderStrikeFrames"), phaseTwoThunderStrikeSprites);
            SetSpriteArray(
                spellSerialized.FindProperty("phaseTwoThunderStrikeGlowFrames"),
                phaseTwoThunderStrikeGlowSprites);
            spellSerialized.FindProperty("phaseTwoThunderStrikeHitStun").boolValue = true;
            spellSerialized.FindProperty("phaseTwoThunderStrikeHitStunDuration").floatValue = 0.35f;
            spellSerialized.FindProperty("firstThunderboltDelay").floatValue = 8f;
            spellSerialized.FindProperty("thunderboltCooldown").floatValue = 16f;
            spellSerialized.FindProperty("thunderboltRange").floatValue = 13f;
            spellSerialized.FindProperty("thunderboltCastDuration").floatValue = 3.4f;
            spellSerialized.FindProperty("thunderboltReleaseTime").floatValue = 0.45f;
            spellSerialized.FindProperty("thunderboltDamage").floatValue = 6f;
            spellSerialized.FindProperty("thunderboltBuildDuration").floatValue = 0.55f;
            spellSerialized.FindProperty("thunderboltDuration").floatValue = 2.4f;
            spellSerialized.FindProperty("phaseTwoThunderboltCastDuration").floatValue = 8.2f;
            spellSerialized.FindProperty("phaseTwoThunderboltDuration").floatValue = 7.2f;
            spellSerialized.FindProperty("thunderboltLength").floatValue = 15f;
            spellSerialized.FindProperty("thunderboltWidth").floatValue = 1.05f;
            spellSerialized.FindProperty("thunderboltRotationDegrees").floatValue = 360f;
            spellSerialized.FindProperty("phaseTwoThunderboltSpawnOppositeBeam").boolValue = true;
            spellSerialized.FindProperty("phaseTwoThunderboltRotationDegrees").floatValue = 1080f;
            spellSerialized.FindProperty("thunderboltDamageInterval").floatValue = 0.35f;
            spellSerialized.FindProperty("thunderboltAnimationFrameRate").floatValue = 18f;
            SetSpriteArray(spellSerialized.FindProperty("thunderboltFrames"), thunderboltSprites);
            SetSpriteArray(spellSerialized.FindProperty("thunderboltGlowFrames"), thunderboltGlowSprites);
            spellSerialized.FindProperty("projectileSortingLayerName").stringValue = "Foreground";
            spellSerialized.FindProperty("projectileSortingOrder").intValue = 24;
            spellSerialized.FindProperty("projectileAnimationFrameRate").floatValue = 12f;
            spellSerialized.FindProperty("projectileGlowScaleMultiplier").floatValue = 1f;
            spellSerialized.FindProperty("projectileGlowAlpha").floatValue = 0.75f;
            spellSerialized.FindProperty("targetInvincibilityDuration").floatValue = 0.35f;
            spellSerialized.FindProperty("projectileSpawnOffset").vector2Value = new Vector2(0.75f, 0.25f);
            spellSerialized.FindProperty("debugLogging").boolValue = false;
            spellSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject encounterSerialized = new SerializedObject(encounterController);
            encounterSerialized.FindProperty("health").objectReferenceValue = health;
            encounterSerialized.FindProperty("animator").objectReferenceValue = animator;
            SerializedProperty combatBehaviours = encounterSerialized.FindProperty("combatBehaviours");
            combatBehaviours.arraySize = 2;
            combatBehaviours.GetArrayElementAtIndex(0).objectReferenceValue = wanderController;
            combatBehaviours.GetArrayElementAtIndex(1).objectReferenceValue = spellCombatController;
            encounterSerialized.FindProperty("combatObjects").arraySize = 0;
            encounterSerialized.FindProperty("disableCombatOnAwake").boolValue = true;
            encounterSerialized.FindProperty("deathStateName").stringValue = "Death";
            encounterSerialized.FindProperty("debugLogging").boolValue = true;
            encounterSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ConfigureHealth(Health health, Animator animator, GameObject model)
        {
            if (health == null)
            {
                return;
            }

            health.Model = model;
            health.CurrentHealth = RenaInitialHealth;
            health.InitialHealth = RenaInitialHealth;
            health.MaximumHealth = RenaInitialHealth;
            health.ResetHealthOnEnable = true;
            health.Invulnerable = false;
            health.ImmuneToDamage = false;
            health.DestroyOnDeath = false;
            health.DelayBeforeDestruction = 0f;
            health.DisableControllerOnDeath = false;
            health.DisableModelOnDeath = false;
            health.DisableCollisionsOnDeath = true;
            health.DisableChildCollisionsOnDeath = false;
            health.TargetAnimator = animator;
            health.DisableAnimatorLogs = true;
        }

        private static void ConfigurePhysics(Rigidbody2D body, BoxCollider2D hurtbox)
        {
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            if (hurtbox != null)
            {
                hurtbox.isTrigger = true;
                hurtbox.offset = new Vector2(0f, -0.1f);
                hurtbox.size = new Vector2(1.45f, 2.15f);
            }
        }

        private static int ResolveLayerMask(string layerName, int fallback)
        {
            int mask = LayerMask.GetMask(layerName);
            return mask != 0 ? mask : fallback;
        }

        private static int ResolveLayerMask(string firstLayerName, string secondLayerName, string thirdLayerName, int fallback)
        {
            int mask = LayerMask.GetMask(firstLayerName, secondLayerName, thirdLayerName);
            return mask != 0 ? mask : fallback;
        }

        private static void SetSpriteArray(SerializedProperty property, IReadOnlyList<Sprite> sprites)
        {
            if (property == null)
            {
                return;
            }

            int count = sprites != null ? sprites.Count : 0;
            property.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
        }

        [MenuItem(Install2FBossTestMenu)]
        public static void InstallRenaInto2FBossTestScene()
        {
            try
            {
                GameObject renaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (renaPrefab == null)
                {
                    BuildAll();
                    renaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                }

                if (renaPrefab == null)
                {
                    Debug.LogError("[RenaBossPrototypeBuilder] RenaRoot prefab was not found or could not be created.");
                    return;
                }

                if (!File.Exists(ProjectRelativeToAbsolutePath(BossTestScenePath)))
                {
                    Debug.LogError("[RenaBossPrototypeBuilder] 2F boss test scene was not found: " + BossTestScenePath);
                    return;
                }

                Scene scene = EditorSceneManager.OpenScene(BossTestScenePath, OpenSceneMode.Single);
                RemoveExistingRenaTestObjects();

                Vector3 spawnPosition = ResolveBossSpawnPosition();
                DisableExistingBossIntros();
                DisableBossAutoStarters();

                GameObject rena = PrefabUtility.InstantiatePrefab(renaPrefab, scene) as GameObject;
                if (rena != null)
                {
                    rena.name = "RenaRoot_Test";
                    rena.transform.position = spawnPosition;
                }

                GameObject player = FindFirstPlayerObject();
                CreatePrototypeController(rena, player, "2F_BossTest");
                InstallBossHealthBar(rena, scene);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[RenaBossPrototypeBuilder] Installed Rena into 2F boss test scene.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        private static void InstallBossHealthBar(GameObject rena, Scene scene)
        {
            RemoveExistingBossHealthBars();

            GameObject healthBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossHealthBarPrefabPath);
            if (healthBarPrefab == null)
            {
                Debug.LogWarning("[RenaBossPrototypeBuilder] Boss health bar prefab was not found: " + BossHealthBarPrefabPath);
                return;
            }

            GameObject healthBar = PrefabUtility.InstantiatePrefab(healthBarPrefab) as GameObject;
            if (healthBar == null)
            {
                Debug.LogWarning("[RenaBossPrototypeBuilder] Failed to instantiate boss health bar prefab.");
                return;
            }

            if (scene.IsValid() && healthBar.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(healthBar, scene);
            }

            healthBar.name = "BossHealthBar_Rena";
            RectTransform rootRect = healthBar.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.pivot = Vector2.zero;
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = Vector2.zero;
                rootRect.localScale = Vector3.one;
                rootRect.localPosition = Vector3.zero;
                rootRect.localRotation = Quaternion.identity;
                EditorUtility.SetDirty(rootRect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rootRect);
            }

            Canvas canvas = healthBar.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                canvas.worldCamera = null;
                EditorUtility.SetDirty(canvas);
                PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
            }

            BossHealthBarView view = healthBar.GetComponentInChildren<BossHealthBarView>(includeInactive: true);
            Health health = rena != null ? rena.GetComponent<Health>() : null;
            BossIntroSequenceController introController =
                rena != null ? rena.GetComponent<BossIntroSequenceController>() : null;
            if (view == null)
            {
                Debug.LogWarning("[RenaBossPrototypeBuilder] BossHealthBarView was not found on instantiated prefab.");
                return;
            }

            SerializedObject serializedView = new SerializedObject(view);
            serializedView.FindProperty("targetHealth").objectReferenceValue = health;
            serializedView.FindProperty("introSequenceController").objectReferenceValue = introController;
            serializedView.FindProperty("autoResolveBerthaBoss").boolValue = true;
            serializedView.FindProperty("displayMode").enumValueIndex = (int)BossHealthBarDisplayMode.OnIntroStarted;
            serializedView.FindProperty("hideOnAwake").boolValue = true;
            serializedView.FindProperty("showWhenDamaged").boolValue = true;
            serializedView.FindProperty("hideOnDeath").boolValue = true;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(healthBar);
            EditorUtility.SetDirty(view);
            Debug.Log("[RenaBossPrototypeBuilder] Installed Rena boss health bar.", healthBar);
        }

        private static void RemoveExistingBossHealthBars()
        {
            BossHealthBarView[] healthBars =
                UnityEngine.Object.FindObjectsByType<BossHealthBarView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < healthBars.Length; i++)
            {
                BossHealthBarView healthBar = healthBars[i];
                if (healthBar == null)
                {
                    continue;
                }

                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(healthBar.gameObject);
                UnityEngine.Object.DestroyImmediate(root != null ? root : healthBar.gameObject);
            }
        }

        private static void RemoveExistingRenaTestObjects()
        {
            RenaBossPrototypeSceneController[] prototypeControllers =
                UnityEngine.Object.FindObjectsByType<RenaBossPrototypeSceneController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < prototypeControllers.Length; i++)
            {
                RenaBossPrototypeSceneController controller = prototypeControllers[i];
                if (controller != null)
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
            }

            RenaBossEncounterController[] existingRenas =
                UnityEngine.Object.FindObjectsByType<RenaBossEncounterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < existingRenas.Length; i++)
            {
                RenaBossEncounterController existingRena = existingRenas[i];
                if (existingRena != null)
                {
                    UnityEngine.Object.DestroyImmediate(existingRena.gameObject);
                }
            }
        }

        private static Vector3 ResolveBossSpawnPosition()
        {
            BossIntroSequenceController[] introControllers =
                UnityEngine.Object.FindObjectsByType<BossIntroSequenceController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < introControllers.Length; i++)
            {
                BossIntroSequenceController introController = introControllers[i];
                if (introController != null && introController.GetComponent<RenaBossEncounterController>() == null)
                {
                    return introController.transform.position;
                }
            }

            GameObject spawnPoint = GameObject.Find("SpawnPoint");
            if (spawnPoint != null)
            {
                return spawnPoint.transform.position;
            }

            return new Vector3(2f, 0f, 0f);
        }

        private static void DisableExistingBossIntros()
        {
            BossIntroSequenceController[] introControllers =
                UnityEngine.Object.FindObjectsByType<BossIntroSequenceController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < introControllers.Length; i++)
            {
                BossIntroSequenceController introController = introControllers[i];
                if (introController == null || introController.GetComponent<RenaBossEncounterController>() != null)
                {
                    continue;
                }

                introController.gameObject.SetActive(false);
                EditorUtility.SetDirty(introController.gameObject);
            }
        }

        private static void DisableBossAutoStarters()
        {
            BossSceneAutoStarter[] autoStarters =
                UnityEngine.Object.FindObjectsByType<BossSceneAutoStarter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < autoStarters.Length; i++)
            {
                BossSceneAutoStarter autoStarter = autoStarters[i];
                if (autoStarter != null)
                {
                    autoStarter.enabled = false;
                    EditorUtility.SetDirty(autoStarter);
                }
            }
        }

        private static GameObject FindFirstPlayerObject()
        {
            Character[] characters =
                UnityEngine.Object.FindObjectsByType<Character>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character != null && character.CharacterType == Character.CharacterTypes.Player)
                {
                    return character.gameObject;
                }
            }

            return GameObject.FindGameObjectWithTag("Player");
        }

        private static void CreatePrototypeController(GameObject rena, GameObject player, string bossSceneName = "2F_BossTest")
        {
            GameObject controllerObject = new GameObject("RenaBossPrototypeSceneController");
            RenaBossPrototypeSceneController controller = controllerObject.AddComponent<RenaBossPrototypeSceneController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("introController").objectReferenceValue =
                rena != null ? rena.GetComponent<BossIntroSequenceController>() : null;
            serialized.FindProperty("encounterController").objectReferenceValue =
                rena != null ? rena.GetComponent<RenaBossEncounterController>() : null;
            SerializedProperty players = serialized.FindProperty("players");
            Character playerCharacter = player != null ? player.GetComponent<Character>() : null;
            players.arraySize = playerCharacter != null ? 1 : 0;
            if (playerCharacter != null)
            {
                players.GetArrayElementAtIndex(0).objectReferenceValue = playerCharacter;
            }
            serialized.FindProperty("introDelaySeconds").floatValue = IntroStartDelaySeconds;
            serialized.FindProperty("bossRoomId").stringValue = "2F_Boss";
            serialized.FindProperty("bossEntryPointId").stringValue = "Rena";
            serialized.FindProperty("bossSceneName").stringValue = bossSceneName;
            serialized.FindProperty("autoStartIntro").boolValue = true;
            serialized.FindProperty("debugLogging").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string ProjectRelativeToAbsolutePath(string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectRelativePath));
        }

        private static void EnsureFolder(string folderPath)
        {
            string normalized = folderPath.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new ArgumentException("Folder must be under Assets: " + folderPath);
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static int CompareAssetPathsNaturally(string leftPath, string rightPath)
        {
            string left = Path.GetFileNameWithoutExtension(leftPath);
            string right = Path.GetFileNameWithoutExtension(rightPath);
            return CompareNaturally(left, right);
        }

        private static int CompareNaturally(string left, string right)
        {
            int leftIndex = 0;
            int rightIndex = 0;

            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                char leftChar = left[leftIndex];
                char rightChar = right[rightIndex];

                if (char.IsDigit(leftChar) && char.IsDigit(rightChar))
                {
                    long leftNumber = ReadNumber(left, ref leftIndex);
                    long rightNumber = ReadNumber(right, ref rightIndex);
                    int numberCompare = leftNumber.CompareTo(rightNumber);
                    if (numberCompare != 0)
                    {
                        return numberCompare;
                    }
                    continue;
                }

                int charCompare = char.ToUpperInvariant(leftChar).CompareTo(char.ToUpperInvariant(rightChar));
                if (charCompare != 0)
                {
                    return charCompare;
                }

                leftIndex++;
                rightIndex++;
            }

            return left.Length.CompareTo(right.Length);
        }

        private static long ReadNumber(string value, ref int index)
        {
            long number = 0;
            while (index < value.Length && char.IsDigit(value[index]))
            {
                number = number * 10 + (value[index] - '0');
                index++;
            }
            return number;
        }
    }
}
