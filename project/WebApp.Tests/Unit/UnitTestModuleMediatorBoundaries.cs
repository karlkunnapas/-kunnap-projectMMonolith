namespace WebApp.Tests.Unit;

public class UnitTestModuleMediatorBoundaries
{
    [Fact]
    public void Modules_DoNotInjectOtherModuleApis()
    {
        var root = FindRepositoryRoot();
        var violations = new List<string>();

        AssertNoForbiddenApiUsage(root, "Modules.Users", ["ICompaniesModuleApi", "IChargingModuleApi"], violations);
        AssertNoForbiddenApiUsage(root, "Modules.Companies", ["IUsersModuleApi", "IChargingModuleApi"], violations);
        AssertNoForbiddenApiUsage(root, "Modules.Charging", ["IUsersModuleApi", "ICompaniesModuleApi"], violations);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static void AssertNoForbiddenApiUsage(
        string root,
        string moduleDirectory,
        IReadOnlyCollection<string> forbiddenNames,
        ICollection<string> violations)
    {
        var modulePath = Path.Combine(root, moduleDirectory);
        foreach (var file in Directory.EnumerateFiles(modulePath, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (var forbiddenName in forbiddenNames.Where(text.Contains))
            {
                violations.Add($"{Path.GetRelativePath(root, file)} references {forbiddenName}");
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "project.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
