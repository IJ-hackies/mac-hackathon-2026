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
        public void AllStats_UseCumulativeCurvesAndStopAtLevelTen()
        {
            Fixture fixture = CreateFixture();
            SetAutoProperty(fixture.Progression, "Gold", 100000);
            var expectedValues = new Dictionary<string, float>
            {
                { "MaxHealth", 460f },
                { "MovementSpeed", 1.99f },
                { "FireRate", 2.17f },
                { "ShootingDamage", 87f },
                { "MeleeDamage", 155f },
                { "Defense", .54f },
                { "MaxAmmo", 69f },
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

            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(59750));
            Assert.That(ReadFloat(fixture.Controller, "MovementSpeedMultiplier"), Is.EqualTo(1.99f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "FireRateMultiplier"), Is.EqualTo(2.17f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "EffectiveRangedDamage"), Is.EqualTo(87f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "EffectiveSecondaryDamage"), Is.EqualTo(97f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "EffectiveElectricDamage"), Is.EqualTo(82f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "EffectiveUltimateSecondaryDamage"), Is.EqualTo(102f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "EffectiveMeleeDamage"), Is.EqualTo(155f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "MeleeDamageBonus"), Is.EqualTo(135f).Within(.001f));
            Assert.That(ReadFloat(fixture.Health, "EffectiveIncomingDamageMultiplier"), Is.EqualTo(.46f).Within(.001f));
            Assert.That(ReadInt(Invoke(fixture.Progression, "GetReserveCapacityAtLevel", 10)), Is.EqualTo(345));
        }

        [Test]
        public void UpgradeCosts_DoubleThroughLevelSixThenRiseByOneHundred()
        {
            Fixture fixture = CreateFixture();
            object stat = fixture.Stat("MaxHealth");
            int[] expectedCosts = { 50, 100, 200, 400, 800, 900, 1000, 1100, 1200 };
            SetAutoProperty(fixture.Progression, "Gold", 100000);

            for (int index = 0; index < expectedCosts.Length; index++)
            {
                Assert.That(ReadInt(Invoke(fixture.Progression, "GetUpgradeCost", stat)), Is.EqualTo(expectedCosts[index]));
                Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", stat), Is.True);
            }

            Assert.That(ReadInt(Invoke(fixture.Progression, "GetUpgradeCost", stat)), Is.EqualTo(0));
            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetValueAtLevel", stat, 0)), Is.EqualTo(100f));
            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetValueAtLevel", stat, 99)), Is.EqualTo(460f));
        }

        [Test]
        public void MaxHealthAndShootingDamage_UseIncreasingEarlyGamePurchaseIncrements()
        {
            Fixture fixture = CreateFixture();
            object maxHealth = fixture.Stat("MaxHealth");
            object shootingDamage = fixture.Stat("ShootingDamage");

            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetValueAtLevel", maxHealth, 1)), Is.EqualTo(100f));
            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetValueAtLevel", maxHealth, 2)), Is.EqualTo(120f));
            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetValueAtLevel", maxHealth, 3)), Is.EqualTo(145f));
            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetValueAtLevel", maxHealth, 10)), Is.EqualTo(460f));

            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetValueAtLevel", shootingDamage, 1)), Is.EqualTo(15f));
            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetValueAtLevel", shootingDamage, 2)), Is.EqualTo(19f));
            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetValueAtLevel", shootingDamage, 3)), Is.EqualTo(24f));
            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetValueAtLevel", shootingDamage, 10)), Is.EqualTo(87f));
        }

        [Test]
        public void PlayerRuntime_DefaultAmmoAndDashMatchTheBalancedBaseLoadout()
        {
            Fixture fixture = CreateFixture();
            Assert.That(ReadIntProperty(fixture.Ammo, "MagazineSize"), Is.EqualTo(15));
            Assert.That(ReadIntProperty(fixture.Ammo, "MaxStorage"), Is.EqualTo(120));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentMagazine"), Is.EqualTo(15));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentStorage"), Is.EqualTo(120));

            Component dash = fixture.Root.AddComponent(RequireType("Player.PlayerDash, Assembly-CSharp"));
            Assert.That(ReadFloat(dash, "CooldownDuration"), Is.EqualTo(2f));
        }

        [Test]
        public void MaxHealthUpgrade_AddsCapacityToCurrentHealthImmediately()
        {
            Fixture fixture = CreateFixture();
            ApplyDamage(fixture.Health, 30f, fixture.Root);
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(70f));

            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("MaxHealth")), Is.True);

            Assert.That(ReadFloat(fixture.Health, "MaxHealth"), Is.EqualTo(120f));
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(90f));
        }

        [Test]
        public void MaxAmmoUpgrade_AddsIncreasingMagazineAndReserveCapacityImmediately()
        {
            Fixture fixture = CreateFixture();
            for (int round = 0; round < 7; round++) Assert.That(InvokeBool(fixture.Ammo, "TryConsumeRound"), Is.True);

            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentMagazine"), Is.EqualTo(8));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentStorage"), Is.EqualTo(120));
            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("MaxAmmo")), Is.True);

            Assert.That(ReadIntProperty(fixture.Ammo, "MagazineSize"), Is.EqualTo(17));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentMagazine"), Is.EqualTo(10));
            Assert.That(ReadIntProperty(fixture.Ammo, "MaxStorage"), Is.EqualTo(125));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentStorage"), Is.EqualTo(125));
        }

        [Test]
        public void SupplyPacks_RefillOnceAndRejectFullOrInsufficientPurchases()
        {
            Fixture fixture = CreateFixture();
            SetAutoProperty(fixture.Progression, "Gold", 10000);
            ApplyDamage(fixture.Health, 70f, fixture.Root);
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSupply", fixture.Supply("HealthPack")), Is.True);
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(80f));
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(9950));

            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSupply", fixture.Supply("LargeHealthPack")), Is.True);
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(100f));
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(9850));

            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSupply", fixture.Supply("HealthPack")), Is.False);
            Assert.That(ReadStringProperty(fixture.Progression, "LastPurchaseResult"), Is.EqualTo("Full"));
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(9850));

            Assert.That(InvokeBool(fixture.Ammo, "TryConsumeRound"), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSupply", fixture.Supply("AmmoPack")), Is.True);
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentMagazine"), Is.EqualTo(15));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentStorage"), Is.EqualTo(120));
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(9750));

            SetAutoProperty(fixture.Progression, "Gold", 0);
            Assert.That(InvokeBool(fixture.Ammo, "TryConsumeRound"), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSupply", fixture.Supply("AmmoPack")), Is.False);
            Assert.That(ReadStringProperty(fixture.Progression, "LastPurchaseResult"), Is.EqualTo("InsufficientGold"));
        }

        [Test]
        public void HoldToFire_IsOneTimeFiftyGoldPurchaseAndResetsWithRun()
        {
            Fixture fixture = CreateFixture();
            SetAutoProperty(fixture.Progression, "Gold", 10000);
            object holdToFire = fixture.Special("HoldToFire");

            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSpecial", holdToFire), Is.True);
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(9950));
            Assert.That(InvokeBool(fixture.Progression, "OwnsSpecial", holdToFire), Is.True);
            Assert.That(ReadBoolProperty(fixture.Combat, "HoldToFireUnlocked"), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSpecial", holdToFire), Is.False);
            Assert.That(ReadStringProperty(fixture.Progression, "LastPurchaseResult"), Is.EqualTo("AlreadyOwned"));

            Invoke(fixture.Progression, "BeginNewRun");
            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(100));
            Assert.That(InvokeBool(fixture.Progression, "OwnsSpecial", holdToFire), Is.False);
            Assert.That(ReadBoolProperty(fixture.Combat, "HoldToFireUnlocked"), Is.False);
        }

        [Test]
        public void SpecialSkills_AreIndependentAndSecretComposesWithArchiveAndMinigun()
        {
            Fixture fixture = CreateFixture();
            SetAutoProperty(fixture.Progression, "Gold", 100000);

            // Fortune II stays independently purchasable: owning Fortune is neither required nor implied.
            object fortuneII = fixture.Special("FortuneII");
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSpecial", fortuneII), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "OwnsSpecial", fixture.Special("Fortune")), Is.False);

            foreach (string statName in new[]
                     { "MaxHealth", "MovementSpeed", "FireRate", "ShootingDamage", "MeleeDamage", "Defense", "MaxAmmo" })
            {
                Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat(statName)), Is.True, statName);
            }

            object secret = fixture.Special("Secret");
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSpecial", secret), Is.True);
            Assert.That(ReadFloat(fixture.Health, "MaxHealth"), Is.EqualTo(360f).Within(.001f));
            Assert.That(ReadFloat(fixture.Controller, "MovementSpeedMultiplier"), Is.EqualTo(3.09f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "FireRateMultiplier"), Is.EqualTo(3.15f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "EffectiveRangedDamage"), Is.EqualTo(57f).Within(.001f));
            Assert.That(ReadFloat(fixture.Combat, "EffectiveMeleeDamage"), Is.EqualTo(69f).Within(.001f));
            Assert.That(ReadFloat(fixture.Health, "EffectiveIncomingDamageMultiplier"), Is.EqualTo(.94f).Within(.001f));
            Assert.That(ReadIntProperty(fixture.Ammo, "MagazineSize"), Is.EqualTo(51));
            Assert.That(ReadIntProperty(fixture.Ammo, "MaxStorage"), Is.EqualTo(375));

            object minigun = fixture.Special("Minigun");
            Assert.That(InvokeBool(fixture.Progression, "TryPurchaseSpecial", minigun), Is.True);
            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetPurchasedValue", fixture.Stat("FireRate"))),
                Is.EqualTo(6.3f).Within(.001f));
            Assert.That(ReadFloat(Invoke(fixture.Progression, "GetPurchasedValue", fixture.Stat("ShootingDamage"))),
                Is.EqualTo(1f).Within(.001f));
            Assert.That(ReadIntProperty(fixture.Ammo, "MagazineSize"), Is.EqualTo(141));
            Assert.That(ReadIntProperty(fixture.Ammo, "MaxStorage"), Is.EqualTo(975));

            Invoke(fixture.Progression, "BeginNewRun");
            Assert.That(InvokeBool(fixture.Progression, "OwnsSpecial", secret), Is.False);
            Assert.That(InvokeBool(fixture.Progression, "OwnsSpecial", minigun), Is.False);
            Assert.That(ReadIntProperty(fixture.Ammo, "MagazineSize"), Is.EqualTo(15));
            Assert.That(ReadIntProperty(fixture.Ammo, "MaxStorage"), Is.EqualTo(120));
        }

        [Test]
        public void BeginNewRun_ResetsUpgradesHealthAmmoAndCombatModifiers()
        {
            Fixture fixture = CreateFixture();
            SetAutoProperty(fixture.Progression, "Gold", 10000);
            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("MaxHealth")), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("MaxAmmo")), Is.True);
            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("ShootingDamage")), Is.True);
            Assert.That(ReadFloat(fixture.Health, "MaxHealth"), Is.EqualTo(120f));
            Assert.That(ReadIntProperty(fixture.Ammo, "MagazineSize"), Is.EqualTo(17));

            Invoke(fixture.Progression, "BeginNewRun");

            Assert.That(ReadProperty<int>(fixture.Progression, "Gold"), Is.EqualTo(100));
            foreach (object stat in Enum.GetValues(fixture.StatType))
                Assert.That(ReadInt(Invoke(fixture.Progression, "GetLevel", stat)), Is.EqualTo(1), stat.ToString());
            Assert.That(ReadFloat(fixture.Health, "MaxHealth"), Is.EqualTo(100f));
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(100f));
            Assert.That(ReadIntProperty(fixture.Ammo, "MagazineSize"), Is.EqualTo(15));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentMagazine"), Is.EqualTo(15));
            Assert.That(ReadIntProperty(fixture.Ammo, "CurrentStorage"), Is.EqualTo(120));
            Assert.That(ReadFloat(fixture.Combat, "RangedDamageMultiplier"), Is.EqualTo(1f));
            Assert.That(ReadFloat(fixture.Combat, "RangedDamageBonus"), Is.EqualTo(0f));
            Assert.That(ReadFloat(fixture.Combat, "MeleeDamageBonus"), Is.EqualTo(0f));
        }

        [Test]
        public void DefenseAndUltimateShield_ComposeInsteadOfOverwritingEachOther()
        {
            Fixture fixture = CreateFixture();
            Assert.That(InvokeBool(fixture.Progression, "TryUpgrade", fixture.Stat("Defense")), Is.True);
            Assert.That(ReadFloat(fixture.Health, "EffectiveIncomingDamageMultiplier"), Is.EqualTo(.98f).Within(.001f));

            Component shield = fixture.Root.AddComponent(RequireType(ShieldTypeName));
            InvokeNonPublic(shield, "Awake");
            Invoke(shield, "SetHeld", true);
            Assert.That(ReadFloat(fixture.Health, "EffectiveIncomingDamageMultiplier"), Is.EqualTo(0f));
            ApplyDamage(fixture.Health, 50f, fixture.Root);
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(100f));

            Invoke(shield, "SetHeld", false);
            Assert.That(ReadFloat(fixture.Health, "EffectiveIncomingDamageMultiplier"), Is.EqualTo(.98f).Within(.001f));
            ApplyDamage(fixture.Health, 50f, fixture.Root);
            Assert.That(ReadFloat(fixture.Health, "CurrentHealth"), Is.EqualTo(51f).Within(.001f));
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
