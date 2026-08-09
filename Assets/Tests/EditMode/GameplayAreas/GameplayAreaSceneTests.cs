using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay.Areas.Tests
{
    public sealed class GameplayAreaSceneTests
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [Test]
        public void SampleScene_HasThreeValidAreasAndSharedBodyTracker()
        {
            Scene scene = SceneManager.GetSceneByPath(SampleScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameplayArea[] areas = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameplayArea>(true))
                    .ToArray();
                Assert.That(areas, Has.Length.EqualTo(3));
                Assert.That(
                    areas.Select(area => area.AreaId),
                    Is.EquivalentTo((GameplayAreaId[])Enum.GetValues(typeof(GameplayAreaId))));

                foreach (GameplayArea area in areas)
                {
                    Assert.That(area.PerimeterPoles, Is.Not.Null, area.name);
                    Assert.That(area.PerimeterPoles.childCount, Is.GreaterThanOrEqualTo(3), area.name);
                    Assert.That(area.RebuildPerimeter(), Is.True, area.ValidationError);
                }

                foreach (GameplayArea arena in areas.Where(area =>
                             area.AreaId == GameplayAreaId.Arena1 ||
                             area.AreaId == GameplayAreaId.Arena2))
                {
                    Assert.That(arena.Entrance, Is.Not.Null, arena.name);
                    Assert.That(
                        arena.Entrance,
                        Is.SameAs(arena.transform.Find("Perimeter/Entrance")),
                        arena.name);
                }

                PlayerAreaTracker[] trackers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PlayerAreaTracker>(true))
                    .ToArray();
                Assert.That(trackers, Has.Length.EqualTo(1));
                Assert.That(trackers[0].TrackedBody, Is.Not.Null);
                Assert.That(
                    trackers[0].Areas.Count == 3 ||
                    (trackers[0].Areas.Count == 0 && trackers[0].DiscoverAreasWhenEmpty),
                    Is.True,
                    "The tracker must explicitly reference all areas or discover them at startup.");

                Type speedEffectType = Type.GetType(
                    "Player.LandingBaseMovementSpeedEffect, Assembly-CSharp");
                Assert.That(speedEffectType, Is.Not.Null);
                Component[] speedEffects = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren(speedEffectType, true))
                    .ToArray();
                Assert.That(speedEffects, Has.Length.EqualTo(1));
                Assert.That(
                    speedEffectType.GetProperty("AreaTracker")?.GetValue(speedEffects[0]),
                    Is.SameAs(trackers[0]));
                Assert.That(
                    ((Component)speedEffectType.GetProperty("PlayerController")?
                        .GetValue(speedEffects[0]))?.transform,
                    Is.SameAs(trackers[0].TrackedBody));
                Assert.That(
                    speedEffectType.GetProperty("SpeedMultiplier")?.GetValue(speedEffects[0]),
                    Is.EqualTo(2f));
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }
    }
}
