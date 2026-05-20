namespace CopperDusk.Aspire.Hosting.Yaml;

public static class AddYamlDocumentsResourceBuilderExtensions
{
    /// <summary>
    ///     Bundles multiple objects into a single multi-document YAML stream (documents joined
    ///     with <c>---</c>). Suited to Kubernetes-style manifests where several resources are
    ///     authored together in one file.
    /// </summary>
    public static IResourceBuilder<YamlSourceResource> AddYamlDocuments(
        this IDistributedApplicationBuilder builder,
        string name,
        IEnumerable<object> documents,
        string? fileName = null
    )
    {
        var sources = documents
            .Select(document => (YamlSource) new ObjectYamlSource(document))
            .ToList();

        return builder.AddYamlCore(name, new MultiDocumentYamlSource(sources), fileName);
    }
}
