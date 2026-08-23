<?php

declare(strict_types=1);

namespace App\Service\Client;

/**
 * The visual identity the app paints itself with — two halves of the same
 * Runeterran city, each authored as a complete dark palette. Hextech is
 * Piltover above: navy, arcane cyan, gold, light falling from the sky. Zaun is
 * the undercity below: green-black iron, chem green, Shimmer violet, light
 * rising from the Sump.
 *
 * The palettes themselves live in assets/styles/theme/; this enum is the
 * server-side half of the contract. It names the identities, validates whatever
 * the cookie claims to be, and carries the browser-chrome colour each one paints.
 */
enum Theme: string
{
    case Hextech = 'hextech';
    case Zaun = 'zaun';

    /**
     * Read by PHP, WRITTEN BY JAVASCRIPT — hence deliberately kept out of the
     * signed `lod_prefs` cookie ({@see RememberPreferencesCookie}), which is
     * httpOnly and therefore unreachable from the toggle. No HMAC either: the
     * value is validated against this closed enum on the way back in, so a
     * tampered cookie can do nothing worse than fall back to the default.
     * Signing a cookie the client is meant to write would be theatre.
     */
    public const COOKIE = 'lod_theme';

    /** A chosen identity is a lasting taste, not a session detail. */
    public const COOKIE_LIFETIME_DAYS = 365;

    /** The identity a visitor gets before ever expressing a preference. */
    public const DEFAULT = self::Hextech;

    /** Absent, unknown or tampered cookie → the default identity, never an error. */
    public static function fromCookie(?string $raw): self
    {
        return self::tryFrom((string) $raw) ?? self::DEFAULT;
    }

    /**
     * Display name. A proper noun, identical in all 21 locales — so it lives
     * here rather than in a translation catalogue nobody could translate.
     */
    public function label(): string
    {
        return match ($this) {
            self::Hextech => 'Hextech',
            self::Zaun => 'Zaun',
        };
    }

    /**
     * Colour of the browser chrome (Android tab strip, iOS status bar, PWA
     * splash). Each theme paints its own deepest surface so the app melts into
     * the OS instead of framing itself with the other identity's black.
     */
    public function browserColor(): string
    {
        return match ($this) {
            self::Hextech => '#010a13',
            self::Zaun => '#030706',
        };
    }
}
