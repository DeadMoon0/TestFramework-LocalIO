using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO;

public static class LocalIO
{
    public static LocalIOArtifacts Artifacts { get; } = new LocalIOArtifacts();
    public static LocalIOTrigger Trigger { get; } = new LocalIOTrigger();
    public static LocalIOEvents Events { get; } = new LocalIOEvents();
}

public class LocalIOArtifacts
{
    public FileArtifactKind FileKind => FileArtifactKind.Kind;

    public FileArtifactReference FileRef(VariableReference<string> path)
    {
        return new FileArtifactReference(path);
    }
}

public class LocalIOTrigger
{
    public CmdTrigger Cmd(VariableReference<string> command)
    {
        return new CmdTrigger(command, Environment.CurrentDirectory);
    }

    public CmdTrigger Cmd(VariableReference<string> command, VariableReference<string> workingDirectory)
    {
        return new CmdTrigger(command, workingDirectory);
    }
}

public class LocalIOEvents
{
    public FileExistsEvent FileExists(VariableReference<string> path)
    {
        return new FileExistsEvent(path);
    }

    public FileExistsEvent FileExists(VariableReference<string> path, VariableReference<TimeSpan> pollDelay)
    {
        return new FileExistsEvent(path, pollDelay);
    }
}