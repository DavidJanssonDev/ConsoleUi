using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ConsoleUi.Rendering.Backends.Windows32;

[StructLayout(LayoutKind.Explicit)]
internal struct Win32CharInfo
{
    [FieldOffset(0)] public char Char;
    [FieldOffset(2)] public ushort Attr;
}
