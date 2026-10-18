using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TestFramework.LocalIO;

/// <summary>
/// Everything a command started, held so it can be taken down after the shell itself has exited.
/// </summary>
/// <remarks>
/// <para>
/// <c>Process.Kill(entireProcessTree: true)</c> walks the tree from a live root. <c>cmd /c start /b worker</c> exits
/// at once and leaves the worker behind, so by the time a step needed to stop it there was no root to walk from:
/// the old kill returned on <c>HasExited</c> and the worker outlived the run.
/// </para>
/// <para>
/// On Windows the shell is placed in a job object right after it starts; what it starts joins the job, and
/// terminating the job reaches all of it whether the shell is alive or not. The job is not kill-on-close, so a
/// child the command deliberately detached - output redirected, nothing held - outlives the step as it always did.
/// There is a window of a few milliseconds before the assignment in which a child started by the shell would be
/// missed; the shell is still loading in that window.
/// </para>
/// <para>
/// On Unix the shell is started through <c>setsid</c>, so it leads a process group of its own that its children
/// stay in, and the group is signalled as a whole. Without <c>setsid</c> - macOS ships none - this falls back to
/// killing the tree from the live shell.
/// </para>
/// </remarks>
internal sealed class CommandProcessTree : IDisposable
{
    private readonly Process _shell;
    private readonly IntPtr _job;
    private readonly bool _ownsGroup;

    private CommandProcessTree(Process shell, IntPtr job, bool ownsGroup)
    {
        _shell = shell;
        _job = job;
        _ownsGroup = ownsGroup;
    }

    /// <summary>
    /// The <c>setsid</c> executable the shell is started through on Unix, or <see langword="null"/> where there is none.
    /// </summary>
    public static string? SetSid { get; } = OperatingSystem.IsWindows()
        ? null
        : Array.Find(["/usr/bin/setsid", "/bin/setsid"], File.Exists);

    /// <summary>
    /// Takes ownership of a shell just started, and of what it goes on to start.
    /// </summary>
    /// <param name="shell">The started shell.</param>
    /// <param name="startedThroughSetSid">Whether the shell was started through <see cref="SetSid"/>.</param>
    public static CommandProcessTree Own(Process shell, bool startedThroughSetSid)
    {
        if (!OperatingSystem.IsWindows())
            return new CommandProcessTree(shell, IntPtr.Zero, startedThroughSetSid);

        IntPtr job = CreateJobObject(IntPtr.Zero, null);
        if (job != IntPtr.Zero && !AssignProcessToJobObject(job, shell.Handle))
        {
            // A shell already in a job that forbids nesting cannot join ours; the live-root kill still applies.
            CloseHandle(job);
            job = IntPtr.Zero;
        }

        return new CommandProcessTree(shell, job, ownsGroup: false);
    }

    /// <summary>
    /// Stops the shell and everything it started, alive root or not.
    /// </summary>
    /// <returns>
    /// Whether everything it started was stopped. Without a job or a process group only a still-running shell's
    /// own tree can be reached, and a child whose shell already exited cannot.
    /// </returns>
    public bool Kill()
    {
        if (_job != IntPtr.Zero)
            return TerminateJobObject(_job, 1);

        if (_ownsGroup && KillProcessGroup(_shell.Id))
            return true;

        try
        {
            if (_shell.HasExited)
                return false;

            _shell.Kill(entireProcessTree: true);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already gone, or not ours to kill; the caller says what it could not stop.
            return false;
        }
    }

    public void Dispose()
    {
        if (_job != IntPtr.Zero)
            CloseHandle(_job);
    }

    private static bool KillProcessGroup(int groupId)
    {
        // kill(-pgid, SIGKILL): the whole group, including members whose parent has already exited.
        const int SigKill = 9;
        return UnixKill(-groupId, SigKill) == 0;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int UnixKill(int pid, int signal);
}
