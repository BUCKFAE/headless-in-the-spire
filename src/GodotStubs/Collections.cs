// from: SaveManager.InitProfileId(0) — TypeLoadException then
//   MissingMethodException progression. Surface enumerated via
//   `just list-members 'Godot.Collections.Dictionary\`2'` (4 members).
//   Headless paths never observe the contents, so we back the dictionary
//   with a real in-memory map — TryGetValue returning a default makes
//   callers behave as if no entries exist.
//
// Lives in its own file because Stubs.cs uses a file-scoped `namespace
// Godot;` declaration, which forbids additional namespace blocks in the
// same file.

namespace Godot.Collections;

public class Dictionary<TKey, TValue> where TKey : notnull
{
    private readonly System.Collections.Generic.Dictionary<TKey, TValue> _inner = new();

    public Dictionary() { }
    public bool ContainsKey(TKey key) => _inner.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value!);
    public TValue this[TKey key] { set => _inner[key] = value; }
}

// from: Node.GetChildren(bool) → Godot.Collections.Array<Node>. SPINY_TOAD's
// Buff intent (Act 2 seed 42 floor 8) walks its own children to drive a
// damage-number VFX or similar. Headless has no scene tree; we return an
// empty enumerable wrapper. Implementing IEnumerable<T> lets foreach-style
// callers iterate over zero items without NREs.
public class Array<T> : System.Collections.Generic.IEnumerable<T>
{
    private readonly System.Collections.Generic.List<T> _inner = new();
    public Array() { }
    public int Count => _inner.Count;
    public T this[int i] => _inner[i];
    public System.Collections.Generic.IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    // from: CardSweep — SWEEPING_BEAM crashed on missing
    //   Godot.Collections.Array`1.Add(!0). The card's VFX path builds a
    //   strongly-typed Array<Node> of beam segments and appends them.
    //   Stub appends to the inner list so any consumer that re-enumerates
    //   sees the items; no headless path reads them back today.
    public void Add(T item) => _inner.Add(item);
}
