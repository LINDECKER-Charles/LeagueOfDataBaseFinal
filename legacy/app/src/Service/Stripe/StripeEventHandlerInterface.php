<?php
declare(strict_types=1);

namespace App\Service\Stripe;

use Stripe\Event;
use Symfony\Component\DependencyInjection\Attribute\AutoconfigureTag;

/**
 * One handler per Stripe event type. The webhook controller iterates the
 * tagged collection and routes each verified event to the matching handler —
 * adding support for a new event is one new class, zero controller change.
 */
#[AutoconfigureTag(self::TAG)]
interface StripeEventHandlerInterface
{
    public const TAG = 'app.stripe_event_handler';

    /** Stripe event type consumed by this handler, e.g. "checkout.session.completed". */
    public function eventType(): string;

    public function handle(Event $event): void;
}
