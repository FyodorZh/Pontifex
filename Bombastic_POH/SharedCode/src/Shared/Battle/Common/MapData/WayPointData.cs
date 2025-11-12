using System;
using Geom2d;

namespace Shared.Battle
{
    [Serializable]
    public class WayPointData : MapObjectData
    {
        public WayPointData() { }
        public WayPointData(Vector position)
            : base(position)
        {
        }
    }
}
