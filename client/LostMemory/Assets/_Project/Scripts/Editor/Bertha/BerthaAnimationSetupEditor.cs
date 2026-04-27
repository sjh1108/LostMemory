using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LostMemory.Stage;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LostMemory.Editor.Bertha
{
    public static class BerthaAnimationSetupEditor
    {
        private const string BerthaRootMenu = "LostMemory/Bertha/Build Idle + Walk + Entry + Combo Animator";
        private const string BaseArtFolder = "Assets/_Project/Art/Enemies/Boss/1_Bertha";
        private const string IdleFramesFolder = BaseArtFolder + "/Idle/NoBite";
        private const string WalkFramesFolder = BaseArtFolder + "/Walk/Walking";
        private const string EntryFramesFolder = BaseArtFolder + "/Entry";
        private const string LightAttack1FramesFolder = BaseArtFolder + "/Attacks/ComboAtk/1";
        private const string LightAttack2FramesFolder = BaseArtFolder + "/Attacks/ComboAtk/2";
        private const string HeavyAttackFramesFolder = BaseArtFolder + "/Attacks/ComboAtk/3";
        private const string DashAttackFramesFolder = BaseArtFolder + "/Attacks/DashAtk/Full";
        private const string FullComboFramesFolder = BaseArtFolder + "/Attacks/ComboAtk/Full";
        private const string OutputFolder = "Assets/_Project/Art/Animations/Enemies/Boss/Bertha";
        private const string IdleClipPath = OutputFolder + "/BerthaIdle.anim";
        private const string WalkClipPath = OutputFolder + "/BerthaWalk.anim";
        private const string EntryClipPath = OutputFolder + "/BerthaEntry.anim";
        private const string LightAttack1ClipPath = OutputFolder + "/BerthaLightAtk1.anim";
        private const string LightAttack2ClipPath = OutputFolder + "/BerthaLightAtk2.anim";
        private const string HeavyAttackClipPath = OutputFolder + "/BerthaHeavyAtk.anim";
        private const string DashAttackClipPath = OutputFolder + "/BerthaDashAtk.anim";
        private const string FullComboClipPath = OutputFolder + "/BerthaFullCombo.anim";
        private const string ControllerPath = OutputFolder + "/Bertha.controller";
        private const string VisualChildName = "Visual";
        private const string SpriteChildName = "BerthaSprite";
        private const int TargetFrameRate = 12;

        [MenuItem(BerthaRootMenu)]
        public static void BuildIdleAndEntryAnimator()
        {
            try
            {
                EnsureFolder(OutputFolder);

                List<Sprite> idleSprites = ImportSprites(IdleFramesFolder);
                List<Sprite> walkSprites = ImportSprites(WalkFramesFolder);
                List<Sprite> entrySprites = ImportSprites(EntryFramesFolder);
                List<Sprite> lightAttack1Sprites = ImportSprites(LightAttack1FramesFolder);
                List<Sprite> lightAttack2Sprites = ImportSprites(LightAttack2FramesFolder);
                List<Sprite> heavyAttackSprites = ImportSprites(HeavyAttackFramesFolder);
                List<Sprite> dashAttackSprites = ImportSprites(DashAttackFramesFolder);
                List<Sprite> fullComboSprites = ImportSprites(FullComboFramesFolder);

                if (idleSprites.Count == 0)
                {
                    Debug.LogError("[BerthaAnimationSetup] Idle frames were not found.");
                    return;
                }

                if (entrySprites.Count == 0)
                {
                    Debug.LogError("[BerthaAnimationSetup] Entry frames were not found.");
                    return;
                }

                if (walkSprites.Count == 0)
                {
                    Debug.LogError("[BerthaAnimationSetup] Walk frames were not found.");
                    return;
                }

                if (lightAttack1Sprites.Count == 0)
                {
                    Debug.LogError("[BerthaAnimationSetup] Light attack 1 frames were not found.");
                    return;
                }

                if (lightAttack2Sprites.Count == 0)
                {
                    Debug.LogError("[BerthaAnimationSetup] Light attack 2 frames were not found.");
                    return;
                }

                if (heavyAttackSprites.Count == 0)
                {
                    Debug.LogError("[BerthaAnimationSetup] Heavy attack frames were not found.");
                    return;
                }

                if (dashAttackSprites.Count == 0)
                {
                    Debug.LogError("[BerthaAnimationSetup] Dash attack frames were not found.");
                    return;
                }

                if (fullComboSprites.Count == 0)
                {
                    Debug.LogError("[BerthaAnimationSetup] Full combo frames were not found.");
                    return;
                }

                AnimationClip idleClip = CreateOrUpdateClip(IdleClipPath, idleSprites, loop: true);
                AnimationClip walkClip = CreateOrUpdateClip(WalkClipPath, walkSprites, loop: true);
                AnimationClip entryClip = CreateOrUpdateClip(EntryClipPath, entrySprites, loop: false);
                AnimationClip lightAttack1Clip = CreateOrUpdateClip(LightAttack1ClipPath, lightAttack1Sprites, loop: false);
                AnimationClip lightAttack2Clip = CreateOrUpdateClip(LightAttack2ClipPath, lightAttack2Sprites, loop: false);
                AnimationClip heavyAttackClip = CreateOrUpdateClip(HeavyAttackClipPath, heavyAttackSprites, loop: false);
                AnimationClip dashAttackClip = CreateOrUpdateClip(DashAttackClipPath, dashAttackSprites, loop: false);
                AnimationClip fullComboClip = CreateOrUpdateClip(FullComboClipPath, fullComboSprites, loop: false);
                AnimatorController controller = CreateOrUpdateController(
                    idleClip,
                    walkClip,
                    entryClip,
                    lightAttack1Clip,
                    lightAttack2Clip,
                    heavyAttackClip,
                    dashAttackClip,
                    fullComboClip);

                if (Selection.activeGameObject != null)
                {
                    ApplyAnimatorToSelectedRoot(Selection.activeGameObject, controller, idleSprites[0]);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[BerthaAnimationSetup] Built Bertha Idle/Walk/Entry/LightAtk1/LightAtk2/HeavyAtk/DashAtk/FullCombo animator assets.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static List<Sprite> ImportSprites(string folderPath)
        {
            string[] pngGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            List<string> assetPaths = new List<string>(pngGuids.Length);

            for (int i = 0; i < pngGuids.Length; i++)
            {
                assetPaths.Add(AssetDatabase.GUIDToAssetPath(pngGuids[i]));
            }

            assetPaths.Sort(CompareByTrailingNumberThenName);

            List<Sprite> sprites = new List<Sprite>(assetPaths.Count);
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string assetPath = assetPaths[i];
                ConfigureSpriteImporter(assetPath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }

            return sprites;
        }

        private static void ConfigureSpriteImporter(string assetPath)
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

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip CreateOrUpdateClip(string clipPath, IReadOnlyList<Sprite> sprites, bool loop)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = TargetFrameRate;
            SetClipLooping(clip, loop);

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
                    time = i / (float)TargetFrameRate,
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController CreateOrUpdateController(
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip entryClip,
            AnimationClip lightAttack1Clip,
            AnimationClip lightAttack2Clip,
            AnimationClip heavyAttackClip,
            AnimationClip dashAttackClip,
            AnimationClip fullComboClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorStateMachine stateMachine = layer.stateMachine;

            for (int i = stateMachine.states.Length - 1; i >= 0; i--)
            {
                stateMachine.RemoveState(stateMachine.states[i].state);
            }

            AnimatorState idleState = stateMachine.AddState("Idle");
            idleState.motion = idleClip;

            AnimatorState walkState = stateMachine.AddState("Walk");
            walkState.motion = walkClip;

            AnimatorState entryState = stateMachine.AddState("Entry");
            entryState.motion = entryClip;

            AnimatorState lightAttack1State = stateMachine.AddState("LightAtk1");
            lightAttack1State.motion = lightAttack1Clip;

            AnimatorState lightAttack2State = stateMachine.AddState("LightAtk2");
            lightAttack2State.motion = lightAttack2Clip;

            AnimatorState heavyAttackState = stateMachine.AddState("HeavyAtk");
            heavyAttackState.motion = heavyAttackClip;

            AnimatorState dashAttackState = stateMachine.AddState("DashAtk");
            dashAttackState.motion = dashAttackClip;

            AnimatorState fullComboState = stateMachine.AddState("FullCombo");
            fullComboState.motion = fullComboClip;

            CreateExitTransition(entryState, idleState);
            CreateExitTransition(lightAttack1State, idleState);
            CreateExitTransition(lightAttack2State, idleState);
            CreateExitTransition(heavyAttackState, idleState);
            CreateExitTransition(dashAttackState, idleState);
            CreateExitTransition(fullComboState, idleState);

            stateMachine.defaultState = idleState;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void CreateExitTransition(AnimatorState fromState, AnimatorState idleState)
        {
            AnimatorStateTransition transition = fromState.AddTransition(idleState);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0.05f;
            transition.hasFixedDuration = true;
        }

        private static void ApplyAnimatorToSelectedRoot(GameObject selectedObject, AnimatorController controller, Sprite idleSprite)
        {
            Transform visualRoot = selectedObject.transform.Find(VisualChildName);
            if (visualRoot == null)
            {
                Debug.LogWarning("[BerthaAnimationSetup] Selected object doesn't have a Visual child. Animator assets were created, but scene binding was skipped.");
                return;
            }

            Transform spriteRoot = visualRoot.Find(SpriteChildName);
            if (spriteRoot == null)
            {
                GameObject spriteObject = new GameObject(SpriteChildName);
                spriteRoot = spriteObject.transform;
                spriteRoot.SetParent(visualRoot, false);
            }

            SpriteRenderer spriteRenderer = spriteRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = spriteRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = idleSprite;

            Animator animator = spriteRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = spriteRoot.gameObject.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;

            BossIntroSequenceController introSequenceController = selectedObject.GetComponent<BossIntroSequenceController>();
            if (introSequenceController != null)
            {
                SerializedObject serializedController = new SerializedObject(introSequenceController);
                SerializedProperty bossAnimatorProperty = serializedController.FindProperty("bossAnimator");
                if (bossAnimatorProperty != null)
                {
                    bossAnimatorProperty.objectReferenceValue = animator;
                    serializedController.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorUtility.SetDirty(spriteRoot.gameObject);
            EditorUtility.SetDirty(selectedObject);
        }

        private static int CompareByTrailingNumberThenName(string left, string right)
        {
            int leftNumber = ExtractTrailingNumber(left);
            int rightNumber = ExtractTrailingNumber(right);

            int numericCompare = leftNumber.CompareTo(rightNumber);
            if (numericCompare != 0)
            {
                return numericCompare;
            }

            return string.CompareOrdinal(left, right);
        }

        private static int ExtractTrailingNumber(string assetPath)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assetPath);
            Match match = Regex.Match(fileNameWithoutExtension, @"(\d+)$");
            if (!match.Success)
            {
                return int.MaxValue;
            }

            return int.TryParse(match.Groups[1].Value, out int value) ? value : int.MaxValue;
        }

        private static void SetClipLooping(AnimationClip clip, bool loop)
        {
            SerializedObject serializedClip = new SerializedObject(clip);
            SerializedProperty settings = serializedClip.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                SerializedProperty loopTime = settings.FindPropertyRelative("m_LoopTime");
                if (loopTime != null)
                {
                    loopTime.boolValue = loop;
                    serializedClip.ApplyModifiedPropertiesWithoutUndo();
                }
            }
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
