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
            ["Messages.cs"] = MessagesTemplate
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
            <PackageReference Include="EpochSim" Version="0.2.0" />
          </ItemGroup>

        </Project>
        """;

    private const string ProgramTemplate = """
        using EpochSim;
        using EpochSim.Kernel.Messaging;

        var state = new WorldState();
        var engine = Epoch.CreateEngine<WorldState>();

        engine.AddSystem(
            name: "PopulationSystem",
            tick: ctx =>
            {
                if (ctx.Time.Tick == 0)
                    ctx.Commands.Enqueue(new GrowCommand(10));
            },
            handle: (ctx, ev) =>
            {
                if (ev is PopulationChanged changed)
                    ctx.State.Population += changed.Delta;
            });

        engine.OnCommand<GrowCommand>((_, command, events) =>
        {
            events.Emit(new PopulationChanged(command.Delta));
        });

        var codec = Epoch.JsonCodecFromAssembly<Program>();
        var serializer = Epoch.JsonStateSerializer<WorldState>();

        using var run = Epoch.RecommendedRun(
            state,
            codec,
            serializer,
            rootDir: "artifacts");

        engine.Attach(run);
        engine.RunTicks(state, seed: 12345, endTickInclusive: 100);

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

    private const string MessagesTemplate = """
        using EpochSim.Kernel.Messaging;

        [MessageKind("Grow")]
        public sealed record GrowCommand(int Delta) : ICommand;

        public sealed record PopulationChanged(int Delta) : IEvent;
        """;
}
