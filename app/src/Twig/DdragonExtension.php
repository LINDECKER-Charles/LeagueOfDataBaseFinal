<?php
declare(strict_types=1);

namespace App\Twig;

use App\Service\Tools\DdragonText;
use Twig\Extension\AbstractExtension;
use Twig\TwigFilter;

/**
 * Template face of {@see DdragonText}: strips the raw tokens Data Dragon leaks
 * before the HTML reaches `.ddragon-rich`, and fixes the case quirks of its
 * copy without destroying it (Twig's own `capitalize` lowercases everything
 * after the first letter — "Force de Demacia" must not become
 * "Force de demacia").
 */
final class DdragonExtension extends AbstractExtension
{
    public function getFilters(): array
    {
        return [
            new TwigFilter('ddragon_text', $this->clean(...)),
            new TwigFilter('ucfirst', $this->ucfirst(...)),
            new TwigFilter('tag_label', $this->tagLabel(...)),
        ];
    }

    public function clean(?string $html): string
    {
        return DdragonText::clean($html);
    }

    /** Display form of a Data Dragon category tag ({@see DdragonText::tagLabel()}). */
    public function tagLabel(?string $tag): string
    {
        return DdragonText::tagLabel($tag);
    }

    /** Uppercases the first letter only — everything after it stays intact. */
    public function ucfirst(?string $text): string
    {
        if ($text === null || $text === '') {
            return '';
        }

        return mb_strtoupper(mb_substr($text, 0, 1)).mb_substr($text, 1);
    }
}
