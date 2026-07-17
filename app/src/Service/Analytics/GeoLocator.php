<?php
declare(strict_types=1);

namespace App\Service\Analytics;

use GeoIp2\Database\Reader;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\HttpFoundation\IpUtils;

/**
 * Resolves a client IP to a country via a local MaxMind/DB-IP `.mmdb` database
 * (GeoLite2-Country format). Everything degrades gracefully: if the geoip2
 * package isn't installed, the database file is absent, or the IP is private /
 * unresolvable, {@see locate()} returns null and the audience map simply shows
 * "unknown" — the rest of analytics is unaffected.
 *
 * Provisioning the database is an ops step (it can't ship in the repo): drop a
 * `GeoLite2-Country.mmdb` at var/geoip/ or point GEOIP_DB_PATH at one. See
 * docs/analytics.md.
 */
final class GeoLocator
{
    private const DEFAULT_RELATIVE_PATH = 'var/geoip/GeoLite2-Country.mmdb';

    private ?Reader $reader = null;
    private bool $resolved = false;
    /** @var array<string, array{code: string, name: string}|null> */
    private array $memo = [];

    public function __construct(
        #[Autowire(env: 'GEOIP_DB_PATH')]
        private readonly string $databasePath = '',
        #[Autowire('%kernel.project_dir%')]
        private readonly string $projectDir = '',
    ) {}

    public function isAvailable(): bool
    {
        return $this->reader() !== null;
    }

    /**
     * @return array{code: string, name: string}|null
     */
    public function locate(?string $ip): ?array
    {
        $ip = trim((string) $ip);
        if ($ip === '' || IpUtils::isPrivateIp($ip)) {
            return null;
        }

        return $this->memo[$ip] ??= $this->lookup($ip);
    }

    /**
     * @return array{code: string, name: string}|null
     */
    private function lookup(string $ip): ?array
    {
        $reader = $this->reader();
        if ($reader === null) {
            return null;
        }

        try {
            $country = $reader->country($ip)->country;
            $code = $country->isoCode;
            if ($code === null) {
                return null;
            }

            return ['code' => $code, 'name' => $country->name ?? $code];
        } catch (\Throwable) {
            // Address not in DB / invalid — treat as unknown, never bubble up.
            return null;
        }
    }

    private function reader(): ?Reader
    {
        if ($this->resolved) {
            return $this->reader;
        }
        $this->resolved = true;

        $path = $this->databasePath !== ''
            ? $this->databasePath
            : rtrim($this->projectDir, '/\\') . '/' . self::DEFAULT_RELATIVE_PATH;

        if (!class_exists(Reader::class) || !is_file($path)) {
            return null;
        }

        try {
            $this->reader = new Reader($path);
        } catch (\Throwable) {
            $this->reader = null;
        }

        return $this->reader;
    }
}
