using Geom2d;

namespace Shared.Battle.Collisions
{
    public sealed class PolygonColliderDef : ColliderDef
    {
        public override ColliderDefType Type()
        {
            return ColliderDefType.Polygon;
        }

        const string FN_POLYGONS = "Polygons";

        public struct Polygon
        {
            const string FN_POINTS = "Points";
            const string FN_DECOMPOSITION = "Decomposition";

            public Vector[] Points;

            public Vector[][] Decomposition;

            public void Serialize(StorageFolder to)
            {
                if (Points != null && Points.Length > 0)
                {
                    var pointsFolder = new StorageFolder(FN_POINTS);
                    for (int i = 0; i < Points.Length; i++)
                    {
                        pointsFolder.AddItem(SerializationUtils.SerializeVector(null, Points[i]));
                    }
                    to.AddItem(pointsFolder);
                }
                if (Decomposition != null && Decomposition.Length > 0)
                {
                    var decompositionFolder = new StorageFolder(FN_DECOMPOSITION);
                    for (int i = 0; i < Decomposition.Length; i++)
                    {
                        var polygon = Decomposition[i];
                        if (polygon != null && polygon.Length > 0)
                        {
                            var polyFolder = new StorageFolder();
                            for(int j = 0; j < polygon.Length; j++)
                            {
                                polyFolder.AddItem(SerializationUtils.SerializeVector(null, polygon[j]));
                            }
                            decompositionFolder.AddItem(polyFolder);
                        }
                    }

                    to.AddItem(decompositionFolder);
                }
            }

            public void Deserialize(StorageFolder from)
            {
                var pointsFolder = from.GetFolder(FN_POINTS);
                if (pointsFolder != null)
                {
                    Points = new Vector[pointsFolder.Count];
                    for (int j = 0; j < pointsFolder.Count; j++)
                    {
                        Points[j] = SerializationUtils.DeserializeVector(pointsFolder.GetItem(j) as StorageFolder);
                    }
                }
                var decompositionFolder = from.GetFolder(FN_DECOMPOSITION);
                if (decompositionFolder != null)
                {
                    Decomposition = new Vector[decompositionFolder.Count][];
                    for (int j = 0; j < decompositionFolder.Count; j++)
                    {
                        StorageFolder polyFolder = decompositionFolder.GetItem(j) as StorageFolder;

                        Decomposition[j] = new Vector[polyFolder.Count];
                        for (int i = 0; i < polyFolder.Count; i++)
                        {
                            Decomposition[j][i] = SerializationUtils.DeserializeVector(polyFolder.GetItem(i) as StorageFolder);
                        }
                    }
                } else
                {
                    Decomposition = new Vector[1][];
                    Decomposition[0] = Points;
                }
            }
        }

        public Polygon[] Polygons;

        protected override void OnDeserialize(StorageFolder from)
        {
            var polyFolder = from.GetFolder(FN_POLYGONS);
            if (polyFolder != null)
            {
                Polygons = new Polygon[polyFolder.Count];

                for (int i = 0; i < Polygons.Length; ++i)
                {
                    var pathFolder = polyFolder.GetItem(i) as StorageFolder;
                    Polygons[i] = new Polygon();
                    Polygons[i].Deserialize(pathFolder);
                }
            }
        }

        protected override void OnSerialize(StorageFolder to)
        {
            if (Polygons != null && Polygons.Length > 0)
            {
                StorageFolder polyFolder = new StorageFolder(FN_POLYGONS);

                for (int i = 0; i < Polygons.Length; i++)
                {
                    var pathFolder = new StorageFolder(i.ToString());
                    Polygons[i].Serialize(pathFolder);
                    polyFolder.AddItem(pathFolder);
                }
                to.AddItem(polyFolder);
            }
        }
    }
}
