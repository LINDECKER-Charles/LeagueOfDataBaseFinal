<?php
declare(strict_types=1);

namespace App\Service\Audit;

/** Coarse action grouping, used by the admin journal filter. */
enum AuditCategory: string
{
    case Auth = 'auth';
    case Account = 'account';
    case Build = 'build';
    case ApiKey = 'apikey';
    case Admin = 'admin';

    public function label(): string
    {
        return match ($this) {
            self::Auth => 'Authentification',
            self::Account => 'Compte',
            self::Build => 'Builds',
            self::ApiKey => 'Clés API',
            self::Admin => 'Administration',
        };
    }
}
