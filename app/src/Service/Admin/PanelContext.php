<?php
declare(strict_types=1);

namespace App\Service\Admin;

use Symfony\Component\HttpFoundation\Request;

/**
 * Query context shared by every deferred admin panel: the analytics time range,
 * the cache-busting flag behind the "Rafraîchir" buttons, and whether the caller
 * asked for the synchronous (no-JavaScript) rendering.
 */
final readonly class PanelContext
{
    public function __construct(
        public string $range = '',
        public bool $fresh = false,
        public bool $sync = false,
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
