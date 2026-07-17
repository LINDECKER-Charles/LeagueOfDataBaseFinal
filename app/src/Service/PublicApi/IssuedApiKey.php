<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

use App\Entity\ApiKey;

/**
 * Result of issuing a key: the persistable entity plus the raw secret. The raw
 * value exists only in this in-flight object — it is shown to the owner once
 * and never stored (api_keys keeps hash + display prefix only).
 */
final readonly class IssuedApiKey
{
    public function __construct(
        public ApiKey $key,
        #[\SensitiveParameter] public string $rawKey,
    ) {}
}
