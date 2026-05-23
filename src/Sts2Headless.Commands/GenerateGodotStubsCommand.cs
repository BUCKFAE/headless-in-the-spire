using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Sts2Headless.Utils;

namespace Sts2Headless.Commands;

// Emits one .g.cs per Godot type with members sts2.dll references but the
// current GodotStubs build output doesn't yet declare. Static metadata only —
// no Mono.Cecil, no Assembly.Load — same idiom as
// GodotStubsCoverageTests / ListMembersCommand.
//
// The audit test's keying is the contract:
//   `Type.Member(paramTypes)` with primitives capitalised ("Int32"),
//   `Type.Field:Type` for fields, `ref T`/`T[]` byref+array prefixes,
//   `!!i`/`!i` for generic method/type params, nested types as `Parent+Name`.
// Two ISignatureTypeProviders below split that into a *key* form (matches
// the audit verbatim) and a *C# emit* form (compilable in a `namespace
// Godot;` block: `int`, `System.Action<T0, T1, T2>`, `ref Variant`).
//
// Output layout: one file per type at
// `<outputDir>/<Type.FQN.with.dots>.g.cs`. Nested types render as nested
// partials inside their parent's file. Files are gitignored via
// `src/GodotStubs/Generated/.gitignore` (committed); the directory itself
// is tracked.
public static class GenerateGodotStubsCommand
{
    public static int Run(string vendorDir, string repoRoot, string[] args)
    {
        var sts2Path = Paths.Sts2DllPath(vendorDir);
        if (!File.Exists(sts2Path))
        {
            Console.Error.WriteLine(Paths.Sts2DllMissingMessage);
            return 1;
        }

        var stubAssemblyPath = ArgValue(args, "--stub-assembly")
            ?? Path.Combine(repoRoot, "src", "GodotStubs", "bin", "Debug", "net10.0", "GodotSharp.dll");
        if (!File.Exists(stubAssemblyPath))
        {
            Console.Error.WriteLine($"generate-godot-stubs: GodotSharp.dll not built at {stubAssemblyPath}");
            Console.Error.WriteLine("  build GodotStubs first: dotnet build src/GodotStubs/GodotStubs.csproj");
            return 1;
        }

        var outputDir = ArgValue(args, "--out")
            ?? Path.Combine(repoRoot, "src", "GodotStubs", "Generated");
        Directory.CreateDirectory(outputDir);

        var required = CollectRequiredMembers(sts2Path);
        var existing = CollectExistingMembers(stubAssemblyPath);

        // Phase 1: bucket missing members by parent FQN.
        var missingByParent = new Dictionary<string, List<RequiredMember>>(StringComparer.Ordinal);
        foreach (var (key, member) in required)
        {
            if (existing.Keys.Contains(key)) continue;
            if (!missingByParent.TryGetValue(member.ParentFqn, out var list))
            {
                list = new List<RequiredMember>();
                missingByParent[member.ParentFqn] = list;
            }
            list.Add(member);
        }

        // Phase 2: also surface any Godot.* types that sts2 references as
        // parameter / return / field types but which our build output
        // doesn't declare anywhere. They wouldn't compile otherwise; an
        // empty shell is enough.
        var allReferencedGodotTypes = required.Select(p => p.Value.ParentFqn).ToHashSet(StringComparer.Ordinal);
        foreach (var member in required.Values)
        {
            foreach (var t in member.ReferencedGodotTypes) allReferencedGodotTypes.Add(t);
        }
        foreach (var fqn in allReferencedGodotTypes)
        {
            if (missingByParent.ContainsKey(fqn)) continue;
            if (existing.Types.Contains(fqn)) continue;
            // No members to add and no existing TypeDef — emit an empty shell.
            missingByParent[fqn] = new List<RequiredMember>();
        }

        // Phase 3: also account for nested types that show up only as
        // parameter types (e.g. `Variant+Type`). Their parent must declare
        // them; bucket them under the parent so the parent's emitter
        // adds the nested-enum shell.
        var nestedToAdd = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var fqn in allReferencedGodotTypes)
        {
            if (!fqn.Contains('+')) continue;
            if (existing.Types.Contains(fqn)) continue;
            var parent = fqn[..fqn.IndexOf('+')];
            var name = fqn[(fqn.IndexOf('+') + 1)..];
            if (!nestedToAdd.TryGetValue(parent, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                nestedToAdd[parent] = set;
            }
            set.Add(name);
            // Make sure the parent is in missingByParent so we emit a file
            // for it even if it has no other missing members.
            if (!missingByParent.ContainsKey(parent)) missingByParent[parent] = new List<RequiredMember>();
        }

        // Phase 4: emit one file per parent FQN. Skip nested-type parents —
        // they're rendered inside their declaring type's file.
        var fileCount = 0;
        var memberCount = 0;
        foreach (var (parentFqn, members) in missingByParent.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (parentFqn.Contains('+')) continue; // rendered inside its declaring type
            nestedToAdd.TryGetValue(parentFqn, out var nestedNames);
            // Plus any nested types missing for a transitively-referenced
            // child (e.g. `Variant+Type+Foo`). We don't yet support
            // multi-level nesting — leaf parents only.  Nested types that
            // showed up only as parameter sigs (empty member list) get
            // rendered as a single-value enum via `nestedTypeNamesOnly`
            // below; pruning them here avoids the "partial class : GodotObject"
            // fallback firing for what's almost certainly an enum.
            var nestedFiles = missingByParent
                .Where(kv => kv.Key.StartsWith(parentFqn + "+", StringComparison.Ordinal) && kv.Value.Count > 0)
                .Select(kv => kv)
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToList();
            // Carry the parameter-type-only nested names too.
            nestedNames ??= new HashSet<string>(StringComparer.Ordinal);
            foreach (var emptyNested in missingByParent
                .Where(kv => kv.Key.StartsWith(parentFqn + "+", StringComparison.Ordinal) && kv.Value.Count == 0)
                .Select(kv => kv.Key[(parentFqn.Length + 1)..]))
            {
                nestedNames.Add(emptyNested);
            }
            var code = EmitFile(parentFqn, members, nestedFiles, nestedNames.Count == 0 ? null : nestedNames, existing);
            if (code is null) continue;
            var outPath = Path.Combine(outputDir, parentFqn + ".g.cs");
            File.WriteAllText(outPath, code);
            fileCount++;
            memberCount += members.Count + nestedFiles.Sum(kv => kv.Value.Count);
        }

        Console.WriteLine($"generate-godot-stubs: wrote {fileCount} file(s), covered {memberCount} member(s).");
        Console.WriteLine($"  vendor:  {sts2Path}");
        Console.WriteLine($"  stub:    {stubAssemblyPath}");
        Console.WriteLine($"  output:  {outputDir}");
        return 0;
    }

    private static string? ArgValue(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag) return args[i + 1];
        }
        return null;
    }

    // ── Phase 1 input: sts2.dll's required member set ────────────────────

    private sealed record RequiredMember(
        string ParentFqn,
        string MemberName,
        MemberReferenceKind Kind,
        string Key,                  // matches audit's canonical key
        bool IsInstance,
        int GenericParameterCount,
        string? ReturnTypeCSharp,    // null for fields
        string? FieldTypeCSharp,     // non-null for fields
        IReadOnlyList<string> ParameterTypesCSharp,
        IReadOnlyList<string> ReferencedGodotTypes);

    // CSharpEmitProvider consults the genericContext for the names of the
    // parent type's generic params, so a method on Array<T>.Contains(!0)
    // decodes to a parameter of type "T", not "T0".  KeyProvider always
    // returns "!0" — the audit's keying form.
    private sealed record GenericContext(IReadOnlyList<string> TypeParameterNames);

    private static Dictionary<string, RequiredMember> CollectRequiredMembers(string sts2Path)
    {
        var result = new Dictionary<string, RequiredMember>(StringComparer.Ordinal);
        using var pe = new PEReader(File.OpenRead(sts2Path));
        var md = pe.GetMetadataReader();
        var keyProv = new KeyProvider();
        var csharpProv = new CSharpEmitProvider();

        foreach (var handle in md.MemberReferences)
        {
            var mr = md.GetMemberReference(handle);
            TypeReferenceHandle? parentRef = mr.Parent.Kind switch
            {
                HandleKind.TypeReference => (TypeReferenceHandle)mr.Parent,
                HandleKind.TypeSpecification => ResolveTypeSpecToRef(md, (TypeSpecificationHandle)mr.Parent),
                _ => null,
            };
            if (parentRef is null) continue;

            var parentFqn = QualifiedTypeName(md, parentRef.Value);
            if (!IsGodotType(parentFqn)) continue;

            // Provide a generic-type-param-name context so the CSharp emit
            // uses the same names the existing partial declares (e.g. "T"
            // on Array<T>).  Falls back to "T", "T1", "T2", … when the
            // declaring type isn't in the existing TypeDef table yet.
            var (_, parentArity) = ParseGenericArity(parentFqn[(parentFqn.LastIndexOf('.') + 1)..].Split('+')[^1]);
            var typeParamNames = parentArity > 0
                ? (LookupGenericParameterNames(parentFqn) ??
                    (IReadOnlyList<string>)(parentArity == 1 ? new[] { "T" } : Enumerable.Range(0, parentArity).Select(i => "T" + i).ToArray()))
                : Array.Empty<string>();
            var csharpCtx = new GenericContext(typeParamNames);

            var memberName = md.GetString(mr.Name);
            switch (mr.GetKind())
            {
                case MemberReferenceKind.Method:
                    {
                        var keySig = mr.DecodeMethodSignature(keyProv, genericContext: (object?)null);
                        var csharpSig = mr.DecodeMethodSignature(csharpProv, genericContext: (object?)csharpCtx);
                        var key = MethodKey(parentFqn, memberName, keySig);
                        var refs = csharpProv.DrainReferencedGodotTypes();
                        var member = new RequiredMember(
                            ParentFqn: parentFqn,
                            MemberName: memberName,
                            Kind: MemberReferenceKind.Method,
                            Key: key,
                            IsInstance: keySig.Header.IsInstance,
                            GenericParameterCount: keySig.GenericParameterCount,
                            ReturnTypeCSharp: csharpSig.ReturnType,
                            FieldTypeCSharp: null,
                            ParameterTypesCSharp: csharpSig.ParameterTypes,
                            ReferencedGodotTypes: refs);
                        result[key] = member;
                        break;
                    }
                case MemberReferenceKind.Field:
                    {
                        var keyType = mr.DecodeFieldSignature(keyProv, genericContext: (object?)null);
                        var csharpType = mr.DecodeFieldSignature(csharpProv, genericContext: (object?)csharpCtx);
                        var key = FieldKey(parentFqn, memberName, keyType);
                        var refs = csharpProv.DrainReferencedGodotTypes();
                        var member = new RequiredMember(
                            ParentFqn: parentFqn,
                            MemberName: memberName,
                            Kind: MemberReferenceKind.Field,
                            Key: key,
                            IsInstance: true,
                            GenericParameterCount: 0,
                            ReturnTypeCSharp: null,
                            FieldTypeCSharp: csharpType,
                            ParameterTypesCSharp: Array.Empty<string>(),
                            ReferencedGodotTypes: refs);
                        result[key] = member;
                        break;
                    }
            }
        }
        return result;
    }

    // ── Phase 2 input: GodotStubs.dll's existing member + type set ────────

    private sealed record ExistingSet(HashSet<string> Keys, HashSet<string> Types);

    private static ExistingSet CollectExistingMembers(string stubAssemblyPath)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var types = new HashSet<string>(StringComparer.Ordinal);
        using var pe = new PEReader(File.OpenRead(stubAssemblyPath));
        var md = pe.GetMetadataReader();
        var prov = new KeyProvider();

        foreach (var typeHandle in md.TypeDefinitions)
        {
            var td = md.GetTypeDefinition(typeHandle);
            var typeFqn = QualifiedTypeName(md, typeHandle);
            if (string.IsNullOrEmpty(typeFqn) || typeFqn == "<Module>") continue;
            types.Add(typeFqn);

            foreach (var methodHandle in td.GetMethods())
            {
                var method = md.GetMethodDefinition(methodHandle);
                var name = md.GetString(method.Name);
                var sig = method.DecodeSignature(prov, genericContext: (object?)null);
                keys.Add(MethodKey(typeFqn, name, sig));
            }
            foreach (var fieldHandle in td.GetFields())
            {
                var field = md.GetFieldDefinition(fieldHandle);
                var name = md.GetString(field.Name);
                if (name.Contains("k__BackingField", StringComparison.Ordinal)) continue;
                var fieldType = field.DecodeSignature(prov, genericContext: (object?)null);
                keys.Add(FieldKey(typeFqn, name, fieldType));
            }
        }
        return new ExistingSet(keys, types);
    }

    private static string MethodKey(string typeFqn, string name, MethodSignature<string> sig)
        => $"{typeFqn}.{name}({string.Join(",", sig.ParameterTypes)})";

    private static string FieldKey(string typeFqn, string name, string fieldType)
        => $"{typeFqn}.{name}:{fieldType}";

    private static bool IsGodotType(string fqn)
        => fqn == "Godot"
        || fqn.StartsWith("Godot.", StringComparison.Ordinal)
        || fqn.StartsWith("Godot+", StringComparison.Ordinal);

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
        // See GodotStubsCoverageTests for the rationale: if the open type was
        // a TypeDef (compiler-synth or sts2-local), the recorded First is
        // actually a type-arg, not the member's real declarer.
        return recorder.OpenTypeWasDefinition ? null : recorder.First;
    }

    // ── Signature providers ──────────────────────────────────────────────

    // Mirrors GodotStubsCoverageTests.SigToString exactly — used so missing
    // and existing keys share the same canonical shape.
    private sealed class KeyProvider : ISignatureTypeProvider<string, object?>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle h, byte rawKind)
        {
            // Recurse through nested-type declaring chain so a TypeDef
            // reference to e.g. CanvasItem.ClipChildrenMode keys as
            // "CanvasItem+ClipChildrenMode" — matching the TypeRef-side
            // form the audit emits for sts2.dll's references.
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

    // Emits C#-source-ready type names. Returned strings drop into a `namespace
    // Godot;` block: primitives → keywords, BCL generics → System.Collections.
    // Generic.<…>, generic params → T0/T1/…, Godot types → simple name (we own
    // the namespace).  Tracks every Godot.* type it emits so the caller can
    // verify all parameter/return types are declared somewhere.
    private sealed class CSharpEmitProvider : ISignatureTypeProvider<string, object?>
    {
        private readonly HashSet<string> _referencedGodotTypes = new(StringComparer.Ordinal);

        public IReadOnlyList<string> DrainReferencedGodotTypes()
        {
            var list = _referencedGodotTypes.ToList();
            _referencedGodotTypes.Clear();
            return list;
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Void => "void",
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.SByte => "sbyte",
            PrimitiveTypeCode.Byte => "byte",
            PrimitiveTypeCode.Int16 => "short",
            PrimitiveTypeCode.UInt16 => "ushort",
            PrimitiveTypeCode.Int32 => "int",
            PrimitiveTypeCode.UInt32 => "uint",
            PrimitiveTypeCode.Int64 => "long",
            PrimitiveTypeCode.UInt64 => "ulong",
            PrimitiveTypeCode.Single => "float",
            PrimitiveTypeCode.Double => "double",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.Object => "object",
            PrimitiveTypeCode.IntPtr => "nint",
            PrimitiveTypeCode.UIntPtr => "nuint",
            PrimitiveTypeCode.TypedReference => "System.TypedReference",
            _ => typeCode.ToString(),
        };

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle h, byte rawKind)
        {
            var td = reader.GetTypeDefinition(h);
            var name = reader.GetString(td.Name);
            if (td.IsNested)
            {
                var declaring = GetTypeFromDefinition(reader, td.GetDeclaringType(), rawKind);
                return $"{declaring}.{name}";
            }
            return name;
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle h, byte rawKind)
        {
            // Also record the type so the caller knows it has to exist
            // somewhere in the build output.  Uses the FQN with '+' for
            // nested types (matches the key shape the audit uses).
            RecordIfGodot(reader, h);

            var tr = reader.GetTypeReference(h);
            var name = reader.GetString(tr.Name);
            if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                var parent = GetTypeFromReference(reader, (TypeReferenceHandle)tr.ResolutionScope, rawKind);
                // Strip arity suffix for cleaner C# (we won't ever emit a
                // backticked name directly — open generics are constructed
                // through GetGenericInstantiation below).
                var bare = StripArity(name);
                return $"{parent}.{bare}";
            }
            var ns = reader.GetString(tr.Namespace);
            var bareName = StripArity(name);
            if (string.IsNullOrEmpty(ns)) return bareName;
            // Top-level Godot types: bare name.
            if (ns == "Godot") return bareName;
            // Sub-namespaced Godot types: emit using the sub-namespace prefix
            // (Collections.Array, NativeInterop.godot_variant, …) so they
            // disambiguate from same-named System types.  Works from any
            // file in the Godot.* tree thanks to namespace resolution rules.
            if (ns.StartsWith("Godot.", StringComparison.Ordinal))
            {
                var sub = ns["Godot.".Length..];
                return $"{sub}.{bareName}";
            }
            // BCL types — emit fully-qualified to avoid namespace import surprises.
            return $"{ns}.{bareName}";
        }

        private void RecordIfGodot(MetadataReader reader, TypeReferenceHandle h)
        {
            // Build the FQN with '+' separators for nested types.
            var tr = reader.GetTypeReference(h);
            var name = reader.GetString(tr.Name);
            if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                // Compute parent FQN by walking up.
                var parentFqn = QualifiedTypeNameFromRef(reader, (TypeReferenceHandle)tr.ResolutionScope);
                var fqn = $"{parentFqn}+{name}";
                if (fqn.StartsWith("Godot", StringComparison.Ordinal))
                    _referencedGodotTypes.Add(fqn);
                return;
            }
            var ns = reader.GetString(tr.Namespace);
            if (string.IsNullOrEmpty(ns)) return;
            if (ns == "Godot" || ns.StartsWith("Godot.", StringComparison.Ordinal))
                _referencedGodotTypes.Add($"{ns}.{name}");
        }

        private static string QualifiedTypeNameFromRef(MetadataReader reader, TypeReferenceHandle h)
        {
            var tr = reader.GetTypeReference(h);
            var name = reader.GetString(tr.Name);
            if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                var parent = QualifiedTypeNameFromRef(reader, (TypeReferenceHandle)tr.ResolutionScope);
                return $"{parent}+{name}";
            }
            var ns = reader.GetString(tr.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        public string GetSZArrayType(string e) => $"{e}[]";
        public string GetArrayType(string e, ArrayShape s) => $"{e}[{new string(',', s.Rank - 1)}]";
        public string GetPointerType(string e) => $"{e}*";
        public string GetByReferenceType(string e) => $"ref {e}";
        public string GetGenericMethodParameter(object? c, int i) => $"T{i}";
        public string GetGenericTypeParameter(object? c, int i)
        {
            if (c is GenericContext ctx && i < ctx.TypeParameterNames.Count)
                return ctx.TypeParameterNames[i];
            return $"T{i}";
        }
        public string GetGenericInstantiation(string t, ImmutableArray<string> args)
        {
            // Some BCL types: Nullable<X> → X?.
            if (t.EndsWith(".Nullable", StringComparison.Ordinal) || t == "Nullable")
            {
                return $"{args[0]}?";
            }
            return $"{t}<{string.Join(", ", args)}>";
        }
        public string GetModifiedType(string m, string u, bool req) => u;
        public string GetPinnedType(string e) => e;
        public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k)
            => r.GetTypeSpecification(h).DecodeSignature(this, c);
        public string GetFunctionPointerType(MethodSignature<string> s) => "object";

        private static string StripArity(string name)
        {
            var i = name.IndexOf('`');
            return i < 0 ? name : name[..i];
        }
    }

    private sealed class TypeRefRecorder : ISignatureTypeProvider<int, object?>
    {
        public TypeReferenceHandle? First { get; private set; }
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

    // ── Emission ─────────────────────────────────────────────────────────

    // Returns null if there's nothing to emit (no members + no nested types).
    private static string? EmitFile(
        string parentFqn,
        List<RequiredMember> members,
        List<KeyValuePair<string, List<RequiredMember>>> nestedFiles,
        HashSet<string>? nestedTypeNamesOnly,
        ExistingSet existing)
    {
        var (ns, typeNamePath) = SplitFqnIntoNamespaceAndType(parentFqn);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by `just regen-godot-stubs` from sts2.dll's MemberReference table.");
        sb.AppendLine("// DO NOT EDIT BY HAND. Hand-curated stubs live in the non-Generated/ files.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        // No usings: Godot.* sub-namespace types render as `<Sub>.<Name>`
        // (Collections.Array, NativeInterop.godot_variant, …) — see
        // CSharpEmitProvider.GetTypeFromReference.  Reachable from any
        // file in the Godot.* tree without needing using-directive plumbing.
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();

        // typeNamePath = ["Variant"] or ["Foo"] — at this layer we only ever
        // emit one root partial. Nested members are emitted inside it.
        var rootName = typeNamePath[0];
        var rootFqn = ns + "." + rootName;
        var (modifiers, baseDecl) = TypeDeclaration(rootFqn, members, existing);

        // Generic-arity backticks get expanded to a <T0, T1, …> declaration
        // and stripped from the source name; the FQN keeps the backtick so
        // closed-generic ImplOf lookups still match the audit's key shape.
        var (sourceName, genericParamCount) = ParseGenericArity(rootName);
        var genericDecl = BuildGenericParamList(rootFqn, genericParamCount, existing);

        sb.AppendLine($"{modifiers} {sourceName}{genericDecl}{baseDecl}");
        sb.AppendLine("{");
        var ctx = new EmitCtx(IsStatic: IsStaticClass(modifiers), IsReadOnlyStruct: IsReadOnlyStruct(modifiers));
        EmitMembersAndNested(sb, rootFqn, members, nestedFiles, nestedTypeNamesOnly, existing, ctx, indent: 1);
        sb.AppendLine("}");
        return sb.ToString();
    }

    private sealed record EmitCtx(bool IsStatic, bool IsReadOnlyStruct);

    private static (string name, int arity) ParseGenericArity(string name)
    {
        var i = name.IndexOf('`');
        if (i < 0) return (name, 0);
        var arity = int.TryParse(name[(i + 1)..], out var n) ? n : 0;
        return (name[..i], arity);
    }

    // For existing generic types, mirror the existing generic-param names so
    // partials line up.  New generic types get the T-with-index convention.
    private static string BuildGenericParamList(string typeFqn, int arity, ExistingSet existing)
    {
        if (arity == 0) return "";
        if (existing.Types.Contains(typeFqn))
        {
            var names = LookupGenericParameterNames(typeFqn);
            if (names is not null && names.Count == arity)
                return "<" + string.Join(", ", names) + ">";
        }
        // arity=1 → just T; otherwise T0, T1, …
        if (arity == 1) return "<T>";
        return "<" + string.Join(", ", Enumerable.Range(0, arity).Select(i => "T" + i)) + ">";
    }

    private static Dictionary<string, IReadOnlyList<string>>? _cachedGenericParams;

    private static IReadOnlyList<string>? LookupGenericParameterNames(string typeFqn)
    {
        _cachedGenericParams ??= LoadGenericParams(_cachedStubAssemblyPath);
        return _cachedGenericParams.TryGetValue(typeFqn, out var v) ? v : null;
    }

    private static Dictionary<string, IReadOnlyList<string>> LoadGenericParams(string stubAssemblyPath)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(stubAssemblyPath) || !File.Exists(stubAssemblyPath)) return result;
        using var pe = new PEReader(File.OpenRead(stubAssemblyPath));
        var md = pe.GetMetadataReader();
        foreach (var typeHandle in md.TypeDefinitions)
        {
            var td = md.GetTypeDefinition(typeHandle);
            var fqn = QualifiedTypeName(md, typeHandle);
            var gps = td.GetGenericParameters();
            if (gps.Count == 0) continue;
            var names = new List<string>(gps.Count);
            foreach (var gpHandle in gps)
            {
                var gp = md.GetGenericParameter(gpHandle);
                names.Add(md.GetString(gp.Name));
            }
            result[fqn] = names;
        }
        return result;
    }

    private static (string ns, string[] typePath) SplitFqnIntoNamespaceAndType(string fqn)
    {
        // Namespace is everything before the last '.'; the type path splits
        // on '+' to allow nesting.
        var i = fqn.LastIndexOf('.');
        var ns = i < 0 ? "" : fqn[..i];
        var typeStr = i < 0 ? fqn : fqn[(i + 1)..];
        return (ns, typeStr.Split('+'));
    }

    // Heuristics for new types (no existing TypeDef in GodotSharp.dll):
    //   Godot.NativeInterop.godot_* → partial struct
    //   Godot.NativeInterop.NativeVariantPtrArgs / *Args → ref partial struct
    //   Godot.NativeInterop.* (else) → partial class
    //   Godot.Bridge.* → partial class
    //   Otherwise → partial class : GodotObject
    private static (string modifiers, string baseDecl) TypeDeclaration(
        string typeFqn, List<RequiredMember> members, ExistingSet existing)
    {
        if (existing.Types.Contains(typeFqn))
        {
            // Existing type — find its kind so we emit the matching partial
            // modifiers. We're reading the same DLL twice; for simplicity
            // re-derive via attribute flags.
            return (MatchExistingTypeKind(typeFqn) ?? "public partial class", "");
        }

        // New type. Apply spec heuristics.
        if (typeFqn.StartsWith("Godot.NativeInterop.godot_", StringComparison.Ordinal))
            return ("public partial struct", "");
        if (typeFqn.StartsWith("Godot.NativeInterop.", StringComparison.Ordinal))
        {
            var leaf = typeFqn[(typeFqn.LastIndexOf('.') + 1)..];
            if (leaf.EndsWith("Args", StringComparison.Ordinal))
                return ("public ref partial struct", "");
            return ("public partial class", "");
        }
        if (typeFqn.StartsWith("Godot.Bridge.", StringComparison.Ordinal))
            return ("public partial class", "");
        // Reasonable fallback for the handful of remaining unknowns.
        return ("public partial class", " : GodotObject");
    }

    // ExistingSet doesn't hold the kind today; we open the DLL once at the
    // call site, then look up here. The (heavy-handed) compromise: re-open
    // GodotSharp.dll lazily on first lookup and cache the result for the
    // process lifetime.
    private static Dictionary<string, string>? _cachedTypeKinds;
    private static string _cachedStubAssemblyPath = "";

    private static string? MatchExistingTypeKind(string typeFqn)
    {
        _cachedTypeKinds ??= LoadTypeKinds(_cachedStubAssemblyPath);
        return _cachedTypeKinds.TryGetValue(typeFqn, out var kind) ? kind : null;
    }

    public static void SetCachedStubAssemblyPath(string path)
    {
        _cachedStubAssemblyPath = path;
        _cachedTypeKinds = null;
    }

    private static Dictionary<string, string> LoadTypeKinds(string stubAssemblyPath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(stubAssemblyPath) || !File.Exists(stubAssemblyPath)) return result;
        using var pe = new PEReader(File.OpenRead(stubAssemblyPath));
        var md = pe.GetMetadataReader();
        foreach (var typeHandle in md.TypeDefinitions)
        {
            var td = md.GetTypeDefinition(typeHandle);
            var fqn = QualifiedTypeName(md, typeHandle);
            if (string.IsNullOrEmpty(fqn) || fqn == "<Module>") continue;
            result[fqn] = ClassifyType(md, td);
        }
        return result;
    }

    private static string ClassifyType(MetadataReader md, TypeDefinition td)
    {
        var attr = td.Attributes;
        // Interfaces.
        if ((attr & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface)
            return "public partial interface";

        // Determine base type FQN for enum/struct detection.
        var baseFqn = ResolveBaseTypeName(md, td);
        if (baseFqn == "System.Enum")
        {
            // Enums are NOT partial (and they're rendered as members, not
            // their own partials). For top-level enums we still emit them
            // as enums; signal that via this special token.
            return "public enum";
        }
        if (baseFqn == "System.ValueType")
        {
            var isReadOnly = HasIsReadOnlyAttribute(md, td);
            var isRefLike = HasIsByRefLikeAttribute(md, td);
            var prefix = "public ";
            if (isRefLike) prefix += "ref ";
            if (isReadOnly) prefix += "readonly ";
            prefix += "partial struct";
            return prefix;
        }
        // Static class: sealed + abstract.  Keep the static marker so the
        // emitter knows not to declare instance members on it.
        if ((attr & (TypeAttributes.Sealed | TypeAttributes.Abstract)) == (TypeAttributes.Sealed | TypeAttributes.Abstract))
            return "public static partial class";
        // Sealed class.
        if ((attr & TypeAttributes.Sealed) != 0)
            return "public sealed partial class";
        return "public partial class";
    }

    private static bool IsStaticClass(string modifiers) => modifiers.Contains(" static ");
    private static bool IsReadOnlyStruct(string modifiers) => modifiers.Contains(" readonly ");

    private static string? ResolveBaseTypeName(MetadataReader md, TypeDefinition td)
    {
        if (td.BaseType.IsNil) return null;
        return td.BaseType.Kind switch
        {
            HandleKind.TypeReference => QualifiedTypeName(md, (TypeReferenceHandle)td.BaseType).Replace('+', '.'),
            HandleKind.TypeDefinition => QualifiedTypeName(md, (TypeDefinitionHandle)td.BaseType).Replace('+', '.'),
            _ => null,
        };
    }

    private static bool HasIsReadOnlyAttribute(MetadataReader md, TypeDefinition td)
        => HasAttribute(md, td, "System.Runtime.CompilerServices", "IsReadOnlyAttribute");

    private static bool HasIsByRefLikeAttribute(MetadataReader md, TypeDefinition td)
        => HasAttribute(md, td, "System.Runtime.CompilerServices", "IsByRefLikeAttribute");

    private static bool HasAttribute(MetadataReader md, TypeDefinition td, string ns, string name)
    {
        foreach (var caHandle in td.GetCustomAttributes())
        {
            var ca = md.GetCustomAttribute(caHandle);
            var ctor = ca.Constructor;
            string? attrTypeName = null;
            string? attrTypeNs = null;
            if (ctor.Kind == HandleKind.MemberReference)
            {
                var mref = md.GetMemberReference((MemberReferenceHandle)ctor);
                if (mref.Parent.Kind == HandleKind.TypeReference)
                {
                    var tr = md.GetTypeReference((TypeReferenceHandle)mref.Parent);
                    attrTypeName = md.GetString(tr.Name);
                    attrTypeNs = md.GetString(tr.Namespace);
                }
            }
            else if (ctor.Kind == HandleKind.MethodDefinition)
            {
                var mdef = md.GetMethodDefinition((MethodDefinitionHandle)ctor);
                var declTd = md.GetTypeDefinition(mdef.GetDeclaringType());
                attrTypeName = md.GetString(declTd.Name);
                attrTypeNs = md.GetString(declTd.Namespace);
            }
            if (attrTypeName == name && attrTypeNs == ns) return true;
        }
        return false;
    }

    // ── Member emission ──────────────────────────────────────────────────

    private static void EmitMembersAndNested(
        StringBuilder sb,
        string parentFqn,
        List<RequiredMember> members,
        List<KeyValuePair<string, List<RequiredMember>>> nestedFiles,
        HashSet<string>? nestedTypeNamesOnly,
        ExistingSet existing,
        EmitCtx ctx,
        int indent)
    {
        // 1) Nested types referenced only as parameter sigs (e.g. Variant+Type).
        if (nestedTypeNamesOnly is { Count: > 0 })
        {
            foreach (var nestedName in nestedTypeNamesOnly.OrderBy(s => s, StringComparer.Ordinal))
            {
                // If this nested name also appears as a parentFqn in nestedFiles,
                // it'll be emitted there with its own members — skip here.
                if (nestedFiles.Any(kv => kv.Key.EndsWith("+" + nestedName, StringComparison.Ordinal))) continue;
                // Default to a single-value enum — sts2's IL never reads the
                // enum's variants by name, only by integer value, so a stub
                // with a single `Default = 0` member matches every cast site.
                Indent(sb, indent);
                sb.AppendLine($"public enum {nestedName} {{ Default = 0 }}");
            }
        }

        // 2) Group members by name root to pair property accessors.
        var byBaseName = members
            .Where(m => m.Kind == MemberReferenceKind.Method)
            .GroupBy(m =>
                m.MemberName.StartsWith("get_", StringComparison.Ordinal) ? ("prop", m.MemberName[4..]) :
                m.MemberName.StartsWith("set_", StringComparison.Ordinal) ? ("prop", m.MemberName[4..]) :
                m.MemberName.StartsWith("add_", StringComparison.Ordinal) ? ("event", m.MemberName[4..]) :
                m.MemberName.StartsWith("remove_", StringComparison.Ordinal) ? ("event", m.MemberName[7..]) :
                ("plain", m.MemberName))
            .ToList();

        var emittedKeys = new HashSet<string>(StringComparer.Ordinal);

        // Pre-pass: pair up operators so == always has a matching != etc.
        var operatorPairs = ComputeOperatorPairs(members);

        foreach (var grp in byBaseName.OrderBy(g => g.Key.Item2, StringComparer.Ordinal))
        {
            var (kind, root) = grp.Key;
            switch (kind)
            {
                case "prop":
                    EmitProperty(sb, root, grp.ToList(), ctx, indent, emittedKeys, existing);
                    break;
                case "event":
                    EmitEvent(sb, root, grp.ToList(), indent, emittedKeys);
                    break;
                case "plain":
                    foreach (var m in grp) EmitMethod(sb, parentFqn, m, ctx, indent, emittedKeys);
                    break;
            }
        }

        // Synthesize missing operator pair partners.
        var anyEqualityDefined = false;
        foreach (var (existingOp, partnerName) in operatorPairs)
        {
            var member = members.FirstOrDefault(m => m.MemberName == existingOp);
            if (member is null || !emittedKeys.Contains(member.Key)) continue;
            if (existingOp == "op_Equality" || existingOp == "op_Inequality") anyEqualityDefined = true;
            // Check if partner is already in members.
            if (members.Any(m => m.MemberName == partnerName)) continue;
            // Synthesize the partner with same sig as existingOp.
            TryGetOperator(partnerName, out var sym, out var opKind);
            if (sym is null) continue;
            var ret = member.ReturnTypeCSharp ?? "void";
            var paramList = BuildParameterList(member.ParameterTypesCSharp);
            Indent(sb, indent);
            sb.Append($"public static {ret} operator {sym}({paramList})");
            AppendBody(sb, ret);
        }

        // CS0660/CS0661 silence: any type that defines == or != also needs
        // Equals(object?) and GetHashCode().  Only emit if not already
        // present in the existing partial side.
        if (anyEqualityDefined)
        {
            var anchor = members.First(m => m.MemberName == "op_Equality" || m.MemberName == "op_Inequality");
            var equalsAlready = existing.Keys.Any(k => k.StartsWith($"{anchor.ParentFqn}.Equals(", StringComparison.Ordinal));
            var hashAlready = existing.Keys.Any(k => k.StartsWith($"{anchor.ParentFqn}.GetHashCode(", StringComparison.Ordinal));
            if (!equalsAlready)
            {
                Indent(sb, indent);
                sb.AppendLine("public override bool Equals(object? obj) => false;");
            }
            if (!hashAlready)
            {
                Indent(sb, indent);
                sb.AppendLine("public override int GetHashCode() => 0;");
            }
        }

        // 3) Fields.
        foreach (var m in members.Where(m => m.Kind == MemberReferenceKind.Field).OrderBy(m => m.MemberName, StringComparer.Ordinal))
        {
            EmitField(sb, m, ctx, indent);
        }

        // 4) Nested types that have their own members.
        foreach (var (nestedFqn, nestedMembers) in nestedFiles)
        {
            var nestedName = nestedFqn[(nestedFqn.LastIndexOf('+') + 1)..];
            // Heuristic: if a nested type's only public members are a
            // `.ctor(Object, IntPtr)` (the Delegate ctor shape), declare it
            // as a delegate so events typed by it bind correctly.
            if (IsDelegateShape(nestedMembers, out var invokeSig))
            {
                Indent(sb, indent);
                sb.AppendLine($"public delegate void {nestedName}({invokeSig});");
                continue;
            }
            var (modifiers, baseDecl) = TypeDeclaration(nestedFqn, nestedMembers, existing);
            // Nested partial types repeat the modifier set; nested-type
            // accessibility defaults to private in C#, but we want public.
            Indent(sb, indent);
            sb.AppendLine($"{modifiers} {nestedName}{baseDecl}");
            Indent(sb, indent);
            sb.AppendLine("{");
            var nestedCtx = new EmitCtx(IsStatic: IsStaticClass(modifiers), IsReadOnlyStruct: IsReadOnlyStruct(modifiers));
            EmitMembersAndNested(sb, nestedFqn, nestedMembers, new(), null, existing, nestedCtx, indent + 1);
            Indent(sb, indent);
            sb.AppendLine("}");
        }
    }

    // Returns a list of (existingOperator, missingPartnerName) for required
    // operator pairs (==/!= and <=/>= and <=>/etc).
    private static List<(string, string)> ComputeOperatorPairs(List<RequiredMember> members)
    {
        var presentNames = members.Where(m => m.Kind == MemberReferenceKind.Method)
            .Select(m => m.MemberName).ToHashSet(StringComparer.Ordinal);
        var pairs = new List<(string, string)>();
        var partnerMap = new (string, string)[]
        {
            ("op_Equality", "op_Inequality"),
            ("op_Inequality", "op_Equality"),
            ("op_LessThan", "op_GreaterThan"),
            ("op_GreaterThan", "op_LessThan"),
            ("op_LessThanOrEqual", "op_GreaterThanOrEqual"),
            ("op_GreaterThanOrEqual", "op_LessThanOrEqual"),
            ("op_True", "op_False"),
            ("op_False", "op_True"),
        };
        foreach (var (a, b) in partnerMap)
        {
            if (presentNames.Contains(a) && !presentNames.Contains(b))
                pairs.Add((a, b));
        }
        return pairs;
    }

    private static bool IsDelegateShape(List<RequiredMember> members, out string invokeSig)
    {
        invokeSig = "";
        if (members.Count == 0) return false;
        var ctors = members.Where(m => m.MemberName == ".ctor").ToList();
        if (ctors.Count != 1) return false;
        var ctor = ctors[0];
        // Multicast delegate signature: .ctor(Object, IntPtr).
        if (ctor.ParameterTypesCSharp.Count == 2
            && ctor.ParameterTypesCSharp[0] == "object"
            && (ctor.ParameterTypesCSharp[1] == "nint" || ctor.ParameterTypesCSharp[1] == "System.IntPtr"))
        {
            // No invoke sig in metadata-only view; default to void().
            invokeSig = "";
            return true;
        }
        return false;
    }

    private static void Indent(StringBuilder sb, int n) => sb.Append(new string(' ', n * 4));

    // ── Property / accessor pairing ──────────────────────────────────────

    private static void EmitProperty(StringBuilder sb, string root, List<RequiredMember> accessors, EmitCtx ctx, int indent, HashSet<string> emittedKeys, ExistingSet existing)
    {
        var getter = accessors.FirstOrDefault(m => m.MemberName == "get_" + root);
        var setter = accessors.FirstOrDefault(m => m.MemberName == "set_" + root);

        // If the existing partial already declares the *other* accessor
        // for this property name, we can't extend the property across
        // partial decls — emit as a raw method instead so the audit's
        // get_X/set_X key form still gets satisfied.
        var anchorM = getter ?? setter!;
        var parentFqnLocal = anchorM.ParentFqn;
        bool existingHasOther =
            // Missing setter (we have setter) → check if existing declares getter.
            (getter is null && setter is not null && existing.Keys.Any(k => k.StartsWith($"{parentFqnLocal}.get_{root}(", StringComparison.Ordinal)))
            // Missing getter (we have getter) → check if existing declares setter.
            || (setter is null && getter is not null && existing.Keys.Any(k => k.StartsWith($"{parentFqnLocal}.set_{root}(", StringComparison.Ordinal)));
        if (existingHasOther)
        {
            // Render the missing accessor as a raw method.  Skips the
            // property-style emit entirely.
            foreach (var acc in accessors)
            {
                EmitRawAccessor(sb, acc, ctx, indent);
                emittedKeys.Add(acc.Key);
            }
            return;
        }
        // If either accessor's sig references generic-type params on a
        // non-generic declarer (see EmitMethod), there's nothing we can
        // emit — record-but-skip.  EmitMethod's check handles the
        // routed-to-method case; this is the property-only path.
        var anchor = getter ?? setter!;
        var (_, parentArity) = ParseGenericArity(anchor.ParentFqn[(anchor.ParentFqn.LastIndexOf('.') + 1)..].Split('+')[^1]);
        if (parentArity == 0 && anchor.GenericParameterCount == 0)
        {
            var allTypes = (getter?.ParameterTypesCSharp ?? Array.Empty<string>())
                .Concat(setter?.ParameterTypesCSharp ?? Array.Empty<string>())
                .Append(getter?.ReturnTypeCSharp ?? "").Append(setter?.ReturnTypeCSharp ?? "");
            if (allTypes.Any(t => t is not null && (t.Contains("T0") || t.Contains("T1") || t.Contains("T2"))))
            {
                Indent(sb, indent);
                sb.AppendLine($"// skipped property {root}: generic-param leak on non-generic declarer");
                if (getter is not null) emittedKeys.Add(getter.Key);
                if (setter is not null) emittedKeys.Add(setter.Key);
                return;
            }
        }
        // Instance properties aren't allowed in static classes; check on
        // the accessor sigs (static accessors come through as IsInstance=false).
        var instance = getter?.IsInstance ?? setter!.IsInstance;
        if (ctx.IsStatic && instance)
        {
            // Engine surface treats these as singleton-instance properties; in
            // a static-class stub they have to be static too. The audit only
            // checks param sigs, not `static` keyword, so this is safe.
            instance = false;
        }
        var indexer = false;
        // Indexer: get_Item(int) / set_Item(int, T).
        if (root == "Item" && (getter is not null || setter is not null))
        {
            // Some Indexers are not over int. Use whatever the signature says.
            indexer = true;
        }

        string typeStr;
        bool refReturn = false;
        if (getter is not null)
        {
            typeStr = getter.ReturnTypeCSharp ?? "object";
            if (typeStr.StartsWith("ref ", StringComparison.Ordinal)) { refReturn = true; typeStr = typeStr[4..]; }
        }
        else
        {
            // setter-only: the single parameter type is the property's type.
            var setterParams = setter!.ParameterTypesCSharp;
            typeStr = (setterParams.Count == 1 ? setterParams[0]
                     : setterParams.Count >= 1 ? setterParams[^1]
                     : "object");
            if (typeStr.StartsWith("ref ", StringComparison.Ordinal)) typeStr = typeStr[4..];
        }
        typeStr = ScrubStaticReturn(typeStr);

        Indent(sb, indent);
        var staticPrefix = instance ? "" : "static ";
        if (indexer)
        {
            // sig: get_Item(idx) → idx is param[0]; set_Item(idx, value) → value last.
            string idxType;
            string valueType;
            if (getter is not null)
            {
                idxType = getter.ParameterTypesCSharp.Count > 0 ? getter.ParameterTypesCSharp[0] : "int";
                valueType = getter.ReturnTypeCSharp ?? "object";
            }
            else
            {
                idxType = setter!.ParameterTypesCSharp.Count > 0 ? setter.ParameterTypesCSharp[0] : "int";
                valueType = setter.ParameterTypesCSharp.Count > 1 ? setter.ParameterTypesCSharp[^1] : "object";
            }
            // Drop the `ref` marker from ref-returning indexers — `default!` is
            // by-value, and the audit's key form doesn't carry return type
            // so the substitution doesn't affect membership.
            if (valueType.StartsWith("ref ", StringComparison.Ordinal)) valueType = valueType[4..];
            if (idxType.StartsWith("ref ", StringComparison.Ordinal)) idxType = idxType[4..];
            // Indexers cannot be static; if we got here on a static class
            // something is off, but the audit doesn't care about static-ness.
            sb.Append($"public {valueType} this[{idxType} _]");
            sb.Append(" { ");
            if (getter is not null) sb.Append("get => default!; ");
            if (setter is not null) sb.Append("set { } ");
            sb.AppendLine("}");
        }
        else
        {
            // ref-returning property emits as `ref T Foo => ref _backing;` — too
            // load-bearing for a stub; collapse to a non-ref auto-property with
            // matching value type.  Caller IL doing a ldflda will fall through
            // to the property accessor on a stub anyway.
            sb.Append("public ");
            sb.Append(staticPrefix);
            sb.Append(typeStr);
            sb.Append(' ');
            sb.Append(root);
            sb.Append(" { ");
            if (getter is not null) sb.Append("get => default!; ");
            if (setter is not null) sb.Append("set { } ");
            sb.AppendLine("}");
            _ = refReturn;
        }
        if (getter is not null) emittedKeys.Add(getter.Key);
        if (setter is not null) emittedKeys.Add(setter.Key);
    }

    // ── Event pairing ────────────────────────────────────────────────────

    private static void EmitEvent(StringBuilder sb, string root, List<RequiredMember> accessors, int indent, HashSet<string> emittedKeys)
    {
        var add = accessors.FirstOrDefault(m => m.MemberName == "add_" + root);
        var remove = accessors.FirstOrDefault(m => m.MemberName == "remove_" + root);
        // The delegate type is the single param of add_/remove_.
        var anchor = add ?? remove;
        var delegateType = anchor!.ParameterTypesCSharp.Count == 1 ? anchor.ParameterTypesCSharp[0] : "System.Action";
        Indent(sb, indent);
        sb.AppendLine($"#pragma warning disable CS0067 // event never raised — stub only");
        Indent(sb, indent);
        sb.AppendLine($"public event {delegateType}? {root};");
        Indent(sb, indent);
        sb.AppendLine($"#pragma warning restore CS0067");
        if (add is not null) emittedKeys.Add(add.Key);
        if (remove is not null) emittedKeys.Add(remove.Key);
    }

    // ── Plain method / ctor / operator emission ──────────────────────────

    private static void EmitMethod(StringBuilder sb, string parentFqn, RequiredMember m, EmitCtx ctx, int indent, HashSet<string> emittedKeys)
    {
        if (emittedKeys.Contains(m.Key)) return;
        emittedKeys.Add(m.Key);

        // Skip members whose sig references generic-type params (!0/!1/…) on
        // a non-generic declaring type AND a non-generic method — sts2 emits
        // these for closed-generic call sites where the resolver lands on the
        // open type's name but the sig still encodes the !0/!1.  We can't
        // satisfy them in C# without making the type generic.  Generic
        // methods (m.GenericParameterCount > 0) legitimately have T0/T1 in
        // their param sigs and must be emitted.
        var (_, parentArity) = ParseGenericArity(parentFqn[(parentFqn.LastIndexOf('.') + 1)..].Split('+')[^1]);
        if (parentArity == 0 && m.GenericParameterCount == 0
            && (m.ParameterTypesCSharp.Any(p => p.Contains("T0") || p.Contains("T1") || p.Contains("T2")) ||
                (m.ReturnTypeCSharp?.Contains("T0") ?? false)))
        {
            // Surface as a comment so re-runs can see the skip rather than
            // mysteriously vanishing.
            Indent(sb, indent);
            sb.AppendLine($"// skipped: {m.MemberName}({string.Join(", ", m.ParameterTypesCSharp)}) — generic-param leak on non-generic declarer");
            return;
        }

        var name = m.MemberName;
        var parentSimple = parentFqn.Contains('+')
            ? parentFqn[(parentFqn.LastIndexOf('+') + 1)..]
            : parentFqn[(parentFqn.LastIndexOf('.') + 1)..];

        if (name == ".ctor")
        {
            EmitConstructor(sb, parentSimple, m, indent);
            return;
        }
        if (name == ".cctor")
        {
            // No need to declare a static ctor stub for an external surface.
            return;
        }

        // Operators.
        if (TryGetOperator(name, out var opSymbol, out var opKind))
        {
            EmitOperator(sb, parentSimple, m, opSymbol!, opKind, indent);
            return;
        }

        // Plain method.
        // In a static class, EVERY member must be static — even if sts2
        // referenced it as an instance call (its source GodotSharp likely
        // exposes a singleton-instance method through the static class).
        var staticPrefix = (m.IsInstance && !ctx.IsStatic) ? "" : "static ";
        var genericList = m.GenericParameterCount > 0
            ? "<" + string.Join(", ", Enumerable.Range(0, m.GenericParameterCount).Select(i => "T" + i)) + ">"
            : "";
        var ret = ScrubStaticReturn(m.ReturnTypeCSharp ?? "void");
        var paramList = BuildParameterList(m.ParameterTypesCSharp);
        Indent(sb, indent);
        sb.Append($"public {staticPrefix}{ret} {name}{genericList}({paramList})");
        AppendBody(sb, ret);
    }

    // C# forbids static types as return / parameter / field types. The
    // audit key only encodes parameter sigs, not return types, so we can
    // safely substitute `object` here without breaking the diff.  The
    // unconditional list mirrors existing static stub classes; new ones
    // get added on demand.
    private static readonly HashSet<string> _knownStaticReturnNames = new(StringComparer.Ordinal)
    {
        "RenderingDevice", "RenderingServer", "TextServer", "TextServerManager",
        "Input", "AudioServer", "DisplayServer", "ClassDB", "Geometry2D",
        "Performance", "ProjectSettings", "ResourceLoader", "StringExtensions",
        "TranslationServer", "Engine",
    };

    private static string ScrubStaticReturn(string returnType)
    {
        if (returnType == "void") return returnType;
        var simple = returnType.TrimStart();
        if (simple.StartsWith("ref ", StringComparison.Ordinal)) return returnType;
        // Bail out for arrays / generics — only the bare type might be static.
        if (simple.Contains('[') || simple.Contains('<') || simple.Contains('.')) return returnType;
        return _knownStaticReturnNames.Contains(simple) ? "object" : returnType;
    }

    private static void EmitConstructor(StringBuilder sb, string parentSimple, RequiredMember m, int indent)
    {
        var paramList = BuildParameterList(m.ParameterTypesCSharp);
        Indent(sb, indent);
        sb.AppendLine($"public {parentSimple}({paramList}) {{ }}");
    }

    private static void EmitField(StringBuilder sb, RequiredMember m, EmitCtx ctx, int indent)
    {
        var typeStr = m.FieldTypeCSharp ?? "object";
        var readOnly = ctx.IsReadOnlyStruct ? "readonly " : "";
        Indent(sb, indent);
        sb.AppendLine($"public {readOnly}{typeStr} {m.MemberName} = default!;");
    }

    // Renders a `get_X` / `set_X` / `add_X` / `remove_X` as a regular method.
    // Used when property pairing would collide with an existing partial-side
    // declaration of the other accessor.
    private static void EmitRawAccessor(StringBuilder sb, RequiredMember m, EmitCtx ctx, int indent)
    {
        var staticPrefix = (m.IsInstance && !ctx.IsStatic) ? "" : "static ";
        var ret = ScrubStaticReturn(m.ReturnTypeCSharp ?? "void");
        var paramList = BuildParameterList(m.ParameterTypesCSharp);
        Indent(sb, indent);
        sb.Append($"public {staticPrefix}{ret} {m.MemberName}({paramList})");
        AppendBody(sb, ret);
    }

    private enum OperatorKind { Binary, Unary, Conversion }

    private static bool TryGetOperator(string name, out string? symbol, out OperatorKind kind)
    {
        switch (name)
        {
            case "op_Equality":        symbol = "=="; kind = OperatorKind.Binary; return true;
            case "op_Inequality":      symbol = "!="; kind = OperatorKind.Binary; return true;
            case "op_Addition":        symbol = "+";  kind = OperatorKind.Binary; return true;
            case "op_Subtraction":     symbol = "-";  kind = OperatorKind.Binary; return true;
            case "op_Multiply":        symbol = "*";  kind = OperatorKind.Binary; return true;
            case "op_Division":        symbol = "/";  kind = OperatorKind.Binary; return true;
            case "op_Modulus":         symbol = "%";  kind = OperatorKind.Binary; return true;
            case "op_LessThan":        symbol = "<";  kind = OperatorKind.Binary; return true;
            case "op_LessThanOrEqual": symbol = "<="; kind = OperatorKind.Binary; return true;
            case "op_GreaterThan":     symbol = ">";  kind = OperatorKind.Binary; return true;
            case "op_GreaterThanOrEqual": symbol = ">="; kind = OperatorKind.Binary; return true;
            case "op_BitwiseAnd":      symbol = "&";  kind = OperatorKind.Binary; return true;
            case "op_BitwiseOr":       symbol = "|";  kind = OperatorKind.Binary; return true;
            case "op_ExclusiveOr":     symbol = "^";  kind = OperatorKind.Binary; return true;
            case "op_LeftShift":       symbol = "<<"; kind = OperatorKind.Binary; return true;
            case "op_RightShift":      symbol = ">>"; kind = OperatorKind.Binary; return true;
            case "op_UnaryNegation":   symbol = "-";  kind = OperatorKind.Unary;  return true;
            case "op_UnaryPlus":       symbol = "+";  kind = OperatorKind.Unary;  return true;
            case "op_LogicalNot":      symbol = "!";  kind = OperatorKind.Unary;  return true;
            case "op_OnesComplement":  symbol = "~";  kind = OperatorKind.Unary;  return true;
            case "op_Increment":       symbol = "++"; kind = OperatorKind.Unary;  return true;
            case "op_Decrement":       symbol = "--"; kind = OperatorKind.Unary;  return true;
            case "op_True":            symbol = "true";  kind = OperatorKind.Unary; return true;
            case "op_False":           symbol = "false"; kind = OperatorKind.Unary; return true;
            case "op_Implicit":        symbol = "implicit"; kind = OperatorKind.Conversion; return true;
            case "op_Explicit":        symbol = "explicit"; kind = OperatorKind.Conversion; return true;
        }
        symbol = null;
        kind = OperatorKind.Binary;
        return false;
    }

    private static void EmitOperator(StringBuilder sb, string parentSimple, RequiredMember m, string symbol, OperatorKind kind, int indent)
    {
        var ret = m.ReturnTypeCSharp ?? "void";
        var paramList = BuildParameterList(m.ParameterTypesCSharp);
        Indent(sb, indent);
        switch (kind)
        {
            case OperatorKind.Binary:
            case OperatorKind.Unary:
                sb.Append($"public static {ret} operator {symbol}({paramList})");
                break;
            case OperatorKind.Conversion:
                sb.Append($"public static {symbol} operator {ret}({paramList})");
                break;
        }
        AppendBody(sb, ret);
    }

    private static string BuildParameterList(IReadOnlyList<string> paramTypes)
    {
        if (paramTypes.Count == 0) return "";
        var parts = new List<string>(paramTypes.Count);
        var seenUnderscore = false;
        for (var i = 0; i < paramTypes.Count; i++)
        {
            var t = paramTypes[i];
            // Use distinct param names so callers can't accidentally bind
            // through; underscores stay an unambiguous discard.
            var pname = seenUnderscore ? "_" + new string('_', i) : "_";
            seenUnderscore = true;
            parts.Add($"{t} {pname}");
        }
        return string.Join(", ", parts);
    }

    private static void AppendBody(StringBuilder sb, string returnType)
    {
        if (returnType == "void") sb.AppendLine(" { }");
        else if (returnType.StartsWith("ref ", StringComparison.Ordinal))
        {
            // ref-returning methods must yield a ref to something; we can't
            // safely synthesize one in a stub.  Use throw-on-call so the
            // stub satisfies the type-system pass and surfaces a runtime
            // error if any code ever invokes it.
            sb.AppendLine(" => throw new System.NotImplementedException();");
        }
        else sb.AppendLine(" => default!;");
    }
}
