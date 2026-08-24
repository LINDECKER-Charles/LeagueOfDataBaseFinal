<?php
declare(strict_types=1);

namespace App\Tests\Unit\Controller\Resource;

use App\Controller\Resource\AbstractResourceController;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Tools\GoFetcherClient;
use App\Tests\Unit\Support\RecordingLogger;
use PHPUnit\Framework\TestCase;
use Psr\Log\LogLevel;
use Psr\Log\NullLogger;
use Symfony\Component\Cache\Adapter\ArrayAdapter;
use Symfony\Component\DependencyInjection\ServiceLocator;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\HttpFoundation\Session\Session;
use Symfony\Component\HttpFoundation\Session\Storage\MockArraySessionStorage;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;

/**
 * A resource page whose data layer is down redirects home with a flash — the
 * visitor keeps a usable site, and until now supervision saw strictly nothing.
 * The point of these tests is that adding the log did NOT change that outcome:
 * same redirect, same flash, plus one record.
 */
final class ResourcePageFailureTest extends TestCase
{
    private const VERSION = '15.1.1';
    private const LANG = 'en_US';

    public function testAFailingPageStillRedirectsHomeWithItsFlash(): void
    {
        $session = new Session(new MockArraySessionStorage());
        $controller = $this->controller(new NullLogger(), $session);

        $response = $controller->fail(new \RuntimeException('minio down'));

        self::assertSame(Response::HTTP_FOUND, $response->getStatusCode());
        self::assertSame('/', $response->headers->get('Location'));
        self::assertCount(1, $session->getFlashBag()->peek('error'));
    }

    /**
     * `warning`, not `error`: the visitor keeps something usable and the cause is
     * usually a transient upstream outage. It is the VOLUME that makes it a signal.
     */
    public function testAFailingPageIsReportedOnTheCatalogChannelAsAWarning(): void
    {
        $logger = new RecordingLogger();
        $boom = new \RuntimeException('minio down');

        $this->controller($logger, new Session(new MockArraySessionStorage()))->fail($boom);

        $record = $logger->only('catalog.page.unavailable');

        self::assertSame(LogLevel::WARNING, $record['level']);
        self::assertSame(self::VERSION, $record['context']['version']);
        self::assertSame(self::LANG, $record['context']['lang']);
        self::assertSame($boom, $record['context']['exception'], 'the object, never getMessage()');
    }

    /** No context key may be called `error`: the collector guesses `level` by regex. */
    public function testTheContextCarriesNoKeyThatWouldReclassifyTheRecord(): void
    {
        $logger = new RecordingLogger();

        $this->controller($logger, new Session(new MockArraySessionStorage()))
            ->fail(new \RuntimeException('minio down'));

        $context = $logger->only('catalog.page.unavailable')['context'];

        self::assertSame(['version', 'lang', 'exception'], array_keys($context));
    }

    private function controller(object $logger, Session $session): object
    {
        $stack = new RequestStack();
        $request = Request::create('/objects?version=' . self::VERSION . '&lang=' . self::LANG);
        $request->setSession($session);
        $stack->push($request);

        $versions = new VersionManager($this->noEgress(), $this->catalogCache(), new NullLogger());
        $clients = new ClientManager($stack, $versions, 'secret', self::LANG);

        $controller = new class (
            $versions,
            $clients,
            new PageContextResolver($stack, $clients, $versions),
            $stack,
            $logger,
        ) extends AbstractResourceController {
            public function fail(\Throwable $e): Response
            {
                return $this->redirectToHomeWithError(
                    ['version' => '15.1.1', 'lang' => 'en_US'],
                    $e,
                );
            }
        };
        $controller->setContainer(new ServiceLocator([
            'request_stack' => static fn (): RequestStack => $stack,
            'router' => fn (): UrlGeneratorInterface => $this->homeRouter(),
        ]));

        return $controller;
    }

    private function homeRouter(): UrlGeneratorInterface
    {
        $router = $this->createStub(UrlGeneratorInterface::class);
        $router->method('generate')->willReturn('/');

        return $router;
    }

    /** Catalog pre-seeded in the cache: the gateway must never be reached here. */
    private function catalogCache(): ArrayAdapter
    {
        $cache = new ArrayAdapter();
        foreach (['riot_versions' => [self::VERSION], 'riot_languages' => [self::LANG]] as $k => $v) {
            $item = $cache->getItem($k);
            $item->set($v);
            $cache->save($item);
        }

        return $cache;
    }

    private function noEgress(): GoFetcherClient
    {
        return new GoFetcherClient(new MockHttpClient(static function (): void {
            throw new \RuntimeException('this page must not touch the gateway');
        }), new NullLogger());
    }
}
