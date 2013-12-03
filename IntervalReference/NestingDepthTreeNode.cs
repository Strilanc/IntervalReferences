using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The building block of a tree that tracks nesting depth over a range.
/// The null node is considered to be an empty tree.
/// 
/// WARNING: Assumptions about the use case (interval references) are implicitely assumed in some places.
/// For example, the tree may or may not work properly if you don't keep it partitioned around holes before continuing to use it.
/// </summary>
public sealed class NestingDepthTreeNode {
    ///<summary>The place where this node's adjustment takes effect.</summary>
    private readonly int _offset;

    /// <summary>How much the nesting depth changes as you go from _offset-1 to _offset.</summary>
    private int _adjust;
    
    /// <summary>
    /// The number of ranged arrays that point into this node.
    /// An array needs its referenced node to be in the tree, so that it can find the root when deallocating.
    /// Thus nodes with RefCount > 0 can't be removed, even if their Adjust=0.
    /// </summary>
    private int _refCount;

    /// <summary>The root of the subtree containing indexes that are less than this node's offset.</summary>
    private NestingDepthTreeNode _less;
    /// <summary>The root of the subtree containing indexes that are more than this node's offset.</summary>
    private NestingDepthTreeNode _more;
    /// <summary>The only node that has this node as a Less or More child.</summary>
    private NestingDepthTreeNode _parent;

    /// <summary>The total amount the nesting depth changes as you cross the sub tree rooted at this node.</summary>
    private int _subTreeTotalAdjust;
    /// <summary>The lowest that the nesting depth gets as you cross the sub tree rooted at this node, ASSUMING your starting depth is 0.</summary>
    private int _subTreeRelativeMinimum;

    /// <summary>This is a constructor. You knew that already. Comments can be helpful sometimes.</summary>
    private NestingDepthTreeNode(int offset, int adjust, int refAdjust) {
        this._offset = offset;
        this._adjust = adjust;
        this._refCount = refAdjust;
        this.RecomputeAggregates();
    }

    ///<summary>Gets the child in the given direction (negative -> lesser child, positive -> larger child).</summary>
    private NestingDepthTreeNode Child(int childSign) {
        if (childSign < 0) return _less;
        if (childSign > 0) return _more;
        throw new ArgumentException("childSign == 0");
    }
    ///<summary>Sets the child in the given direction (negative -> lesser child, positive -> larger child).</summary>
    private void SetChild(int childSign, NestingDepthTreeNode value) {
        if (childSign < 0) _less = value;
        else if (childSign > 0) _more = value;
        else throw new ArgumentException("childSign == 0");
    }
    ///<summary>+1 if the parent has a larger value, -1 if the parent has a lesser value, and 0 if we're the root</summary>
    private int DirToParent() {
        if (_parent == null) return 0;
        if (_parent._less == this) return +1;
        return -1;
    }
    ///<summary>Travels upwards from the given node until the root of the tree containing it is found.</summary>
    public static NestingDepthTreeNode RootOf(NestingDepthTreeNode node) {
        if (node == null) return null;
        if (node._parent == null) return node;
        return RootOf(node._parent);
    }
    ///<summary>Determines how much the nesting depth changes as you cross the tree rooted at the given node.</summary>
    public static int GetTotalAdjust(NestingDepthTreeNode root) {
        return root == null ? 0 : root._subTreeTotalAdjust;
    }
    /// <summary>Determines the range of indexes spanned by the sub tree rooted at the given node.</summary>
    public static Interval GetInterval(NestingDepthTreeNode root) {
        if (root == null) return default(Interval);
        var min = root;
        var max = root;
        while (min._less != null) min = min._less;
        while (max._more != null) max = max._more;
        return new Interval(min._offset, max._offset - min._offset);
    }
    ///<summary>Returns a result that is larger when the given value is a multiple of a higher power of 2.</summary>
    private static int PowerOf2Ness(int value) {
        // only the lowest set bit affects the result
        // xxxxxxxxx1000000 - 1 == xxxxxxxxx0111111
        // xxxxxxxxx1000000 ^ xxxxxxxxx0111111 == 0000000001111111
        // larger powers of 2 have their first set bit higher
        return value ^ (value - 1);
    }
    /// <summary>
    /// Numbers with more power-of-2-ness are superior.
    /// Also, power-of-2-ness makes a nice balanced binary structure: 1317131F1317131...
    /// This is not optimal, because nodes may be sparse w.r.t. the range, but it's good enough for an experiment.
    /// </summary>
    private bool ShouldBeParentOf(NestingDepthTreeNode other) {
        if (other == null) return false;
        return PowerOf2Ness(other._offset) < PowerOf2Ness(this._offset);
    }

    ///<summary>Returns the nesting depth at a particular index, as determined by the tree rooted at the given node.</summary>
    public static int QueryNestingDepthAt(NestingDepthTreeNode root, int index) {
        if (root == null) return 0;

        var pre = QueryNestingDepthAt(root._less, index);
        if (root._offset > index) return pre;
        
        var on = pre + root._adjust;
        if (root._offset == index) return on;

        var post = on + QueryNestingDepthAt(root._more, index);
        return post;
    }

    /// <summary>Scans the tree for transitions into and out of holes, running a callback for each one.</summary>
    private static void FindAndCallbackHoleTransitionsInOrder(NestingDepthTreeNode node, int initialNestingDepth, Action<NestingDepthTreeNode, bool> callback) {
        if (node == null) return;

        // we can skip this whole subtree if it's guaranteed to stay above water the whole time
        var lowestNestingDepth = node._subTreeRelativeMinimum + initialNestingDepth;
        if (initialNestingDepth > 0 && lowestNestingDepth > 0) return;

        // scan left subtree
        FindAndCallbackHoleTransitionsInOrder(node._less, initialNestingDepth, callback);

        // check for transition here
        var nestingDepthJustBeforeNode = initialNestingDepth + GetTotalAdjust(node._less);
        var nestingDepthJustAfterNode = nestingDepthJustBeforeNode + node._adjust;
        var wasInHole = nestingDepthJustBeforeNode <= 0;
        var nowInHole = nestingDepthJustAfterNode <= 0;
        if (wasInHole != nowInHole) {
            callback(node, nowInHole);
        }

        // scan right subtree
        FindAndCallbackHoleTransitionsInOrder(node._more, nestingDepthJustAfterNode, callback);
    }
    
    ///<summary>Returns all of the areas in the given interval where the tree would return zero if you queried the nesting depth there.</summary>
    public static IReadOnlyList<Interval> FindHolesIn(Interval interval, NestingDepthTreeNode root) {
        var relevantSegments = 
            FindCoveredIntervals(root)
            .SkipWhile(e => !e.Overlaps(interval))
            .TakeWhile(e => e.Overlaps(interval))
            .ToArray();

        var holeStarts = 
            new[] {interval.Offset}
            .Concat(relevantSegments
                    .Select(e => e.Offset + e.Length));
        var holeEnds = 
            relevantSegments
            .Select(e => e.Offset)
            .Concat(new[] {interval.Offset + interval.Length});

        return holeStarts
            .Zip(holeEnds, (start, end) => new Interval(start, end - start))
            .Where(e => e.Length > 0)
            .ToArray();
    }

    ///<summary>Returns the areas that the tree has covered; that aren't holes in the nesting depth.</summary>
    public static IReadOnlyList<Interval> FindCoveredIntervals(NestingDepthTreeNode root) {
        var coveredStart = (int?)null;
        var results = new List<Interval>();

        FindAndCallbackHoleTransitionsInOrder(
            root,
            0,
            (node, isIntoHole) => {
                if (coveredStart.HasValue != isIntoHole) {
                    throw new InvalidOperationException("Invariant violated: in-out-repeat");
                }
                if (coveredStart.HasValue) {
                    results.Add(new Interval(coveredStart.Value, node._offset - coveredStart.Value));
                    coveredStart = null;
                } else {
                    coveredStart = node._offset;
                }
            });

        return results;
    }

    /// <summary>
    /// When the nesting depth hits zero, we can actually split the tree in two around that hole and make future queries more efficient.
    /// This only works because arrays only care about the segment they're in, as opposed to the global state.
    /// </summary>
    public static void PartitionAroundHoles(NestingDepthTreeNode root) {
        var partitionsToPerform = new List<Action>();

        // find the nodes that have a hole to their left or right, so we can cut there
        FindAndCallbackHoleTransitionsInOrder(
            root, 
            0, 
            (node, isIntoHole) => partitionsToPerform.Add(() => PartitionToSideOfNode(node, isIntoHole ? +1 : -1)));
        
        // only do the partitioning after the search is done, so we don't interfere with it
        foreach (var e in partitionsToPerform) {
            e.Invoke();
        }
    }
    /// <summary>Cuts the tree in two, to the left of the given node if d is negative and to the right if d is positive.</summary>
    private static void PartitionToSideOfNode(NestingDepthTreeNode n, int d) {
        var orphan = n.Child(d);
        if (orphan != null) orphan._parent = null;
        n.SetChild(d, null);
        PartitionUpwardToParentSideOf(n, d, orphan);
    }
    /// <summary>Continues cutting the tree in two, assuming that child nodes have been handled.</summary>
    private static void PartitionUpwardToParentSideOf(NestingDepthTreeNode n, int d, NestingDepthTreeNode orphan) {
        if (n == null) return;
        n.RecomputeAggregates();

        var p = n._parent;
        if (n.DirToParent() == d) {
            // going from n to p crosses the cut line, so we must disconnect them
            n._parent = null;

            // the orphan node takes n's place
            p.SetChild(-d, orphan);
            if (orphan != null) orphan._parent = p;

            // switch directions back towards the cut, and onward with our new orphan n!
            PartitionUpwardToParentSideOf(p, -d, n);
        } else {
            // didn't pass over the cut line, keep going upwards
            PartitionUpwardToParentSideOf(p, d, orphan);
        }
    }

    ///<summary>Gets the child in the given direction (negative -> lesser child, positive -> larger child).</summary>
    public struct IncludeResult {
        /// <summary>
        /// The node that was created or modified by the include operation.
        /// Null when the node was imploded due to having no effect anymore.
        /// </summary>
        public readonly NestingDepthTreeNode AdjustedNode;
        /// <summary>
        /// The new root of the tree (or sub-tree).
        /// The node is in there somewhere...
        /// </summary>
        public readonly NestingDepthTreeNode NewRoot;

        public IncludeResult(NestingDepthTreeNode newRootAndAdjustedNode) {
            AdjustedNode = NewRoot = newRootAndAdjustedNode;
        }
        public IncludeResult(NestingDepthTreeNode adjustedNode, NestingDepthTreeNode newRoot) {
            AdjustedNode = adjustedNode;
            NewRoot = newRoot;
        }
    }

    /// <summary>
    /// Adds an adjustment to the tree rooted at the given node.
    /// The nesting depth will be perceived as the given adjust higher after the given index.
    /// Can also adjust reference counts.
    /// This can create, modify, or remove a node in the tree.
    /// </summary>
    public static IncludeResult Include(NestingDepthTreeNode root, int index, int adjust, int refAdjust) {
        if (root != null && root._parent != null) throw new ArgumentException("root.Parent != null");

        var preTotal = GetTotalAdjust(root);
        var result = IncludeHelper(root, index, adjust, refAdjust);
        var postTotal = GetTotalAdjust(result.NewRoot);
        if (preTotal + adjust != postTotal) {
            throw new InvalidOperationException("Invariant violated: total adjustment did not shift by the included adjustment.");
        }
        return result;
    }
    public static IncludeResult IncludeHelper(NestingDepthTreeNode root, int index, int adjust, int refAdjust) {
        // Do we need to create a new node?
        if (root == null) {
            return new IncludeResult(new NestingDepthTreeNode(index, adjust, refAdjust));
        }

        // It is understood that the caller will fixup our parent afterwards; we must act independently of the tree above us
        root._parent = null;

        // Is this node the one we need to adjust?
        if (index == root._offset) {
            root._adjust += adjust;
            root._refCount += refAdjust;
            root.RecomputeAggregates();

            // nodes can be removed when they are not referenced and have no effect on the totals
            if (root._adjust == 0 && root._refCount == 0) {
                return new IncludeResult(null, root.Implode());
            }

            return new IncludeResult(root);
        }

        // Pick the subtree the node has to end up and recurse the inclusion that-a-way
        var d = index.CompareTo(root._offset);
        var subtree = root.Child(d);
        var preTotal = GetTotalAdjust(subtree);
        var subResult = IncludeHelper(subtree, index, adjust, refAdjust);
        var postTotal = GetTotalAdjust(subResult.NewRoot);
        if (preTotal + adjust != postTotal) {
            throw new InvalidOperationException("Invariant violated: total adjustment did not shift by the included adjustment.");
        }

        // Great! Now we just need to fixup so the new subtree is linked in
        var c = subResult.NewRoot;
        root.SetChild(d, c);
        if (c != null) c._parent = root;
        root.RecomputeAggregates();

        // Oh, and do a token effort to keep things balanced using our hacky hierarchical ordering over the indexes
        // Can we get away with not rotating the new child above ourselves to keep things sorta balanced?
        if (c == null || !c.ShouldBeParentOf(root)) {
            return new IncludeResult(subResult.AdjustedNode, root);
        }

        // darn, need to rotate
        var s = c.Child(-d);
        c.SetChild(-d, root);
        root.SetChild(d, s);

        // fixup
        c._parent = null;
        root._parent = c;
        if (s != null) s._parent = root;
        root.RecomputeAggregates();
        c.RecomputeAggregates();

        // finally
        return new IncludeResult(subResult.AdjustedNode, c);
    }
    /// <summary>Removes this node from the tree, returning the child that moved into its place.</summary>
    private NestingDepthTreeNode Implode() {
        // I sure hope we're not in the inconvenient case...
        if (_less != null && _more != null) {
            // pick your favorite child to promote
            var d = _less.ShouldBeParentOf(_more) ? -1 : +1;
            var c = Child(d);
            var s = Child(-d);
            c._parent = null;

            // find a place for orphaned s
            var m = c;
            while (m.Child(-d) != null) {
                m = m.Child(-d);
            }
            m.SetChild(-d, s);
            s._parent = m;

            // a node can implode at most once, so we can afford to pay O(log n) for each
            // not sure if this blows the per-operation worst case past logarithmic or not, though
            m.RecomputeAggregatesToRoot();

            return c;
        }

        // No extra child to put somewhere, so we just toss the one (or none) we have off on our parent so we can go have fun.
        var p = _less ?? _more;
        if (p != null) p._parent = null;
        return p;
    }

    /// <summary>Fixes up the aggregate values stored by this node.</summary>
    private void RecomputeAggregates() {
        // total
        _subTreeTotalAdjust = GetTotalAdjust(_less) + _adjust + GetTotalAdjust(_more);

        // lowest dive
        var totalAdjustJustBefore = GetTotalAdjust(_less) + _adjust;
        var totalAdjustJustAfter = totalAdjustJustBefore + (_more == null ? 0 : _more._subTreeRelativeMinimum);
        _subTreeRelativeMinimum = Math.Min(totalAdjustJustBefore, totalAdjustJustAfter);
        if (_less != null) _subTreeRelativeMinimum = Math.Min(_less._subTreeRelativeMinimum, _subTreeRelativeMinimum);
    }
    /// <summary>Fixes up the aggregate values stored by this node and its ancestors.</summary>
    private void RecomputeAggregatesToRoot() {
        this.RecomputeAggregates();
        if (this._parent != null) this._parent.RecomputeAggregatesToRoot();
    }

    public override string ToString() {
        return "" + _offset;
    }
}
