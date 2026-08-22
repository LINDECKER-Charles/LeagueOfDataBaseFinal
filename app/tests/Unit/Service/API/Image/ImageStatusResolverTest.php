<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API\Image;

use App\Service\API\Image\ImageStatusInterface;
use App\Service\API\Image\ImageStatusResolver;
use PHPUnit\Framework\TestCase;

/**
 * The in-page refresh is a manifest read: browser-ready paths for what landed
 * (WebP twin derived server-side), null for a settled absence, the rest left
 * pending — and the ingestion is re-queued on the client's LAST attempt only.
 */
final class ImageStatusResolverTest extends TestCase
{
    private const VERSION = '16.16.1';

    public function testMapsSettledPathsToBrowserCandidatesAndKeepsPendingNames(): void
    {
        $manager = $this->manager('item', ['1001.png' => 'cdn/blobs/a.png', '9999.png' => null]);
        $resolver = new ImageStatusResolver([$manager]);

        $status = $resolver->status('item', self::VERSION, ['1001.png', '9999.png', '2003.png'], false);

        self::assertSame(
            ['1001.png' => ['src' => '/cdn/blobs/a.png', 'webp' => '/cdn/blobs/a.webp'], '9999.png' => null],
            $status['images'],
        );
        self::assertSame(['2003.png'], $status['pending']);
        self::assertSame([], $manager->warmed, 'an ordinary poll never queues work');
    }

    public function testReQueuesThePendingNamesOnTheLastAttemptOnly(): void
    {
        $manager = $this->manager('item', ['1001.png' => 'cdn/blobs/a.png']);
        $resolver = new ImageStatusResolver([$manager]);

        $resolver->status('item', self::VERSION, ['1001.png', '2003.png'], true);

        self::assertSame([[self::VERSION, ['2003.png']]], $manager->warmed);
    }

    public function testALastAttemptWithNothingPendingQueuesNothing(): void
    {
        $manager = $this->manager('item', ['1001.png' => 'cdn/blobs/a.png']);
        $resolver = new ImageStatusResolver([$manager]);

        $resolver->status('item', self::VERSION, ['1001.png'], true);

        self::assertSame([], $manager->warmed);
    }

    public function testOnlyImageStatusManagersAreRouted(): void
    {
        $resolver = new ImageStatusResolver([$this->manager('champion', []), new \stdClass()]);

        self::assertTrue($resolver->knowsType('champion'));
        self::assertFalse($resolver->knowsType('item'));
    }

    /**
     * @param array<string,?string> $manifest
     * @return ImageStatusInterface&object{warmed: list<array{0: string, 1: string[]}>}
     */
    private function manager(string $type, array $manifest): ImageStatusInterface
    {
        return new class($type, $manifest) implements ImageStatusInterface {
            /** @var list<array{0: string, 1: string[]}> */
            public array $warmed = [];

            /** @param array<string,?string> $manifest */
            public function __construct(private readonly string $type, private readonly array $manifest) {}

            public function type(): string
            {
                return $this->type;
            }

            public function manifestStatus(string $version, array $names): array
            {
                $images = [];
                $pending = [];
                foreach ($names as $name) {
                    if (\array_key_exists($name, $this->manifest)) {
                        $images[$name] = $this->manifest[$name];
                    } else {
                        $pending[] = $name;
                    }
                }

                return ['images' => $images, 'pending' => $pending];
            }

            public function warmLater(string $version, array $names): void
            {
                $this->warmed[] = [$version, $names];
            }
        };
    }
}
