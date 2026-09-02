namespace OriPrecisionGrapple.Core;

public readonly struct ScreenPoint
{
    public ScreenPoint(double x, double y, double depth = 1.0)
    {
        X = x;
        Y = y;
        Depth = depth;
    }

    public double X { get; }

    public double Y { get; }

    public double Depth { get; }
}
