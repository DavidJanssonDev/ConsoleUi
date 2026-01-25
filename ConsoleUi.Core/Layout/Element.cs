using ConsoleUi.Core.Primitives;

namespace ConsoleUi.Core.Layout;

public sealed class Element
{
    public Element(string id)
    {
        Id = id;
    }

    public string Id { get; }

    // "CSS-like" properties
    public Vec2 Position { get; set; } = new(0, 0);
    public Vec2 Size { get; set; } = new(10, 5);

    public Thickness Margin { get; set; } = Thickness.All(0);
    public Thickness Padding { get; set; } = Thickness.All(0);

    public List<Element> Children { get; } = [];

    // Computed Layout resluts
    public RectF BorderBox { get; internal set; }

    public RectF ContentBox { get; internal set; }

}
