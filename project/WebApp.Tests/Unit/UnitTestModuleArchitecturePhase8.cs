using System.Text.RegularExpressions;

namespace WebApp.Tests.Unit;

public class UnitTestModuleArchitecturePhase8
{
    private static readonly string[] ModuleNames = ["Modules.Users", "Modules.Companies", "Modules.Charging"];

    [Fact]
    public void ModuleProjects_DoNotReferenceOtherModules()
    {
        var root = FindRepositoryRoot();
        var violations = new List<string>();

        foreach (var moduleName in ModuleNames)
        {
            var csprojPath = Path.Combine(root, moduleName, $"{moduleName}.csproj");
            var csproj = File.ReadAllText(csprojPath);

            foreach (var otherModuleName in ModuleNames.Where(x => !string.Equals(x, moduleName, StringComparison.Ordinal)))
            {
                if (csproj.Contains($"..\\{otherModuleName}\\", StringComparison.OrdinalIgnoreCase)
                    || csproj.Contains($"../{otherModuleName}/", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{moduleName} references {otherModuleName} in {Path.GetRelativePath(root, csprojPath)}");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Modules_DoNotImportOtherModuleNamespaces()
    {
        var root = FindRepositoryRoot();
        var violations = new List<string>();

        AssertNoForbiddenModuleUsings(root, "Modules.Users", ["using Modules.Companies", "using Modules.Charging"], violations);
        AssertNoForbiddenModuleUsings(root, "Modules.Companies", ["using Modules.Users", "using Modules.Charging"], violations);
        AssertNoForbiddenModuleUsings(root, "Modules.Charging", ["using Modules.Users", "using Modules.Companies"], violations);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ModuleClassConventions_AreInternalSealed_ForCoreTypes()
    {
        var root = FindRepositoryRoot();
        var violations = new List<string>();

        foreach (var moduleName in ModuleNames)
        {
            var modulePath = Path.Combine(root, moduleName);
            var files = Directory.EnumerateFiles(modulePath, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .ToList();

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var isCoreConventionTarget =
                    fileName.EndsWith("DbContext.cs", StringComparison.Ordinal)
                    || fileName.EndsWith("Repository.cs", StringComparison.Ordinal)
                    || fileName.EndsWith("Handler.cs", StringComparison.Ordinal)
                    || fileName.EndsWith("UnitOfWork.cs", StringComparison.Ordinal);
                if (fileName.StartsWith("I", StringComparison.Ordinal))
                {
                    isCoreConventionTarget = false;
                }

                var isDomainEntity = file.Contains($"{Path.DirectorySeparatorChar}Domain{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                     && fileName.EndsWith(".cs", StringComparison.Ordinal)
                                     && !fileName.StartsWith("I", StringComparison.Ordinal) // interfaces
                                     && !fileName.StartsWith("E", StringComparison.Ordinal) // enums
                                     && !fileName.Equals("LangStr.cs", StringComparison.Ordinal); // value object in shared contracts

                if (!isCoreConventionTarget && !isDomainEntity)
                {
                    continue;
                }

                // Identity root types are intentionally public for ASP.NET Identity wiring.
                if (fileName is "AppUser.cs" or "AppRole.cs")
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                if (!Regex.IsMatch(source, @"\binternal\s+sealed\s+class\s+\w+", RegexOptions.Multiline))
                {
                    violations.Add($"{Path.GetRelativePath(root, file)} should declare an internal sealed class.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static void AssertNoForbiddenModuleUsings(
        string root,
        string moduleDirectory,
        IReadOnlyCollection<string> forbiddenUsings,
        ICollection<string> violations)
    {
        var modulePath = Path.Combine(root, moduleDirectory);
        foreach (var file in Directory.EnumerateFiles(modulePath, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (var forbidden in forbiddenUsings.Where(text.Contains))
            {
                violations.Add($"{Path.GetRelativePath(root, file)} contains '{forbidden}'");
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
