using System.Collections;
using System.Reflection;
using Sts2Headless.IntegrationTests.Coverage;
using Sts2Headless.Runtime;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Phase A of "cards as enum, generated": find the most reliable path
// from booted sts2 to "every card id sts2 ships." Two candidates:
//
//   1. ModelDb enumeration API (whatever it is). Authoritative — only
//      the cards sts2 registered are visible, and they carry the same
//      `Id.Entry` string the wire surfaces. Discover its shape (a
//      generic GetAll<T>? a Values property? Enumerator?) by inspecting
//      ModelDb's public surface against the booted assembly.
//
//   2. Reflection over CardModel subclasses. Walk every Type derived
//      from MegaCrit.Sts2.Core.Models.CardModel. Risks: includes
//      retired/internal classes that never make it into ModelDb;
//      misses cards registered via factories.
//
// Probe dumps both lists to /tmp/cardid-probe.md so a human can compare.
// Marked diagnostic — not part of the default test run.
[Collection(InProcessSts2Collection.Name)]
public class CardIdEnumerationProbeTests
{
    private readonly ITestOutputHelper _output;

    public CardIdEnumerationProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("category", "diagnostic")]
    public void DumpCardEnumeration()
    {
        // Load sts2 *in-process* (not via the host subprocess) so this
        // probe can reflect freely against the booted assembly. The
        // bootstrap is idempotent — running it twice is harmless — and
        // we need the model db populated before enumerating.
        var repoRoot = RepoRoot();
        var vendor = Path.Combine(repoRoot, "vendor/sts2.dll");
        Assert.True(File.Exists(vendor), $"vendor/sts2.dll not present at {vendor} — `just pull-game-libs`.");

        VendorAssemblyResolver.Install(Path.GetDirectoryName(vendor)!);
        var sts2 = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(vendor);

        var bootstrap = BootstrapSequence.Apply(sts2);
        Assert.True(bootstrap.All(s => s.Ok), "bootstrap failed: " + string.Join(" | ",
            bootstrap.Where(s => !s.Ok).Select(s => $"{s.Label}: {s.Detail}")));

        var md = new System.Text.StringBuilder();
        md.AppendLine("# CardId enumeration probe");
        md.AppendLine();

        // ── 1. Inspect ModelDb's public surface ──
        var modelDbType = sts2.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? throw new InvalidOperationException("ModelDb type not found");
        md.AppendLine($"## ModelDb surface ({modelDbType.FullName})");
        md.AppendLine();
        foreach (var m in modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
        {
            var generics = m.IsGenericMethodDefinition ? "<" + string.Join(",", m.GetGenericArguments().Select(a => a.Name)) + ">" : "";
            md.AppendLine($"  meth  {m.ReturnType.Name} {m.Name}{generics}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
        }
        foreach (var p in modelDbType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            md.AppendLine($"  prop  {p.PropertyType.Name} {p.Name}");
        }
        foreach (var f in modelDbType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            md.AppendLine($"  field {f.FieldType.Name} {f.Name}");
        }
        md.AppendLine();

        // ── 2. Try the most likely enumeration shapes ──
        var cardModelType = sts2.GetType("MegaCrit.Sts2.Core.Models.CardModel")
            ?? throw new InvalidOperationException("CardModel type not found");

        var enumerated = TryEnumerateModelDbForType(modelDbType, cardModelType);
        md.AppendLine($"## ModelDb cards enumerated: {enumerated.via}");
        md.AppendLine();
        if (enumerated.ids is not null)
        {
            md.AppendLine($"  count: {enumerated.ids.Count}");
            md.AppendLine();
            foreach (var id in enumerated.ids.OrderBy(s => s, StringComparer.Ordinal))
            {
                md.AppendLine($"  - {id}");
            }
        }
        else
        {
            md.AppendLine($"  (no path resolved; first 3 attempts: {enumerated.via})");
        }
        md.AppendLine();

        // ── 3. Fallback: walk CardModel subclasses in sts2.dll ──
        Type?[] types;
        try { types = sts2.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }

        var cardSubclasses = new List<string>();
        foreach (var t in types)
        {
            if (t is null || t.IsAbstract) continue;
            if (!cardModelType.IsAssignableFrom(t)) continue;
            cardSubclasses.Add(t.FullName ?? t.Name);
        }
        md.AppendLine($"## Reflection: concrete CardModel subclasses ({cardSubclasses.Count})");
        md.AppendLine();
        foreach (var n in cardSubclasses.OrderBy(s => s, StringComparer.Ordinal))
        {
            md.AppendLine($"  - {n}");
        }

        // ── 4. Sample card mechanics shape (DynamicVars + keywords + target) ──
        var sampleAllCards = modelDbType.GetProperty("AllCards", BindingFlags.Public | BindingFlags.Static);
        if (sampleAllCards?.GetValue(null) is IEnumerable rawAll)
        {
            md.AppendLine("## Sample card-mechanics shape (BLUDGEON, BASH, STRIKE_IRONCLAD, BODY_SLAM)");
            md.AppendLine();
            var samples = new HashSet<string>(StringComparer.Ordinal)
                { "BLUDGEON", "BASH", "STRIKE_IRONCLAD", "BODY_SLAM", "THUNDERCLAP", "ARMAMENTS", "TRUE_GRIT", "SWORD_BOOMERANG", "UPPERCUT", "HEADBUTT" };
            foreach (var m in rawAll)
            {
                if (m is null) continue;
                var idProp = m.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                if (idProp?.GetValue(m) is not object idObj) continue;
                var entry = idObj.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance)?.GetValue(idObj) as string;
                if (entry is null || !samples.Contains(entry)) continue;

                md.AppendLine($"### {entry}");
                md.AppendLine();
                DumpAllMembers(md, m);
                md.AppendLine();

                // DynamicVars: declarative numeric variables (Damage, Block, …)
                var dynVarsProp = m.GetType().GetProperty("DynamicVars", BindingFlags.Public | BindingFlags.Instance);
                if (dynVarsProp?.GetValue(m) is object dynVars)
                {
                    md.AppendLine("  DynamicVars:");
                    var valuesProp = dynVars.GetType().GetProperty("Values", BindingFlags.Public | BindingFlags.Instance);
                    if (valuesProp?.GetValue(dynVars) is IEnumerable values)
                    {
                        foreach (var dv in values)
                        {
                            if (dv is null) continue;
                            var name = dv.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(dv);
                            var baseVal = dv.GetType().GetProperty("BaseValue", BindingFlags.Public | BindingFlags.Instance)?.GetValue(dv);
                            md.AppendLine($"    - {name} = {baseVal}");
                        }
                    }
                }
                md.AppendLine();
            }
        }

        var path = "/tmp/cardid-probe.md";
        File.WriteAllText(path, md.ToString());
        _output.WriteLine($"=== wrote {path} ({md.Length} chars) ===");
        _output.WriteLine($"=== ModelDb enumeration via: {enumerated.via} ===");
        _output.WriteLine($"=== ModelDb cards: {enumerated.ids?.Count ?? -1} ; CardModel subclasses: {cardSubclasses.Count} ===");
    }

    private static void DumpAllMembers(System.Text.StringBuilder md, object instance)
    {
        var t = instance.GetType();
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0
                && !p.PropertyType.Name.StartsWith("Texture", StringComparison.Ordinal)
                && !p.PropertyType.Name.StartsWith("Material", StringComparison.Ordinal)
                && !p.PropertyType.Name.StartsWith("Control", StringComparison.Ordinal)
                && !p.PropertyType.Name.StartsWith("LocString", StringComparison.Ordinal))
            .OrderBy(p => p.Name)
            .ToList();
        foreach (var p in props)
        {
            object? val = null;
            try { val = p.GetValue(instance); } catch { continue; }
            var s = val switch
            {
                null => "null",
                string str => $"\"{str}\"",
                System.Collections.IEnumerable e when val is not string => "[" + string.Join(",", e.Cast<object?>().Take(8).Select(x => x?.ToString() ?? "null")) + "]",
                _ => val.ToString() ?? "<null>"
            };
            if (s.Length > 120) s = s[..117] + "...";
            md.AppendLine($"  prop {p.PropertyType.Name} {p.Name} = {s}");
        }
    }

    // Try, in order, the most plausible ModelDb enumeration APIs. Return
    // the path that worked + the discovered card-ID strings (or null).
    private static (string via, IReadOnlyList<string>? ids) TryEnumerateModelDbForType(Type modelDbType, Type cardModelType)
    {
        // Candidate A: generic GetAll<T>() returning IEnumerable<T>.
        var getAllGen = modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetAll" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
        if (getAllGen is not null)
        {
            try
            {
                var raw = getAllGen.MakeGenericMethod(cardModelType).Invoke(null, null);
                if (raw is IEnumerable e) return ("ModelDb.GetAll<CardModel>()", ExtractIds(e));
            }
            catch (Exception ex) { return ($"GetAll<CardModel> threw: {ex.GetType().Name}", null); }
        }

        // Candidate B: non-generic GetAll(Type) returning IEnumerable.
        var getAll = modelDbType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetAll" && !m.IsGenericMethod
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(Type));
        if (getAll is not null)
        {
            try
            {
                var raw = getAll.Invoke(null, new object?[] { cardModelType });
                if (raw is IEnumerable e) return ("ModelDb.GetAll(Type)", ExtractIds(e));
            }
            catch (Exception ex) { return ($"GetAll(Type) threw: {ex.GetType().Name}", null); }
        }

        // Candidate C: static "AllCards" property — discovered via the
        // ModelDb surface dump in this same probe. Returns
        // IEnumerable<CardModel>; we extract each model's Id.Entry.
        var allCardsProp = modelDbType.GetProperty("AllCards", BindingFlags.Public | BindingFlags.Static);
        if (allCardsProp is not null)
        {
            try
            {
                var raw = allCardsProp.GetValue(null);
                if (raw is IEnumerable e) return ("ModelDb.AllCards", ExtractIds(e, cardModelType));
            }
            catch (Exception ex) { return ($"AllCards threw: {ex.GetType().Name}", null); }
        }

        return ("no enumeration method matched", null);
    }

    private static IReadOnlyList<string> ExtractIds(IEnumerable models, Type? filterTo = null)
    {
        var ids = new List<string>();
        foreach (var m in models)
        {
            if (m is null) continue;
            if (filterTo is not null && !filterTo.IsAssignableFrom(m.GetType())) continue;
            var idProp = m.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (idProp is null) continue;
            var idObj = idProp.GetValue(m);
            if (idObj is null) continue;
            var entryProp = idObj.GetType().GetProperty("Entry", BindingFlags.Public | BindingFlags.Instance);
            var entry = entryProp?.GetValue(idObj) as string;
            if (entry is not null) ids.Add(entry);
        }
        return ids;
    }

    private static string RepoRoot()
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
}
