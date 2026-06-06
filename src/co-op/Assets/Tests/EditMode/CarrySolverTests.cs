using NUnit.Framework;
using UnityEngine;
using Gameplay.Player.Carry;

public class CarrySolverTests
{
    private const float Eps = 0.001f;

    [Test]
    public void SingleHolder_PlacesAnchorAtGripPoint()
    {
        var grip = new HolderGrip(new Vector3(0, 1, 2), Vector3.forward, Vector3.up);
        var anchorLocal = new Vector3(0, 0, -0.5f);

        var t = CarrySolver.SolveTarget(new[] { grip }, new[] { anchorLocal }, Vector3.up);

        var anchorWorld = t.Position + t.Rotation * anchorLocal;
        Assert.That(Vector3.Distance(anchorWorld, grip.GripPoint), Is.LessThan(Eps));
    }

    [Test]
    public void SingleHolder_FacesAlongForward()
    {
        var grip = new HolderGrip(Vector3.zero, Vector3.forward, Vector3.up);
        var t = CarrySolver.SolveTarget(new[] { grip }, new[] { Vector3.zero }, Vector3.up);
        Assert.That(Vector3.Dot(t.Rotation * Vector3.forward, Vector3.forward), Is.GreaterThan(0.99f));
    }

    [Test]
    public void TwoHolders_MidpointOfAnchorsLandsAtMidpointOfGrips()
    {
        var g1 = new HolderGrip(new Vector3(-1, 0, 0), Vector3.forward, Vector3.up);
        var g2 = new HolderGrip(new Vector3(1, 0, 0), Vector3.forward, Vector3.up);
        var a1 = new Vector3(-0.5f, 0, 0);
        var a2 = new Vector3(0.5f, 0, 0);

        var t = CarrySolver.SolveTarget(new[] { g1, g2 }, new[] { a1, a2 }, Vector3.up);

        var w1 = t.Position + t.Rotation * a1;
        var w2 = t.Position + t.Rotation * a2;
        var midWorld = (w1 + w2) * 0.5f;
        var midGrip = (g1.GripPoint + g2.GripPoint) * 0.5f;
        Assert.That(Vector3.Distance(midWorld, midGrip), Is.LessThan(Eps));
    }

    [Test]
    public void TwoHolders_AlignsAnchorAxisToGripAxis()
    {
        var g1 = new HolderGrip(new Vector3(-1, 0, 0), Vector3.forward, Vector3.up);
        var g2 = new HolderGrip(new Vector3(1, 0, 0), Vector3.forward, Vector3.up);
        var a1 = new Vector3(0, 0, -0.5f);
        var a2 = new Vector3(0, 0, 0.5f);
        var t = CarrySolver.SolveTarget(new[] { g1, g2 }, new[] { a1, a2 }, Vector3.up);

        var w1 = t.Position + t.Rotation * a1;
        var w2 = t.Position + t.Rotation * a2;
        var worldAxis = (w2 - w1).normalized;
        Assert.That(Vector3.Dot(worldAxis, (g2.GripPoint - g1.GripPoint).normalized), Is.GreaterThan(0.99f));
    }

    [Test]
    public void FollowVelocity_IsClampedToMaxSpeed()
    {
        var v = CarrySolver.FollowVelocity(Vector3.zero, new Vector3(100, 0, 0), dt: 0.02f, maxSpeed: 8f, responsiveness: 20f);
        Assert.That(v.magnitude, Is.LessThanOrEqualTo(8f + Eps));
    }

    [Test]
    public void FollowVelocity_PointsTowardTarget()
    {
        var v = CarrySolver.FollowVelocity(Vector3.zero, new Vector3(0, 0, 5), dt: 0.02f, maxSpeed: 8f, responsiveness: 20f);
        Assert.That(Vector3.Dot(v.normalized, Vector3.forward), Is.GreaterThan(0.99f));
    }
}
