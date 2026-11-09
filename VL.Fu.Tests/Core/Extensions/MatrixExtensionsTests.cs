using VL.Fu.Core.Extensions;
using SkiaSharp;
using Stride.Core.Mathematics;

namespace VL.Fu.Tests.Core.Extensions
{
    [TestClass]
    public sealed class MatrixExtensionsTests
    {
        private const float Tolerance = 1e-4f;

        private static readonly Vector2[] Points = { new(0, 0), new(1, 0), new(0, 1), new(3.5f, -2f) };

        private static Matrix Affine() =>
            Matrix.Scaling(2, 3, 1) * Matrix.RotationZ(0.3f) * Matrix.Translation(10, -4, 0);

        private static void AssertMapsLikeStride(Matrix stride, SKMatrix skia)
        {
            foreach (var p in Points)
            {
                var expected = Vector3.TransformCoordinate(new Vector3(p, 0), stride);
                var actual = skia.MapPoint(p.X, p.Y);
                Assert.AreEqual(expected.X, actual.X, Tolerance, $"x of {p}");
                Assert.AreEqual(expected.Y, actual.Y, Tolerance, $"y of {p}");
            }
        }

        private static void AssertEqual(SKMatrix expected, SKMatrix actual)
        {
            CollectionAssert.AreEqual(expected.Values, actual.Values);
        }

        [TestMethod]
        public void ToSKMatrix_MapsPointsLikeStride()
        {
            var m = Affine();

            AssertMapsLikeStride(m, m.ToSKMatrix());
        }

        [TestMethod]
        public void ToSKMatrix_Translation_MovesPoint()
        {
            var p = Matrix.Translation(3, 4, 0).ToSKMatrix().MapPoint(1, 2);

            Assert.AreEqual(new SKPoint(4, 6), p);
        }

        [TestMethod]
        public void ToSKMatrix_StrideProduct_EqualsSkiaPostConcat()
        {
            var a = Matrix.Scaling(2, 2, 1);
            var b = Matrix.Translation(10, 0, 0);

            var product = (a * b).ToSKMatrix();

            AssertEqual(a.ToSKMatrix().PostConcat(b.ToSKMatrix()), product);
            Assert.AreEqual(new SKPoint(12, 2), product.MapPoint(1, 1));
        }

        [TestMethod]
        public void ToSKMatrix_DropsZ()
        {
            var m = Matrix.Translation(0, 0, 5) * Matrix.Scaling(1, 1, 7);

            AssertEqual(SKMatrix.Identity, m.ToSKMatrix());
        }

        [TestMethod]
        public void ToMatrix_MapsPointsLikeSkia()
        {
            var skia = SKMatrix.CreateRotationDegrees(30).PostConcat(SKMatrix.CreateTranslation(5, 6));

            AssertMapsLikeStride(skia.ToMatrix(), skia);
        }

        [TestMethod]
        public void RoundTrip_FromSkia_KeepsAllValuesIncludingPerspective()
        {
            var skia = new SKMatrix(2, 0.5f, 10, -0.25f, 3, -4, 0.001f, 0.002f, 1);

            AssertEqual(skia, skia.ToMatrix().ToSKMatrix());
        }

        [TestMethod]
        public void RoundTrip_From2DStride_KeepsMatrix()
        {
            var m = Affine();

            Assert.AreEqual(m, m.ToSKMatrix().ToMatrix());
        }
    }
}
