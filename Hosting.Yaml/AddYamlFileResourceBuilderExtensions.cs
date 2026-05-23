using CopperDusk.Aspire.Hosting.Yaml.BifurcatedEndpoint;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CopperDusk.Aspire.Hosting.Yaml;

public static class AddYamlFileResourceBuilderExtensions
{
    public static IResourceBuilder<YamlSourceResource> AddYamlFile(
        this IDistributedApplicationBuilder builder,
        string name,
        string content,
        string? fileName = null
    )
    {
        var trimmed = content.TrimStart();
        YamlSource source = trimmed.StartsWith('{') || trimmed.StartsWith('[')
            ? new RawJsonSource(content)
            : new RawYamlSource(content);

        return builder.AddYamlCore(name, source, fileName);
    }

    public static IResourceBuilder<YamlSourceResource> AddYamlFile(
        this IDistributedApplicationBuilder builder,
        string name,
        object content,
        string? fileName = null
    )
    {
        // Already a sequence of sources — route straight to multi-document so each entry
        // keeps its own rendering strategy instead of being reflected over as a plain object.
        if (content is IEnumerable<YamlSource> sources)
        {
            return builder.AddYamlCore(name, new MultiDocumentYamlSource(sources.ToList()), fileName);
        }

        return builder.AddYamlCore(name, new ObjectYamlSource(content), fileName);
    }

    /// <summary>
    ///     Collection-expression-friendly overload: callers can write
    ///     <c>builder.AddYaml("name", [sourceA, sourceB])</c> to bundle pre-built sources into a
    ///     single multi-document YAML file without going through the <see cref="object"/> overload's
    ///     runtime type check.
    /// </summary>
    public static IResourceBuilder<YamlSourceResource> AddYamlFile(
        this IDistributedApplicationBuilder builder,
        string name,
        IEnumerable<YamlSource> documents,
        string? fileName = null
    )
    {
        return builder.AddYamlCore(name, new MultiDocumentYamlSource(documents.ToList()), fileName);
    }

    internal static IResourceBuilder<YamlSourceResource> AddYamlCore(
        this IDistributedApplicationBuilder builder,
        string name,
        YamlSource source,
        string? fileName
    )
    {
        var currentYaml = builder.EnsureYaml();

        var resource = new YamlSourceResource
        {
            Name = name,
            Source = source,
            FileName = fileName ?? $"{name}.yaml",
        };

        var resourceBuilder = builder
            .AddResource(resource)
            .WithIconName("DocumentBulletList")
            .WithParentRelationship(currentYaml)
        ;

        resourceBuilder.OnInitializeResource(async (res, initializeResourceEvent, cancellationToken) =>
        {
            var eventing = initializeResourceEvent.Eventing;
            var notifications = initializeResourceEvent.Notifications;
            var services = initializeResourceEvent.Services;

            var provisioner = services.GetRequiredService<YamlProvisioner>();
            var logger = services.GetRequiredService<ResourceLoggerService>().GetLogger(res);

            try
            {
                var hostRendered = await provisioner.RenderContentAsync(res, YamlPerspective.Host, cancellationToken);
                await File.WriteAllTextAsync(res.HostOutputPath, hostRendered, cancellationToken);
                logger.LogInformation("Rendered {FileName} (host) to {OutputPath}:\n{Content}", res.FileName, res.HostOutputPath, hostRendered);

                var containerRendered = await provisioner.RenderContentAsync(res, YamlPerspective.Container, cancellationToken);
                await File.WriteAllTextAsync(res.ContainerOutputPath, containerRendered, cancellationToken);
                logger.LogInformation("Rendered {FileName} (container) to {OutputPath}:\n{Content}", res.FileName, res.ContainerOutputPath, containerRendered);

                await eventing.PublishAsync(new BeforeResourceStartedEvent(res, services), cancellationToken);

                await notifications.PublishUpdateAsync(res, previous => previous with
                {
                    State = new(KnownResourceStates.Running, KnownResourceStateStyles.Success),
                });

                await eventing.PublishAsync(new ResourceReadyEvent(res, services), cancellationToken);

                await notifications.PublishUpdateAsync(res, previous => previous with
                {
                    State = new(KnownResourceStates.Finished, KnownResourceStateStyles.Success),
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to render YAML for resource {ResourceName}.", res.Name);

                await notifications.PublishUpdateAsync(res, previous => previous with
                {
                    State = new(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error),
                });
            }
        });

        return resourceBuilder;
    }
}
