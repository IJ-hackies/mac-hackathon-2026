using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Progression.Tests
{
    /// <summary>Locks the public, run-scoped progression contract without duplicating runtime code.</summary>
    public sealed class ProgressionPresentationContractTests
    {
        [Test]
        public void FreshRun_HasApprovedGoldAndLevelOneStats()
        {
            using (var fixture = new ProgressionFixture())
            {
                Assert.That(ReadInt(fixture.Type, "StartingGold"), Is.EqualTo(100));
                Assert.That(ReadInt(fixture.Type, "FirstStatUpgradeCost"), Is.EqualTo(50));
                Assert.That(ReadInt(fixture.Type, "SupplyHealthCost"), Is.EqualTo(50));
                Assert.That(ReadInt(fixture.Type, "SupplyLargeHealthCost"), Is.EqualTo(100));
                Assert.That(ReadInt(fixture.Type, "SupplyAmmoCost"), Is.EqualTo(100));
                Assert.That(ReadInt(fixture.Type, "HoldToFireCost"), Is.EqualTo(50));
                Assert.That(ReadProperty<int>(fixture.Component, "Gold"), Is.EqualTo(100));
                Assert.That(ReadProperty<int>(fixture.Component, "MaxLevel"), Is.EqualTo(10));
            }
        }

        [Test]
        public void FreshRun_InitializesEveryProgressionStatAtLevelOne()
        {
            using (var fixture = new ProgressionFixture())
            {
                Type statType = Type.GetType("Player.UI.Progression.ProgressionStat, Assembly-CSharp");
                Assert.That(statType, Is.Not.Null);
                MethodInfo getLevel = fixture.Type.GetMethod("GetLevel", BindingFlags.Instance | BindingFlags.Public);
                Assert.That(getLevel, Is.Not.Null);
                foreach (object stat in Enum.GetValues(statType))
                {
                    Assert.That((int)getLevel.Invoke(fixture.Component, new[] { stat }), Is.EqualTo(1), stat.ToString());
                }
            }
        }

        [Test]
        public void SpecialCatalog_ContainsEveryApprovedIndependentRunSkill()
        {
            Type catalogType = Type.GetType("Player.UI.Progression.ProgressionSpecialSkillCatalog, Assembly-CSharp");
            Assert.That(catalogType, Is.Not.Null);
            PropertyInfo all = catalogType.GetProperty("All", BindingFlags.Public | BindingFlags.Static);
            Assert.That(all, Is.Not.Null);
            var definitions = (System.Collections.IEnumerable)all.GetValue(null);
            var actual = new System.Collections.Generic.List<string>();
            object secretDefinition = null;
            foreach (object definition in definitions)
            {
                Type definitionType = definition.GetType();
                if (definitionType.GetProperty("Skill")?.GetValue(definition)?.ToString() == "Secret")
                    secretDefinition = definition;
                actual.Add(definitionType.GetProperty("Skill")?.GetValue(definition) + ":" +
                    definitionType.GetProperty("Cost")?.GetValue(definition) + ":" +
                    definitionType.GetProperty("Flavor")?.GetValue(definition));
            }

            string[] expected =
            {
                "HoldToFire:50:hold to fire",
                "BulletBounce:750:skill issue",
                "Fortune:500:2007 bitcoin",
                "FortuneII:500:2012 dropshipping",
                "MedKit:400:nursing school she said.",
                "AmmoKit:600:its meta trust",
                "Ultimate:800:the best feature in the game",
                "Quickdraw:1200:it's hiiigghh noon",
                "Vampire:2000:sucky sucky",
                "ExplosiveBullets:750:bom bom bakudan!",
                "Headshot:800:FOUR!",
                "Minigun:4000:pew pew haha",
                "Secret:10000:how'd you get here?",
            };
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(secretDefinition, Is.Not.Null);
            Type secretType = secretDefinition.GetType();
            Assert.That((bool)secretType.GetProperty("HideEffect")?.GetValue(secretDefinition), Is.True);
            Assert.That((string)secretType.GetProperty("Effect")?.GetValue(secretDefinition), Is.Empty);
        }

        [Test]
        public void SpecialShop_OrdersEverySkillByAscendingCostWithStableTies()
        {
            Type screenType = Type.GetType("Player.UI.Progression.SpecialShopStationScreen, Assembly-CSharp");
            Assert.That(screenType, Is.Not.Null);
            MethodInfo copyDefinitions = screenType.GetMethod("CopyDefinitions",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(copyDefinitions, Is.Not.Null);

            Array definitions = (Array)copyDefinitions.Invoke(null, null);
            Assert.That(definitions.Length, Is.EqualTo(13));

            string[] expectedSkills =
            {
                "HoldToFire", "MedKit", "Fortune", "FortuneII", "AmmoKit",
                "BulletBounce", "ExplosiveBullets", "Ultimate", "Headshot", "Quickdraw",
                "Vampire", "Minigun", "Secret",
            };
            int previousCost = int.MinValue;
            for (int index = 0; index < definitions.Length; index++)
            {
                object definition = definitions.GetValue(index);
                Type definitionType = definition.GetType();
                int cost = (int)definitionType.GetProperty("Cost")?.GetValue(definition);
                string skill = definitionType.GetProperty("Skill")?.GetValue(definition)?.ToString();
                Assert.That(cost, Is.GreaterThanOrEqualTo(previousCost), skill);
                Assert.That(skill, Is.EqualTo(expectedSkills[index]));
                previousCost = cost;
            }
        }

        [Test]
        public void SmoothStationScroll_WheelInputAddsVelocityWithoutJumpingContent()
        {
            Type smoothType = Type.GetType("Player.UI.Progression.SmoothStationScrollRect, Assembly-CSharp");
            Assert.That(smoothType, Is.Not.Null);

            var root = new GameObject("Smooth Scroll", typeof(RectTransform));
            var viewportObject = new GameObject("Viewport", typeof(RectTransform));
            var contentObject = new GameObject("Content", typeof(RectTransform));
            var eventSystemObject = new GameObject("Event System", typeof(EventSystem));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                RectTransform viewport = viewportObject.GetComponent<RectTransform>();
                RectTransform content = contentObject.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(500f, 300f);
                viewport.SetParent(rootRect, false);
                viewport.sizeDelta = rootRect.sizeDelta;
                content.SetParent(viewport, false);
                content.sizeDelta = new Vector2(500f, 900f);

                var scroll = (ScrollRect)root.AddComponent(smoothType);
                scroll.viewport = viewport;
                scroll.content = content;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.inertia = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.verticalNormalizedPosition = 1f;
                Canvas.ForceUpdateCanvases();

                Vector2 before = content.anchoredPosition;
                var eventData = new PointerEventData(eventSystemObject.GetComponent<EventSystem>())
                {
                    scrollDelta = new Vector2(0f, -1f),
                };
                smoothType.GetMethod("OnScroll", BindingFlags.Instance | BindingFlags.Public)
                    ?.Invoke(scroll, new object[] { eventData });

                Assert.That(content.anchoredPosition, Is.EqualTo(before),
                    "A wheel step should become velocity instead of an immediate position jump.");
                Assert.That(scroll.velocity.y, Is.GreaterThan(0f));
                Assert.That(scroll.velocity.x, Is.Zero);
                Assert.That(eventData.used, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(eventSystemObject);
                UnityEngine.Object.DestroyImmediate(contentObject);
                UnityEngine.Object.DestroyImmediate(viewportObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StationCardReadability_MovesOnlyDirectCardTextIntoTheIconSafeLane()
        {
            Type controllerType = Type.GetType("Gameplay.Interaction.StationMenuController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            MethodInfo applyReadability = controllerType.GetMethod("ApplyStationReadability",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(applyReadability, Is.Not.Null);

            var root = new GameObject("Station Root", typeof(RectTransform));
            var card = new GameObject("Card_01", typeof(RectTransform));
            var cardTextObject = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            try
            {
                card.transform.SetParent(root.transform, false);
                cardTextObject.transform.SetParent(card.transform, false);
                titleObject.transform.SetParent(root.transform, false);
                RectTransform cardText = cardTextObject.GetComponent<RectTransform>();
                RectTransform title = titleObject.GetComponent<RectTransform>();

                applyReadability.Invoke(null, new object[] { root, 0f });

                Assert.That(cardText.anchoredPosition.x, Is.EqualTo(152f));
                Assert.That(title.anchoredPosition.x, Is.Zero,
                    "Panel headings must not move when the card text lane is offset.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(titleObject);
                UnityEngine.Object.DestroyImmediate(cardTextObject);
                UnityEngine.Object.DestroyImmediate(card);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RunStatsOverview_FormatsRowsAsReadableTelemetryBlocks()
        {
            Type overviewType = Type.GetType("Player.UI.Progression.RunStatsOverview, Assembly-CSharp");
            Type statType = Type.GetType("Player.UI.Progression.ProgressionStat, Assembly-CSharp");
            Assert.That(overviewType, Is.Not.Null);
            Assert.That(statType, Is.Not.Null);
            MethodInfo formatter = overviewType.GetMethod("FormatStatRow",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(formatter, Is.Not.Null);
            string health = (string)formatter.Invoke(null,
                new[] { Enum.Parse(statType, "MaxHealth"), (object)4, 175f, 0 });
            string ammo = (string)formatter.Invoke(null,
                new[] { Enum.Parse(statType, "MaxAmmo"), (object)7, 45f, 260 });

            Assert.That(health, Does.StartWith("MAX HEALTH\n"));
            Assert.That(health, Does.Contain("LEVEL 04"));
            Assert.That(health, Does.Contain("175"));
            Assert.That(ammo, Does.StartWith("AMMO CAPACITY\n"));
            Assert.That(ammo, Does.Contain("LEVEL 07"));
            Assert.That(ammo, Does.Contain("MAG 45   RES 260"));
        }

        private static int ReadInt(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name);
            return (int)field.GetValue(null);
        }

        private static T ReadProperty<T>(Component component, string name)
        {
            PropertyInfo property = component.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(component);
        }

        private sealed class ProgressionFixture : IDisposable
        {
            public ProgressionFixture()
            {
                Type = Type.GetType("Player.UI.Progression.PlayerProgression, Assembly-CSharp");
                Assert.That(Type, Is.Not.Null, "PlayerProgression must remain a runtime component.");
                GameObject = new GameObject("Progression Test Player");
                Component = GameObject.AddComponent(Type);
                Type.GetMethod("BeginNewRun", BindingFlags.Instance | BindingFlags.Public)?.Invoke(Component, null);
            }

            public Type Type { get; }
            public GameObject GameObject { get; }
            public Component Component { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
            }
        }
    }
}
