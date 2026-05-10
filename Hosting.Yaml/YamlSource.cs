namespace CopperDusk.Aspire.Hosting.Yaml;

public interface YamlSource
{
}

// Debating these two, as they handle differently.
// internal sealed record FileYamlSource(string Path) : YamlSource;    // copy or JSON→YAML
// internal sealed record JsonYamlSource(string Path) : YamlSource;    // copy or JSON→YAML
internal sealed record ObjectYamlSource(object Thing) : YamlSource; // serialize via YamlDotNet
internal sealed record RawYamlSource(string Yaml) : YamlSource;     // write as-is
internal sealed record RawJsonSource(string Json) : YamlSource;     // convert then write
