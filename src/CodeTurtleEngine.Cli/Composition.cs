using CodeTurtleEngine.Council;
using CodeTurtleEngine.Gatekeeper;
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTurtleEngine.Cli;

public static class Composition
{
    public static IServiceProvider BuildServices(string repoPath)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var llm = config.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        var turtle = config.GetSection(TurtleOptions.SectionName).Get<TurtleOptions>() ?? new TurtleOptions();
        var home = Environment.GetEnvironmentVariable("TURTLE_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".turtle");

        var sc = new ServiceCollection();
        sc.AddSingleton(llm);
        sc.AddSingleton(turtle);
        sc.AddSingleton<IChatClientFactory, BailianChatClientFactory>();
        sc.AddSingleton<ILlmGateway>(sp => new LlmGateway(sp.GetRequiredService<LlmOptions>(), sp.GetRequiredService<IChatClientFactory>()));
        sc.AddSingleton<IStructuredChatClient>(sp => new StructuredChatClient(
            sp.GetRequiredService<ILlmGateway>(), sp.GetRequiredService<LlmOptions>().JsonRetries));
        sc.AddSingleton<IDiffProvider, GitDiffProvider>();
        sc.AddSingleton<ICompilationLoader, MsBuildCompilationLoader>();
        sc.AddSingleton<IPersonaRunner>(sp => new PersonaRunner(sp.GetRequiredService<IStructuredChatClient>(), sp.GetRequiredService<TurtleOptions>().Council));
        sc.AddSingleton<IArbiter, Arbiter>();
        sc.AddSingleton<ITurtleShellGuard>(sp => new TurtleShellGuard(sp.GetRequiredService<TurtleOptions>().Council));
        sc.AddSingleton<RubricLoader>();
        sc.AddSingleton(new ArtifactWriter(home));
        sc.AddSingleton<ReviewPipeline>();
        return sc.BuildServiceProvider();
    }

    public static string DiscoverProject(string repoPath)
    {
        var sln = Directory.EnumerateFiles(repoPath, "*.sln").FirstOrDefault();
        if (sln is not null) return sln;
        var csproj = Directory.EnumerateFiles(repoPath, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
        return csproj ?? throw new DiffException($"No .sln or .csproj found under '{repoPath}'.");
    }
}
