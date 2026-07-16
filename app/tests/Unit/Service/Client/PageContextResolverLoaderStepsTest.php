<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Client;

use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Tools\GoFetcherClient;
use App\Service\Tools\Utils;
use PHPUnit\Framework\TestCase;
use Psr\Log\NullLogger;
use Symfony\Component\Cache\Adapter\ArrayAdapter;
use Symfony\Component\Filesystem\Filesystem;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * loaderSteps() maps a destination path to the resource-warming plan the
 * streaming loader runs — purely from the query (no session), mirroring each
 * list controller's pagination.
 */
final class PageContextResolverLoaderStepsTest extends TestCase
{
    /** Real (final) collaborators wired against a no-egress gateway; loaderSteps never calls them. */
    private function resolver(array $query = []): PageContextResolver
    {
        $stack = new RequestStack();
        $stack->push(new Request(query: $query));

        $noEgress = new GoFetcherClient(new MockHttpClient(static function (): void {
            throw new \RuntimeException('loaderSteps must not touch the gateway/session');
        }));
        $version = new VersionManager($noEgress, new ArrayAdapter(), new NullLogger());
        $client = new ClientManager($stack, $version, new Utils(new Filesystem(), $version, sys_get_temp_dir()), 'secret', 'en_US');

        return new PageContextResolver($stack, $client, $version);
    }

    public function testHomeWarmsFourPreviews(): void
    {
        self::assertSame([
            ['type' => 'champion', 'perPage' => 4, 'page' => 1],
            ['type' => 'item', 'perPage' => 4, 'page' => 1],
            ['type' => 'summoner', 'perPage' => 4, 'page' => 1],
            ['type' => 'runesReforged', 'perPage' => 4, 'page' => 1],
        ], $this->resolver()->loaderSteps('/home'));
    }

    public function testListUsesRouteDefaults(): void
    {
        self::assertSame(
            [['type' => 'champion', 'perPage' => 20, 'page' => 1]],
            $this->resolver()->loaderSteps('/champions'),
        );
        self::assertSame(
            [['type' => 'item', 'perPage' => 8, 'page' => 1]],
            $this->resolver()->loaderSteps('/objects'),
        );
    }

    public function testListReadsQueryPaginationAndClamps(): void
    {
        self::assertSame(
            [['type' => 'champion', 'perPage' => 20, 'page' => 3]],
            $this->resolver(['numpage' => '3', 'itemperpage' => '50'])->loaderSteps('/champions'),
            'itemperpage is clamped to the route maximum',
        );
    }

    public function testSummonersHasNoPerPageCap(): void
    {
        self::assertSame(
            [['type' => 'summoner', 'perPage' => 500, 'page' => 1]],
            $this->resolver(['itemperpage' => '500'])->loaderSteps('/summoners'),
        );
    }

    public function testTrailingSlashIsNormalised(): void
    {
        self::assertCount(4, $this->resolver()->loaderSteps('/home/'));
    }

    public function testDetailAndUnknownPathsWarmNothing(): void
    {
        $resolver = $this->resolver();
        foreach (['/champion/Ahri', '/object/1001', '/rune/8000', '/', '/working-progress'] as $path) {
            self::assertSame([], $resolver->loaderSteps($path), $path);
        }
    }
}
