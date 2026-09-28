namespace LoDb.Api.Tests.Cli;

/// <summary>
/// The runs of the API's entry point in <c>Development</c>, alone: the environment comes from
/// <c>ASPNETCORE_ENVIRONMENT</c>, which every host of the process would read meanwhile.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DevelopmentHostGroup
{
    public const string Name = "Development command host";
}
