using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LostMemory.Combat;
using LostMemory.Combat.Telegraph;
using LostMemory.Enemies;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LostMemory.Editor
{
    [InitializeOnLoad]
    public static class ChobombAnimationInstaller
    {
        private const string SourceTexturePath = "Assets/_Project/Art/Enemies/chobomb/chobomb ALL/chobomb1.png";
        private const string OutputFolder = "Assets/_Project/Art/Animations/Enemies/Chobomb";
        private const string IdleFrontClipPath = OutputFolder + "/Chobomb_Idle_Front.anim";
        private const string IdleClipPath = OutputFolder + "/Chobomb_Idle.anim";
        private const string IdleBackClipPath = OutputFolder + "/Chobomb_Idle_Back.anim";
        private const string WalkFrontClipPath = OutputFolder + "/Chobomb_Walk_Front.anim";
        private const string WalkClipPath = OutputFolder + "/Chobomb_Walk.anim";
        private const string WalkBackClipPath = OutputFolder + "/Chobomb_Walk_Back.anim";
        private const string AttackFrontClipPath = OutputFolder + "/Chobomb_Attack_Front.anim";
        private const string AttackClipPath = OutputFolder + "/Chobomb_Attack.anim";
        private const string AttackBackClipPath = OutputFolder + "/Chobomb_Attack_Back.anim";
        private const string HurtFrontClipPath = OutputFolder + "/Chobomb_Hurt_Front.anim";
        private const string HurtClipPath = OutputFolder + "/Chobomb_Hurt.anim";
        private const string HurtBackClipPath = OutputFolder + "/Chobomb_Hurt_Back.anim";
        private const string DeathClipPath = OutputFolder + "/Chobomb_Death.anim";
        private const string ControllerPath = OutputFolder + "/Chobomb.controller";
        private const string BaseEnemyPrefabPath = "Assets/_Project/Prefabs/Enemies/Orc_CL037.prefab";
        private const string ChobombPrefabPath = "Assets/_Project/Prefabs/Enemies/Chobomb_CL212.prefab";
        private const string EnemyDataPath = "Assets/_Project/ScriptableObjects/Enemies/EnemyData_Chobomb.asset";
        private const string EnemyCatalogPath = "Assets/_Project/ScriptableObjects/Enemies/EnemyCatalog_Default.asset";
        private const string ChobombCatalogId = "enemy_chobomb";
        private const string RequestFileName = "ChobombAnimationInstaller.request";
        private const int CellSize = 32;
        private const int FrameRate = 10;
        private const int AttackFrameRate = 12;
        private const int IdleRowStart = 0;
        private const int WalkRowStart = 3;
        private const int HurtRowStart = 6;
        private const int SelfDestructWarningRowStart = 9;
        private const int ExplosionRow = 12;
        private const int FrontDirectionOffset = 0;
        private const int SideDirectionOffset = 1;
        private const int BackDirectionOffset = 2;
        private const int IdleFrameCount = 4;
        private const int WalkFrameCount = 6;
        private const int HurtFrameCount = 4;
        private const int SelfDestructWarningFrameCount = 7;
        private const int ExplosionFrameCount = 6;
        private const float SelfDestructArmingRadius = 1.35f;
        private const float SelfDestructWarningDuration = SelfDestructWarningFrameCount / (float)AttackFrameRate;
        private const float SelfDestructExplosionRadius = 1.8f;
        private const float SelfDestructDamage = 20f;
        private const float SelfDestructKnockbackForce = 500f;
        private const float SelfDestructInvincibilityDuration = 0.45f;
        private const float SelfDestructDestroyDelay = 0.75f;
        private const int TargetLayerMaskBits = 1 << 10;

        static ChobombAnimationInstaller()
        {
            RunWhenRequested();
        }

        [InitializeOnLoadMethod]
        private static void RunWhenRequested()
        {
            if (!File.Exists(RequestFilePath))
            {
                return;
            }

            EditorApplication.delayCall += TryRunRequestedSetup;
        }

        [MenuItem("Lost Memory/Enemies/Build Chobomb Animation Set")]
        public static void BuildMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Build Chobomb Animation Set",
                "Chobomb sprite slicing, animation clips, animator controller, prefab, EnemyData, and EnemyCatalog entry were created.",
                "OK");
        }

        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        private static string RequestFilePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", RequestFileName));

        private static void TryRunRequestedSetup()
        {
            if (!File.Exists(RequestFilePath))
            {
                return;
            }

            File.Delete(RequestFilePath);
            Build();
        }

        private static void Build()
        {
            EnsureFolder(OutputFolder);
            EnsureFolder("Assets/_Project/Prefabs/Enemies");
            EnsureFolder("Assets/_Project/ScriptableObjects/Enemies");
            ConfigureSourceTexture();
            Dictionary<string, Sprite> spritesByName = LoadSlicedSprites();

            List<Sprite> idleFrontFrames = PickRowFrames(
                spritesByName,
                row: IdleRowStart + FrontDirectionOffset,
                startColumn: 0,
                count: IdleFrameCount);
            List<Sprite> idleSideFrames = PickRowFrames(
                spritesByName,
                row: IdleRowStart + SideDirectionOffset,
                startColumn: 0,
                count: IdleFrameCount);
            List<Sprite> idleBackFrames = PickRowFrames(
                spritesByName,
                row: IdleRowStart + BackDirectionOffset,
                startColumn: 0,
                count: IdleFrameCount);

            List<Sprite> walkFrontFrames = PickRowFrames(
                spritesByName,
                row: WalkRowStart + FrontDirectionOffset,
                startColumn: 0,
                count: WalkFrameCount);
            List<Sprite> walkSideFrames = PickRowFrames(
                spritesByName,
                row: WalkRowStart + SideDirectionOffset,
                startColumn: 0,
                count: WalkFrameCount);
            List<Sprite> walkBackFrames = PickRowFrames(
                spritesByName,
                row: WalkRowStart + BackDirectionOffset,
                startColumn: 0,
                count: WalkFrameCount);

            List<Sprite> hurtFrontFrames = PickRowFrames(
                spritesByName,
                row: HurtRowStart + FrontDirectionOffset,
                startColumn: 0,
                count: HurtFrameCount);
            List<Sprite> hurtSideFrames = PickRowFrames(
                spritesByName,
                row: HurtRowStart + SideDirectionOffset,
                startColumn: 0,
                count: HurtFrameCount);
            List<Sprite> hurtBackFrames = PickRowFrames(
                spritesByName,
                row: HurtRowStart + BackDirectionOffset,
                startColumn: 0,
                count: HurtFrameCount);

            List<Sprite> warningFrontFrames = PickRowFrames(
                spritesByName,
                row: SelfDestructWarningRowStart + FrontDirectionOffset,
                startColumn: 0,
                count: SelfDestructWarningFrameCount);
            List<Sprite> warningSideFrames = PickRowFrames(
                spritesByName,
                row: SelfDestructWarningRowStart + SideDirectionOffset,
                startColumn: 0,
                count: SelfDestructWarningFrameCount);
            List<Sprite> warningBackFrames = PickRowFrames(
                spritesByName,
                row: SelfDestructWarningRowStart + BackDirectionOffset,
                startColumn: 0,
                count: SelfDestructWarningFrameCount);

            List<Sprite> explosionFrames = PickRowFrames(
                spritesByName,
                row: ExplosionRow,
                startColumn: 0,
                count: ExplosionFrameCount);

            AnimationClip idleFrontClip = CreateOrUpdateClip(
                IdleFrontClipPath,
                idleFrontFrames,
                loop: true,
                FrameRate);
            AnimationClip idleSideClip = CreateOrUpdateClip(
                IdleClipPath,
                idleSideFrames,
                loop: true,
                FrameRate);
            AnimationClip idleBackClip = CreateOrUpdateClip(
                IdleBackClipPath,
                idleBackFrames,
                loop: true,
                FrameRate);

            AnimationClip walkFrontClip = CreateOrUpdateClip(
                WalkFrontClipPath,
                walkFrontFrames,
                loop: true,
                FrameRate);
            AnimationClip walkSideClip = CreateOrUpdateClip(
                WalkClipPath,
                walkSideFrames,
                loop: true,
                FrameRate);
            AnimationClip walkBackClip = CreateOrUpdateClip(
                WalkBackClipPath,
                walkBackFrames,
                loop: true,
                FrameRate);

            AnimationClip attackFrontClip = CreateOrUpdateClip(
                AttackFrontClipPath,
                warningFrontFrames,
                loop: false,
                AttackFrameRate);
            AnimationClip attackSideClip = CreateOrUpdateClip(
                AttackClipPath,
                warningSideFrames,
                loop: false,
                AttackFrameRate);
            AnimationClip attackBackClip = CreateOrUpdateClip(
                AttackBackClipPath,
                warningBackFrames,
                loop: false,
                AttackFrameRate);

            AnimationClip hurtFrontClip = CreateOrUpdateClip(
                HurtFrontClipPath,
                hurtFrontFrames,
                loop: false,
                AttackFrameRate);
            AnimationClip hurtSideClip = CreateOrUpdateClip(
                HurtClipPath,
                hurtSideFrames,
                loop: false,
                AttackFrameRate);
            AnimationClip hurtBackClip = CreateOrUpdateClip(
                HurtBackClipPath,
                hurtBackFrames,
                loop: false,
                AttackFrameRate);

            AnimationClip deathClip = CreateOrUpdateClip(
                DeathClipPath,
                explosionFrames,
                loop: false,
                AttackFrameRate);

            AnimatorController controller = CreateOrUpdateController(
                idleFrontClip,
                idleSideClip,
                idleBackClip,
                walkFrontClip,
                walkSideClip,
                walkBackClip,
                attackFrontClip,
                attackSideClip,
                attackBackClip,
                hurtFrontClip,
                hurtSideClip,
                hurtBackClip,
                deathClip);
            CreateOrUpdatePrefab(controller, idleSideFrames[0]);
            EnemyData enemyData = CreateOrUpdateEnemyData();
            CreateOrUpdateEnemyCatalogEntry(enemyData);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ChobombAnimationInstaller] Built Chobomb directional sprite slicing, animation clips, animator controller, prefab, EnemyData, and EnemyCatalog mapping.");
        }

        private static void ConfigureSourceTexture()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceTexturePath);
            if (texture == null)
            {
                throw new InvalidOperationException($"Chobomb source texture was not found: {SourceTexturePath}");
            }

            TextureImporter importer = AssetImporter.GetAtPath(SourceTexturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"TextureImporter was not found: {SourceTexturePath}");
            }

            int columns = texture.width / CellSize;
            int rows = texture.height / CellSize;
            if (columns <= 0 || rows <= 0)
            {
                throw new InvalidOperationException($"Invalid Chobomb sheet size: {texture.width}x{texture.height}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = CellSize;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var metadata = new List<SpriteMetaData>(columns * rows);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    metadata.Add(new SpriteMetaData
                    {
                        name = SpriteName(row, column),
                        rect = new Rect(column * CellSize, texture.height - ((row + 1) * CellSize), CellSize, CellSize),
                        pivot = new Vector2(0.5f, 0.5f),
                        alignment = (int)SpriteAlignment.Center
                    });
                }
            }

            importer.spritesheet = metadata.ToArray();
            importer.SaveAndReimport();
        }

        private static Dictionary<string, Sprite> LoadSlicedSprites()
        {
            AssetDatabase.ImportAsset(SourceTexturePath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(SourceTexturePath)
                .OfType<Sprite>()
                .ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
        }

        private static List<Sprite> PickRowFrames(
            IReadOnlyDictionary<string, Sprite> spritesByName,
            int row,
            int startColumn,
            int count)
        {
            var frames = new List<Sprite>(count);
            for (int i = 0; i < count; i++)
            {
                string spriteName = SpriteName(row, startColumn + i);
                if (!spritesByName.TryGetValue(spriteName, out Sprite sprite))
                {
                    throw new InvalidOperationException($"Chobomb sprite frame was not found: {spriteName}");
                }

                frames.Add(sprite);
            }

            return frames;
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

            clip.frameRate = Mathf.Max(1, frameRate);
            SetClipLooping(clip, loop);

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };

            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController CreateOrUpdateController(
            AnimationClip idleFrontClip,
            AnimationClip idleSideClip,
            AnimationClip idleBackClip,
            AnimationClip walkFrontClip,
            AnimationClip walkSideClip,
            AnimationClip walkBackClip,
            AnimationClip attackFrontClip,
            AnimationClip attackSideClip,
            AnimationClip attackBackClip,
            AnimationClip hurtFrontClip,
            AnimationClip hurtSideClip,
            AnimationClip hurtBackClip,
            AnimationClip deathClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            EnsureParameter(controller, "Walking", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Damage", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "FacingDirection2D", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "HorizontalDirection", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "VerticalDirection", AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            ClearStateMachine(stateMachine);
            ClearControllerBlendTrees();

            BlendTree idleTree = CreateDirectionalBlendTree(controller, "Chobomb_Idle_Directional", idleFrontClip, idleSideClip, idleBackClip);
            BlendTree walkTree = CreateDirectionalBlendTree(controller, "Chobomb_Walk_Directional", walkFrontClip, walkSideClip, walkBackClip);
            BlendTree attackTree = CreateDirectionalBlendTree(controller, "Chobomb_Attack_Directional", attackFrontClip, attackSideClip, attackBackClip);
            BlendTree hurtTree = CreateDirectionalBlendTree(controller, "Chobomb_Hurt_Directional", hurtFrontClip, hurtSideClip, hurtBackClip);

            AnimatorState idle = AddState(stateMachine, "Chobomb_Idle", idleTree, new Vector3(280f, 100f, 0f));
            AnimatorState walk = AddState(stateMachine, "Chobomb_Walk", walkTree, new Vector3(520f, 100f, 0f));
            AnimatorState attack = AddState(stateMachine, "Chobomb_Attack", attackTree, new Vector3(400f, 290f, 0f));
            AnimatorState hurt = AddState(stateMachine, "Chobomb_Hurt", hurtTree, new Vector3(160f, 290f, 0f));
            AnimatorState death = AddState(stateMachine, "Chobomb_Death", deathClip, new Vector3(640f, 290f, 0f));

            stateMachine.defaultState = idle;

            AddBoolTransition(idle, walk, "Walking", true);
            AddBoolTransition(walk, idle, "Walking", false);
            AddBoolTransition(idle, attack, "Attack", true);
            AddBoolTransition(walk, attack, "Attack", true);
            AddExitTransition(attack, idle);
            AddExitTransition(hurt, idle);
            AddAnyStateTriggerTransition(stateMachine, hurt, "Damage");
            AddAnyStateTriggerTransition(stateMachine, death, "Death");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void CreateOrUpdatePrefab(RuntimeAnimatorController controller, Sprite idleSprite)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BaseEnemyPrefabPath) == null)
            {
                throw new InvalidOperationException($"Base enemy prefab was not found: {BaseEnemyPrefabPath}");
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ChobombPrefabPath) == null)
            {
                if (!AssetDatabase.CopyAsset(BaseEnemyPrefabPath, ChobombPrefabPath))
                {
                    throw new InvalidOperationException($"Failed to copy Chobomb prefab from {BaseEnemyPrefabPath}");
                }

                AssetDatabase.ImportAsset(ChobombPrefabPath, ImportAssetOptions.ForceUpdate);
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ChobombPrefabPath);
            try
            {
                prefabRoot.name = "Chobomb_CL212";

                Animator animator = prefabRoot.GetComponentInChildren<Animator>(includeInactive: true);
                if (animator == null)
                {
                    throw new InvalidOperationException("Chobomb prefab has no Animator in children.");
                }

                animator.runtimeAnimatorController = controller;
                animator.gameObject.name = "ChobombModel";

                SpriteRenderer spriteRenderer = animator.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = prefabRoot.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
                }

                if (spriteRenderer == null)
                {
                    throw new InvalidOperationException("Chobomb prefab has no SpriteRenderer in children.");
                }

                spriteRenderer.sprite = idleSprite;
                spriteRenderer.transform.localScale = Vector3.one;
                ConfigureChobombOrientation(prefabRoot);
                ConfigureChobombSelfDestruct(prefabRoot, animator);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ChobombPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static EnemyData CreateOrUpdateEnemyData()
        {
            EnemyData enemyData = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyDataPath);
            if (enemyData == null)
            {
                enemyData = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(enemyData, EnemyDataPath);
            }

            SerializedObject serializedData = new SerializedObject(enemyData);
            serializedData.FindProperty("_displayName").stringValue = "Chobomb";
            serializedData.FindProperty("_maxHealth").floatValue = 35f;
            serializedData.FindProperty("_moveSpeed").floatValue = 2.8f;
            serializedData.FindProperty("_expReward").intValue = 0;
            serializedData.FindProperty("_dropWeight").floatValue = 1f;

            SerializedProperty attackDamages = serializedData.FindProperty("_attackDamages");
            attackDamages.arraySize = 1;
            SerializedProperty selfDestructDamage = attackDamages.GetArrayElementAtIndex(0);
            selfDestructDamage.FindPropertyRelative("_attackType").enumValueIndex = (int)EnemyAttackType.Slam;
            selfDestructDamage.FindPropertyRelative("_damage").floatValue = SelfDestructDamage;

            serializedData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(enemyData);
            return enemyData;
        }

        private static void CreateOrUpdateEnemyCatalogEntry(EnemyData enemyData)
        {
            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(EnemyCatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Enemy catalog was not found: {EnemyCatalogPath}");
            }

            GameObject chobombPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChobombPrefabPath);
            if (chobombPrefab == null)
            {
                throw new InvalidOperationException($"Chobomb prefab was not found: {ChobombPrefabPath}");
            }

            if (enemyData == null)
            {
                throw new InvalidOperationException($"Chobomb EnemyData was not found: {EnemyDataPath}");
            }

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty entries = serializedCatalog.FindProperty("entries");
            if (entries == null)
            {
                throw new InvalidOperationException("EnemyCatalog.entries serialized property was not found.");
            }

            int entryIndex = FindCatalogEntryIndex(entries, ChobombCatalogId);
            if (entryIndex < 0)
            {
                entryIndex = entries.arraySize;
                entries.arraySize++;
            }

            SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex);
            entry.FindPropertyRelative("id").stringValue = ChobombCatalogId;
            entry.FindPropertyRelative("prefab").objectReferenceValue = chobombPrefab;
            entry.FindPropertyRelative("data").objectReferenceValue = enemyData;

            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static int FindCatalogEntryIndex(SerializedProperty entries, string id)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty idProperty = entry.FindPropertyRelative("id");
                if (idProperty != null && idProperty.stringValue == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private static BlendTree CreateDirectionalBlendTree(
            AnimatorController controller,
            string treeName,
            AnimationClip frontClip,
            AnimationClip sideClip,
            AnimationClip backClip)
        {
            var tree = new BlendTree
            {
                name = treeName,
                blendType = BlendTreeType.Simple1D,
                blendParameter = "FacingDirection2D",
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            // CharacterOrientation2D maps FacingDirection2D as West=0, North=1, East=2, South=3.
            tree.AddChild(sideClip, 0f);
            tree.AddChild(backClip, 1f);
            tree.AddChild(sideClip, 2f);
            tree.AddChild(frontClip, 3f);

            EditorUtility.SetDirty(tree);
            return tree;
        }

        private static void ConfigureChobombOrientation(GameObject prefabRoot)
        {
            CharacterOrientation2D orientation = prefabRoot.GetComponent<CharacterOrientation2D>();
            if (orientation == null)
            {
                return;
            }

            orientation.ModelShouldFlip = false;
            orientation.ModelShouldRotate = true;
            orientation.ModelRotationValueLeft = Vector3.zero;
            orientation.ModelRotationValueRight = new Vector3(0f, 180f, 0f);
            orientation.InitialFacingDirection = Character.FacingDirections.West;
            orientation.CurrentFacingDirection = Character.FacingDirections.West;
            orientation.IsFacingRight = false;
            EditorUtility.SetDirty(orientation);
        }

        private static void ConfigureChobombSelfDestruct(GameObject prefabRoot, Animator animator)
        {
            AIBrain brain = prefabRoot.GetComponent<AIBrain>();
            CharacterMovement movement = prefabRoot.GetComponent<CharacterMovement>();
            CharacterOrientation2D orientation = prefabRoot.GetComponent<CharacterOrientation2D>();
            CharacterHandleWeapon handleWeapon = prefabRoot.GetComponent<CharacterHandleWeapon>();
            Health health = prefabRoot.GetComponent<Health>();

            if (handleWeapon != null)
            {
                handleWeapon.PermitAbility(false);
                EditorUtility.SetDirty(handleWeapon);
            }

            if (health != null)
            {
                health.InitialHealth = 35f;
                health.MaximumHealth = 35f;
                health.CurrentHealth = 35f;
                health.TargetAnimator = animator;
                health.DestroyOnDeath = true;
                health.DelayBeforeDestruction = Mathf.Max(health.DelayBeforeDestruction, SelfDestructDestroyDelay);
                health.DisableModelOnDeath = false;
                health.DisableControllerOnDeath = true;
                health.DisableCollisionsOnDeath = true;
                EditorUtility.SetDirty(health);
            }

            AttackTelegraph2DView telegraphView = prefabRoot.GetComponent<AttackTelegraph2DView>();
            if (telegraphView == null)
            {
                telegraphView = prefabRoot.AddComponent<AttackTelegraph2DView>();
            }

            ChobombSelfDestructController selfDestruct = prefabRoot.GetComponent<ChobombSelfDestructController>();
            if (selfDestruct == null)
            {
                selfDestruct = prefabRoot.AddComponent<ChobombSelfDestructController>();
            }

            selfDestruct.Configure(
                animator,
                telegraphView,
                brain,
                movement,
                orientation,
                handleWeapon,
                health);
            selfDestruct.ConfigureTuning(
                SelfDestructArmingRadius,
                SelfDestructWarningDuration,
                SelfDestructExplosionRadius,
                SelfDestructDamage,
                SelfDestructInvincibilityDuration,
                SelfDestructDestroyDelay,
                new LayerMask { value = TargetLayerMaskBits });

            ChobombExplosionKnockback explosionKnockback = prefabRoot.GetComponent<ChobombExplosionKnockback>();
            if (explosionKnockback == null)
            {
                explosionKnockback = prefabRoot.AddComponent<ChobombExplosionKnockback>();
            }

            explosionKnockback.Configure(SelfDestructKnockbackForce);

            EditorUtility.SetDirty(telegraphView);
            EditorUtility.SetDirty(selfDestruct);
            EditorUtility.SetDirty(explosionKnockback);
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

        private static void ClearStateMachine(AnimatorStateMachine stateMachine)
        {
            for (int i = stateMachine.anyStateTransitions.Length - 1; i >= 0; i--)
            {
                stateMachine.RemoveAnyStateTransition(stateMachine.anyStateTransitions[i]);
            }

            for (int i = stateMachine.states.Length - 1; i >= 0; i--)
            {
                stateMachine.RemoveState(stateMachine.states[i].state);
            }
        }

        private static void ClearControllerBlendTrees()
        {
            BlendTree[] blendTrees = AssetDatabase.LoadAllAssetsAtPath(ControllerPath)
                .OfType<BlendTree>()
                .ToArray();

            for (int i = blendTrees.Length - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(blendTrees[i], allowDestroyingAssets: true);
            }
        }

        private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameterName, bool expectedValue)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.hasFixedDuration = true;
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameterName);
        }

        private static void AddExitTransition(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0.05f;
            transition.hasFixedDuration = true;
        }

        private static void AddAnyStateTriggerTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState to,
            string parameterName)
        {
            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.02f;
            transition.hasFixedDuration = true;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameterName);
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            AnimatorControllerParameter existing = controller.parameters.FirstOrDefault(parameter =>
                parameter != null && parameter.name == parameterName);
            if (existing != null)
            {
                if (existing.type != parameterType)
                {
                    controller.RemoveParameter(existing);
                    controller.AddParameter(parameterName, parameterType);
                }

                return;
            }

            controller.AddParameter(parameterName, parameterType);
        }

        private static void SetClipLooping(AnimationClip clip, bool loop)
        {
            SerializedObject serializedClip = new SerializedObject(clip);
            SerializedProperty settings = serializedClip.FindProperty("m_AnimationClipSettings");
            SerializedProperty loopTime = settings?.FindPropertyRelative("m_LoopTime");
            if (loopTime == null)
            {
                return;
            }

            loopTime.boolValue = loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string SpriteName(int row, int column)
        {
            return $"Chobomb1_r{row:00}_c{column:00}";
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }

                currentPath = nextPath;
            }
        }
    }
}
