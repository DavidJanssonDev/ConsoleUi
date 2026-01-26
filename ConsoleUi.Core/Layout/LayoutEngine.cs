using ConsoleUI.Core.Primitives;

namespace ConsoleUI.Core.Layout;

public sealed class LayoutEngine
{
    /// <summary>
    /// Simple layout:
    /// - RootElement at origin
    /// - Children are stacked vertically in the parrent's content box
    /// </summary>
    public void Layout(Element root, Vec2 origin)
    {
        root.BorderBox = new RectF(origin + root.Position, root.Size);
        root.ContentBox = root.BorderBox.Deflate(root.Padding);

        LayoutChildrenVertical(root);
    }

    private static void LayoutChildrenVertical(Element parent)
    {
        float cursorY = parent.ContentBox.Top;

        foreach (Element child in parent.Children)
        {
            Vec2 childPos = new(
                parent.ContentBox.Left + child.Margin.Left + child.Position.X,
                cursorY + child.Margin.Top + child.Position.Y

            );

            child.BorderBox = new RectF(childPos, child.Size);
            child.ContentBox = child.BorderBox.Deflate(child.Padding);

            cursorY = child.BorderBox.Bottom + child.Margin.Bottom;

            LayoutChildrenVertical(child);
        }
    }
}
