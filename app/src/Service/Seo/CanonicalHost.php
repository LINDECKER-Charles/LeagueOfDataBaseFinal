<?php
declare(strict_types=1);

namespace App\Service\Seo;

/**
 * Resolves the single host every indexable URL points to.
 *
 * The deployment answers on four domains (apex + www, .com + .fr) that serve
 * byte-identical content in the same language — the interface locale comes from
 * the visitor's selection, never from the domain. Without folding, each domain
 * would be a self-canonical copy of the whole site competing with the others,
 * splitting ranking signals across four duplicates of every page.
 *
 * Folding is opt-in per environment: with no preferred host configured every
 * request stays self-canonical, so dev and staging never advertise production.
 */
final class CanonicalHost
{
    private const WWW_PREFIX = 'www.';

    /**
     * @param string $preferredHost apex host all mirrors fold into; '' disables folding
     * @param list<string> $mirrorHosts apex hosts serving identical content
     */
    public function __construct(
        private readonly string $preferredHost,
        private readonly array $mirrorHosts,
    ) {}

    /**
     * Canonical form of a request host. The `www.` prefix always collapses (both
     * forms hit the same upstream); a known mirror additionally folds into the
     * preferred host. An unknown host is returned as-is — a local or preview
     * environment must canonicalize to itself.
     */
    public function resolve(string $host): string
    {
        $apex = $this->apex($host);

        if ($this->preferredHost === '') {
            return $apex;
        }

        return \in_array($apex, $this->mirrorHosts, true) ? $this->preferredHost : $apex;
    }

    /** Scheme + canonical host (+ port when non-default), no trailing slash. */
    public function origin(string $scheme, string $host, ?int $port = null): string
    {
        $origin = $scheme . '://' . $this->resolve($host);
        $isDefaultPort = $port === null
            || ($scheme === 'https' && $port === 443)
            || ($scheme === 'http' && $port === 80);

        return $isDefaultPort ? $origin : $origin . ':' . $port;
    }

    private function apex(string $host): string
    {
        return str_starts_with($host, self::WWW_PREFIX) ? substr($host, \strlen(self::WWW_PREFIX)) : $host;
    }
}
