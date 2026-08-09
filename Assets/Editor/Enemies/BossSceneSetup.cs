using System.Linq;
using CharacterEditor;
using Combat;
using Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EnemiesEditor
{
    /// Builds the two-stage Barbara the Bee boss (Astronaut Stage 1 + Mech Stage 2) - the
    /// AnimatorControllers, measured hit colliders, Health/AI components, and the
    /// BossFightController that wires the Stage 1 -> Stage 2 transformation between them. Called
    /// from EnemySceneSetup.AddEnemiesToScene in place of the old untouched placeholders.
    public static class BossSceneSetup
    {
        private const string ModelFolder = "Assets/Art/Models/Characters/";
        private const string AnimationFolder = "Assets/Art/Animations/";
        private const string MaterialFolder = "Assets/Art/Materials/";

        // Imported Asset Store packs (optional - BossMechAI/PlayerCombat fall back to their
        // procedural look if any of these fail to load, e.g. the pack hasn't been imported). Only
        // the transformation-cutscene burst still uses Free Quick Effects Vol.1 - the mech's
        // machine guns and top-down attacks were reworked onto Lana Studio's Casual RPG VFX (see
        // LanaVfxFolder below); its old bullet/big-projectile/impact/fireball-conjure prefab paths
        // were removed rather than left dangling.
        private const string VfxPackFolder = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/";
        // The Creepy Cat pack (Effect_06/Effect_07) has been removed from the project - these now
        // point at the still-present Free Quick Effects Vol.1 pack instead.
        private const string TransformationBurstEffectPath = VfxPackFolder + "vfx_Explosion_02.prefab";

        // Lana Studio "Casual RPG VFX" - imported for the projectile/hit-effect rework (dark magic
        // player shot, light bolts for the astronaut/mech machine guns, magic bolts for the mech's
        // heavy machine gun, top-down ground-target attacks replacing the mech's Dance fireballs).
        private const string LanaVfxFolder = "Assets/Lana Studio/Casual RPG VFX/Prefabs/";
        private const string LightProjectilePath = LanaVfxFolder + "Range_attack/Projectiles_light.prefab";
        private const string LightImpactPath = LanaVfxFolder + "Range_attack/Hit_light.prefab";
        private const string MagicProjectilePath = LanaVfxFolder + "Range_attack/Projectiles_magic.prefab";
        private const string MagicImpactPath = LanaVfxFolder + "Range_attack/Hit_magic.prefab";
        private const string TopDownBeamPath = LanaVfxFolder + "Top_down_attack/top_down_beam_line_blue.prefab";
        private const string TopDownRocketPath = LanaVfxFolder + "Top_down_attack/top_down_rocket_circle_red.prefab";

        private static readonly Vector3 AstronautScale = new Vector3(2f, 2f, 2f);
        private static readonly Vector3 MechScale = new Vector3(4f, 4f, 4f);

        private static readonly string[] AstronautLoopClipNames = { "Idle_Gun", "Walk_Gun", "Run_Gun", "Run_Gun_Shoot" };
        private static readonly string[] MechLoopClipNames = { "Idle", "Walk" };

        private static readonly string[] LowerBodyBoneNameFragments =
        {
            "leg", "foot", "toe", "hip", "pelvis", "thigh", "shin", "calf",
        };

        public static void BuildBossFight(Transform parent, Vector3 position, int enemyLayer, System.Func<string, Material> getOrLoadPaletteMaterial)
        {
            var astronautModel = LoadModel("Astronaut_BarbaraTheBee");
            var mechModel = LoadModel("Mech_BarbaraTheBee");
            if (astronautModel == null || mechModel == null) return;

            var bossRoot = new GameObject("BossFight_BarbaraTheBee");
            bossRoot.transform.SetParent(parent, false);
            bossRoot.transform.localPosition = position;

            var (astronautGo, astronautHealth) = BuildBossAstronaut(astronautModel, bossRoot.transform, Vector3.zero, enemyLayer, getOrLoadPaletteMaterial);
            var (mechGo, mechAi, mechCollider) = BuildBossMech(mechModel, bossRoot.transform, new Vector3(0f, 0f, 3f), enemyLayer, getOrLoadPaletteMaterial);

            var controller = bossRoot.AddComponent<BossFightController>();
            var so = new SerializedObject(controller);
            so.FindProperty("astronautHealth").objectReferenceValue = astronautHealth;
            so.FindProperty("mechRoot").objectReferenceValue = mechGo;
            so.FindProperty("mechAi").objectReferenceValue = mechAi;
            so.FindProperty("mechCollider").objectReferenceValue = mechCollider;
            so.FindProperty("importedBurstEffectPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(TransformationBurstEffectPath);
            so.ApplyModifiedProperties();
        }

        private static (GameObject go, Health health) BuildBossAstronaut(
            GameObject model, Transform parent, Vector3 localPosition, int enemyLayer, System.Func<string, Material> getOrLoadPaletteMaterial)
        {
            ModelAnimationUtility.ConfigureAnimationLooping(model, AstronautLoopClipNames);
            var controller = BuildBossAstronautController(model, AnimationFolder + "AC_BossAstronaut.controller");
            var material = getOrLoadPaletteMaterial(MaterialFolder + "M_Astronaut_BarbaraTheBee.mat");

            var instance = SpawnModel(model, "Boss_Astronaut_BarbaraTheBee", parent, localPosition, AstronautScale, material, enemyLayer);

            // BossAstronautAI is CharacterController-driven (like EnemyLargeAI/BossMechAI), not a
            // CapsuleCollider-only flyer. Astronaut_BarbaraTheBee shares its CharacterArmature rig
            // with the player model, so this uses the same fixed local-space dimensions as the
            // player's own capsule (PlayerSceneSetup.BuildPlayer: center (0, 1.275, 0), height
            // 2.55, radius 0.55 - the comment here previously cited stale/incorrect values (0.35/2)
            // that predated the player's own capsule being tuned up, leaving this collider visibly
            // too small relative to the rendered (2x-scaled) model) instead of measuring render
            // bounds - GetLocalRenderBounds samples the SkinnedMeshRenderer's bind/T-pose bounds at
            // edit time (arms spread far wider than the standing idle pose), which produced a
            // radius wide enough to invalidate the capsule and made the boss hover instead of
            // resting on the ground. CharacterController dimensions scale with the transform's
            // lossyScale automatically, so these local-space values become 2x in world space to
            // match AstronautScale.
            var characterController = instance.AddComponent<CharacterController>();
            characterController.center = new Vector3(0f, 1.275f, 0f);
            characterController.height = 2.55f;
            characterController.radius = 0.55f;

            WireAnimator(instance, controller);
            var health = instance.AddComponent<Health>();
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").floatValue = 300f;
            healthSo.ApplyModifiedProperties();
            var astronautAi = instance.AddComponent<BossAstronautAI>();

            // Same local offset PlayerSceneSetup.BuildCombatAndEmotes uses for the player's own
            // muzzle - same rig/proportions, so it lines up with the held gun instead of defaulting
            // to the root transform (which sits at the feet, which is why bolts were firing from
            // the ground).
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(instance.transform, false);
            muzzle.localPosition = new Vector3(0.208f, 1.574f, 2.558f); // hand-tuned in Editor
            var astronautSo = new SerializedObject(astronautAi);
            astronautSo.FindProperty("muzzle").objectReferenceValue = muzzle;
            astronautSo.FindProperty("rangedProjectileVisualPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(LightProjectilePath);
            astronautSo.FindProperty("rangedImpactEffectPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(LightImpactPath);
            // Lana Studio's Range_attack prefabs are authored travelling along local +Y, not the
            // +Z BossProjectile's LookRotation aligns to the aim direction - without this the
            // bolt rendered as a near-vertical column regardless of aim.
            astronautSo.FindProperty("rangedProjectileRotationOffset").quaternionValue = Quaternion.Euler(0f, 90f, 0f);
            astronautSo.ApplyModifiedProperties();


            return (instance, health);
        }

        private static (GameObject go, BossMechAI ai, Collider collider) BuildBossMech(
            GameObject model, Transform parent, Vector3 localPosition, int enemyLayer, System.Func<string, Material> getOrLoadPaletteMaterial)
        {
            ModelAnimationUtility.ConfigureAnimationLooping(model, MechLoopClipNames);
            var controller = BuildBossMechController(model, AnimationFolder + "AC_BossMech.controller");
            var material = getOrLoadPaletteMaterial(MaterialFolder + "M_Mech_BarbaraTheBee.mat");

            var instance = SpawnModel(model, "Boss_Mech_BarbaraTheBee", parent, localPosition, MechScale, material, enemyLayer);

            // Fixed local-space dimensions rather than GetLocalRenderBounds - same reasoning as
            // BuildBossAstronaut: measuring the SkinnedMeshRenderer at edit time samples whatever
            // bind/reference pose it's sitting in (not the actual standing Idle pose), which can
            // skew the center/height/radius enough to invalidate the capsule and leave the
            // CharacterController's ground-contact point floating above the visible mesh.
            //
            // The original (0, 1.1, 0) / height 2.2 / radius 0.55 left the collider visibly far
            // too small - only wrapping roughly the hip-to-knee area, well below the head/
            // shoulders - the same class of stale-undersized-collider bug as BuildBossAstronaut's
            // fix above. Re-derived from the vendor OBJ export's actual measured mesh bounds
            // (asset packs/.../Characters/OBJ/Mech_BarbaraTheBee.obj - Y 2.882, X 2.912, Z 2.173)
            // scaled proportionally against Astronaut_BarbaraTheBee.obj's own measured bounds
            // (Y 3.025, Z 1.028) and the astronaut's now-correct Unity capsule (height 2.55,
            // radius 0.55) as a calibration reference - the mech's raw mesh is ~0.95x the
            // astronaut's height but ~2.1x its depth (a wide, low, 4-legged stance, not a taller
            // biped), so the fix widens the radius more than it grows the height. Still an
            // estimate, not an in-Editor measurement - if it's off, the CharacterController's
            // Center/Height/Radius are plain Inspector fields; report back the values you find
            // work (same workflow as the muzzle transforms) and they'll be baked in here.
            var bounds = new Bounds(new Vector3(0f, 1.6f, 0f), new Vector3(1.5f, 2.9f, 1.5f));
            var characterController = instance.AddComponent<CharacterController>();
            characterController.center = bounds.center;
            characterController.height = bounds.size.y;
            characterController.radius = 0.75f;

            // Supplementary trigger hit collider for the legs - a single capsule fundamentally
            // cannot wrap both the torso AND a 4-legged splayed stance without either missing the
            // legs entirely (a torso-sized capsule, what CharacterController above is) or being
            // absurdly oversized (a capsule wide enough to reach the full leg spread would engulf
            // huge empty space between the legs too). This is a second, separate collider purely
            // for hit registration - BossProjectile's OnTriggerEnter only needs the hit collider's
            // GameObject to sit on the Enemy layer and resolve an IDamageable via
            // GetComponentInParent, so this works automatically alongside the CharacterController
            // without touching movement/grounding at all. Positioned low and wide to span the leg
            // footprint; a rough estimate like the CharacterController above - report back exact
            // Center/Radius/Height found in Play mode and they'll be baked in here.
            var legHitCollider = instance.AddComponent<CapsuleCollider>();
            legHitCollider.isTrigger = true;
            legHitCollider.direction = 0; // capsule's long axis along local X - spans left/right leg spread
            legHitCollider.center = new Vector3(0f, 0.7f, 0f);
            legHitCollider.radius = 1.3f;
            legHitCollider.height = 3.2f;

            WireAnimator(instance, controller);
            var health = instance.AddComponent<Health>();
            // Boss stage 2 should meaningfully outlast stage 1 - default Health.maxHealth (100,
            // shared with the three basic enemies) made the mech phase feel too short given how
            // much spectacle (cutscene, multi-attack rotation) leads into it.
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").floatValue = 800f;
            healthSo.ApplyModifiedProperties();

            var ai = instance.AddComponent<BossMechAI>();

            // Approximate arm-cannon positions (shoulder height, either side, slightly forward) -
            // measured against the fixed bounds above, not the actual rig, since there's no bone
            // data to query here. "Forward" is local +Z: SpawnModel applies a 180-degree yaw to
            // every boss/enemy, so local +Z is what ends up facing the model's actual front in
            // world space (see FacePlayer/other boss facing code for the same convention). Nudge
            // muzzleLeft/muzzleRight by hand in the Inspector if they don't line up with the guns.
            var muzzleLeft = new GameObject("MuzzleLeft").transform;
            muzzleLeft.SetParent(instance.transform, false);
            muzzleLeft.localPosition = new Vector3(-1.001f, 1.943f, 0.677f); // hand-tuned in Editor

            var muzzleRight = new GameObject("MuzzleRight").transform;
            muzzleRight.SetParent(instance.transform, false);
            muzzleRight.localPosition = new Vector3(1.01f, 2.0189f, 0.612f); // hand-tuned in Editor

            // Shoot_Small -> Projectiles_light, Shoot_Big -> Projectiles_magic (reskin of the
            // existing bullet/missile burst attacks - see BossMechAI's Shoot Small/Big headers).
            var lightVisual = AssetDatabase.LoadAssetAtPath<GameObject>(LightProjectilePath);
            var lightImpact = AssetDatabase.LoadAssetAtPath<GameObject>(LightImpactPath);
            var magicVisual = AssetDatabase.LoadAssetAtPath<GameObject>(MagicProjectilePath);
            var magicImpact = AssetDatabase.LoadAssetAtPath<GameObject>(MagicImpactPath);
            var topDownBeam = AssetDatabase.LoadAssetAtPath<GameObject>(TopDownBeamPath);
            var topDownRocket = AssetDatabase.LoadAssetAtPath<GameObject>(TopDownRocketPath);
            if (lightVisual == null) Debug.LogWarning($"BossSceneSetup: no light-bolt visual at {LightProjectilePath} - falling back to the procedural look. Import Lana Studio's Casual RPG VFX first.");
            if (magicVisual == null) Debug.LogWarning($"BossSceneSetup: no magic-bolt visual at {MagicProjectilePath} - falling back to the procedural look.");
            if (topDownBeam == null) Debug.LogWarning($"BossSceneSetup: no top-down beam prefab at {TopDownBeamPath} - TopDownBeam attack will skip its VFX/damage.");
            if (topDownRocket == null) Debug.LogWarning($"BossSceneSetup: no top-down rocket prefab at {TopDownRocketPath} - TopDownRocket attack will skip its VFX/damage.");

            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("chaseSpeed").floatValue = 9f;
            aiSo.FindProperty("bulletDamage").floatValue = 3f;
            aiSo.FindProperty("firePointLeft").objectReferenceValue = muzzleLeft;
            aiSo.FindProperty("firePointRight").objectReferenceValue = muzzleRight;
            aiSo.FindProperty("bulletVisualPrefab").objectReferenceValue = lightVisual;
            aiSo.FindProperty("impactEffectPrefab").objectReferenceValue = lightImpact;
            aiSo.FindProperty("bigProjectileVisualPrefab").objectReferenceValue = magicVisual;
            aiSo.FindProperty("bigProjectileImpactEffectPrefab").objectReferenceValue = magicImpact;
            // Lana Studio's Range_attack prefabs are authored travelling along local +Y, not +Z -
            // see BossAstronautAI's rangedProjectileRotationOffset wiring above for the full
            // explanation.
            aiSo.FindProperty("bulletVisualRotationOffset").quaternionValue = Quaternion.Euler(0f, 90f, 0f);
            aiSo.FindProperty("bigProjectileVisualRotationOffset").quaternionValue = Quaternion.Euler(0f, 90f, 0f);
            aiSo.FindProperty("topDownBeamPrefab").objectReferenceValue = topDownBeam;
            aiSo.FindProperty("topDownRocketPrefab").objectReferenceValue = topDownRocket;
            aiSo.ApplyModifiedProperties();


            // Inert until BossFightController activates it post-cutscene - scale 0 is applied by
            // BossFightController.Awake at runtime (keeping the built scene's authored transform
            // at MechScale for editing), AI/collider disabled here so it can't act or block
            // raycasts/collisions while hidden.
            ai.enabled = false;
            characterController.enabled = false;

            return (instance, ai, characterController);
        }

        private static GameObject LoadModel(string fileName)
        {
            string path = ModelFolder + fileName + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                Debug.LogError($"BossSceneSetup: no model found at {path}. " +
                                "Make sure the FBX has been imported by Unity first.");
            }
            return model;
        }

        private static GameObject SpawnModel(GameObject model, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, int layer)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            instance.transform.localScale = scale;

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

        private static void AddHealthBar(GameObject instance, Bounds localBounds, Health health)
        {
            const float barWidth = 1.4f;
            const float barHeight = 0.16f;
            const float headroom = 0.4f;

            // EnemyHealthBarUI.LateUpdate adds this offset to the anchor's world position
            // directly (not transformed by the anchor's own scale/rotation), so the local-space
            // bounds height measured above has to be scaled back up to world units here - unlike
            // EnemySceneSetup's basic enemies (scale 1, local == world), these bosses are 2x/4x.
            float worldHeight = (localBounds.max.y + headroom) * instance.transform.lossyScale.y;
            var offset = new Vector3(0f, worldHeight, 0f);

            var barGo = new GameObject(instance.name + "_HealthBar", typeof(Canvas));
            var canvas = barGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = barGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(barWidth, barHeight);

            var canvasGroup = barGo.AddComponent<CanvasGroup>();

            var backdrop = new GameObject("Backdrop", typeof(UnityEngine.UI.Image));
            backdrop.transform.SetParent(barGo.transform, false);
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            backdrop.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var fill = new GameObject("Fill", typeof(UnityEngine.UI.Image));
            fill.transform.SetParent(barGo.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(0.02f, 0.02f);
            fillRect.offsetMax = new Vector2(-0.02f, -0.02f);
            var fillImage = fill.GetComponent<UnityEngine.UI.Image>();

            var healthBarUi = barGo.AddComponent<EnemyHealthBarUI>();
            healthBarUi.Initialize(instance.transform, offset, fillRect, fillImage, canvasGroup);
            healthBarUi.Bind(health);

            // Parented purely for Hierarchy/prefab organization (see BuildBossFight) -
            // EnemyHealthBarUI drives world position/rotation itself every LateUpdate regardless
            // of hierarchy, but the instance's own 2x/4x scale would otherwise inflate the bar's
            // visual size once it's a child, since a WorldSpace Canvas sizes by its RectTransform
            // times lossyScale - compensate by dividing the parent's scale back out.
            Vector3 parentScale = instance.transform.lossyScale;
            barGo.transform.SetParent(instance.transform, false);
            barGo.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
        }

        private static void WireAnimator(GameObject instance, AnimatorController controller)
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        /// Base layer: Idle/Move blend tree (Idle_Gun/Walk_Gun/Run_Gun) plus AnyState one-shots
        /// for Punch/Weapon (melee), Taunt (Wave), Stagger (No - the "recollecting itself" beat),
        /// HitReact, Death. Second Arms override layer (upper-body-masked, same rig/mask approach
        /// as AC_Player.controller) carries the ranged Shoot pose, toggled on/off by
        /// BossAstronautAI around its ranged burst so it doesn't fight the base layer's full-body
        /// one-shots the rest of the time.
        private static AnimatorController BuildBossAstronautController(GameObject model, string controllerPath)
        {
            EnsureFolder("Assets/Art/Animations");
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var sourceClips = ModelAnimationUtility.LoadSourceClips(model, out string modelPath);
            AnimationClip Get(string clipName) => ModelAnimationUtility.GetClip(sourceClips, modelPath, clipName);

            AnimationClip idleGun = Get("Idle_Gun");
            AnimationClip walkGun = Get("Walk_Gun");
            AnimationClip runGun = Get("Run_Gun");
            AnimationClip runGunShoot = Get("Run_Gun_Shoot");
            AnimationClip punch = Get("Punch");
            AnimationClip weapon = Get("Weapon");
            AnimationClip wave = Get("Wave");
            AnimationClip no = Get("No");
            AnimationClip hitReact = Get("HitReact");
            AnimationClip death = Get("Death");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Firing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Punch", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Weapon", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Taunt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Stagger", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitReact", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (var childState in sm.states.ToList())
            {
                sm.RemoveState(childState.state);
            }

            var idleState = sm.AddState("Idle");
            idleState.motion = idleGun;

            var moveTree = new BlendTree
            {
                name = "Move",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(moveTree, controller);
            if (walkGun != null) moveTree.AddChild(walkGun, 0.5f);
            if (runGun != null) moveTree.AddChild(runGun, 1f);

            var moveState = sm.AddState("Move");
            moveState.motion = moveTree;

            var punchState = sm.AddState("Punch");
            punchState.motion = punch;

            var weaponState = sm.AddState("Weapon");
            weaponState.motion = weapon;

            var tauntState = sm.AddState("Taunt");
            tauntState.motion = wave;

            var staggerState = sm.AddState("Stagger");
            staggerState.motion = no;

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

            AddOneShot(sm, idleState, punchState, "Punch");
            AddOneShot(sm, idleState, weaponState, "Weapon");
            AddOneShot(sm, idleState, tauntState, "Taunt");
            AddOneShot(sm, idleState, staggerState, "Stagger");

            // Added last so Death/HitReact win AnyState evaluation priority over a still-armed
            // attack trigger the same frame the killing blow lands - same ordering reasoning as
            // EnemySceneSetup.BuildGroundController.
            var anyToHitReact = sm.AddAnyStateTransition(hitReactState);
            anyToHitReact.canTransitionToSelf = false;
            anyToHitReact.hasExitTime = false;
            anyToHitReact.duration = 0.05f;
            anyToHitReact.AddCondition(AnimatorConditionMode.If, 0, "HitReact");

            var hitReactToIdle = hitReactState.AddTransition(idleState);
            hitReactToIdle.hasExitTime = true;
            hitReactToIdle.exitTime = 0.9f;
            hitReactToIdle.duration = 0.1f;

            var anyToDeath = sm.AddAnyStateTransition(deathState);
            anyToDeath.canTransitionToSelf = false;
            anyToDeath.hasExitTime = false;
            anyToDeath.duration = 0.05f;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");

            BuildArmsLayer(controller, model, idleGun, runGunShoot);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddOneShot(AnimatorStateMachine sm, AnimatorState idleState, AnimatorState targetState, string triggerParam)
        {
            var anyToTarget = sm.AddAnyStateTransition(targetState);
            anyToTarget.canTransitionToSelf = false;
            anyToTarget.hasExitTime = false;
            anyToTarget.duration = 0.05f;
            anyToTarget.AddCondition(AnimatorConditionMode.If, 0, triggerParam);

            var targetToIdle = targetState.AddTransition(idleState);
            targetToIdle.hasExitTime = true;
            targetToIdle.exitTime = 0.9f;
            targetToIdle.duration = 0.1f;
        }

        /// Two-state upper-body-masked Arms layer: Arms_Idle (Idle_Gun pose) <-> Arms_Shoot
        /// (Run_Gun_Shoot), toggled purely by the Firing bool. Simpler than AC_Player's four-state
        /// version since this boss doesn't need a separate walk/run/jump shoot pose - default
        /// weight 0, BossAstronautAI switches it to 1 only for the ranged burst.
        private static void BuildArmsLayer(AnimatorController controller, GameObject model, AnimationClip idleClip, AnimationClip shootClip)
        {
            controller.AddLayer("Arms");
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer armsLayer = layers[layers.Length - 1];
            armsLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            armsLayer.defaultWeight = 0f;
            armsLayer.avatarMask = BuildUpperBodyMask(model, AnimationFolder + "AM_BossAstronautUpperBody.mask");

            AnimatorStateMachine armsSm = armsLayer.stateMachine;

            var armsIdleState = armsSm.AddState("Arms_Idle");
            armsIdleState.motion = idleClip;
            armsSm.defaultState = armsIdleState;

            var armsShootState = armsSm.AddState("Arms_Shoot");
            armsShootState.motion = shootClip;

            var idleToShoot = armsIdleState.AddTransition(armsShootState);
            idleToShoot.hasExitTime = false;
            idleToShoot.duration = 0.1f;
            idleToShoot.AddCondition(AnimatorConditionMode.If, 0, "Firing");

            var shootToIdle = armsShootState.AddTransition(armsIdleState);
            shootToIdle.hasExitTime = false;
            shootToIdle.duration = 0.1f;
            shootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Firing");

            layers[layers.Length - 1] = armsLayer;
            controller.layers = layers;
        }

        private static AvatarMask BuildUpperBodyMask(GameObject model, string maskPath)
        {
            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath) != null)
            {
                AssetDatabase.DeleteAsset(maskPath);
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

            AssetDatabase.CreateAsset(mask, maskPath);
            return mask;
        }

        /// Single layer: Idle/Walk blend tree, AnyState one-shots for TopDownBeam/TopDownRocket/
        /// Shoot_Small/Shoot_Big/Jump (chained into Jump_Landing), HitRecieve_1/HitRecieve_2
        /// (BossMechAI drives these directly via Health.Hit rather than the generic HitReact
        /// path), Death. HitReact/Death params are still declared (unused for HitReact) purely so
        /// Health's own SetTrigger calls don't warn about a missing animator parameter.
        /// TopDownBeam/TopDownRocket reuse the old Dance clip (if present in the source FBX) as a
        /// simple animated hold - the imported Lana Studio ground-target VFX carries the actual
        /// read for these attacks, not character animation, so no dedicated clip was authored.
        private static AnimatorController BuildBossMechController(GameObject model, string controllerPath)
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
            AnimationClip topDownHold = Get("Dance"); // reused as a simple animated hold - see doc comment above
            AnimationClip shootSmall = Get("Shoot_Small");
            AnimationClip shootBig = Get("Shoot_Big");
            AnimationClip jump = Get("Jump");
            AnimationClip jumpLanding = Get("Jump_Landing");
            AnimationClip hitRecieve1 = Get("HitRecieve_1");
            AnimationClip hitRecieve2 = Get("HitRecieve_2");
            AnimationClip death = Get("Death");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("TopDownBeam", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("TopDownRocket", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ShootSmall", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ShootBig", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitRecieve1", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitRecieve2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitReact", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (var childState in sm.states.ToList())
            {
                sm.RemoveState(childState.state);
            }

            var idleState = sm.AddState("Idle");
            idleState.motion = idle;

            var walkState = sm.AddState("Walk");
            walkState.motion = walk;

            var topDownBeamState = sm.AddState("TopDown_Beam");
            topDownBeamState.motion = topDownHold;

            var topDownRocketState = sm.AddState("TopDown_Rocket");
            topDownRocketState.motion = topDownHold;

            var shootSmallState = sm.AddState("Shoot_Small");
            shootSmallState.motion = shootSmall;

            var shootBigState = sm.AddState("Shoot_Big");
            shootBigState.motion = shootBig;

            var jumpState = sm.AddState("Jump");
            jumpState.motion = jump;

            var jumpLandingState = sm.AddState("Jump_Landing");
            jumpLandingState.motion = jumpLanding;

            var hitRecieve1State = sm.AddState("HitRecieve_1");
            hitRecieve1State.motion = hitRecieve1;

            var hitRecieve2State = sm.AddState("HitRecieve_2");
            hitRecieve2State.motion = hitRecieve2;

            var deathState = sm.AddState("Death");
            deathState.motion = death;

            sm.defaultState = idleState;

            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.15f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.15f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            AddOneShot(sm, idleState, topDownBeamState, "TopDownBeam");
            AddOneShot(sm, idleState, topDownRocketState, "TopDownRocket");
            AddOneShot(sm, idleState, shootSmallState, "ShootSmall");
            AddOneShot(sm, idleState, shootBigState, "ShootBig");
            AddOneShot(sm, idleState, hitRecieve1State, "HitRecieve1");
            AddOneShot(sm, idleState, hitRecieve2State, "HitRecieve2");

            // Jump chains straight into Jump_Landing rather than another AnyState trigger -
            // BossMechAI's JumpSlam coroutine drives the actual ascend/descend motion itself and
            // just fires the one Jump trigger; the clip transition alone carries the visual.
            var anyToJump = sm.AddAnyStateTransition(jumpState);
            anyToJump.canTransitionToSelf = false;
            anyToJump.hasExitTime = false;
            anyToJump.duration = 0.1f;
            anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            var jumpToLanding = jumpState.AddTransition(jumpLandingState);
            jumpToLanding.hasExitTime = true;
            jumpToLanding.exitTime = 0.85f;
            jumpToLanding.duration = 0.1f;

            var landingToIdle = jumpLandingState.AddTransition(idleState);
            landingToIdle.hasExitTime = true;
            landingToIdle.exitTime = 0.9f;
            landingToIdle.duration = 0.15f;

            var anyToDeath = sm.AddAnyStateTransition(deathState);
            anyToDeath.canTransitionToSelf = false;
            anyToDeath.hasExitTime = false;
            anyToDeath.duration = 0.05f;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
