<?php
declare(strict_types=1);

namespace App\Service\Analytics\Model;

/**
 * A resolved geolocation: ISO-3166 alpha-2 code plus the display name that goes
 * with it. The name falls back to the code when the database has no localised
 * label, so both fields are always populated.
 */
final readonly class Country
{
    public function __construct(
        public string $code,
        public string $name,
    ) {}
}
