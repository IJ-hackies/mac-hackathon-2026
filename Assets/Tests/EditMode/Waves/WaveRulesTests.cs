using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Waves.Tests
{
    public sealed class WaveRulesTests
    {
        [TestCase(1, "Regular")]
        [TestCase(5, "Arena1")]
        [TestCase(10, "Arena2")]
        [TestCase(15, "Arena1")]
        [TestCase(20, "Arena2")]
        public void ClassifiesEveryWaveOnItsTenWaveCycle(int wave, string expected)
        {
            Assert.That(Invoke("GetKind", wave).ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void RegularSpawnRulesReachTheirSpecifiedCaps()
        {
            Assert.That(Convert.ToInt32(Invoke("ActiveCap", 1)), Is.EqualTo(6));
            Assert.That(Convert.ToInt32(Invoke("ActiveCap", 100)), Is.EqualTo(40));
            Assert.That(Convert.ToSingle(Invoke("SpawnInterval", 1)), Is.EqualTo(2.2f).Within(.0001f));
            Assert.That(Convert.ToSingle(Invoke("SpawnInterval", 100)), Is.EqualTo(.55f).Within(.0001f));
        }

        [TestCase(1, 30f)]
        [TestCase(10, 30f)]
        [TestCase(11, 25f)]
        [TestCase(20, 25f)]
        [TestCase(21, 20f)]
        [TestCase(100, 20f)]
        public void RegularDurationUsesTheConfiguredWaveBands(int wave, float expectedSeconds)
        {
            Assert.That(Convert.ToSingle(Invoke("RegularDurationForWave", wave)), Is.EqualTo(expectedSeconds));
        }

        [Test]
        public void RegularEnemyMixUnlocksOnTheSpecifiedWaves()
        {
            AssertRegularMix(1, 10, 0, 0);
            AssertRegularMix(2, 7, 3, 0);
            AssertRegularMix(3, 5, 3, 2);
            AssertRegularMix(50, 5, 3, 2);
        }

        [Test]
        public void ArenaOneUsesUncappedFormulaAndExactIntegerMix()
        {
            Assert.That(Convert.ToInt32(Invoke("Arena1Count", 5)), Is.EqualTo(10));
            Assert.That(Convert.ToInt32(Invoke("Arena1Count", 25)), Is.EqualTo(30));
            object[] arguments = { 30, 0, 0, 0 };
            Rules.GetMethod("GetArena1Composition").Invoke(null, arguments);
            Assert.That(Convert.ToInt32(arguments[1]), Is.EqualTo(15));
            Assert.That(Convert.ToInt32(arguments[2]), Is.EqualTo(9));
            Assert.That(Convert.ToInt32(arguments[3]), Is.EqualTo(6));
        }

        [Test]
        public void EconomyAndStatsUseSpecifiedMultipliersAndCaps()
        {
            Assert.That(Convert.ToInt32(Invoke("KillGold", Enemy("Small"), 1)), Is.EqualTo(20));
            Assert.That(Convert.ToInt32(Invoke("KillGold", Enemy("Flying"), 11)), Is.EqualTo(50));
            Assert.That(Convert.ToInt32(Invoke("KillGold", Enemy("Large"), 21)), Is.EqualTo(90));
            Assert.That(Convert.ToSingle(Invoke("KillMultiplier", 1)), Is.EqualTo(1f));
            Assert.That(Convert.ToSingle(Invoke("KillMultiplier", 11)), Is.EqualTo(2f));
            Assert.That(Convert.ToSingle(Invoke("KillMultiplier", 21)), Is.EqualTo(3f));
            Assert.That(Convert.ToSingle(Invoke("KillMultiplier", 100)), Is.EqualTo(3f));
            Assert.That(Convert.ToInt32(Invoke("ArenaCompletionGold", Kind("Arena2"), 100)), Is.EqualTo(900));
            Assert.That(Convert.ToSingle(Invoke("HealthMultiplier", 11)), Is.EqualTo(2f));
            Assert.That(Convert.ToSingle(Invoke("DamageMultiplier", 5)), Is.EqualTo(1.3f).Within(.0001f));
            Assert.That(Convert.ToSingle(Invoke("MovementMultiplier", 100)), Is.EqualTo(2f));
            Assert.That(Convert.ToSingle(Invoke("AttackRateMultiplier", 100)), Is.EqualTo(2f));
            Assert.That(Convert.ToSingle(Invoke("ProjectileSpeedMultiplier", 100)), Is.EqualTo(2f));
        }

        [Test]
        public void FortuneRoundsEachIndividualGoldAwardAndPickupBudgetsAreFixed()
        {
            Assert.That(Convert.ToInt32(Invoke("GoldWithSpecialBonus", 20, true)), Is.EqualTo(23));
            Assert.That(Convert.ToInt32(Invoke("GoldWithSpecialBonus", 25, true)), Is.EqualTo(29));
            Assert.That(Convert.ToInt32(Invoke("GoldWithSpecialBonus", 300, true)), Is.EqualTo(345));
            Assert.That(Convert.ToInt32(Invoke("GoldWithSpecialBonus", 25, false)), Is.EqualTo(25));
            Assert.That(Convert.ToInt32(Rules.GetField("MedKitPickupsPerRegularWave").GetRawConstantValue()), Is.EqualTo(15));
            Assert.That(Convert.ToInt32(Rules.GetField("AmmoKitPickupsPerRegularWave").GetRawConstantValue()), Is.EqualTo(10));
        }

        [Test]
        public void PickupLayoutSpreadsSlotsAcrossTheGlobeAndWaveCompletionCleansThemUp()
        {
            Type spawnerType = RequireType("Gameplay.Waves.WavePickupSpawner, Assembly-CSharp");
            MethodInfo direction = spawnerType.GetMethod("EvenlyDistributedDirection", BindingFlags.Public | BindingFlags.Static);
            Assert.That(direction, Is.Not.Null);
            Vector3[] positions = new Vector3[15];
            for (int index = 0; index < positions.Length; index++)
            {
                positions[index] = (Vector3)direction.Invoke(null, new object[] { index, positions.Length, .35f });
                Assert.That(positions[index].magnitude, Is.EqualTo(1f).Within(.0001f));
            }
            for (int first = 0; first < positions.Length; first++)
            for (int second = first + 1; second < positions.Length; second++)
                Assert.That(Vector3.Dot(positions[first], positions[second]), Is.LessThan(.91f));

            object spawner = Activator.CreateInstance(spawnerType);
            GameObject pickup = new GameObject("Wave Pickup Test");
            GameObject directorRoot = new GameObject("Wave Director Test");
            try
            {
                Type directorType = RequireType("Gameplay.Waves.WaveDirector, Assembly-CSharp");
                Component director = directorRoot.AddComponent(directorType);
                MethodInfo spawn = spawnerType.GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance);
                PropertyInfo activeCount = spawnerType.GetProperty("ActiveCount", BindingFlags.Public | BindingFlags.Instance);
                FieldInfo directorSpawner = directorType.GetField("pickupSpawner", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo finishWave = directorType.GetMethod("FinishWave", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(spawn, Is.Not.Null);
                Assert.That(activeCount, Is.Not.Null);
                Assert.That(directorSpawner, Is.Not.Null);
                Assert.That(finishWave, Is.Not.Null);

                directorSpawner.SetValue(director, spawner);
                spawn.Invoke(spawner, new object[] { pickup, Vector3.zero, Quaternion.identity, null });
                Assert.That(Convert.ToInt32(activeCount.GetValue(spawner)), Is.EqualTo(1));
                finishWave.Invoke(director, null);
                Assert.That(Convert.ToInt32(activeCount.GetValue(spawner)), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(directorRoot);
                UnityEngine.Object.DestroyImmediate(pickup);
            }
        }

        [Test]
        public void RegularPickupSpawner_UsesTheFullFifteenAndTenBudgetsOnSafeGround()
        {
            Type samplerType = RequireType("Gameplay.Waves.WaveSurfaceSpawnSampler, Assembly-CSharp");
            Type spawnerType = RequireType("Gameplay.Waves.WavePickupSpawner, Assembly-CSharp");
            Type areaType = RequireType("Gameplay.Areas.GameplayArea, Gameplay.Areas");
            object sampler = Activator.CreateInstance(samplerType);
            object spawner = Activator.CreateInstance(spawnerType);

            GameObject planet = new GameObject("Pickup Test Planet", typeof(SphereCollider));
            GameObject player = new GameObject("Pickup Test Player");
            GameObject parent = new GameObject("Pickup Test Parent");
            GameObject healthPrefab = new GameObject("Health Budget Pickup");
            GameObject ammoPrefab = new GameObject("Ammo Budget Pickup");
            try
            {
                planet.GetComponent<SphereCollider>().radius = 10f;
                player.transform.position = Vector3.up * 10f;
                Physics.SyncTransforms();

                samplerType.GetMethod("ConfigurePlanet", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(sampler, new object[] { planet.transform });
                MethodInfo spawnRegular = spawnerType.GetMethod("SpawnRegularPickups",
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.That(spawnRegular, Is.Not.Null);
                spawnRegular.Invoke(spawner, new object[]
                {
                    sampler, planet.transform, player.transform, Array.CreateInstance(areaType, 0),
                    healthPrefab, true, ammoPrefab, true, parent.transform
                });

                PropertyInfo activeCount = spawnerType.GetProperty("ActiveCount",
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.That(Convert.ToInt32(activeCount?.GetValue(spawner)), Is.EqualTo(25));

                FieldInfo spawnedField = spawnerType.GetField("_spawned",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                int healthCount = 0;
                int ammoCount = 0;
                foreach (object entry in (IEnumerable)spawnedField?.GetValue(spawner))
                {
                    string name = ((GameObject)entry).name;
                    if (name.StartsWith("Health Budget Pickup", StringComparison.Ordinal)) healthCount++;
                    if (name.StartsWith("Ammo Budget Pickup", StringComparison.Ordinal)) ammoCount++;
                }
                Assert.That(healthCount, Is.EqualTo(15));
                Assert.That(ammoCount, Is.EqualTo(10));

                spawnerType.GetMethod("Cleanup", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(spawner, null);
                Assert.That(Convert.ToInt32(activeCount?.GetValue(spawner)), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(healthPrefab);
                UnityEngine.Object.DestroyImmediate(ammoPrefab);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(planet);
            }
        }

        [Test]
        public void SampleSceneSerializesAllThreeSpecialPickupPrefabs()
        {
            string scene = File.ReadAllText(Path.GetFullPath("Assets/Scenes/SampleScene.unity"));
            StringAssert.Contains(
                "healthPickupPrefab: {fileID: 4690128446073603275, guid: 677090fc93d804b179a1c84fff2729f1, type: 3}", scene);
            StringAssert.Contains(
                "ammoPickupPrefab: {fileID: 7516799705066395824, guid: a4cab9a8e350d49f3b994210ce54c422, type: 3}", scene);
            StringAssert.Contains(
                "thunderPickupPrefab: {fileID: 1598913225129411355, guid: 16cc5725a1ec54cd1b94e9971a4a2e73, type: 3}", scene);
        }

        [Test]
        public void EnemyPrefabsUseTheBalancedHealthAndMovementDefaults()
        {
            AssertPrefabFloat("Assets/Prefabs/Enemies/Enemy_Small.prefab", "Combat.Health", "maxHealth", 100f);
            AssertPrefabFloat("Assets/Prefabs/Enemies/Enemy_Small.prefab", "Enemies.EnemySmallAI", "approachSpeed", 4f);
            AssertPrefabFloat("Assets/Prefabs/Enemies/Enemy_Flying.prefab", "Combat.Health", "maxHealth", 120f);
            AssertPrefabFloat("Assets/Prefabs/Enemies/Enemy_Flying.prefab", "Enemies.EnemyFlyingAI", "wanderSpeed", 1.5f);
            AssertPrefabFloat("Assets/Prefabs/Enemies/Enemy_Flying.prefab", "Enemies.EnemyFlyingAI", "approachSpeed", 2.25f);
            AssertPrefabFloat("Assets/Prefabs/Enemies/Enemy_Large.prefab", "Combat.Health", "maxHealth", 150f);
            AssertPrefabFloat("Assets/Prefabs/Enemies/Enemy_Large.prefab", "Enemies.EnemyLargeAI", "walkSpeed", 2f);
            AssertPrefabFloat("Assets/Prefabs/Enemies/Enemy_Large.prefab", "Enemies.EnemyLargeAI", "runSpeed", 4.5f);
        }

        [Test]
        public void BarbaraPrefabUsesTheBalancedStageHealthChaseAndSmallBurstDamage()
        {
            const string prefabPath = "Assets/Prefabs/Enemies/BossFight_BarbaraTheBee.prefab";
            AssertPrefabChildFloat(prefabPath, "Boss_Astronaut_BarbaraTheBee", "Combat.Health", "maxHealth", 300f);
            AssertPrefabFloat(prefabPath, "Enemies.BossMechAI", "chaseSpeed", 9f);
            AssertPrefabFloat(prefabPath, "Enemies.BossMechAI", "bulletDamage", 3f);
            AssertPrefabFloat(prefabPath, "Enemies.BossMechAI", "bulletBurstCount", 80f);

            Assert.That(Convert.ToSingle(Invoke("HealthMultiplier", 10)), Is.EqualTo(1.9f).Within(.0001f));
            Assert.That(Convert.ToSingle(Invoke("BarbaraHealthMultiplier", 10)), Is.EqualTo(2.35f).Within(.0001f));
            Assert.That(300f * Convert.ToSingle(Invoke("BarbaraHealthMultiplier", 10)), Is.EqualTo(705f));
            // Wave 10 applies 1.675x damage, making the full Shoot Small burst 402 raw damage.
            Assert.That(80f * 3f * Convert.ToSingle(Invoke("DamageMultiplier", 10)), Is.EqualTo(402f));
        }

        [TestCase("Intermission", true)]
        [TestCase("ArenaTravel", false)]
        [TestCase("Regular", false)]
        [TestCase("ArenaSeal", false)]
        [TestCase("ArenaCombat", false)]
        [TestCase("GameOver", false)]
        public void BaseTeleportIsAvailableOnlyBetweenWaves(string phaseName, bool expected)
        {
            Type phaseType = RequireType("Gameplay.Waves.WavePhase, Assembly-CSharp");
            Type controllerType = RequireType("Gameplay.Waves.WaveGameController, Assembly-CSharp");
            MethodInfo method = controllerType.GetMethod(
                "IsTeleportAllowedDuring",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            object phase = Enum.Parse(phaseType, phaseName);
            Assert.That(Convert.ToBoolean(method.Invoke(null, new[] { phase })), Is.EqualTo(expected));
        }

        [Test]
        public void BaseTeleportRechecksPhaseAndRefreshesPausedRuntimeState()
        {
            float previousTimeScale = Time.timeScale;
            var ground = new GameObject("Ground", typeof(BoxCollider));
            var directorObject = new GameObject("Wave Director");
            var rig = new GameObject("Player Rig");
            var player = new GameObject("Player");
            var cameraPivot = new GameObject("Camera Pivot");
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var staleAreaObject = new GameObject("Stale Area");

            try
            {
                ground.transform.position = new Vector3(0f, -0.5f, 0f);
                ground.transform.localScale = new Vector3(100f, 1f, 100f);

                player.transform.SetParent(rig.transform, true);
                player.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
                Component playerController = player.AddComponent(
                    RequireType("Player.PlayerController, Assembly-CSharp"));

                cameraPivot.transform.SetParent(rig.transform, true);
                cameraObject.transform.SetParent(cameraPivot.transform, false);
                Component cameraController = cameraPivot.AddComponent(
                    RequireType("Player.ThirdPersonCameraController, Assembly-CSharp"));
                InvokeInstance(cameraController, "SetTarget", player.transform);

                Type areaType = RequireType("Gameplay.Areas.GameplayArea, Gameplay.Areas");
                Component areaTracker = rig.AddComponent(
                    RequireType("Gameplay.Areas.PlayerAreaTracker, Gameplay.Areas"));
                InvokeInstance(
                    areaTracker,
                    "Configure",
                    player.transform,
                    Array.CreateInstance(areaType, 0),
                    false);

                Component director = directorObject.AddComponent(
                    RequireType("Gameplay.Waves.WaveDirector, Assembly-CSharp"));
                InvokeInstance(
                    director,
                    "ConfigureReferences",
                    player.transform,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);

                Component gameController = rig.AddComponent(
                    RequireType("Gameplay.Waves.WaveGameController, Assembly-CSharp"));
                Vector3 remotePosition = new Vector3(20f, 1f, 0f);
                player.transform.position = remotePosition;
                Physics.SyncTransforms();

                Assert.That(Convert.ToBoolean(InvokeInstance(director, "TryStartNextWave")), Is.True);
                Time.timeScale = 0f;
                Assert.That(Convert.ToBoolean(InvokeInstance(gameController, "TryTeleportToBase")), Is.False);
                Assert.That(player.transform.position, Is.EqualTo(remotePosition));

                InvokeInstance(director, "BeginNewRun");
                Component staleArea = staleAreaObject.AddComponent(areaType);
                PropertyInfo currentArea = areaTracker.GetType().GetProperty(
                    "CurrentArea",
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.That(currentArea, Is.Not.Null);
                currentArea.SetValue(areaTracker, staleArea);

                Assert.That(Convert.ToBoolean(InvokeInstance(gameController, "TryTeleportToBase")), Is.True);
                Assert.That(player.transform.position.x, Is.EqualTo(0f).Within(.001f));
                Assert.That(player.transform.position.z, Is.EqualTo(0f).Within(.001f));
                Assert.That(player.transform.position.y, Is.EqualTo(1.02f).Within(.05f));
                Assert.That(currentArea.GetValue(areaTracker), Is.Null);

                Vector3 expectedCameraPivot = player.transform.position + Vector3.up * 3.2f;
                Assert.That(
                    Vector3.Distance(cameraPivot.transform.position, expectedCameraPivot),
                    Is.LessThan(.01f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(staleAreaObject);
                UnityEngine.Object.DestroyImmediate(rig);
                UnityEngine.Object.DestroyImmediate(directorObject);
                UnityEngine.Object.DestroyImmediate(ground);
            }
        }

        [Test]
        public void WaveSpawnedEnemiesTripleTheirAuthoredScale()
        {
            var enemy = new GameObject("Wave Enemy");
            try
            {
                enemy.transform.localScale = new Vector3(2f, 3f, 4f);
                MethodInfo applySize = RequireType("Gameplay.Waves.WaveDirector, Assembly-CSharp").GetMethod(
                    "ApplyEnemySizeMultiplier",
                    BindingFlags.NonPublic | BindingFlags.Static);

                Assert.That(applySize, Is.Not.Null);
                applySize.Invoke(null, new object[] { enemy.transform });

                Assert.That(enemy.transform.localScale, Is.EqualTo(new Vector3(6f, 9f, 12f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void ArenaObjectiveShowsDefeatedAndRemainingCounts()
        {
            GameObject root = new GameObject("Arena Objective", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                Component view = CreateArenaObjectiveView(root, out Text title, out Text objective, out Text detail,
                    out GameObject healthRoot, out _, out _);

                InvokeInstance(view, "SetArena1Progress", 3, 7);

                Assert.That(title.text, Is.EqualTo("ARENA 1 // SWARM"));
                Assert.That(objective.text, Is.EqualTo("3 DEFEATED  //  7 LEFT"));
                Assert.That(detail.text, Is.EqualTo("CLEAR ALL HOSTILES"));
                Assert.That(healthRoot.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ArenaObjectiveShowsBossHealthAsBarAndValue()
        {
            GameObject root = new GameObject("Arena Objective", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                Component view = CreateArenaObjectiveView(root, out _, out Text objective, out Text detail,
                    out GameObject healthRoot, out Image healthFill, out Text healthText);

                InvokeInstance(view, "SetArena2Health", "STAGE 2 // MECH", 325f, 800f, "BARBARA");

                Assert.That(objective.text, Is.EqualTo("BARBARA"));
                Assert.That(detail.text, Is.EqualTo("STAGE 2 // MECH"));
                Assert.That(healthRoot.activeSelf, Is.True);
                Assert.That(healthFill.fillAmount, Is.EqualTo(325f / 800f).Within(.0001f));
                Assert.That(healthText.text, Is.EqualTo("325 / 800 HP"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Type Rules => RequireType("Gameplay.Waves.WaveRules, Assembly-CSharp");
        private static Type ArenaObjectiveView => RequireType("Player.UI.Waves.ArenaObjectiveView, Assembly-CSharp");
        private static object Invoke(string methodName, params object[] arguments)
        {
            MethodInfo method = Rules.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }
        private static object Enemy(string value) => Enum.Parse(RequireType("Gameplay.Waves.WaveEnemyType, Assembly-CSharp"), value);
        private static object Kind(string value) => Enum.Parse(RequireType("Gameplay.Waves.WaveKind, Assembly-CSharp"), value);
        private static void AssertPrefabFloat(string prefabPath, string componentTypeName, string propertyName, float expected)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            Component component = Array.Find(prefab.GetComponentsInChildren<Component>(true),
                candidate => candidate != null && candidate.GetType().FullName == componentTypeName);
            Assert.That(component, Is.Not.Null, componentTypeName + " on " + prefabPath);
            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName + " on " + componentTypeName);
            float actual = property.propertyType == SerializedPropertyType.Integer
                ? property.intValue
                : property.floatValue;
            Assert.That(actual, Is.EqualTo(expected));
        }
        private static void AssertPrefabChildFloat(string prefabPath, string childName, string componentTypeName,
            string propertyName, float expected)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            Transform child = Array.Find(prefab.GetComponentsInChildren<Transform>(true),
                candidate => candidate != null && candidate.name == childName);
            Assert.That(child, Is.Not.Null, childName + " on " + prefabPath);
            Component component = Array.Find(child.GetComponents<Component>(),
                candidate => candidate != null && candidate.GetType().FullName == componentTypeName);
            Assert.That(component, Is.Not.Null, componentTypeName + " on " + childName);
            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName + " on " + componentTypeName);
            float actual = property.propertyType == SerializedPropertyType.Integer
                ? property.intValue
                : property.floatValue;
            Assert.That(actual, Is.EqualTo(expected));
        }
        private static Type RequireType(string name)
        {
            Type type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }
        private static Component CreateArenaObjectiveView(
            GameObject root,
            out Text title,
            out Text objective,
            out Text detail,
            out GameObject healthRoot,
            out Image healthFill,
            out Text healthText)
        {
            title = CreateText("Title", root.transform);
            objective = CreateText("Objective", root.transform);
            detail = CreateText("Detail", root.transform);
            healthRoot = new GameObject("Boss Health", typeof(RectTransform));
            healthRoot.transform.SetParent(root.transform, false);
            healthFill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            healthFill.transform.SetParent(healthRoot.transform, false);
            healthText = CreateText("Value", healthRoot.transform);

            Component view = root.AddComponent(ArenaObjectiveView);
            InvokeInstance(view, "Configure", root.GetComponent<CanvasGroup>(), title, objective, detail,
                healthRoot, healthFill, healthText);
            return view;
        }
        private static Text CreateText(string name, Transform parent)
        {
            Text text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            return text;
        }
        private static object InvokeInstance(Component target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }
        private static void AssertRegularMix(int wave, int expectedSmall, int expectedFlying, int expectedLarge)
        {
            object[] arguments = { wave, 0, 0, 0 };
            Rules.GetMethod("GetRegularComposition").Invoke(null, arguments);
            Assert.That(Convert.ToInt32(arguments[1]), Is.EqualTo(expectedSmall));
            Assert.That(Convert.ToInt32(arguments[2]), Is.EqualTo(expectedFlying));
            Assert.That(Convert.ToInt32(arguments[3]), Is.EqualTo(expectedLarge));
        }
    }
}
