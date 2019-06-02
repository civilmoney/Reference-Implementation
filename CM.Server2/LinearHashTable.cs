#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CM.Server {

    public delegate TkeyOrValue DeserializeDelegate<TkeyOrValue>(System.IO.BinaryReader br);

    public delegate int HashDelegate<TKey>(TKey key);

    public delegate void SerializeDelegate<T>(T keyOrValue, System.IO.BinaryWriter bw);

    public class LinearHashTableException : Exception {
        public LinearHashTableException(string msg) : base(msg) { }
    }

    /// <summary>
    /// A disk-persisted linear hash table used by the Storage class.
    /// </summary>
    public class LinearHashTable<Tkey, Tvalue> : IDisposable {
        private const int MinBucketCount = 32;

        private readonly object _Sync = new object();
        private DeserializeDelegate<Tkey> _DeserializeKey;
        private DeserializeDelegate<Tvalue> _DeserializeValue;
        private HashDelegate<Tkey> _Hash;
        private SerializeDelegate<Tkey> _SerializeKey;
        private SerializeDelegate<Tvalue> _SerializeValue;
        private Store _Store;
        private int Capacity = 5;
        private double MaxLoadFactor = 0.95;
        private double MinLoadFactor = 0.75;
        private int N;
        private int P;

        public LinearHashTable() {
            N = MinBucketCount;
            _Hash = new HashDelegate<Tkey>(DefaultHash);
            _Store = new MemoryStore(this);
            _Store.BucketCount = N;
        }

        public LinearHashTable(string file,
            HashDelegate<Tkey> onHashKey,
            SerializeDelegate<Tkey> onSerializeKey, DeserializeDelegate<Tkey> onDeserializeKey,
            SerializeDelegate<Tvalue> onSerializeValue, DeserializeDelegate<Tvalue> onDeserializeValue
            ) {
            N = MinBucketCount;
            _Hash = onHashKey;
            _SerializeKey = onSerializeKey;
            _DeserializeKey = onDeserializeKey;
            _SerializeValue = onSerializeValue;
            _DeserializeValue = onDeserializeValue;
            _Store = new FileStore(file, this);
        }

        private double LoadFactor {
            get { return (double)_Store.Count / (double)(Capacity * _Store.BucketCount); }
        }

        public void Flush() {
            if (_Store != null)
                _Store.Flush();
        }

        public void Set(Tkey key, Tvalue value) {
            lock (_Sync) {
                int idx = Bucket(key);
                _Store.Set(idx, key, value);
                while (LoadFactor > MaxLoadFactor)
                    _Store.Grow();
            }
        }

        public bool TryGetValue(Tkey key, out Tvalue value) {
            lock (_Sync) {
                int idx = Bucket(key);
                return _Store.Get(idx, key, out value);
            }
        }

        public bool TryRemove(Tkey key, out Tvalue value) {
            lock (_Sync) {
                int idx = Bucket(key);
                bool res = _Store.Remove(idx, key, out value);
                if (res) {
                    while (_Store.BucketCount > MinBucketCount
                        && LoadFactor < MinLoadFactor)
                        _Store.Shrink();
                }
                return res;
            }
        }

        private static int DefaultHash(Tkey k) {
            return k.GetHashCode();
        }

        private int Bucket(Tkey k) {
            int hash = _Hash(k);
            int idx = hash & (N - 1);
            if (idx < P)
                idx = hash & ((N << 1) - 1);
            return idx;
        }
      
        /// <summary>
        /// A disk-persisted linear hash table store
        /// </summary>
        private class FileStore : Store {
            private List<Bucket> _Buckets;
            private System.IO.BinaryReader _DataReader;
            private System.IO.BinaryWriter _DataWriter;
            private string _File;
            private Queue<Bucket> _Free;
            private Index _Index;
            private object _Sync;

            public FileStore(string file, LinearHashTable<Tkey, Tvalue> owner) : base(owner) {
                _File = file;
                _Sync = new object();
                _DataReader = new System.IO.BinaryReader(new System.IO.FileStream(file + ".htdata", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite, 4096, System.IO.FileOptions.WriteThrough));
                _DataWriter = new System.IO.BinaryWriter(new System.IO.FileStream(_File + ".htdata", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite, 4096, System.IO.FileOptions.WriteThrough));
                _Buckets = new List<Bucket>();
                _Free = new Queue<Bucket>();
                try {
                    _Index = new Index(file);
                    InitializeFromIndex();
                } catch {
                    // Make sure we don't lock the data file
                    Dispose();
                    throw;
                }
            }

            [Flags]
            private enum BucketFlags : int {
                None = 0,
                IsOverflow = 1 << 1
            }

            public override int BucketCount {
                get {
                    return _Buckets.Count;
                }

                set {
                    while (_Buckets.Count < value)
                        AddNewBucket(null);
                }
            }

            public override int Count {
                get {
                    return _Index.Count;
                }
            }
   
            public override void Flush() {
                lock (_Sync) {
                    if (disposedValue)
                        return;

                    PerformConsistencyCheck();

                    for (int i = 0; i < _Buckets.Count; i++) {
                        _Buckets[i].Flush(_DataWriter, _DataReader);
                        var child = _Buckets[i].Overflow;
                        while (child != null) {
                            child.Flush(_DataWriter, _DataReader);
                            child = child.Overflow;
                        }
                    }
                    foreach (var f in _Free)
                        f.Flush(_DataWriter, _DataReader);

                    _Index?.Flush(); // Index can be null here if it failed to instantiate
                }
            }

            public override bool Get(int bucket, Tkey key, out Tvalue value) {
                lock (_Sync) {
                    var b = _Buckets[bucket];
                    while (b != null) {
                        for (int i = 0; i < b.Count; i++) {
                            long keyOffset, dataOffet;
                            b.Get(i, out keyOffset, out dataOffet);
                            Debug.Assert(keyOffset >= 0 && dataOffet > 0, "Invalid key/data offsets.");
                            _DataReader.BaseStream.Position = keyOffset;
                            var k = Owner._DeserializeKey(_DataReader);
                            if (k == null || !k.Equals(key))
                                continue;
                            _DataReader.BaseStream.Position = dataOffet;
                            value = Owner._DeserializeValue(_DataReader);
                            return true;
                        }
                        b = b.Overflow;
                    }
                    value = default(Tvalue);
                    return false;
                }
            }

            public override void Grow() {
                lock (_Sync) {
                    var target = _Buckets[Owner.P];
                    BucketCount++;
                    int newBucket = BucketCount - 1;
                    var newItems = _Buckets[newBucket];
                    var b = target;
                    while (b != null) {
                        for (int i = 0; i < b.Count; i++) {
                            long keyOffset, dataOffset;
                            b.Get(i, out keyOffset, out dataOffset);
                            _DataReader.BaseStream.Position = keyOffset;
                            var k = Owner._DeserializeKey(_DataReader);
                            int hash = Owner._Hash(k);
                            hash = hash & ((Owner.N << 1) - 1);
                            if (hash == newBucket) {
                                while (!newItems.Append(keyOffset, dataOffset))
                                    newItems = AddNewBucket(newItems);
                                b.RemoveAt(i--);
                            }
                        }
                        b = b.Overflow;
                    }

                    CompactBucket(target);

                    if (++Owner.P == Owner.N) {
                        Owner.N *= 2;
                        Owner.P = 0;
                    }
                    _Index.N = Owner.N;
                    _Index.P = Owner.P;
                    _Index.IsDirty = true;
                }
            }

            public override bool Remove(int bucket, Tkey key, out Tvalue value) {
                lock (_Sync) {
                    var b = _Buckets[bucket];
                    while (b != null) {
                        for (int i = 0; i < b.Count; i++) {
                            long keyOffset, dataOffet;
                            b.Get(i, out keyOffset, out dataOffet);
                            Debug.Assert(keyOffset >= 0 && dataOffet > 0, "Invalid key/data offsets.");
                            _DataReader.BaseStream.Position = keyOffset;
                            var k = Owner._DeserializeKey(_DataReader);
                            if (k == null || !k.Equals(key))
                                continue;
                            _DataReader.BaseStream.Position = dataOffet;
                            value = Owner._DeserializeValue(_DataReader);
                            b.RemoveAt(i);
                            _Index.Count--;
                            _Index.IsDirty = true;
                            return true;
                        }
                        b = b.Overflow;
                    }
                    value = default(Tvalue);
                    return false;
                }
            }

            public override void Set(int bucket, Tkey key, Tvalue value) {
                lock (_Sync) {
                    long keyOffset, dataOffet;
                    var b = _Buckets[bucket];
                    while (b != null) {
                        for (int i = 0; i < b.Count; i++) {
                            b.Get(i, out keyOffset, out dataOffet);
                            Debug.Assert(keyOffset >= 0 && dataOffet > 0, "Invalid key/data offsets.");
                            _DataReader.BaseStream.Position = keyOffset;
                            var k = Owner._DeserializeKey(_DataReader);
                            if (k == null || !k.Equals(key))
                                continue;
                            dataOffet = _DataWriter.BaseStream.Position = _DataWriter.BaseStream.Length;
                            Owner._SerializeValue(value, _DataWriter);
                            b.Set(i, keyOffset, dataOffet);

                            _DataWriter.Flush();

                            _Index.Fragmentations++;
                            if (_Index.Fragmentations >= 100) {
                                CompactData();
                            }
                            return;
                        }
                        b = b.Overflow;
                    }
                  
                    keyOffset = _DataWriter.BaseStream.Position = _DataWriter.BaseStream.Length;
                    Owner._SerializeKey(key, _DataWriter);
                    dataOffet = _DataWriter.BaseStream.Position = _DataWriter.BaseStream.Length;
                    Owner._SerializeValue(value, _DataWriter);
                    b = _Buckets[bucket];
                    while (!b.Append(keyOffset, dataOffet)) {
                        b = AddNewBucket(b);
                    }
                    _DataWriter.Flush();
                    _Index.Count++;
                    _Index.IsDirty = true;
                }
            }

            public override void Shrink() {
                lock (_Sync) {
                    var target = _Buckets[_Buckets.Count - 1];
                    _Buckets.RemoveAt(_Buckets.Count - 1);
                    if (Owner.P == 0) {
                        Owner.N /= 2;
                        Owner.P = Owner.N - 1;
                    } else {
                        Owner.P--;
                    }
                    _Index.N = Owner.N;
                    _Index.P = Owner.P;
                    var b = target;
                    while (b != null) {
                        for (int i = 0; i < b.Count; i++) {
                            long keyOffset, dataOffset;
                            b.Get(i, out keyOffset, out dataOffset);
                            _DataReader.BaseStream.Position = keyOffset;
                            var k = Owner._DeserializeKey(_DataReader);
                            int newBucket = Owner.Bucket(k);
                            var dst = _Buckets[newBucket];
                            while (!dst.Append(keyOffset, dataOffset))
                                dst = AddNewBucket(dst);
                        }
                        var tmp = b;
                        b = b.Overflow;
                        Discard(tmp);
                    }
                    _Index.IsDirty = true;
                }
            }

            public void CompactData() {
                lock (_Sync) {
                   
                    var newBucketOffsets = new List<long>();
                    var newBuckets = new List<Bucket>();

                    using (var s = new System.IO.FileStream(_File + ".htdata-compact", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite)){
                        s.SetLength(0);
                        var bw = new System.IO.BinaryWriter(s);
                        var br = new System.IO.BinaryReader(s);
                     
                       
                        for (int i = 0; i < _Buckets.Count; i++) {
                            var b = _Buckets[i];

                            newBucketOffsets.Add(s.Length);
                            var newBucket = new Bucket(br, bw, s.Length, _Index.BucketCapacity);
                            newBucket.Index = newBuckets.Count;
                            newBucket.IsDirty = true;
                            newBuckets.Add(newBucket);
                            while (b != null) {
                                for (int x = 0; x < b.Count; x++) {
                                    long keyOffset, dataOffset;
                                    b.Get(x, out keyOffset, out dataOffset);
                                    object lastKey = null;
                                    try {
                                        _DataReader.BaseStream.Position = keyOffset;
                                        var k = Owner._DeserializeKey(_DataReader);
                                        lastKey = k;
                                        if (k == null)
                                            continue;
                                        _DataReader.BaseStream.Position = dataOffset;
                                        var v = Owner._DeserializeValue(_DataReader);

                                        keyOffset = s.Length;
                                        if (s.Position != keyOffset) s.Position = keyOffset;
                                        Owner._SerializeKey(k, bw);

                                        dataOffset = s.Length;
                                        if (s.Position != dataOffset) s.Position = dataOffset;
                                        Owner._SerializeValue(v, bw);

                                        bw.Flush();

                                        while (!newBucket.Append(keyOffset, dataOffset)) {
                                            newBucket.Flush(bw, br);
                                            var parent = newBucket;
                                            newBucketOffsets.Add(s.Length);
                                            newBucket = new Bucket(br, bw, s.Length, _Index.BucketCapacity);
                                            newBucket.Index = parent.Index;
                                            newBucket.Flags |= BucketFlags.IsOverflow;
                                            newBucket.IsDirty = true;
                                            parent.Overflow = newBucket;
                                            newBucket.Parent = parent;
                                        }

                                    } catch (Exception ex) {
                                        Debug.WriteLine("WARNING: Compact failed to move data on key '"+ lastKey + "': "+ex);
                                        // delete the errant record
                                    }
                                }
                                newBucket.Flush(bw, br);
                                b = b.Overflow;
                            }
                        }

                        bw.Flush();
                    }
                   
                    _DataWriter.Dispose();
                    _DataWriter = null;
                    _DataReader.Dispose();
                    _DataReader = null;

                    System.IO.File.Delete(_File + ".htdata"); // If this succeeds, so should the move.
                    System.IO.File.Move(_File + ".htdata-compact", _File + ".htdata");

                    _DataReader = new System.IO.BinaryReader(new System.IO.FileStream(_File + ".htdata", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite, 4096, System.IO.FileOptions.WriteThrough));
                    _DataWriter = new System.IO.BinaryWriter(new System.IO.FileStream(_File + ".htdata", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite, 4096, System.IO.FileOptions.WriteThrough));
                    _Index.Fragmentations = 0;
                    _Index.BucketOffsets = newBucketOffsets;
                    _Index.IsDirty = true;
                    _Free.Clear();
                    _Buckets = newBuckets;
                    Flush();
                }

            }

            protected override void Dispose(bool disposing) {
                lock (_Sync) {
                    if (!disposedValue) {
                        try {
                            base.Dispose(disposing); // will call flush
                        } catch (LinearHashTableException) {
                            // ignore consistency check failure
                        }
                        if (disposing) {
                            _Buckets = null;
                            if (_Index != null) {
                                _Index.Dispose();
                                _Index = null;
                            }
                            _DataReader.Dispose();
                            _DataReader = null;
                            _DataWriter.Dispose();
                            _DataWriter = null;
                        }
                        disposedValue = true;
                    }
                }
            }

            private Bucket AddNewBucket(Bucket parent) {
                lock (_Sync) {
                    Bucket b;
                    if (_Free.Count > 0) {
                        b = _Free.Dequeue();
                        Debug.Assert(b.Overflow == null && b.Parent == null && b.Index == -1);
                    } else {
                        long baseOffset = _DataWriter.BaseStream.Length;
                        b = new Bucket(_DataReader, _DataWriter, baseOffset, _Index.BucketCapacity);
                        _Index.BucketOffsets.Add(baseOffset);
                    }
                    if (parent == null) {
                        b.Index = _Buckets.Count;
                        _Buckets.Add(b);
                    } else {
                        while (parent.Overflow != null)
                            parent = parent.Overflow;
                        b.Index = parent.Index;
                        b.Flags |= BucketFlags.IsOverflow;
                        parent.Overflow = b;
                        b.Parent = parent;
                    }
                    PerformConsistencyCheck();
                    b.IsDirty = true;
                    _Index.IsDirty = true;
                    return b;
                }
            }

            private void CompactBucket(Bucket b) {
                if (b.Overflow == null)
                    return; // nothing to clean

                // Reorganise overflowed data into the main bucket
                var dest = b;
                var child = b.Overflow;
                while (child != null) {
                    for (int i = 0; i < child.Count && child != dest; i++) {
                        long keyOffset, dataOffset;
                        child.Get(i, out keyOffset, out dataOffset);
                        child.RemoveAt(i--);
                        while (!dest.Append(keyOffset, dataOffset))
                            dest = dest.Overflow;
                    }
                    if (child.Count == 0) {
                        // remove from chain
                        var tmp = child;
                        child = tmp.Parent;
                        child.Overflow = tmp.Overflow;
                        if (child.Overflow != null)
                            child.Overflow.Parent = tmp.Parent;
                        Discard(tmp);
                    }
                    child = child.Overflow;
                }
            }

            private void Discard(Bucket tmp) {
                tmp.Index = -1;
                tmp.Flags = BucketFlags.None;
                tmp.Overflow = null;
                tmp.Parent = null;
                tmp.IsDirty = true;
                _Free.Enqueue(tmp);
            }

            private void InitializeFromIndex() {
                if (_Index.BucketOffsets.Count != 0) {
                    // start up validation
                    if (_Index.BucketCapacity != Owner.Capacity)
                        Debug.WriteLine(String.Format("WARNING: This database uses capacity {0} but {1} was requested.", _Index.BucketCapacity, Owner.Capacity));
                    Owner.Capacity = _Index.BucketCapacity;
                    Owner.N = _Index.N;
                    Owner.P = _Index.P;

                    // Load bucket state

                    var overflows = new List<Bucket>();
                    for (int i = 0; i < _Index.BucketOffsets.Count; i++) {
                        var b = new Bucket(_DataReader, _DataWriter, _Index.BucketOffsets[i], Owner.Capacity);
                        if (b.Index == -1
                            || (b.Flags & BucketFlags.IsOverflow) == BucketFlags.IsOverflow) {
                            overflows.Add(b);
                        } else {
                            _Buckets.Add(b);
                        }
                    }
                    _Buckets.Sort((a, b) => { return a.Index.CompareTo(b.Index); });

                    for (int i = 0; i < overflows.Count; i++) {
                        if (overflows[i].Index == -1) {
                            _Free.Enqueue(overflows[i]);
                            continue;
                        }
                        var b = _Buckets[overflows[i].Index];
                        while (b != null) {
                            if (b.Overflow == null) {
                                b.Overflow = overflows[i];
                                overflows[i].Parent = b;
                                break;
                            }
                            b = b.Overflow;
                        }
                    }

                    PerformConsistencyCheck();
                } else {
                    _Index.BucketCapacity = Owner.Capacity;
                    _Index.N = Owner.N;
                    _Index.P = Owner.P;
                    BucketCount = _Index.N;
                    _Index.IsDirty = true;
                }
            }

            private void PerformConsistencyCheck() {

                //long max = _DataReader.BaseStream.Length;
               
                // Basic consistency checks
                for (int i = 0; i < _Buckets.Count; i++) {
                    var b = _Buckets[i];
                    if (b.Index != i) {
                        throw new LinearHashTableException("LinearHashTable bucket consistency check failure: " + _File);
                    }

                    //var seen = new HashSet<Tkey>();
                    //for (int x = 0; x < b.Count; x++) {
                    //    long key, data;
                    //    b.Get(x, out key, out data);
                    //
                    //    if (key >= max)
                    //        throw new LinearHashTableException("Bad bucket key");
                    //    if (data >= max)
                    //        throw new LinearHashTableException("Bad bucket value");
                    //    _DataReader.BaseStream.Position = key;
                    //    var k = Owner._DeserializeKey(_DataReader);
                    //    if (seen.Contains(k))
                    //        throw new LinearHashTableException("Dupe key");
                    //    seen.Add(k);
                    //    if (k == null)
                    //        throw new LinearHashTableException("Bad key");
                    //}

                    var child = b.Overflow;
                    while (child != null) {
                        if ((child.Flags & BucketFlags.IsOverflow) != BucketFlags.IsOverflow) {
                            throw new LinearHashTableException("LinearHashTable overflow consistency check failure: " + _File);
                        }
                        if (child.Index != i) {
                            throw new LinearHashTableException("LinearHashTable overflow index consistency check failure: " + _File);
                        }

                        //for (int x = 0; x < child.Count; x++) {
                        //    long key, data;
                        //    child.Get(x, out key, out data);
                        //   
                        //    if (key >= max) 
                        //        throw new LinearHashTableException("Bad bucket key");
                        //    if (data >= max)
                        //        throw new LinearHashTableException("Bad bucket value");
                        //    _DataReader.BaseStream.Position = key;
                        //    var k = Owner._DeserializeKey(_DataReader);
                        //    if(k==null)
                        //        throw new LinearHashTableException("Bad key");
                        //    if (seen.Contains(k))
                        //        throw new LinearHashTableException("Dupe key");
                        //    seen.Add(k);
                        //}

                        child = child.Overflow;
                    }
                }
                foreach (var b in _Free) {
                    if (b.Index != -1)
                        throw new LinearHashTableException("LinearHashTable free consistency check failure: " + _File);
                }
            }

            /// <summary>
            /// Buckets are fixed-length with some overflow capability.
            /// Capacities are always constant -- the number of key/data file offset entries.
            /// </summary>
            /// <remarks>
            /// Binary format is:
            /// [index][flags][keyOffsetN][dataOffsetN]
            /// </remarks>
            private class Bucket {
                public Bucket Overflow;
                public Bucket Parent;
                private long _BaseOffset;
                private long[] _DataOffsets;
                private long _EndOffset;
                private long[] _KeyOffsets;
                private int _Size;
                private object _Sync;

                public Bucket(System.IO.BinaryReader reader, System.IO.BinaryWriter writer, long baseOffset, int capacity) {
                    _Sync = new object();
                    _Size = (capacity * sizeof(long) * 2)
                        + 4 + 4; // Index + Flags
                    _BaseOffset = baseOffset;
                    _EndOffset = _BaseOffset + _Size;
                    _KeyOffsets = new long[capacity];
                    _DataOffsets = new long[capacity];
                    Capacity = capacity;
                    Load(reader, writer);
                }

                public int Capacity { get; private set; }
                public int Count { get; private set; }
                public BucketFlags Flags { get; set; }
                public int Index { get; set; }
                public bool IsDirty { get; set; }

                public bool Append(long keyoffset, long dataoffset) {
                    if (Count == Capacity)
                        return false; // The caller will have to make an Overflow bucket
                    Count++;
                    int index = Count - 1;
                    _KeyOffsets[index] = keyoffset;
                    _DataOffsets[index] = dataoffset;
                    IsDirty = true;
                    return true;
                }
                public void MoveToNewFileOffset(System.IO.BinaryWriter writer, System.IO.BinaryReader reader, long newBaseOffset) {
                    _BaseOffset = newBaseOffset;
                    _EndOffset = _BaseOffset + _Size;
                    IsDirty = true;
                    Flush(writer, reader);
                }
                public void Flush(System.IO.BinaryWriter writer, System.IO.BinaryReader reader) {
                    lock (_Sync) {
                        if (!IsDirty)
                            return;
                        writer.BaseStream.Position = _BaseOffset;
                        writer.Write(Index);
                        writer.Write((int)Flags);
                        for (int i = 0; i < Capacity; i++) {
                            writer.Write(_KeyOffsets[i]);
                            writer.Write(_DataOffsets[i]);
                        }
                        writer.Flush();

                        reader.BaseStream.Position = _BaseOffset;
                        int testIndex = reader.ReadInt32();
                        BucketFlags testFlags = (BucketFlags)reader.ReadInt32();
                        if (testIndex != Index || testFlags != Flags)
                            throw new LinearHashTableException("Sanity check failure during flush.");
                        
                        IsDirty = false;
                    }
                }

                public void Get(int index, out long keyoffset, out long dataoffset) {
                    keyoffset = _KeyOffsets[index];
                    dataoffset = _DataOffsets[index];
                }

                public void RemoveAt(int index) {
                    Count = Math.Max(0, Count - 1);
                    if (index < Count) {
                        Array.Copy(_KeyOffsets, index + 1, _KeyOffsets, index, Count - index);
                        Array.Copy(_DataOffsets, index + 1, _DataOffsets, index, Count - index);
                    }
                    _KeyOffsets[Count] = 0;
                    _DataOffsets[Count] = 0;
                    IsDirty = true;
                }

                public void Set(int index, long keyoffset, long dataoffset) {
                    _KeyOffsets[index] = keyoffset;
                    _DataOffsets[index] = dataoffset;
                    IsDirty = true;
                }

                public override string ToString() {
                    int count = Count;
                    var b = Overflow;
                    while (b != null) {
                        count += b.Count;
                        b = b.Overflow;
                    }

                    return Index + " Count: " + count.ToString() + " Flags: " + this.Flags;
                }

                private void Load(System.IO.BinaryReader reader, System.IO.BinaryWriter writer) {
                    lock (_Sync) {
                        reader.BaseStream.Position = _BaseOffset;
                        if (writer.BaseStream.Length < _EndOffset)
                            writer.BaseStream.SetLength(_EndOffset);
                        Index = reader.ReadInt32();
                        Flags = (BucketFlags)reader.ReadInt32();
                        for (int i = 0; i < Capacity; i++) {
                            _KeyOffsets[i] = reader.ReadInt64();
                            _DataOffsets[i] = reader.ReadInt64();
                            if (_DataOffsets[i] != 0)
                                Count++;
                        }
                    }
                }
            }

            private class Index : IDisposable {
                public int BucketCapacity;
                public List<long> BucketOffsets;
                public int Count;
                public bool IsDirty;
                public int N;
                public int P;
                public int Fragmentations;
                private System.IO.FileStream _Bak;
                private System.IO.FileStream _File;
                private System.IO.BinaryReader _Reader;
                private object _Sync;
                private System.IO.BinaryWriter _Writer;

                public Index(string file) {
                    _Sync = new object();
                    BucketOffsets = new List<long>();

                    _Bak = new System.IO.FileStream(file + ".htindex-bak", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None);
                    _File = new System.IO.FileStream(file + ".htindex", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None);
                    _Reader = new System.IO.BinaryReader(_File);
                    _Writer = new System.IO.BinaryWriter(_Reader.BaseStream);

                    if (_Bak.Length > 0) {
                        // The htindex is in an inconsistent state, recover
                        Recover();
                    }
                    Load();
                }

                public void Flush() {
                    lock (_Sync) {
                        if (!IsDirty)
                            return;

                        Backup();

                        _Writer.BaseStream.Position = 0;
                        _Writer.Write(Count);
                        _Writer.Write(P);
                        _Writer.Write(N);
                        _Writer.Write(BucketCapacity);
                        _Writer.Write(BucketOffsets.Count);
                        for (int i = 0; i < BucketOffsets.Count; i++)
                            _Writer.Write(BucketOffsets[i]);
                        _Writer.Write(Fragmentations);
                        _Writer.BaseStream.SetLength(_Writer.BaseStream.Position);

                        // We got this far without power failure, delete
                        // the backup.
                        _Bak.SetLength(0);

                        IsDirty = false;
                    }
                }

                private void Backup() {
                    _Bak.SetLength(0);
                    // The backup file will have bytes 1,2,3,4 at the beginning
                    // only when it is in a consistent state.
                    _Bak.WriteByte(0);
                    _Bak.WriteByte(0);
                    _Bak.WriteByte(0);
                    _Bak.WriteByte(0);
                    _Reader.BaseStream.Position = 0;
                    _Reader.BaseStream.CopyTo(_Bak);
                    // We got this far without a power failure, flag as consistent
                    _Bak.Position = 0;
                    _Bak.WriteByte(1);
                    _Bak.WriteByte(2);
                    _Bak.WriteByte(3);
                    _Bak.WriteByte(4);
                    _Bak.Flush();
                }

                private void Load() {
                    lock (_Sync) {
                        _Reader.BaseStream.Position = 0;
                        if (_Reader.BaseStream.Length == 0)
                            return;
                        Count = _Reader.ReadInt32();
                        P = _Reader.ReadInt32();
                        N = _Reader.ReadInt32();
                        BucketCapacity = _Reader.ReadInt32();
                        int buckets = _Reader.ReadInt32();
                        for (int i = 0; i < buckets; i++) {
                            BucketOffsets.Add(_Reader.ReadInt64());
                        }
                        if (_Reader.BaseStream.Position == _Reader.BaseStream.Length)
                            return;
                        Fragmentations = Math.Max(0, _Reader.ReadInt32());
                    }
                }

                private void Recover() {
                    _Bak.Position = 0;
                    if (_Bak.ReadByte() == 1
                     && _Bak.ReadByte() == 2
                     && _Bak.ReadByte() == 3
                     && _Bak.ReadByte() == 4) {
                        // We're consistent, repair the index
                        _Reader.BaseStream.Position = 0;
                        _Bak.CopyTo(_Reader.BaseStream);
                        _Reader.BaseStream.Flush();
                        _Bak.SetLength(0); // clear the bak
                    } else {
                        Dispose();
                        // Both the index and backup are inconsistent.
                        throw new LinearHashTableCorruptionException();
                    }
                }

                #region IDisposable Support

                private bool disposedValue = false;

                public void Dispose() {
                    Dispose(true);
                }

                protected virtual void Dispose(bool disposing) {
                    lock (_Sync) {
                        if (!disposedValue) {
                            try {
                                Flush(); 
                            } finally {
                                // We still need to dispose when there's a consistency failure
                                if (disposing) {
                                    BucketOffsets = null;
                                    _Reader.Dispose();
                                    _Reader = null;
                                    _Writer.Dispose();
                                    _Writer = null;
                                    _Bak.Dispose();
                                    _Bak = null;
                                    _File.Dispose();
                                    _File = null;
                                }
                                disposedValue = true;
                            }
                        }
                    }
                }

                #endregion IDisposable Support
            }
        }

        /// <summary>
        /// A basic RAM implementation of the hash table store
        /// </summary>
        private class MemoryStore : Store {
            private int _Count;
            private List<List<KeyValuePair<Tkey, Tvalue>>> _Items;

            public MemoryStore(LinearHashTable<Tkey, Tvalue> owner) : base(owner) {
                _Items = new List<List<KeyValuePair<Tkey, Tvalue>>>();
            }

            public override int BucketCount {
                get {
                    return _Items.Count;
                }
                set {
                    while (_Items.Count < value)
                        _Items.Add(new List<KeyValuePair<Tkey, Tvalue>>());
                }
            }

            public override int Count { get { return _Count; } }

            public override void Flush() {
                // nothing to do
            }

            public override bool Get(int bucket, Tkey key, out Tvalue value) {
                var ar = _Items[bucket];
                var o = ar.Find(x => x.Key.Equals(key));
                value = o.Value;
                return o.Key != null;
            }

            public override void Grow() {
                var ar = _Items[Owner.P];
                BucketCount++;
                int newBucket = BucketCount - 1;
                var newItems = _Items[newBucket];
                for (int i = 0; i < ar.Count; i++) {
                    int hash = Owner._Hash(ar[i].Key);
                    hash = hash & ((Owner.N << 1) - 1);
                    if (hash == newBucket) {
                        newItems.Add(ar[i]);
                        ar.RemoveAt(i--);
                    }
                }
                if (++Owner.P == Owner.N) {
                    Owner.N *= 2;
                    Owner.P = 0;
                }
            }

            public override bool Remove(int bucket, Tkey key, out Tvalue value) {
                var ar = _Items[bucket];
                var o = ar.Find(x => x.Key.Equals(key));
                if (o.Key != null) {
                    ar.Remove(o);
                    _Count--;
                }
                value = o.Value;
                return o.Key != null;
            }

            public override void Set(int bucket, Tkey key, Tvalue value) {
                var ar = _Items[bucket];
                for (int i = 0; i < ar.Count; i++)
                    if (ar[i].Key.Equals(key)) {
                        ar[i] = new KeyValuePair<Tkey, Tvalue>(key, value);
                        return;
                    }
                ar.Add(new KeyValuePair<Tkey, Tvalue>(key, value));
                _Count++;
            }

            public override void Shrink() {
                var target = _Items[_Items.Count - 1];
                _Items.RemoveAt(_Items.Count - 1);
                if (Owner.P == 0) {
                    Owner.N /= 2;
                    Owner.P = Owner.N - 1;
                } else {
                    Owner.P--;
                }
                for (int i = 0; i < target.Count; i++) {
                    int newBucket = Owner.Bucket(target[i].Key);
                    var dst = _Items[newBucket];
                    dst.Add(target[i]);
                }
            }
        }

        /// <summary>
        /// Describes a type of storage
        /// </summary>
        private abstract class Store : IDisposable {
            protected LinearHashTable<Tkey, Tvalue> Owner;

            public Store(LinearHashTable<Tkey, Tvalue> owner) {
                Owner = owner;
            }

            public abstract int BucketCount { get; set; }
            public abstract int Count { get; }

            public abstract void Flush();

            public abstract bool Get(int bucket, Tkey key, out Tvalue value);

            public abstract void Grow();

            public abstract bool Remove(int bucket, Tkey key, out Tvalue value);

            public abstract void Set(int bucket, Tkey key, Tvalue value);

            public abstract void Shrink();

            #region IDisposable Support

            protected bool disposedValue = false;

            public void Dispose() {
                Dispose(true);
            }

            protected virtual void Dispose(bool disposing) {
                if (!disposedValue) {
                    if (disposing) {
                        Flush();
                    }
                    disposedValue = true;
                }
            }

            #endregion IDisposable Support
        }

        #region IDisposable Support

        private bool disposedValue = false;

        public void Dispose() {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (_Store != null)
                        _Store.Dispose();
                }
                disposedValue = true;
            }
        }

        #endregion IDisposable Support
    }

    /// <summary>
    /// This will throw during LinearHashTable ctor if the index file and its backup
    /// are both corrupt (in theory this should only occur because of disk/hardware faults.)
    /// </summary>
    public class LinearHashTableCorruptionException : Exception { }
}
