using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Progression.Tests
{
    /// <summary>
    /// Exercises the run economy against the real player runtime components. Reflection keeps
    /// this EditMode contract assembly independent from Assembly-CSharp's implementation layout.
    /// </summary>
    public sealed class PlayerProgressionBehaviorTests
    {
        private const string ProgressionTypeName = "Player.UI.Progression.PlayerProgression, Assembly-CSharp";
        private const string StatTypeName = "Player.UI.Progression.ProgressionStat, Assembly-CSharp";
        private const string SupplyTypeName = "Player.UI.Progression.ProgressionSupply, Assembly-CSharp";
        private const string SpecialTypeName = "Player.UI.Progression.ProgressionSpecialSkill, Assembly-CSharp";
        private const string HealthTypeName = "Combat.Health, Assembly-CSharp";
        private const string AmmoTypeName = "Player.PlayerAmmo, Assembly-CSharp";
        private const string CombatTypeName = "Player.PlayerCombat, Assembly-CSharp";
        private const string ControllerTypeName = "Player.PlayerController, Assembly-CSharp";
        private const string ShieldTypeName = "Player.PlayerShield, Assembly-CSharp";

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _created.Count - 1; index >= 0; index--)
            {
                if (_created[index] != null) UnityEngine.Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
        }

        [Test]
        public void AllStats_CostOneHundredScaleCorrectlyAndStopAtLevelTen()
        {
            Fixture fixture = CreateFixture();
            var expectedValues = new Dictionary<string, float>
            {
                { "MaxHealth", 190f },
                { "MovementSpeed", 1.27f },
                { "FireRate", 1.45f },
                { "ShootingDamage", 28.5f },
                { "MeleeDamage", 38f },
                { "Defense", .36f },
                { "MaxAmmo", 30f },
            };

            foreach (object stat in Enum.GetValues(fixture.StatType))
            {
                for (int level = 1; level < 10; level++)
                {
                    Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", stat), Is.True, stat + " level " + level);
                }

                Assert.That(ReadInt(Invoke(fixture.Progression, "GetLevel", stat)), Is.EqualTo(10), stat.ToString());
                Assert.That(ReadFloat(Invoke(fixture.Progression, "GetPurchasedValue", stat)),
                    Is.EqualTo(expectedValues[stat.ToString()]).Within(.001f), stat.ToString());
                Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", stat), Is.False);
                Assert.That(ReadStringProperty(fixture.Progression, "LastPurchaseResult"), Is.EqualTo("MaxLevel"));
            }

            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(3700));
            Assert.That(ReadFloat(fixture.Controller, "MovementSpeedMultiplier"), Is.EqualTo(1.27f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "FireRateMultiplier"), Is.EqualTo(1.45f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "EffectiveRangedDamage"), Is.EqualTo(28.5f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "EffectiveMeleeDamage"), Is.EqualTo(38f).Within(.001f));
            Assert.That(ReadFloat(fixture.Health, "EffectiveIncomingDamageMultiplier"), Is.EqualTo(.64f).Within(.001f));
        }

        [Test]
        public void MaxHealthUpgrade_AddsCapacityToCurrentHealthImmediately()
        {
            Fixture fixture = CreateFixture();
            ApplyDamage(fixture.Health, 30f, fixture.Root);
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(70f));

            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("MaxHealth")), Is.True);

            Assert.That(ReadFloat(fixture.Health, "MaxHealth"), Is.EqualTo(110f));
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(80f));
        }

        [Test]
        public void MaxAmmoUpgrade_AddsTwoLoadedRoundsWithoutChangingReserve()
        {
            Fixture fixture = CreateFixture();
            for (int round = 0; round < 7; round++) Assert.That(InvokeBool(fixture.Ammo, "TryConsumeRound"), Is.True);

            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentMagazine"), Is.EqualTo(5));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentStorage"), Is.EqualTo(90));
            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("MaxAmmo")), Is.True);

            Assert.That(ReadIntProperty(fixture.Ammo, "MagazineSize"), Is.EqualTo(14));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentMagazine"), Is.EqualTo(7));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentStorage"), Is.EqualTo(90));
        }

        [Test]
        public void SupplyPacks_RefillOnceAndRejectFullOrInsufficientPurchases()
        {
            Fixture fixture = CreateFixture();
            ApplyDamage(fixture.Health, 40f, fixture.Root);
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSupply", fixture.Supply("HealthPack")), Is.True);
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(100f));
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(9950));

            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSupply", fixture.Supply("HealthPack")), Is.False);
            Assert.That(ReadStringProperty(fixture.Progression, "LastPurchaseResult"), Is.EqualTo("Full"));
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(9950));

            Assert.That(InvokeBool(fixture.Ammo, "TryConsumeRound"), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSupply", fixture.Supply("AmmoPack")), Is.True);
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentMagazine"), Is.EqualTo(12));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentStorage"), Is.EqualTo(90));
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(9850));

            SetAutoProperty(fixture.Progression, "Gold", 0);
            Assert.That(InvokeBool(fixture.Ammo, "TryConsumeRound"), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSupply", fixture.Supply("AmmoPack")), Is.False);
            Assert.That(ReadStringProperty(fixture.Progression, "LastPurchaseResult"), Is.EqualTo("InsufficientGold"));
        }

        [Test]
        public void HoldToFire_IsOneTimeFiveHundredGoldPurchaseAndResetsWithRun()
        {
            Fixture fixture = CreateFixture();
            object holdToFire = fixture.Special("HoldToFire");

            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSpecial", holdToFire), Is.True);
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(9500));
            Assert.That(InvokeBool(fixture.Progression, "OwnsSpecial", holdToFire), Is.True);
            Assert.That(ReadBoolProperty(fixture.Combat, "HoldToFireUnlocked"), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSpecial", holdToFire), Is.False);
            Assert.That(ReadStringProperty(fixture.Progression, "LastPurchaseResult"), Is.EqualTo("AlreadyOwned"));

            Invoke(fixture.Progression, "BeginNewRun");
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(10000));
            Assert.That(InvokeBool(fixture.Progression, "OwnsSpecial", holdToFire), Is.False);
            Assert.That(ReadBoolProperty(fixture.Combat, "HoldToFireUnlocked"), Is.False);
        }

        [Test]
        public void BeginNewRun_ResetsUpgradesHealthAmmoAndCombatModifiers()
        {
            Fixture fixture = CreateFixture();
            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("MaxHealth")), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("MaxAmmo")), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("ShootingDamage")), Is.True);
            Assert.That(ReadFloat(fixture.Health, "MaxHealth"), Is.EqualTo(110f));
            Assert.That(ReadIntProperty(fixture.Ammo, "MagazineSize"), Is.EqualTo(14));

            Invoke(fixture.Progression, "BeginNewRun");

            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(10000));
            foreach (object stat in Enum.GetValues(fixture.StatType))
                Assert.That(ReadInt(Invoke(fixture.Progression, "GetLevel", stat)), Is.EqualTo(1), stat.ToString());
            Assert.That(ReadFloat(fixture.Health, "MaxHealth"), Is.EqualTo(100f));
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(100f));
            Assert.That(ReadIntProperty(fixture.Ammo, "MagazineSize"), Is.EqualTo(12));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentMagazine"), Is.EqualTo(12));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentStorage"), Is.EqualTo(90));
            Assert.That(ReadFloat(fixture.Combat, "RangedDamageMultiplier"), Is.EqualTo(1f));
        }

        [Test]
        public void DefenseAndUltimateShield_ComposeInsteadOfOverwritingEachOther()
        {
            Fixture fixture = CreateFixture();
            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("Defense")), Is.True);
            Assert.That(ReadFloat(fixture.Health, "EffectiveIncomingDamageMultiplier"), Is.EqualTo(.96f).Within(.001f));

            Component shield = fixture.Root.AddComponent(RequireType(ShieldTypeName));
            InvokeNonPublic(shield, "Awake");
            Invoke(shield, "SetHeld", true);
            Assert.That(ReadFloat(fixture.Health, "EffectiveIncomingDamageMultiplier"), Is.EqualTo(0f));
            ApplyDamage(fixture.Health, 50f, fixture.Root);
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(100f));

            Invoke(shield, "SetHeld", false);
            Assert.That(ReadFloat(fixture.Health, "EffectiveIncomingDamageMultiplier"), Is.EqualTo(.96f).Within(.001f));
            ApplyDamage(fixture.Health, 50f, fixture.Root);
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(52f).Within(.001f));
        }

        private Fixture CreateFixture()
        {
            GameObject root = new GameObject("Progression Behavior Test Player");
            _created.Add(root);
            Component health = root.AddComponent(RequireType(HealthTypeName));
            Component ammo = root.AddComponent(RequireType(AmmoTypeName));
            Component controller = root.AddComponent(RequireType(ControllerTypeName));
            Component combat = root.AddComponent(RequireType(CombatTypeName));
            Component progression = root.AddComponent(RequireType(ProgressionTypeName));
            Invoke(progression, "BeginNewRun");
            return new Fixture(root, health, ammo, controller, combat, progression,
                RequireType(StatTypeName), RequireType(SupplyTypeName), RequireType(SpecialTypeName));
        }

        private static Type RequireType(string name)
        {
            Type type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static bool InvokeBool(object target, string methodName, params object[] arguments) =>
            (bool)Invoke(target, methodName, arguments);

        private static void InvokeNonPublic(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static void ApplyDamage(Component health, float amount, GameObject instigator)
        {
            MethodInfo method = health.GetType().GetMethod("ApplyDamage", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            Type damageType = method.GetParameters()[3].ParameterType;
            method.Invoke(health, new[] { (object)amount, Vector3.zero, instigator, Enum.Parse(damageType, "Generic") });
        }

        private static T ReadProperty<T>(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(target);
        }

        private static int ReadIntProperty(object target, string name) => ReadProperty<int>(target, name);
        private static bool ReadBoolProperty(object target, string name) => ReadProperty<bool>(target, name);
        private static float ReadFloat(object target, string name) => ReadProperty<float>(target, name);
        private static string ReadStringProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, name);
            return Convert.ToString(property.GetValue(target));
        }
        private static int ReadInt(object value) => Convert.ToInt32(value);
        private static float ReadFloat(object value) => Convert.ToSingle(value);

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            FieldInfo field = target.GetType().GetField("<" + propertyName + ">k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, propertyName);
            field.SetValue(target, value);
        }

        private readonly struct Fixture
        {
            public Fixture(GameObject root, Component health, Component ammo, Component controller,
                Component combat, Component progression, Type statType, Type supplyType, Type specialType)
            {
                Root = root;
                Health = health;
                Ammo = ammo;
                Controller = controller;
                Combat = combat;
                Progression = progression;
                StatType = statType;
                SupplyType = supplyType;
                SpecialType = specialType;
            }

            public GameObject Root { get; }
            public Component Health { get; }
            public Component Ammo { get; }
            public Component Controller { get; }
            public Component Combat { get; }
            public Component Progression { get; }
            public Type StatType { get; }
            public Type SupplyType { get; }
            public Type SpecialType { get; }
            public object Stat(string value) => Enum.Parse(StatType, value);
            public object Supply(string value) => Enum.Parse(SupplyType, value);
            public object Special(string value) => Enum.Parse(SpecialType, value);
        }
    }
}
