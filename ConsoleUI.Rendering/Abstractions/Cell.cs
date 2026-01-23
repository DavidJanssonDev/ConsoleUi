namespace ConsoleUi.Rendering.Abstractions;


public readonly record struct Cell(
    char Ch,
    ConsoleColor Fg,
    ConsoleColor Bg
);
