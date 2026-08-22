<?php

declare(strict_types=1);

namespace App\Tests\Unit\Template;

use App\Twig\Codex\ImageExtension;
use PHPUnit\Framework\TestCase;
use Twig\Environment;
use Twig\Loader\FilesystemLoader;

/**
 * Behaviour of the dependency-free shared partials the pages compose. They are
 * rendered through a bare Twig environment (no kernel): these components carry
 * rules — the sibling-WebP guard, the rune path allowlist — that used to live
 * copy-pasted in a dozen templates, and a regression there is silent in the
 * browser (a 404 inside a <picture>, a build tinted with the wrong tree).
 */
final class SharedComponentsTest extends TestCase
{
    private Environment $twig;

    protected function setUp(): void
    {
        $this->twig = new Environment(new FilesystemLoader(__DIR__ . '/../../../templates'));
        // The WebP rule the partial delegates to (its owner is PHP, see WebpSibling).
        $this->twig->addExtension(new ImageExtension());
    }

    public function testWebpSourceIsDerivedFromAPngOriginal(): void
    {
        $html = $this->render('components/_webp_source.html.twig', ['src' => '/cdn/blobs/ab.png']);

        self::assertSame(
            '<source srcset="/cdn/blobs/ab.webp" type="image/webp">',
            trim($html),
        );
    }

    /**
     * MinIO only writes a WebP twin next to a PNG blob: deriving one for any
     * other extension points the browser at a file that was never written.
     */
    public function testWebpSourceIsOmittedForANonPngOriginal(): void
    {
        foreach (['/cdn/blobs/ab.jpg', '/cdn/blobs/ab.webp', '/preview/home.svg'] as $src) {
            self::assertSame('', trim($this->render(
                'components/_webp_source.html.twig',
                ['src' => $src],
            )), $src);
        }
    }

    public function testRunePathThemeClassPerTree(): void
    {
        $expected = [
            'Precision' => 'path-precision',
            'Domination' => 'path-domination',
            'Sorcery' => 'path-sorcery',
            'Resolve' => 'path-resolve',
            'Inspiration' => 'path-inspiration',
        ];

        foreach ($expected as $key => $class) {
            self::assertSame($class, $this->themeClass($key));
        }
    }

    /** An unknown tree must still render a valid theme, never an empty class. */
    public function testRunePathThemeClassFallsBackToPrecision(): void
    {
        self::assertSame('path-precision', $this->themeClass('Chronomancy'));
    }

    public function testChampionPortraitFallsBackToInitialsWhenTheIconIsCold(): void
    {
        $html = $this->render('components/_champion_portrait.html.twig', [
            'champion' => ['image' => null, 'name' => 'Ahri', 'missing' => false],
            'keystone' => ['icon' => null, 'name' => ''],
        ]);

        self::assertStringContainsString('<span class="forge-row__initials">AH</span>', $html);
        self::assertStringNotContainsString('<picture>', $html);
    }

    public function testChampionPortraitMarksAChampionMissingFromThePatch(): void
    {
        $html = $this->render('components/_champion_portrait.html.twig', [
            'champion' => ['image' => 'cdn/blobs/ahri.png', 'name' => 'Ahri', 'missing' => true],
            'keystone' => ['icon' => 'cdn/blobs/electrocute.png', 'name' => 'Electrocute'],
        ]);

        self::assertStringContainsString('forge-ghost', $html);
        self::assertStringContainsString('src="/cdn/blobs/ahri.png"', $html);
        self::assertStringContainsString('srcset="/cdn/blobs/ahri.webp"', $html);
        self::assertStringContainsString('class="hx-img forge-row__keystone"', $html);
    }

    /** @param array<string, mixed> $context */
    private function render(string $template, array $context): string
    {
        return $this->twig->render($template, $context);
    }

    private function themeClass(string $key): string
    {
        return trim($this->twig->createTemplate(
            "{% import 'components/_rune_path.html.twig' as p %}{{ p.theme_class(key) }}",
        )->render(['key' => $key]));
    }
}
