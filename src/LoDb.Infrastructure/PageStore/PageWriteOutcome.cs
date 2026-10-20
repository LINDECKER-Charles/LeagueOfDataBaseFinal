namespace LoDb.Infrastructure.PageStore;

/// <summary>What a write left in the store.</summary>
public enum PageWriteOutcome
{
    /// <summary>The object now holds the body sent.</summary>
    Written,

    /// <summary>
    /// A create-only write (<see cref="PageObject.IsCreateOnly"/>) found the key already taken:
    /// the object kept is the one written first.
    /// </summary>
    AlreadyPresent,
}
