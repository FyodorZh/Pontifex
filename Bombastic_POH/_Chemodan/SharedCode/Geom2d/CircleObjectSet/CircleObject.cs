namespace Geom2d
{
    public abstract class CircleObject<TData>
    {
        public abstract void Destroy();

        public abstract float Size { get; set; }

        public abstract TData UserData { get; }

        public abstract Vector Position { get; set; }
    }
}