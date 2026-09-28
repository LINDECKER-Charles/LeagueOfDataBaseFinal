using System.Reflection;

namespace LoDb.Api.Hosting;

/// <summary>
/// Types placed under a root folder of an assembly, such as <c>Cli</c> or <c>Workers</c>.
/// </summary>
/// <remarks>
/// Discovery by folder lets a chantier add a command or a background task in its own zone
/// folder, without any shared registration list to edit.
/// </remarks>
internal static class ConventionNamespace
{
    public static IEnumerable<Type> TypesUnder(Assembly assembly, string folder)
    {
        var root = $"{assembly.GetName().Name}.{folder}";
        return assembly.GetTypes().Where(type => IsUnder(type.Namespace, root));
    }

    private static bool IsUnder(string? typeNamespace, string root) =>
        typeNamespace is not null
        && (string.Equals(typeNamespace, root, StringComparison.Ordinal)
            || typeNamespace.StartsWith(root + ".", StringComparison.Ordinal));
}
