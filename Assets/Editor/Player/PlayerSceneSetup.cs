using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterEditor;
using Combat;
using Gameplay.Areas;
using Player;
using Player.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlayerEditor
{
    public static class PlayerSceneSetup
    {
        private const string ModelPath = "Assets/Art/Models/Characters/Astronaut_FinnTheFrog.fbx";
        private const string AstronautMaterialPath = "Assets/Art/Materials/M_Astronaut.mat";
        private const string GroundMaterialPath = "Assets/Art/Materials/M_Ground.mat";
        private const string ControllerPath = "Assets/Art/Animations/AC_Player.controller";
        private const string UpperBodyMaskPath = "Assets/Art/Animations/AM_UpperBody.mask";
        private const string ScenePath = "Assets/Scenes/Player.unity";
        private const string PrefabPath = "Assets/Art/Models/Characters/Player.prefab";
        private const string PlayerRigPrefabPath = "Assets/Prefabs/PlayerRig.prefab";
        private const string HealthBarTrackPath =
            "Assets/Art/Textures/UI/Health/SpaceExpansion_BarTrack_Grey.png";
        private const string HealthBarFillPath =
            "Assets/Art/Textures/UI/Health/SpaceExpansion_BarFill_Gloss.png";
        private const string HudUtilityFontPath = "Assets/Art/Fonts/UI/KenneyFutureNarrow.ttf";
        private const string PlayerLayerName = "Player";
        private const string EnemyLayerName = "Enemy";

        private const string LanaVfxFolder = "Assets/Lana Studio/Casual RPG VFX/Prefabs/";
        private const string PlayerProjectileVisualPath = LanaVfxFolder + "Range_attack/Projectiles_dark_magic.prefab";
        private const string PlayerProjectileImpactPath = LanaVfxFolder + "Range_attack/Hit_dark_magic.prefab";
        private const string MeleeHitEffectPath = LanaVfxFolder + "Slash/Hit_stone.prefab";
        private const string StunVfxPath = LanaVfxFolder + "States/Stun.prefab";

        // Ultimate (Mech mode) - electric machine guns, lightning-circle secondary, base-player
        // beam-dot secondary, and the shield.
        private const string VendorMechModelPath =
            "asset packs/visuals/Ultimate Space Kit - March 2023/Characters/FBX/Mech_FinnTheFrog.fbx";
        private const string MechModelPath = "Assets/Art/Models/Characters/Mech_FinnTheFrog.fbx";
        // Shared by PlayerCombat.aimViewportY and BuildCrosshair - moved up from dead-center
        // (0.5) so the crosshair/actual aim point open up more visible ground ahead instead of
        // landing on the character/mech's own body. Keep these two wiring points in sync.
        private const float CrosshairViewportY = 0.62f;
        private const string MechMaterialPath = "Assets/Art/Materials/M_MechFinnTheFrog.mat";
        private const string SpacePaletteTexturePath = "Assets/Art/Textures/T_SpacePalette.png";
        private const string MechControllerPath = "Assets/Art/Animations/AC_PlayerMech.controller";
        private const string MechUpperBodyMaskPath = "Assets/Art/Animations/AM_MechUpperBody.mask";
        private const string ElectricProjectilePath = LanaVfxFolder + "Range_attack/Projectiles_electric.prefab";
        private const string ElectricImpactPath = LanaVfxFolder + "Range_attack/Hit_electric.prefab";
        private const string TopDownBeamDotPurplePath = LanaVfxFolder + "Top_down_attack/top_down_beam_dot_purple.prefab";
        private const string TopDownLightningCircleBluePath = LanaVfxFolder + "Top_down_attack/top_down_lightning_circle_blue.prefab";
        private const string ShieldElectricPath = LanaVfxFolder + "Shields/Shield_electric.prefab";
        private const string DashVfxPath = LanaVfxFolder + "Burst/Poof_electric.prefab";

        // Walking plays Run_Gun_Shoot slowed (Arms_Shoot_Walk below) so its swing doesn't read as
        // an exaggerated wave against the much slower leg cycle; sprinting keeps it at full speed
        // (Arms_Shoot_Run). PlayerCombat.CheckShootBeat reads this state's playback live off the
        // Animator (AnimatorStateInfo.normalizedTime), so walking-while-shooting fires slower
        // than running-while-shooting purely as a side effect of this speed difference, not a
        // separately tuned rate.
        private const float WalkShootAnimSpeed = 0.6f;

        // Historical: PlayerController used to normalize Speed against sprintSpeed with a
        // separate, slower walkSpeed tier (walkSpeed / sprintSpeed =~ 0.538), so a walking player
        // stayed below this threshold and only sprinting crossed it - used to tell walking-while-
        // shooting (slowed arm swing) apart from sprinting-while-shooting (full speed) on the
        // Arms layer. The walk/sprint split was removed (single moveSpeed, "the character
        // doesn't even walk") - keyboard input now jumps straight to normalizedSpeed 1 the
        // instant the player moves, so Arms_Shoot_Walk/this threshold in practice only matters
        // for analog gamepad stick input at partial magnitude. Left in place rather than ripped
        // out, since the Arms_Shoot_Walk clip/states still exist and still work correctly for
        // that case.
        private const float SprintSpeedThreshold = 0.55f;

        // Bone-name fragments (case-insensitive substring match) used to build the upper-body
        // AvatarMask below - anything matching stays driven by the base locomotion layer instead
        // of the Shoot overlay layer, so legs keep running normally while only the arms react to
        // firing. The rig (CharacterArmature) is a Generic avatar, not Humanoid, so this has to
        // be done by transform name rather than AvatarMaskBodyPart.
        private static readonly string[] LowerBodyBoneNameFragments =
        {
            "leg", "foot", "toe", "hip", "pelvis", "thigh", "shin", "calf",
        };

        private static readonly string[] LoopingClipShortNames =
        {
            "Idle_Gun", "Walk_Gun", "Run_Gun", "Jump_Idle", "Idle_Shoot", "Run_Gun_Shoot", "Jump_Shoot",
        };

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

            ModelAnimationUtility.ConfigureAnimationLooping(model, LoopingClipShortNames);

            int playerLayer = ModelAnimationUtility.EnsureLayer(PlayerLayerName);
            // Ensured here (even though no enemies exist yet at this point) purely so its layer
            // index is stable and known for the camera's collision mask below - EnemySceneSetup
            // ensures the same layer again (idempotent) and assigns it to enemy models.
            int enemyLayer = ModelAnimationUtility.EnsureLayer(EnemyLayerName);
            AnimatorController controller = BuildAnimatorController(model);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            CreateGround();

            GameObject player = BuildPlayer(model, controller, playerLayer);
            var (cameraController, mainCamera) = BuildCamera(player, playerLayer, enemyLayer);
            var (wheelUi, crosshairUi, healthHudUi, abilityHudUi, ultimateHudUi, ammoHudUi, hudCanvasGo) = BuildUI();

            Animator animator = player.GetComponentInChildren<Animator>();
            var sourceClips = ModelAnimationUtility.LoadSourceClips(model, out string modelPath);
            AnimationClip wave = ModelAnimationUtility.GetClip(sourceClips, modelPath, "Wave");
            AnimationClip yes = ModelAnimationUtility.GetClip(sourceClips, modelPath, "Yes");
            AnimationClip no = ModelAnimationUtility.GetClip(sourceClips, modelPath, "No");
            BuildCombatAndEmotes(player, animator, mainCamera, cameraController,
                wheelUi, crosshairUi, wave, yes, no, playerLayer);

            Health playerHealth = player.AddComponent<Health>();
            player.AddComponent<PlayerDeathHandler>();
            PlayerAmmo playerAmmo = player.AddComponent<PlayerAmmo>();

            // Health/PlayerAnimatorRelay/PlayerEmoteController didn't exist yet when BuildUltimate
            // wired PlayerUltimate's other references - finish wiring the animator-swap targets
            // now that all of them exist.
            var ultimateForWiring = player.GetComponent<PlayerUltimate>();
            if (ultimateForWiring != null)
            {
                var ultimateSo2 = new SerializedObject(ultimateForWiring);
                ultimateSo2.FindProperty("playerHealth").objectReferenceValue = playerHealth;
                ultimateSo2.FindProperty("animatorRelay").objectReferenceValue = player.GetComponent<PlayerAnimatorRelay>();
                ultimateSo2.FindProperty("emoteController").objectReferenceValue = player.GetComponent<PlayerEmoteController>();
                ultimateSo2.FindProperty("playerAmmo").objectReferenceValue = playerAmmo;
                ultimateSo2.ApplyModifiedProperties();
            }

            healthHudUi.Bind(playerHealth);
            abilityHudUi.Bind(player.GetComponent<PlayerDash>(), player.GetComponent<PlayerShield>(),
                player.GetComponent<PlayerCombat>(), player.GetComponent<PlayerUltimate>());
            ultimateHudUi.Bind(player.GetComponent<PlayerUltimate>());
            ammoHudUi.Bind(playerAmmo);

            // Groups the player, its camera rig, and its HUD under one object so the whole setup
            // can be saved as a single prefab from the Hierarchy, rather than three separate root
            // objects. worldPositionStays: true - nothing above has moved yet, this is purely a
            // re-parent.
            var playerRig = new GameObject("PlayerRig");
            player.transform.SetParent(playerRig.transform, true);
            cameraController.transform.SetParent(playerRig.transform, true);
            hudCanvasGo.transform.SetParent(playerRig.transform, true);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EnsureFolder("Assets/Art/Models/Characters");
            PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);

            AssetDatabase.SaveAssets();
            Debug.Log("PlayerSceneSetup: built " + ScenePath + " and " + PrefabPath);
        }

        // Re-links Assets/Prefabs/PlayerRig.prefab's nested Player instance to the current
        // Assets/Art/Models/Characters/Player.prefab (rebuilt above) and re-wires the camera/HUD
        // cross-references that Unity's prefab-instance overrides otherwise silently drop when the
        // source prefab's component layout changes (e.g. new PlayerCombat/PlayerEmoteController
        // fields). Kept as a separate, idempotent tool from BuildTestScene since PlayerRig.prefab
        // is the version actually placed in gameplay scenes, not the Player.unity test scene.
        [MenuItem("Tools/Player Prototype/Repair Player Rig Prefab")]
        public static void RepairPlayerRigPrefab()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null)
            {
                throw new System.InvalidOperationException(
                    $"PlayerSceneSetup: no player prefab found at {PrefabPath}.");
            }

            GameObject rigRoot = PrefabUtility.LoadPrefabContents(PlayerRigPrefabPath);
            try
            {
                Transform oldPlayer = RequireDirectChild(rigRoot.transform, "Player");
                Transform cameraPivot = RequireDirectChild(rigRoot.transform, "CameraPivot");
                Transform hudCanvas = RequireDirectChild(rigRoot.transform, "HUD Canvas");

                ThirdPersonCameraController cameraController =
                    RequireComponent<ThirdPersonCameraController>(cameraPivot.gameObject);
                Camera aimCamera = RequireComponentInChildren<Camera>(cameraPivot.gameObject);
                ConfigureCameraRendering(aimCamera);
                EmoteWheelUI wheelUi = RequireComponentInChildren<EmoteWheelUI>(hudCanvas.gameObject);
                CrosshairUI crosshairUi = RequireComponentInChildren<CrosshairUI>(hudCanvas.gameObject);
                HealthHudUI healthHudUi = RequireComponentInChildren<HealthHudUI>(hudCanvas.gameObject);
                Player.UI.AbilityHudUI abilityHudUi =
                    RequireComponentInChildren<Player.UI.AbilityHudUI>(hudCanvas.gameObject);
                Player.UI.UltimateHudUI ultimateHudUi =
                    RequireComponentInChildren<Player.UI.UltimateHudUI>(hudCanvas.gameObject);
                Player.UI.AmmoHudUI ammoHudUi =
                    RequireComponentInChildren<Player.UI.AmmoHudUI>(hudCanvas.gameObject);

                GameObject replacement;
                Object currentPlayerSource =
                    PrefabUtility.GetCorrespondingObjectFromSource(oldPlayer.gameObject);
                bool alreadyLinked = currentPlayerSource == playerPrefab &&
                    AssetDatabase.GetAssetPath(currentPlayerSource) == PrefabPath;
                if (alreadyLinked)
                {
                    replacement = oldPlayer.gameObject;
                }
                else
                {
                    int playerSiblingIndex = oldPlayer.GetSiblingIndex();
                    bool playerWasActive = oldPlayer.gameObject.activeSelf;
                    replacement = (GameObject)PrefabUtility.InstantiatePrefab(
                        playerPrefab,
                        rigRoot.transform);
                    if (replacement == null)
                    {
                        throw new System.InvalidOperationException(
                            $"PlayerSceneSetup: could not instantiate {PrefabPath} inside " +
                            $"{PlayerRigPrefabPath}.");
                    }

                    replacement.name = "Player";
                    replacement.transform.SetSiblingIndex(playerSiblingIndex);
                    replacement.SetActive(playerWasActive);
                    Object.DestroyImmediate(oldPlayer.gameObject);
                }

                replacement.transform.localPosition = Vector3.zero;
                replacement.transform.localRotation = Quaternion.identity;
                replacement.transform.localScale = Vector3.one;

                PlayerController playerController = RequireComponent<PlayerController>(replacement);
                PlayerAreaTracker areaTracker = rigRoot.GetComponent<PlayerAreaTracker>();
                if (areaTracker == null)
                {
                    areaTracker = rigRoot.AddComponent<PlayerAreaTracker>();
                }

                LandingBaseMovementSpeedEffect speedEffect =
                    rigRoot.GetComponent<LandingBaseMovementSpeedEffect>();
                if (speedEffect == null)
                {
                    speedEffect = rigRoot.AddComponent<LandingBaseMovementSpeedEffect>();
                }

                areaTracker.Configure(
                    playerController.transform,
                    System.Array.Empty<GameplayArea>());
                speedEffect.Configure(areaTracker, playerController, 2f);
                PlayerVisualGroundConformer visualConformer =
                    RequireComponent<PlayerVisualGroundConformer>(replacement);
                CapsuleCollider playerCapsule = RequireComponent<CapsuleCollider>(replacement);
                RequireComponent<Rigidbody>(replacement);
                RequireComponent<RadialCapsuleMotor>(replacement);
                PlayerCombat combat = RequireComponent<PlayerCombat>(replacement);
                PlayerDash dash = RequireComponent<PlayerDash>(replacement);
                PlayerShield shield = RequireComponent<PlayerShield>(replacement);
                PlayerUltimate ultimate = RequireComponent<PlayerUltimate>(replacement);
                PlayerAmmo ammo = RequireComponent<PlayerAmmo>(replacement);
                PlayerEmoteController emotes = RequireComponent<PlayerEmoteController>(replacement);
                Transform visualRoot = RequireDirectChild(replacement.transform, "VisualRoot");
                Animator animator = RequireComponentInChildren<Animator>(visualRoot.gameObject);
                Health health = RequireComponent<Health>(replacement);

                SetObjectReference(cameraController, "target", replacement.transform);
                SetObjectReference(playerController, "cameraReference", aimCamera.transform);
                SetObjectReference(playerController, "animator", animator);
                SetObjectReference(visualConformer, "visualRoot", visualRoot);
                SetObjectReference(combat, "aimCamera", aimCamera);
                SetObjectReference(ultimate, "cameraController", cameraController);
                SetObjectReference(emotes, "playerController", playerController);
                SetObjectReference(emotes, "playerCombat", combat);
                SetObjectReference(emotes, "cameraController", cameraController);
                SetObjectReference(emotes, "wheelUi", wheelUi);
                SetObjectReference(emotes, "crosshairUi", crosshairUi);
                healthHudUi.Bind(health);
                abilityHudUi.Bind(dash, shield, combat, ultimate);
                ultimateHudUi.Bind(ultimate);
                ammoHudUi.Bind(ammo);

                rigRoot.transform.localPosition = Vector3.zero;
                rigRoot.transform.localRotation = Quaternion.identity;
                rigRoot.transform.localScale = Vector3.one;

                ValidatePlayerRigContents(rigRoot, playerPrefab);
                if (PrefabUtility.SaveAsPrefabAsset(rigRoot, PlayerRigPrefabPath) == null)
                {
                    throw new System.InvalidOperationException(
                        $"PlayerSceneSetup: failed to save {PlayerRigPrefabPath}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rigRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PlayerRigPrefabPath, ImportAssetOptions.ForceUpdate);

            GameObject validationRoot = PrefabUtility.LoadPrefabContents(PlayerRigPrefabPath);
            try
            {
                ValidatePlayerRigContents(validationRoot, playerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(validationRoot);
            }

            Debug.Log(
                $"PlayerSceneSetup: repaired and validated {PlayerRigPrefabPath} against {PrefabPath}.");
        }

        [MenuItem("Tools/Player Prototype/Refresh Health HUD %#h")]
        public static void RefreshPlayerRigHealthHud()
        {
            GameObject rigRoot = PrefabUtility.LoadPrefabContents(PlayerRigPrefabPath);
            try
            {
                Transform hudCanvas = RequireDirectChild(rigRoot.transform, "HUD Canvas");
                HealthHudUI existingHud =
                    RequireComponentInChildren<HealthHudUI>(hudCanvas.gameObject);
                int siblingIndex = existingHud.transform.GetSiblingIndex();

                Object.DestroyImmediate(existingHud.gameObject);
                HealthHudUI replacementHud = BuildHealthHud(hudCanvas);
                replacementHud.transform.SetSiblingIndex(siblingIndex);
                replacementHud.Bind(RequireComponentInChildren<Health>(rigRoot));

                if (PrefabUtility.SaveAsPrefabAsset(rigRoot, PlayerRigPrefabPath) == null)
                {
                    throw new System.InvalidOperationException(
                        $"PlayerSceneSetup: failed to save {PlayerRigPrefabPath}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rigRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PlayerRigPrefabPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"PlayerSceneSetup: refreshed the Space Expansion health HUD in {PlayerRigPrefabPath}.");
        }

        private static Transform RequireDirectChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null || child.parent != parent)
            {
                throw new System.InvalidOperationException(
                    $"PlayerSceneSetup: '{parent.name}' requires a direct child named '{childName}'.");
            }

            return child;
        }

        private static T RequireComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                throw new System.InvalidOperationException(
                    $"PlayerSceneSetup: '{gameObject.name}' requires {typeof(T).Name}.");
            }

            return component;
        }

        private static T RequireComponentInChildren<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponentInChildren<T>(true);
            if (component == null)
            {
                throw new System.InvalidOperationException(
                    $"PlayerSceneSetup: '{gameObject.name}' requires a child {typeof(T).Name}.");
            }

            return component;
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new System.InvalidOperationException(
                    $"PlayerSceneSetup: {target.GetType().Name} has no serialized '{propertyName}' field.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireObjectReference(
            Object target,
            string propertyName,
            Object expectedValue)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != expectedValue)
            {
                throw new System.InvalidOperationException(
                    $"PlayerSceneSetup: {target.GetType().Name}.{propertyName} is not wired to " +
                    $"'{expectedValue?.name ?? "null"}'.");
            }
        }

        private static Object RequireAssignedObjectReference(Object target, string propertyName)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                throw new System.InvalidOperationException(
                    $"PlayerSceneSetup: {target.GetType().Name}.{propertyName} is unassigned.");
            }

            return property.objectReferenceValue;
        }

        private static void ValidatePlayerRigContents(GameObject rigRoot, GameObject playerPrefab)
        {
            if (rigRoot.transform.localPosition != Vector3.zero ||
                rigRoot.transform.localRotation != Quaternion.identity ||
                rigRoot.transform.localScale != Vector3.one)
            {
                throw new System.InvalidOperationException(
                    "PlayerSceneSetup: PlayerRig root transform must be normalized.");
            }

            Transform player = RequireDirectChild(rigRoot.transform, "Player");
            Transform cameraPivot = RequireDirectChild(rigRoot.transform, "CameraPivot");
            Transform hudCanvas = RequireDirectChild(rigRoot.transform, "HUD Canvas");
            Transform visualRoot = RequireDirectChild(player, "VisualRoot");

            if (player.localPosition != Vector3.zero ||
                player.localRotation != Quaternion.identity ||
                player.localScale != Vector3.one)
            {
                throw new System.InvalidOperationException(
                    "PlayerSceneSetup: nested Player transform must be normalized.");
            }

            Object playerSource = PrefabUtility.GetCorrespondingObjectFromSource(player.gameObject);
            if (playerSource != playerPrefab || AssetDatabase.GetAssetPath(playerSource) != PrefabPath)
            {
                throw new System.InvalidOperationException(
                    $"PlayerSceneSetup: Player child is not linked to {PrefabPath}.");
            }

            PlayerController playerController = RequireComponent<PlayerController>(player.gameObject);
            PlayerAreaTracker areaTracker = RequireComponent<PlayerAreaTracker>(rigRoot);
            LandingBaseMovementSpeedEffect speedEffect =
                RequireComponent<LandingBaseMovementSpeedEffect>(rigRoot);
            PlayerVisualGroundConformer visualConformer =
                RequireComponent<PlayerVisualGroundConformer>(player.gameObject);
            CapsuleCollider playerCapsule = RequireComponent<CapsuleCollider>(player.gameObject);
            Rigidbody playerBody = RequireComponent<Rigidbody>(player.gameObject);
            RequireComponent<RadialCapsuleMotor>(player.gameObject);
            PlayerAnimatorRelay animatorRelay = RequireComponent<PlayerAnimatorRelay>(player.gameObject);
            PlayerCombat combat = RequireComponent<PlayerCombat>(player.gameObject);
            PlayerEmoteController emotes = RequireComponent<PlayerEmoteController>(player.gameObject);
            Health health = RequireComponent<Health>(player.gameObject);
            ThirdPersonCameraController cameraController =
                RequireComponent<ThirdPersonCameraController>(cameraPivot.gameObject);
            Camera aimCamera = RequireComponentInChildren<Camera>(cameraPivot.gameObject);
            UniversalAdditionalCameraData cameraData =
                RequireComponent<UniversalAdditionalCameraData>(aimCamera.gameObject);
            EmoteWheelUI wheelUi = RequireComponentInChildren<EmoteWheelUI>(hudCanvas.gameObject);
            CrosshairUI crosshairUi = RequireComponentInChildren<CrosshairUI>(hudCanvas.gameObject);
            HealthHudUI healthHudUi = RequireComponentInChildren<HealthHudUI>(hudCanvas.gameObject);
            Transform muzzle = RequireDirectChild(visualRoot, "Muzzle");
            Animator animator = RequireComponentInChildren<Animator>(visualRoot.gameObject);

            RequireObjectReference(cameraController, "target", player);
            RequireObjectReference(cameraController, "cameraTransform", aimCamera.transform);
            if (areaTracker.TrackedBody != player ||
                areaTracker.Areas.Count != 0 ||
                !areaTracker.DiscoverAreasWhenEmpty)
            {
                throw new System.InvalidOperationException(
                    "PlayerSceneSetup: PlayerRig area tracker must follow the nested Player " +
                    "and discover scene areas.");
            }

            if (speedEffect.AreaTracker != areaTracker ||
                speedEffect.PlayerController != playerController ||
                !Mathf.Approximately(speedEffect.SpeedMultiplier, 2f))
            {
                throw new System.InvalidOperationException(
                    "PlayerSceneSetup: PlayerRig must have one 2x Landing Base speed effect " +
                    "wired to its tracker and nested PlayerController.");
            }

            if (!cameraData.renderShadows ||
                !cameraData.renderPostProcessing ||
                cameraData.antialiasing != AntialiasingMode.FastApproximateAntialiasing)
            {
                throw new System.InvalidOperationException(
                    "PlayerSceneSetup: rig camera must render shadows, post-processing, and FXAA.");
            }
            RequireObjectReference(playerController, "cameraReference", aimCamera.transform);
            if (playerCapsule.direction != 1 || playerCapsule.isTrigger ||
                !playerBody.isKinematic || playerBody.useGravity ||
                playerBody.interpolation != RigidbodyInterpolation.Interpolate ||
                playerBody.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative)
            {
                throw new System.InvalidOperationException(
                    "PlayerSceneSetup: radial capsule physics settings are invalid.");
            }
            RequireObjectReference(playerController, "animator", animator);
            RequireObjectReference(visualConformer, "visualRoot", visualRoot);
            RequireObjectReference(animatorRelay, "animator", animator);
            RequireObjectReference(combat, "animator", animator);
            RequireObjectReference(combat, "muzzle", muzzle);
            RequireObjectReference(combat, "aimCamera", aimCamera);
            RequireObjectReference(emotes, "animator", animator);
            RequireObjectReference(emotes, "playerController", playerController);
            RequireObjectReference(emotes, "playerCombat", combat);
            RequireObjectReference(emotes, "cameraController", cameraController);
            RequireObjectReference(emotes, "wheelUi", wheelUi);
            RequireObjectReference(emotes, "crosshairUi", crosshairUi);
            RequireAssignedObjectReference(emotes, "waveClip");
            RequireAssignedObjectReference(emotes, "yesClip");
            RequireAssignedObjectReference(emotes, "noClip");
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

            var sourceClips = ModelAnimationUtility.LoadSourceClips(model, out string modelPath);
            AnimationClip Get(string clipName) => ModelAnimationUtility.GetClip(sourceClips, modelPath, clipName);

            AnimationClip idle = Get("Idle_Gun");
            AnimationClip walk = Get("Walk_Gun");
            AnimationClip run = Get("Run_Gun");
            AnimationClip jump = Get("Jump");
            AnimationClip jumpIdle = Get("Jump_Idle");
            AnimationClip jumpLand = Get("Jump_Land");
            AnimationClip punch = Get("Punch");
            AnimationClip runGunShoot = Get("Run_Gun_Shoot");
            AnimationClip idleShoot = Get("Idle_Shoot");
            AnimationClip jumpShoot = Get("Jump_Shoot");
            AnimationClip wave = Get("Wave");
            AnimationClip yes = Get("Yes");
            AnimationClip no = Get("No");
            AnimationClip duck = Get("Duck");
            AnimationClip hitReact = Get("HitReact");
            AnimationClip death = Get("Death");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Melee", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("FireStart", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Firing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Emoting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("EmoteIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("PlayEmote", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitReact", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Stagger", AnimatorControllerParameterType.Trigger);

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

            var emoteWaveState = sm.AddState("Emote_Wave");
            emoteWaveState.motion = wave;

            var emoteYesState = sm.AddState("Emote_Yes");
            emoteYesState.motion = yes;

            var emoteNoState = sm.AddState("Emote_No");
            emoteNoState.motion = no;

            var hitReactState = sm.AddState("HitReact");
            hitReactState.motion = hitReact;

            var deathState = sm.AddState("Death");
            deathState.motion = death;

            var duckState = sm.AddState("Duck");
            duckState.motion = duck;

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

            // Landing only plays the stand-still Land pose (feet-planted recovery) when there's
            // no move input at touchdown. Landing while still holding a move direction skips
            // straight into Move instead - playing Land there froze the legs in that stand-still
            // recovery pose for its whole duration despite the character continuing to run,
            // which is what looked like standing still on landing while moving.
            var fallToLand = fallState.AddTransition(landState);
            fallToLand.hasExitTime = false;
            fallToLand.duration = 0.05f;
            fallToLand.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            fallToLand.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var fallToMove = fallState.AddTransition(moveState);
            fallToMove.hasExitTime = false;
            fallToMove.duration = 0.15f;
            fallToMove.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            fallToMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var landToIdle = landState.AddTransition(idleState);
            landToIdle.hasExitTime = true;
            landToIdle.exitTime = 0.9f;
            landToIdle.duration = 0.15f;

            // If move input starts partway through the stand-still Land pose (rather than being
            // held from before touchdown), still break out into Move instead of finishing the
            // whole recovery animation stationary.
            var landToMove = landState.AddTransition(moveState);
            landToMove.hasExitTime = false;
            landToMove.duration = 0.15f;
            landToMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

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

            // HitReact: one-shot overlay from any state, back to Idle when it finishes. Health
            // fires this trigger without waiting on it, so PlayerController/PlayerCombat keep
            // driving movement and input underneath the reaction pose instead of stalling.
            var anyToHitReact = sm.AddAnyStateTransition(hitReactState);
            anyToHitReact.canTransitionToSelf = false;
            anyToHitReact.hasExitTime = false;
            anyToHitReact.duration = 0.05f;
            anyToHitReact.AddCondition(AnimatorConditionMode.If, 0, "HitReact");

            var hitReactToIdle = hitReactState.AddTransition(idleState);
            hitReactToIdle.hasExitTime = true;
            hitReactToIdle.exitTime = 0.9f;
            hitReactToIdle.duration = 0.1f;

            // Death: terminal, no return transition. PlayerDeathHandler disables movement/combat
            // input separately so this doesn't need to gate anything itself.
            var anyToDeath = sm.AddAnyStateTransition(deathState);
            anyToDeath.canTransitionToSelf = false;
            anyToDeath.hasExitTime = false;
            anyToDeath.duration = 0.05f;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");

            // Stagger: one-shot Duck overlay used by boss impact attacks (see BossMechAI's
            // ground-slam) to visually sell "the player cannot move" while PlayerController's own
            // input lock (Stagger()/IsStaggered) does the actual movement gating. Same
            // AnyState-trigger/exitTime pattern as Melee above.
            var anyToDuck = sm.AddAnyStateTransition(duckState);
            anyToDuck.canTransitionToSelf = false;
            anyToDuck.hasExitTime = false;
            anyToDuck.duration = 0.05f;
            anyToDuck.AddCondition(AnimatorConditionMode.If, 0, "Stagger");

            var duckToIdle = duckState.AddTransition(idleState);
            duckToIdle.hasExitTime = true;
            duckToIdle.exitTime = 0.9f;
            duckToIdle.duration = 0.1f;

            BuildArmsLayer(controller, model, idle, idleShoot, runGunShoot, jumpShoot);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // A second, upper-body-masked layer carrying just the Shoot poses, separate from the
        // base layer's full-body locomotion. Originally Shoot/Idle_Shoot/Jump_Shoot lived on the
        // base layer as full-body one-shots, same as Melee - which meant every shot replaced the
        // *entire* pose, legs included, so continuously firing while moving fought the Move blend
        // tree every retrigger (legs stuttering/snapping between the run cycle and the shoot
        // clip's own leg pose). Splitting the arms onto their own Override layer means the base
        // layer keeps driving Move/Idle continuously and untouched while this layer only ever
        // touches upper-body bones (see the mask). PlayerCombat toggles this layer's weight
        // (0 while not firing, 1 while firing) rather than letting it sit at weight 1 all the
        // time - a masked-but-always-on layer would otherwise still fight the base layer's arms
        // during Melee/Emotes/HitReact/Death, all of which stay full-body on the base layer.
        private static void BuildArmsLayer(
            AnimatorController controller,
            GameObject model,
            AnimationClip idleClip,
            AnimationClip idleShootClip,
            AnimationClip runGunShootClip,
            AnimationClip jumpShootClip)
        {
            controller.AddLayer("Arms");
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer armsLayer = layers[layers.Length - 1];
            armsLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            armsLayer.defaultWeight = 0f;
            armsLayer.avatarMask = BuildUpperBodyMask(model);

            AnimatorStateMachine armsSm = armsLayer.stateMachine;

            var armsIdleState = armsSm.AddState("Arms_Idle");
            armsIdleState.motion = idleClip;
            armsSm.defaultState = armsIdleState;

            // Run_Gun_Shoot's arm swing reads as an exaggerated wave at full speed while only
            // walking (much less horizontal motion to sell the same swing amplitude against), so
            // it's slowed just for the walking case - sprinting keeps the clip at full speed,
            // where the same swing reads fine against the faster leg cycle. Two separate states
            // (rather than one state with a runtime speed multiplier) so each can also be
            // targeted directly by the Grounded/Speed-branched entry/exit transitions below.
            var armsShootState = armsSm.AddState("Arms_Shoot_Walk");
            armsShootState.motion = runGunShootClip;
            armsShootState.speed = WalkShootAnimSpeed;

            var armsShootRunState = armsSm.AddState("Arms_Shoot_Run");
            armsShootRunState.motion = runGunShootClip;

            var armsIdleShootState = armsSm.AddState("Arms_Idle_Shoot");
            armsIdleShootState.motion = idleShootClip;

            var armsJumpShootState = armsSm.AddState("Arms_Jump_Shoot");
            armsJumpShootState.motion = jumpShootClip;

            // Entry is gated by a one-shot FireStart trigger (+ Grounded/Speed for which pose),
            // not by the Firing bool directly - same reasoning as the emote wheel above: gating
            // AnyState entry on a bool that stays true for the whole action causes it to keep
            // re-satisfying and restart the clip from frame 0 every frame. FireStart only fires
            // once per firing session (PlayerCombat.OnFireStarted), not once per shot - the clips
            // themselves now loop (see LoopingClipShortNames), so holding Fire plays one smooth,
            // continuous recoil/fire cycle instead of retriggering (and re-blending) the same
            // one-shot clip from scratch on every damage tick, which is what was reading as
            // flicker at a fast fireCooldown. The discrete hitscan/damage/muzzle-flash rate in
            // PlayerCombat is unaffected - only the arm's visual retrigger was decoupled from it.
            var armsAnyToIdleShoot = armsSm.AddAnyStateTransition(armsIdleShootState);
            armsAnyToIdleShoot.canTransitionToSelf = false;
            armsAnyToIdleShoot.hasExitTime = false;
            armsAnyToIdleShoot.duration = 0.15f;
            armsAnyToIdleShoot.AddCondition(AnimatorConditionMode.If, 0, "FireStart");
            armsAnyToIdleShoot.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            armsAnyToIdleShoot.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var armsAnyToShoot = armsSm.AddAnyStateTransition(armsShootState);
            armsAnyToShoot.canTransitionToSelf = false;
            armsAnyToShoot.hasExitTime = false;
            armsAnyToShoot.duration = 0.15f;
            armsAnyToShoot.AddCondition(AnimatorConditionMode.If, 0, "FireStart");
            armsAnyToShoot.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            armsAnyToShoot.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            armsAnyToShoot.AddCondition(AnimatorConditionMode.Less, SprintSpeedThreshold, "Speed");

            var armsAnyToShootRun = armsSm.AddAnyStateTransition(armsShootRunState);
            armsAnyToShootRun.canTransitionToSelf = false;
            armsAnyToShootRun.hasExitTime = false;
            armsAnyToShootRun.duration = 0.15f;
            armsAnyToShootRun.AddCondition(AnimatorConditionMode.If, 0, "FireStart");
            armsAnyToShootRun.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            armsAnyToShootRun.AddCondition(AnimatorConditionMode.Greater, SprintSpeedThreshold, "Speed");

            var armsAnyToJumpShoot = armsSm.AddAnyStateTransition(armsJumpShootState);
            armsAnyToJumpShoot.canTransitionToSelf = false;
            armsAnyToJumpShoot.hasExitTime = false;
            armsAnyToJumpShoot.duration = 0.15f;
            armsAnyToJumpShoot.AddCondition(AnimatorConditionMode.If, 0, "FireStart");
            armsAnyToJumpShoot.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");

            // While still firing, let the pose follow locomotion context live (e.g. the player
            // starts walking, or breaks into a sprint, partway through a sustained burst) instead
            // of only being decided once at FireStart. Airborne transitions in/out of Jump_Shoot
            // mid-burst are skipped as a rare enough edge case not worth the extra transitions.
            var armsIdleShootToShoot = armsIdleShootState.AddTransition(armsShootState);
            armsIdleShootToShoot.hasExitTime = false;
            armsIdleShootToShoot.duration = 0.15f;
            armsIdleShootToShoot.AddCondition(AnimatorConditionMode.If, 0, "Firing");
            armsIdleShootToShoot.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            armsIdleShootToShoot.AddCondition(AnimatorConditionMode.Less, SprintSpeedThreshold, "Speed");

            var armsIdleShootToShootRun = armsIdleShootState.AddTransition(armsShootRunState);
            armsIdleShootToShootRun.hasExitTime = false;
            armsIdleShootToShootRun.duration = 0.15f;
            armsIdleShootToShootRun.AddCondition(AnimatorConditionMode.If, 0, "Firing");
            armsIdleShootToShootRun.AddCondition(AnimatorConditionMode.Greater, SprintSpeedThreshold, "Speed");

            var armsShootToIdleShoot = armsShootState.AddTransition(armsIdleShootState);
            armsShootToIdleShoot.hasExitTime = false;
            armsShootToIdleShoot.duration = 0.15f;
            armsShootToIdleShoot.AddCondition(AnimatorConditionMode.If, 0, "Firing");
            armsShootToIdleShoot.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var armsShootToShootRun = armsShootState.AddTransition(armsShootRunState);
            armsShootToShootRun.hasExitTime = false;
            armsShootToShootRun.duration = 0.15f;
            armsShootToShootRun.AddCondition(AnimatorConditionMode.If, 0, "Firing");
            armsShootToShootRun.AddCondition(AnimatorConditionMode.Greater, SprintSpeedThreshold, "Speed");

            var armsShootRunToIdleShoot = armsShootRunState.AddTransition(armsIdleShootState);
            armsShootRunToIdleShoot.hasExitTime = false;
            armsShootRunToIdleShoot.duration = 0.15f;
            armsShootRunToIdleShoot.AddCondition(AnimatorConditionMode.If, 0, "Firing");
            armsShootRunToIdleShoot.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var armsShootRunToShoot = armsShootRunState.AddTransition(armsShootState);
            armsShootRunToShoot.hasExitTime = false;
            armsShootRunToShoot.duration = 0.15f;
            armsShootRunToShoot.AddCondition(AnimatorConditionMode.If, 0, "Firing");
            armsShootRunToShoot.AddCondition(AnimatorConditionMode.Less, SprintSpeedThreshold, "Speed");

            // Exit only happens when Firing goes false (PlayerCombat.OnFireCanceled) - the shoot
            // clips loop indefinitely otherwise, so there's no natural "finished" exitTime to key
            // off like the old one-shot states had.
            var armsShootToIdle = armsShootState.AddTransition(armsIdleState);
            armsShootToIdle.hasExitTime = false;
            armsShootToIdle.duration = 0.15f;
            armsShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Firing");

            var armsShootRunToIdle = armsShootRunState.AddTransition(armsIdleState);
            armsShootRunToIdle.hasExitTime = false;
            armsShootRunToIdle.duration = 0.15f;
            armsShootRunToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Firing");

            var armsIdleShootToIdle = armsIdleShootState.AddTransition(armsIdleState);
            armsIdleShootToIdle.hasExitTime = false;
            armsIdleShootToIdle.duration = 0.15f;
            armsIdleShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Firing");

            var armsJumpShootToIdle = armsJumpShootState.AddTransition(armsIdleState);
            armsJumpShootToIdle.hasExitTime = false;
            armsJumpShootToIdle.duration = 0.15f;
            armsJumpShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Firing");

            layers[layers.Length - 1] = armsLayer;
            controller.layers = layers;
        }

        // Mech AnimatorController (Ultimate mode) - deliberately shares the astronaut
        // controller's parameter/state-NAME contract (Speed, Grounded, Jump, Melee, FireStart,
        // Firing, Emoting/EmoteIndex/PlayEmote, Death, an "Arms" layer with an "Arms_Idle" state)
        // so PlayerController/PlayerCombat/PlayerAnimatorRelay/PlayerEmoteController/Health can
        // just retarget which Animator they drive (see each's SetAnimator) on Ultimate activate/
        // end and keep working unmodified - CheckShootBeat's ArmsIdleHash detection in particular
        // relies on "Arms_Idle" hashing the same regardless of which controller it's read from.
        // Simpler than the astronaut's controller: one Walk clip (no run tier), one Shoot_Small
        // loop (no walk/run split), Shoot_Big as a one-shot Arms-layer overlay (not full-body, so
        // legs keep walking/idling under it per spec), no HitReact/Stagger/Duck states (the mech
        // FBX has no matching clips - SetTrigger on an undeclared param is a harmless no-op).
        private static AnimatorController BuildMechAnimatorController(GameObject model)
        {
            EnsureFolder("Assets/Art/Animations");
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(MechControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(MechControllerPath);
            }

            var sourceClips = ModelAnimationUtility.LoadSourceClips(model, out string modelPath);
            AnimationClip Get(string clipName) => ModelAnimationUtility.GetClip(sourceClips, modelPath, clipName);

            AnimationClip idle = Get("Idle");
            AnimationClip walk = Get("Walk");
            AnimationClip jump = Get("Jump");
            AnimationClip kick = Get("Kick");
            AnimationClip death = Get("Death");
            AnimationClip shootSmall = Get("Shoot_Small");
            AnimationClip shootBig = Get("Shoot_Big");
            // The Mech's own take is named "Hello", not "Wave" like the astronaut's (confirmed
            // via the ModelAnimationUtility "no clip matching Wave" warning listing the model's
            // actual clips) - still played through the shared "Emote_Wave" state/EmoteIndex 0.
            AnimationClip wave = Get("Hello");
            AnimationClip yes = Get("Yes");
            AnimationClip no = Get("No");
            AnimationClip dance = Get("Dance");
            // Played for the duration of a stagger (e.g. BossMechAI's own ground-slam landing on
            // the player) in place of the astronaut's "Duck" clip, which the mech doesn't have -
            // "play the pickup animation for the mech for the duration it is stunned."
            AnimationClip pickup = Get("Pickup");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(MechControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Melee", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("FireStart", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Firing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("ShootBig", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Emoting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("EmoteIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("PlayEmote", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            // Same param name/hash PlayerController.Stagger() already fires on the astronaut
            // controller (Animator.StringToHash("Stagger") is a plain string hash, not scoped to
            // a specific controller instance) - declaring it here means Stagger() driving the
            // Mech's animator "just works" with no PlayerController code changes, same as every
            // other shared-name param in this controller.
            controller.AddParameter("Stagger", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (var childState in sm.states.ToList())
            {
                sm.RemoveState(childState.state);
            }

            var idleState = sm.AddState("Idle");
            idleState.motion = idle;

            var moveState = sm.AddState("Move");
            moveState.motion = walk;

            var jumpState = sm.AddState("Jump");
            jumpState.motion = jump;

            var kickState = sm.AddState("Kick");
            kickState.motion = kick;

            var emoteWaveState = sm.AddState("Emote_Wave");
            emoteWaveState.motion = wave;

            var emoteYesState = sm.AddState("Emote_Yes");
            emoteYesState.motion = yes;

            var emoteNoState = sm.AddState("Emote_No");
            emoteNoState.motion = no;

            var emoteDanceState = sm.AddState("Emote_Dance");
            emoteDanceState.motion = dance;

            var deathState = sm.AddState("Death");
            deathState.motion = death;

            var staggerState = sm.AddState("Stagger");
            staggerState.motion = pickup;

            sm.defaultState = idleState;

            var idleToMove = idleState.AddTransition(moveState);
            idleToMove.hasExitTime = false;
            idleToMove.duration = 0.15f;
            idleToMove.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

            var moveToIdle = moveState.AddTransition(idleState);
            moveToIdle.hasExitTime = false;
            moveToIdle.duration = 0.15f;
            moveToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            // No separate fall/land clips (only one "Jump" clip provided) - one-shot from
            // AnyState. Exiting used to be gated purely by exitTime (90% of the Jump clip's own,
            // short, authored length), completely independent of whether the character had
            // actually landed - since PlayerController's jump physics/airtime has nothing to do
            // with the clip's length (especially after jumpHeight x3, which lengthens real
            // airtime further), this made the mech visibly land/stand while still physically
            // airborne. Exit is now gated on the same "Grounded" bool PlayerAnimatorRelay already
            // drives every frame (mirroring the astronaut controller's Fall/Land pattern above),
            // not exit-time - Mecanim holds on the Jump clip's last frame once it finishes playing
            // (it's non-looping) rather than popping back to Idle, which reads fine as a "hang
            // time" airborne pose until Grounded actually goes true.
            var anyToJump = sm.AddAnyStateTransition(jumpState);
            anyToJump.canTransitionToSelf = false;
            anyToJump.hasExitTime = false;
            anyToJump.duration = 0.05f;
            anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            var jumpToIdle = jumpState.AddTransition(idleState);
            jumpToIdle.hasExitTime = false;
            jumpToIdle.duration = 0.15f;
            jumpToIdle.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            jumpToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            var jumpToMove = jumpState.AddTransition(moveState);
            jumpToMove.hasExitTime = false;
            jumpToMove.duration = 0.15f;
            jumpToMove.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            jumpToMove.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

            // Kick (melee): full-body one-shot, same AnyState/exitTime shape as the astronaut's
            // Punch - a melee swing isn't expected to blend with locomotion the way shooting is.
            var anyToKick = sm.AddAnyStateTransition(kickState);
            anyToKick.canTransitionToSelf = false;
            anyToKick.hasExitTime = false;
            anyToKick.duration = 0.05f;
            anyToKick.AddCondition(AnimatorConditionMode.If, 0, "Melee");

            var kickToIdle = kickState.AddTransition(idleState);
            kickToIdle.hasExitTime = true;
            kickToIdle.exitTime = 0.9f;
            kickToIdle.duration = 0.1f;

            // Stagger (e.g. BossMechAI's ground-slam landing on the player): one-shot Pickup
            // overlay, same AnyState/exitTime shape as Kick above. PlayerController.Stagger()
            // already extends its own lock duration to at least the astronaut's Duck clip length
            // when driving that controller; the Mech has no clip named "Duck" so that extension
            // is a no-op here and the lock instead just uses the caller's raw duration - the
            // Pickup clip is short enough that this reads fine without a matching extension.
            var anyToStagger = sm.AddAnyStateTransition(staggerState);
            anyToStagger.canTransitionToSelf = false;
            anyToStagger.hasExitTime = false;
            anyToStagger.duration = 0.05f;
            anyToStagger.AddCondition(AnimatorConditionMode.If, 0, "Stagger");

            var staggerToIdle = staggerState.AddTransition(idleState);
            staggerToIdle.hasExitTime = true;
            staggerToIdle.exitTime = 0.9f;
            staggerToIdle.duration = 0.15f;

            // Emotes: same PlayEmote+EmoteIndex-gated AnyState entry as the astronaut controller
            // (see BuildAnimatorController's comment on why a Trigger, not the held Emoting bool,
            // drives entry). Wave(0)/Yes(1)/No(2) finish naturally (exit-time) or on interrupt;
            // Dance(3) has NO exit-time transition - it only ever leaves via the Emoting-false
            // interrupt below, so it keeps looping (clip itself is also set to loop, see
            // ConfigureAnimationLooping's "Dance" entry in BuildUltimate) until PlayerEmoteController
            // clears Emoting (movement/attack/re-opening the wheel).
            var emoteStates = new[] { emoteWaveState, emoteYesState, emoteNoState, emoteDanceState };
            for (int i = 0; i < emoteStates.Length; i++)
            {
                var anyToEmote = sm.AddAnyStateTransition(emoteStates[i]);
                anyToEmote.canTransitionToSelf = false;
                anyToEmote.hasExitTime = false;
                anyToEmote.duration = 0.15f;
                anyToEmote.AddCondition(AnimatorConditionMode.If, 0, "PlayEmote");
                anyToEmote.AddCondition(AnimatorConditionMode.Equals, i, "EmoteIndex");

                bool isDance = emoteStates[i] == emoteDanceState;
                if (!isDance)
                {
                    var emoteFinish = emoteStates[i].AddTransition(idleState);
                    emoteFinish.hasExitTime = true;
                    emoteFinish.exitTime = 0.95f;
                    emoteFinish.duration = 0.15f;
                }

                var emoteInterrupt = emoteStates[i].AddTransition(idleState);
                emoteInterrupt.hasExitTime = false;
                emoteInterrupt.duration = 0.1f;
                emoteInterrupt.AddCondition(AnimatorConditionMode.IfNot, 0, "Emoting");
            }

            // Death: terminal, no return transition - PlayerDeathHandler disables movement/combat
            // input separately.
            var anyToDeath = sm.AddAnyStateTransition(deathState);
            anyToDeath.canTransitionToSelf = false;
            anyToDeath.hasExitTime = false;
            anyToDeath.duration = 0.05f;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");

            BuildMechArmsLayer(controller, model, idle, shootSmall, shootBig);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // Upper-body-masked Arms layer, same Override/weight-toggle shape as the astronaut's
        // BuildArmsLayer - Shoot_Small loops while Firing is held (entered once via FireStart,
        // exited when Firing goes false, exactly the pattern CheckShootBeat's normalizedTime-
        // crossing beat detection expects - "Arms_Idle" is named identically to the astronaut's
        // so ArmsIdleHash matches on either controller). Shoot_Big is a one-shot AnyState overlay
        // on this SAME layer (not the base layer) specifically so the legs keep playing Idle/Move
        // underneath it while it plays - "top half using shooting animations" even while jumping/
        // moving, per spec.
        private static void BuildMechArmsLayer(
            AnimatorController controller,
            GameObject model,
            AnimationClip idleClip,
            AnimationClip shootSmallClip,
            AnimationClip shootBigClip)
        {
            controller.AddLayer("Arms");
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer armsLayer = layers[layers.Length - 1];
            armsLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            armsLayer.defaultWeight = 0f;
            armsLayer.avatarMask = BuildUpperBodyMask(model, MechUpperBodyMaskPath);

            AnimatorStateMachine armsSm = armsLayer.stateMachine;

            var armsIdleState = armsSm.AddState("Arms_Idle");
            armsIdleState.motion = idleClip;
            armsSm.defaultState = armsIdleState;

            var armsShootState = armsSm.AddState("Arms_Shoot_Small");
            armsShootState.motion = shootSmallClip;

            var armsShootBigState = armsSm.AddState("Arms_Shoot_Big");
            armsShootBigState.motion = shootBigClip;
            // Sped up 1.6x - the lightning-circle cast's actual damage timing (PlayerCombat.
            // ultimateSecondaryTelegraphDelay) is already fast, but the full-length windup clip
            // made the attack read as slower than it actually is.
            armsShootBigState.speed = 1.6f;

            var armsAnyToShoot = armsSm.AddAnyStateTransition(armsShootState);
            armsAnyToShoot.canTransitionToSelf = false;
            armsAnyToShoot.hasExitTime = false;
            armsAnyToShoot.duration = 0.15f;
            armsAnyToShoot.AddCondition(AnimatorConditionMode.If, 0, "FireStart");

            var armsShootToIdle = armsShootState.AddTransition(armsIdleState);
            armsShootToIdle.hasExitTime = false;
            armsShootToIdle.duration = 0.15f;
            armsShootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Firing");

            var armsAnyToShootBig = armsSm.AddAnyStateTransition(armsShootBigState);
            armsAnyToShootBig.canTransitionToSelf = false;
            armsAnyToShootBig.hasExitTime = false;
            armsAnyToShootBig.duration = 0.1f;
            armsAnyToShootBig.AddCondition(AnimatorConditionMode.If, 0, "ShootBig");

            var armsShootBigToIdle = armsShootBigState.AddTransition(armsIdleState);
            armsShootBigToIdle.hasExitTime = true;
            armsShootBigToIdle.exitTime = 0.9f;
            armsShootBigToIdle.duration = 0.15f;

            layers[layers.Length - 1] = armsLayer;
            controller.layers = layers;
        }

        // Builds a transform-path AvatarMask limiting the Arms layer to upper-body bones, so its
        // Override blend only ever replaces arm/hand/spine poses and never touches the legs the
        // base layer is driving. Computed from the actual model hierarchy (by bone-name fragment,
        // see LowerBodyBoneNameFragments) rather than hardcoded paths, since the rig is a Generic
        // avatar (not Humanoid) imported from Blender.
        private static AvatarMask BuildUpperBodyMask(GameObject model)
        {
            return BuildUpperBodyMask(model, UpperBodyMaskPath);
        }

        // Parametrized on assetPath so the Mech's own upper-body mask (MechUpperBodyMaskPath)
        // doesn't overwrite the astronaut's (both call this same bone-name-fragment logic, just
        // against different rigs/asset paths).
        private static AvatarMask BuildUpperBodyMask(GameObject model, string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            var mask = new AvatarMask();
            mask.AddTransformPath(model.transform, true);

            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                string boneName = path.Substring(path.LastIndexOf('/') + 1);
                bool isLowerBody = LowerBodyBoneNameFragments.Any(fragment =>
                    boneName.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0);

                if (isLowerBody)
                {
                    mask.SetTransformActive(i, false);
                }
            }

            AssetDatabase.CreateAsset(mask, assetPath);
            return mask;
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

            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(root.transform, false);
            visualRoot.gameObject.layer = playerLayer;

            var playerCapsule = root.AddComponent<CapsuleCollider>();
            playerCapsule.center = new Vector3(0f, 1.275f, 0f);
            playerCapsule.height = 2.55f;
            playerCapsule.radius = 0.55f;
            playerCapsule.direction = 1;

            var playerBody = root.AddComponent<Rigidbody>();
            playerBody.useGravity = false;
            playerBody.isKinematic = true;
            playerBody.interpolation = RigidbodyInterpolation.Interpolate;
            playerBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            root.AddComponent<RadialCapsuleMotor>();

            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model, visualRoot);
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

            var playerController = root.AddComponent<PlayerController>();
            var playerControllerSo = new SerializedObject(playerController);
            playerControllerSo.FindProperty("animator").objectReferenceValue = animator;
            playerControllerSo.ApplyModifiedProperties();

            var visualConformer = root.AddComponent<PlayerVisualGroundConformer>();
            var visualConformerSo = new SerializedObject(visualConformer);
            visualConformerSo.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            visualConformerSo.ApplyModifiedProperties();

            var relay = root.AddComponent<PlayerAnimatorRelay>();
            var relaySo = new SerializedObject(relay);
            relaySo.FindProperty("animator").objectReferenceValue = animator;
            relaySo.ApplyModifiedProperties();

            return root;
        }

        private static (ThirdPersonCameraController controller, Camera camera) BuildCamera(
            GameObject player, int playerLayer, int enemyLayer)
        {
            var pivot = new GameObject("CameraPivot");
            pivot.transform.position = player.transform.position + new Vector3(0f, 1.6f, 0f);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(pivot.transform);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -7f);
            cameraGo.transform.localRotation = Quaternion.identity;
            var camera = cameraGo.AddComponent<Camera>();
            ConfigureCameraRendering(camera);
            cameraGo.AddComponent<AudioListener>();
            cameraGo.tag = "MainCamera";

            var cameraController = pivot.AddComponent<ThirdPersonCameraController>();
            var so = new SerializedObject(cameraController);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.FindProperty("cameraTransform").objectReferenceValue = cameraGo.transform;
            // Excludes both Player and Enemy from what can push the camera in: collision should
            // only come from static level geometry (ground/walls), not other characters. Without
            // the Enemy exclusion, any enemy standing between the pivot and the desired camera
            // position (including circling around/behind it while swarming) would SphereCast-clip
            // the camera in close, effectively letting enemies block your own view of yourself.
            so.FindProperty("collisionMask").intValue = ~((1 << playerLayer) | (1 << enemyLayer));
            so.ApplyModifiedProperties();

            return (cameraController, camera);
        }

        private static void ConfigureCameraRendering(Camera camera)
        {
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = true;
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            EditorUtility.SetDirty(cameraData);
        }

        private static void BuildCombatAndEmotes(
            GameObject player,
            Animator animator,
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
            // wider than the player capsule's collider radius (0.55), so a muzzle point
            // just outside the collider still rendered the flash on/inside the character mesh.
            Transform visualRoot = RequireDirectChild(player.transform, "VisualRoot");
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(visualRoot, false);
            muzzle.localPosition = new Vector3(0.0448f, 1.707f, 2.648f); // hand-tuned in Editor

            // Above the head, roughly centered - not sourced from a humanoid bone (the rig is
            // Generic, not Humanoid - see BuildArmsLayer), so authored the same way Muzzle is:
            // a hand-picked local offset checked against the model in-editor.
            var headAnchor = new GameObject("HeadAnchor").transform;
            headAnchor.SetParent(visualRoot, false);
            headAnchor.localPosition = new Vector3(0f, 2.15f, 0.1f);

            var playerController = player.GetComponent<PlayerController>();
            var playerControllerSo = new SerializedObject(playerController);
            playerControllerSo.FindProperty("headAnchor").objectReferenceValue = headAnchor;
            playerControllerSo.FindProperty("stunVfxPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(StunVfxPath);
            playerControllerSo.ApplyModifiedProperties();

            var combat = player.AddComponent<PlayerCombat>();
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("animator").objectReferenceValue = animator;
            combatSo.FindProperty("muzzle").objectReferenceValue = muzzle;
            combatSo.FindProperty("aimCamera").objectReferenceValue = aimCamera;
            combatSo.FindProperty("aimMask").intValue = ~(1 << playerLayer);
            // Moved up from dead-center (0.5) - a center-screen crosshair left too little visible
            // ground ahead with the character/camera rig this tall. CrosshairViewportY (used to
            // position the visual reticle in BuildCrosshair) must match this exactly.
            combatSo.FindProperty("aimViewportY").floatValue = CrosshairViewportY;
            combatSo.FindProperty("enemyHitMask").intValue = LayerMask.GetMask(EnemyLayerName);
            // Optional imported muzzle flash (Free Quick Effects Vol.1 - the Creepy Cat pack this
            // used to point at has been removed from the project). Null (pack not imported) falls
            // back to PlayerCombat's own procedural light.
            combatSo.FindProperty("muzzleFlashEffectPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/vfx_MuzzleFlash_01.prefab");
            // The player's shot is now a real travelling, damage-dealing projectile (Lana Studio's
            // dark-magic bolt) rather than a cosmetic catch-up for an instant hitscan - see
            // PlayerCombat.FireProjectile/BossProjectile. Null falls back to the old tracer+
            // instant-raycast behavior.
            combatSo.FindProperty("projectileVisualPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerProjectileVisualPath);
            combatSo.FindProperty("impactEffectPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerProjectileImpactPath);
            combatSo.FindProperty("meleeHitEffectPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(MeleeHitEffectPath);
            combatSo.FindProperty("topDownBeamDotPurplePrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(TopDownBeamDotPurplePath);
            combatSo.FindProperty("lightningCirclePrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(TopDownLightningCircleBluePath);
            combatSo.FindProperty("electricProjectilePrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(ElectricProjectilePath);
            combatSo.FindProperty("electricImpactPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(ElectricImpactPath);
            // Lana Studio's Range_attack prefabs are authored travelling along local +Y, not the
            // +Z BossProjectile's LookRotation aligns to the aim direction - without this the
            // bolt rendered as a near-vertical column at the muzzle regardless of aim direction.
            combatSo.FindProperty("projectileVisualRotationOffsetEuler").vector3Value = new Vector3(0f, 90f, 0f);
            combatSo.FindProperty("electricProjectileRotationOffset").quaternionValue = Quaternion.Euler(0f, 90f, 0f);
            combatSo.ApplyModifiedProperties();

            BuildUltimate(player, visualRoot, animator.gameObject, playerLayer, combat, cameraController);

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
            emotesSo.FindProperty("playerUltimate").objectReferenceValue = player.GetComponent<PlayerUltimate>();

            // Mech's own Wave/Yes/No/Dance clips (separate skeleton/clip lengths from the
            // astronaut's) - only used for _emoteEndTime timing (see ActiveClips); the actual
            // playback comes from the mech's own AnimatorController, built in BuildUltimate.
            GameObject mechModelForClips = AssetDatabase.LoadAssetAtPath<GameObject>(MechModelPath);
            if (mechModelForClips != null)
            {
                var mechClips = ModelAnimationUtility.LoadSourceClips(mechModelForClips, out string mechClipsPath);
                AnimationClip MechGet(string n) => ModelAnimationUtility.GetClip(mechClips, mechClipsPath, n);
                emotesSo.FindProperty("mechWaveClip").objectReferenceValue = MechGet("Hello"); // see BuildMechAnimatorController's comment
                emotesSo.FindProperty("mechYesClip").objectReferenceValue = MechGet("Yes");
                emotesSo.FindProperty("mechNoClip").objectReferenceValue = MechGet("No");
                emotesSo.FindProperty("danceClip").objectReferenceValue = MechGet("Dance");
            }

            emotesSo.ApplyModifiedProperties();
        }

        // Builds the pre-built-but-inactive Mech visual (astronaut model stays the active one
        // until PlayerUltimate toggles them) plus PlayerDash/PlayerShield/PlayerUltimate/
        // PlayerAbilityInput. Mirrors ItemAssetSetup's "copy vendor FBX into Assets/Art on first
        // use" pattern for the Mech model, in miniature.
        // Mirrors EnemySceneSetup.CreateOrLoadPaletteMaterial - a fresh URP/Lit material bound to
        // the shared T_SpacePalette atlas, cached at its own asset path rather than reusing
        // M_Astronaut.mat (which rendered the mech solid white - see the call site's comment).
        private static Material CreateOrLoadMechMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MechMaterialPath);
            if (existing != null) return existing;

            EnsureFolder("Assets/Art/Materials");

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = "M_MechFinnTheFrog" };

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SpacePaletteTexturePath);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
            }

            AssetDatabase.CreateAsset(material, MechMaterialPath);
            return material;
        }

        private static void BuildUltimate(GameObject player, Transform visualRoot,
            GameObject astronautVisual, int playerLayer, PlayerCombat combat,
            ThirdPersonCameraController cameraController)
        {
            GameObject mechModel = AssetDatabase.LoadAssetAtPath<GameObject>(MechModelPath);
            if (mechModel == null)
            {
                // VendorMechModelPath lives under "asset packs/" - outside Assets/, so it isn't a
                // Unity-tracked asset and AssetDatabase.CopyAsset can't touch it (that API only
                // works between two paths already inside Assets/). Needs a raw filesystem copy
                // resolved against the actual project root, then an explicit Refresh so Unity
                // notices the new file - same pattern as ItemAssetSetup.CopyVendorModelsWhenMissing.
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (!string.IsNullOrWhiteSpace(projectRoot))
                {
                    string source = Path.Combine(projectRoot, VendorMechModelPath.Replace('/', Path.DirectorySeparatorChar));
                    string destination = Path.Combine(projectRoot, MechModelPath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(source) && !File.Exists(destination))
                    {
                        EnsureFolder("Assets/Art/Models/Characters");
                        File.Copy(source, destination, overwrite: false);
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }
                }

                mechModel = AssetDatabase.LoadAssetAtPath<GameObject>(MechModelPath);
            }

            GameObject mechInstance = null;
            Transform mechMuzzleLeft = null;
            Transform mechMuzzleRight = null;
            Transform mechHeadAnchor = null;
            Animator mechAnimator = null;

            if (mechModel != null)
            {
                mechInstance = (GameObject)PrefabUtility.InstantiatePrefab(mechModel, visualRoot);
                mechInstance.name = "MechVisual";
                mechInstance.transform.localPosition = Vector3.zero;
                mechInstance.transform.localRotation = Quaternion.identity;
                foreach (Transform child in mechInstance.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = playerLayer;
                }

                // Hand-offset arm-cannon points, same convention as the astronaut's single
                // Muzzle/HeadAnchor - no bone data to query on a Generic-rig import, nudge by eye
                // in the Inspector if they don't line up once the model is visible. Spread wide
                // (well past the mech's own body width) so the two bolts read as distinct twin
                // cannons rather than firing from almost the same point.
                mechMuzzleLeft = new GameObject("MechMuzzleLeft").transform;
                mechMuzzleLeft.SetParent(mechInstance.transform, false);
                mechMuzzleLeft.localPosition = new Vector3(-1.035f, 2.505f, 0.657f); // hand-tuned in Editor

                mechMuzzleRight = new GameObject("MechMuzzleRight").transform;
                mechMuzzleRight.SetParent(mechInstance.transform, false);
                mechMuzzleRight.localPosition = new Vector3(1.026f, 2.478f, 0.582f); // hand-tuned in Editor

                // Child of the mech (not VisualRoot) so it automatically inherits the mech's own
                // localScale (mechScale, applied at ActivateUltimate time) - the Stun VFX placed
                // here reads at the right size next to the bigger Mech with no extra scale math.
                // Placeholder offset like the muzzles above - nudge by eye once visible.
                mechHeadAnchor = new GameObject("MechHeadAnchor").transform;
                mechHeadAnchor.SetParent(mechInstance.transform, false);
                // Raised from 2f - the muzzles sit around Y~2.5 (see MechMuzzleLeft/Right, hand-
                // tuned by the user), and the head/dome sits well above that, so 2f rendered the
                // Stun VFX inside the mech's own body instead of above its head.
                mechHeadAnchor.localPosition = new Vector3(0f, 3.6f, 0.2f);

                // ConfigureAnimationLooping calls ModelImporter.SaveAndReimport() on the mech FBX -
                // reimporting a Model asset re-syncs every existing scene instance of it back to
                // the importer's own default material mapping ("Atlas"), silently discarding any
                // renderer.sharedMaterial(s) assignment made *before* this point (confirmed: the
                // material was correctly applied and logged, then reverted to Atlas by the time it
                // was inspected - this reimport, which used to run after the material assignment,
                // was why). Every FBX-reimport-triggering call (ConfigureAnimationLooping,
                // BuildMechAnimatorController's own clip lookups don't reimport, only this one
                // does) must run BEFORE the material fixup below, not after.
                ModelAnimationUtility.ConfigureAnimationLooping(mechModel, new[] { "Idle", "Walk", "Dance" });
                AnimatorController mechController = BuildMechAnimatorController(mechModel);
                mechAnimator = mechInstance.GetComponent<Animator>();
                if (mechAnimator == null) mechAnimator = mechInstance.AddComponent<Animator>();
                mechAnimator.runtimeAnimatorController = mechController;
                mechAnimator.applyRootMotion = false;

                // Reusing M_Astronaut.mat rendered the mech solid white - that material's atlas
                // binding assumes the astronaut's own UV layout, and the mech's completely
                // different (Blender-baked) UVs sampled outside the intended region. Every other
                // character in this project (enemies, bosses) instead gets its own dedicated
                // material bound to the same shared T_SpacePalette atlas (see EnemySceneSetup.
                // CreateOrLoadPaletteMaterial) - mirrored here rather than reusing the astronaut's.
                // Runs LAST (after every FBX-reimport-triggering call above) so a reimport can't
                // wipe it out again - see the comment above ConfigureAnimationLooping.
                Material mechMaterial = CreateOrLoadMechMaterial();
                if (mechMaterial != null)
                {
                    foreach (var renderer in mechInstance.GetComponentsInChildren<Renderer>(true))
                    {
                        // sharedMaterial only ever touches slot 0 - if this mesh has multiple
                        // material slots (separate submeshes), the others silently kept whatever
                        // the FBX importer's own extracted material was (often untextured/white).
                        var slots = renderer.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++)
                        {
                            slots[i] = mechMaterial;
                        }
                        renderer.sharedMaterials = slots;
                    }
                }

                mechInstance.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"PlayerSceneSetup: no Mech model at {MechModelPath} (or vendor " +
                                  $"source {VendorMechModelPath}) - Ultimate will have no mech visual.");
            }

            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("mechMuzzleLeft").objectReferenceValue = mechMuzzleLeft;
            combatSo.FindProperty("mechMuzzleRight").objectReferenceValue = mechMuzzleRight;
            combatSo.ApplyModifiedProperties();

            var dash = player.AddComponent<PlayerDash>();
            var dashSo = new SerializedObject(dash);
            dashSo.FindProperty("dashVfxPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(DashVfxPath);
            dashSo.ApplyModifiedProperties();

            var shield = player.AddComponent<PlayerShield>();
            var shieldSo = new SerializedObject(shield);
            shieldSo.FindProperty("mechVisualRoot").objectReferenceValue = mechInstance != null ? mechInstance.transform : null;
            shieldSo.FindProperty("shieldVfxPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(ShieldElectricPath);
            // Just large enough to wrap the 1.2x mech, not the oversized sphere it was before.
            shieldSo.FindProperty("shieldVfxScale").floatValue = 0.9f;
            shieldSo.ApplyModifiedProperties();

            var ultimate = player.AddComponent<PlayerUltimate>();
            var ultimateSo = new SerializedObject(ultimate);
            ultimateSo.FindProperty("astronautVisualRoot").objectReferenceValue = astronautVisual;
            ultimateSo.FindProperty("mechVisualRoot").objectReferenceValue = mechInstance;
            ultimateSo.FindProperty("playerController").objectReferenceValue = player.GetComponent<PlayerController>();
            ultimateSo.FindProperty("playerCombat").objectReferenceValue = combat;
            ultimateSo.FindProperty("playerShield").objectReferenceValue = shield;
            ultimateSo.FindProperty("cameraController").objectReferenceValue = cameraController;
            ultimateSo.FindProperty("astronautAnimator").objectReferenceValue = astronautVisual.GetComponent<Animator>();
            ultimateSo.FindProperty("mechAnimator").objectReferenceValue = mechAnimator;
            ultimateSo.FindProperty("mechHeadAnchor").objectReferenceValue = mechHeadAnchor;
            ultimateSo.ApplyModifiedProperties();

            var abilityInput = player.AddComponent<PlayerAbilityInput>();
            var abilityInputSo = new SerializedObject(abilityInput);
            abilityInputSo.FindProperty("playerUltimate").objectReferenceValue = ultimate;
            abilityInputSo.FindProperty("playerDash").objectReferenceValue = dash;
            abilityInputSo.FindProperty("playerShield").objectReferenceValue = shield;
            abilityInputSo.ApplyModifiedProperties();
        }

        private static (EmoteWheelUI wheelUi, CrosshairUI crosshairUi, HealthHudUI healthHudUi,
            Player.UI.AbilityHudUI abilityHudUi, Player.UI.UltimateHudUI ultimateHudUi,
            Player.UI.AmmoHudUI ammoHudUi, GameObject canvasGo) BuildUI()
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var crosshairUi = BuildCrosshair(canvasGo.transform);
            var wheelUi = BuildEmoteWheel(canvasGo.transform);
            var healthHudUi = BuildHealthHud(canvasGo.transform);
            var abilityHudUi = BuildAbilityHud(canvasGo.transform);
            var ultimateHudUi = BuildUltimateHud(canvasGo.transform);
            var ammoHudUi = BuildAmmoHud(canvasGo.transform);

            return (wheelUi, crosshairUi, healthHudUi, abilityHudUi, ultimateHudUi, ammoHudUi, canvasGo);
        }

        /// Bottom-right magazine/storage readout. Same generated-rect convention as
        /// BuildHealthHud/BuildAbilityHud - the corner opposite Ability (bottom-left) and below
        /// Health (top-right) so none of the HUD panels overlap.
        private static Player.UI.AmmoHudUI BuildAmmoHud(Transform parent)
        {
            const float panelWidth = 220f;
            const float panelHeight = 64f;
            var bottomRight = new Vector2(1f, 0f);

            var root = CreateUiRect("AmmoHud", parent, new Vector2(panelWidth, panelHeight),
                new Vector2(-24f, 24f), bottomRight);
            var hud = root.gameObject.AddComponent<Player.UI.AmmoHudUI>();

            var backdrop = CreateUiRect("Backdrop", root, new Vector2(panelWidth, panelHeight), Vector2.zero, bottomRight);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.08f, 0.55f);

            var ammoRect = CreateUiRect("AmmoText", root, new Vector2(panelWidth - 24f, 28f),
                new Vector2(-12f, 26f), bottomRight);
            var ammoText = ammoRect.gameObject.AddComponent<Text>();
            ammoText.alignment = TextAnchor.MiddleRight;
            ammoText.color = Color.white;
            ammoText.fontSize = 22;
            ammoText.fontStyle = FontStyle.Bold;
            ammoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ammoText.raycastTarget = false;
            ammoText.text = "0 / 0";

            var reloadingRect = CreateUiRect("ReloadingText", root, new Vector2(panelWidth - 24f, 16f),
                new Vector2(-12f, 6f), bottomRight);
            var reloadingText = reloadingRect.gameObject.AddComponent<Text>();
            reloadingText.alignment = TextAnchor.MiddleRight;
            reloadingText.color = new Color(1f, 0.6f, 0.2f);
            reloadingText.fontSize = 13;
            reloadingText.fontStyle = FontStyle.Bold;
            reloadingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            reloadingText.raycastTarget = false;
            reloadingText.text = "RELOADING";
            reloadingText.enabled = false;

            hud.SetAmmoText(ammoText);
            hud.SetReloadingText(reloadingText);

            return hud;
        }

        /// Bottom-left ability cooldowns (Dash/Shield + secondary attack). Same generated-rect
        /// convention as BuildHealthHud, anchored to the opposite corner.
        private static Player.UI.AbilityHudUI BuildAbilityHud(Transform parent)
        {
            const float panelWidth = 220f;
            const float panelHeight = 64f;
            const float barHeight = 14f;

            var bottomLeft = new Vector2(0f, 0f);

            var root = CreateUiRect("AbilityHud", parent, new Vector2(panelWidth, panelHeight),
                new Vector2(24f, 24f), bottomLeft);
            var hud = root.gameObject.AddComponent<Player.UI.AbilityHudUI>();

            var backdrop = CreateUiRect("Backdrop", root, new Vector2(panelWidth, panelHeight), Vector2.zero, bottomLeft);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.08f, 0.55f);

            var (slotAFill, slotALabel) = BuildAbilitySlot(root, "SlotA", new Vector2(8f, panelHeight - 26f),
                panelWidth - 16f, barHeight, "DASH", new Color(0.6f, 0.85f, 1f));
            var (slotBFill, slotBLabel) = BuildAbilitySlot(root, "SlotB", new Vector2(8f, panelHeight - 26f - barHeight - 8f),
                panelWidth - 16f, barHeight, "BEAM", new Color(0.75f, 0.5f, 1f));

            hud.SetWidgets(slotAFill, slotALabel, slotBFill, slotBLabel);
            return hud;
        }

        private static Sprite _solidSprite;

        // Wraps Unity's built-in 1x1 white texture as a Sprite - Image.Type.Filled silently
        // renders as an unclipped full rect when Image.sprite is null (the built-in white
        // texture fallback a bare Image uses doesn't support fill clipping), so every filled
        // progress-bar Image in this file needs an explicit sprite even though it's just a
        // plain color. Cached/shared since it's the same tiny sprite everywhere it's used.
        private static Sprite GetOrCreateSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            var texture = Texture2D.whiteTexture;
            _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }

        private static (Image fill, Text label) BuildAbilitySlot(Transform parent, string name,
            Vector2 anchoredPosition, float width, float height, string labelText, Color fillColor)
        {
            var bottomLeft = new Vector2(0f, 0f);
            var slotRect = CreateUiRect(name, parent, new Vector2(width, height), anchoredPosition, bottomLeft);

            var track = CreateUiRect("Track", slotRect, new Vector2(width, height), Vector2.zero, bottomLeft);
            track.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            var fillRect = CreateUiRect("Fill", slotRect, new Vector2(width, height), Vector2.zero, bottomLeft);
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = GetOrCreateSolidSprite(); // Image.Type.Filled needs a real sprite - a
            // null sprite renders as a plain unclipped rect regardless of fillAmount, which is
            // why these bars looked "always full" no matter what PlayerDash/PlayerShield/
            // PlayerCombat's cooldown values actually were.
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var labelRect = CreateUiRect("Label", slotRect, new Vector2(width - 8f, height), new Vector2(4f, 0f), bottomLeft);
            var label = labelRect.gameObject.AddComponent<Text>();
            label.text = labelText;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.fontSize = 11;
            label.fontStyle = FontStyle.Bold;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.raycastTarget = false;

            return (fill, label);
        }

        /// Top-left ultimate time-remaining bar, hidden unless PlayerUltimate.IsActive.
        private static Player.UI.UltimateHudUI BuildUltimateHud(Transform parent)
        {
            const float panelWidth = 260f;
            const float panelHeight = 36f;

            var topLeft = new Vector2(0f, 1f);

            // The UltimateHudUI component lives on this always-active host, separate from the
            // "panel" GameObject it toggles - a component can't stay subscribed/updating once
            // its own GameObject is deactivated, so the hideable visuals must be a child instead.
            var host = CreateUiRect("UltimateHud", parent, new Vector2(panelWidth, panelHeight),
                new Vector2(24f, -24f), topLeft);
            var hud = host.gameObject.AddComponent<Player.UI.UltimateHudUI>();

            var panel = CreateUiRect("Panel", host, new Vector2(panelWidth, panelHeight), Vector2.zero, topLeft);

            var backdrop = CreateUiRect("Backdrop", panel, new Vector2(panelWidth, panelHeight), Vector2.zero, topLeft);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.03f, 0.08f, 0.6f);

            var track = CreateUiRect("Track", panel, new Vector2(panelWidth - 16f, 12f), new Vector2(8f, -8f), topLeft);
            track.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            var fillRect = CreateUiRect("Fill", panel, new Vector2(panelWidth - 16f, 12f), new Vector2(8f, -8f), topLeft);
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = GetOrCreateSolidSprite(); // see BuildAbilitySlot's comment on this
            fill.color = new Color(0.85f, 0.4f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var textRect = CreateUiRect("Time", panel, new Vector2(panelWidth - 16f, 16f), new Vector2(8f, -22f), topLeft);
            var text = textRect.gameObject.AddComponent<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.95f, 0.85f, 1f);
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;

            hud.SetWidgets(panel.gameObject, fill, text);
            return hud;
        }

        /// Minimal Space Expansion UI health module: one red bar with current HP centered in it.
        private static HealthHudUI BuildHealthHud(Transform parent)
        {
            const float barWidth = 304f;
            const float barHeight = 36f;

            var topRight = new Vector2(1f, 1f);

            Sprite trackSprite = LoadHudSprite(
                HealthBarTrackPath,
                new Vector4(24f, 12f, 24f, 12f));
            Sprite fillSprite = LoadHudSprite(
                HealthBarFillPath,
                new Vector4(24f, 12f, 24f, 12f));
            Font utilityFont = RequireAsset<Font>(HudUtilityFontPath);

            var root = CreateUiRect("HealthHud", parent, new Vector2(barWidth, barHeight),
                new Vector2(-28f, -28f), topRight);
            var hud = root.gameObject.AddComponent<HealthHudUI>();

            Image track = CreateStretchImage("Track", root, trackSprite);
            track.type = Image.Type.Sliced;
            track.color = new Color(0.16f, 0.025f, 0.035f, 0.92f);

            Image fill = CreateStretchImage("Fill", root, fillSprite);
            fill.type = Image.Type.Sliced;
            fill.color = new Color(0.94f, 0.055f, 0.09f, 1f);

            var valueRect = CreateUiRect("HealthValue", root, new Vector2(barWidth, barHeight),
                Vector2.zero);
            Text valueText = valueRect.gameObject.AddComponent<Text>();
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = Color.white;
            valueText.fontSize = 20;
            valueText.fontStyle = FontStyle.Bold;
            valueText.font = utilityFont;
            valueText.raycastTarget = false;
            var valueOutline = valueRect.gameObject.AddComponent<Outline>();
            valueOutline.effectColor = new Color(0.08f, 0f, 0f, 0.9f);
            valueOutline.effectDistance = new Vector2(1f, -1f);

            hud.Configure(fill, valueText);

            return hud;
        }

        private static Image CreateStretchImage(string name, Transform parent, Sprite sprite)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);

            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite LoadHudSprite(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new System.InvalidOperationException(
                    $"PlayerSceneSetup: no UI texture found at {path}.");
            }

            bool requiresImport = importer.textureType != TextureImporterType.Sprite ||
                                  importer.spriteImportMode != SpriteImportMode.Single ||
                                  importer.mipmapEnabled ||
                                  importer.wrapMode != TextureWrapMode.Clamp ||
                                  importer.spriteBorder != border;
            if (requiresImport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }

            return RequireAsset<Sprite>(path);
        }

        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new System.InvalidOperationException(
                    $"PlayerSceneSetup: required {typeof(T).Name} is missing at {path}.");
            }

            return asset;
        }

        private static CrosshairUI BuildCrosshair(Transform parent)
        {
            // Anchor fraction (0.5, CrosshairViewportY), not a pixel offset from center - anchors
            // are true fractions of the parent Canvas rect, so this lines up exactly with
            // PlayerCombat.ComputeAimDirection's ViewportPointToRay(0.5, aimViewportY) regardless
            // of the actual screen/game-view aspect ratio. The previous approach assumed a fixed
            // 1920x1080 reference resolution pixel offset, which only mapped correctly onto real
            // viewport fractions when the screen aspect happened to exactly match 16:9 - at
            // aimViewportY 0.5 (dead-center) that assumption was invisible (offset was always 0
            // either way), but moving the aim point off-center exposed it as the reticle visibly
            // drifting from where shots actually went.
            var crosshairAnchor = new Vector2(0.5f, CrosshairViewportY);
            var root = CreateUiRect("Crosshair", parent, new Vector2(24f, 24f), Vector2.zero, crosshairAnchor);
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

            var so = new SerializedObject(wheelUi);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("ringSprite").objectReferenceValue = ringSprite;
            so.ApplyModifiedProperties();

            // Builds the default 3-wedge (Wave/Yes/No) layout via the same runtime Configure path
            // PlayerEmoteController uses to switch to the Mech's 4-wedge (+Dance) layout - see
            // EmoteWheelUI.Configure. Angle 0 = top, increasing clockwise.
            wheelUi.Configure(new[] { "Wave", "Yes", "No" });

            root.gameObject.SetActive(false);

            return wheelUi;
        }

        private static RectTransform CreateUiRect(string name, Transform parent, Vector2 size, Vector2 anchoredPosition)
        {
            return CreateUiRect(name, parent, size, anchoredPosition, new Vector2(0.5f, 0.5f));
        }

        private static RectTransform CreateUiRect(
            string name, Transform parent, Vector2 size, Vector2 anchoredPosition, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
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
