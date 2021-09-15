/// A smol anagram solver.
/// You are free to reuse and modify this source.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Anagrams
{
    /// <summary>A class representing an immutable, stack-like, one-way sequence of
    /// <typeparamref name="T"/>s.
    ///</summary>
    public class Sequence<T> : IEnumerable<T>, IComparable<Sequence<T>>
    {
        public int Count { get; private set; }

        private class SequenceNode
        {
            public T value;
            public Sequence<T> next;
        }

        private SequenceNode first;
        public T Value {
            get { return first.value; }
        }
        public Sequence<T> Next {
            get { return first.next; }
        }

        private Sequence() { }
        public static readonly Sequence<T> Empty = new Sequence<T>();

        public Sequence<T> Push(T value)
        {
            return new Sequence<T> {
                Count = this.Count + 1,
                first = new SequenceNode { value = value, next = this }
            };
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (Sequence<T> s = this; s.first != null; s = s.Next) {
                yield return s.Value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
            => (this as IEnumerable<T>).GetEnumerator();

        public static Sequence<T> ToSequence(IEnumerable<T> e)
            => e.Aggregate(Empty, (s, t) => s.Push(t));

        public int CompareTo(Sequence<T> that)
            => Enumerable.Zip(this, that, Comparer<T>.Default.Compare)
                .Append(this.Count - that.Count).FirstOrDefault(x => x != 0);
    }

    /// <summary>A class representing a tree of
    /// <typeparamref name="V"/>s sorted by sequences of
    /// <typeparamref name="K"/>s.
    ///</summary>
    public class SortedTree<K, V>
    {
        public V Value { get; set; }
        private SortedDictionary<K, SortedTree<K, V>> children;

        public bool TryGetChild(K key, out SortedTree<K, V> childOrSelf)
        {
            if (children != null && children.TryGetValue(key, out childOrSelf)) {
                return true;
            } else {
                childOrSelf = this;
                return false;
            }
        }

        public SortedTree<K, V> GetDescendant(IEnumerable<K> keys)
        {
            SortedTree<K, V> t = this;
            foreach (var key in keys.SkipWhile(k => t.TryGetChild(k, out t))) {
                t.children ??= new SortedDictionary<K, SortedTree<K, V>>();
                t.children.Add(key, t = new SortedTree<K, V>());
            }
            return t;
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 1) {
                return;
            }

            Console.Write("Building index...");
            var dict = new SortedTree<char, List<string>>();
            foreach (string word in File.ReadLines(args[0])) {
                if (string.IsNullOrEmpty(word)) {
                    continue;
                }
                var node = dict.GetDescendant(word.OrderBy(c => c));
                node.Value ??= new List<string>();
                node.Value.Add(word);
            }
            var s = new AnagramSolver<char, List<string>>(dict);
            Console.Write("done\n");
            Console.Write("> ");

            s.maxNumValues = 5;

            string source;
            while ((source = Console.ReadLine()) != null) {
                foreach (var a in s.Anagram(source.Where(c => !char.IsWhiteSpace(c)))) {
                    Console.WriteLine(a.Aggregate("", (gs, g) => $@"{(g.Count == 1
                         ? g[0] : $"[{g.Aggregate(/**/(ws, w) => $@"{ws}, {w}")}]")} {gs}"));
                }
                Console.Write("> ");
            }
        }
    }

    /// <summary>A class representing an anagram solver for sequences of
    /// <typeparamref name="K"/>s, usually <see cref="char"/>s, mapped into matching
    /// <typeparamref name="V"/>s, usually <see cref="string"/>s, by means of a given
    /// dictionary.
    ///</summary>
    public class AnagramSolver<K, V>
        where K : notnull, IComparable<K>
        where V : class
    {
        private SortedTree<K, V> dict;

        /// <summary>Minimum "length" of each "word";
        /// defaults to <see langword="1"/>.
        ///</summary>
        public int minNumKeys = 1;
        /// <summary>Maximum number of "words";
        /// defaults to <see cref="int.MaxValue"/>.
        ///</summary>
        public int maxNumValues = int.MaxValue;

        public AnagramSolver(SortedTree<K, V> dict)
        {
            this.dict = dict;
        }

        private class SearchRecord
        {
            public SortedTree<K, V> node;
            public Sequence<K> src = Sequence<K>.Empty; // source keys
            public Sequence<K> dsc = Sequence<K>.Empty; // discarded keys
            public Sequence<K> cns = Sequence<K>.Empty; // consumed keys

            public SearchRecord(SortedTree<K, V> node)
            {
                this.node = node;
            }
        }

        /// <summary>Enumerates all the anagrams of the given source, containing at most
        /// <see cref="maxNumValues"/> "words" of <see cref="dict"/> of at least "length"
        /// <see cref="minNumKeys"/>.
        ///</summary>
        /// <remarks>The enumeration may be empty or infinite.</remarks>
        /// <param name="src">The given source.</param>
        public IEnumerable<IEnumerable<V>> Anagram(IEnumerable<K> src)
            => Anagram(src.OrderByDescending(k => k),
                Sequence<K>.Empty,
                Sequence<V>.Empty);

        private IEnumerable<IEnumerable<V>> Anagram(IEnumerable<K> src,
            Sequence<K> prev_k, // previous key-sequence
            Sequence<V> prev_v) // previous value-sequence
        {
            if (prev_v.Count >= maxNumValues) {
                yield break;
            }
            var stk = new Stack<SearchRecord>();
            stk.Push(new SearchRecord(dict));
            stk.Peek().src = Sequence<K>.ToSequence(src);

            while (stk.Count > 0) {
                var r = stk.Pop();
                if (r.src.Count > 0) {
                    SortedTree<K, V> next;
                    if (r.dsc.Count == 0 || r.dsc.Value.CompareTo(r.src.Value) < 0) {
                        if (r.node.TryGetChild(r.src.Value, out next)) {
                            // try consuming key
                            stk.Push(new SearchRecord(next));
                            stk.Peek().dsc = r.dsc;
                            stk.Peek().cns = r.cns.Push(r.src.Value);
                            stk.Peek().src = r.src.Next;
                        } else if (r.cns.Count == 0) {
                            yield break;
                        }
                    }
                    // try discarding key
                    stk.Push(r);
                    stk.Peek().dsc = r.dsc.Push(r.src.Value);
                    stk.Peek().src = r.src.Next;
                } else if (r.node.Value != null) {
                    var curr_v = prev_v.Push(r.node.Value);
                    var curr_k = Sequence<K>.ToSequence(r.cns);
                    if (curr_k.Count >= minNumKeys && curr_k.CompareTo(prev_k) >= 0) {
                        if (r.dsc.Count == 0) {
                            yield return curr_v; // last_v
                        } else {
                            // try consuming value
                            foreach (var last_v in Anagram(r.dsc, curr_k, curr_v)) {
                                yield return last_v;
                            }
                        }
                    }
                }
            }
        }
    }
}
