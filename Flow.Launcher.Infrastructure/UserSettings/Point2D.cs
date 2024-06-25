using System;

namespace Flow.Launcher.Infrastructure.UserSettings;

public record struct Point2D(double X, double Y)
{
    public static implicit operator Point2D((double X, double Y) point)
    {
        return new Point2D(point.X, point.Y);
    }

    public static Point2D operator +(Point2D point1, Point2D point2)
    {
        return new Point2D(point1.X + point2.X, point1.Y + point2.Y);
    }

    public static Point2D operator -(Point2D point1, Point2D point2)
    {
        return new Point2D(point1.X - point2.X, point1.Y - point2.Y);
    }

    public static Point2D operator *(Point2D point, double scalar)
    {
        return new Point2D(point.X * scalar, point.Y * scalar);
    }

    public static Point2D operator /(Point2D point, double scalar)
    {
        return new Point2D(point.X / scalar, point.Y / scalar);
    }

    public static Point2D operator /(Point2D point1, Point2D point2)
    {
        return new Point2D(point1.X / point2.X, point1.Y / point2.Y);
    }
    
    public static Point2D operator *(Point2D point1, Point2D point2)
    {
        return new Point2D(point1.X * point2.X, point1.Y * point2.Y);
    }
    
    public Point2D Clamp(Point2D min, Point2D max)
    {
        return new Point2D(Math.Clamp(X, min.X, max.X), Math.Clamp(Y, min.Y, max.Y));
    }
}
