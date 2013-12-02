using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

public static class TestingUtilities {
    public static void AssertTrue(this bool value) {
        Assert.IsTrue(value);
    }
    public static void AssertFalse(this bool value) {
        Assert.IsFalse(value);
    }
    public static void AssertEquals<T1, T2>(this T1 actual, T2 expected) {
        Assert.AreEqual(expected, actual);
    }
    public static void AssertReferenceEquals<T1, T2>(this T1 actual, T2 expected) {
        Assert.AreSame(expected, actual);
    }
    public static void AssertNotEqualTo<T1, T2>(this T1 actual, T2 expected) {
        Assert.AreNotEqual(expected, actual);
    }
    public static void AssertEqualsImplicit<T>(this T actual, T expected) {
        Assert.AreEqual(expected, actual);
    }

    public static void AssertThrows(Action action) {
        try {
            action();
        } catch (Exception) {
            return;
        }
        Assert.Fail("Expected method to throw, but it ran succesfully.");
    }
    public static void AssertThrows<T>(Func<T> action) {
        T result;
        try {
            result = action();
        } catch {
            return;
        }
        Assert.Fail("Expected method to throw, but it returned {0} instead.", result);
    }
    public static void AssertDoesNotThrow(Action action) {
        action();
    }
    public static void AssertDoesNotThrow<T>(Func<T> action) {
        action();
    }

    public static void AssertSequenceEquals<T>(this IEnumerable<T> actualSequence, params T[] expectedSequence) {
        actualSequence.AssertSequenceEquals(expectedSequence.AsEnumerable());
    }
    public static void AssertSequenceEquals<T>(this IEnumerable<T> actualSequence, IEnumerable<T> expectedSequence) {
        var items1 = actualSequence.ToArray();
        var items2 = expectedSequence.ToArray();
        if (items1.SequenceEqual(items2)) return;
        Assert.Fail("Expected sequences to be equal.{0}Actual Sequence: {1}{0}Expected Sequence: {2}", 
            Environment.NewLine + "\t",
            string.Join(", ", items1), 
            string.Join(", ", items2));
    }
}
