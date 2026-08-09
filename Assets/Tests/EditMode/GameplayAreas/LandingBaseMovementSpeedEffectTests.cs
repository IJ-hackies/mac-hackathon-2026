using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Gameplay.Areas.Tests
{
    public sealed class LandingBaseMovementSpeedEffectTests
    {
        private const string ControllerTypeName =
            "Player.PlayerController, Assembly-CSharp";
        private const string EffectTypeName =
            "Player.LandingBaseMovementSpeedEffect, Assembly-CSharp";

        private readonly List<GameObject> _createdRoots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _createdRoots.Count - 1; index >= 0; index--)
            {
                if (_createdRoots[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdRoots[index]);
                }
            }

            _createdRoots.Clear();
        }

        [Test]
        public void LandingBase_AppliesDoubleSpeedAndPreservesOtherModifiersOnExit()
        {
            TestRig rig = CreateRig();
            object otherModifier = new object();
            Invoke(rig.Controller, "SetMovementSpeedModifier", otherModifier, 1.5f);

            Assert.That(ReadFloat(rig.Controller, "MovementSpeedMultiplier"), Is.EqualTo(1.5f));
            Assert.That(ReadFloat(rig.Controller, "EffectiveMoveSpeed"), Is.EqualTo(14.625f));

            rig.Body.position = Vector3.up * 100f;
            Assert.That(rig.Tracker.EvaluateCurrentArea(), Is.SameAs(rig.LandingBase));
            Assert.That(ReadFloat(rig.Controller, "MovementSpeedMultiplier"), Is.EqualTo(3f));
            Assert.That(ReadFloat(rig.Controller, "EffectiveMoveSpeed"), Is.EqualTo(29.25f));
            WriteFloat(rig.Controller, "_currentSpeed", 29.25f);

            rig.Body.position = GameplayAreaTestFactory.DirectionOffset(Vector3.up, 15f) * 100f;
            Assert.That(rig.Tracker.EvaluateCurrentArea(), Is.Null);
            Assert.That(ReadFloat(rig.Controller, "MovementSpeedMultiplier"), Is.EqualTo(1.5f));
            Assert.That(ReadFloat(rig.Controller, "EffectiveMoveSpeed"), Is.EqualTo(14.625f));
            Assert.That(ReadFloat(rig.Controller, "CurrentHorizontalSpeed"), Is.EqualTo(14.625f));
        }

        [Test]
        public void ReenableInsideLandingBase_ReappliesCurrentAreaAndDisableRestoresSpeed()
        {
            TestRig rig = CreateRig();
            rig.Body.position = Vector3.up * 100f;
            rig.Tracker.EvaluateCurrentArea();
            Assert.That(ReadFloat(rig.Controller, "MovementSpeedMultiplier"), Is.EqualTo(2f));
            Assert.That(ReadFloat(rig.Controller, "EffectiveMoveSpeed"), Is.EqualTo(19.5f));

            InvokeNonPublic(rig.Effect, "OnDisable");
            Assert.That(ReadFloat(rig.Controller, "MovementSpeedMultiplier"), Is.EqualTo(1f));
            Assert.That(ReadFloat(rig.Controller, "EffectiveMoveSpeed"), Is.EqualTo(9.75f));

            InvokeNonPublic(rig.Effect, "OnEnable");
            Assert.That(ReadFloat(rig.Controller, "MovementSpeedMultiplier"), Is.EqualTo(2f));
            Assert.That(ReadFloat(rig.Controller, "EffectiveMoveSpeed"), Is.EqualTo(19.5f));
        }

        private TestRig CreateRig()
        {
            Type controllerType = RequireType(ControllerTypeName);
            Type effectType = RequireType(EffectTypeName);

            GameObject planet = Track(new GameObject("Planet Ground"));
            GameObject rigRoot = Track(new GameObject("PlayerRig"));
            GameObject body = new GameObject("Player");
            body.transform.SetParent(rigRoot.transform);

            Component controller = body.AddComponent(controllerType);
            PlayerAreaTracker tracker = rigRoot.AddComponent<PlayerAreaTracker>();
            GameplayArea landingBase = GameplayAreaTestFactory.CreateArea(
                planet.transform,
                GameplayAreaId.LandingBase,
                exitPadding: 0f);
            Track(landingBase.gameObject);
            tracker.Configure(body.transform, new[] { landingBase });

            Behaviour effect = (Behaviour)rigRoot.AddComponent(effectType);
            Invoke(effect, "Configure", tracker, controller, 2f);

            return new TestRig(
                body.transform,
                controller,
                tracker,
                effect,
                landingBase);
        }

        private GameObject Track(GameObject root)
        {
            _createdRoots.Add(root);
            return root;
        }

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName);
            Assert.That(type, Is.Not.Null, assemblyQualifiedName);
            return type;
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static float ReadFloat(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (float)property.GetValue(target);
        }

        private static void WriteFloat(object target, string fieldName, float value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private readonly struct TestRig
        {
            public TestRig(
                Transform body,
                Component controller,
                PlayerAreaTracker tracker,
                Behaviour effect,
                GameplayArea landingBase)
            {
                Body = body;
                Controller = controller;
                Tracker = tracker;
                Effect = effect;
                LandingBase = landingBase;
            }

            public Transform Body { get; }
            public Component Controller { get; }
            public PlayerAreaTracker Tracker { get; }
            public Behaviour Effect { get; }
            public GameplayArea LandingBase { get; }
        }
    }
}
