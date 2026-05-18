using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Enemies;
using LostMemory.UI.Minimap;
using MoreMountains.TopDownEngine;
using MoreMountains.Tools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostMemory.Editor.Enemies
{
    [InitializeOnLoad]
    public static class SlimeEnemyAssetBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Slime_Test.prefab";
        private const string DataPath = "Assets/_Project/ScriptableObjects/Enemies/EnemyData_Slime.asset";
        private const string CatalogPath = "Assets/_Project/ScriptableObjects/Enemies/EnemyCatalog_Default.asset";
        private const string SpriteSheetPath = "Assets/_Project/Art/Enemies/Slime/Sprites/momo_spriteSheet1.png";
        private const string AnimationFolder = "Assets/_Project/Art/Animations/Enemies/Slime";
        private const string ControllerPath = AnimationFolder + "/Slime.controller";
        private const string CatalogId = "enemy_slime";
        private const string EnemyMinimapIconGuid = "69bdc1f611c0f8d4dbc71613a9b04b9b";
        private const string ShadowSpriteGuid = "e8e3b7691aa7ca7458f64e04cb4830bf";
        private const string ShadowSpriteName = "AdventurerShadow";
        private const int IdleFrameRate = 8;
        private const int MoveFrameRate = 8;
        private const int JumpFrameRate = 15;
        private const int HurtFrameRate = 12;
        private const int DeathFrameRate = 12;
        private const int AttackFrameRate = 18;
        private const float VisualScale = 3.85f;
        private const string RequestFileName = "SlimeEnemyAssetBuilder.request";

        static SlimeEnemyAssetBuilder()
        {
            RunWhenRequested();
        }

        [InitializeOnLoadMethod]
        private static void RunWhenRequested()
        {
            if (!System.IO.File.Exists(RequestFilePath))
            {
                return;
            }

            EditorApplication.delayCall += TryRunRequestedBuild;
        }

        [MenuItem("Lost Memory/Enemies/Build Slime Enemy Assets")]
        public static void Build()
        {
            EnsureFolder(AnimationFolder);
            EnemyData data = CreateOrUpdateData();
            RuntimeAnimatorController animatorController = CreateOrUpdateAnimatorAssets(out Sprite idleSprite);
            GameObject prefab = CreateOrUpdatePrefab(animatorController, idleSprite);
            AddToCatalog(prefab, data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SlimeEnemyAssetBuilder] Slime enemy assets built.");
        }

        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        private static string RequestFilePath =>
            System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Temp", RequestFileName));

        private static void TryRunRequestedBuild()
        {
            if (!System.IO.File.Exists(RequestFilePath))
            {
                return;
            }

            System.IO.File.Delete(RequestFilePath);
            Build();
        }

        private static EnemyData CreateOrUpdateData()
        {
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }

            SerializedObject serializedData = new SerializedObject(data);
            serializedData.FindProperty("_displayName").stringValue = "Slime";
            serializedData.FindProperty("_maxHealth").floatValue = 35f;
            serializedData.FindProperty("_moveSpeed").floatValue = 2f;
            serializedData.FindProperty("_expReward").intValue = 0;
            serializedData.FindProperty("_dropWeight").floatValue = 1f;

            SerializedProperty damages = serializedData.FindProperty("_attackDamages");
            damages.arraySize = 1;
            SerializedProperty meleeDamage = damages.GetArrayElementAtIndex(0);
            meleeDamage.FindPropertyRelative("_attackType").enumValueIndex = (int)EnemyAttackType.Melee;
            meleeDamage.FindPropertyRelative("_damage").floatValue = 8f;

            serializedData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static RuntimeAnimatorController CreateOrUpdateAnimatorAssets(out Sprite idleSprite)
        {
            DeleteLegacyReadyAssets();

            Sprite[] idleFrames = LoadSlicedRow(15, 4);
            idleSprite = idleFrames.Length > 0 ? idleFrames[0] : null;

            AnimationClip idleClip = CreateOrUpdateClip("Slime_Idle", idleFrames, loop: true, IdleFrameRate);
            AnimationClip moveFrontClip = CreateOrUpdateClip("Slime_Move_Front", LoadSlicedRow(0, 4), loop: true, MoveFrameRate);
            AnimationClip moveSideClip = CreateOrUpdateClip("Slime_Move_Side", LoadSlicedRow(1, 4), loop: true, MoveFrameRate);
            AnimationClip moveBackClip = CreateOrUpdateClip("Slime_Move_Back", LoadSlicedRow(2, 4), loop: true, MoveFrameRate);
            AnimationClip jumpFrontClip = CreateOrUpdateClip("Slime_Jump_Front", LoadSlicedRow(3, 5), loop: false, JumpFrameRate);
            AnimationClip jumpSideClip = CreateOrUpdateClip("Slime_Jump_Side", LoadSlicedRow(4, 5), loop: false, JumpFrameRate);
            AnimationClip jumpBackClip = CreateOrUpdateClip("Slime_Jump_Back", LoadSlicedRow(5, 5), loop: false, JumpFrameRate);
            AnimationClip hurtFrontClip = CreateOrUpdateClip("Slime_Hurt_Front", LoadSlicedRow(6, 5), loop: false, HurtFrameRate);
            AnimationClip hurtSideClip = CreateOrUpdateClip("Slime_Hurt_Side", LoadSlicedRow(7, 5), loop: false, HurtFrameRate);
            AnimationClip hurtBackClip = CreateOrUpdateClip("Slime_Hurt_Back", LoadSlicedRow(8, 5), loop: false, HurtFrameRate);
            AnimationClip deathFrontClip = CreateOrUpdateClip("Slime_Death_Front", LoadSlicedRow(9, 6), loop: false, DeathFrameRate);
            AnimationClip deathSideClip = CreateOrUpdateClip("Slime_Death_Side", LoadSlicedRow(10, 6), loop: false, DeathFrameRate);
            AnimationClip deathBackClip = CreateOrUpdateClip("Slime_Death_Back", LoadSlicedRow(11, 6), loop: false, DeathFrameRate);
            AnimationClip attackFrontClip = CreateOrUpdateClip("Slime_Attack_Front", LoadSlicedRow(12, 6), loop: false, AttackFrameRate);
            AnimationClip attackSideClip = CreateOrUpdateClip("Slime_Attack_Side", LoadSlicedRow(13, 6), loop: false, AttackFrameRate);
            AnimationClip attackBackClip = CreateOrUpdateClip("Slime_Attack_Back", LoadSlicedRow(14, 6), loop: false, AttackFrameRate);

            return CreateOrUpdateController(
                idleClip,
                moveFrontClip,
                moveSideClip,
                moveBackClip,
                jumpFrontClip,
                jumpSideClip,
                jumpBackClip,
                hurtFrontClip,
                hurtSideClip,
                hurtBackClip,
                deathFrontClip,
                deathSideClip,
                deathBackClip,
                attackFrontClip,
                attackSideClip,
                attackBackClip);
        }

        private static AnimationClip CreateOrUpdateClip(
            string clipName,
            IReadOnlyList<Sprite> sprites,
            bool loop,
            int frameRate)
        {
            if (sprites == null || sprites.Count == 0)
            {
                throw new System.InvalidOperationException($"No sprites found for Slime animation clip: {clipName}");
            }

            string clipPath = AnimationFolder + "/" + clipName + ".anim";
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

        private static RuntimeAnimatorController CreateOrUpdateController(
            AnimationClip idleClip,
            AnimationClip moveFrontClip,
            AnimationClip moveSideClip,
            AnimationClip moveBackClip,
            AnimationClip jumpFrontClip,
            AnimationClip jumpSideClip,
            AnimationClip jumpBackClip,
            AnimationClip hurtFrontClip,
            AnimationClip hurtSideClip,
            AnimationClip hurtBackClip,
            AnimationClip deathFrontClip,
            AnimationClip deathSideClip,
            AnimationClip deathBackClip,
            AnimationClip attackFrontClip,
            AnimationClip attackSideClip,
            AnimationClip attackBackClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            EnsureParameter(controller, "FacingDirection2D", AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            ClearStateMachine(stateMachine);
            ClearControllerBlendTrees();

            BlendTree idleTree = CreateDirectionalBlendTree(controller, "Slime_Idle_Directional", idleClip, idleClip, idleClip);
            BlendTree moveTree = CreateDirectionalBlendTree(controller, "Slime_Move_Directional", moveFrontClip, moveSideClip, moveBackClip);
            BlendTree jumpTree = CreateDirectionalBlendTree(controller, "Slime_Jump_Directional", jumpFrontClip, jumpSideClip, jumpBackClip);
            BlendTree hurtTree = CreateDirectionalBlendTree(controller, "Slime_Hurt_Directional", hurtFrontClip, hurtSideClip, hurtBackClip);
            BlendTree deathTree = CreateDirectionalBlendTree(controller, "Slime_Death_Directional", deathFrontClip, deathSideClip, deathBackClip);
            BlendTree attackTree = CreateDirectionalBlendTree(controller, "Slime_Attack_Directional", attackFrontClip, attackSideClip, attackBackClip);

            AnimatorState idle = AddState(stateMachine, "Slime_Idle", idleTree, new Vector3(280f, 80f, 0f));
            AddState(stateMachine, "Slime_Move", moveTree, new Vector3(520f, 80f, 0f));
            AddState(stateMachine, "Slime_Jump", jumpTree, new Vector3(280f, 240f, 0f));
            AddState(stateMachine, "Slime_Attack", attackTree, new Vector3(520f, 240f, 0f));
            AddState(stateMachine, "Slime_Hurt", hurtTree, new Vector3(40f, 240f, 0f));
            AddState(stateMachine, "Slime_Death", deathTree, new Vector3(760f, 240f, 0f));
            stateMachine.defaultState = idle;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject CreateOrUpdatePrefab(RuntimeAnimatorController animatorController, Sprite idleSprite)
        {
            int enemiesLayer = LayerMask.NameToLayer("Enemies");
            int defaultLayer = LayerMask.NameToLayer("Default");
            int playerLayer = LayerMask.NameToLayer("Player");

            GameObject root = new GameObject("Slime_Test");
            root.tag = "Enemy";
            root.layer = enemiesLayer;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = 40f;
            body.linearDamping = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            BoxCollider2D bodyCollider = root.AddComponent<BoxCollider2D>();
            bodyCollider.offset = new Vector2(0f, 0.12f);
            bodyCollider.size = new Vector2(0.74f, 0.46f);

            SortingGroup sortingGroup = root.AddComponent<SortingGroup>();
            sortingGroup.sortingLayerName = "Characters";
            sortingGroup.sortingOrder = 0;

            Health health = root.AddComponent<Health>();
            health.InitialHealth = 35f;
            health.MaximumHealth = 35f;
            health.CurrentHealth = 35f;
            health.ResetHealthOnEnable = true;
            health.DestroyOnDeath = true;
            health.DelayBeforeDestruction = 0.65f;
            health.DisableModelOnDeath = false;
            health.DisableCollisionsOnDeath = true;
            health.DisableChildCollisionsOnDeath = true;
            health.KnockbackForceMultiplier = 1f;

            MMHealthBar healthBar = root.AddComponent<MMHealthBar>();
            ConfigureHealthBar(healthBar);

            MinimapAgent minimapAgent = root.AddComponent<MinimapAgent>();
            ConfigureMinimapAgent(minimapAgent);

            GameObject visual = new GameObject("SlimeModel");
            visual.layer = enemiesLayer;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * VisualScale;
            SpriteRenderer spriteRenderer = visual.AddComponent<SpriteRenderer>();
            if (idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }

            spriteRenderer.sortingLayerName = "Characters";
            spriteRenderer.sortingOrder = 0;
            Animator animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
            health.Model = visual;
            health.TargetAnimator = animator;

            CreateShadow(root.transform, enemiesLayer);

            GameObject attackObject = new GameObject("AttackHitbox");
            attackObject.layer = defaultLayer;
            attackObject.transform.SetParent(root.transform, false);
            attackObject.transform.localPosition = new Vector3(0.46f, 0.12f, 0f);
            BoxCollider2D attackHitbox = attackObject.AddComponent<BoxCollider2D>();
            attackHitbox.isTrigger = true;
            attackHitbox.size = new Vector2(0.9f, 0.52f);
            attackHitbox.enabled = false;

            SlimeEnemyController controller = root.AddComponent<SlimeEnemyController>();
            SerializedObject serializedController = new SerializedObject(controller);
            SetObject(serializedController, "animator", animator);
            SetObject(serializedController, "spriteRenderer", spriteRenderer);
            SetObject(serializedController, "body", body);
            SetObject(serializedController, "bodyCollider", bodyCollider);
            SetObject(serializedController, "attackHitbox", attackHitbox);
            SetObject(serializedController, "health", health);
            SetBool(serializedController, "flipSideSpriteOnPositiveX", true);
            SetFloat(serializedController, "maxHealth", 35f);
            SetFloat(serializedController, "moveSpeed", 2f);
            SetFloat(serializedController, "detectionRadius", 6f);
            SetFloat(serializedController, "attackRange", 1.05f);
            SetFloat(serializedController, "wanderSpeedMultiplier", 0.55f);
            SetFloat(serializedController, "wanderMoveDuration", 1.2f);
            SetFloat(serializedController, "wanderPauseDuration", 0.6f);
            SetFloat(serializedController, "wanderIdleChance", 0.3f);
            SetInt(serializedController, "hopsBeforePause", 2);
            SetFloat(serializedController, "hopDuration", 0.32f);
            SetFloat(serializedController, "pauseDuration", 0.45f);
            SetFloat(serializedController, "preAttackPause", 0.45f);
            SetFloat(serializedController, "attackWindup", 0.1f);
            SetFloat(serializedController, "attackActiveDuration", 0.22f);
            SetFloat(serializedController, "attackRecoverDuration", 0.35f);
            SetFloat(serializedController, "attackCooldown", 3f);
            SetFloat(serializedController, "attackDamage", 8f);
            SetFloat(serializedController, "attackInvincibilityDuration", 0.5f);
            serializedController.FindProperty("attackHitboxBaseOffset").vector2Value = new Vector2(0f, 0.12f);
            SetFloat(serializedController, "attackHitboxDistance", 0.46f);
            SetBool(serializedController, "applyAttackKnockback", true);
            SetFloat(serializedController, "attackKnockbackForce", 250f);
            SetLayerMask(serializedController, "targetLayerMask", 1 << playerLayer);
            SetInt(serializedController, "attackOverlapBufferSize", 8);
            SetFloat(serializedController, "hurtVisualDuration", 0.18f);
            SetFloat(serializedController, "deathDestroyDelay", 0.65f);
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static void ConfigureHealthBar(MMHealthBar healthBar)
        {
            if (healthBar == null)
            {
                return;
            }

            SerializedObject serializedHealthBar = new SerializedObject(healthBar);
            SetSerializedInt(serializedHealthBar, "HealthBarType", 1);
            SetSerializedInt(serializedHealthBar, "TimeScale", 0);
            SetSerializedVector2(serializedHealthBar, "Size", new Vector2(1.5f, 0.4f));
            SetSerializedVector2(serializedHealthBar, "BackgroundPadding", new Vector2(0.05f, 0.05f));
            SetSerializedVector3(serializedHealthBar, "InitialRotationAngles", Vector3.zero);
            SetSerializedGradient(serializedHealthBar, "ForegroundColor", CreateGradient(new Color(0.735849f, 0f, 0f, 1f), new Color(1f, 0.16001588f, 0f, 1f)));
            SetSerializedGradient(serializedHealthBar, "DelayedColor", CreateGradient(new Color(1f, 0.41178104f, 0f, 1f), new Color(1f, 0.6604273f, 0f, 1f)));
            SetSerializedGradient(serializedHealthBar, "BorderColor", CreateGradient(Color.black, Color.black));
            SetSerializedGradient(serializedHealthBar, "BackgroundColor", CreateGradient(new Color(0.3207547f, 0f, 0f, 1f), new Color(0.3773585f, 0f, 0f, 1f)));
            SetSerializedString(serializedHealthBar, "SortingLayerName", "Above");
            SetSerializedFloat(serializedHealthBar, "Delay", 0.5f);
            SetSerializedBool(serializedHealthBar, "LerpFrontBar", true);
            SetSerializedFloat(serializedHealthBar, "LerpFrontBarSpeed", 15f);
            SetSerializedBool(serializedHealthBar, "LerpDelayedBar", true);
            SetSerializedFloat(serializedHealthBar, "LerpDelayedBarSpeed", 30f);
            SetSerializedBool(serializedHealthBar, "BumpScaleOnChange", true);
            SetSerializedFloat(serializedHealthBar, "BumpDuration", 0.2f);
            SetSerializedInt(serializedHealthBar, "FollowTargetMode", 2);
            SetSerializedBool(serializedHealthBar, "FollowRotation", false);
            SetSerializedBool(serializedHealthBar, "FollowScale", true);
            SetSerializedBool(serializedHealthBar, "NestDrawnHealthBar", false);
            SetSerializedBool(serializedHealthBar, "Billboard", false);
            SetSerializedVector3(serializedHealthBar, "HealthBarOffset", new Vector3(0f, 1.25f, 0f));
            SetSerializedBool(serializedHealthBar, "AlwaysVisible", false);
            SetSerializedFloat(serializedHealthBar, "DisplayDurationOnHit", 1f);
            SetSerializedBool(serializedHealthBar, "HideBarAtZero", true);
            SetSerializedFloat(serializedHealthBar, "HideBarAtZeroDelay", 0f);
            SetSerializedFloat(serializedHealthBar, "TestMinHealth", 0f);
            SetSerializedFloat(serializedHealthBar, "TestMaxHealth", 100f);
            SetSerializedFloat(serializedHealthBar, "TestCurrentHealth", 25f);
            serializedHealthBar.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Gradient CreateGradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(end.a, 1f)
                });
            return gradient;
        }

        private static void ConfigureMinimapAgent(MinimapAgent minimapAgent)
        {
            if (minimapAgent == null)
            {
                return;
            }

            SerializedObject serializedAgent = new SerializedObject(minimapAgent);
            serializedAgent.FindProperty("kind").enumValueIndex = (int)MinimapAgent.AgentKind.Enemy;
            serializedAgent.FindProperty("icon").objectReferenceValue = LoadSpriteByGuid(EnemyMinimapIconGuid);
            serializedAgent.FindProperty("tint").colorValue = Color.white;
            serializedAgent.FindProperty("iconScale").floatValue = 1f;
            serializedAgent.FindProperty("rotateWithTransform").boolValue = false;
            serializedAgent.FindProperty("priority").intValue = 10;
            serializedAgent.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedInt(SerializedObject target, string propertyName, int value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.Enum)
            {
                property.enumValueIndex = value;
            }
            else
            {
                property.intValue = value;
            }
        }

        private static void SetSerializedFloat(SerializedObject target, string propertyName, float value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetSerializedBool(SerializedObject target, string propertyName, bool value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetSerializedString(SerializedObject target, string propertyName, string value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetSerializedVector2(SerializedObject target, string propertyName, Vector2 value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
            {
                property.vector2Value = value;
            }
        }

        private static void SetSerializedVector3(SerializedObject target, string propertyName, Vector3 value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
            }
        }

        private static void SetSerializedGradient(SerializedObject target, string propertyName, Gradient value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
            {
                property.gradientValue = value;
            }
        }

        private static void CreateShadow(Transform parent, int layer)
        {
            GameObject shadow = new GameObject("AdventurerShadow");
            shadow.layer = layer;
            shadow.transform.SetParent(parent, false);
            shadow.transform.localPosition = new Vector3(0f, -0.002f, 0f);
            shadow.transform.localScale = new Vector3(1f, 1f, 2f);

            SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = LoadSpriteByGuidAndName(ShadowSpriteGuid, ShadowSpriteName);
            shadowRenderer.sortingLayerName = "Ground";
            shadowRenderer.sortingOrder = 0;
        }

        private static Sprite LoadSpriteByGuidAndName(string guid, string spriteName)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite assetSprite && assetSprite.name == spriteName)
                {
                    return assetSprite;
                }
            }

            return LoadSpriteByGuid(guid);
        }

        private static Sprite LoadSpriteByGuid(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite assetSprite)
                {
                    return assetSprite;
                }
            }

            return null;
        }

        private static void AddToCatalog(GameObject prefab, EnemyData data)
        {
            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            if (catalog == null || prefab == null || data == null)
            {
                Debug.LogWarning("[SlimeEnemyAssetBuilder] Catalog, prefab, or data is missing. Skip catalog update.");
                return;
            }

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty entries = serializedCatalog.FindProperty("entries");
            int index = FindCatalogEntry(entries, CatalogId);
            if (index < 0)
            {
                index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
            }

            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("id").stringValue = CatalogId;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("data").objectReferenceValue = data;
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static int FindCatalogEntry(SerializedProperty entries, string id)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("id").stringValue == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void DeleteLegacyReadyAssets()
        {
            DeleteAssetIfExists(AnimationFolder + "/Slime_Ready_Front.anim");
            DeleteAssetIfExists(AnimationFolder + "/Slime_Ready_Side.anim");
            DeleteAssetIfExists(AnimationFolder + "/Slime_Ready_Back.anim");
        }

        private static void DeleteAssetIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
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
            tree.AddChild(sideClip, 0f);
            tree.AddChild(backClip, 1f);
            tree.AddChild(sideClip, 2f);
            tree.AddChild(frontClip, 3f);
            EditorUtility.SetDirty(tree);
            return tree;
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
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ControllerPath);
            for (int i = assets.Length - 1; i >= 0; i--)
            {
                if (assets[i] is BlendTree blendTree)
                {
                    Object.DestroyImmediate(blendTree, allowDestroyingAssets: true);
                }
            }
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter == null || parameter.name != parameterName)
                {
                    continue;
                }

                if (parameter.type != parameterType)
                {
                    controller.RemoveParameter(parameter);
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

        private static void SetObject(SerializedObject target, string propertyName, Object value)
        {
            target.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void SetFloat(SerializedObject target, string propertyName, float value)
        {
            target.FindProperty(propertyName).floatValue = value;
        }

        private static void SetInt(SerializedObject target, string propertyName, int value)
        {
            target.FindProperty(propertyName).intValue = value;
        }

        private static void SetBool(SerializedObject target, string propertyName, bool value)
        {
            target.FindProperty(propertyName).boolValue = value;
        }

        private static void SetLayerMask(SerializedObject target, string propertyName, int value)
        {
            target.FindProperty(propertyName).intValue = value;
        }

        private static Sprite[] LoadSlicedRow(int rowFromTop, int frameCount)
        {
            Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpriteSheetPath);
            List<Sprite> sprites = new List<Sprite>();
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }

            sprites.Sort((left, right) =>
            {
                int yComparison = right.rect.y.CompareTo(left.rect.y);
                return yComparison != 0 ? yComparison : left.rect.x.CompareTo(right.rect.x);
            });

            List<float> rows = new List<float>();
            for (int i = 0; i < sprites.Count; i++)
            {
                float y = sprites[i].rect.y;
                if (!rows.Contains(y))
                {
                    rows.Add(y);
                }
            }

            if (rows.Count == 0)
            {
                return System.Array.Empty<Sprite>();
            }

            int rowIndex = Mathf.Clamp(rowFromTop, 0, rows.Count - 1);
            float targetY = rows[rowIndex];
            List<Sprite> rowSprites = new List<Sprite>();
            for (int i = 0; i < sprites.Count; i++)
            {
                if (Mathf.Approximately(sprites[i].rect.y, targetY))
                {
                    rowSprites.Add(sprites[i]);
                }
            }

            int count = Mathf.Clamp(frameCount, 0, rowSprites.Count);
            Sprite[] result = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = rowSprites[i];
            }

            return result;
        }
    }
}
