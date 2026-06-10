using EpochSim.Cli.App;

namespace EpochSim.Cli.Commands;

public sealed class InitCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        var targetArg = args.Length > 0 ? args[0] : ctx.Root;
        var targetDir = Path.GetFullPath(string.IsNullOrWhiteSpace(targetArg) ? ctx.Root : targetArg);
        Directory.CreateDirectory(targetDir);

        var filesToCreate = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Program.cs"] = ProgramTemplate,
            ["WorldState.cs"] = WorldStateTemplate,
            ["SessionInboxExample.cs"] = SessionInboxTemplate
        };

        if (!Directory.EnumerateFiles(targetDir, "*.csproj").Any())
            filesToCreate["EpochSim.App.csproj"] = ProjectTemplate;

        var existingFiles = filesToCreate.Keys
            .Where(fileName => File.Exists(Path.Combine(targetDir, fileName)))
            .ToArray();

        if (existingFiles.Length > 0)
        {
            Console.Error.WriteLine("Scaffold aborted: files already exist:");
            foreach (var existingFile in existingFiles)
                Console.Error.WriteLine($"  {existingFile}");
            return 2;
        }

        foreach (var file in filesToCreate)
        {
            var path = Path.Combine(targetDir, file.Key);
            File.WriteAllText(path, file.Value);
            Console.WriteLine($"Created: {path}");
        }

        Console.WriteLine();
        Console.WriteLine($"Scaffold ready in: {targetDir}");
        Console.WriteLine("Next steps:");
        Console.WriteLine("  dotnet restore");
        Console.WriteLine("  dotnet run");
        return 0;
    }

    private const string ProjectTemplate = """
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="EpochSim" Version="0.2.2" />
          </ItemGroup>

        </Project>
        """;

    private const string ProgramTemplate = """
        using EpochSim;

        var state = new WorldState();
        var engine = Epoch.CreateEngine<WorldState>();

        engine.AddSystem("World", tick: ctx => ctx.State.Population++);

        using var run = Epoch.QuickRun(state, rootDir: "artifacts");

        run.RunTicks(engine, seed: 1, endTickInclusive: 100);

        Console.WriteLine($"RunId={run.RunId}");
        Console.WriteLine($"RunDir={run.Paths.RunDir}");
        Console.WriteLine($"Population={state.Population}");
        """;

    private const string WorldStateTemplate = """
        public sealed class WorldState
        {
            public int Population { get; set; }
        }
        """;

    private const string SessionInboxTemplate = """
        using EpochSim;
        using EpochSim.Kernel.Messaging;

        public static class SessionInboxExample
        {
            public static int RunInteractiveOnce()
            {
                var state = new WorldState();
                var engine = Epoch.CreateEngine<WorldState>();
                var inbox = new CommandInbox();

                engine.AttachInbox(inbox);
                engine.OnCommand<AddPopulation>((s, cmd, _) => s.Population += cmd.Delta);

                using var session = engine.CreateSession(state, seed: 1, startTick: 0);
                inbox.Enqueue(new AddPopulation(5));
                session.TickOnce();

                return state.Population;
            }

            [MessageKind("AddPopulation")]
            private sealed record AddPopulation(int Delta) : ICommand;
        }
        """;
}
