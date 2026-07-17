<?php
declare(strict_types=1);

namespace App\Service\Analytics;

use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;

/**
 * Builds a {@see RequestEvent} from the request/response at kernel.terminate.
 * Returns null for anything that isn't a public resource page view (API, admin,
 * setup, SSE, non-GET) — the whitelist is the route name, which is far more
 * robust than path matching and naturally excludes everything else.
 *
 * It reads only the request and query — never the session — so logging a view
 * never force-starts a session (mirroring ClientManager::getSelectedLocale()).
 */
final class RequestEventFactory
{
    /** Loggable route name => [resource type, page kind]. */
    private const ROUTES = [
        'app_home' => ['home', 'home'],
        'app_champions' => ['champion', 'list'],
        'app_champion' => ['champion', 'detail'],
        'app_items' => ['item', 'list'],
        'app_item' => ['item', 'detail'],
        'app_runes' => ['runesReforged', 'list'],
        'app_rune' => ['runesReforged', 'detail'],
        'app_summoners' => ['summoner', 'list'],
        'app_summoner' => ['summoner', 'detail'],
    ];

    public function __construct(
        private readonly UserAgentParser $userAgents,
        private readonly RefererClassifier $referers,
        private readonly GeoLocator $geo,
        #[Autowire('%kernel.secret%')]
        private readonly string $appSecret,
    ) {}

    public function fromRequestResponse(Request $request, Response $response): ?RequestEvent
    {
        if ($request->getMethod() !== 'GET') {
            return null;
        }

        $route = (string) $request->attributes->get('_route');
        $mapping = self::ROUTES[$route] ?? null;
        if ($mapping === null) {
            return null;
        }

        [$type, $kind] = $mapping;
        $ip = $request->getClientIp();
        $userAgent = $request->headers->get('User-Agent');
        $ua = $this->userAgents->parse($userAgent);
        $referer = $this->referers->classify($request->headers->get('Referer'), $request->getHost());
        $country = $this->geo->locate($ip);

        return new RequestEvent(
            at: new \DateTimeImmutable('now', new \DateTimeZone('UTC')),
            route: $route,
            path: $request->getPathInfo(),
            type: $type,
            kind: $kind,
            entity: $kind === 'detail' ? $this->entity($request) : null,
            status: $response->getStatusCode(),
            version: $this->queryString($request, 'version'),
            lang: $this->queryString($request, 'lang'),
            locale: $request->getLocale(),
            ip: $ip,
            visitorId: $this->visitorId($ip, $userAgent),
            userAgent: $userAgent,
            browser: $ua['browser'],
            os: $ua['os'],
            device: $ua['device'],
            isBot: $ua['isBot'],
            refererHost: $referer['host'],
            refererSource: $referer['source'],
            country: $country['code'] ?? null,
            countryName: $country['name'] ?? null,
        );
    }

    private function entity(Request $request): ?string
    {
        $name = $request->attributes->get('name');

        return is_string($name) && $name !== '' ? $name : null;
    }

    private function queryString(Request $request, string $key): ?string
    {
        $value = trim((string) $request->query->get($key, ''));

        return $value === '' ? null : $value;
    }

    /**
     * Stable pseudonymous id for unique-visitor counting. Peppered with the app
     * secret so the raw (ip, ua) pair can't be recovered from the id alone; the
     * raw ip/ua are stored alongside (admin-only) per the chosen data model.
     */
    private function visitorId(?string $ip, ?string $userAgent): string
    {
        $material = ($ip ?? 'unknown') . '|' . ($userAgent ?? '');

        return substr(hash_hmac('sha256', $material, $this->appSecret), 0, 16);
    }
}
