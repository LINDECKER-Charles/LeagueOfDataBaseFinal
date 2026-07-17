<?php
declare(strict_types=1);

namespace App\Service\Audit;

/**
 * Closed set of auditable actions — the contract shared by every writer (the
 * controllers and the security listener) and the NDJSON line format. The string
 * value is what is persisted; keep them stable, dotted `domain.action` names.
 *
 * Deliberately excludes page views: those are anonymous, high-frequency, and
 * already captured by the analytics pipeline. The audit journal only records
 * state-changing / security-relevant actions tied to an actor.
 */
enum AuditAction: string
{
    // Authentication / security
    case UserLogin = 'user.login';
    case UserLoginFailed = 'user.login_failed';
    case UserLogout = 'user.logout';

    // Account lifecycle
    case UserRegister = 'user.register';
    case UserPasswordReset = 'user.password_reset';
    case UserEmailVerified = 'user.email_verified';
    case ProfileUpdate = 'profile.update';
    case AccountDelete = 'account.delete';

    // Builds
    case BuildCreate = 'build.create';
    case BuildUpdate = 'build.update';
    case BuildDelete = 'build.delete';
    case BuildVote = 'build.vote';

    // API keys (player-owned)
    case ApiKeyCreate = 'apikey.create';
    case ApiKeyRegenerate = 'apikey.regenerate';
    case ApiKeyRevoke = 'apikey.revoke';

    // Administration (operator accountability)
    case AdminUserBan = 'admin.user_ban';
    case AdminUserUnban = 'admin.user_unban';
    case AdminUserDelete = 'admin.user_delete';
    case AdminBuildHide = 'admin.build_hide';
    case AdminBuildDelete = 'admin.build_delete';
    case AdminApiClientRevoke = 'admin.api_client_revoke';
    case AdminApiClientCredit = 'admin.api_client_credit';
    case AdminAnalyticsRollup = 'admin.analytics_rollup';
    case AdminLogsPurge = 'admin.logs_purge';

    /** Coarse grouping used by the admin filter. Derived from the dotted prefix. */
    public function category(): AuditCategory
    {
        return match ($this) {
            self::UserLogin, self::UserLoginFailed, self::UserLogout => AuditCategory::Auth,
            self::UserRegister, self::UserPasswordReset, self::UserEmailVerified,
            self::ProfileUpdate, self::AccountDelete => AuditCategory::Account,
            self::BuildCreate, self::BuildUpdate, self::BuildDelete, self::BuildVote => AuditCategory::Build,
            self::ApiKeyCreate, self::ApiKeyRegenerate, self::ApiKeyRevoke => AuditCategory::ApiKey,
            default => AuditCategory::Admin,
        };
    }

    /** Operator-facing FR label for tables and timelines. */
    public function label(): string
    {
        return match ($this) {
            self::UserLogin => 'Connexion',
            self::UserLoginFailed => 'Échec de connexion',
            self::UserLogout => 'Déconnexion',
            self::UserRegister => 'Inscription',
            self::UserPasswordReset => 'Réinitialisation du mot de passe',
            self::UserEmailVerified => 'Adresse e-mail vérifiée',
            self::ProfileUpdate => 'Profil mis à jour',
            self::AccountDelete => 'Compte supprimé',
            self::BuildCreate => 'Build créé',
            self::BuildUpdate => 'Build modifié',
            self::BuildDelete => 'Build supprimé',
            self::BuildVote => 'Vote sur un build',
            self::ApiKeyCreate => 'Clé API créée',
            self::ApiKeyRegenerate => 'Clé API régénérée',
            self::ApiKeyRevoke => 'Clé API révoquée',
            self::AdminUserBan => 'Bannissement de compte',
            self::AdminUserUnban => 'Rétablissement de compte',
            self::AdminUserDelete => 'Suppression de compte',
            self::AdminBuildHide => 'Build dépublié',
            self::AdminBuildDelete => 'Build supprimé (modération)',
            self::AdminApiClientRevoke => 'Clé client API révoquée',
            self::AdminApiClientCredit => 'Crédit client API ajusté',
            self::AdminAnalyticsRollup => 'Consolidation analytics',
            self::AdminLogsPurge => 'Purge des journaux',
        };
    }
}
