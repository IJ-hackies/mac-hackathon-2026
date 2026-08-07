using System.Collections.Generic;
using System.IO;
using System.Linq;
using Player;
using Player.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlayerEditor
{
    public static class PlayerSceneSetup
    {
        private const string ModelPath = "Assets/Art/Models/Characters/Astronaut_FinnTheFrog.fbx";
        private const string AstronautMaterialPath = "Assets/Art/Materials/M_Astronaut.mat";
        private const string GroundMaterialPath = "Assets/Art/Materials/M_Ground.mat";
        private const string ProjectileMaterialPath = "Assets/Art/Materials/M_Projectile.mat";
        private const string ControllerPath = "Assets/Art/Animations/AC_Player.controller";
        private const string ScenePath = "Assets/Scenes/Player.unity";
        private const string PrefabPath = "Assets/Art/Models/Characters/Player.prefab";
        private const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";
        private const string PlayerLayerName = "Player";

        [MenuItem("Tools/Player Prototype/Build Test Scene")]
        public static void BuildTestScene()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"PlayerSceneSetup: no model found at {ModelPath}. " +
                                "Make sure the FBX has been imported by Unity first.");
                return;
            }

            ConfigureAnimationLooping(model);

            int playerLayer = EnsurePlayerLayer();
            AnimatorController controller = BuildAnimatorController(model);
            GameObject projectilePrefab = BuildProjectilePrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            CreateGround();

            GameObject player = BuildPlayer(model, controller, playerLayer);
            var (cameraController, mainCamera) = BuildCamera(player, playerLayer);
            var (wheelUi, crosshairUi) = BuildUI();

            Animator animator = player.GetComponentInChildren<Animator>();
            var sourceClips = LoadSourceClips(model, out string modelPath);
            AnimationClip wave = GetClip(sourceClips, modelPath, "Wave");
            AnimationClip yes = GetClip(sourceClips, modelPath, "Yes");
            AnimationClip no = GetClip(sourceClips, modelPath, "No");

            BuildCombatAndEmotes(player, animator, projectilePrefab, mainCamera, cameraController,
                wheelUi, crosshairUi, wave, yes, no, playerLayer);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EnsureFolder("Assets/Art/Models/Characters");
            PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);

            AssetDatabase.SaveAssets();
            Debug.Log("PlayerSceneSetup: built " + ScenePath + " and " + PrefabPath);
        }

        private static readonly string[] LoopingClipShortNames = { "Idle", "Walk", "Run", "Jump_Idle" };

        private static string ShortClipName(string fullName)
        {
            int pipe = fullName.LastIndexOf('|');
            return pipe >= 0 ? fullName.Substring(pipe + 1) : fullName;
        }

        private static bool ShouldLoop(string fullClipName)
        {
            string shortName = ShortClipName(fullClipName);
            foreach (var loopingName in LoopingClipShortNames)
            {
                if (string.Equals(shortName, loopingName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// Forces "Loop Time" on the locomotion clips (Idle/Walk/Run/Jump_Idle) via the FBX's
        /// ModelImporter, since Blender-exported clips import as non-looping by default and
        /// otherwise this requires manually ticking checkboxes per clip in the Inspector.
        private static void ConfigureAnimationLooping(GameObject model)
        {
            string modelPath = AssetDatabase.GetAssetPath(model);
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) return;

            var sourceClips = LoadSourceClips(model, out _);

            var existing = importer.clipAnimations;
            bool alreadyExplicit = existing != null && existing.Length > 0;

            var entries = new List<ModelImporterClipAnimation>();
            bool changed = false;

            foreach (var clip in sourceClips)
            {
                ModelImporterClipAnimation entry = alreadyExplicit
                    ? System.Array.Find(existing, e => e.takeName == clip.name || e.name == clip.name)
                    : null;

                bool wantLoop = ShouldLoop(clip.name);

                if (entry == null)
                {
                    entry = new ModelImporterClipAnimation
                    {
                        name = clip.name,
                        takeName = clip.name,
                        firstFrame = 0,
                        lastFrame = clip.length * clip.frameRate,
                    };
                    changed = true;
                }

                if (entry.loopTime != wantLoop)
                {
                    entry.loopTime = wantLoop;
                    changed = true;
                }

                entries.Add(entry);
            }

            if (changed)
            {
                importer.clipAnimations = entries.ToArray();
                importer.SaveAndReimport();
            }
        }

        private static int EnsurePlayerLayer()
        {
            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            var tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == PlayerLayerName)
                {
                    return i;
                }
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = PlayerLayerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }

            Debug.LogWarning("PlayerSceneSetup: no free layer slot for a \"Player\" layer; using Default (0).");
            return 0;
        }

        // Blender exports take names as "<ArmatureName>|<ActionName>" (e.g.
        // "CharacterArmature|Idle"); Unity keeps that full string as the clip name.
        // Match by exact name first, then by the suffix after the last '|'.
        private static AnimationClip GetClip(List<AnimationClip> sourceClips, string modelPath, string clipName)
        {
            var exact = sourceClips.FirstOrDefault(c =>
                string.Equals(c.name.Trim(), clipName, System.StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var bySuffix = sourceClips.FirstOrDefault(c =>
                c.name.Trim().EndsWith("|" + clipName, System.StringComparison.OrdinalIgnoreCase));
            if (bySuffix != null) return bySuffix;

            // Some takes come through with stray whitespace/suffixes from the FBX's binary
            // data (seen on a couple of clips in this pack); fall back to comparing the
            // trimmed short name after the last '|' rather than an exact substring match.
            var loose = sourceClips.FirstOrDefault(c =>
                string.Equals(ShortClipName(c.name).Trim(), clipName, System.StringComparison.OrdinalIgnoreCase));
            if (loose != null) return loose;

            Debug.LogWarning($"PlayerSceneSetup: no animation clip matching \"{clipName}\" " +
                              $"found on {modelPath}. Found clips: " +
                              string.Join(", ", sourceClips.Select(c => c.name)));
            return null;
        }

        private static List<AnimationClip> LoadSourceClips(GameObject model, out string modelPath)
        {
            modelPath = AssetDatabase.GetAssetPath(model);
            return AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToList();
        }

        private static AnimatorController BuildAnimatorController(GameObject model)
        {
            EnsureFolder("Assets/Art/Animations");

            // Always rebuild: regenerating from a stale/broken controller (e.g. one built
            // before a clip-name matching fix) should not require manually deleting the asset.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            var sourceClips = LoadSourceClips(model, out string modelPath);
            AnimationClip Get(string clipName) => GetClip(sourceClips, modelPath, clipName);

            AnimationClip idle = Get("Idle");
            AnimationClip walk = Get("Walk");
            AnimationClip run = Get("Run");
            AnimationClip jump = Get("Jump");
            AnimationClip jumpIdle = Get("Jump_Idle");
            AnimationClip jumpLand = Get("Jump_Land");
            AnimationClip punch = Get("Punch");
            AnimationClip runGunShoot = Get("Run_Gun_Shoot");
            AnimationClip wave = Get("Wave");
            AnimationClip yes = Get("Yes");
            AnimationClip no = Get("No");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Melee", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Emoting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("EmoteIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("PlayEmote", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (var childState in sm.states.ToList())
            {
                sm.RemoveState(childState.state);
            }

            var idleState = sm.AddState("Idle");
            idleState.motion = idle;

            var moveTree = new BlendTree
            {
                name = "Move",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(moveTree, controller);
            if (walk != null) moveTree.AddChild(walk, 0.5f);
            if (run != null) moveTree.AddChild(run, 1f);

            var moveState = sm.AddState("Move");
            moveState.motion = moveTree;

            var jumpState = sm.AddState("Jump");
            jumpState.motion = jump;

            var fallState = sm.AddState("Fall");
            fallState.motion = jumpIdle;

            var landState = sm.AddState("Land");
            landState.motion = jumpLand;

            var meleeState = sm.AddState("Melee");
            meleeState.motion = punch;

            var shootState = sm.AddState("Shoot");
            shootState.motion = runGunShoot;

            var emoteWaveState = sm.AddState("Emote_Wave");
            emoteWaveState.motion = wave;

            var emoteYesState = sm.AddState("Emote_Yes");
            emoteYesState.motion = yes;

            var emoteNoState = sm.AddState("Emote_No");
            emoteNoState.motion = no;

            sm.defaultState = idleState;

            var idleToMove = idleState.AddTransition(moveState);
            idleToMove.hasExitTime = false;
            idleToMove.duration = 0.15f;
            idleToMove.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

            var moveToIdle = moveState.AddTransition(idleState);
            moveToIdle.hasExitTime = false;
            moveToIdle.duration = 0.15f;
            moveToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            // Standing jump: play the takeoff pose. Running jump: the pack has no dedicated
            // running-jump clip, so skip straight to the falling loop instead of showing the
            // standing takeoff pose over running legs.
            var anyToJumpStanding = sm.AddAnyStateTransition(jumpState);
            anyToJumpStanding.canTransitionToSelf = false;
            anyToJumpStanding.hasExitTime = false;
            anyToJumpStanding.duration = 0.05f;
            anyToJumpStanding.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            anyToJumpStanding.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var anyToJumpMoving = sm.AddAnyStateTransition(fallState);
            anyToJumpMoving.canTransitionToSelf = false;
            anyToJumpMoving.hasExitTime = false;
            anyToJumpMoving.duration = 0.05f;
            anyToJumpMoving.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            anyToJumpMoving.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var jumpToFall = jumpState.AddTransition(fallState);
            jumpToFall.hasExitTime = true;
            jumpToFall.exitTime = 0.8f;
            jumpToFall.duration = 0.1f;

            var fallToLand = fallState.AddTransition(landState);
            fallToLand.hasExitTime = false;
            fallToLand.duration = 0.05f;
            fallToLand.AddCondition(AnimatorConditionMode.If, 0, "Grounded");

            var landToIdle = landState.AddTransition(idleState);
            landToIdle.hasExitTime = true;
            landToIdle.exitTime = 0.9f;
            landToIdle.duration = 0.15f;

            // Melee: one-shot from any state, back to Idle when it finishes.
            var anyToMelee = sm.AddAnyStateTransition(meleeState);
            anyToMelee.canTransitionToSelf = false;
            anyToMelee.hasExitTime = false;
            anyToMelee.duration = 0.05f;
            anyToMelee.AddCondition(AnimatorConditionMode.If, 0, "Melee");

            var meleeToIdle = meleeState.AddTransition(idleState);
            meleeToIdle.hasExitTime = true;
            meleeToIdle.exitTime = 0.9f;
            meleeToIdle.duration = 0.1f;

            // Shoot: one-shot from any state (only clip available is the running-gun pose;
            // reused regardless of locomotion state), back to Idle when it finishes.
            var anyToShoot = sm.AddAnyStateTransition(shootState);
            anyToShoot.canTransitionToSelf = false;
            anyToShoot.hasExitTime = false;
            anyToShoot.duration = 0.05f;
            anyToShoot.AddCondition(AnimatorConditionMode.If, 0, "Fire");

            var shootToIdle = shootState.AddTransition(idleState);
            shootToIdle.hasExitTime = true;
            shootToIdle.exitTime = 0.9f;
            shootToIdle.duration = 0.1f;

            // Emotes: entry is gated by the PlayEmote trigger (+ EmoteIndex), not the held
            // Emoting bool. A trigger auto-consumes after one use, same as Jump/Melee/Fire, so
            // it can't re-satisfy an AnyState condition on the following frame and restart the
            // clip from frame 0 — which is what happened when entry was gated by Emoting alone
            // (a bool that stays true for the whole action), even with canTransitionToSelf off.
            // Emoting is still used, but only for the early-interrupt exit below.
            var emoteStates = new[] { emoteWaveState, emoteYesState, emoteNoState };
            for (int i = 0; i < emoteStates.Length; i++)
            {
                var anyToEmote = sm.AddAnyStateTransition(emoteStates[i]);
                anyToEmote.canTransitionToSelf = false;
                anyToEmote.hasExitTime = false;
                anyToEmote.duration = 0.15f;
                anyToEmote.AddCondition(AnimatorConditionMode.If, 0, "PlayEmote");
                anyToEmote.AddCondition(AnimatorConditionMode.Equals, i, "EmoteIndex");

                var emoteFinish = emoteStates[i].AddTransition(idleState);
                emoteFinish.hasExitTime = true;
                emoteFinish.exitTime = 0.95f;
                emoteFinish.duration = 0.15f;

                var emoteInterrupt = emoteStates[i].AddTransition(idleState);
                emoteInterrupt.hasExitTime = false;
                emoteInterrupt.duration = 0.1f;
                emoteInterrupt.AddCondition(AnimatorConditionMode.IfNot, 0, "Emoting");
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void CreateLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f);

            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (material != null)
            {
                ground.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static GameObject BuildPlayer(GameObject model, AnimatorController controller, int playerLayer)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AstronautMaterialPath);

            var root = new GameObject("Player");
            root.layer = playerLayer;
            root.transform.position = Vector3.zero;

            var characterController = root.AddComponent<CharacterController>();
            characterController.center = new Vector3(0f, 1f, 0f);
            characterController.height = 2f;
            characterController.radius = 0.35f;

            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            foreach (Transform child in modelInstance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = playerLayer;
            }

            if (material != null)
            {
                foreach (var renderer in modelInstance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = material;
                }
            }

            var animator = modelInstance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = modelInstance.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            root.AddComponent<PlayerController>();

            var relay = root.AddComponent<PlayerAnimatorRelay>();
            var relaySo = new SerializedObject(relay);
            relaySo.FindProperty("animator").objectReferenceValue = animator;
            relaySo.ApplyModifiedProperties();

            return root;
        }

        private static (ThirdPersonCameraController controller, Camera camera) BuildCamera(GameObject player, int playerLayer)
        {
            var pivot = new GameObject("CameraPivot");
            pivot.transform.position = player.transform.position + new Vector3(0f, 1.6f, 0f);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(pivot.transform);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -7f);
            cameraGo.transform.localRotation = Quaternion.identity;
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();
            cameraGo.tag = "MainCamera";

            var cameraController = pivot.AddComponent<ThirdPersonCameraController>();
            var so = new SerializedObject(cameraController);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.FindProperty("cameraTransform").objectReferenceValue = cameraGo.transform;
            so.FindProperty("collisionMask").intValue = ~(1 << playerLayer);
            so.ApplyModifiedProperties();

            return (cameraController, camera);
        }

        private static GameObject BuildProjectilePrefab()
        {
            EnsureFolder("Assets/Prefabs");

            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temp.name = "Projectile";
            temp.transform.localScale = Vector3.one * 0.25f;

            Object.DestroyImmediate(temp.GetComponent<SphereCollider>());
            var trigger = temp.AddComponent<SphereCollider>();
            trigger.isTrigger = true;

            temp.AddComponent<Projectile>();

            var material = AssetDatabase.LoadAssetAtPath<Material>(ProjectileMaterialPath);
            if (material != null)
            {
                temp.GetComponent<Renderer>().sharedMaterial = material;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, ProjectilePrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static void BuildCombatAndEmotes(
            GameObject player,
            Animator animator,
            GameObject projectilePrefab,
            Camera aimCamera,
            ThirdPersonCameraController cameraController,
            EmoteWheelUI wheelUi,
            CrosshairUI crosshairUi,
            AnimationClip waveClip,
            AnimationClip yesClip,
            AnimationClip noClip,
            int playerLayer)
        {
            // Pushed well forward of the body: the astronaut mesh is a rounded silhouette
            // wider than the CharacterController's collider radius (0.35), so a muzzle point
            // just outside the collider still rendered the flash on/inside the character mesh.
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(player.transform, false);
            muzzle.localPosition = new Vector3(0.952f, 1.2f, 1.602f);

            var characterController = player.GetComponent<CharacterController>();
            var playerController = player.GetComponent<PlayerController>();

            var combat = player.AddComponent<PlayerCombat>();
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("animator").objectReferenceValue = animator;
            combatSo.FindProperty("muzzle").objectReferenceValue = muzzle;
            combatSo.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            combatSo.FindProperty("aimCamera").objectReferenceValue = aimCamera;
            combatSo.FindProperty("ownCollider").objectReferenceValue = characterController;
            combatSo.FindProperty("aimMask").intValue = ~(1 << playerLayer);
            combatSo.ApplyModifiedProperties();

            var emotes = player.AddComponent<PlayerEmoteController>();
            var emotesSo = new SerializedObject(emotes);
            emotesSo.FindProperty("animator").objectReferenceValue = animator;
            emotesSo.FindProperty("playerController").objectReferenceValue = playerController;
            emotesSo.FindProperty("playerCombat").objectReferenceValue = combat;
            emotesSo.FindProperty("cameraController").objectReferenceValue = cameraController;
            emotesSo.FindProperty("wheelUi").objectReferenceValue = wheelUi;
            emotesSo.FindProperty("crosshairUi").objectReferenceValue = crosshairUi;
            emotesSo.FindProperty("waveClip").objectReferenceValue = waveClip;
            emotesSo.FindProperty("yesClip").objectReferenceValue = yesClip;
            emotesSo.FindProperty("noClip").objectReferenceValue = noClip;
            emotesSo.ApplyModifiedProperties();
        }

        private static (EmoteWheelUI wheelUi, CrosshairUI crosshairUi) BuildUI()
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var crosshairUi = BuildCrosshair(canvasGo.transform);
            var wheelUi = BuildEmoteWheel(canvasGo.transform);

            return (wheelUi, crosshairUi);
        }

        private static CrosshairUI BuildCrosshair(Transform parent)
        {
            var root = CreateUiRect("Crosshair", parent, new Vector2(24f, 24f), Vector2.zero);
            var canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            var crosshairUi = root.gameObject.AddComponent<CrosshairUI>();

            var horizontal = CreateUiRect("Horizontal", root, new Vector2(14f, 2f), Vector2.zero);
            horizontal.gameObject.AddComponent<Image>().color = Color.white;

            var vertical = CreateUiRect("Vertical", root, new Vector2(2f, 14f), Vector2.zero);
            vertical.gameObject.AddComponent<Image>().color = Color.white;

            var so = new SerializedObject(crosshairUi);
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.ApplyModifiedProperties();

            return crosshairUi;
        }

        private const string WheelRingTexturePath = "Assets/Art/Textures/T_UIWheelRing.asset";

        /// Procedurally generates a ring (annulus) texture/sprite so the wheel's wedges can use
        /// Image.Type.Filled + Radial360 to render as a true hollow-center donut (like the
        /// Fortnite emote wheel) rather than a solid pie or floating icon buttons. Saved as a
        /// persistent asset so the sprite reference survives editor reloads.
        private static Sprite GetOrCreateWheelRingSprite()
        {
            var existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WheelRingTexturePath);
            if (existingSprite != null) return existingSprite;

            EnsureFolder("Assets/Art/Textures");

            const int size = 128;
            const float innerRadiusRatio = 0.5f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "T_UIWheelRing",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var center = new Vector2(size * 0.5f, size * 0.5f);
            float outerRadius = size * 0.5f;
            float innerRadius = outerRadius * innerRadiusRatio;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    byte alpha = (byte)(dist >= innerRadius && dist <= outerRadius ? 255 : 0);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "T_UIWheelRing";

            AssetDatabase.CreateAsset(texture, WheelRingTexturePath);
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.ImportAsset(WheelRingTexturePath);

            return AssetDatabase.LoadAssetAtPath<Sprite>(WheelRingTexturePath);
        }

        private static EmoteWheelUI BuildEmoteWheel(Transform parent)
        {
            const float wheelSize = 340f;
            var root = CreateUiRect("EmoteWheel", parent, new Vector2(wheelSize, wheelSize), Vector2.zero);
            var wheelUi = root.gameObject.AddComponent<EmoteWheelUI>();

            Sprite ringSprite = GetOrCreateWheelRingSprite();

            var backdropRect = CreateUiRect("Backdrop", root, new Vector2(wheelSize, wheelSize), Vector2.zero);
            var backdrop = backdropRect.gameObject.AddComponent<Image>();
            backdrop.sprite = ringSprite;
            backdrop.type = Image.Type.Filled;
            backdrop.fillMethod = Image.FillMethod.Radial360;
            backdrop.fillOrigin = (int)Image.Origin360.Top;
            backdrop.fillAmount = 1f;
            backdrop.color = new Color(0f, 0f, 0f, 0.55f);
            backdrop.raycastTarget = false;

            // Angle 0 = top, increasing clockwise, matching EmoteWheelUI's hover-angle math.
            string[] labels = { "Wave", "Yes", "No" };
            var slices = new Image[labels.Length];
            float sliceDegrees = 360f / labels.Length;
            const float labelRadius = 130f;

            for (int i = 0; i < labels.Length; i++)
            {
                var sliceRect = CreateUiRect($"Slice_{labels[i]}", root, new Vector2(wheelSize, wheelSize), Vector2.zero);
                sliceRect.localRotation = Quaternion.Euler(0f, 0f, -(i * sliceDegrees));

                var slice = sliceRect.gameObject.AddComponent<Image>();
                slice.sprite = ringSprite;
                slice.type = Image.Type.Filled;
                slice.fillMethod = Image.FillMethod.Radial360;
                slice.fillOrigin = (int)Image.Origin360.Top;
                slice.fillClockwise = true;
                slice.fillAmount = 1f / labels.Length;
                slice.raycastTarget = false;
                slices[i] = slice;

                float midAngle = (i + 0.5f) * sliceDegrees * Mathf.Deg2Rad;
                var labelPos = new Vector2(Mathf.Sin(midAngle) * labelRadius, Mathf.Cos(midAngle) * labelRadius);
                var labelRect = CreateUiRect($"Label_{labels[i]}", root, new Vector2(100f, 30f), labelPos);

                var text = labelRect.gameObject.AddComponent<Text>();
                text.text = labels[i];
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.fontSize = 16;
                text.fontStyle = FontStyle.Bold;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.raycastTarget = false;
            }

            var so = new SerializedObject(wheelUi);
            so.FindProperty("root").objectReferenceValue = root;
            var slicesProp = so.FindProperty("slices");
            slicesProp.arraySize = slices.Length;
            for (int i = 0; i < slices.Length; i++)
            {
                slicesProp.GetArrayElementAtIndex(i).objectReferenceValue = slices[i];
            }
            so.ApplyModifiedProperties();

            root.gameObject.SetActive(false);

            return wheelUi;
        }

        private static RectTransform CreateUiRect(string name, Transform parent, Vector2 size, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            return rect;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
