using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pontifex.Tests.Determinism;

/// <summary>
/// Static guard for the default Direct suite: a fixture that is not declared as integration must not poll wall
/// time or start an uncontrolled thread.
/// </summary>
[TestFixture]
public sealed class DirectTestDeterminismTests
{
    private static readonly Regex IntegrationCategory = new(
        "Category\\(\"(?:[^\"]*\\.)?Integration\"\\)", RegexOptions.Compiled);

    private static readonly Regex Fixture = new("\\[TestFixture\\]", RegexOptions.Compiled);

    private static readonly (string Name, Regex Pattern)[] Forbidden =
    {
        ("Thread.Sleep", new Regex("Thread\\.Sleep", RegexOptions.Compiled)),
        ("WaitFor", new Regex("\\bWaitFor\\s*\\(", RegexOptions.Compiled)),
        ("SpinWait", new Regex("\\bSpinWait\\b", RegexOptions.Compiled)),
        ("DateTime.UtcNow deadline", new Regex("DateTime\\.UtcNow", RegexOptions.Compiled)),
        ("Task.Delay", new Regex("Task\\.Delay", RegexOptions.Compiled)),
        ("Task.Run", new Regex("Task\\.Run", RegexOptions.Compiled)),
        ("new Thread", new Regex("new\\s+Thread\\s*\\(", RegexOptions.Compiled)),
        ("real timer", new Regex("new\\s+(?:System\\.Threading\\.)?Timer\\s*\\(", RegexOptions.Compiled)),
        ("timed monitor wait", new Regex("Monitor\\.Wait\\s*\\([^)]*,", RegexOptions.Compiled))
    };

    [Test]
    public void DefaultFixtures_DoNotPollWallClockTimeOrStartUnknownThreads()
    {
        string root = FindRepositoryRoot();
        string projectDirectory = Path.Combine(root, "Tests", "Pontifex.Transport.Direct.Tests");
        List<string> violations = new();

        foreach (string file in Directory.EnumerateFiles(projectDirectory, "*.cs",
                     SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(file);
            if (fullPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || fullPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            if (string.Equals(fullPath, Path.GetFullPath(
                    Path.Combine(projectDirectory, "DirectTestDeterminismTests.cs")),
                StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            if (!Fixture.IsMatch(text) || IntegrationCategory.IsMatch(text))
            {
                continue;
            }

            foreach ((string name, Regex pattern) in Forbidden)
            {
                if (pattern.IsMatch(text))
                {
                    violations.Add($"{Path.GetRelativePath(root, file)}: {name}");
                }
            }
        }

        Assert.That(violations, Is.Empty,
            "a default Direct fixture must use a scheduling seam or carry an integration category");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pontifex.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root was not found.");
    }
}
