<?php

declare(strict_types=1);

namespace App\Service\Client;

use Symfony\Component\HttpFoundation\Cookie;
use Symfony\Component\HttpFoundation\Request;

/**
 * Wire format of the `lod_prefs` preference cookie:
 * `base64(json) . '|' . hmac_sha256(json, secret)`.
 *
 * Sole owner of that format and of the signing key, so a payload can never be
 * signed with one rule and verified with another. The HMAC guarantees integrity,
 * NOT confidentiality — the payload travels in clear, so it must carry nothing
 * sensitive. Reading is total: an absent, malformed or tampered cookie yields
 * null rather than an error, because a bad cookie must never break a request.
 */
final readonly class RememberPreferencesCookie
{
    /** Documented to visitors in the cookie policy page — renaming it is a legal change. */
    public const NAME = 'lod_prefs';

    /**
     * Key of last resort when %kernel.secret% is empty. Signing and verification
     * MUST use the same value — hence a single constant, never a repeated
     * literal (a divergence would make emitted signatures unverifiable).
     */
    private const FALLBACK_SECRET = 'fallback-secret';

    private const SECONDS_PER_DAY = 86400;

    public function __construct(#[\SensitiveParameter] private string $secret = '') {}

    /**
     * @param string      $locale  locale to remember, e.g. "fr_FR"
     * @param string|null $version Data Dragon version to remember, e.g. "14.16.1"
     * @param int         $days    lifetime in days
     */
    public function create(string $locale, ?string $version, int $days): Cookie
    {
        $json = (string) json_encode(['l' => $locale, 'v' => $version], JSON_UNESCAPED_SLASHES);

        return Cookie::create(
            name: self::NAME,
            value: base64_encode($json) . '|' . $this->sign($json),
            expire: time() + ($days * self::SECONDS_PER_DAY),
            path: '/',
            secure: true,
            httpOnly: true,
            sameSite: 'lax'
        );
    }

    /**
     * @return array{l?: string, v?: ?string}|null validated payload, or null when
     *         absent or tampered with
     */
    public function read(Request $request): ?array
    {
        $raw = $request->cookies->get(self::NAME);
        if (!is_string($raw) || !str_contains($raw, '|')) {
            return null;
        }

        [$encodedPayload, $signature] = explode('|', $raw, 2);
        $json = base64_decode($encodedPayload, true);
        if ($json === false || !hash_equals($this->sign($json), $signature)) {
            return null;
        }

        $payload = json_decode($json, true);

        return is_array($payload) ? $payload : null;
    }

    private function sign(string $json): string
    {
        return hash_hmac('sha256', $json, $this->secret ?: self::FALLBACK_SECRET);
    }
}
