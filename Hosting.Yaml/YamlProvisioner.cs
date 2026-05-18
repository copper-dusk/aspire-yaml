using System.Text.Json;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CopperDusk.Aspire.Hosting.Yaml;

internal class YamlProvisioner(
    ResourceLoggerService resourceLogger
)
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public async Task ProvisionYamlAsync(Yaml yaml, CancellationToken cancellationToken)
    {
        var logger = resourceLogger.GetLogger(yaml);

        foreach (var resource in yaml.YamlResources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation("Writing {FileName} to {OutputPath}", resource.FileName, resource.OutputPath);

            var content = resource.Source switch
            {
                RawYamlSource raw => raw.Yaml,
                RawJsonSource json => ConvertJsonToYaml(json.Json),
                ObjectYamlSource obj => Serializer.Serialize(obj.Thing),
                _ => throw new NotSupportedException($"Unsupported source type: {resource.Source.GetType().Name}"),
            };

            await File.WriteAllTextAsync(resource.OutputPath, content, cancellationToken);
        }
    }

    private static string ConvertJsonToYaml(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Serializer.Serialize(ConvertElement(doc.RootElement));
    }

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