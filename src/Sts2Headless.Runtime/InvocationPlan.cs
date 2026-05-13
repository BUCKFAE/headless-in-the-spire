using System.Reflection;

namespace Sts2Headless.Runtime;

// Bind-time-captured method + parameter shape. Invoke by supplying a name→
// value dict; anything the dict doesn't carry must have a default in the
// method signature, otherwise we throw a diagnostic naming the signature.
// Used wherever sts2's signatures have optional parameters we don't care to
// understand — version drift in those params then doesn't reach us.
internal sealed class InvocationPlan
{
    public MethodInfo Method { get; }
    public ParameterInfo[] Parameters { get; }

    public InvocationPlan(MethodInfo method)
    {
        Method = method;
        Parameters = method.GetParameters();
    }

    public object? Invoke(object? target, IReadOnlyDictionary<string, object?> known)
    {
        var args = new object?[Parameters.Length];
        for (var i = 0; i < Parameters.Length; i++)
        {
            var p = Parameters[i];
            if (known.TryGetValue(p.Name!, out var v))
            {
                args[i] = v;
            }
            else if (p.HasDefaultValue)
            {
                args[i] = p.DefaultValue;
            }
            else
            {
                throw new InvalidOperationException(
                    $"InvocationPlan({Method.DeclaringType?.Name}.{Method.Name}): " +
                    $"parameter '{p.Name}' has no provided value and no default. " +
                    $"signature: {Describe()}");
            }
        }
        return Method.Invoke(target, args);
    }

    public string Describe() =>
        $"({string.Join(", ", Parameters.Select(p => $"{p.Name}: {p.ParameterType.Name}{(p.HasDefaultValue ? "?" : "")}"))})";
}
