<?php
declare(strict_types=1);

namespace App\Service\Audit;

/** Result of an audited action. `denied` = authorization/CSRF refusal. */
enum AuditOutcome: string
{
    case Success = 'success';
    case Failure = 'failure';
    case Denied = 'denied';

    public function label(): string
    {
        return match ($this) {
            self::Success => 'Succès',
            self::Failure => 'Échec',
            self::Denied => 'Refusé',
        };
    }
}
