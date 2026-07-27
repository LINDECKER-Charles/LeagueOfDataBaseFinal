<?php
declare(strict_types=1);

namespace App\Tests\Functional;

use PHPUnit\Framework\Attributes\DataProvider;
use Symfony\Bundle\FrameworkBundle\Test\WebTestCase;
use Symfony\Component\Security\Core\User\InMemoryUser;

/**
 * Covers the deferred admin panels end-to-end: the page shells emit skeletons
 * instead of the reports, the fragment endpoint renders each panel bare, and the
 * `?sync=1` fallback inlines the same fragments for no-JavaScript clients.
 *
 * Every panel degrades gracefully (unreachable MinIO/Postgres yield an inline
 * alert, never a 500), so these assertions hold without a live stack.
 */
final class AdminPanelTest extends WebTestCase
{
    private const FIREWALL = 'admin';

    public static function panels(): array
    {
        return [
            ['overview-app'], ['overview-traffic'], ['overview-storage'],
            ['traffic'], ['audience'], ['storage'], ['monitoring'],
        ];
    }

    public static function pages(): array
    {
        return [
            ['/admin', 'overview-app'],
            ['/admin/traffic', 'traffic'],
            ['/admin/audience', 'audience'],
            ['/admin/storage', 'storage'],
            ['/admin/monitoring', 'monitoring'],
        ];
    }

    #[DataProvider('panels')]
    public function testEveryPanelRendersAsABareFragment(string $panel): void
    {
        $client = static::createClient();
        $client->loginUser($this->admin(), self::FIREWALL);

        $client->request('GET', '/admin/panel/' . $panel);

        self::assertResponseIsSuccessful();
        self::assertStringNotContainsString('<!DOCTYPE', $client->getResponse()->getContent());
        self::assertStringContainsString('no-store', (string) $client->getResponse()->headers->get('Cache-Control'));
    }

    #[DataProvider('pages')]
    public function testPageShellDefersItsPanel(string $path, string $panel): void
    {
        $client = static::createClient();
        $client->loginUser($this->admin(), self::FIREWALL);

        $crawler = $client->request('GET', $path);

        self::assertResponseIsSuccessful();
        self::assertGreaterThan(0, $crawler->filter(sprintf('[data-panel-url*="/admin/panel/%s"]', $panel))->count());
        self::assertGreaterThan(0, $crawler->filter('.sk')->count(), 'the shell must show a skeleton while loading');
    }

    #[DataProvider('pages')]
    public function testSyncModeInlinesThePanel(string $path, string $panel): void
    {
        $client = static::createClient();
        $client->loginUser($this->admin(), self::FIREWALL);

        $crawler = $client->request('GET', $path . '?sync=1');

        self::assertResponseIsSuccessful();
        self::assertSame(0, $crawler->filter('[data-panel-url]')->count());
        self::assertSame(0, $crawler->filter('.sk')->count());
        self::assertGreaterThan(0, $crawler->filter('.panel-async')->count());
    }

    public function testUnknownPanelIsNotFound(): void
    {
        $client = static::createClient();
        $client->loginUser($this->admin(), self::FIREWALL);

        $client->request('GET', '/admin/panel/nope');

        self::assertResponseStatusCodeSame(404);
    }

    public function testFragmentsAreBehindTheAdminFirewall(): void
    {
        $client = static::createClient();
        $client->request('GET', '/admin/panel/storage');

        self::assertResponseRedirects('/admin/login');
    }

    private function admin(): InMemoryUser
    {
        return new InMemoryUser('admin', null, ['ROLE_ADMIN']);
    }
}
