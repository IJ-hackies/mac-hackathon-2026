using System.Linq;
using CharacterEditor;
using Combat;
using Player;
using Tutorial;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Vfx;

namespace TutorialEditor
{
    /// Additive helper for hand-building Tutorial.unity: drops in one example of every gameplay
    /// object type (the five named stage gates, the practice dummy, the three item pickups, one
    /// info zone, the UI, and the manager) so there's a working reference to look at, without
    /// ever touching or removing anything already in the scene. Each piece is skipped
    /// individually if one of its type (or, for gates, that exact named gate) already exists -
    /// safe to run repeatedly as you build out the rest by hand. The room geometry itself is
    /// intentionally not touched by this or any other script - see
    /// Context/Chunks/gameplay/tutorial.md for why.
    public static class TutorialGameplayStarterKit
    {
        private const string EnemyModelPath = "Assets/Art/Models/Characters/Enemy_Large.fbx";
        private const string FlyingEnemyModelPath = "Assets/Art/Models/Characters/Enemy_Flying.fbx";
        private const string AnimationFolder = "Assets/Art/Animations/";
        private const string HealthPickupPath = "Assets/Prefabs/Items/Pickup_Health.prefab";
        private const string AmmoPickupPath = "Assets/Prefabs/Items/Pickup_Ammo.prefab";
        private const string ThunderPickupPath = "Assets/Prefabs/Items/Pickup_Thunder.prefab";
        private const string ExitZoneVfxPath = "Assets/Lana Studio/Casual RPG VFX/Prefabs/Area_generic/Area_generic_blue.prefab";
        private const string ShurikenProjectilePath = "Assets/Lana Studio/Casual RPG VFX/Prefabs/Range_attack/Projectiles_green_shuriken.prefab";
        private const string ShurikenImpactPath = "Assets/Lana Studio/Casual RPG VFX/Prefabs/Range_attack/Hit_wind.prefab";

        // (scene object name, TutorialManager field name) for each of the five stage-boundary
        // gates, in tutorial order - matches TutorialManager.Configure's gate parameters exactly.
        private static readonly (string Name, string FieldName)[] GateSpecs =
        {
            ("Gate To Jump", "gateToJump"),
            ("Gate To Dash", "gateToDash"),
            ("Gate To Combat", "gateToCombat"),
            ("Gate To Items", "gateToItems"),
            ("Gate To Overview", "gateToOverview"),
        };

        // Isolated single-purpose command - only touches the exit zone, nothing else in the
        // scene. Safe to run against an already-fully-wired scene: it only adds the zone if none
        // named "Exit Zone" exists yet (found via FindObjectsByType, which searches the whole
        // scene regardless of nesting depth, not just root objects), and only fills its own
        // "manager" field if that field is currently empty - it never touches any other
        // GameObject or overwrites an existing reference.
        [MenuItem("Tools/Tutorial/Add Exit Zone VFX")]
        public static void AddExitZoneVfxOnly()
        {
            AddExitZoneIfMissing();
        }

        // Isolated single-purpose command, same guarantees as Add Exit Zone VFX above - only
        // adds the flying shield-trainer enemy if none exists yet, and only fills its own
        // Player Shield/Player Health/manager fields if they're currently empty.
        [MenuItem("Tools/Tutorial/Add Shield Trainer Only")]
        public static void AddShieldTrainerOnly()
        {
            AddShieldTrainerIfMissing();
        }

        [MenuItem("Tools/Tutorial/Add One Of Each Gameplay Object")]
        public static void AddOneOfEach()
        {
            // Created first (not last) so every other piece below can find it and self-wire its
            // own "manager" field in the same pass, instead of only wiring up on a second run.
            AddManagerIfMissing();
            AddGatesIfMissing();
            AddDummyIfMissing();
            AddShieldTrainerIfMissing();
            AddPickupIfMissing(HealthPickupPath, TutorialPickupWatcher.Kind.Health);
            AddPickupIfMissing(AmmoPickupPath, TutorialPickupWatcher.Kind.Ammo);
            AddPickupIfMissing(ThunderPickupPath, TutorialPickupWatcher.Kind.Thunder);
            AddInfoZoneIfMissing();
            AddExitZoneIfMissing();
            AddUiIfMissing();

            // Ui/Dummy/ShieldTrainer are wired last, once all three are guaranteed to exist - the
            // single most common cause of "nothing happens in Play mode" is
            // TutorialManager.Start() hitting a null Ui reference and throwing before it ever
            // shows the Movement banner.
            var manager = FindManager();
            AssignFieldIfEmpty(manager, "ui", Object.FindFirstObjectByType<TutorialUIController>(FindObjectsInactive.Include));
            AssignFieldIfEmpty(manager, "dummy", Object.FindFirstObjectByType<TutorialDummyAI>(FindObjectsInactive.Include));
            AssignFieldIfEmpty(manager, "shieldTrainer", Object.FindFirstObjectByType<TutorialShieldTrainerAI>(FindObjectsInactive.Include));

            Debug.Log("TutorialGameplayStarterKit: done. Position each new object where you want " +
                      "it, duplicate the info zone for your other Overview callouts, then drag " +
                      "everything into the TutorialManager's Inspector fields (already done for " +
                      "any gate/zone/pickup/trainer the manager didn't already have wired).");
        }

        // Creates all five named gates the manager expects (skipping any that already exist by
        // name), spaced out along Z so they don't stack on top of each other before you move
        // them into their real doorways. Also fills in the manager's matching field for any gate
        // it finds still unassigned, without ever overwriting a field you've already wired.
        private static void AddGatesIfMissing()
        {
            var existingGates = Object.FindObjectsByType<TutorialGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // An earlier run of this tool (before it made all five) may have left one generic
            // "TutorialGate" behind. Adopt it as the first slot instead of leaving it stray
            // alongside five new ones - keeps the total at five, not six.
            bool anyNamedGateExists = existingGates.Any(g => GateSpecs.Any(spec => spec.Name == g.name));
            var legacyGate = existingGates.FirstOrDefault(g => g.name == "TutorialGate");
            if (!anyNamedGateExists && legacyGate != null)
            {
                legacyGate.name = GateSpecs[0].Name;
                Debug.Log($"Renamed the earlier \"TutorialGate\" to \"{GateSpecs[0].Name}\" instead of adding a duplicate.");
            }

            for (int i = 0; i < GateSpecs.Length; i++)
            {
                var (name, fieldName) = GateSpecs[i];
                var gate = existingGates.FirstOrDefault(g => g.name == name);

                if (gate == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = name;
                    go.transform.position = new Vector3(0f, 2f, i * 5f);
                    go.transform.localScale = new Vector3(3.6f, 4.8f, 0.25f);
                    gate = go.AddComponent<TutorialGate>();
                    Debug.Log($"Added \"{name}\" at (0, 2, {i * 5f}) - move it into its doorway.");
                }

                AssignFieldIfEmpty(FindManager(), fieldName, gate);
            }
        }

        private static TutorialManager FindManager() =>
            Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);

        // Only ever sets a field that's currently null on the given component - never overrides
        // a reference you've already dragged in by hand. Used for the manager's own gate fields
        // and for every zone/pickup's own "manager" back-reference.
        private static void AssignFieldIfEmpty(Object target, string fieldName, Object value)
        {
            if (target == null || value == null) return;

            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null || property.objectReferenceValue != null) return;

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddDummyIfMissing()
        {
            if (Object.FindFirstObjectByType<TutorialDummyAI>(FindObjectsInactive.Include) != null) return;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyModelPath);
            if (model == null)
            {
                Debug.LogError($"TutorialGameplayStarterKit: no model found at {EnemyModelPath}.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "TrainingDummy";
            instance.transform.position = new Vector3(0f, 0f, 5f);

            Bounds bounds = GetLocalRenderBounds(instance);
            var collider = instance.AddComponent<CapsuleCollider>();
            collider.center = bounds.center;
            collider.height = Mathf.Max(bounds.size.y, 0.1f);
            collider.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = BuildIdleOnlyController(model, "AC_TutorialDummy.controller", "Idle");
            animator.applyRootMotion = false;

            instance.AddComponent<Health>();
            var dummy = instance.AddComponent<TutorialDummyAI>();

            var playerCombat = Object.FindFirstObjectByType<PlayerCombat>();
            if (playerCombat != null)
            {
                dummy.Configure(playerCombat);
            }
            else
            {
                Debug.LogWarning("TutorialGameplayStarterKit: no PlayerCombat found in the scene - " +
                                  "add your PlayerRig first, or drag it onto the dummy's Player Combat field by hand.");
            }

            Debug.Log("Added one TrainingDummy at (0, 0, 5).");
        }

        // Stationary flying enemy for the Power-Ups room: teaches raising Shield (Shift, once
        // Thunder has activated Ultimate). It never puts the player in real danger - see
        // TutorialShieldTrainerAI's own doc comment - so no Health/Collider is needed on it.
        private static void AddShieldTrainerIfMissing()
        {
            if (Object.FindFirstObjectByType<TutorialShieldTrainerAI>(FindObjectsInactive.Include) != null) return;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FlyingEnemyModelPath);
            if (model == null)
            {
                Debug.LogError($"TutorialGameplayStarterKit: no model found at {FlyingEnemyModelPath}.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "ShieldTrainer";
            instance.transform.position = new Vector3(0f, 3f, 10f);

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = BuildIdleOnlyController(
                model, "AC_TutorialShieldTrainer.controller", "Flying_Idle");
            animator.applyRootMotion = false;

            var firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(instance.transform, false);
            firePoint.localPosition = new Vector3(0f, 1.971f, 0.563f); // same hand-tuned offset as EnemyFlyingAI's

            var trainer = instance.AddComponent<TutorialShieldTrainerAI>();
            var trainerSo = new SerializedObject(trainer);
            trainerSo.FindProperty("firePoint").objectReferenceValue = firePoint;
            trainerSo.FindProperty("projectileVisualPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(ShurikenProjectilePath);
            trainerSo.FindProperty("impactEffectPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(ShurikenImpactPath);
            trainerSo.ApplyModifiedPropertiesWithoutUndo();

            var playerController = Object.FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                trainer.Configure(playerController.GetComponent<PlayerShield>(), playerController.GetComponent<Health>());
            }
            else
            {
                Debug.LogWarning("TutorialGameplayStarterKit: no PlayerRig found in the scene - " +
                                  "add it first, or drag Player Shield/Player Health onto the trainer's fields by hand.");
            }

            AssignFieldIfEmpty(FindManager(), "shieldTrainer", trainer);

            Debug.Log("Added one ShieldTrainer at (0, 3, 10) - move it into your Power-Ups room.");
        }

        private static void AddPickupIfMissing(string prefabPath, TutorialPickupWatcher.Kind kind)
        {
            bool exists = Object.FindObjectsByType<TutorialPickupWatcher>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(w => WatcherKind(w) == kind);
            if (exists) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"TutorialGameplayStarterKit: no item prefab found at {prefabPath}.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3((int)kind * 2f - 2f, 0f, 8f);

            var watcher = instance.AddComponent<TutorialPickupWatcher>();
            var so = new SerializedObject(watcher);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssignFieldIfEmpty(watcher, "manager", FindManager());

            Debug.Log($"Added one {kind} pickup at {instance.transform.position}.");
        }

        // TutorialPickupWatcher.Kind is private-serialized (set via the Inspector dropdown
        // normally); reflection-free readback for the "does one already exist" check below via
        // the same SerializedObject path used to write it above.
        private static TutorialPickupWatcher.Kind WatcherKind(TutorialPickupWatcher watcher)
        {
            var so = new SerializedObject(watcher);
            return (TutorialPickupWatcher.Kind)so.FindProperty("kind").enumValueIndex;
        }

        private static void AddInfoZoneIfMissing()
        {
            if (Object.FindObjectsByType<TutorialZone>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Any(z => z.name == "TutorialInfoZone"))
            {
                return;
            }

            var go = new GameObject("TutorialInfoZone", typeof(BoxCollider));
            go.transform.position = new Vector3(0f, 1f, 12f);
            var collider = go.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(6f, 4f, 4f);
            var zone = go.AddComponent<TutorialZone>();
            AssignFieldIfEmpty(zone, "manager", FindManager());

            Debug.Log("Added one TutorialInfoZone at (0, 1, 12) - set its Message field and " +
                      "duplicate it for each station/wave callout (leave Advances To Complete " +
                      "unticked on these - that's what the dedicated Exit Zone is for).");
        }

        // A properly-configured finish line, not just another info callout: Advances To Complete
        // is pre-ticked, the message is left blank, and Lana Studio's Area_generic_blue VFX
        // (already imported under Assets/Lana Studio/) marks it visually so the player notices it
        // instead of walking past. FixUrpMaterials/ForceHierarchyParticleScaling are the same
        // pair every other imported VFX instantiation in this project runs - see
        // Items/ItemPickup.cs or Player/PlayerCombat.cs for the established pattern.
        private static void AddExitZoneIfMissing()
        {
            bool exists = Object.FindObjectsByType<TutorialZone>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(z => z.name == "Exit Zone");
            if (exists) return;

            var go = new GameObject("Exit Zone", typeof(BoxCollider));
            go.transform.position = new Vector3(0f, 1f, 30f);
            var collider = go.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(8f, 4f, 3f);

            var zone = go.AddComponent<TutorialZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("advancesToComplete").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssignFieldIfEmpty(zone, "manager", FindManager());

            var vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitZoneVfxPath);
            if (vfxPrefab != null)
            {
                var vfx = (GameObject)PrefabUtility.InstantiatePrefab(vfxPrefab, go.transform);
                vfx.transform.localPosition = Vector3.zero;
                ImportedVfxUtility.FixUrpMaterials(vfx);
                ImportedVfxUtility.ForceHierarchyParticleScaling(vfx);
            }
            else
            {
                Debug.LogError($"TutorialGameplayStarterKit: no VFX prefab found at {ExitZoneVfxPath}.");
            }

            Debug.Log("Added \"Exit Zone\" at (0, 1, 30) with the Area_generic_blue VFX - move it " +
                      "to the far end of your Overview room, resized to your hallway width.");
        }

        private static void AddUiIfMissing()
        {
            if (Object.FindFirstObjectByType<TutorialUIController>(FindObjectsInactive.Include) != null) return;

            var canvasGo = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                Debug.Log("Added one EventSystem (none existed yet).");
            }

            canvasGo.AddComponent<TutorialUIController>();
            Debug.Log("Added one TutorialCanvas with TutorialUIController.");
        }

        private static void AddManagerIfMissing()
        {
            if (Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include) != null) return;

            var go = new GameObject("TutorialManager");
            go.AddComponent<TutorialManager>();
            Debug.Log("Added one TutorialManager - drag the UI, dummy, and your five gates into " +
                      "its Inspector fields once you've placed all five.");
        }

        private static Bounds GetLocalRenderBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.up, Vector3.one);

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

            Vector3 localCenter = instance.transform.InverseTransformPoint(worldBounds.center);
            return new Bounds(localCenter, worldBounds.size);
        }

        private static AnimatorController BuildIdleOnlyController(GameObject model, string controllerFileName, string idleClipName)
        {
            string controllerPath = AnimationFolder + controllerFileName;
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                return AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            }

            var sourceClips = ModelAnimationUtility.LoadSourceClips(model, out string modelPath);
            AnimationClip idle = ModelAnimationUtility.GetClip(sourceClips, modelPath, idleClipName);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (var childState in sm.states.ToList()) sm.RemoveState(childState.state);

            var idleState = sm.AddState("Idle");
            idleState.motion = idle;
            sm.defaultState = idleState;

            EditorUtility.SetDirty(controller);
            return controller;
        }
    }
}
