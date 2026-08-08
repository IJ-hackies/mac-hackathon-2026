using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Gameplay.Areas.Tests
{
    public sealed class SphericalPerimeterPolygonTests
    {
        [Test]
        public void Contains_CenterBoundaryAndOutside_AreClassified()
        {
            List<Vector3> ring = GameplayAreaTestFactory.CreateRing(
                Vector3.zero,
                Vector3.up,
                100f,
                10f,
                16);

            Assert.That(
                SphericalPerimeterPolygon.TryCreate(
                    ring,
                    Vector3.zero,
                    out SphericalPerimeterPolygon polygon,
                    out string error),
                Is.True,
                error);
            Assert.That(polygon.ContainsWorldPosition(Vector3.up * 100f), Is.True);
            Assert.That(polygon.ContainsWorldPosition(ring[0]), Is.True);
            Vector3 outside = GameplayAreaTestFactory.DirectionOffset(Vector3.up, 12f) * 100f;
            Assert.That(polygon.ContainsWorldPosition(outside), Is.False);
        }

        [Test]
        public void Contains_IgnoresRadialHeight()
        {
            List<Vector3> ring = GameplayAreaTestFactory.CreateRing(
                Vector3.zero,
                Vector3.up,
                100f,
                10f);
            Assert.That(
                SphericalPerimeterPolygon.TryCreate(
                    ring,
                    Vector3.zero,
                    out SphericalPerimeterPolygon polygon,
                    out string error),
                Is.True,
                error);

            Vector3 direction = GameplayAreaTestFactory.DirectionOffset(Vector3.up, 4f);
            Assert.That(polygon.ContainsWorldPosition(direction * 100f), Is.True);
            Assert.That(polygon.ContainsWorldPosition(direction * 112f), Is.True);
            Assert.That(polygon.ContainsWorldPosition(direction * 1000f), Is.True);
        }

        [Test]
        public void TryCreate_SortsUnorderedPoles()
        {
            List<Vector3> ordered = GameplayAreaTestFactory.CreateRing(
                new Vector3(7f, -3f, 2f),
                Vector3.left,
                80f,
                8f,
                9);
            var shuffled = new List<Vector3>
            {
                ordered[4], ordered[1], ordered[7], ordered[0], ordered[5],
                ordered[2], ordered[8], ordered[3], ordered[6]
            };

            Assert.That(
                SphericalPerimeterPolygon.TryCreate(
                    shuffled,
                    new Vector3(7f, -3f, 2f),
                    out SphericalPerimeterPolygon polygon,
                    out string error),
                Is.True,
                error);
            Vector3 centerPoint = new Vector3(7f, -3f, 2f) + Vector3.left * 80f;
            Assert.That(polygon.ContainsWorldPosition(centerPoint), Is.True);
        }

        [Test]
        public void TryCreate_RejectsInvalidPerimeters()
        {
            Assert.That(
                SphericalPerimeterPolygon.TryCreate(
                    new[] { Vector3.up, Vector3.right },
                    Vector3.zero,
                    out _,
                    out string tooFewError),
                Is.False);
            Assert.That(tooFewError, Does.Contain("at least three"));

            Assert.That(
                SphericalPerimeterPolygon.TryCreate(
                    new[] { Vector3.up, Vector3.up * 2f, Vector3.up * 3f },
                    Vector3.zero,
                    out _,
                    out string degenerateError),
                Is.False);
            Assert.That(degenerateError, Does.Contain("degenerate"));
        }
    }
}
