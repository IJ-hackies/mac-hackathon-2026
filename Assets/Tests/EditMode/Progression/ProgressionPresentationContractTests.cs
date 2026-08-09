using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

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
                "MedKit:750:nursing school she said.",
                "AmmoKit:1000:its meta trust",
                "Ultimate:1500:the best feature in the game",
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
