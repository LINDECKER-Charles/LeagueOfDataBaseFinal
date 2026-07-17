<?php
declare(strict_types=1);

namespace App\Tests\Unit\EventListener;

use App\EventListener\RecordRequestListener;
use App\Service\Analytics\EventStore;
use App\Service\Analytics\GeoLocator;
use App\Service\Analytics\RefererClassifier;
use App\Service\Analytics\RequestEventFactory;
use App\Service\Analytics\UserAgentParser;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\HttpKernel\Event\TerminateEvent;
use Symfony\Component\HttpKernel\HttpKernelInterface;

final class RecordRequestListenerTest extends TestCase
{
    private string $dir;
    private EventStore $store;
    private RecordRequestListener $listener;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir() . '/lodb_listener_' . bin2hex(random_bytes(6));
        $this->store = new EventStore($this->dir);
        $factory = new RequestEventFactory(
            new UserAgentParser(),
            new RefererClassifier(),
            new GeoLocator('', sys_get_temp_dir()),
            'secret',
        );
        $this->listener = new RecordRequestListener($factory, $this->store);
    }

    protected function tearDown(): void
    {
        if (!is_dir($this->dir)) {
            return;
        }
        $it = new \RecursiveIteratorIterator(
            new \RecursiveDirectoryIterator($this->dir, \FilesystemIterator::SKIP_DOTS),
            \RecursiveIteratorIterator::CHILD_FIRST,
        );
        foreach ($it as $f) {
            $f->isDir() ? rmdir($f->getPathname()) : unlink($f->getPathname());
        }
        rmdir($this->dir);
    }

    private function terminate(string $route): TerminateEvent
    {
        $request = Request::create('/x', 'GET');
        $request->attributes->set('_route', $route);

        return new TerminateEvent(
            $this->createStub(HttpKernelInterface::class),
            $request,
            new Response('', 200),
        );
    }

    public function testResourceViewIsRecorded(): void
    {
        ($this->listener)($this->terminate('app_champions'));

        $rows = iterator_to_array($this->store->readDay(gmdate('Y-m-d')));
        self::assertCount(1, $rows);
        self::assertSame('champion', $rows[0]['type']);
    }

    public function testNonResourceRouteIsNotRecorded(): void
    {
        ($this->listener)($this->terminate('admin_dashboard'));
        ($this->listener)($this->terminate('app_setup'));

        self::assertFalse($this->store->hasDay(gmdate('Y-m-d')));
    }
}
