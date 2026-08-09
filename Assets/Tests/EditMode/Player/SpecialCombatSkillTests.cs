using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Player.Tests
{
    public sealed class SpecialCombatSkillTests
    {
        private GameObject _player;
        private Component _ammo;
        private Component _combat;
        private readonly List<GameObject> _createdObjects = new List<GameObject>();
        private readonly HashSet<int> _preExistingAudioManagerIds = new HashSet<int>();

        [SetUp]
        public void SetUp()
        {
            foreach (UnityEngine.Object manager in Resources.FindObjectsOfTypeAll(RequireType("Audio.AudioManager, Assembly-CSharp")))
                _preExistingAudioManagerIds.Add(manager.GetInstanceID());
            _player = new GameObject("Special Combat Skill Test Player");
            _player.AddComponent(RequireType("Combat.Health, Assembly-CSharp"));
            _ammo = _player.AddComponent(RequireType("Player.PlayerAmmo, Assembly-CSharp"));
            _combat = _player.AddComponent(RequireType("Player.PlayerCombat, Assembly-CSharp"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _createdObjects)
                if (created != null) UnityEngine.Object.DestroyImmediate(created);
            _createdObjects.Clear();
            if (_player != null) UnityEngine.Object.DestroyImmediate(_player);
            foreach (UnityEngine.Object manager in Resources.FindObjectsOfTypeAll(RequireType("Audio.AudioManager, Assembly-CSharp")))
            {
                if (!_preExistingAudioManagerIds.Contains(manager.GetInstanceID()))
                    UnityEngine.Object.DestroyImmediate(((Component)manager).gameObject);
            }
            _preExistingAudioManagerIds.Clear();
        }

        [Test]
        public void MinigunDamage_IsRawReductionBeforeSecretMultiplierAndClamped()
        {
            InvokePublic(_combat, "SetRangedDamageBonus", this, 10f);
            InvokePublic(_combat, "SetMinigunEnabled", true);
            InvokePublic(_combat, "SetRangedDamageModifier", this, 3f);

            Assert.That(ReadFloatProperty(_combat, "EffectivePistolDamage"), Is.EqualTo(15f).Within(0.0001f));

            InvokePublic(_combat, "RemoveRangedDamageBonus", this);
            InvokePublic(_combat, "RemoveRangedDamageModifier", this);
            Assert.That(ReadFloatProperty(_combat, "EffectivePistolDamage"), Is.EqualTo(1f));
        }

        [Test]
        public void MinigunDoublesOnlyOrdinaryPrimaryCadence()
        {
            InvokePublic(_combat, "SetFireRateModifier", this, 1.5f);
            InvokePublic(_combat, "SetMinigunEnabled", true);

            Assert.That(ReadPrimaryInterval(), Is.EqualTo(0.5f / 3f).Within(0.0001f));

            InvokePublic(_combat, "SetUltimateActive", true);
            Assert.That(ReadPrimaryInterval(), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void HeadshotRoundCounter_AdvancesForRealPistolRoundsAndResetsExplicitly()
        {
            InvokePublic(_combat, "SetHeadshotEnabled", true);
            for (int i = 0; i < 4; i++) Assert.That(InvokeTryFireShot(), Is.True);

            Assert.That(ReadIntProperty(_combat, "OrdinaryPistolRoundsFired"), Is.EqualTo(4));

            InvokePublic(_combat, "ResetOrdinaryPistolRoundCount");
            Assert.That(ReadIntProperty(_combat, "OrdinaryPistolRoundsFired"), Is.Zero);
        }

        [Test]
        public void HeadshotFourthPistolRound_DealsDoubleDamageThroughTheRealHitscanPath()
        {
            GameObject muzzle = Track(new GameObject("Pistol Muzzle"));
            muzzle.transform.SetParent(_player.transform, false);
            muzzle.transform.forward = Vector3.forward;
            SetPrivateField(_combat, "muzzle", muzzle.transform);

            GameObject target = Track(new GameObject("Headshot Target"));
            target.transform.position = new Vector3(0f, 0f, 5f);
            target.AddComponent<BoxCollider>();
            Component targetHealth = target.AddComponent(RequireType("Combat.Health, Assembly-CSharp"));
            Physics.SyncTransforms();

            InvokePublic(_combat, "SetHeadshotEnabled", true);
            for (int index = 0; index < 4; index++)
                Assert.That(InvokeTryFireShot(), Is.True);

            // Three ordinary 15-damage rounds plus one 30-damage fourth round.
            Assert.That(ReadFloatProperty(targetHealth, "CurrentHealth"), Is.EqualTo(25f).Within(.0001f));
        }

        [Test]
        public void Explosion_ExcludesDirectTargetAndVampireHealsFromActualSplashDamage()
        {
            Component playerHealth = _player.GetComponent(RequireType("Combat.Health, Assembly-CSharp"));
            InvokePublic(playerHealth, "ApplyDamageAndGetApplied", 50f, Vector3.zero, null,
                Enum.Parse(RequireType("Combat.DamageType, Assembly-CSharp"), "Generic"));

            GameObject direct = Track(new GameObject("Direct Target"));
            direct.transform.position = new Vector3(5f, 0f, 0f);
            direct.AddComponent<BoxCollider>();
            Component directHealth = direct.AddComponent(RequireType("Combat.Health, Assembly-CSharp"));

            GameObject splash = Track(new GameObject("Splash Target"));
            splash.transform.position = new Vector3(6f, 0f, 0f);
            splash.AddComponent<BoxCollider>();
            Component splashHealth = splash.AddComponent(RequireType("Combat.Health, Assembly-CSharp"));
            Physics.SyncTransforms();

            InvokePublic(_combat, "SetVampireEnabled", true);
            InvokeNonPublic(_combat, "ApplyPistolExplosion", direct.transform.position, directHealth, 10f);

            Assert.That(ReadFloatProperty(directHealth, "CurrentHealth"), Is.EqualTo(100f).Within(.0001f));
            Assert.That(ReadFloatProperty(splashHealth, "CurrentHealth"), Is.EqualTo(90f).Within(.0001f));
            Assert.That(ReadFloatProperty(playerHealth, "CurrentHealth"), Is.EqualTo(50.2f).Within(.0001f));
        }

        [Test]
        public void AmmoCapacityModifiers_ComposeArchiveMinigunThenSecretMultiplier()
        {
            InvokePublic(_ammo, "SetCapacities", 20, 100, true);
            InvokePublic(_ammo, "SetMinigunCapacityEnabled", true);
            InvokePublic(_ammo, "SetCapacityMultiplier", this, 3f, true);

            Assert.That(ReadIntProperty(_ammo, "MagazineSize"), Is.EqualTo(150));
            Assert.That(ReadIntProperty(_ammo, "MaxStorage"), Is.EqualTo(900));

            // Future archive upgrades remain the unmodified base and recompute through both skills.
            InvokePublic(_ammo, "SetCapacities", 25, 120, true);
            Assert.That(ReadIntProperty(_ammo, "MagazineSize"), Is.EqualTo(165));
            Assert.That(ReadIntProperty(_ammo, "MaxStorage"), Is.EqualTo(960));
        }

        [Test]
        public void HealthReporting_ReturnsPostMitigationActualDamageWithoutOverkillCredit()
        {
            var targetObject = new GameObject("Health Target");
            Component health = targetObject.AddComponent(RequireType("Combat.Health, Assembly-CSharp"));
            InvokePublic(health, "SetIncomingDamageModifier", this, 0.5f);

            float applied = Convert.ToSingle(InvokePublic(health, "ApplyDamageAndGetApplied", 30f, Vector3.zero, _player,
                Enum.Parse(RequireType("Combat.DamageType, Assembly-CSharp"), "Generic")));
            Assert.That(applied, Is.EqualTo(15f).Within(0.0001f));
            Assert.That(ReadFloatProperty(health, "CurrentHealth"), Is.EqualTo(85f).Within(0.0001f));

            applied = Convert.ToSingle(InvokePublic(health, "ApplyDamageAndGetApplied", 200f, Vector3.zero, _player,
                Enum.Parse(RequireType("Combat.DamageType, Assembly-CSharp"), "Generic")));
            Assert.That(applied, Is.EqualTo(85f).Within(0.0001f));
            Assert.That(ReadFloatProperty(health, "CurrentHealth"), Is.Zero);
            UnityEngine.Object.DestroyImmediate(targetObject);
        }

        private float ReadPrimaryInterval()
        {
            PropertyInfo property = _combat.GetType().GetProperty("EffectivePrimaryFireInterval",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (float)property.GetValue(_combat);
        }

        private bool InvokeTryFireShot()
        {
            MethodInfo method = _combat.GetType().GetMethod("TryFireShot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(_combat, Array.Empty<object>());
        }

        private static object InvokePublic(Component target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static object InvokeNonPublic(Component target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static void SetPrivateField(Component target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private GameObject Track(GameObject created)
        {
            _createdObjects.Add(created);
            return created;
        }

        private static int ReadIntProperty(Component target, string propertyName) =>
            Convert.ToInt32(ReadProperty(target, propertyName));

        private static float ReadFloatProperty(Component target, string propertyName) =>
            Convert.ToSingle(ReadProperty(target, propertyName));

        private static object ReadProperty(Component target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(target);
        }

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName);
            Assert.That(type, Is.Not.Null, assemblyQualifiedName);
            return type;
        }
    }
}
