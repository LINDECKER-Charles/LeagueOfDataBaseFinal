<?php
declare(strict_types=1);

namespace App\Service\Audit;

use App\Entity\User;

/**
 * The object an audited action operates on. Kept as a small value object so the
 * {@see AuditLogger::log()} signature stays within the parameter budget instead
 * of threading type/id/label separately through every call site.
 */
final readonly class AuditTarget
{
    public const TYPE_USER = 'user';
    public const TYPE_BUILD = 'build';
    public const TYPE_API_KEY = 'apikey';
    public const TYPE_API_CLIENT = 'api_client';

    public function __construct(
        public string $type,
        public ?string $id,
        public ?string $label,
    ) {}

    public static function of(string $type, int|string|null $id, ?string $label = null): self
    {
        return new self($type, $id === null ? null : (string) $id, $label);
    }

    public static function user(User $user): self
    {
        return new self(self::TYPE_USER, (string) $user->getId(), $user->displayName());
    }
}
