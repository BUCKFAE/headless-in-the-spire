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
