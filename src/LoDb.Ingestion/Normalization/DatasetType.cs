using System.Text.Json.Serialization.Metadata;

namespace LoDb.Ingestion.Normalization;

/// <summary>
/// A dataset type: its storage name and the entries it holds. The four instances live in
/// <see cref="DatasetTypes"/>.
/// </summary>
public sealed class DatasetType<TEntry>
{
    internal DatasetType(string name, JsonTypeInfo<DatasetDocument<TEntry>> contract)
    {
        Name = name;
        Contract = contract;
    }

    /// <summary>The <c>{type}</c> segment of the dataset key ("champions").</summary>
    public string Name { get; }

    internal JsonTypeInfo<DatasetDocument<TEntry>> Contract { get; }

    public override string ToString() => Name;
}
