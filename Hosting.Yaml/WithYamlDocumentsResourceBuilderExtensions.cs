namespace CopperDusk.Aspire.Hosting.Yaml;

public static class WithYamlDocumentsResourceBuilderExtensions
{
    public static IResourceBuilder<YamlFileGroupResource> WithYamlDocuments(
        this IResourceBuilder<YamlFileGroupResource> group,
        string name,
        IEnumerable<object> documents,
        string? fileName = null
    )
    {
        var file = group.ApplicationBuilder.AddMultiYamlFile(name, documents, fileName);
        return group.AttachYamlFile(file);
    }
}
