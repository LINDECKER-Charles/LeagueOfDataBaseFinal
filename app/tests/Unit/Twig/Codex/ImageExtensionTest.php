<?php
declare(strict_types=1);

namespace App\Tests\Unit\Twig\Codex;

use App\Service\API\Image\ImageStatusResolver;
use App\Twig\Codex\ImageExtension;
use PHPUnit\Framework\TestCase;

final class ImageExtensionTest extends TestCase
{
    public function testExposesTheWebpRuleAndThePollBatchUnderTheirTemplateNames(): void
    {
        $functions = [];
        foreach ((new ImageExtension())->getFunctions() as $function) {
            $functions[$function->getName()] = $function->getCallable();
        }

        self::assertSame('/cdn/blobs/a.webp', $functions['webp_sibling']('/cdn/blobs/a.png'));
        self::assertNull($functions['webp_sibling']('/cdn/blobs/a.svg'));
        self::assertSame(ImageStatusResolver::MAX_NAMES_PER_CALL, $functions['image_status_batch']());
    }
}
