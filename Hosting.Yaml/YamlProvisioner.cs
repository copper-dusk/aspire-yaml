using System.Text;
using System.Text.Json;
using CopperDusk.Aspire.Hosting.Yaml.BifurcatedEndpoint;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CopperDusk.Aspire.Hosting.Yaml;

/// <summary>
///     Renders the YAML content for a <see cref="YamlSourceResource"/> by dispatching on the
///     underlying <see cref="YamlSource"/> variant: raw YAML is validated and passed through,
///     raw JSON is converted, and object sources are resolved into a serializable shape and
///     then emitted via YamlDotNet.
/// </summary>
internal class YamlProvisioner
{
    private static readonly INamingConvention NamingConvention = NullNamingConvention.Instance;

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(NamingConvention)
        .Build();

    /// <summary>
    ///     Renders the final YAML string for the given resource by dispatching to
    ///     <see cref="RenderSourceAsync"/> on its source variant, using the supplied perspective
    ///     to decide how any <see cref="PerspectiveAware"/> values inside the source tree should
    ///     resolve.
    /// </summary>
    public Task<string> RenderContentAsync(YamlSourceResource resource, YamlPerspective perspective, CancellationToken cancellationToken) =>
        RenderSourceAsync(resource.Source, perspective, cancellationToken);

    /// <summary>
    ///     Renders a single <see cref="YamlSource"/> to its YAML string form. Composed recursively
    ///     by <see cref="MultiDocumentYamlSource"/> so each contained source picks the right
    ///     rendering strategy (raw passthrough, JSON conversion, object serialization, or further
    ///     nesting). Throws <see cref="NotSupportedException"/> for unknown variants.
    /// </summary>
    private static async Task<string> RenderSourceAsync(YamlSource source, YamlPerspective perspective, CancellationToken cancellationToken) => source switch
    {
        RawYamlSource raw => ValidateYaml(raw.Yaml),
        RawJsonSource json => ConvertJsonToYaml(json.Json),
        ObjectYamlSource obj => Serializer.Serialize(await obj.Thing.ResolveForYamlAsync(NamingConvention, perspective, cancellationToken)),
        MultiDocumentYamlSource multi => await JoinDocumentsAsync(multi.Documents, perspective, cancellationToken),
        _ => throw new NotSupportedException($"Unsupported source type: {source.GetType().Name}"),
    };

    /// <summary>
    ///     Renders each contained source in turn and joins them with the YAML document-separator
    ///     <c>---</c> to produce a single multi-document YAML stream (the form Kubernetes and
    ///     similar tools accept when bundling manifests in one file).
    /// </summary>
    private static async Task<string> JoinDocumentsAsync(IEnumerable<YamlSource> documents, YamlPerspective perspective, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var first = true;

        foreach (var document in documents)
        {
            if (!first) sb.AppendLine("---");

            sb.Append(await RenderSourceAsync(document, perspective, cancellationToken));

            first = false;
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Parses the supplied YAML to confirm it is well-formed and returns it unchanged.
    ///     Any parse error surfaces as a YamlDotNet exception.
    /// </summary>
    private static string ValidateYaml(string yaml)
    {
        using var reader = new StringReader(yaml);

        new YamlStream().Load(reader);

        return yaml;
    }

    /// <summary>
    ///     Parses the supplied JSON and re-emits it as YAML by converting the document tree
    ///     into plain CLR objects and serializing the result.
    /// </summary>
    private static string ConvertJsonToYaml(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Serializer.Serialize(ConvertElement(doc.RootElement));
    }

    /// <summary>
    ///     Recursively converts a <see cref="JsonElement"/> into a plain CLR value:
    ///     objects become <see cref="Dictionary{TKey,TValue}"/>, arrays become
    ///     <see cref="List{T}"/>, numbers prefer <see cref="long"/> when integral and fall back
    ///     to <see cref="double"/>, and null/undefined map to <c>null</c>.
    /// </summary>
    private static object? ConvertElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => ConvertElement(p.Value)),
        JsonValueKind.Array => element.EnumerateArray()
            .Select(ConvertElement)
            .ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? (object) l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}
