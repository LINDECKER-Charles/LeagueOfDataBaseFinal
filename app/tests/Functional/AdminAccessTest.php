<?php
declare(strict_types=1);

namespace App\Tests\Functional;

use Symfony\Bundle\FrameworkBundle\Test\WebTestCase;
use Symfony\Component\Security\Core\User\InMemoryUser;

/**
 * Covers the /admin firewall wiring end-to-end: gating, the public login page, the
 * env-credential authenticator (App\Security\AdminAuthenticator) and the routing of an
 * authenticated admin. Form credentials come from ADMIN_LOGIN / ADMIN_PASSWORD in .env.test.
 */
final class AdminAccessTest extends WebTestCase
{
    private const FIREWALL = 'admin';

    public function testStorageRequiresAuthentication(): void
    {
        $client = static::createClient();
        $client->request('GET', '/admin/storage');

        self::assertResponseRedirects('/admin/login');
    }

    public function testLoginPageIsPublic(): void
    {
        $client = static::createClient();
        $client->request('GET', '/admin/login');

        self::assertResponseIsSuccessful();
        self::assertSelectorExists('form input[name="username"]');
        self::assertSelectorExists('form input[name="password"]');
    }

    public function testValidCredentialsAuthenticate(): void
    {
        $client = static::createClient();
        $crawler = $client->request('GET', '/admin/login');

        $client->submit($crawler->selectButton('Se connecter')->form([
            'username' => $_ENV['ADMIN_LOGIN'] ?? 'admin',
            'password' => $_ENV['ADMIN_PASSWORD'] ?? 'test_admin',
        ]));

        self::assertResponseRedirects('/admin/storage');
    }

    public function testInvalidCredentialsAreRejected(): void
    {
        $client = static::createClient();
        $crawler = $client->request('GET', '/admin/login');

        $client->submit($crawler->selectButton('Se connecter')->form([
            'username' => 'admin',
            'password' => 'wrong-password',
        ]));

        self::assertResponseRedirects('/admin/login');
    }

    public function testDashboardRendersForAdmin(): void
    {
        $client = static::createClient();
        $client->loginUser($this->admin(), self::FIREWALL);

        $client->request('GET', '/admin');

        // Like the storage panel, the overview degrades gracefully when MinIO is
        // unreachable (inline alert) and still answers 200.
        self::assertResponseIsSuccessful();
        self::assertSelectorTextContains('h1', "Vue d'ensemble");
    }

    public function testStoragePanelRendersForAdmin(): void
    {
        $client = static::createClient();
        $client->loginUser($this->admin(), self::FIREWALL);

        $client->request('GET', '/admin/storage');

        // The panel is resilient: it returns 200 whether or not MinIO is reachable
        // (StorageUsageService degrades to ok=false instead of 500).
        self::assertResponseIsSuccessful();
        self::assertSelectorTextContains('h1', 'Stockage');
    }

    private function admin(): InMemoryUser
    {
        return new InMemoryUser('admin', null, ['ROLE_ADMIN']);
    }
}
