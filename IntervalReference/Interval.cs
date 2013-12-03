using System;
using System.Diagnostics;

/// <summary>
/// A contiguous range of integers.
/// May be degenerate, with an offset but no length.
/// </summary>
[DebuggerDisplay("{ToString()}")]
public struct Interval {
    public readonly int Offset;
    public readonly int Length;
    public Interval(int offset, int length) {
        if (length < 0) throw new ArgumentOutOfRangeException("length");
        Offset = offset;
        Length = length;
    }
    public bool Overlaps(Interval other) {
        return this.Offset < other.Offset + other.Length 
            && other.Offset < this.Offset + this.Length;
    }
    public override string ToString() {
        if (Length == 0) return string.Format("Empty interval at {0}", Offset);
        return string.Format("[{0}, {1})", Offset, Offset + Length);
    }
}
