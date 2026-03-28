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

    /// <summary>
    /// Registers both <see cref="IToolIndex"/> and <see cref="ToolRouter"/> as singletons with
    /// prompt distillation support via an <see cref="IChatClient"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="tools">The initial MCP tool definitions to index for routing.</param>
    /// <param name="chatClient">
    /// Optional chat client for prompt distillation. When provided and
    /// <see cref="ToolRouterOptions.EnableDistillation"/> is true, user prompts are
    /// distilled into single-sentence intents before semantic search.
    /// </param>
    /// <param name="configure">Optional callback to configure <see cref="ToolRouterOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpToolRouter(
        this IServiceCollection services,
        IEnumerable<Tool> tools,
        IChatClient? chatClient,
        Action<ToolRouterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(tools);

        var routerOptions = new ToolRouterOptions();
        configure?.Invoke(routerOptions);

        // Register IToolIndex (same pattern as existing overloads)
        var indexOptions = routerOptions.IndexOptions ?? new ToolIndexOptions();
        services.AddSingleton<IToolIndex>(sp =>
        {
            var generator = sp.GetService<IEmbeddingGenerator<string, Embedding<float>>>();

            var toolArray = tools.ToArray();
            if (toolArray.Length == 0)
            {
                return ToolIndex.CreateEmptyAsync(generator, indexOptions).GetAwaiter().GetResult();
            }

            return ToolIndex.CreateAsync(toolArray, generator, indexOptions).GetAwaiter().GetResult();
        });

        // Register ToolRouter wrapping the IToolIndex
        services.AddSingleton(sp =>
        {
            var index = sp.GetRequiredService<IToolIndex>();
            return ToolRouter.FromIndex(index, chatClient, routerOptions);
        });

        return services;
    }
}
