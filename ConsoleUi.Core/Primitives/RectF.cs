namespace ConsoleUi.Core.Primitives;

public readonly record struct RectF(Vec2 Possition, Vec2 Size)
{
    public float Left => Possition.X;
    public float Top => Possition.Y;
    public float Right => Possition.X + Size.X;
    public float Bottom => Possition.Y + Size.Y;

    public RectF Deflate(Thickness t)
        => new(
            new Vec2(Possition.X + t.Left, Possition.Y + t.Top),
            new Vec2(Size.X - t.Horizontal, Size.Y - t.Vertical)
        );
}
