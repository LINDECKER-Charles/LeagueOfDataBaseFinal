<?php
declare(strict_types=1);

namespace App\Tests\Unit\Controller\Api;

use App\Controller\Api\ImageStatusController;
use App\Service\API\Image\ImageStatusInterface;
use App\Service\API\Image\ImageStatusResolver;
use App\Service\Client\VersionManager;
use App\Service\Tools\GoFetcherClient;
use PHPUnit\Framework\TestCase;
use Psr\Log\NullLogger;
use Symfony\Component\Cache\Adapter\ArrayAdapter;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpClient\Response\MockResponse;
use Symfony\Component\HttpFoundation\Request;

/**
 * The poll endpoint validates everything it is handed (type, patch, names,
 * batch size) and never lets a shared cache replay a "still pending".
 */
final class ImageStatusControllerTest extends TestCase
{
    private const VERSION = '16.16.1';

    public function testAnswersSettledAndPendingNamesWithoutCaching(): void
    {
        $response = $this->controller()->status('item', $this->request([
            'version' => self::VERSION,
            'names' => '1001.png, 9999.png,',
        ]));

        self::assertSame(200, $response->getStatusCode());
        self::assertStringContainsString('no-store', (string) $response->headers->get('Cache-Control'));
        self::assertSame(
            [
                'images' => ['1001.png' => ['src' => '/cdn/blobs/a.png', 'webp' => '/cdn/blobs/a.webp']],
                'pending' => ['9999.png'],
            ],
            json_decode((string) $response->getContent(), true, flags: JSON_THROW_ON_ERROR),
        );
    }

    public function testRejectsAnUnknownTypeOrPatch(): void
    {
        $controller = $this->controller();

        self::assertSame(
            404,
            $controller->status('nope', $this->request(['version' => self::VERSION, 'names' => 'a.png']))
                ->getStatusCode(),
        );
        self::assertSame(
            400,
            $controller->status('item', $this->request(['version' => '0.0.0', 'names' => 'a.png']))
                ->getStatusCode(),
        );
    }

    public function testRejectsEmptyOversizedOrMalformedNameLists(): void
    {
        $controller = $this->controller();
        $tooMany = implode(',', array_map(
            static fn (int $i): string => $i.'.png',
            range(1, ImageStatusResolver::MAX_NAMES_PER_CALL + 1),
        ));

        foreach (['', $tooMany, '../etc/passwd', 'a.png,b c.png'] as $names) {
            self::assertSame(
                400,
                $controller->status('item', $this->request(['version' => self::VERSION, 'names' => $names]))
                    ->getStatusCode(),
                sprintf('names=%s', substr($names, 0, 30)),
            );
        }
    }

    /** @param array<string,string> $query */
    private function request(array $query): Request
    {
        return Request::create('/api/images/item', 'GET', $query);
    }

    private function controller(): ImageStatusController
    {
        $versions = new VersionManager(
            new GoFetcherClient(new MockHttpClient([
                new MockResponse(json_encode([self::VERSION, '16.15.1'], JSON_THROW_ON_ERROR)),
            ]), new NullLogger()),
            new ArrayAdapter(),
            new NullLogger(),
        );

        return new ImageStatusController(new ImageStatusResolver([$this->itemManager()]), $versions);
    }

    private function itemManager(): ImageStatusInterface
    {
        return new class implements ImageStatusInterface {
            public function type(): string
            {
                return 'item';
            }

            public function manifestStatus(string $version, array $names): array
            {
                $settled = ['1001.png' => 'cdn/blobs/a.png'];
                $images = array_intersect_key($settled, array_flip($names));

                return [
                    'images' => $images,
                    'pending' => array_values(array_diff($names, array_keys($settled))),
                ];
            }

            public function warmLater(string $version, array $names): void {}
        };
    }
}
