namespace OriPrecisionBash.Core;

public static class PrecisionHitTest
{
    public const double DefaultRadiusPixels = 24.0;
    public const int DefaultReferenceHeight = 1080;

    public static bool IsHit(
        ScreenPoint cursor,
        ScreenPoint target,
        Viewport viewport,
        double radiusPixels = DefaultRadiusPixels,
        int referenceHeight = DefaultReferenceHeight)
    {
        if (!viewport.IsValid || radiusPixels < 0 || referenceHeight <= 0 || target.Depth <= 0)
        {
            return false;
        }

        if (!IsFinite(cursor) || !IsFinite(target))
        {
            return false;
        }

        if (cursor.X < 0 || cursor.X > viewport.Width || cursor.Y < 0 || cursor.Y > viewport.Height)
        {
            return false;
        }

        var scale = (double)viewport.Height / referenceHeight;
        var effectiveRadius = radiusPixels * scale;
        var deltaX = cursor.X - target.X;
        var deltaY = cursor.Y - target.Y;

        return (deltaX * deltaX) + (deltaY * deltaY) <= effectiveRadius * effectiveRadius;
    }

    private static bool IsFinite(ScreenPoint point) =>
        double.IsFinite(point.X) &&
        double.IsFinite(point.Y) &&
        double.IsFinite(point.Depth);
}
