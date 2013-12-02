using System;
using System.Collections.Generic;

public sealed class TheFanciestMemory {
    public int MemoryInUse { get; private set; }
    private enum WordState {
        Free,
        Uninitialized,
        Readable
    }
    private sealed class Word {
        public WordState State;
        public int Value;
    }
    private readonly List<Word> _memory = new List<Word>();

    public int this[int location] {
        get {
            var v = location < 0 || location >= _memory.Count ? new Word() : _memory[location];
            if (v.State == WordState.Free) throw new InvalidOperationException("Read memory that's not allocated.");
            if (v.State == WordState.Uninitialized) throw new InvalidOperationException("Read memory that hasn't been initialized.");
            return v.Value;
        }
        set {
            var v = location < 0 || location >= _memory.Count ? new Word() : _memory[location];
            if (v.State == WordState.Free) throw new InvalidOperationException("Wrote to memory that's not allocated.");
            v.State = WordState.Readable;
            v.Value = value;
        }
    }

    public Interval Malloc(int len) {
        if (len == 0) return default(Interval);

        // O(M+N) time is the best time

        var i = 0;
        var run = 0;
        while (i < _memory.Count) {
            run += 1;
            if (_memory[i].State != WordState.Free) run = 0;
            i++;
            if (run == len) break;
        }
        
        i -= run;
        while (run < len) {
            _memory.Add(new Word {State = WordState.Free});
            run += 1;
        }

        for (var j = 0; j < run; j++) {
            _memory[i + j].State = WordState.Uninitialized;
            MemoryInUse += 1;
        }
        return new Interval(i, len);
    }
    public void Free(Interval span) {
        if (span.Offset < 0) throw new InvalidOperationException("Tried to free already-free memory.");
        if (span.Offset + span.Length > _memory.Count) throw new InvalidOperationException("Tried to free already-free memory.");

        for (var i = 0; i < span.Length; i++) {
            if (_memory[span.Offset+i].State == WordState.Free) throw new InvalidOperationException("Tried to free already-free memory.");
        }
        for (var i = 0; i < span.Length; i++) {
            _memory[span.Offset + i].State = WordState.Free;
            MemoryInUse -= 1;
        }
    }

    public RangedArray NewArray(int length) {
        return new RangedArray(length, this);
    }
    public RangedArray SliceArray(RangedArray array, Interval interval) {
        return new RangedArray(array, interval, this);
    }
}
