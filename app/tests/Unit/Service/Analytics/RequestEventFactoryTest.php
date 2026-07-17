<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics;

use App\Service\Analytics\GeoLocator;
use App\Service\Analytics\RefererClassifier;
use App\Service\Analytics\RequestEventFactory;
use App\Service\Analytics\UserAgentParser;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;

final class RequestEventFactoryTest extends TestCase
{
    private RequestEventFactory $factory;

    protected function setUp(): void
    {
        $this->factory = new RequestEventFactory(
            new UserAgentParser(),
            new RefererClassifier(),
            new GeoLocator('', sys_get_temp_dir()),
            'test-secret',
        );
    }

    private function request(string $route, array $query = [], array $attrs = [], array $server = []): Request
    {
        $request = Request::create('/x', 'GET', $query, [], [], $server);
        $request->attributes->set('_route', $route);
        foreach ($attrs as $k => $v) {
            $request->attributes->set($k, $v);
        }

        return $request;
    }

    public function testNonGetIsIgnored(): void
    {
        $request = Request::create('/champions', 'POST');
        $request->attributes->set('_route', 'app_champions');

        self::assertNull($this->factory->fromRequestResponse($request, new Response()));
    }

    public function testNonWhitelistedRouteIsIgnored(): void
    {
        $request = $this->request('app_home_legacy');

        self::assertNull($this->factory->fromRequestResponse($request, new Response()));
        self::assertNull($this->factory->fromRequestResponse($this->request('admin_dashboard'), new Response()));
        self::assertNull($this->factory->fromRequestResponse($this->request('api_champions_search'), new Response()));
    }

    public function testDetailRouteCapturesEntityAndKind(): void
    {
        $request = $this->request('app_champion', ['version' => '15.1.1', 'lang' => 'fr_FR'], ['name' => 'Aatrox']);
        $event = $this->factory->fromRequestResponse($request, new Response('', 200));

        self::assertNotNull($event);
        self::assertSame('champion', $event->type);
        self::assertSame('detail', $event->kind);
        self::assertSame('Aatrox', $event->entity);
        self::assertSame('15.1.1', $event->version);
        self::assertSame('fr_FR', $event->lang);
        self::assertSame(200, $event->status);
    }

    public function testListRouteHasNoEntity(): void
    {
        $event = $this->factory->fromRequestResponse($this->request('app_items'), new Response());

        self::assertNotNull($event);
        self::assertSame('item', $event->type);
        self::assertSame('list', $event->kind);
        self::assertNull($event->entity);
    }

    public function testUserAgentIsParsedAndRefererClassified(): void
    {
        $request = $this->request('app_home', [], [], [
            'HTTP_USER_AGENT' => 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) Mobile Safari/604.1',
            'HTTP_REFERER' => 'https://www.google.com/search',
        ]);
        $event = $this->factory->fromRequestResponse($request, new Response());

        self::assertSame('mobile', $event->device);
        self::assertSame('search', $event->refererSource);
        self::assertSame('www.google.com', $event->refererHost);
    }

    public function testVisitorIdIsStablePerIpAndAgentButDiffersOtherwise(): void
    {
        $a = $this->factory->fromRequestResponse($this->request('app_home', [], [], ['HTTP_USER_AGENT' => 'UA-1']), new Response());
        $b = $this->factory->fromRequestResponse($this->request('app_home', [], [], ['HTTP_USER_AGENT' => 'UA-1']), new Response());
        $c = $this->factory->fromRequestResponse($this->request('app_home', [], [], ['HTTP_USER_AGENT' => 'UA-2']), new Response());

        self::assertSame($a->visitorId, $b->visitorId);
        self::assertNotSame($a->visitorId, $c->visitorId);
        self::assertSame(16, strlen($a->visitorId));
    }
}
