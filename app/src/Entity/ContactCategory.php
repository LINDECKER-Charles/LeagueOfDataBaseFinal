<?php
declare(strict_types=1);

namespace App\Entity;

/**
 * Nature of a footer contact submission. Backed value is the on-disk/DB contract
 * ({@see ContactMessage::$category}); labels/tones are French because the only
 * consumers (the /admin inbox and the operator notification email) are French.
 */
enum ContactCategory: string
{
    case Bug = 'bug';
    case Feedback = 'feedback';
    case Review = 'review';
    case Commercial = 'commercial';

    public function label(): string
    {
        return match ($this) {
            self::Bug => 'Bug / anomalie',
            self::Feedback => 'Suggestion / feedback',
            self::Review => 'Avis',
            self::Commercial => 'Contact commercial',
        };
    }

    /** Admin table badge tone ({@see _ui.html.twig} cell badges). */
    public function tone(): string
    {
        return match ($this) {
            self::Bug => 'bad',
            self::Commercial => 'good',
            default => '',
        };
    }
}
