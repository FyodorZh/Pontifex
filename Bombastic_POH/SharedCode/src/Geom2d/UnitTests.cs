using System;
using UT;

namespace Geom2d
{
    static class UnitTests
    {
        [UT.UT("Geom2d intersections")]
        private static void UT_Line(UT.IUTest ut)
        {
            UT_CircleWithPoint(ut);
            UT_CircleWithCircle(ut);

            UT_RectWithPoint(ut);
            UT_RectWithRect(ut);
            UT_RectWithCircle(ut);
            UT_RectWithCircleSector(ut);

            UT_RectWithQuad(ut);

            UT_Lines(ut);

            UT_CheckAlgorithms(ut);

            UT_LineWithPont(ut);
            UT_LineWithCircle(ut);

            UT_SircleSector(ut);
        }

        private static void UT_RectWithQuad(IUTest ut)
        {
            Quad q = new Quad(new Vector(-8, -4), new Vector(6, -4), new Vector(-5, 10), new Vector(-12, 2));

            // Too far
            ut.Assert(!q.Contains(new Rect(new Vector(-20, 12), 6, 4)));
            ut.Assert(!q.Intersect(new Rect(new Vector(-20, 12), 6, 4)));
            // Ok
            ut.Assert(q.Contains(new Rect(new Vector(-8, -2), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-8, -2), 6, 4)));
            // Ok
            ut.Assert(q.Contains(new Rect(new Vector(-8, 2), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-8, -2), 6, 4)));
            // Ok
            ut.Assert(q.Contains(new Rect(new Vector(-8, -4), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-8, -2), 6, 4)));
            // весь снаружи - 00
            ut.Assert(!q.Contains(new Rect(new Vector(-17, -6), 6, 4)));
            ut.Assert(!q.Intersect(new Rect(new Vector(-17, -6), 6, 4)));
            // 11
            ut.Assert(!q.Contains(new Rect(new Vector(-7, 3), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-7, 3), 6, 4)));
            // 01
            ut.Assert(!q.Contains(new Rect(new Vector(0, -2), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(0, -2), 6, 4)));
            // 10
            ut.Assert(!q.Contains(new Rect(new Vector(-9, 2), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-9, 2), 6, 4)));
            // 00
            ut.Assert(!q.Contains(new Rect(new Vector(-12, -2), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-9, 2), 6, 4)));
            // 10
            ut.Assert(!q.Contains(new Rect(new Vector(-12, 2), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-12, 2), 6, 4)));
            // 00
            ut.Assert(!q.Contains(new Rect(new Vector(-2, -6), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-2, -6), 6, 4)));

            ut.Assert(q.Intersect(new Rect(new Vector(2, -6), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-18, -2), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-6, 8), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-12, -6), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-4, -2), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-8, 8), 6, 4)));
            ut.Assert(q.Intersect(new Rect(new Vector(-16, 0), 6, 4)));
            
            ut.Assert(!q.Intersect(new Rect(new Vector(2, 2), 6, 4)));
            ut.Assert(!q.Intersect(new Rect(new Vector(-8, 12), 6, 4)));
            ut.Assert(!q.Intersect(new Rect(new Vector(-17, -5), 6, 4)));

            ut.Assert(q.Intersect(new Rect(new Vector(-10, -2), 16, 8)));
        }

        private static void UT_RectWithCircleSector(IUTest ut)
        {
            Rect rc = new Rect(new Vector(6, 4), new Vector(17, 9));

            // Too far
            ut.Assert(!rc.Intersect(new CircleSector(new Vector(24, 2), new Vector(18, 5), Rotation.ToRadians(28))));
            // inside
            ut.Assert(rc.Intersect(new CircleSector(new Vector(9, 5), new Vector(17, 6), Rotation.ToRadians(28))));
            // Closest point (between rays)
            ut.Assert(rc.Intersect(new CircleSector(new Vector(27, 7), new Vector(17, 6), Rotation.ToRadians(300))));
            // Closest poin (between rays)
            ut.Assert(rc.Intersect(new CircleSector(new Vector(27, 7), new Vector(17, 8), Rotation.ToRadians(28))));
            // Closest poin (between rays)
            ut.Assert(rc.Intersect(new CircleSector(new Vector(4, 14), new Vector(14, 6), Rotation.ToRadians(74))));
            // Not
            ut.Assert(!rc.Intersect(new CircleSector(new Vector(4, 14), new Vector(10, 14), Rotation.ToRadians(70))));
            // L1
            ut.Assert(rc.Intersect(new CircleSector(new Vector(4, 14), new Vector(10, 14), Rotation.ToRadians(128))));
            // L0
            ut.Assert(rc.Intersect(new CircleSector(new Vector(2, 0), new Vector(-8, -10), Rotation.ToRadians(320))));
            // L3
            ut.Assert(rc.Intersect(new CircleSector(new Vector(0, 0), new Vector(-8, -10), Rotation.ToRadians(320))));
            // L2
            ut.Assert(rc.Intersect(new CircleSector(new Vector(20, 2), new Vector(18, 9), Rotation.ToRadians(20))));
            // Not
            ut.Assert(!rc.Intersect(new CircleSector(new Vector(2, 0), new Vector(-8, -10), Rotation.ToRadians(300))));
            // Not
            ut.Assert(!rc.Intersect(new CircleSector(new Vector(18, 8), new Vector(17, 10), Rotation.ToRadians(20))));
        }

        private static void UT_Lines(IUTest ut)
        {

            Line l = new Line(Vector.Zero, 2 * Vector.One);

            ut.Assert(Line.Intersect(l, l));
            ut.Assert(Line.Intersect(l, new Line(Vector.Zero, Vector.Ordinate)));
            ut.Assert(Line.Intersect(l, new Line(2 * Vector.One, Vector.Ordinate)));

            ut.Assert(Line.Intersect(l, new Line(Vector.Ordinate, Vector.Abscissa)));
            ut.Assert(Line.Intersect(l, new Line(Vector.Abscissa, 2 * Vector.Ordinate)));
            ut.Assert(Line.Intersect(l, new Line(Vector.Ordinate, 2 * Vector.Abscissa)));

            ut.Assert(!Line.Intersect(l, new Line(-Vector.Ordinate, -Vector.Abscissa)));
            ut.Assert(!Line.Intersect(l, new Line(-3 * Vector.One, new Vector(-1f, 4f))));

            ut.Assert(!Line.Intersect(l, new Line(Vector.Zero + new Vector(0f, 0.2f), 2 * Vector.One + new Vector(0f, 0.2f))));
            ut.Assert(!Line.Intersect(l, new Line(Vector.Zero + new Vector(0f, 0.2f), 2 * Vector.One + new Vector(0f, 1f))));
            ut.Assert(!Line.Intersect(l, new Line(Vector.Zero + new Vector(0f, -0.2f), 2 * Vector.One + new Vector(0f, -1f))));
        }

        private static void UT_CheckAlgorithms(IUTest ut)
        {
            CheckAlgorithm(ut: ut,
                pos: new Vector(0, 0),
                c: new Circle(0.36f, 3.29f, 0.8f),
                size: new Vector(0.8f, 2f),
                offset: new Vector(0f, 1f));

            CheckAlgorithm(ut: ut,
                pos: new Vector(-92.4f, 5.0651f),
                c: new Circle(-92.03586f, 8.355892f, 0.8f),
                size: new Vector(0.8f, 2f),
                offset: new Vector(0f, 1f));

            CheckAlgorithm(ut: ut,
                pos: new Vector(-92.4f, 5.0651f),
                c: new Circle(-93.58575f, 6.726146f, 0.8f),
                size: new Vector(0.8f, 2f),
                offset: new Vector(0f, 1f));

            CheckAlgorithm(ut: ut,
                pos: new Vector(-97.97993f, 1.7451f),
                c: new Circle(-97.97993f, 3.1451f, 0.4f),
                size: new Vector(0.8f, 1.88f),
                offset: new Vector(0f, 0.94f));
        }

        private static void CheckAlgorithm(IUTest ut, Vector pos, Circle c, Vector size, Vector offset)
        {
            var quad = Quad.Rect(pos + offset, size.x, size.y, Vector.Abscissa);
            bool qi = quad.Intersect(c);

            var rc = new Rect(offset.x - size.x/2, offset.y - size.y/2, size.x, size.y);
            var rect = Rect.Translate(rc, pos);

            var quad2 = Quad.Rect(rect);
            bool qi2 = quad2.Intersect(c);

            bool ri = rect.Intersect(c);

            ut.Equal(qi, qi2);

            ut.Equal(qi, ri);
        }

        private static void UT_RectWithCircle(IUTest ut)
        {
            Rect rc = Rect.UnitSquare;

            ut.Equal(rc.Contains(new Circle(0.5f, 0.5f, 0.5f)), true);
            ut.Equal(rc.Contains(new Circle(0.5f, 0.6f, 0.5f)), false);
            ut.Equal(rc.Contains(new Circle(1, 1f, 1)), false);
            ut.Equal(rc.Contains(new Circle(1, 1f, 5)), false);

            ut.Equal(rc.Intersect(new Circle(0.5f, 0.5f, 0.5f)), true);
            ut.Equal(rc.Intersect(new Circle(0.5f, 0.5f, 4)), true);
            ut.Equal(rc.Intersect(new Circle(Vector.Abscissa, 4)), true);

            ut.Equal(rc.Intersect(new Circle(-Vector.Abscissa, 1)), true);
            ut.Equal(rc.Intersect(new Circle(-Vector.Abscissa, 0.5f)), false);

            ut.Equal(rc.Intersect(new Circle(3, 3, 2)), false);
            ut.Equal(rc.Intersect(new Circle(3, 2, 2)), false);
            ut.Equal(rc.Intersect(new Circle(3, 1, 2)), true);
            ut.Equal(rc.Intersect(new Circle(2, 2, 2)), true);

            Rect rc2 = new Rect(-10, -10, 20, 20);
            ut.Equal((new Circle(0, -20, 32)).Contains(rc2), true);
            ut.Equal((new Circle(-2, 2, 18)).Contains(rc2), true);

            float R = (float)Math.Sqrt(rc2.Width * rc2.Width + rc2.Height * rc2.Height) / 2.0f;

            ut.Equal(rc2.CanContainsInCircle(R - 0.01f), false);
            ut.Equal(rc2.CanContainsInCircle(R + 0.01f), true);

            ut.Equal((new Circle(0, 0, R + 0.01f)).Contains(rc2), true);

            //fail 3
            ut.Equal((new Circle(-20, 0, 31)).Contains(rc2), false);
            //fail 4
            ut.Equal((new Circle(-4, 4, 17)).Contains(rc2), false);
            //fail 1
            ut.Equal((new Circle(4, 6, 20)).Contains(rc2), false);
            //fail 2
            ut.Equal((new Circle(6, -6, 18)).Contains(rc2), false);
        }

        private static void UT_RectWithPoint(IUTest ut)
        {
            Rect rc = new Rect(1, 1, 1, 1);

            ut.Equal(rc.Contains(1.5f, 1.2f), rc.Contains(new Vector(1.2f, 1.5f)));
            ut.Equal(rc.Contains(Vector.One), true);
            ut.Equal(rc.Contains(2 * Vector.One), true);
            ut.Equal(rc.Contains(Vector.Abscissa), false);
            ut.Equal(rc.Contains(Vector.OrdinateNegative), false);

            ut.Equal(rc.Distance(Vector.One), 0);
            ut.Equal(rc.Distance(rc.Center), 0);
            ut.Equal(rc.Distance(Vector.One + Vector.Abscissa), 0);
            ut.Equal(rc.Distance(Vector.One + Vector.Ordinate), 0);

            ut.Equal(rc.Distance(Vector.Zero), (float)Math.Sqrt(2));
            ut.Equal(rc.Distance(1.5f * Vector.Abscissa - Vector.Ordinate), 2);
        }

        private static void UT_RectWithRect(IUTest ut)
        {
            Rect rc = new Rect(1, 1, 1, 1);

            ut.Equal(rc.Intersect(Rect.UnitSquare), true);
            ut.Equal(rc.Contains(Rect.UnitSquare), false);

            ut.Equal(rc.Intersect(rc), true);
            ut.Equal(rc.Contains(rc), true);

            ut.Equal(rc.Intersect(Rect.Translate(Rect.Scale(Rect.UnitSquare, 2f), new Vector(0.5f, 0.5f))), true);
            ut.Equal(rc.Intersect(Rect.Translate(Rect.UnitSquare, new Vector(2f, -3f))), false);
        }

        private static void UT_CircleWithPoint(IUTest ut)
        {
            Circle c = new Circle(Vector.Zero, 2);

            ut.Equal(c.Distance(Vector.One), 0);
            ut.Equal(c.Distance(-2 *Vector.Abscissa), 0);
            ut.Equal(c.Distance(3 * Vector.Ordinate), 1);

            ut.Equal(c.Contains(Vector.Abscissa), true);
            ut.Equal(c.Contains(Vector.Ordinate), true);
            ut.Equal(c.Contains(2 * Vector.AbscissaNegative), true);
            ut.Equal(c.Contains(2 * Vector.Ordinate), true);

            ut.Equal(c.Contains(Vector.One), true);
            ut.Equal(c.Contains(2 * Vector.One), false);
        }

        private static void UT_CircleWithCircle(IUTest ut)
        {
            Circle c = new Circle(Vector.Zero, 2);

            ut.Equal(c.Contains(new Circle(Vector.Zero, 1)), true);
            ut.Equal(c.Contains(new Circle(Vector.Zero, 2)), true);
            ut.Equal(c.Contains(new Circle(Vector.Abscissa, 1)), true);
            ut.Equal(c.Contains(new Circle(Vector.Abscissa, 1.1f)), false);
            ut.Equal(c.Contains(new Circle(100 * Vector.One, 10f)), false);

            ut.Equal(c.Intersect(new Circle(Vector.Zero, 2)), true);
            ut.Equal(c.Intersect(new Circle(Vector.Zero, 1)), true);

            ut.Equal(c.Intersect(new Circle(Vector.One, 1)), true);
            ut.Equal(c.Intersect(new Circle(Vector.One, 100)), true);

            ut.Equal(c.Intersect(new Circle(2 * Vector.One, 1)), true);
            ut.Equal(c.Intersect(new Circle(3 * Vector.Abscissa, 1)), true);

            ut.Equal(c.Intersect(new Circle(3 * Vector.Abscissa + Vector.Ordinate, 1)), false);
        }

        private static void UT_LineWithCircle(IUTest ut)
        {
            Line l = new Line(Vector.Zero, 2 * Vector.Abscissa);

            Circle c0 = new Circle(l.Start - Vector.Abscissa, 0.5f);

            Circle c1 = new Circle(l.Start - Vector.Abscissa, 1);
            Circle c2 = new Circle(l.Start, 1);

            Circle c3 = new Circle(l.Middle, 1);

            Circle c4 = new Circle(l.End, 1);
            Circle c5 = new Circle(l.End + Vector.Abscissa, 1);

            Circle c6 = new Circle(l.End + Vector.Abscissa, 0.5f);

            Circle c7 = new Circle(l.Middle + Vector.Ordinate, 1);
            Circle c8 = new Circle(l.Middle - Vector.Ordinate, 1);

            Circle c9 = new Circle(l.Middle + 2 * Vector.Ordinate, 1);
            Circle c10 = new Circle(l.Middle - 2 * Vector.Ordinate, 1);

            Circle c11 = new Circle(l.Middle + 0.5f * Vector.Ordinate, 1);

            Circle c12 = new Circle(l.Start + Vector.Ordinate, 1);
            Circle c13 = new Circle(l.End - Vector.Ordinate, 1);

            ut.Equal(l.PlacedAtLeftOrLie(c0), true);
            ut.Equal(l.PlacedAtLeftOrLie(c3), true);
            ut.Equal(l.PlacedAtLeftOrLie(c6), true);

            ut.Equal(l.PlacedAtLeftOrLie(c7), true);
            ut.Equal(l.PlacedAtLeftOrLie(c8), true);

            ut.Equal(l.PlacedAtRight(c9), true);
            ut.Equal(l.PlacedAtLeftOrLie(c10), true);

            ut.Equal(l.PlacedAtRight(c11), false);

            ut.Equal(l.Intersect(c8), true);
            ut.Equal(l.Intersect(c9), false);
            ut.Equal(l.Intersect(c11), true);

            ut.Equal(l.IntersectSegment(c0), false);
            ut.Equal(l.IntersectSegment(c1), true);
            ut.Equal(l.IntersectSegment(c2), true);
            ut.Equal(l.IntersectSegment(c3), true);
            ut.Equal(l.IntersectSegment(c4), true);
            ut.Equal(l.IntersectSegment(c5), true);
            ut.Equal(l.IntersectSegment(c6), false);

            ut.Equal(l.IntersectSegment(c7), true);
            ut.Equal(l.IntersectSegment(c8), true);
            ut.Equal(l.IntersectSegment(c9), false);
            ut.Equal(l.IntersectSegment(c10), false);
            ut.Equal(l.IntersectSegment(c11), true);

            ut.Equal(l.IntersectSegment(c12), true);
            ut.Equal(l.IntersectSegment(c13), true);
        }

        private static void UT_LineWithPont(IUTest ut)
        {
            Line l = new Line(Vector.Zero, 2 * Vector.Abscissa);

            ut.Equal(l.Distance(Vector.Zero), 0);
            ut.Equal(l.Distance(Vector.Abscissa), 0);
            ut.Equal(l.Distance(2 * Vector.Abscissa), 0);
            ut.Equal(l.Distance(10 * Vector.One), -10);

            Vector up = Vector.Abscissa + Vector.Ordinate;
            Vector lie = Vector.Abscissa;
            Vector down = Vector.Abscissa + Vector.OrdinateNegative;

            ut.Equal(l.Distance(up), -1);
            ut.Equal(l.Distance(lie), 0);
            ut.Equal(l.Distance(down), 1);

            ut.Equal(l.PlacedAtRight(up), true);
            ut.Equal(l.PlacedAtLeftOrLie(up), false);

            ut.Equal(l.PlacedAtRight(lie), false);
            ut.Equal(l.PlacedAtLeftOrLie(lie), true);

            ut.Equal(l.PlacedAtRight(down), false);
            ut.Equal(l.PlacedAtLeftOrLie(down), true);

            ut.Equal(l.PlacedAtRight(lie + new Vector(0, 0.01f)), true);
            ut.Equal(l.PlacedAtRight(lie - new Vector(0, 0.01f)), false);
        }

        private static void UT_SircleSector(UT.IUTest ut)
        {
            UT_SircleSectorLessThan180(ut);
            UT_SircleSectorGreatherThan180(ut);
            UT_SircleSectorWithCircle(ut);
        }

        private static void UT_SircleSectorWithCircle(IUTest ut)
        {
            CircleSector ray = new CircleSector(Vector.Zero, 2, Vector.Abscissa, 0);

            ut.Assert(ray.Intersect(new Circle(Vector.Zero, 1)));
            ut.Assert(ray.Intersect(new Circle(Vector.AbscissaNegative, 1)));
            ut.Assert(ray.Intersect(new Circle(Vector.Zero + 3 * Vector.Abscissa, 1)));
            ut.Assert(ray.Intersect(new Circle(new Vector(1.5f, 0.5f), 1)));
            ut.Assert(ray.Intersect(new Circle(Vector.One, 1)));

            CircleSector sec = new CircleSector(Vector.Zero, 2, Vector.Abscissa, Rotation.ToRadians(162));

            ut.Assert(sec.Intersect(new Circle(Vector.Zero, 1)));
            ut.Assert(sec.Intersect(new Circle(Vector.AbscissaNegative, 2)));
            ut.Assert(sec.Intersect(new Circle(Vector.AbscissaNegative, 1)));
            ut.Assert(sec.Intersect(new Circle(Vector.Zero + 3 * Vector.Abscissa, 1)));
            ut.Assert(sec.Intersect(new Circle(new Vector(1.5f, 0.5f), 1)));
            ut.Assert(sec.Intersect(new Circle(Vector.One, 1)));
        }

        private static void UT_SircleSectorLessThan180(IUTest ut)
        {
            CircleSector circleSectorZeroPosOneRadiusAbscissaDir162Angle = new CircleSector(Vector.Zero, 1, Vector.Abscissa, Rotation.ToRadians(162));

            ut.Assert(circleSectorZeroPosOneRadiusAbscissaDir162Angle.Contains(Vector.Zero));
            ut.Assert(circleSectorZeroPosOneRadiusAbscissaDir162Angle.Contains(Vector.Abscissa / 2));
            ut.Assert(circleSectorZeroPosOneRadiusAbscissaDir162Angle.Contains(Vector.Abscissa));

            ut.Assert(!circleSectorZeroPosOneRadiusAbscissaDir162Angle.Contains(Vector.Ordinate));
            ut.Assert(!circleSectorZeroPosOneRadiusAbscissaDir162Angle.Contains(-Vector.Ordinate));
            ut.Assert(!circleSectorZeroPosOneRadiusAbscissaDir162Angle.Contains(-Vector.Abscissa / 2));
            ut.Assert(!circleSectorZeroPosOneRadiusAbscissaDir162Angle.Contains(Vector.Abscissa + C.EPS * Vector.Abscissa));
        }

        private static void UT_SircleSectorGreatherThan180(IUTest ut)
        {
            CircleSector circleSectorZeroPosOneRadiusAbscissaDir342Angle = new CircleSector(Vector.Zero, 1, Vector.Abscissa, Rotation.ToRadians(342));

            ut.Assert(circleSectorZeroPosOneRadiusAbscissaDir342Angle.Contains(Vector.Zero));
            ut.Assert(circleSectorZeroPosOneRadiusAbscissaDir342Angle.Contains(Vector.Abscissa / 2));
            ut.Assert(circleSectorZeroPosOneRadiusAbscissaDir342Angle.Contains(Vector.Abscissa));
            ut.Assert(circleSectorZeroPosOneRadiusAbscissaDir342Angle.Contains(Vector.Ordinate));
            ut.Assert(circleSectorZeroPosOneRadiusAbscissaDir342Angle.Contains(-Vector.Ordinate));

            ut.Assert(!circleSectorZeroPosOneRadiusAbscissaDir342Angle.Contains(-Vector.Abscissa / 2));

            ut.Assert(!circleSectorZeroPosOneRadiusAbscissaDir342Angle.Contains(Vector.Abscissa + C.EPS * Vector.Abscissa));
            ut.Assert(!circleSectorZeroPosOneRadiusAbscissaDir342Angle.Contains(Vector.Ordinate + C.EPS * Vector.Ordinate));
            ut.Assert(!circleSectorZeroPosOneRadiusAbscissaDir342Angle.Contains(-Vector.Ordinate - C.EPS * Vector.Ordinate));
        }


        [UT.UT("Geom2d.Rotation")]
        private static void UT_Rotation(UT.IUTest ut)
        {
            ut.Equal((Rotation.Identity.Direction - Vector.Abscissa).Magnitude(), 0);
            ut.Equal((new Rotation(Rotation.PiHalf).Direction - Vector.Ordinate).Magnitude(), 0);
            ut.Equal((new Rotation(-Rotation.PiHalf).Direction - Vector.OrdinateNegative).Magnitude(), 0);

            ut.Equal(new Rotation(1 + Rotation.Pi2).Angle, new Rotation(1).Angle);

            ut.Equal(Rotation.Lerp(new Rotation(2), new Rotation(1), 0).Angle, 2.0f);
            ut.Equal(Rotation.Lerp(new Rotation(1), new Rotation(-1), 0.5f).Angle, 0.0f);

            ut.Assert(Rotation.Lerp(new Rotation(2), new Rotation(-2), 0.01f).Angle > 2.0f);
            ut.Assert(Rotation.Lerp(new Rotation(2), new Rotation(-2), 0.99f).Angle < -2.0f);

            for (int i = 0; i < 1000; ++i)
            {
                Rotation r = new Rotation(-10 + i * 0.001f * 20.0f);
                for (int j = 0; j < 1000; ++j)
                {
                    Rotation origin = new Rotation(-10 + j * 0.001f * 20.0f);

                    Rotation diff = r - origin;

                    Rotation rr = diff + origin;

                    ut.Equal(r.Angle, rr.Angle);
                }
            }

#if UNITY_5
            {
                const int Len = 500;
                float[] angles = new float[Len * 2];
                angles[0] = 0;
                angles[1] = (float)Math.PI / 2;
                angles[2] = (float)Math.PI;
                angles[3] = (float)Math.PI * 2;
                for (int i = 4; i < Len; ++i)
                    angles[i] = UnityEngine.Random.Range(0, 1) * 10;
                for (int i = 0; i < Len; ++i)
                    angles[i + Len] = -angles[i];

                for (int i = 0; i < Len * 2; ++i)
                {
                    Rotation rot = new Rotation(angles[i]);
                    var q1 = UnityEngine.Quaternion.LookRotation(rot.Direction.ToVector3());
                    var q2 = rot.ToQuaternion();
                    var res = UnityEngine.Quaternion.Angle(q1, q2);
                    ut.Assert(res < 0.1f);
                }
            }

            //{
            //    Rotation[] angles = new Rotation[1000000];
            //    for (int i = 0; i < 1000000; ++i)
            //    {
            //        angles[i] = new Rotation(Pi * 2 / 1000000.0f);
            //    }

            //    System.Diagnostics.Stopwatch sw1 = new System.Diagnostics.Stopwatch();
            //    System.Diagnostics.Stopwatch sw2 = new System.Diagnostics.Stopwatch();

            //    sw1.Start();
            //    for (int i = 0; i < 1000000; ++i)
            //    {
            //        UnityEngine.Quaternion.LookRotation(angles[i].Direction);
            //    }
            //    sw1.Stop();

            //    sw2.Start();
            //    for (int i = 0; i < 1000000; ++i)
            //    {
            //        angles[i].ToQuaternion();
            //    }
            //    sw2.Stop();

            //    Log.i("{0} {1}", sw1.ElapsedMilliseconds, sw2.ElapsedMilliseconds);
            //}
#endif
        }

    }
}
