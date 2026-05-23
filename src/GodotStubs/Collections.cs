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

// `where TKey : notnull` was an artefact of backing this with a real
// System.Collections.Generic.Dictionary in an earlier iteration. The
// constraint then leaked into every generated method that takes a
// Variant.As/CreateFrom<TKey, TValue> overload, since sts2's signatures
// don't carry it — yielding CS8714 across Generated/. Drop both: the
// stub stores nothing (headless never reads back) so the BCL backing
// was already dead weight.
public partial class Dictionary<TKey, TValue>
{
    public Dictionary() { }
    public bool ContainsKey(TKey key) => false;
    public bool TryGetValue(TKey key, out TValue value) { value = default!; return false; }
    public TValue this[TKey key] { set { } }
}

// from: Node.GetChildren(bool) → Godot.Collections.Array<Node>. SPINY_TOAD's
// Buff intent (Act 2 seed 42 floor 8) walks its own children to drive a
// damage-number VFX or similar. Headless has no scene tree; we return an
// empty enumerable wrapper. Implementing IEnumerable<T> lets foreach-style
// callers iterate over zero items without NREs.
public partial class Array<T> : System.Collections.Generic.IEnumerable<T>
{
    private readonly System.Collections.Generic.List<T> _inner = new();
    public Array() { }
    public int Count => _inner.Count;
    // Indexer with get+set so the generated `set_Item(Int32,!0)` requirement
    // (captured by GodotStubsCoverageTests as a MemberRef from sts2.dll's
    // SG-emitted code) lands on this declaration rather than colliding with
    // a generator-emitted partial-side accessor.
    public T this[int i] { get => _inner[i]; set => _inner[i] = value; }
    public System.Collections.Generic.IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    // from: CardSweep — SWEEPING_BEAM crashed on missing
    //   Godot.Collections.Array`1.Add(!0). The card's VFX path builds a
    //   strongly-typed Array<Node> of beam segments and appends them.
    //   Stub appends to the inner list so any consumer that re-enumerates
    //   sees the items; no headless path reads them back today.
    public void Add(T item) => _inner.Add(item);
}
