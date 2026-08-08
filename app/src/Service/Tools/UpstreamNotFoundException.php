<?php
declare(strict_types=1);

namespace App\Service\Tools;

/**
 * Thrown by {@see GoFetcherClient::fetch()} when Data Dragon answers with a
 * *definitive* absence status (403/404) for a JSON resource — as opposed to a
 * transient outage (5xx, timeout, transport error).
 *
 * Callers use it to degrade gracefully (empty dataset, language fallback)
 * instead of propagating an error, while still letting real outages bubble up:
 * a resource legitimately missing for a (version, language) — e.g.
 * runesReforged before patch 7.22, or a locale absent from an old patch — must
 * never break a page nor be mistaken for an incident.
 */
final class UpstreamNotFoundException extends \RuntimeException
{
}
