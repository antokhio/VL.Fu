using Stride.Core.Mathematics;
using VL.Fu.Behaviors;
using VL.Fu.Core.Interaction;

namespace VL.Fu.Tests.Behaviors
{
    /// <summary>
    /// <see cref="Transformable"/> on Front (at (30, 30), 40 × 40, under the untransformed root, so its parent space is the window):
    /// a drag translates, a pinch scales and turns about the touches, and the transform origin doesn't move what is under them.
    /// </summary>
    /// <remarks>
    /// Results are checked where they show: a point of the node is mapped through the transformation about the node's pivot,
    /// as the node draws it, and compared with where the touch on it is.
    /// </remarks>
    [TestClass]
    public sealed class TransformableTests
    {
        private static readonly Vector2 FrontPosition = new(30, 30);
        private const float FrontSize = 40;

        [TestMethod]
        public void Drag_Translates()
        {
            using var scene = new BehaviorScene();
            using var transformation = new Recorder<Matrix>(Matrix.Identity);
            scene.Attach(scene.Front, Connect(new Transformable(null!), transformation));

            scene.Down(60, 60);
            scene.Move(70, 65);
            Assert.IsTrue(scene.Up(70, 65));

            AssertClose(new Vector2(70, 65), Shown(transformation.Value, Vector2.Zero, new Vector2(30, 30)));
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        public void Drag_WithoutTranslate_WritesNothing_AndLeavesThePointer()
        {
            using var scene = new BehaviorScene();
            using var transformation = new Recorder<Matrix>(Matrix.Identity);
            var transformable = Connect(new Transformable(null!), transformation);
            transformable.SetAllow(translate: false);
            scene.Attach(scene.Front, transformable);

            scene.Down(60, 60);
            scene.Move(70, 65);

            Assert.AreEqual(0, transformation.Values.Count);
            Assert.IsNull(scene.Claims.ClaimOf(PointerIds.Mouse));
        }

        [TestMethod]
        [DataRow(0f, 0f)]
        [DataRow(-1f, -1f)]
        public void Pinch_KeepsWhatIsUnderTheTouchesUnderThem_WhateverTheOrigin(float originX, float originY)
        {
            using var scene = new BehaviorScene();
            using var transformation = new Recorder<Matrix>(Matrix.Identity);
            var origin = new Vector2(originX, originY);
            scene.Front.SetTransformOrigin(origin);
            scene.Attach(scene.Front, Connect(new Transformable(null!), transformation));

            scene.TouchDown(7, 45, 45);
            scene.TouchDown(8, 65, 65);
            scene.TouchMove(8, 75, 75);

            // Scaled by 1.5 about (55, 55), which moved to (60, 60).
            AssertClose(new Vector2(45, 45), Shown(transformation.Value, origin, new Vector2(15, 15)));
            AssertClose(new Vector2(75, 75), Shown(transformation.Value, origin, new Vector2(35, 35)));

            scene.TouchUp(8, 75, 75);
            Assert.IsNull(scene.Claims.ClaimOf(7));
            Assert.IsNull(scene.Claims.ClaimOf(8));
        }

        [TestMethod]
        [DataRow(true, 45f, 70f)]
        [DataRow(false, 55f, 60f)]
        public void Pinch_TurnsOnlyWhenRotateIsAllowed(bool rotate, float expectedX, float expectedY)
        {
            using var scene = new BehaviorScene();
            using var transformation = new Recorder<Matrix>(Matrix.Identity);
            var transformable = Connect(new Transformable(null!), transformation);
            transformable.SetAllow(rotate: rotate);
            scene.Attach(scene.Front, transformable);

            // A quarter turn about touch 7, which stays: the center moves from (55, 50) to (45, 60).
            scene.TouchDown(7, 45, 50);
            scene.TouchDown(8, 65, 50);
            scene.TouchMove(8, 45, 70);

            AssertClose(new Vector2(expectedX, expectedY), Shown(transformation.Value, Vector2.Zero, new Vector2(35, 20)));
        }

        [TestMethod]
        [DataRow(true, 65f)]
        [DataRow(false, 60f)]
        public void Pinch_ScalesOnlyWhenScaleIsAllowed(bool scale, float expectedX)
        {
            using var scene = new BehaviorScene();
            using var transformation = new Recorder<Matrix>(Matrix.Identity);
            var transformable = Connect(new Transformable(null!), transformation);
            transformable.SetAllow(scale: scale);
            scene.Attach(scene.Front, transformable);

            // Twice as far apart: the center moves from (50, 50) to (55, 50).
            scene.TouchDown(7, 45, 50);
            scene.TouchDown(8, 55, 50);
            scene.TouchMove(8, 65, 50);

            AssertClose(new Vector2(expectedX, 50), Shown(transformation.Value, Vector2.Zero, new Vector2(25, 20)));
        }

        [TestMethod]
        public void Pinch_WithoutTranslate_KeepsTheCenterAtThePairing()
        {
            using var scene = new BehaviorScene();
            using var transformation = new Recorder<Matrix>(Matrix.Identity);
            var transformable = Connect(new Transformable(null!), transformation);
            transformable.SetAllow(translate: false);
            scene.Attach(scene.Front, transformable);

            scene.TouchDown(7, 45, 50);
            scene.TouchDown(8, 55, 50);
            scene.TouchMove(8, 65, 50);

            AssertClose(new Vector2(50, 50), Shown(transformation.Value, Vector2.Zero, new Vector2(20, 20)));
            AssertClose(new Vector2(60, 50), Shown(transformation.Value, Vector2.Zero, new Vector2(25, 20)));
        }

        [TestMethod]
        public void PinchAfterADrag_CarriesOnFromTheDrag_WithoutResetting()
        {
            using var scene = new BehaviorScene();
            using var transformation = new Recorder<Matrix>(Matrix.Identity);
            scene.Attach(scene.Front, Connect(new Transformable(null!), transformation));

            scene.TouchDown(7, 45, 45);
            scene.TouchMove(7, 55, 45);
            AssertClose(new Vector2(55, 45), Shown(transformation.Value, Vector2.Zero, new Vector2(15, 15)));
            var writesByTheDrag = transformation.Values.Count;

            // The node point under touch 8 is (25, 35): (65, 65) minus the drag's (10, 0) and Front's (30, 30).
            scene.TouchDown(8, 65, 65);
            scene.TouchMove(8, 75, 75);

            Assert.IsTrue(transformation.Values.Count > writesByTheDrag);
            Assert.IsFalse(transformation.Values.Contains(Matrix.Identity));
            AssertClose(new Vector2(55, 45), Shown(transformation.Value, Vector2.Zero, new Vector2(15, 15)));
            AssertClose(new Vector2(75, 75), Shown(transformation.Value, Vector2.Zero, new Vector2(25, 35)));
        }

        [TestMethod]
        public void Escape_ReturnsToTheTransformationAtTheStart()
        {
            using var scene = new BehaviorScene();
            var start = Matrix.RotationZ(0.3f);
            using var transformation = new Recorder<Matrix>(start);
            scene.Attach(scene.Front, Connect(new Transformable(null!), transformation));
            scene.Down(60, 60);
            scene.Move(70, 65);
            Assert.AreNotEqual(start, transformation.Value);

            scene.Escape();

            Assert.AreEqual(start, transformation.Value);
        }

        [TestMethod]
        public void TransformableAndMovable_OnOneNode_Warn()
        {
            using var scene = new BehaviorScene();
            var transformable = new Transformable(null!);
            var movable = new Movable(null!);
            scene.Attach(scene.Front, transformable, movable);

            scene.Down(60, 60);

            Assert.IsTrue(transformable.WarnedMovable);
            Assert.IsTrue(movable.WarnedTransformable);
        }

        private static Transformable Connect(Transformable transformable, Recorder<Matrix> transformation)
        {
            transformable.SetTransformation(transformation.Channel);
            return transformable;
        }

        /// <summary>
        /// Where Front shows <paramref name="pointInNode"/> on screen with <paramref name="transformation"/> applied about
        /// the pivot that <paramref name="origin"/> picks: the pivot's place in the parent plus the transformed way to the point.
        /// </summary>
        internal static Vector2 Shown(Matrix transformation, Vector2 origin, Vector2 pointInNode) =>
            Shown(FrontPosition, FrontSize, transformation, origin, pointInNode);

        /// <inheritdoc cref="Shown(Matrix, Vector2, Vector2)"/>
        internal static Vector2 Shown(Vector2 position, float size, Matrix transformation, Vector2 origin, Vector2 pointInNode)
        {
            var pivot = (origin + Vector2.One) / 2 * size;
            return position + pivot + Vector2.TransformCoordinate(pointInNode - pivot, transformation);
        }

        internal static void AssertClose(Vector2 expected, Vector2 actual, float tolerance = 1e-3f)
        {
            Assert.IsTrue(Vector2.Distance(expected, actual) <= tolerance, $"Expected {expected}, got {actual}.");
        }
    }
}
