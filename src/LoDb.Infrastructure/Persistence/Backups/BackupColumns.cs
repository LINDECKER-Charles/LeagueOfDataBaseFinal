using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Backups;

/// <summary>
/// Stored spelling of the steps: the words the backup container of infra-vps writes
/// (<c>sauvegardes/scripts/commun.sh</c>), which the table's check constraint holds to, and
/// the admin shows.
/// </summary>
public static class BackupColumns
{
    public static readonly ValueConverter<BackupStep, string> StepConverter = new(
        step => ToText(step),
        value => ParseStep(value));

    public static string ToText(BackupStep step) => step switch
    {
        BackupStep.Backup => "backup",
        BackupStep.Retention => "retention",
        BackupStep.Restore => "restore",
        BackupStep.Check => "check",
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
    };

    public static BackupStep ParseStep(string value) => value switch
    {
        "backup" => BackupStep.Backup,
        "retention" => BackupStep.Retention,
        "restore" => BackupStep.Restore,
        "check" => BackupStep.Check,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
