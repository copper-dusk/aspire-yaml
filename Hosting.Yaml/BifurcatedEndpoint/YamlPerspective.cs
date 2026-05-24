namespace CopperDusk.Aspire.Hosting.Yaml.BifurcatedEndpoint;

/// <summary>
///     The runtime view a rendered YAML file is meant for. <see cref="Host"/> resolves endpoints
///     to <c>localhost</c> and the proxied host port (suitable for processes running directly on
///     the developer's machine). <see cref="Container"/> resolves endpoints to the service's
///     container-network DNS name and target (in-container) port — the view a sidecar or another
///     container on the same network would see.
/// </summary>
public enum YamlPerspective
{
    Host = 0,
    Container = 1,
}
