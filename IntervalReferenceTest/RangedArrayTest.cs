using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class CoverageTreeTest {
    [TestMethod]
    public void TestSimpleCase() {
        var mem = new TheFanciestMemory();
        mem.MemoryInUse.AssertEquals(0);
        var r = new RangedArray(100, mem);
        mem.MemoryInUse.AssertEquals(100);
        r.Dispose();
        mem.MemoryInUse.AssertEquals(0);

        var r2 = mem.NewArray(50);
        mem.MemoryInUse.AssertEquals(50);
        NestingDepthTreeNode.QueryNestingDepthAt(r2._refNode, -1).AssertEquals(0);
        NestingDepthTreeNode.QueryNestingDepthAt(r2._refNode, 0).AssertEquals(1);
        NestingDepthTreeNode.QueryNestingDepthAt(r2._refNode, 49).AssertEquals(1);
        NestingDepthTreeNode.QueryNestingDepthAt(r2._refNode, 50).AssertEquals(0);

        var r3 = r2.Slice(new Interval(10, 15));
        r2[10] = 5;
        mem.MemoryInUse.AssertEquals(50);
        r2.Dispose();
        r3[0].AssertEquals(5);
        mem.MemoryInUse.AssertEquals(15);
        r3.Dispose();
        mem.MemoryInUse.AssertEquals(0);
    }

    [TestMethod]
    public void TestRandomizedCase() {
        var mem = new TheFanciestMemory();
        var rng = new Random(123456);

        var bigArraySize = 1000;
        var bigArray = mem.NewArray(bigArraySize);
        var slicesOfBigArray = new List<RangedArray>();
        var sliceRanges = new List<Tuple<int, int>>();
        for (var i = 0; i < 100; i++) {
            var v1 = rng.Next(bigArraySize);
            var v2 = rng.Next(bigArraySize);
            var offset = Math.Min(v2, v1);
            var len = Math.Max(v2, v1) - offset + 1;

            slicesOfBigArray.Add(bigArray.Slice(new Interval(offset, len)));
            sliceRanges.Add(Tuple.Create(offset, len));
        }

        bigArray.Dispose();
        while (slicesOfBigArray.Count > 0) {
            var covered = Enumerable.Range(0, bigArraySize).Count(i => sliceRanges.Any(a => i >= a.Item1 && i < a.Item1 + a.Item2));
            mem.MemoryInUse.AssertEquals(covered);
            var x = rng.Next(slicesOfBigArray.Count);
            foreach (var slice in slicesOfBigArray) {
                NestingDepthTreeNode.GetTotalAdjust(NestingDepthTreeNode.RootOf(slice._refNode)).AssertEquals(0);
            }
            slicesOfBigArray[x].Dispose();
            slicesOfBigArray.RemoveAt(x);
            sliceRanges.RemoveAt(x);
        }
        mem.MemoryInUse.AssertEquals(0);
    }
}
