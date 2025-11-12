using System;
using System.Collections.Generic;

namespace Geom2d
{
    public struct ConvexPoly
    {
        Geom2d.Vector[] _points;

        Geom2d.Rect _bounds;

        public Geom2d.Rect Bounds
        {
            get { return _bounds; }
        }

        public ConvexPoly(Geom2d.Vector[] points)
        {
            List<Geom2d.Vector> pointsList = new List<Vector>(points);
            Normalize(pointsList);
            _points = pointsList.ToArray();

            if (_points.Length > 0)
            {
                _bounds = new Geom2d.Rect(_points[0].x, _points[0].y, 0, 0);

                if (_points.Length > 1)
                {
                    for (int i = 1; i < _points.Length; i++)
                    {
                        _bounds.Union(_points[i]);
                    }
                }
            }
            else
            {
                _bounds = Geom2d.Rect.Empty;
            }
        }

        static void Normalize(List<Geom2d.Vector> pointsList)
        {

            for (int i = 0; i < pointsList.Count; ++i)
            {
                Geom2d.Vector pL = pointsList[i];
                Geom2d.Vector pC = pointsList[(i + 1) % pointsList.Count];
                Geom2d.Vector pR = pointsList[(i + 2) % pointsList.Count];

                if (Math.Abs(Geom2d.Vector.Cross(pC - pL, pR - pL)) < 1e-5f)
                {
                    int id = (i + 1) % pointsList.Count;
                    pointsList.RemoveAt(id);
                    --i;
                }
            }
        }

        public bool IsIntersect(Geom2d.Vector pos)
        {
            if (_points.Length > 0)
            {
                if (!Bounds.Contains(pos))
                {
                    return false;
                }

                Geom2d.Vector p1 = _points[_points.Length - 1] - pos;
                Geom2d.Vector p2 = _points[0] - pos;

                bool flag = Geom2d.Vector.Cross(p1, p2) < 0;

                for (int i = 1; i < _points.Length; ++i)
                {
                    p1 = p2;
                    p2 = _points[i] - pos;

                    if (flag != (Geom2d.Vector.Cross(p1, p2) < 0))
                    {
                        return false;
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}