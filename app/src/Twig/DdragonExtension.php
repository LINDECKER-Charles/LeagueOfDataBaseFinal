<?php
declare(strict_types=1);

namespace App\Twig;

use Twig\Extension\AbstractExtension;
use Twig\TwigFilter;

/**
 * Data Dragon copy occasionally leaks raw template tokens the CDN never
 * resolves ("{{ Item_Cooldown }}", "@BaseHeal@"…). They carry no displayable
 * value, so they are stripped before the HTML reaches `.ddragon-rich`.
 */
final class DdragonExtension extends AbstractExtension
{
    public function getFilters(): array
    {
        return [new TwigFilter('ddragon_text', $this->clean(...))];
    }

    public function clean(?string $html): string
    {
        if ($html === null || $html === '') {
            return '';
        }

        $stripped = preg_replace(['/\{\{[^{}]*\}\}/', '/@[\w.]+@/'], '', $html) ?? $html;

        return trim(preg_replace('/[ \t]{2,}/', ' ', $stripped) ?? $stripped);
    }
}
