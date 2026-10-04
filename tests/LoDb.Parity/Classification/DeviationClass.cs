namespace LoDb.Parity.Classification;

/// <summary>The three verdicts L1.8 allows a deviation.</summary>
public enum DeviationClass
{
    /// <summary>The new stack is wrong: to fix before the lot closes.</summary>
    NextDefect,

    /// <summary>The legacy stack is wrong and the new one fixes it on purpose.</summary>
    LegacyDefect,

    /// <summary>Both are right: a difference of design, justified.</summary>
    Expected,
}
