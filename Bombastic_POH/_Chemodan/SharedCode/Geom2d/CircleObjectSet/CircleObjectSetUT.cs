using System;
using System.Collections.Generic;

namespace Geom2d
{
    public static class CircleObjectSetUT
    {
        private class ObjectImpl
        {
            public Circle Geometry;
            public CircleObject<ObjectImpl> Handler;

            public ObjectImpl(float x, float y, float r)
            {
                Geometry = new Circle(x, y, r);
            }
        }

        [UT.UT("CircleObjectSet")]
        private static void UT(UT.IUTest test)
        {
            TestSet(test, new CircleObjectTrivialSet<ObjectImpl>(), 1000);
            TestSet(test, new CircleObjectSortedSet<ObjectImpl>(), 20000);
        }

        private static void TestSet(UT.IUTest test, BaseCircleObjectSet<ObjectImpl> set, int repetition)
        {
            Random rnd = new Random();

            float range = 10.0f;
            float size = 1.0f;

            List<ObjectImpl> objects = new List<ObjectImpl>();
            HashSet<ObjectImpl> selection = new HashSet<ObjectImpl>();
            for (int i = 0; i < repetition; ++i)
            {
                if (rnd.NextDouble() < 0.1 * (1 - objects.Count / 100.0))
                {
                    ObjectImpl obj = new ObjectImpl((float)(rnd.NextDouble() * range), (float)(rnd.NextDouble() * range), (float)(rnd.NextDouble() * size));
                    obj.Handler = set.CreateObject(obj.Geometry.r, obj);
                    obj.Handler.Position = obj.Geometry.Position;
                    objects.Add(obj);
                }
                if (rnd.NextDouble() > 1.0 - 0.1 * (objects.Count / 100.0))
                {
                    if (objects.Count > 0)
                    {
                        int id = rnd.Next(objects.Count);
                        var obj = objects[id];
                        objects.RemoveAt(id);
                        set.RemoveObject(obj.Handler);
                    }
                }

                if (rnd.NextDouble() < 0.5)
                {
                    if (objects.Count > 0)
                    {
                        int id = rnd.Next(objects.Count);
                        objects[id].Geometry.Position = new Vector((float)(rnd.NextDouble() * range), (float)(rnd.NextDouble() * range));
                        objects[id].Handler.Position = objects[id].Geometry.Position;
                    }
                }

                Circle c = new Circle((float)(rnd.NextDouble() * range), (float)(rnd.NextDouble() * range), (float)(rnd.NextDouble() * size * 3));

                for (int j = 0; j < objects.Count; ++j)
                {
                    if (objects[j].Geometry.Intersect(c))
                    {
                        selection.Add(objects[j]);
                    }
                }

                foreach (var obj in set.Select(c))
                {
                    test.Assert(selection.Contains(obj), "Found wrong circle");
                    selection.Remove(obj);
                }
                //if (selection.Count != 0)
                //{
                //    foreach (var obj in set.SelectIn(c))
                //    {
                //    }
                //}
                test.Assert(selection.Count == 0, "Not all circles were found");
            }
        }
    }
}
