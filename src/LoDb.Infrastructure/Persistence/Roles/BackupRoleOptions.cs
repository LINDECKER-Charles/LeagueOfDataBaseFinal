namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>
/// The role of the host's off-host backups as <c>migrate</c> sets it
/// (<c>LoDb:Database:BackupRole</c>), checked when the host starts.
/// </summary>
/// <remarks>
/// The same password is given to infra-vps (<c>SAUVEGARDE_LODB_MOT_DE_PASSE</c>), whose
/// backup container signs in as <see cref="DatabaseRoleNames.Backup"/>. Staging sets none:
/// the role exists there without a password, and nobody can sign in as it.
/// </remarks>
public sealed class BackupRoleOptions
{
    public const string SectionName = "LoDb:Database:BackupRole";

    /// <summary>
    /// Sessions the role may hold at once: the dump of a pass and the deploy's check that the
    /// role answers, which infra-vps may run during a pass.
    /// </summary>
    public const int ConnectionLimit = 2;

    /// <summary>
    /// Password of the role, printable ASCII. Unset, <c>migrate</c> leaves the current one
    /// as it is, and a role it creates has none until a run sets it.
    /// </summary>
    public string? Password { get; set; }
}
