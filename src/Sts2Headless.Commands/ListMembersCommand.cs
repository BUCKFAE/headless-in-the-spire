using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Sts2Headless.Utils;

namespace Sts2Headless.Commands;

// Enumerates every member of a given external type that sts2.dll references.
// Reads the MemberReference table directly via System.Reflection.Metadata —
// no assembly load required, no Cecil dependency.
//
// Intended use: feed GodotStubs growth. Instead of iterating one
// MissingMethodException at a time, run e.g.
//     just runner::probe::list-members Godot.OS
// to learn the complete surface sts2 expects from that type, then add the
// stubs in one pass. Keeps GodotStubs in sync with what sts2 actually
// touches; never speculates beyond that.
internal static class ListMembersCommand
{
    public static int Run(string vendorDir, string targetFullName)
    {
        var sts2Path = Paths.Sts2DllPath(vendorDir);
        if (!File.Exists(sts2Path))
        {
            Console.Error.WriteLine(Paths.Sts2DllMissingMessage);
            return 1;
        }

        var (ns, name) = SplitFqn(targetFullName);

        using var pe = new PEReader(File.OpenRead(sts2Path));
        var md = pe.GetMetadataReader();

        // A single (namespace, name) may resolve to TypeRefs in multiple
        // referenced assemblies — match them all so we don't silently
        // miss members coming through a different scope.
        var targets = new HashSet<TypeReferenceHandle>();
        foreach (var handle in md.TypeReferences)
        {
            var tr = md.GetTypeReference(handle);
            if (md.GetString(tr.Name) == name && md.GetString(tr.Namespace) == ns)
            {
                targets.Add(handle);
            }
        }
        if (targets.Count == 0)
        {
            Console.WriteLine($"list-members {targetFullName}: no TypeReference in sts2.dll");
            return 0;
        }

        var provider = new SignatureToStringProvider();
        var lines = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var handle in md.MemberReferences)
        {
            var mr = md.GetMemberReference(handle);

            // Members touched on generic instantiations have a TypeSpec
            // parent (the closed generic) — peel it back to the underlying
            // TypeRef so e.g. Dictionary`2 members show up alongside Dictionary's.
            TypeReferenceHandle? parentRef = mr.Parent.Kind switch
            {
                HandleKind.TypeReference => (TypeReferenceHandle)mr.Parent,
                HandleKind.TypeSpecification => ResolveTypeSpecToRef(md, (TypeSpecificationHandle)mr.Parent),
                _ => null,
            };
            if (parentRef is null || !targets.Contains(parentRef.Value)) continue;

            var memberName = md.GetString(mr.Name);
            switch (mr.GetKind())
            {
                case MemberReferenceKind.Method:
                    var sig = mr.DecodeMethodSignature(provider, genericContext: null);
                    var args = string.Join(", ", sig.ParameterTypes);
                    var prefix = (sig.Header.IsInstance ? "method " : "static ")
                                 + (memberName == ".ctor" || memberName == ".cctor" ? "" : sig.ReturnType + " ");
                    lines.Add($"{prefix}{memberName}({args})");
                    break;
                case MemberReferenceKind.Field:
                    var fieldType = mr.DecodeFieldSignature(provider, genericContext: null);
                    lines.Add($"field  {fieldType} {memberName}");
                    break;
            }
        }

        Console.WriteLine($"list-members {targetFullName}: {lines.Count} unique reference(s)");
        foreach (var line in lines) Console.WriteLine($"  {line}");
        return 0;
    }

    private static (string ns, string name) SplitFqn(string fqn)
    {
        var i = fqn.LastIndexOf('.');
        return i < 0 ? ("", fqn) : (fqn[..i], fqn[(i + 1)..]);
    }

    // Decode a TypeSpec blob just enough to find the underlying TypeRef it
    // instantiates. TypeSpec signatures for closed generics start with
    // ELEMENT_TYPE_GENERICINST (0x15) then ELEMENT_TYPE_CLASS/VALUETYPE
    // followed by a TypeDefOrRef coded index pointing at the open generic.
    // Using a recording provider is simpler than hand-decoding the blob.
    private static TypeReferenceHandle? ResolveTypeSpecToRef(MetadataReader md, TypeSpecificationHandle handle)
    {
        var recorder = new TypeRefRecorder();
        md.GetTypeSpecification(handle).DecodeSignature(recorder, genericContext: (object?)null);
        return recorder.First;
    }
}

// Throwaway ISignatureTypeProvider used solely to capture the first
// TypeReference encountered while decoding a TypeSpec signature. For
// `Dictionary<TKey,TValue>` that's the open generic Dictionary`2 itself —
// the only thing we need to filter MemberRef parents back to our targets.
internal sealed class TypeRefRecorder : ISignatureTypeProvider<int, object?>
{
    public TypeReferenceHandle? First { get; private set; }

    public int GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        First ??= handle;
        return 0;
    }

    public int GetGenericInstantiation(int genericType, ImmutableArray<int> typeArguments) => 0;
    public int GetPrimitiveType(PrimitiveTypeCode typeCode) => 0;
    public int GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => 0;
    public int GetSZArrayType(int elementType) => 0;
    public int GetArrayType(int elementType, ArrayShape shape) => 0;
    public int GetPointerType(int elementType) => 0;
    public int GetByReferenceType(int elementType) => 0;
    public int GetGenericMethodParameter(object? genericContext, int index) => 0;
    public int GetGenericTypeParameter(object? genericContext, int index) => 0;
    public int GetModifiedType(int modifier, int unmodifiedType, bool isRequired) => 0;
    public int GetPinnedType(int elementType) => 0;
    public int GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => 0;
    public int GetFunctionPointerType(MethodSignature<int> signature) => 0;
}

// Minimal ISignatureTypeProvider that stringifies signatures. Returns short
// names ("Int32", "String[]") rather than fully-qualified ones — readable
// output is the whole point and FQNs are noise here.
internal sealed class SignatureToStringProvider : ISignatureTypeProvider<string, object?>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var td = reader.GetTypeDefinition(handle);
        var ns = reader.GetString(td.Namespace);
        var name = reader.GetString(td.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var tr = reader.GetTypeReference(handle);
        var name = reader.GetString(tr.Name);
        // Nested types have a TypeRef as their resolution scope; chain up
        // to make the relationship visible (e.g. FileAccess+ModeFlags).
        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            var parent = GetTypeFromReference(reader, (TypeReferenceHandle)tr.ResolutionScope, rawTypeKind);
            return $"{parent}.{name}";
        }
        return name;
    }

    public string GetSZArrayType(string elementType) => $"{elementType}[]";
    public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";
    public string GetPointerType(string elementType) => $"{elementType}*";
    public string GetByReferenceType(string elementType) => $"ref {elementType}";
    public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
    public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => $"{genericType}<{string.Join(",", typeArguments)}>";
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
}
