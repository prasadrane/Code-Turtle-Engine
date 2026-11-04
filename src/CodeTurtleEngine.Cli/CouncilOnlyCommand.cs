using System.CommandLine;
using CodeTurtleEngine.Council;
using CodeTurtleEngine.Llm;
using Microsoft.Extensions.DependencyInjection;

namespace CodeTurtleEngine.Cli;

internal static class CouncilOnlyCommand
{
    public static Command Create()
    {
        var analysis = new Option<string>("--analysis") { Required = true,
            Description = "Analyze response produced by the trusted isolated worker orchestrator" };
        var output = new Option<string>("--out") { Required = true,
            Description = "Fresh JSON file for unverified proposals; requires isolated Audit before publication" };
        var command = new Command("council", "Run grounded personas on isolated compiler facts without loading a repository");
        command.Options.Add(analysis);
        command.Options.Add(output);
        command.SetAction(async (parse, ct) =>
        {
            try
            {
                // Configuration is application-relative, never relative to the PR checkout.
                var services = Composition.BuildServices(AppContext.BaseDirectory);
                var llm = services.GetRequiredService<LlmOptions>();
                if (llm.Routes.Count == 0 || llm.Routes.Any(route =>
                        string.IsNullOrWhiteSpace(route.BaseUrlEnv) || string.IsNullOrWhiteSpace(route.ApiKeyEnv) ||
                        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(route.BaseUrlEnv)) ||
                        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(route.ApiKeyEnv))))
                    throw new InvalidOperationException("Configure the application LLM route credentials in environment variables.");
                await new CouncilOnlyPipeline(services.GetRequiredService<IPersonaRunner>(),
                    services.GetRequiredService<TurtleOptions>().Council)
                    .RunAsync(parse.GetValue(analysis)!, parse.GetValue(output)!, ct).ConfigureAwait(false);
                Console.Error.WriteLine("Unverified proposals written; isolated Audit is required before use.");
                return 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Provider exceptions and payload fields can contain untrusted text or secrets.
                Console.Error.WriteLine($"Council transport failed ({ex.GetType().Name}).");
                return 1;
            }
        });
        return command;
    }
}
