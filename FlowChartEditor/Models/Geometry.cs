namespace FlowChartEditor.Models;

public record struct PointF(double X, double Y)
{
    public static PointF Zero => new(0, 0);

    public PointF Offset(double dx, double dy) => new(X + dx, Y + dy);

    public double DistanceTo(PointF other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public record struct RectF(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public PointF Center => new(X + Width / 2, Y + Height / 2);

    public bool Contains(PointF p) =>
        p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public bool IntersectsWith(RectF other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}
