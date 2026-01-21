namespace ConsoleUi.Core.Primitives;

public readonly record struct Thickness (float Left, float Top, float Right, float Bottom)
{
    public static Thickness All(float value) => new(value, value, value, value);

    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;
}
