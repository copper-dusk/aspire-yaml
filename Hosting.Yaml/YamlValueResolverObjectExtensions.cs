using System.Collections;
using System.Reflection;
using CopperDusk.Aspire.Hosting.Yaml.BifurcatedEndpoint;
using YamlDotNet.Serialization;

namespace CopperDusk.Aspire.Hosting.Yaml;

/// <summary>
///     Flattens an arbitrary CLR value into the plain object / dictionary / list tree shape that
///     YamlDotNet's serializer accepts. Lives outside <see cref="YamlProvisioner"/> so it can be
///     factored, replaced, or unit-tested in isolation from the broader rendering pipeline.
/// </summary>
internal static class YamlValueResolverObjectExtensions
{
    /// <summary>
    ///     Recursively converts an arbitrary CLR value into a tree of plain objects, dictionaries,
    ///     and lists that YamlDotNet can serialize directly.
    ///     <br /><br />
    ///     Also unwraps <see cref="IResourceBuilder{T}"/> to its underlying resource, dispatches
    ///     <see cref="PerspectiveAware"/> values against the active <paramref name="perspective"/>,
    ///     awaits <see cref="IValueProvider"/> values, leaves primitives and well-known scalar
    ///     types as-is, walks <see cref="IDictionary"/> and <see cref="IEnumerable"/> elements, and
    ///     otherwise reflects over public readable instance properties, honouring
    ///     <see cref="YamlMemberAttribute.Alias"/> when present and falling back to the supplied
    ///     <paramref name="namingConvention"/>.
    /// </summary>
    public static async Task<object?> ResolveForYamlAsync(
        this object? value,
        INamingConvention namingConvention,
        YamlPerspective perspective,
        CancellationToken cancellationToken = default
    )
    {
        // Nothing to resolve — preserve null so it round-trips as a YAML null.
        if (value is null) return null;

        // Reflection root: we need runtime type info, not the declared static type.
        var type = value.GetType();

        // Look for IResourceBuilder<T> via interface scan because T is unknown here —
        // we can't pattern-match on the closed generic without it.
        var resourceBuilderInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IResourceBuilder<>));

        if (resourceBuilderInterface is not null)
        {
            // Unwrap the builder by reading its Resource property, then recurse on the
            // underlying resource so the builder wrapper never appears in the YAML.
            var resource = resourceBuilderInterface.GetProperty(nameof(IResourceBuilder<IResource>.Resource))?.GetValue(value);
            return await resource.ResolveForYamlAsync(namingConvention, perspective, cancellationToken);
        }

        // Perspective-aware values pick a branch based on the current render target — check
        // this BEFORE the generic IValueProvider branch since PerspectiveValue implements both
        // and the generic path would silently collapse to the host view.
        if (value is PerspectiveAware perspectiveAware)
        {
            return await perspectiveAware.GetValueAsync(perspective, cancellationToken);
        }

        // ReferenceExpression's own GetValueAsync resolves each inner provider via the plain
        // IValueProvider path, which loses our perspective signal for any PerspectiveAware
        // values embedded inside the interpolation. Re-implement the format step ourselves so
        // those embedded values get the perspective treatment.
        if (value is ReferenceExpression referenceExpression)
        {
            return await ResolveReferenceExpressionAsync(referenceExpression, perspective, cancellationToken);
        }

        // IValueProvider is Aspire's deferred-value abstraction (e.g. parameters, references
        // that only resolve at run time). Await it to get the materialized value.
        if (value is IValueProvider valueProvider)
        {
            return await valueProvider.GetValueAsync(cancellationToken);
        }

        // Scalars pass through untouched — YamlDotNet knows how to render these directly,
        // so we don't want to walk into them as if they were structured objects.
        if (
            type.IsPrimitive
            || value
                is string
                or decimal
                or DateTime
                or DateTimeOffset
                or DateOnly
                or TimeOnly
                or TimeSpan
                or Guid
                or Enum
        )
        {
            return value;
        }

        // Dictionaries become a fresh Dictionary<object, object?> with each value resolved
        // recursively. We keep the original key (no naming convention applied) since dictionary
        // keys are data, not property names.
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<object, object?>();
            foreach (DictionaryEntry entry in dictionary)
            {
                result[entry.Key] = await entry.Value.ResolveForYamlAsync(namingConvention, perspective, cancellationToken);
            }
            return result;
        }

        // Any other enumerable (arrays, lists, IEnumerable<T>) becomes a List<object?> of
        // resolved items. This check must come AFTER IDictionary, since dictionaries are also
        // enumerable but should be treated as maps, not sequences.
        if (value is IEnumerable enumerable)
        {
            var list = new List<object?>();

            foreach (var item in enumerable)
            {
                list.Add(await item.ResolveForYamlAsync(namingConvention, perspective, cancellationToken));
            }

            return list;
        }

        // We've ruled out scalars and collections, so treat the value as a structured object
        // and enumerate its public instance properties.
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // No properties to project — there's nothing meaningful to build a map from, so hand
        // the value back as-is and let YamlDotNet do whatever it would do with it.
        if (props.Length == 0)
        {
            return value;
        }

        // Build a string-keyed map representing this object's serializable shape.
        var members = new Dictionary<string, object?>();
        foreach (var prop in props)
        {
            // Skip indexers (this[int]) — they aren't members in the YAML sense and would throw
            // when invoked without arguments.
            if (prop.GetIndexParameters().Length > 0) continue;
            // Skip write-only properties; there's no value to read.
            if (!prop.CanRead) continue;

            // Honour [YamlMember(Alias = "...")] if the author set one, otherwise apply the
            // supplied naming convention to the CLR property name.
            var alias = prop.GetCustomAttribute<YamlMemberAttribute>()?.Alias;
            var key = !string.IsNullOrEmpty(alias) ? alias : namingConvention.Apply(prop.Name);
            // Recurse so nested objects, collections, and deferred values are all flattened
            // before serialization.
            members[key] = await prop.GetValue(value).ResolveForYamlAsync(namingConvention, perspective, cancellationToken);
        }

        return members;
    }

    /// <summary>
    ///     Mirrors <see cref="ReferenceExpression.GetValueAsync"/>'s positional <c>string.Format</c>
    ///     step but resolves each inner provider via <see cref="ResolveProviderAsync"/> so any
    ///     <see cref="PerspectiveAware"/> values embedded in the interpolation pick the right
    ///     branch. Conditional expressions are uncommon in YAML authoring and intersect awkwardly
    ///     with perspective swaps, so we hand those back to Aspire's own resolver.
    /// </summary>
    private static async Task<string?> ResolveReferenceExpressionAsync(
        ReferenceExpression expression,
        YamlPerspective perspective,
        CancellationToken cancellationToken
    )
    {
        if (expression.IsConditional)
        {
            return await expression.GetValueAsync(cancellationToken);
        }

        var args = new object?[expression.ValueProviders.Count];
        for (var i = 0; i < expression.ValueProviders.Count; i++)
        {
            var resolved = await ResolveProviderAsync(expression.ValueProviders[i], perspective, cancellationToken);
            var formatSpecifier = i < expression.StringFormats.Count ? expression.StringFormats[i] : null;
            // Per-provider format specifiers (e.g. {0:X}) are reapplied around the resolved value.
            args[i] = string.IsNullOrEmpty(formatSpecifier)
                ? resolved
                : string.Format($"{{0:{formatSpecifier}}}", resolved);
        }

        return string.Format(expression.Format, args);
    }

    /// <summary>
    ///     Resolves a single <see cref="IValueProvider"/> with perspective awareness: dispatches
    ///     to <see cref="PerspectiveAware"/> when applicable, recurses into nested
    ///     <see cref="ReferenceExpression"/>s, and otherwise falls back to the standard
    ///     <see cref="IValueProvider.GetValueAsync"/>.
    /// </summary>
    private static async Task<string?> ResolveProviderAsync(
        IValueProvider provider,
        YamlPerspective perspective,
        CancellationToken cancellationToken
    ) => provider switch
    {
        PerspectiveAware pa => await pa.GetValueAsync(perspective, cancellationToken),
        ReferenceExpression nested => await ResolveReferenceExpressionAsync(nested, perspective, cancellationToken),
        _ => await provider.GetValueAsync(cancellationToken),
    };
}
