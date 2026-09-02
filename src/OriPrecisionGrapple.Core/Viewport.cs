namespace OriPrecisionGrapple.Core;

public readonly struct Viewport
{
    public Viewport(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public bool IsValid => Width > 0 && Height > 0;
}
