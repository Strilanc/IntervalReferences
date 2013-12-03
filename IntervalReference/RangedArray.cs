using System;

/// <summary>
/// An array of ints that supports slicing (in logarithmic time) without the potential for memory leaks due to slices keeping the parent alive.
/// </summary>
public sealed class RangedArray {
    private readonly Interval _memInterval;
    private readonly NestingDepthTreeNode _refNode;
    private bool _disposed;
    private readonly TheFanciestMemory _mem;

    ///<summary>Reads/writes within the array.</summary>
    public int this[int index] {
        get {
            if (_disposed) throw new ObjectDisposedException("this");
            if (index < 0 || index >= _memInterval.Length) throw new ArgumentOutOfRangeException("index");
            return _mem[_memInterval.Offset + index];
        }
        set {
            if (_disposed) throw new ObjectDisposedException("this");
            if (index < 0 || index >= _memInterval.Length) throw new ArgumentOutOfRangeException("index");
            _mem[_memInterval.Offset + index] = value;
        }
    }

    ///<summary>Allocates an array with the given number of ints, using the given mock memory to store its data.</summary>
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

    ///<summary>Returns an array backed by the interior of this array.</summary>
    public RangedArray Slice(Interval interval) {
        return new RangedArray(this, interval, _mem);
    }
    private RangedArray(RangedArray other, Interval interval, TheFanciestMemory mem) {
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

    ///<summary>Collects this array, free-ing any memory that is not referenced by other slices.</summary>
    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);

        // find the tree we're in
        var root = NestingDepthTreeNode.RootOf(_refNode);
        var interval = NestingDepthTreeNode.GetInterval(root);
        if (NestingDepthTreeNode.GetTotalAdjust(root) != 0) {
            throw new InvalidOperationException("Invariant violated: total adjust is not zero");
        }

        // cancel our effect on the nesting depth
        root = NestingDepthTreeNode.Include(root, _memInterval.Offset + _memInterval.Length, +1, 0).NewRoot;
        if (NestingDepthTreeNode.GetTotalAdjust(root) != +1) {
            throw new InvalidOperationException("Invariant violated: total adjust is not (temporarily) one");
        }
        root = NestingDepthTreeNode.Include(root, _memInterval.Offset, -1, 0).NewRoot;
        if (NestingDepthTreeNode.GetTotalAdjust(root) != 0) {
            throw new InvalidOperationException("Invariant violated: total adjust is not zero");
        }

        // find holes that need to be free'd
        var holes = NestingDepthTreeNode.FindHolesIn(interval, root);

        // decrement our reference count on nodes in the tree, so they can be imploded
        root = NestingDepthTreeNode.Include(root, _memInterval.Offset + _memInterval.Length, 0, -1).NewRoot;
        if (NestingDepthTreeNode.GetTotalAdjust(root) != 0) {
            throw new InvalidOperationException("Invariant violated: total adjust is not zero");
        }
        root = NestingDepthTreeNode.Include(root, _memInterval.Offset, 0, -1).NewRoot;
        if (NestingDepthTreeNode.GetTotalAdjust(root) != 0) {
            throw new InvalidOperationException("Invariant violated: total adjust is not zero");
        }

        // try to partition the tree, so later arrays have less to search
        NestingDepthTreeNode.PartitionAroundHoles(root);
        if (NestingDepthTreeNode.GetTotalAdjust(root) != 0) {
            throw new InvalidOperationException("Invariant violated: total adjust is not zero");
        }

        // free the memory
        foreach (var hole in holes) {
            _mem.Free(hole);
        }
    }
    ~RangedArray() {
        this.Dispose();
    }
}
