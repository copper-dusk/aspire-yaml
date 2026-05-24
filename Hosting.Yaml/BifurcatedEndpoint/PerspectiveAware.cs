namespace CopperDusk.Aspire.Hosting.Yaml.BifurcatedEndpoint;

/// <summary>
///     A value that resolves differently depending on whether the rendered YAML is destined for
///     the host process or a container. The YAML resolver checks for this interface before
///     falling through to the standard <see cref="IValueProvider"/> path, so types that
///     implement it can return per-perspective text without needing the caller to know in
///     advance which perspective is in play.
/// </summary>
public interface PerspectiveAware
{
    ValueTask<string?> GetValueAsync(YamlPerspective perspective, CancellationToken cancellationToken);
}
