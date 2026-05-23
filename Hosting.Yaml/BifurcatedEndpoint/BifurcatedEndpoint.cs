namespace CopperDusk.Aspire.Hosting.Yaml.BifurcatedEndpoint;

/// <summary>
///     A perspective-aware view over an <see cref="EndpointReference"/>. Each property returns a
///     <see cref="PerspectiveValue"/> that holds both the host-side and container-side forms of
///     that piece of the endpoint, so the same interpolated template can render correctly for
///     either consumer.
/// </summary>
public sealed class BifurcatedEndpoint
{
    private readonly EndpointReference endpoint;

    internal BifurcatedEndpoint(EndpointReference endpoint)
    {
        this.endpoint = endpoint;
    }

    /// <summary>
    ///     Hostname for reaching the endpoint. Host view returns the proxied
    ///     <see cref="EndpointProperty.Host"/>; container view returns the resource name, which
    ///     Aspire wires up as the in-network DNS name for the resource.
    /// </summary>
    public PerspectiveValue Host => new(
        host: endpoint.Property(EndpointProperty.Host),
        container: ReferenceExpression.Create($"{endpoint.Resource.Name}")
    );

    /// <summary>
    ///     Port for reaching the endpoint. Host view returns the proxied
    ///     <see cref="EndpointProperty.Port"/>; container view returns the in-container
    ///     <see cref="EndpointProperty.TargetPort"/>.
    /// </summary>
    public PerspectiveValue Port => new(
        host: endpoint.Property(EndpointProperty.Port),
        container: endpoint.Property(EndpointProperty.TargetPort)
    );

    /// <summary>
    ///     Combined <c>host:port</c> for the active perspective.
    /// </summary>
    public PerspectiveValue Address => new(
        host: ReferenceExpression.Create($"{endpoint.Property(EndpointProperty.Host)}:{endpoint.Property(EndpointProperty.Port)}"),
        container: ReferenceExpression.Create($"{endpoint.Resource.Name}:{endpoint.Property(EndpointProperty.TargetPort)}")
    );
}

public static class BifurcatedEndpointExtensions
{
    /// <summary>
    ///     Mirrors Aspire's <c>GetEndpoint</c> but returns a <see cref="BifurcatedEndpoint"/> so
    ///     the endpoint's pieces can be interpolated into a YAML-bound <see cref="ReferenceExpression"/>
    ///     and resolve to the right view (host vs. container) at render time.
    /// </summary>
    public static BifurcatedEndpoint GetEndpointForYaml<T>(this IResourceBuilder<T> builder, string name)
        where T : IResourceWithEndpoints
        => new(builder.GetEndpoint(name));

    /// <inheritdoc cref="GetEndpointForYaml{T}(IResourceBuilder{T},string)"/>
    public static BifurcatedEndpoint GetEndpointForYaml(this IResourceWithEndpoints resource, string endpointName)
        => new(resource.GetEndpoint(endpointName));
}
