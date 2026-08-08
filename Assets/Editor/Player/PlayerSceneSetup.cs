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
        private const string PlayerLayerName = "Player";
        private const string EnemyLayerName = "Enemy";

        // Walking plays Run_Gun_Shoot slowed (Arms_Shoot_Walk below) so its swing doesn't read as
        // an exaggerated wave against the much slower leg cycle; sprinting keeps it at full speed
        // (Arms_Shoot_Run). PlayerCombat.CheckShootBeat reads this state's playback live off the
        // Animator (AnimatorStateInfo.normalizedTime), so walking-while-shooting fires slower
        // than running-while-shooting purely as a side effect of this speed difference, not a
        // separately tuned rate.
        private const float WalkShootAnimSpeed = 0.6f;

        // PlayerController normalizes Speed against sprintSpeed (walkSpeed / sprintSpeed = 3.5 /
        // 6.5 =~ 0.538), so a walking player never exceeds ~0.54 and only sprinting reaches
        // above it. Used to tell walking-while-shooting (slowed arm swing) apart from sprinting-
        // while-shooting (full speed) on the Arms layer.
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
            var (wheelUi, crosshairUi, healthHudUi, hudCanvasGo) = BuildUI();

            Animator animator = player.GetComponentInChildren<Animator>();
            var sourceClips = ModelAnimationUtility.LoadSourceClips(model, out string modelPath);
            AnimationClip wave = ModelAnimationUtility.GetClip(sourceClips, modelPath, "Wave");
            AnimationClip yes = ModelAnimationUtility.GetClip(sourceClips, modelPath, "Yes");
            AnimationClip no = ModelAnimationUtility.GetClip(sourceClips, modelPath, "No");
            BuildCombatAndEmotes(player, animator, mainCamera, cameraController,
                wheelUi, crosshairUi, wave, yes, no, playerLayer);

            Health playerHealth = player.AddComponent<Health>();
            player.AddComponent<PlayerDeathHandler>();
            healthHudUi.Bind(playerHealth);

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
                PlayerEmoteController emotes = RequireComponent<PlayerEmoteController>(replacement);
                Transform visualRoot = RequireDirectChild(replacement.transform, "VisualRoot");
                Animator animator = RequireComponentInChildren<Animator>(visualRoot.gameObject);
                Health health = RequireComponent<Health>(replacement);

                SetObjectReference(cameraController, "target", replacement.transform);
                SetObjectReference(playerController, "cameraReference", aimCamera.transform);
                SetObjectReference(playerController, "animator", animator);
                SetObjectReference(visualConformer, "visualRoot", visualRoot);
                SetObjectReference(combat, "aimCamera", aimCamera);
                SetObjectReference(emotes, "playerController", playerController);
                SetObjectReference(emotes, "playerCombat", combat);
                SetObjectReference(emotes, "cameraController", cameraController);
                SetObjectReference(emotes, "wheelUi", wheelUi);
                SetObjectReference(emotes, "crosshairUi", crosshairUi);
                healthHudUi.Bind(health);

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

        // Builds a transform-path AvatarMask limiting the Arms layer to upper-body bones, so its
        // Override blend only ever replaces arm/hand/spine poses and never touches the legs the
        // base layer is driving. Computed from the actual model hierarchy (by bone-name fragment,
        // see LowerBodyBoneNameFragments) rather than hardcoded paths, since the rig is a Generic
        // avatar (not Humanoid) imported from Blender.
        private static AvatarMask BuildUpperBodyMask(GameObject model)
        {
            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath) != null)
            {
                AssetDatabase.DeleteAsset(UpperBodyMaskPath);
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

            AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
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
            muzzle.localPosition = new Vector3(0.383f, 1.863f, 2.661f);

            var playerController = player.GetComponent<PlayerController>();

            var combat = player.AddComponent<PlayerCombat>();
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("animator").objectReferenceValue = animator;
            combatSo.FindProperty("muzzle").objectReferenceValue = muzzle;
            combatSo.FindProperty("aimCamera").objectReferenceValue = aimCamera;
            combatSo.FindProperty("aimMask").intValue = ~(1 << playerLayer);
            // Optional imported muzzle flash (Free Quick Effects Vol.1 - the Creepy Cat pack this
            // used to point at has been removed from the project). Null (pack not imported) falls
            // back to PlayerCombat's own procedural light. The impact spark is left on
            // PlayerCombat's own procedural burst deliberately - a full effects-pack burst read as
            // out of place on a hitscan impact, so nothing is wired there.
            combatSo.FindProperty("muzzleFlashEffectPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/vfx_MuzzleFlash_01.prefab");
            // Same projectile visual the mech's own bullets use (Free Quick Effects Vol.1) - flown
            // from muzzle to hit point as a cosmetic catch-up for the instant hitscan resolution,
            // replacing the plain LineRenderer tracer. Null (pack not imported) falls back to it.
            combatSo.FindProperty("projectileVisualPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/vfx_Projectile_01.prefab");
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

        private static (EmoteWheelUI wheelUi, CrosshairUI crosshairUi, HealthHudUI healthHudUi, GameObject canvasGo) BuildUI()
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

            return (wheelUi, crosshairUi, healthHudUi, canvasGo);
        }

        /// Segmented "energy cell" readout, built the same way as the emote wheel/crosshair -
        /// generated UI rects, no external art - so a health bar doesn't require an extra asset
        /// pack dependency. Anchored top-right per the game's HUD layout.
        private static HealthHudUI BuildHealthHud(Transform parent)
        {
            const int segmentCount = 12;
            const float segmentWidth = 16f;
            const float segmentHeight = 16f;
            const float segmentGap = 3f;
            const float panelWidth = 260f;
            const float panelHeight = 86f;
            float rowWidth = segmentCount * segmentWidth + (segmentCount - 1) * segmentGap;

            var topRight = new Vector2(1f, 1f);
            var topLeft = new Vector2(0f, 1f);

            var root = CreateUiRect("HealthHud", parent, new Vector2(panelWidth, panelHeight),
                new Vector2(-24f, -24f), topRight);
            var hud = root.gameObject.AddComponent<HealthHudUI>();

            var backdrop = CreateUiRect("Backdrop", root, new Vector2(panelWidth, panelHeight), Vector2.zero, topRight);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.08f, 0.55f);

            var labelRect = CreateUiRect("Label", root, new Vector2(panelWidth - 24f, 18f),
                new Vector2(-12f, -10f), topRight);
            var label = labelRect.gameObject.AddComponent<Text>();
            label.text = "HULL INTEGRITY";
            label.alignment = TextAnchor.MiddleRight;
            label.color = new Color(0.75f, 0.92f, 1f);
            label.fontSize = 13;
            label.fontStyle = FontStyle.Bold;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.raycastTarget = false;

            var rowRect = CreateUiRect("SegmentsRow", root, new Vector2(rowWidth, segmentHeight),
                new Vector2(-12f, -32f), topRight);

            var segments = new Image[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                var segmentRect = CreateUiRect($"Segment_{i}", rowRect, new Vector2(segmentWidth, segmentHeight),
                    new Vector2(i * (segmentWidth + segmentGap), 0f), topLeft);
                var segmentImage = segmentRect.gameObject.AddComponent<Image>();
                segmentImage.raycastTarget = false;
                segments[i] = segmentImage;
            }

            var percentRect = CreateUiRect("Percent", root, new Vector2(panelWidth - 24f, 16f),
                new Vector2(-12f, -52f), topRight);
            var percentText = percentRect.gameObject.AddComponent<Text>();
            percentText.alignment = TextAnchor.MiddleRight;
            percentText.color = Color.white;
            percentText.fontSize = 12;
            percentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            percentText.raycastTarget = false;

            hud.SetSegments(segments);
            hud.SetPercentText(percentText);

            return hud;
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
