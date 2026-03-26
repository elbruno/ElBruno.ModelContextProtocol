using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Extension methods for registering MCPToolRouter services in a dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IToolIndex"/> as a singleton with optional configuration.
    /// Tools can be added later via <see cref="IToolIndex.AddToolsAsync"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to configure <see cref="ToolIndexOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpToolRouter(
        this IServiceCollection services,
        Action<ToolIndexOptions>? configure = null)
    {
        return AddMcpToolRouter(services, Enumerable.Empty<Tool>(), configure);
    }

    /// <summary>
    /// Registers <see cref="IToolIndex"/> as a singleton with pre-defined tools and optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="tools">The initial MCP tool definitions to index.</param>
    /// <param name="configure">Optional callback to configure <see cref="ToolIndexOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpToolRouter(
        this IServiceCollection services,
        IEnumerable<Tool> tools,
        Action<ToolIndexOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(tools);

        services.AddSingleton<IToolIndex>(sp =>
        {
            var options = new ToolIndexOptions();
            configure?.Invoke(options);

            var generator = sp.GetService<IEmbeddingGenerator<string, Embedding<float>>>();

            var toolArray = tools.ToArray();
            if (toolArray.Length == 0)
            {
                return ToolIndex.CreateEmptyAsync(generator, options).GetAwaiter().GetResult();
            }

            return ToolIndex.CreateAsync(toolArray, generator, options).GetAwaiter().GetResult();
        });

        return services;
    }
}
