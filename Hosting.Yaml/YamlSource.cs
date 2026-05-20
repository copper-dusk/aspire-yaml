namespace CopperDusk.Aspire.Hosting.Yaml;

public interface YamlSource
{
}

public sealed record ObjectYamlSource(object Thing) : YamlSource;
internal sealed record MultiDocumentYamlSource(IEnumerable<YamlSource> Documents) : YamlSource;
internal sealed record RawYamlSource(string Yaml) : YamlSource;
internal sealed record RawJsonSource(string Json) : YamlSource;
