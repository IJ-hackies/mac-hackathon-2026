using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Progression.Tests
{
    public sealed class StationInteractionBehaviorTests
    {
        private GameObject _station;
        private GameObject _controller;
        private GameObject _promptHost;
        private GameObject _promptRoot;
        private GameObject _menuHost;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_station != null) UnityEngine.Object.DestroyImmediate(_station);
            if (_controller != null) UnityEngine.Object.DestroyImmediate(_controller);
            if (_promptHost != null) UnityEngine.Object.DestroyImmediate(_promptHost);
            if (_promptRoot != null) UnityEngine.Object.DestroyImmediate(_promptRoot);
            if (_menuHost != null) UnityEngine.Object.DestroyImmediate(_menuHost);
        }

        [Test]
        public void StationRange_UsesConfiguredWorldDistance()
        {
            Component station = CreateStation(8f);
            Assert.That(Invoke<bool>(station, "IsInRange", new Vector3(7.99f, 0f, 0f)), Is.True);
            Assert.That(Invoke<bool>(station, "IsInRange", new Vector3(8.01f, 0f, 0f)), Is.False);
        }

        [Test]
        public void ControllerDistanceFallback_ShowsAndHidesPromptWithoutTriggerCallbacks()
        {
            CreateStation(8f);
            Type promptType = RequireType("Gameplay.Interaction.InteractionPromptView, Assembly-CSharp");
            Type controllerType = RequireType("Gameplay.Interaction.StationInteractionController, Assembly-CSharp");

            _promptHost = new GameObject("Prompt Host");
            Component prompt = _promptHost.AddComponent(promptType);
            _promptRoot = new GameObject("Prompt Root");
            SetField(prompt, "root", _promptRoot);
            _promptRoot.SetActive(false);

            _controller = new GameObject("Interaction Controller");
            _controller.transform.position = new Vector3(7f, 0f, 0f);
            Component controller = _controller.AddComponent(controllerType);
            SetField(controller, "prompt", prompt);
            InvokeLifecycle(controller, "Awake");
            InvokeLifecycle(controller, "Update");
            Assert.That(_promptRoot.activeSelf, Is.True);

            _controller.transform.position = new Vector3(9f, 0f, 0f);
            InvokeLifecycle(controller, "Update");
            Assert.That(_promptRoot.activeSelf, Is.False);
        }

        [Test]
        public void Controller_WhenInteractionIsAttemptedInRange_OpensStationMenu()
        {
            CreateStation(8f);
            Type promptType = RequireType("Gameplay.Interaction.InteractionPromptView, Assembly-CSharp");
            Type menuType = RequireType("Gameplay.Interaction.StationMenuController, Assembly-CSharp");
            Type controllerType = RequireType("Gameplay.Interaction.StationInteractionController, Assembly-CSharp");

            _promptHost = new GameObject("Prompt Host");
            Component prompt = _promptHost.AddComponent(promptType);
            _promptRoot = new GameObject("Prompt Root");
            SetField(prompt, "root", _promptRoot);
            _promptRoot.SetActive(false);

            _menuHost = new GameObject("Menu Host");
            Component menu = _menuHost.AddComponent(menuType);
            var shell = new GameObject("Shell");
            var supply = new GameObject("Supply");
            shell.transform.SetParent(_menuHost.transform);
            supply.transform.SetParent(_menuHost.transform);
            SetField(menu, "shellRoot", shell);
            SetField(menu, "supplyRoot", supply);
            InvokeLifecycle(menu, "Awake");

            _controller = new GameObject("Interaction Controller");
            _controller.transform.position = new Vector3(7f, 0f, 0f);
            Component controller = _controller.AddComponent(controllerType);
            SetField(controller, "prompt", prompt);
            SetField(controller, "stationMenu", menu);
            InvokeLifecycle(controller, "Awake");

            InvokeLifecycle(controller, "Update");
            Assert.That(Invoke<bool>(controller, "TryInteract"), Is.True);
            Assert.That(GetProperty<bool>(menu, "IsOpen"), Is.True);
            Assert.That(shell.activeSelf, Is.True);
            Assert.That(supply.activeSelf, Is.True);
        }

        [Test]
        public void Menu_IgnoresCloseInputDuringItsOpeningFrame()
        {
            Component station = CreateStation(8f);
            Type menuType = RequireType("Gameplay.Interaction.StationMenuController, Assembly-CSharp");

            _menuHost = new GameObject("Menu Host");
            Component menu = _menuHost.AddComponent(menuType);
            var shell = new GameObject("Shell");
            shell.transform.SetParent(_menuHost.transform);
            SetField(menu, "shellRoot", shell);
            InvokeLifecycle(menu, "Awake");

            InvokeVoid(menu, "Open", station);
            Assert.That(GetProperty<bool>(menu, "IsOpen"), Is.True);
            Assert.That(InvokePrivate<bool>(menu, "TryCloseFromInput"), Is.False);
            Assert.That(GetProperty<bool>(menu, "IsOpen"), Is.True);

            SetField(menu, "_openedFrame", Time.frameCount - 1);
            Assert.That(InvokePrivate<bool>(menu, "TryCloseFromInput"), Is.True);
            Assert.That(GetProperty<bool>(menu, "IsOpen"), Is.False);
        }

        private Component CreateStation(float radius)
        {
            _station = new GameObject("Station");
            Component station = _station.AddComponent(
                RequireType("Gameplay.Interaction.InteractableStation, Assembly-CSharp"));
            SetField(station, "interactionRadius", radius);
            return station;
        }

        private static Type RequireType(string name)
        {
            Type type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static T Invoke<T>(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, name);
            return (T)method.Invoke(target, arguments);
        }

        private static void InvokeVoid(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, arguments);
        }

        private static T InvokePrivate<T>(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, name);
            return (T)method.Invoke(target, null);
        }

        private static T GetProperty<T>(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(target);
        }

        private static void InvokeLifecycle(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, null);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
