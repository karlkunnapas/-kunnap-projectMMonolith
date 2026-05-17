namespace WebApp.Tests.Unit;

public class UnitTestModuleUnitOfWorkBoundaries
{
    [Theory]
    [InlineData("Modules.Users", "IUsersUnitOfWork", "UsersUnitOfWork")]
    [InlineData("Modules.Companies", "ICompaniesUnitOfWork", "CompaniesUnitOfWork")]
    [InlineData("Modules.Charging", "IChargingUnitOfWork", "ChargingUnitOfWork")]
    public void Modules_DefineAndRegisterUnitOfWork(string moduleDirectory, string interfaceName, string implementationName)
    {
        var root = FindRepositoryRoot();
        var modulePath = Path.Combine(root, moduleDirectory);
        var files = Directory.EnumerateFiles(modulePath, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        var combinedSource = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.Contains($"interface {interfaceName}", combinedSource);
        Assert.Contains($"class {implementationName}", combinedSource);
        Assert.Contains($"AddScoped<{interfaceName}, {implementationName}>", combinedSource);
        Assert.Contains(interfaceName, combinedSource);
        Assert.Contains(".SaveChangesAsync(", combinedSource);
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
