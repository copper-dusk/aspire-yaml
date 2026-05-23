namespace CopperDusk.Aspire.Hosting.Yaml.BifurcatedEndpoint;

/// <summary>
///     Pairs two <see cref="IValueProvider"/>s — one for the host view and one for the container
///     view — into a single value the YAML resolver can switch between at render time. When read
///     through the plain <see cref="IValueProvider"/> surface (i.e. from anywhere outside this
///     library's renderer), the host branch wins, matching Aspire's general "default to localhost"
///     convention.
/// </summary>
public sealed class PerspectiveValue : IValueProvider, IManifestExpressionProvider, PerspectiveAware
{
    private readonly IValueProvider host;
    private readonly IValueProvider container;

    public PerspectiveValue(IValueProvider host, IValueProvider container)
    {
        this.host = host;
        this.container = container;
    }

    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default) =>
        host.GetValueAsync(cancellationToken);

    public ValueTask<string?> GetValueAsync(YamlPerspective perspective, CancellationToken cancellationToken) =>
        perspective switch
        {
            YamlPerspective.Container => container.GetValueAsync(cancellationToken),
            _ => host.GetValueAsync(cancellationToken),
        };

    /// <summary>
    ///     Manifest expression used when Aspire emits an aspire-manifest.json (or otherwise reads
    ///     the value statically). Defers to the host branch's manifest expression when it has one
    ///     so deployments see a sensible value; a static fallback covers branches that aren't
    ///     manifest-aware.
    /// </summary>
    public string ValueExpression => host is IManifestExpressionProvider provider
        ? provider.ValueExpression
        : "{perspective-aware}";
}
