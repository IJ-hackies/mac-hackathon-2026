using System.IO;
using System.Linq;
using Items;
using Tutorial;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TutorialEditor
{
    /// Targeted, position-preserving fixes for objects already hand-placed in Tutorial.unity.
    /// Nothing here moves, rotates, or rescales anything - each command only adds a missing
    /// component or swaps a material/layer on whatever is already in the scene.
    public static class TutorialFixups
    {
        private const string BarrierMaterialPath = "Assets/Art/Materials/Tutorial/M_TutorialBarrier.mat";
        private const string HudPanelSpritePath = "Assets/Art/Textures/UI/Health/SpaceExpansion_BarTrack_Grey.png";
        private const string HudFontPath = "Assets/Art/Fonts/UI/KenneyFutureNarrow.ttf";
        private const string ShurikenProjectilePath = "Assets/Lana Studio/Casual RPG VFX/Prefabs/Range_attack/Projectiles_green_shuriken.prefab";
        private const string ShurikenImpactPath = "Assets/Lana Studio/Casual RPG VFX/Prefabs/Range_attack/Hit_wind.prefab";

        // ---- Item pickups orbiting around their pivot instead of spinning in place ----

        // Items.ItemPickup.Update() rotates the object around its OWN transform - if that
        // transform's origin isn't at the model's visual center (an off-center import pivot,
        // which the vendor Ultimate Space Kit models are known to have inconsistently - see
        // world-authoring.md's "Imported model pivots are not universally at the visible base"),
        // the mesh visibly sweeps around that off-center point as it spins, reading as "orbiting"
        // rather than spinning on the spot - even with no unusual parenting at all. This
        // recenters the instance in place: every direct child is shifted back by the render
        // bounds' local center, and the root is moved forward by the same amount in world space,
        // so the pivot now sits exactly where the mesh already visually is - nothing moves on
        // screen, only where future rotations are centered. Only touches instances actually
        // found off-center; does nothing to already-centered pickups (Ammo/Thunder, normally).
        [MenuItem("Tools/Tutorial/Fix Off-Center Pickup Pivots")]
        public static void FixOffCenterPickupPivots()
        {
            var pickups = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int fixedCount = 0;

            foreach (var pickup in pickups)
            {
                Transform root = pickup.transform;
                Bounds localBounds = GetLocalRenderBounds(root.gameObject);
                Vector3 centerLocal = localBounds.center;
                centerLocal.y = 0f; // only re-center the spin axis (X/Z); keep the authored hover height

                if (centerLocal.sqrMagnitude < 0.0004f) continue; // already centered (< 2cm off)

                foreach (Transform child in root)
                {
                    child.localPosition -= centerLocal;
                }
                root.position += root.TransformVector(centerLocal);

                fixedCount++;
                Debug.Log($"TutorialFixups: recentered \"{pickup.name}\"'s spin pivot by {centerLocal} " +
                           "(local) - it should now spin in place instead of orbiting.");
            }

            if (fixedCount == 0)
            {
                Debug.Log("TutorialFixups: every ItemPickup's spin pivot was already centered.");
            }
        }

        // ---- Console spam from Health's HitReact/Death triggers ----

        // Combat.Health.ApplyDamage unconditionally calls animator.SetTrigger("HitReact") (and
        // "Death" on a killing blow) on whatever Animator it's given - a harmless no-op on an
        // unknown parameter, but Unity still logs a warning for it every single time, which was
        // burying real warnings under repeated hit-spam during a combat test. The dummy/trainer's
        // generated Idle-only AnimatorController never declared those parameters at all. This
        // only adds the two missing Trigger parameters (no states/transitions) to whichever of
        // the two generated controllers already exist - it does not touch anything at runtime.
        [MenuItem("Tools/Tutorial/Fix Animator Trigger Warnings")]
        public static void FixAnimatorTriggerWarnings()
        {
            string[] controllerPaths =
            {
                "Assets/Art/Animations/AC_TutorialDummy.controller",
                "Assets/Art/Animations/AC_TutorialShieldTrainer.controller",
            };
            string[] requiredTriggers = { "HitReact", "Death" };

            foreach (string path in controllerPaths)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null) continue;

                bool dirty = false;
                foreach (string trigger in requiredTriggers)
                {
                    if (controller.parameters.Any(p => p.name == trigger)) continue;
                    controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                    dirty = true;
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(controller);
                    Debug.Log($"TutorialFixups: added missing HitReact/Death trigger parameters to {path}.");
                }
            }

            AssetDatabase.SaveAssets();
        }

        // ---- Info zones doing nothing on contact ----

        // Reports every TutorialZone's actual configuration - the most common reason "walking
        // into it does nothing" is that Message is blank and Advances To Complete is off (in
        // which case OnTriggerEnter now also logs a warning at runtime, see TutorialZone.cs), or
        // its Collider was never set to Is Trigger (in which case it silently blocks the player
        // like a wall instead of firing OnTriggerEnter at all). Read-only - fixes nothing itself,
        // since "what should this zone say" isn't something a script can decide for you.
        [MenuItem("Tools/Tutorial/Diagnose Info Zones")]
        public static void DiagnoseInfoZones()
        {
            var zones = Object.FindObjectsByType<Tutorial.TutorialZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (zones.Length == 0)
            {
                Debug.LogWarning("TutorialFixups: no TutorialZone found in the scene.");
                return;
            }

            foreach (var zone in zones)
            {
                var so = new SerializedObject(zone);
                string message = so.FindProperty("message").stringValue;
                bool advances = so.FindProperty("advancesToComplete").boolValue;
                var manager = so.FindProperty("manager").objectReferenceValue;

                var collider = zone.GetComponent<Collider>();
                bool isTrigger = collider != null && collider.isTrigger;

                bool useless = string.IsNullOrEmpty(message) && !advances;

                Debug.Log($"TutorialZone \"{zone.name}\": Message={(string.IsNullOrEmpty(message) ? "(empty)" : $"\"{message}\"")}, " +
                          $"AdvancesToComplete={advances}, Manager={(manager != null ? "OK" : "MISSING")}, " +
                          $"Collider={(collider == null ? "MISSING" : isTrigger ? "OK (Is Trigger)" : "NOT a trigger - will block the player instead of firing")}" +
                          (useless ? " -- USELESS: no message and doesn't advance, walking in will do nothing" : ""));
            }
        }

        // ---- Power-Ups stage stuck (text never changes / shield mitigation does nothing) ----

        // Both symptoms trace back to a missing reference somewhere in
        // Manager <-> Pickups / Manager <-> ShieldTrainer <-> Player wiring - a pickup whose own
        // "manager" field is empty silently no-ops on collection (TutorialPickupWatcher.OnDestroy
        // guards on manager != null), so TutorialManager's _healthCollected/_ammoCollected/
        // _thunderCollected flags never all become true and the instructions never switch; a
        // ShieldTrainer whose Player Shield field is empty can never detect that Shield is
        // active, so DamageMitigated never fires no matter how long you hold it. This checks and
        // fixes every link in one pass and logs exactly what it found, including each pickup's
        // configured Kind (in case two pickups were accidentally left set to the same Kind).
        [MenuItem("Tools/Tutorial/Diagnose And Fix Power-Ups Wiring")]
        public static void DiagnoseAndFixPowerUpsWiring()
        {
            var manager = Object.FindFirstObjectByType<Tutorial.TutorialManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Debug.LogError("TutorialFixups: no TutorialManager found in the scene - nothing to check.");
                return;
            }

            var trainer = Object.FindFirstObjectByType<Tutorial.TutorialShieldTrainerAI>(FindObjectsInactive.Include);
            var managerSo = new SerializedObject(manager);
            bool trainerWasEmpty = managerSo.FindProperty("shieldTrainer").objectReferenceValue == null;
            AssignIfEmpty(managerSo, "shieldTrainer", trainer);
            managerSo.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log(trainer == null
                ? "TutorialFixups: no TutorialShieldTrainerAI found in the scene at all!"
                : $"TutorialFixups: TutorialManager.Shield Trainer {(trainerWasEmpty ? "was EMPTY - fixed" : "OK")}.");

            var watchers = Object.FindObjectsByType<Tutorial.TutorialPickupWatcher>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (watchers.Length == 0)
            {
                Debug.LogError("TutorialFixups: no TutorialPickupWatcher found in the scene at all!");
            }
            var seenKinds = new System.Collections.Generic.List<int>();
            foreach (var watcher in watchers)
            {
                var so = new SerializedObject(watcher);
                var managerProperty = so.FindProperty("manager");
                bool wasEmpty = managerProperty.objectReferenceValue == null;
                if (wasEmpty) managerProperty.objectReferenceValue = manager;
                so.ApplyModifiedPropertiesWithoutUndo();

                int kindIndex = so.FindProperty("kind").enumValueIndex;
                seenKinds.Add(kindIndex);
                var kind = (Tutorial.TutorialPickupWatcher.Kind)kindIndex;
                Debug.Log($"TutorialFixups: pickup \"{watcher.name}\" - Kind={kind}, Manager={(wasEmpty ? "was EMPTY - fixed" : "OK")}.");
            }
            foreach (var kindValue in seenKinds.Distinct())
            {
                int count = seenKinds.Count(k => k == kindValue);
                if (count > 1)
                {
                    Debug.LogError($"TutorialFixups: {count} pickups are all set to Kind=" +
                                     $"{(Tutorial.TutorialPickupWatcher.Kind)kindValue} - exactly one of each " +
                                     "(Health/Ammo/Thunder) is required for the stage to ever complete.");
                }
            }

            if (trainer != null)
            {
                var trainerSo = new SerializedObject(trainer);
                bool shieldWasEmpty = trainerSo.FindProperty("playerShield").objectReferenceValue == null;
                bool healthWasEmpty = trainerSo.FindProperty("playerHealth").objectReferenceValue == null;
                var playerController = Object.FindFirstObjectByType<Player.PlayerController>();
                if (playerController != null)
                {
                    AssignIfEmpty(trainerSo, "playerShield", playerController.GetComponent<Player.PlayerShield>());
                    AssignIfEmpty(trainerSo, "playerHealth", playerController.GetComponent<Combat.Health>());
                }
                trainerSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"TutorialFixups: ShieldTrainer.Player Shield {(shieldWasEmpty ? "was EMPTY - fixed" : "OK")}, " +
                          $"Player Health {(healthWasEmpty ? "was EMPTY - fixed" : "OK")}.");
            }

            Debug.Log("TutorialFixups: Power-Ups wiring check complete - see the log lines above.");
        }

        // ---- Shield trainer not attacking/facing the player ----

        // Retrofits an already-placed TutorialShieldTrainerAI without moving it: adds a FirePoint
        // child if it doesn't have one (same offset EnemyFlyingAI's own FirePoint uses on this
        // model), and wires the shuriken projectile/impact VFX and Player Shield/Player Health if
        // those fields are currently empty. An enemy created before this script fired real
        // projectiles will otherwise just sit there with no way to actually reach the player.
        [MenuItem("Tools/Tutorial/Fix Shield Trainer Targeting")]
        public static void FixShieldTrainerTargeting()
        {
            var trainer = Object.FindFirstObjectByType<Tutorial.TutorialShieldTrainerAI>(FindObjectsInactive.Include);
            if (trainer == null)
            {
                Debug.LogWarning("TutorialFixups: no TutorialShieldTrainerAI found in the scene.");
                return;
            }

            var so = new SerializedObject(trainer);

            var firePointProperty = so.FindProperty("firePoint");
            if (firePointProperty.objectReferenceValue == null)
            {
                var firePoint = new GameObject("FirePoint").transform;
                firePoint.SetParent(trainer.transform, false);
                firePoint.localPosition = new Vector3(0f, 1.971f, 0.563f);
                firePointProperty.objectReferenceValue = firePoint;
                Debug.Log($"TutorialFixups: added a FirePoint child to \"{trainer.name}\".");
            }

            AssignIfEmpty(so, "projectileVisualPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ShurikenProjectilePath));
            AssignIfEmpty(so, "impactEffectPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ShurikenImpactPath));

            var playerController = Object.FindFirstObjectByType<Player.PlayerController>();
            if (playerController != null)
            {
                AssignIfEmpty(so, "playerShield", playerController.GetComponent<Player.PlayerShield>());
                AssignIfEmpty(so, "playerHealth", playerController.GetComponent<Combat.Health>());
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"TutorialFixups: \"{trainer.name}\" is now configured to face and fire at the player.");
        }

        private static void AssignIfEmpty(SerializedObject so, string fieldName, Object value)
        {
            if (value == null) return;
            var property = so.FindProperty(fieldName);
            if (property != null && property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
            }
        }

        // Items.ItemPickup.Update() only ever rotates its OWN transform around its own local up
        // axis - a pure spin-in-place. The "orbiting" look happens when a pickup ends up as a
        // CHILD of something that itself rotates every frame: TutorialDummyAI.FacePlayer() turns
        // the dummy to face the player every frame, and ItemPickup's own spin does the same to
        // any other pickup accidentally nested under it - either way, a child's world position
        // sweeps around that parent's pivot as it turns, reading as "rotating in circles" rather
        // than spinning on the spot. This unparents any pickup found nested under a
        // TutorialDummyAI or another ItemPickup, preserving its exact world position/rotation
        // (worldPositionStays: true) - nothing else about it changes.
        [MenuItem("Tools/Tutorial/Fix Orbiting Item Pickups")]
        public static void FixOrbitingItemPickups()
        {
            var pickups = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int fixedCount = 0;

            foreach (var pickup in pickups)
            {
                Transform parent = pickup.transform.parent;
                if (parent == null) continue;

                bool rotatingAncestor = parent.GetComponentInParent<TutorialDummyAI>() != null ||
                                         parent.GetComponentInParent<ItemPickup>() != null;
                if (!rotatingAncestor) continue;

                pickup.transform.SetParent(null, worldPositionStays: true);
                fixedCount++;
                Debug.Log($"TutorialFixups: unparented \"{pickup.name}\" from a rotating object - " +
                           "it should now only spin in place.");
            }

            if (fixedCount == 0)
            {
                Debug.Log("TutorialFixups: no pickups were parented under a rotating object - if " +
                           "one is still orbiting, check what it's actually parented under by hand.");
            }
        }

        // ---- Tutorial UI look ----

        // Swaps the tutorial UI's flat-color panels and default font for the exact sliced
        // Space Expansion UI sprite and Kenney font the health/ammo/ability HUD bars already use
        // (see PlayerSceneSetup.BuildHealthHud) - both are already imported/configured as a
        // Sprite and Font by that existing setup, so this just loads and assigns them. Only
        // touches the TutorialUIController's own two style fields; does not rebuild or move any
        // UI element.
        [MenuItem("Tools/Tutorial/Polish Tutorial UI")]
        public static void PolishTutorialUi()
        {
            var ui = Object.FindFirstObjectByType<TutorialUIController>(FindObjectsInactive.Include);
            if (ui == null)
            {
                Debug.LogWarning("TutorialFixups: no TutorialUIController found in the scene.");
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HudPanelSpritePath);
            var font = AssetDatabase.LoadAssetAtPath<Font>(HudFontPath);
            if (sprite == null) Debug.LogWarning($"TutorialFixups: no sprite found at {HudPanelSpritePath}.");
            if (font == null) Debug.LogWarning($"TutorialFixups: no font found at {HudFontPath}.");

            var so = new SerializedObject(ui);
            so.FindProperty("panelSprite").objectReferenceValue = sprite;
            so.FindProperty("hudFont").objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("TutorialFixups: applied the HUD panel sprite/font to the tutorial UI - " +
                      "since it self-builds at Play mode start, enter Play mode to see the result.");
        }

        // ---- Attacks passing through the practice dummy ----

        // A travelling shot (BossProjectile) only registers a hit via Unity's own OnTriggerEnter,
        // which requires the target to have SOME Collider (trigger or not - the projectile
        // already carries its own trigger SphereCollider + kinematic Rigidbody, see
        // Enemies/BossProjectile.cs). A hand-placed dummy that never got a Collider added is the
        // single most common cause of "shots pass straight through" - this only adds one if
        // missing, sized from the model's actual render bounds, and never touches its transform.
        [MenuItem("Tools/Tutorial/Fix Training Dummy Hit Collider")]
        public static void FixTrainingDummyHitCollider()
        {
            var dummies = Object.FindObjectsByType<TutorialDummyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (dummies.Length == 0)
            {
                Debug.LogWarning("TutorialFixups: no TutorialDummyAI found in the scene.");
                return;
            }

            int enemyLayer = LayerMask.NameToLayer("Enemy");

            foreach (var dummy in dummies)
            {
                GameObject go = dummy.gameObject;

                if (go.GetComponent<Collider>() == null)
                {
                    Bounds bounds = GetLocalRenderBounds(go);
                    var collider = go.AddComponent<CapsuleCollider>();
                    collider.center = bounds.center;
                    collider.height = Mathf.Max(bounds.size.y, 0.1f);
                    collider.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
                    Debug.Log($"TutorialFixups: added a CapsuleCollider to \"{go.name}\" (had none).");
                }
                else
                {
                    Debug.Log($"TutorialFixups: \"{go.name}\" already has a Collider - leaving it as-is.");
                }

                // BossProjectile's OnTriggerEnter checks the hit collider's layer against
                // PlayerCombat's Enemy Hit Mask (defaults to Everything, but worth guaranteeing).
                if (enemyLayer >= 0)
                {
                    foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                    {
                        child.gameObject.layer = enemyLayer;
                    }
                }
            }
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

        // ---- Multi-piece gates ----

        // Select every TutorialGate piece that makes up one physical barrier (e.g. several panels
        // spanning one doorway) and run this - the first selected piece becomes the "primary"
        // (the one you drag into TutorialManager's field for that boundary) and the rest are
        // wired into its Linked Gates list, so Open() cascades to all of them at once. Only sets
        // that one field; nothing is moved or restyled.
        [MenuItem("Tools/Tutorial/Link Selected Gates")]
        public static void LinkSelectedGates()
        {
            var gates = Selection.gameObjects
                .Select(go => go.GetComponent<TutorialGate>())
                .Where(gate => gate != null)
                .ToArray();

            if (gates.Length < 2)
            {
                Debug.LogWarning("TutorialFixups: select two or more TutorialGate objects (the " +
                                  "pieces of one barrier) before running Link Selected Gates.");
                return;
            }

            var primary = gates[0];
            var rest = gates.Skip(1).ToArray();

            var so = new SerializedObject(primary);
            var linkedProperty = so.FindProperty("linkedGates");
            linkedProperty.arraySize = rest.Length;
            for (int i = 0; i < rest.Length; i++)
            {
                linkedProperty.GetArrayElementAtIndex(i).objectReferenceValue = rest[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"TutorialFixups: linked {rest.Length} gate(s) to \"{primary.name}\" - wire " +
                      $"\"{primary.name}\" (only) into the TutorialManager field for this boundary.");
        }

        // ---- Barrier look ----

        // Recolors every TutorialGate already in the scene to a transparent yellow energy-barrier
        // look, wherever its renderer(s) actually are (the gate's own primitive mesh, or child
        // meshes if it was grouped under a parent) - never touches transform, collider, or the
        // Linked Gates wiring.
        [MenuItem("Tools/Tutorial/Style Gates As Energy Barriers")]
        public static void StyleGatesAsEnergyBarriers()
        {
            var gates = Object.FindObjectsByType<TutorialGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (gates.Length == 0)
            {
                Debug.LogWarning("TutorialFixups: no TutorialGate found in the scene.");
                return;
            }

            Material barrier = CreateOrLoadBarrierMaterial();
            int styled = 0;
            foreach (var gate in gates)
            {
                foreach (var renderer in gate.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterial = barrier;
                    styled++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"TutorialFixups: applied the transparent yellow barrier material to {styled} renderer(s) across {gates.Length} gate(s).");
        }

        private static Material CreateOrLoadBarrierMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(BarrierMaterialPath);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(BarrierMaterialPath)?.Replace("\\", "/"));

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(BarrierMaterialPath) };

            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.15f, 0.35f));
            material.SetColor("_EmissionColor", new Color(1.2f, 0.9f, 0.1f));
            material.EnableKeyword("_EMISSION");
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.6f);

            AssetDatabase.CreateAsset(material, BarrierMaterialPath);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
