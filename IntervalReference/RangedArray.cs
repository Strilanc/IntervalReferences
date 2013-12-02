using System;

public sealed class RangedArray {
    private readonly Interval _memInterval;
    public readonly NestingDepthTreeNode _refNode;
    private bool _disposed;
    private readonly TheFanciestMemory _mem;

    public int this[int index] {
        get {
            if (_disposed) throw new ObjectDisposedException("this");
            return _mem[_memInterval.Offset + index];
        }
        set {
            if (_disposed) throw new ObjectDisposedException("this");
            _mem[_memInterval.Offset + index] = value;
        }
    }

    public RangedArray(int numberOfItems, TheFanciestMemory mem) {
        if (mem == null) throw new ArgumentNullException("mem");
        _mem = mem;
        if (numberOfItems == 0) {
            _refNode = null;
            return;
        }

        _memInterval = mem.Malloc(numberOfItems);
        var root = NestingDepthTreeNode.Include(null, _memInterval.Offset, +1, +1).NewRoot;
        _refNode = NestingDepthTreeNode.Include(root, _memInterval.Offset + _memInterval.Length, -1, +1).AdjustedNode;
    }
    public RangedArray(RangedArray other, Interval interval, TheFanciestMemory mem) {
        if (mem == null) throw new ArgumentNullException("mem");
        _mem = mem;
        if (other._disposed) throw new ObjectDisposedException("other");
        if (interval.Offset < 0) throw new ArgumentOutOfRangeException();
        if (interval.Length < 0) throw new ArgumentOutOfRangeException();
        if (interval.Offset + interval.Length > other._memInterval.Length) throw new ArgumentOutOfRangeException();
        if (interval.Length == 0) {
            _refNode = null;
            return;
        }

        _memInterval = new Interval(other._memInterval.Offset + interval.Offset, interval.Length);
        var root = NestingDepthTreeNode.RootOf(other._refNode);
        root = NestingDepthTreeNode.Include(root, _memInterval.Offset, +1, +1).NewRoot;
        _refNode = NestingDepthTreeNode.Include(root, _memInterval.Offset + _memInterval.Length, -1, +1).AdjustedNode;
    }
    public RangedArray Slice(Interval interval) {
        return new RangedArray(this, interval, _mem);
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);

        var root = NestingDepthTreeNode.RootOf(_refNode);
        var interval = NestingDepthTreeNode.GetInterval(root);
        if (NestingDepthTreeNode.GetTotalAdjust(root) != 0) throw new Exception();
        root = NestingDepthTreeNode.Include(root, _memInterval.Offset + _memInterval.Length, +1, 0).NewRoot;
        if (NestingDepthTreeNode.GetTotalAdjust(root) != +1) {
            throw new InvalidOperationException("Invariant violated: total adjust is not (temporarily) one");
        }
        root = NestingDepthTreeNode.Include(root, _memInterval.Offset, -1, 0).NewRoot;
        if (NestingDepthTreeNode.GetTotalAdjust(root) != 0) {
            throw new InvalidOperationException("Invariant violated: total adjust is not zero");
        }

        var r = NestingDepthTreeNode.FindHolesIn(interval, root);

        root = NestingDepthTreeNode.Include(root, _memInterval.Offset + _memInterval.Length, 0, -1).NewRoot;
        if (NestingDepthTreeNode.GetTotalAdjust(root) != 0) {
            throw new InvalidOperationException("Invariant violated: total adjust is not zero");
        }
        root = NestingDepthTreeNode.Include(root, _memInterval.Offset, 0, -1).NewRoot;
        if (NestingDepthTreeNode.GetTotalAdjust(root) != 0) {
            throw new InvalidOperationException("Invariant violated: total adjust is not zero");
        }
        NestingDepthTreeNode.PartitionAroundHoles(root);
        if (NestingDepthTreeNode.GetTotalAdjust(root) != 0) {
            throw new InvalidOperationException("Invariant violated: total adjust is not zero");
        }

        foreach (var hole in r) {
            _mem.Free(hole);
        }
    }
    ~RangedArray() {
        this.Dispose();
    }
}
