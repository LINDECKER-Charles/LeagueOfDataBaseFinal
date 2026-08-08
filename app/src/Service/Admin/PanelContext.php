<?php
declare(strict_types=1);

namespace App\Service\Admin;

use Symfony\Component\HttpFoundation\Request;

/**
 * Query context shared by every deferred admin panel: the analytics time range,
 * the cache-busting flag behind the "Rafraîchir" buttons, and whether the caller
 * asked for the synchronous (no-JavaScript) rendering.
 *
 * The flags keep the name their query parameter and their template variable
 * already use, so the same idea is not called three things along the way.
 */
final readonly class PanelContext
{
    public function __construct(
        public string $range = '',
        public bool $shouldRefresh = false,
        public bool $isSync = false,
    ) {}

    public static function fromRequest(Request $request): self
    {
        return new self(
            (string) $request->query->get('range', ''),
            $request->query->getBoolean('refresh'),
            $request->query->getBoolean('sync'),
        );
    }
}
