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
                Assert.That(ReadInt(fixture.Type, "StartingGold"), Is.EqualTo(10000));
                Assert.That(ReadInt(fixture.Type, "StatUpgradeCost"), Is.EqualTo(100));
                Assert.That(ReadInt(fixture.Type, "SupplyHealthCost"), Is.EqualTo(50));
                Assert.That(ReadInt(fixture.Type, "SupplyAmmoCost"), Is.EqualTo(100));
                Assert.That(ReadInt(fixture.Type, "HoldToFireCost"), Is.EqualTo(500));
                Assert.That(ReadProperty<int>(fixture.Component, "Gold"), Is.EqualTo(10000));
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
