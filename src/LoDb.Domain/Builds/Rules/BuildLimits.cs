namespace LoDb.Domain.Builds.Rules;

/// <summary>The bounds of a build, shared by the server rules and the editor.</summary>
public static class BuildLimits
{
    public const int NameMin = 3;
    public const int NameMax = 80;
    public const int DescriptionMax = 2000;

    public const int StepsMin = 1;
    public const int StepsMax = 10;
    public const int StepLabelMax = 40;
    public const int StepNoteMax = 300;
    public const int ItemsPerStepMin = 1;
    public const int ItemsPerStepMax = 8;

    /// <summary>Across every step: duplicates count, each purchase being one.</summary>
    public const int TotalItemsMax = 40;

    /// <summary>The picks of the primary tree: one per slot, the keystone included.</summary>
    public const int PrimaryPicks = 4;

    public const int SecondaryPicks = 2;
    public const int KeystoneSlot = 0;

    /// <summary>The first slot a secondary pick may come from: never the keystone row.</summary>
    public const int FirstMinorSlot = KeystoneSlot + 1;
}
