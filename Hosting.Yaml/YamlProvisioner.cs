namespace CopperDusk.Aspire.Hosting.Yaml;

internal class YamlProvisioner(
    ResourceLoggerService resourceLogger
)
{
    public async Task ProvisionYamlAsync(Yaml yaml, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}