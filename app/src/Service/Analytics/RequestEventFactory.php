<?php
declare(strict_types=1);

namespace App\Service\Analytics;

use App\Service\Analytics\Model\RefererOrigin;
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
    /** The only page kind that carries an entity name (the detail target). */
    private const KIND_DETAIL = 'detail';
    private const KIND_LIST = 'list';
    private const KIND_HOME = 'home';

    /** Loggable route name => [resource type, page kind]. */
    private const ROUTES = [
        'app_home' => ['home', self::KIND_HOME],
        'app_champions' => ['champion', self::KIND_LIST],
        'app_champion' => ['champion', self::KIND_DETAIL],
        'app_items' => ['item', self::KIND_LIST],
        'app_item' => ['item', self::KIND_DETAIL],
        'app_runes' => ['runesReforged', self::KIND_LIST],
        'app_rune' => ['runesReforged', self::KIND_DETAIL],
        'app_summoners' => ['summoner', self::KIND_LIST],
        'app_summoner' => ['summoner', self::KIND_DETAIL],
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
        $route = (string) $request->attributes->get('_route');
        if ($request->getMethod() !== 'GET' || !isset(self::ROUTES[$route])) {
            return null;
        }

        return $this->capture($request, $response, $route);
    }

    /** @param string $route already proven loggable by {@see fromRequestResponse()} */
    private function capture(Request $request, Response $response, string $route): RequestEvent
    {
        [$type, $kind] = self::ROUTES[$route];
        $ip = $request->getClientIp();
        $userAgent = $request->headers->get('User-Agent');
        $client = $this->userAgents->parse($userAgent);
        $referer = $this->classifyReferer($request);
        $country = $this->geo->locate($ip);

        return new RequestEvent(
            at: new \DateTimeImmutable('now', new \DateTimeZone('UTC')),
            route: $route,
            path: $request->getPathInfo(),
            type: $type,
            kind: $kind,
            entity: $kind === self::KIND_DETAIL ? $this->entity($request) : null,
            status: $response->getStatusCode(),
            version: $this->queryString($request, 'version'),
            lang: $this->queryString($request, 'lang'),
            locale: $request->getLocale(),
            ip: $ip,
            visitorId: $this->visitorId($ip, $userAgent),
            userAgent: $userAgent,
            browser: $client->browser, os: $client->os,
            device: $client->device, isBot: $client->isBot,
            refererHost: $referer->host, refererSource: $referer->source->value,
            country: $country?->code, countryName: $country?->name,
        );
    }

    private function classifyReferer(Request $request): RefererOrigin
    {
        return $this->referers->classify($request->headers->get('Referer'), $request->getHost());
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
