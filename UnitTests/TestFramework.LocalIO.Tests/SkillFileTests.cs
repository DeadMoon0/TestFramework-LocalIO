using System.Text.RegularExpressions;

namespace TestFramework.LocalIO.Tests;

public class SkillFileTests
{
    [Fact]
    public void GroundingFiles_AllExistOnDisk()
    {
        string repositoryRoot = FindRepositoryRoot();
        string skillPath = Path.Combine(repositoryRoot, "AI", "TestFramework.LocalIO.SKILL.md");
        Assert.True(File.Exists(skillPath), $"The skill file is missing at \"{skillPath}\".");

        string[] groundingFiles = ReadListedFiles(File.ReadAllText(skillPath), "grounding_files");
        Assert.NotEmpty(groundingFiles);

        string[] missing = groundingFiles
            .Where(relativePath => !File.Exists(Path.Combine(repositoryRoot, relativePath)))
            .ToArray();

        Assert.True(missing.Length == 0, $"The skill file grounds on files that do not exist: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Sources_AllExistOnDisk()
    {
        string repositoryRoot = FindRepositoryRoot();
        string skillPath = Path.Combine(repositoryRoot, "AI", "TestFramework.LocalIO.SKILL.md");

        string[] sources = ReadListedFiles(File.ReadAllText(skillPath), "sources");
        Assert.NotEmpty(sources);

        string[] missing = sources
            .Where(relativePath => !File.Exists(Path.Combine(repositoryRoot, relativePath)))
            .ToArray();

        Assert.True(missing.Length == 0, $"The skill file cites sources that do not exist: {string.Join(", ", missing)}");
    }

    private static string[] ReadListedFiles(string skill, string elementName)
    {
        Match block = Regex.Match(skill, $"<{elementName}>(?<body>.*?)</{elementName}>", RegexOptions.Singleline);
        Assert.True(block.Success, $"The skill file has no <{elementName}> block.");

        return block.Groups["body"].Value
            .Split('\n')
            .Select(line => line.Trim().TrimStart('-').Trim())
            .Where(line => line.Length > 0 && line.Contains('.') && !line.EndsWith(':'))
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TestFramework.LocalIO.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
