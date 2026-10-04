namespace LoDb.Parity.Deviations;

/// <summary>How the two stacks differ at one place.</summary>
public enum DeviationKind
{
    /// <summary>The language a dataset is written in, fallback included.</summary>
    ContentLanguage,

    /// <summary>An entry, list element or manifest key the new stack lacks.</summary>
    OnlyInLegacy,

    /// <summary>An entry, list element or manifest key the legacy stack lacks.</summary>
    OnlyInNext,

    /// <summary>The same elements, in another order.</summary>
    Order,

    /// <summary>The same place, another value: a field, an image verdict, a manifest row.</summary>
    Value,
}
