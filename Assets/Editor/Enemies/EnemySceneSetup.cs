using System.IO;
using System.Linq;
using CharacterEditor;
using Combat;
using Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EnemiesEditor
{
    /// Builds the three fightable enemy prefabs in-place in the Player scene (AnimatorController,
    /// hit collider, Health, and the matching AI script), plus drops in Astronaut_BarbaraTheBee /
    /// Mech_BarbaraTheBee as plain textured placeholders for the later boss fights. Re-run after
    /// Tools/Player Prototype/Build Test Scene, since that command rebuilds Player.unity from an
    /// empty scene and would otherwise wipe these out.
    public static class EnemySceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Player.unity";
        private const string ModelFolder = "Assets/Art/Models/Characters/";
        private const string AnimationFolder = "Assets/Art/Animations/";
        private const string MaterialFolder = "Assets/Art/Materials/";
        private const string PaletteTexturePath = "Assets/Art/Textures/T_SpacePalette.png";
        private const string ParentName = "Enemies";
        private const string EnemyLayerName = "Enemy";

        // Added to each melee enemy's measured collider radius to get its attackRange, so
        // "right in front of me" (meshes touching/overlapping) reliably counts as in range
        // instead of a guessed flat distance that didn't account for how big the model actually
        // is - roughly the player's own collider radius (0.35) plus some arm/weapon reach.
        private const float MeleeReachMargin = 1f;

        private static readonly string[] HoverLoopClipNames = { "Flying_Idle", "Fast_Flying" };
        private static readonly string[] GroundLoopClipNames = { "Idle", "Walk", "Run" };

        [MenuItem("Tools/Enemies/Add Enemies To Player Scene")]
        public static void AddEnemiesToScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existingParent = GameObject.Find(ParentName);
            if (existingParent != null)
            {
                Object.DestroyImmediate(existingParent);
            }

            var parent = new GameObject(ParentName);

            // Idempotent with PlayerSceneSetup's own EnsureLayer(EnemyLayerName) call - both
            // agree on the same layer index either way, whichever runs first.
            int enemyLayer = ModelAnimationUtility.EnsureLayer(EnemyLayerName);

            BuildFlyingEnemy(parent.transform, new Vector3(-8f, 0f, 6f), enemyLayer);
            BuildSmallEnemy(parent.transform, new Vector3(-4f, 0f, 6f), enemyLayer);
            BuildLargeEnemy(parent.transform, new Vector3(0f, 0f, 6f), enemyLayer);
            BuildPlaceholder("Astronaut_BarbaraTheBee", "Enemy_Astronaut_BarbaraTheBee", parent.transform, new Vector3(4f, 0f, 6f), enemyLayer);
            BuildPlaceholder("Mech_BarbaraTheBee", "Enemy_Mech_BarbaraTheBee", parent.transform, new Vector3(8f, 0f, 6f), enemyLayer);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("EnemySceneSetup: added enemies to " + ScenePath);
        }

        private static void BuildFlyingEnemy(Transform parent, Vector3 position, int enemyLayer)
        {
            var model = LoadModel("Enemy_Flying");
            if (model == null) return;

            ModelAnimationUtility.ConfigureAnimationLooping(model, HoverLoopClipNames);
            var controller = BuildHoverController(model, AnimationFolder + "AC_EnemyFlying.controller", "Headbutt");
            var material = CreateOrLoadPaletteMaterial(MaterialFolder + "M_EnemyFlying.mat");

            var instance = SpawnModel(model, "Enemy_Flying", parent, position, material, enemyLayer);

            Bounds bounds = GetLocalRenderBounds(instance);
            var collider = instance.AddComponent<CapsuleCollider>();
            collider.center = bounds.center;
            collider.height = Mathf.Max(bounds.size.y, 0.1f);
            collider.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;

            WireAnimator(instance, controller);
            var health = instance.AddComponent<Health>();
            instance.AddComponent<EnemyFlyingAI>();
            AddHealthBar(instance, bounds, health);
        }

        private static void BuildSmallEnemy(Transform parent, Vector3 position, int enemyLayer)
        {
            var model = LoadModel("Enemy_Small");
            if (model == null) return;

            ModelAnimationUtility.ConfigureAnimationLooping(model, HoverLoopClipNames);
            var controller = BuildHoverController(model, AnimationFolder + "AC_EnemySmall.controller", "Punch");
            var material = CreateOrLoadPaletteMaterial(MaterialFolder + "M_EnemySmall.mat");

            var instance = SpawnModel(model, "Enemy_Small", parent, position, material, enemyLayer);

            Bounds bounds = GetLocalRenderBounds(instance);
            var collider = instance.AddComponent<CapsuleCollider>();
            collider.center = bounds.center;
            collider.height = Mathf.Max(bounds.size.y, 0.1f);
            float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
            collider.radius = radius;

            WireAnimator(instance, controller);
            var health = instance.AddComponent<Health>();
            var ai = instance.AddComponent<EnemySmallAI>();
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("attackRange").floatValue = radius + MeleeReachMargin;
            aiSo.ApplyModifiedProperties();
            AddHealthBar(instance, bounds, health);
        }

        private static void BuildLargeEnemy(Transform parent, Vector3 position, int enemyLayer)
        {
            var model = LoadModel("Enemy_Large");
            if (model == null) return;

            ModelAnimationUtility.ConfigureAnimationLooping(model, GroundLoopClipNames);
            var controller = BuildGroundController(model, AnimationFolder + "AC_EnemyLarge.controller");
            var material = CreateOrLoadPaletteMaterial(MaterialFolder + "M_EnemyLarge.mat");

            var instance = SpawnModel(model, "Enemy_Large", parent, position, material, enemyLayer);

            Bounds bounds = GetLocalRenderBounds(instance);
            var characterController = instance.AddComponent<CharacterController>();
            characterController.center = bounds.center;
            characterController.height = Mathf.Max(bounds.size.y, 0.1f);
            float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
            characterController.radius = radius;

            WireAnimator(instance, controller);
            var health = instance.AddComponent<Health>();
            var ai = instance.AddComponent<EnemyLargeAI>();
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("attackRange").floatValue = radius + MeleeReachMargin;
            aiSo.ApplyModifiedProperties();
            AddHealthBar(instance, bounds, health);
        }

        private static void BuildPlaceholder(string fileName, string displayName, Transform parent, Vector3 position, int enemyLayer)
        {
            var model = LoadModel(fileName);
            if (model == null) return;

            var material = CreateOrLoadPaletteMaterial(MaterialFolder + "M_" + fileName + ".mat");
            SpawnModel(model, displayName, parent, position, material, enemyLayer);
        }

        private static GameObject LoadModel(string fileName)
        {
            string path = ModelFolder + fileName + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                Debug.LogError($"EnemySceneSetup: no model found at {path}. " +
                                "Make sure the FBX has been imported by Unity first.");
            }
            return model;
        }

        private static GameObject SpawnModel(GameObject model, string name, Transform parent, Vector3 position, Material material, int layer)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Recursive, not just the root: ThirdPersonCameraController's collision SphereCast
            // and PlayerCombat's aim raycast both key off layer, and a child renderer/collider
            // left on Default would still register as camera-blocking geometry even with the
            // root itself moved to Enemy.
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }

            if (material != null)
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = material;
                }
            }

            return instance;
        }

        // Sizes hit colliders from the model's actual rendered geometry instead of hand-picked
        // guesses (which, unverified, were the likely reason shots weren't registering against
        // these models - their pivots/silhouettes aren't the humanoid-shaped assumption the
        // original constants were guessed from). Returns bounds in the instance's own local
        // space so the caller can assign them directly to a CapsuleCollider/CharacterController
        // center regardless of the instance's world position/rotation.
        private static Bounds GetLocalRenderBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.up, Vector3.one);

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 localCenter = instance.transform.InverseTransformPoint(worldBounds.center);
            return new Bounds(localCenter, worldBounds.size);
        }

        // Builds a small world-space fill bar floating above the enemy's head, using the same
        // measured bounds already used to size its hit collider (so it sits just above the
        // actual model height instead of a guessed constant). Not parented under the enemy -
        // EnemyHealthBarUI drives its transform off an anchor reference itself, so the model's
        // own rotation/scale can't distort the bar.
        private static void AddHealthBar(GameObject instance, Bounds localBounds, Health health)
        {
            const float barWidth = 1.2f;
            const float barHeight = 0.14f;
            const float headroom = 0.3f;

            var offset = new Vector3(0f, localBounds.max.y + headroom, 0f);

            var barGo = new GameObject(instance.name + "_HealthBar", typeof(Canvas));
            var canvas = barGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = barGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(barWidth, barHeight);

            var canvasGroup = barGo.AddComponent<CanvasGroup>();

            var backdrop = new GameObject("Backdrop", typeof(Image));
            backdrop.transform.SetParent(barGo.transform, false);
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            // Resized live by EnemyHealthBarUI via anchorMax.x (0-1 = health fraction), not
            // Image.Type.Filled - the sprite-less Filled mesh path didn't reliably shrink.
            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(barGo.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(0.02f, 0.02f);
            fillRect.offsetMax = new Vector2(-0.02f, -0.02f);
            var fillImage = fill.GetComponent<Image>();

            var healthBarUi = barGo.AddComponent<EnemyHealthBarUI>();
            healthBarUi.Initialize(instance.transform, offset, fillRect, fillImage, canvasGroup);
            healthBarUi.Bind(health);
        }

        private static void WireAnimator(GameObject instance, AnimatorController controller)
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        private static Material CreateOrLoadPaletteMaterial(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder.TrimEnd('/'));

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTexturePath);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// Shared by the two flyers (Enemy_Flying, Enemy_Small): idle/fast-flying blend by
        /// Speed, one attack trigger driving whichever clip the caller passes in, plus
        /// HitReact/Death overlays. Mirrors AC_Player.controller's AnyState-trigger pattern so
        /// hit reactions never block the AI script driving movement underneath them.
        private static AnimatorController BuildHoverController(GameObject model, string controllerPath, string attackClipName)
        {
            EnsureFolder("Assets/Art/Animations");
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var sourceClips = ModelAnimationUtility.LoadSourceClips(model, out string modelPath);
            AnimationClip Get(string clipName) => ModelAnimationUtility.GetClip(sourceClips, modelPath, clipName);

            AnimationClip idle = Get("Flying_Idle");
            AnimationClip fastFlying = Get("Fast_Flying");
            AnimationClip attack = Get(attackClipName);
            AnimationClip hitReact = Get("HitReact");
            AnimationClip death = Get("Death");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitReact", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (var childState in sm.states.ToList())
            {
                sm.RemoveState(childState.state);
            }

            var idleState = sm.AddState("Flying_Idle");
            idleState.motion = idle;

            var fastState = sm.AddState("Fast_Flying");
            fastState.motion = fastFlying;

            var attackState = sm.AddState("Attack");
            attackState.motion = attack;

            var hitReactState = sm.AddState("HitReact");
            hitReactState.motion = hitReact;

            var deathState = sm.AddState("Death");
            deathState.motion = death;

            sm.defaultState = idleState;

            var idleToFast = idleState.AddTransition(fastState);
            idleToFast.hasExitTime = false;
            idleToFast.duration = 0.2f;
            idleToFast.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var fastToIdle = fastState.AddTransition(idleState);
            fastToIdle.hasExitTime = false;
            fastToIdle.duration = 0.2f;
            fastToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var anyToAttack = sm.AddAnyStateTransition(attackState);
            anyToAttack.canTransitionToSelf = false;
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.05f;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.9f;
            attackToIdle.duration = 0.1f;

            AddHitReactAndDeath(sm, idleState, hitReactState, deathState);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// Enemy_Large: Idle/blend-tree Walk+Run by Speed, two attack triggers (Punch, Weapon)
        /// so the AI script can alternate them, plus HitReact/Death overlays.
        private static AnimatorController BuildGroundController(GameObject model, string controllerPath)
        {
            EnsureFolder("Assets/Art/Animations");
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var sourceClips = ModelAnimationUtility.LoadSourceClips(model, out string modelPath);
            AnimationClip Get(string clipName) => ModelAnimationUtility.GetClip(sourceClips, modelPath, clipName);

            AnimationClip idle = Get("Idle");
            AnimationClip walk = Get("Walk");
            AnimationClip run = Get("Run");
            AnimationClip punch = Get("Punch");
            AnimationClip weapon = Get("Weapon");
            AnimationClip hitReact = Get("HitReact");
            AnimationClip death = Get("Death");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Punch", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Weapon", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitReact", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

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

            var punchState = sm.AddState("Punch");
            punchState.motion = punch;

            var weaponState = sm.AddState("Weapon");
            weaponState.motion = weapon;

            var hitReactState = sm.AddState("HitReact");
            hitReactState.motion = hitReact;

            var deathState = sm.AddState("Death");
            deathState.motion = death;

            sm.defaultState = idleState;

            var idleToMove = idleState.AddTransition(moveState);
            idleToMove.hasExitTime = false;
            idleToMove.duration = 0.15f;
            idleToMove.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

            var moveToIdle = moveState.AddTransition(idleState);
            moveToIdle.hasExitTime = false;
            moveToIdle.duration = 0.15f;
            moveToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            // Added before Punch/Weapon so Death/HitReact are earlier in the AnyState
            // evaluation order and always win priority - if the killing blow lands the same
            // frame an attack trigger is still armed, Death must not lose the race.
            AddHitReactAndDeath(sm, idleState, hitReactState, deathState);

            var anyToPunch = sm.AddAnyStateTransition(punchState);
            anyToPunch.canTransitionToSelf = false;
            anyToPunch.hasExitTime = false;
            anyToPunch.duration = 0.05f;
            anyToPunch.AddCondition(AnimatorConditionMode.If, 0, "Punch");

            var punchToIdle = punchState.AddTransition(idleState);
            punchToIdle.hasExitTime = true;
            punchToIdle.exitTime = 0.9f;
            punchToIdle.duration = 0.1f;

            var anyToWeapon = sm.AddAnyStateTransition(weaponState);
            anyToWeapon.canTransitionToSelf = false;
            anyToWeapon.hasExitTime = false;
            anyToWeapon.duration = 0.05f;
            anyToWeapon.AddCondition(AnimatorConditionMode.If, 0, "Weapon");

            var weaponToIdle = weaponState.AddTransition(idleState);
            weaponToIdle.hasExitTime = true;
            weaponToIdle.exitTime = 0.9f;
            weaponToIdle.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // HitReact: one-shot overlay from any state, back to the idle/locomotion default when
        // it finishes. EnemyBase's Health.Died fires the Death trigger without the AI script
        // waiting on either, so movement/attack logic keeps running underneath a hit reaction
        // instead of stalling on it (same pattern as AC_Player.controller).
        private static void AddHitReactAndDeath(
            AnimatorStateMachine sm, AnimatorState idleState, AnimatorState hitReactState, AnimatorState deathState)
        {
            var anyToHitReact = sm.AddAnyStateTransition(hitReactState);
            anyToHitReact.canTransitionToSelf = false;
            anyToHitReact.hasExitTime = false;
            anyToHitReact.duration = 0.05f;
            anyToHitReact.AddCondition(AnimatorConditionMode.If, 0, "HitReact");

            var hitReactToIdle = hitReactState.AddTransition(idleState);
            hitReactToIdle.hasExitTime = true;
            hitReactToIdle.exitTime = 0.9f;
            hitReactToIdle.duration = 0.1f;

            // Terminal: no return transition. EnemyBase disables no components itself and just
            // stops acting on isDead, so this doesn't need to gate anything.
            var anyToDeath = sm.AddAnyStateTransition(deathState);
            anyToDeath.canTransitionToSelf = false;
            anyToDeath.hasExitTime = false;
            anyToDeath.duration = 0.05f;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");
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
