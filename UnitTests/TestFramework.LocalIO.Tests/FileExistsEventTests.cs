using TestFramework.Core;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFrameworkLocalIO;
using LocalIOFacade = TestFrameworkLocalIO.LocalIO;

namespace TestFramework.LocalIO.Tests;

public class FileExistsEventTests
{
    [Fact]
    public async Task OnSequentialPolling_ReturnsTrueAndConfiguredDelayWhenFileExists()
    {
        RuntimeContext runtime = RuntimeContext.Create();
        string path = Path.Combine(Path.GetTempPath(), $"exists-{Guid.NewGuid():N}.txt");
        VariableIdentifier pathIdentifier = new("path");
        VariableIdentifier delayIdentifier = new("delay");

        try
        {
            File.WriteAllText(path, "ready");
            runtime.VariableStore.SetVariable(pathIdentifier, path);
            runtime.VariableStore.SetVariable(delayIdentifier, TimeSpan.FromMilliseconds(125));

            FileExistsEvent fileExists = LocalIOFacade.Events.FileExists(Var.Ref<string>(pathIdentifier), Var.Ref<TimeSpan>(delayIdentifier));

            var result = await fileExists.OnSequentialPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

            Assert.True(result.IsDone);
            Assert.Equal(TimeSpan.FromMilliseconds(125), result.NextDelay);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task OnSequentialPolling_ThrowsWhenResolvedPathIsNull()
    {
        RuntimeContext runtime = RuntimeContext.Create();
        VariableIdentifier pathIdentifier = new("path");
        runtime.VariableStore.SetVariable<string?>(pathIdentifier, null);
        FileExistsEvent fileExists = LocalIOFacade.Events.FileExists(Var.Ref<string>(pathIdentifier));

        await Assert.ThrowsAsync<ArgumentNullException>(() => fileExists.OnSequentialPolling(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None));
    }

    [Fact]
    public void DeclareIO_AddsPathAndPollDelayMetadata()
    {
        FileExistsEvent fileExists = LocalIOFacade.Events.FileExists(Var.Ref<string>("path"), Var.Ref<TimeSpan>("delay"));
        StepIOContract contract = new();

        fileExists.DeclareIO(contract);

        Assert.Collection(
            contract.Inputs,
            entry =>
            {
                Assert.Equal("path", entry.Key);
                Assert.Equal(StepIOKind.Variable, entry.Kind);
                Assert.True(entry.Required);
                Assert.Equal(typeof(string), entry.DeclaredType);
            },
            entry =>
            {
                Assert.Equal("delay", entry.Key);
                Assert.Equal(StepIOKind.Variable, entry.Kind);
                Assert.False(entry.Required);
                Assert.Equal(typeof(TimeSpan), entry.DeclaredType);
            });
    }

    [Fact]
    public void LocalIOCmdFactory_UsesCurrentDirectoryAsDefaultWorkingDirectoryDeclaration()
    {
        CmdTrigger trigger = LocalIOFacade.Trigger.Cmd(Var.Ref<string>("cmd"));
        StepIOContract contract = new();

        trigger.DeclareIO(contract);

        Assert.Collection(
            contract.Inputs,
            entry =>
            {
                Assert.Equal("cmd", entry.Key);
                Assert.Equal(StepIOKind.Variable, entry.Kind);
                Assert.True(entry.Required);
                Assert.Equal(typeof(string), entry.DeclaredType);
            });
    }

    private sealed class RuntimeContext
    {
        public IServiceProvider ServiceProvider { get; } = new EmptyServiceProvider();
        public ScopedLogger Logger { get; } = new(null);
        public DebuggingRunSession DebuggingSession { get; } = new(EmptyRunDebugger.CreateNew());
        public VariableStore VariableStore { get; }
        public ArtifactStore ArtifactStore { get; }

        private RuntimeContext()
        {
            VariableStore = new VariableStore(Logger, DebuggingSession);
            ArtifactStore = new ArtifactStore(Logger, DebuggingSession);
        }

        public static RuntimeContext Create() => new();
    }
}