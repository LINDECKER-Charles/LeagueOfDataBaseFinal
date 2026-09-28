using LoDb.Parity.Deviations;

namespace LoDb.Parity.Classification;

/// <summary>A deviation and the first rule that matches it, or none.</summary>
public sealed record ClassifiedDeviation(Deviation Deviation, DeviationRule? Rule);
