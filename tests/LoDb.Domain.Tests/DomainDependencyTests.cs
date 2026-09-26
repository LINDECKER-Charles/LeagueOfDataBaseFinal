using System.Reflection;
using System.Runtime.InteropServices;

namespace LoDb.Domain.Tests;

/// <summary>
/// The domain stays pure: no package, no ASP.NET Core, no I/O library, only the base runtime.
/// </summary>
public sealed class DomainDependencyTests
{
    private const string DomainAssembly = "LoDb.Domain";
    private const string AssemblyExtension = ".dll";

    [Fact]
    public void DomainReferencesOnlyTheBaseRuntime()
    {
        var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
        var domain = Assembly.Load(DomainAssembly);

        var foreign = domain.GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(name => !File.Exists(Path.Combine(runtimeDirectory, name + AssemblyExtension)));

        Assert.Empty(foreign);
    }
}
