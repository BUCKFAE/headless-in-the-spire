using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Stub-surface guard. Every MemberReference sts2.dll holds against a
// `Godot.*` TypeRef must be matched by a TypeDef/Method/Field combination
// in our GodotStubs build output (AssemblyName=GodotSharp). A miss here is
// a future MissingMethodException waiting to surface at runtime — the
// failure mode that hard-locked seed 42's treasure-chest open
// (`Godot.Colors.get_Black()`) and the card-removal path before it.
//
// Metadata-only on both sides:
//   * sts2.dll is read via PEReader; never loaded into the runtime, so
//     AD-4 is preserved (the test project takes no code reference either).
//   * GodotSharp.dll is also read via PEReader (not Assembly.Load),
//     against the build output the test runner copies into bin/.
//
// Two facts:
//   * Narrow Color/Colors test is a fast canary on hand-written stubs in
//     Values.cs / CombatStubs.cs — these still drive specific bugs (the
//     Color.Black initialiser, etc.) and want a focused signal independent
//     of the broader generated surface.
//   * Broad test is mandatory: every Godot.* MemberRef in sts2.dll must
//     have a matching stub. Was Diagnostic-only when GodotStubs grew on
//     demand; now mandatory because `just build::regen-godot-stubs` generates the
//     full surface from sts2's MemberReference table into
//     src/GodotStubs/Generated/*.g.cs. A red broad test means the generated
//     output is out of date — re-run the recipe.
public class GodotStubsCoverageTests
{
    [Fact]
    public void Color_And_Colors_References_From_Sts2_Resolve_On_GodotStubs()
    {
        var missing = ComputeMissing(t => t == "Godot.Color" || t == "Godot.Colors");
        Assert.True(
            missing.Count == 0,
            $"GodotStubs is missing {missing.Count} Color/Colors member(s) referenced by sts2.dll — "
            + "add them to src/GodotStubs/Values.cs (Color) or src/GodotStubs/CombatStubs.cs (Colors).\n"
            + string.Join('\n', missing.Select(m => "  - " + m)));
    }

    [Fact]
    public void All_Godot_References_From_Sts2_Resolve_On_GodotStubs()
    {
        var missing = ComputeMissing(IsGodotType);
        Assert.True(
            missing.Count == 0,
            $"GodotStubs is missing {missing.Count} member(s) referenced by sts2.dll. "
            + "Run `just build::regen-godot-stubs` to refresh the generated stubs from sts2's MemberReference table.\n"
            + string.Join('\n', missing.Select(m => "  - " + m)));
    }

    // Complement to the MemberRef check above: TypeRefs sts2 holds without
    // any MemberRef (e.g. `typeof(Godot.Foo)`, `(Godot.Foo)x` casts, base-class
    // refs from sts2 subclasses) still need a TypeDef in the stub or the
    // JIT throws TypeLoadException at first reach. MemberRef coverage doesn't
    // catch them because the parent-peel-back never visits a TypeRef that
    // has zero members referenced.
    [Fact]
    public void All_Godot_TypeRefs_From_Sts2_Resolve_On_GodotStubs()
    {
        var (sts2Path, stubPath) = LocatePaths();
        var required = CollectGodotTypeRefs(sts2Path);
        Assert.True(required.Count > 0,
            "no Godot.* TypeReferences in sts2.dll — IsGodotType filter is broken, not the stub.");

        var available = CollectStubTypeDefs(stubPath);
        var missing = required
            .Where(r => !available.Contains(r))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"GodotStubs is missing {missing.Count} Godot.* type(s) referenced by sts2.dll — "
            + "would surface as TypeLoadException on first reach. Run `just build::regen-godot-stubs`.\n"
            + string.Join('\n', missing.Select(t => "  - " + t)));
    }

    private static (string Sts2Path, string StubPath) LocatePaths()
    {
        var repoRoot = LocateRepoRoot();
        var sts2Path = Path.Combine(repoRoot, "vendor", "sts2.dll");
        Assert.True(File.Exists(sts2Path),
            $"vendor/sts2.dll not present at {sts2Path} — run `just setup::setup` first.");
        var stubPath = Path.Combine(AppContext.BaseDirectory, "GodotSharp.dll");
        Assert.True(File.Exists(stubPath),
            $"GodotSharp.dll not in test bin at {stubPath} — GodotStubs build output missing.");
        return (sts2Path, stubPath);
    }

    private static HashSet<string> CollectGodotTypeRefs(string sts2Path)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        using var pe = new PEReader(File.OpenRead(sts2Path));
        var md = pe.GetMetadataReader();
        foreach (var handle in md.TypeReferences)
        {
            var fqn = QualifiedTypeName(md, handle);
            if (IsGodotType(fqn)) result.Add(fqn);
        }
        return result;
    }

    private static HashSet<string> CollectStubTypeDefs(string stubPath)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        using var pe = new PEReader(File.OpenRead(stubPath));
        var md = pe.GetMetadataReader();
        foreach (var handle in md.TypeDefinitions)
        {
            var fqn = QualifiedTypeName(md, handle);
            if (!string.IsNullOrEmpty(fqn) && fqn != "<Module>") result.Add(fqn);
        }
        return result;
    }

    private static List<string> ComputeMissing(Func<string, bool> typeFilter)
    {
        var repoRoot = LocateRepoRoot();
        var sts2Path = Path.Combine(repoRoot, "vendor", "sts2.dll");
        Assert.True(File.Exists(sts2Path),
            $"vendor/sts2.dll not present at {sts2Path} — run `just setup::setup` first.");

        var stubPath = Path.Combine(AppContext.BaseDirectory, "GodotSharp.dll");
        Assert.True(File.Exists(stubPath),
            $"GodotSharp.dll not in test bin at {stubPath} — GodotStubs build output missing.");

        var required = CollectGodotReferences(sts2Path, typeFilter);
        Assert.True(required.Count > 0,
            "no matching MemberReferences in sts2.dll — the test filter is broken, not the stub.");

        var available = CollectStubMembers(stubPath);

        return required
            .Where(r => !available.Contains(r))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }

    // Walk sts2.dll's MemberReference table; capture every member whose
    // parent TypeRef passes the supplied filter. Returns canonical
    // `Type.Member(sig)` strings — same shape as CollectStubMembers below
    // so a set diff is the whole audit.
    private static HashSet<string> CollectGodotReferences(string sts2Path, Func<string, bool> typeFilter)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        using var pe = new PEReader(File.OpenRead(sts2Path));
        var md = pe.GetMetadataReader();
        var provider = new SigToString();

        foreach (var handle in md.MemberReferences)
        {
            var mr = md.GetMemberReference(handle);

            // Parent may be a TypeRef directly or a TypeSpec (closed generic)
            // wrapping one. Peel back to the underlying TypeRef so e.g.
            // Array<T> members land on the open generic Array.
            TypeReferenceHandle? parentRef = mr.Parent.Kind switch
            {
                HandleKind.TypeReference => (TypeReferenceHandle)mr.Parent,
                HandleKind.TypeSpecification => ResolveTypeSpecToRef(md, (TypeSpecificationHandle)mr.Parent),
                _ => null,
            };
            if (parentRef is null) continue;

            var parentFqn = QualifiedTypeName(md, parentRef.Value);
            if (!typeFilter(parentFqn)) continue;

            var memberName = md.GetString(mr.Name);
            string key = mr.GetKind() switch
            {
                MemberReferenceKind.Method => MethodKey(parentFqn, memberName,
                    mr.DecodeMethodSignature(provider, genericContext: null)),
                MemberReferenceKind.Field => FieldKey(parentFqn, memberName,
                    mr.DecodeFieldSignature(provider, genericContext: null)),
                _ => throw new InvalidOperationException("unknown member ref kind"),
            };
            result.Add(key);
        }

        return result;
    }

    // Walk GodotSharp.dll's TypeDefinition table; emit `Type.Member(sig)`
    // strings for every method (including ctors and property accessors —
    // they're MethodDefs at the metadata layer) and every field.
    // Auto-property backing fields are filtered: sts2 never holds a
    // MemberRef against them.
    private static HashSet<string> CollectStubMembers(string stubPath)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        using var pe = new PEReader(File.OpenRead(stubPath));
        var md = pe.GetMetadataReader();
        var provider = new SigToString();

        foreach (var typeHandle in md.TypeDefinitions)
        {
            var td = md.GetTypeDefinition(typeHandle);
            var typeFqn = QualifiedTypeName(md, typeHandle);
            if (string.IsNullOrEmpty(typeFqn) || typeFqn == "<Module>") continue;

            foreach (var methodHandle in td.GetMethods())
            {
                var method = md.GetMethodDefinition(methodHandle);
                var name = md.GetString(method.Name);
                var sig = method.DecodeSignature(provider, genericContext: null);
                result.Add(MethodKey(typeFqn, name, sig));
            }

            foreach (var fieldHandle in td.GetFields())
            {
                var field = md.GetFieldDefinition(fieldHandle);
                var name = md.GetString(field.Name);
                if (name.Contains("k__BackingField", StringComparison.Ordinal)) continue;
                var fieldType = field.DecodeSignature(provider, genericContext: null);
                result.Add(FieldKey(typeFqn, name, fieldType));
            }
        }

        return result;
    }

    private static bool IsGodotType(string fqn)
        => fqn == "Godot"
        || fqn.StartsWith("Godot.", StringComparison.Ordinal)
        || fqn.StartsWith("Godot+", StringComparison.Ordinal);

    private static string MethodKey(string typeFqn, string name, MethodSignature<string> sig)
        => $"{typeFqn}.{name}({string.Join(",", sig.ParameterTypes)})";

    private static string FieldKey(string typeFqn, string name, string fieldType)
        => $"{typeFqn}.{name}:{fieldType}";

    private static string QualifiedTypeName(MetadataReader md, TypeReferenceHandle handle)
    {
        var tr = md.GetTypeReference(handle);
        var name = md.GetString(tr.Name);
        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            var parent = QualifiedTypeName(md, (TypeReferenceHandle)tr.ResolutionScope);
            return $"{parent}+{name}";
        }
        var ns = md.GetString(tr.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string QualifiedTypeName(MetadataReader md, TypeDefinitionHandle handle)
    {
        var td = md.GetTypeDefinition(handle);
        var name = md.GetString(td.Name);
        if (td.IsNested)
        {
            var declaring = QualifiedTypeName(md, td.GetDeclaringType());
            return $"{declaring}+{name}";
        }
        var ns = md.GetString(td.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static TypeReferenceHandle? ResolveTypeSpecToRef(MetadataReader md, TypeSpecificationHandle handle)
    {
        var recorder = new TypeRefRecorder();
        md.GetTypeSpecification(handle).DecodeSignature(recorder, genericContext: (object?)null);
        // If the open type was a TypeDef (sts2-local, e.g. compiler-synth
        // `<>z__ReadOnlyArray`1<Godot.Control>`), the recorder's First
        // captured the first type-arg by accident — the MemberRef actually
        // targets the sts2-internal type, not Godot.Control.  Drop these.
        return recorder.OpenTypeWasDefinition ? null : recorder.First;
    }

    private static string LocateRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "Sts2Headless.slnx"))) return dir;
            var p = Directory.GetParent(dir);
            if (p is null) break;
            dir = p.FullName;
        }
        throw new InvalidOperationException("repo root not found");
    }

    // Stringifies method/field signatures into the short, namespaceless
    // form used by both walks. Keeping them identical is the whole point —
    // `Single` vs `System.Single` would yield a false-positive gap report.
    private sealed class SigToString : ISignatureTypeProvider<string, object?>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle h, byte rawKind)
        {
            // Same-assembly nested types are TypeDef-encoded in parameter
            // signatures; recursing through GetDeclaringType keeps the key
            // shape symmetric with GetTypeFromReference's `Parent+Name`
            // form below — without the recurse, every nested-enum sig
            // (CanvasItem+ClipChildrenMode etc.) would key as just
            // "ClipChildrenMode" on the GodotSharp side and never match.
            var td = reader.GetTypeDefinition(h);
            var name = reader.GetString(td.Name);
            if (td.IsNested)
            {
                var declaring = GetTypeFromDefinition(reader, td.GetDeclaringType(), rawKind);
                return $"{declaring}+{name}";
            }
            return name;
        }
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle h, byte rawKind)
        {
            var tr = reader.GetTypeReference(h);
            var name = reader.GetString(tr.Name);
            if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                var parent = GetTypeFromReference(reader, (TypeReferenceHandle)tr.ResolutionScope, rawKind);
                return $"{parent}+{name}";
            }
            return name;
        }
        public string GetSZArrayType(string e) => $"{e}[]";
        public string GetArrayType(string e, ArrayShape s) => $"{e}[{new string(',', s.Rank - 1)}]";
        public string GetPointerType(string e) => $"{e}*";
        public string GetByReferenceType(string e) => $"ref {e}";
        public string GetGenericMethodParameter(object? c, int i) => $"!!{i}";
        public string GetGenericTypeParameter(object? c, int i) => $"!{i}";
        public string GetGenericInstantiation(string t, ImmutableArray<string> args)
            => $"{t}<{string.Join(",", args)}>";
        public string GetModifiedType(string m, string u, bool req) => u;
        public string GetPinnedType(string e) => e;
        public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k)
            => r.GetTypeSpecification(h).DecodeSignature(this, c);
        public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
    }

    // Captures the first underlying TypeRef of a TypeSpec — closed generics
    // like `Array<int>` resolve here so member-refs against them group
    // with the open Array's members. Same trick as ListMembersCommand.
    private sealed class TypeRefRecorder : ISignatureTypeProvider<int, object?>
    {
        public TypeReferenceHandle? First { get; private set; }
        // True iff a TypeDef was the *first* thing visited — i.e. the
        // generic instantiation's open type is sts2-local rather than a
        // Godot.* TypeRef.  Lets the caller drop bogus parent attributions.
        public bool OpenTypeWasDefinition { get; private set; }
        private bool _firstVisited;

        public int GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k)
        {
            First ??= h;
            _firstVisited = true;
            return 0;
        }
        public int GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k)
        {
            if (!_firstVisited) OpenTypeWasDefinition = true;
            _firstVisited = true;
            return 0;
        }
        public int GetGenericInstantiation(int t, ImmutableArray<int> args) => 0;
        public int GetPrimitiveType(PrimitiveTypeCode c) => 0;
        public int GetSZArrayType(int e) => 0;
        public int GetArrayType(int e, ArrayShape s) => 0;
        public int GetPointerType(int e) => 0;
        public int GetByReferenceType(int e) => 0;
        public int GetGenericMethodParameter(object? c, int i) => 0;
        public int GetGenericTypeParameter(object? c, int i) => 0;
        public int GetModifiedType(int m, int u, bool req) => 0;
        public int GetPinnedType(int e) => 0;
        public int GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) => 0;
        public int GetFunctionPointerType(MethodSignature<int> s) => 0;
    }
}
