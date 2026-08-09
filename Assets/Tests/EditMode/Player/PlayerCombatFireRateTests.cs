using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player.Tests
{
    public sealed class PlayerCombatFireRateTests
    {
        private GameObject _player;
        private Component _ammo;
        private Component _combat;
        private readonly HashSet<int> _preExistingAudioManagerIds = new HashSet<int>();

        [SetUp]
        public void SetUp()
        {
            RememberExistingAudioManagers();
            _player = new GameObject("Player Combat Fire Rate Test");
            _ammo = _player.AddComponent(RequireType("Player.PlayerAmmo, Assembly-CSharp"));
            _combat = _player.AddComponent(RequireType("Player.PlayerCombat, Assembly-CSharp"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) UnityEngine.Object.DestroyImmediate(_player);
            DestroyTestCreatedAudioManagers();
        }

        [Test]
        public void RepeatedClicks_CannotFireAgainBeforePrimaryCadenceAllowsIt()
        {
            int startingRounds = ReadIntProperty(_ammo, "CurrentMagazine");

            InvokeFireStarted();
            InvokeFireCanceled();
            InvokeFireStarted();

            Assert.That(ReadIntProperty(_ammo, "CurrentMagazine"), Is.EqualTo(startingRounds - 1));

            SetPrivateField("_nextPrimaryFireTime", Time.time - 0.01f);
            InvokeFireCanceled();
            InvokeFireStarted();

            Assert.That(ReadIntProperty(_ammo, "CurrentMagazine"), Is.EqualTo(startingRounds - 2));
        }

        [Test]
        public void PrimaryCadence_UsesPlayerFireRateButUltimateKeepsItsFixedProfile()
        {
            InvokePublic("SetFireRateModifier", this, 1.45f);

            Assert.That(ReadEffectivePrimaryInterval(), Is.EqualTo(0.5f / 1.45f).Within(0.0001f));

            InvokePublic("SetUltimateActive", true);

            Assert.That(ReadEffectivePrimaryInterval(), Is.EqualTo(0.5f).Within(0.0001f));
        }

        private void InvokeFireStarted() => InvokeInputCallback("OnFireStarted");

        private void InvokeFireCanceled() => InvokeInputCallback("OnFireCanceled");

        private void InvokeInputCallback(string methodName)
        {
            MethodInfo method = _combat.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(_combat, new object[] { default(InputAction.CallbackContext) });
        }

        private float ReadEffectivePrimaryInterval()
        {
            PropertyInfo property = _combat.GetType().GetProperty("EffectivePrimaryFireInterval",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (float)property.GetValue(_combat);
        }

        private void SetPrivateField(string fieldName, object value)
        {
            FieldInfo field = _combat.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(_combat, value);
        }

        private void InvokePublic(string methodName, params object[] arguments)
        {
            MethodInfo method = _combat.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(_combat, arguments);
        }

        private static int ReadIntProperty(Component target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return Convert.ToInt32(property.GetValue(target));
        }

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName);
            Assert.That(type, Is.Not.Null, assemblyQualifiedName);
            return type;
        }

        private void RememberExistingAudioManagers()
        {
            _preExistingAudioManagerIds.Clear();
            Type type = RequireType("Audio.AudioManager, Assembly-CSharp");
            foreach (UnityEngine.Object instance in Resources.FindObjectsOfTypeAll(type))
            {
                _preExistingAudioManagerIds.Add(instance.GetInstanceID());
            }
        }

        private void DestroyTestCreatedAudioManagers()
        {
            Type type = RequireType("Audio.AudioManager, Assembly-CSharp");
            foreach (UnityEngine.Object instance in Resources.FindObjectsOfTypeAll(type))
            {
                if (_preExistingAudioManagerIds.Contains(instance.GetInstanceID())) continue;
                if (instance is Component component)
                {
                    UnityEngine.Object.DestroyImmediate(component.gameObject);
                }
            }
        }
    }
}
