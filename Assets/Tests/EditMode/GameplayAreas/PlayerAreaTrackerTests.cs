using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Gameplay.Areas.Tests
{
    public sealed class PlayerAreaTrackerTests
    {
        private readonly List<GameObject> _createdRoots = new List<GameObject>();
        private Transform _planetCenter;
        private Transform _body;
        private PlayerAreaTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _planetCenter = Track(new GameObject("Planet Ground")).transform;
            _body = Track(new GameObject("Player Body")).transform;
            _tracker = _body.gameObject.AddComponent<PlayerAreaTracker>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _createdRoots.Count - 1; index >= 0; index--)
            {
                if (_createdRoots[index] != null)
                {
                    Object.DestroyImmediate(_createdRoots[index]);
                }
            }

            _createdRoots.Clear();
        }

        [Test]
        public void Evaluate_PublishesInitialEnterOnceAndThenExit()
        {
            GameplayArea area = TrackArea(GameplayAreaTestFactory.CreateArea(
                _planetCenter,
                GameplayAreaId.LandingBase,
                exitPadding: 0f));
            _tracker.Configure(_body, new[] { area });
            _body.position = Vector3.up * 100f;

            int entered = 0;
            int exited = 0;
            int changed = 0;
            _tracker.AreaEntered += enteredArea =>
            {
                Assert.That(enteredArea, Is.SameAs(area));
                entered++;
            };
            _tracker.AreaExited += exitedArea =>
            {
                Assert.That(exitedArea, Is.SameAs(area));
                exited++;
            };
            _tracker.AreaChanged += (_, _) => changed++;

            Assert.That(_tracker.EvaluateCurrentArea(), Is.SameAs(area));
            Assert.That(_tracker.EvaluateCurrentArea(), Is.SameAs(area));
            Assert.That(entered, Is.EqualTo(1));
            Assert.That(exited, Is.Zero);
            Assert.That(changed, Is.EqualTo(1));

            _body.position = GameplayAreaTestFactory.DirectionOffset(Vector3.up, 15f) * 100f;
            Assert.That(_tracker.EvaluateCurrentArea(), Is.Null);
            Assert.That(entered, Is.EqualTo(1));
            Assert.That(exited, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void Evaluate_UsesExitPaddingAsHysteresis()
        {
            GameplayArea area = TrackArea(GameplayAreaTestFactory.CreateArea(
                _planetCenter,
                GameplayAreaId.Arena1,
                angularRadiusDegrees: 10f,
                exitPadding: 5f));
            _tracker.Configure(_body, new[] { area });
            _body.position = Vector3.up * 100f;
            Assert.That(_tracker.EvaluateCurrentArea(), Is.SameAs(area));

            _body.position = GameplayAreaTestFactory.DirectionOffset(Vector3.up, 11f) * 100f;
            Assert.That(_tracker.EvaluateCurrentArea(), Is.SameAs(area));

            _body.position = GameplayAreaTestFactory.DirectionOffset(Vector3.up, 14f) * 100f;
            Assert.That(_tracker.EvaluateCurrentArea(), Is.Null);
        }

        [Test]
        public void Evaluate_SelectsHighestPriorityOverlap()
        {
            GameplayArea lower = TrackArea(GameplayAreaTestFactory.CreateArea(
                _planetCenter,
                GameplayAreaId.LandingBase,
                priority: 1));
            GameplayArea higher = TrackArea(GameplayAreaTestFactory.CreateArea(
                _planetCenter,
                GameplayAreaId.Arena2,
                priority: 5));
            _tracker.Configure(_body, new[] { lower, higher });
            _body.position = Vector3.up * 100f;

            Assert.That(_tracker.EvaluateCurrentArea(), Is.SameAs(higher));
        }

        [Test]
        public void Evaluate_UsesAreaIdAsDeterministicPriorityTieBreak()
        {
            GameplayArea arena = TrackArea(GameplayAreaTestFactory.CreateArea(
                _planetCenter,
                GameplayAreaId.Arena1,
                priority: 2));
            GameplayArea landingBase = TrackArea(GameplayAreaTestFactory.CreateArea(
                _planetCenter,
                GameplayAreaId.LandingBase,
                priority: 2));
            _tracker.Configure(_body, new[] { arena, landingBase });
            _body.position = Vector3.up * 100f;

            Assert.That(_tracker.EvaluateCurrentArea(), Is.SameAs(landingBase));
        }

        private GameObject Track(GameObject root)
        {
            _createdRoots.Add(root);
            return root;
        }

        private GameplayArea TrackArea(GameplayArea area)
        {
            Track(area.gameObject);
            return area;
        }
    }
}
