namespace Geom2d
{
    public interface IShape
    {
        Vector Center { get; }
        float CircumcircleRadius { get; }
        Circle CircumCircle(Vector selfPosition);

        float Distance(Vector selfPosition, Vector position);

        bool Intersect(Vector selfPosition, Circle c);
        bool ContainsIn(Vector selfPosition, Circle c);

        bool Intersect(Vector selfPosition, CircleSector cs);

        bool Intersect(Vector selfPosition, Quad q);
        bool ContainsIn(Vector selfPosition, Quad q);
    }
}
