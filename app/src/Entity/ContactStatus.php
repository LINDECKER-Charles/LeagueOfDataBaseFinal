<?php
declare(strict_types=1);

namespace App\Entity;

/**
 * Triage state of a contact submission in the /admin inbox. `New` = awaiting the
 * operator; `Handled` = resolved (kept as a record, not deleted). Spam is deleted
 * outright rather than transitioned.
 */
enum ContactStatus: string
{
    case New = 'new';
    case Handled = 'handled';

    public function label(): string
    {
        return match ($this) {
            self::New => 'Nouveau',
            self::Handled => 'Traité',
        };
    }

    public function tone(): string
    {
        return match ($this) {
            self::New => 'warn',
            self::Handled => 'good',
        };
    }
}
