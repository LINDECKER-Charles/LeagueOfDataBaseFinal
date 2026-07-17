<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

use App\Entity\ApiKey;
use App\Entity\User;

/**
 * Mints API keys in the go-api contract shape: raw = "lodb_" + 40 lowercase
 * hex chars, stored as SHA-256 hex + a 12-char display prefix. Pure factory —
 * persistence stays with the caller.
 */
final class ApiKeyIssuer
{
    public const RAW_PREFIX = 'lodb_';
    /** 20 random bytes -> 40 hex chars, the exact secret length go-api validates. */
    private const SECRET_BYTES = 20;
    /** "lodb_" + first 7 hex chars — enough to recognise a key, useless to guess it. */
    private const DISPLAY_PREFIX_LENGTH = 12;
    private const DEFAULT_NAME = 'default';

    public function issue(User $user, string $name = ''): IssuedApiKey
    {
        $rawKey = self::RAW_PREFIX . bin2hex(random_bytes(self::SECRET_BYTES));

        $key = new ApiKey(
            $user,
            $this->normalizeName($name),
            hash('sha256', $rawKey),
            substr($rawKey, 0, self::DISPLAY_PREFIX_LENGTH),
        );

        return new IssuedApiKey($key, $rawKey);
    }

    /**
     * Rotates the secret without touching the entitlements: revokes $previous
     * (deliberate side effect — both keys must flush in the same transaction)
     * and returns its replacement carrying plan, quota, credits, rate limit
     * and Stripe ids. Single-active-key policy, v1.
     */
    public function regenerate(ApiKey $previous): IssuedApiKey
    {
        $issued = $this->issue($previous->getUser(), $previous->getName());
        $issued->key->carryEntitlementsFrom($previous);
        $previous->revoke();

        return $issued;
    }

    private function normalizeName(string $name): string
    {
        $name = trim($name);

        return $name === '' ? self::DEFAULT_NAME : mb_substr($name, 0, ApiKey::NAME_MAX_LENGTH);
    }
}
