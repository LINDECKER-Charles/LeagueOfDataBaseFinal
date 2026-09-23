<?php

declare(strict_types=1);

namespace App\Service\Client;

/**
 * The visual identity the app paints itself with — four Runeterran cities, each
 * authored as a complete dark palette.
 *
 * They are not four recolourings of one design. Each answers the same question
 * — what is a panel's edge made of? — a different way, and that is what
 * separates them: Hextech cuts the corner into a gem facet (Piltover's Art
 * Deco), Zaun bends two corners into wrought iron (Art Nouveau), Noxus leaves
 * them square and stamps an edge (the banner's hoist), Spirit Blossom draws no
 * edge at all (a lit sheet of paper). Each also brings its own display face.
 *
 * The palettes live in assets/styles/theme/<slug>/; this enum is the
 * server-side half of the contract. It names the identities, validates whatever
 * the cookie claims to be, and carries what the chrome outside CSS needs.
 */
enum Theme: string
{
    case Hextech = 'hextech';
    case Zaun = 'zaun';
    case Noxus = 'noxus';
    case SpiritBlossom = 'spirit-blossom';

    /**
     * Read by PHP, WRITTEN BY JAVASCRIPT — hence deliberately kept out of the
     * signed `lod_prefs` cookie ({@see RememberPreferencesCookie}), which is
     * httpOnly and therefore unreachable from the picker. No HMAC either: the
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
            self::Noxus => 'Noxus',
            self::SpiritBlossom => 'Spirit Blossom',
        };
    }

    /**
     * Where the identity comes from, shown under its name in the picker. Also a
     * proper noun, and the only thing that tells a visitor why Hextech and Zaun
     * look like two halves of one argument.
     */
    public function origin(): string
    {
        return match ($this) {
            self::Hextech => 'Piltover',
            self::Zaun => 'Zaun',
            self::Noxus => 'Noxus',
            self::SpiritBlossom => 'Ionia',
        };
    }

    /**
     * The display face this identity loads, so the page can preload it. Null
     * for the default, whose faces are preloaded unconditionally by base.html.
     * Path only — the @font-face lives in the theme's own type.css.
     */
    public function displayFontUrl(): ?string
    {
        return match ($this) {
            self::Hextech => null,
            self::Zaun => '/fonts/grenze/latin.woff2',
            self::Noxus => '/fonts/archivo/latin.woff2',
            self::SpiritBlossom => '/fonts/shippori/latin-400.woff2',
        };
    }

    /**
     * Colour of the browser chrome (Android tab strip, iOS status bar, PWA
     * splash). Each theme paints its own deepest surface so the app melts into
     * the OS instead of framing itself with another identity's black.
     */
    public function browserColor(): string
    {
        return match ($this) {
            self::Hextech => '#010a13',
            self::Zaun => '#030706',
            self::Noxus => '#08090b',
            self::SpiritBlossom => '#07070e',
        };
    }
}
