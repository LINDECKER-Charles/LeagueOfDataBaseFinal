<?php
declare(strict_types=1);

namespace App\EventListener;

use App\Service\Analytics\EventStore;
use App\Service\Analytics\RequestEventFactory;
use Symfony\Component\EventDispatcher\Attribute\AsEventListener;
use Symfony\Component\HttpKernel\Event\TerminateEvent;

/**
 * Records one analytics event per resource-page view, at kernel.terminate. Like
 * {@see FlushDeferredImagesListener} this runs after php-fpm's
 * fastcgi_finish_request, i.e. off the request's critical path — the visitor has
 * already received the response, so capture adds no perceptible latency.
 *
 * TerminateEvent only fires for the main request, so sub-requests and CLI are
 * naturally excluded; the factory further narrows to whitelisted GET routes.
 */
#[AsEventListener(event: TerminateEvent::class)]
final class RecordRequestListener
{
    public function __construct(
        private readonly RequestEventFactory $factory,
        private readonly EventStore $store,
    ) {}

    public function __invoke(TerminateEvent $event): void
    {
        $requestEvent = $this->factory->fromRequestResponse($event->getRequest(), $event->getResponse());
        if ($requestEvent !== null) {
            $this->store->append($requestEvent);
        }
    }
}
