namespace PolygonEditor.Core.Geometry;

/// <summary>Immutable 2D vector / point in screen coordinates (y grows downwards).</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;

    public Vec2 Normalized()
    {
        double length = Length;
        return length < 1e-12 ? Zero : new Vec2(X / length, Y / length);
    }

    /// <summary>Vector rotated by +90 degrees: (-y, x).</summary>
    public Vec2 Perp() => new(-Y, X);

    /// <summary>Rotation with precomputed cos/sin (used by incremental arc generation).</summary>
    public Vec2 Rotate(double cos, double sin) => new(X * cos - Y * sin, X * sin + Y * cos);

    public static Vec2 FromAngle(double angle) => new(Math.Cos(angle), Math.Sin(angle));
    public static double Angle(Vec2 v) => Math.Atan2(v.Y, v.X);

    public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
    public static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;
    public static double Distance(Vec2 a, Vec2 b) => (b - a).Length;
    public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => a + (b - a) * t;

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 v) => new(-v.X, -v.Y);
    public static Vec2 operator *(Vec2 v, double s) => new(v.X * s, v.Y * s);
    public static Vec2 operator *(double s, Vec2 v) => new(v.X * s, v.Y * s);
    public static Vec2 operator /(Vec2 v, double s) => new(v.X / s, v.Y / s);

    public override string ToString() => $"({X:0.##}, {Y:0.##})";
}
