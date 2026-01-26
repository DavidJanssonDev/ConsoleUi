using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleUI.Core.Rendering;

public readonly record struct CellPoint(int X, int Y);
public sealed class ConsoleTransform
{
    private readonly float _sx;
    private readonly float _sy;


    public ConsoleTransform(float sx, float sy)
    {
        _sx = sx;
        _sy = sy;
    }

    public CellPoint ToCell(Vec2 logical) 
        => new(
            (int) System.MathF.Round(logical.X * _sx),
            (int) System.MathF.Round(logical.Y * _sy)
        );
    public (int W, int H) ToCellSize(Vec2 logalSize)
        => (
            W: System.Math.Max(0, (int)System.MathF.Round(logalSize.X * _sx)),
            H: System.Math.Max(0, (int)System.MathF.Round(logalSize.Y * _sy))
       );
}
