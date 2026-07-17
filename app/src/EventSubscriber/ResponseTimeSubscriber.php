<?php
declare(strict_types=1);

namespace App\EventSubscriber;

use Symfony\Component\EventDispatcher\EventSubscriberInterface;
use Symfony\Component\HttpKernel\Event\ResponseEvent;
use Symfony\Component\HttpKernel\KernelEvents;

/**
 * Stamps every main-request response with an `X-Runtime` header carrying the
 * server processing time in milliseconds (elapsed since REQUEST_TIME_FLOAT).
 *
 * The detail-page load-time badge reads it off the Turbo fetch response to show
 * the real server figure on soft navigations; it is also useful standalone in
 * devtools / curl for any route. The value measures a wider span than the
 * inline {@see \App\Twig\PerformanceExtension::pageRenderMs()} (it runs after the
 * whole response is built), which is why the two can differ by a few ms.
 */
final class ResponseTimeSubscriber implements EventSubscriberInterface
{
    private const HEADER = 'X-Runtime';

    public static function getSubscribedEvents(): array
    {
        return [KernelEvents::RESPONSE => 'onKernelResponse'];
    }

    public function onKernelResponse(ResponseEvent $event): void
    {
        if (!$event->isMainRequest()) {
            return;
        }

        $start = $event->getRequest()->server->get('REQUEST_TIME_FLOAT');
        if (!is_float($start) && !is_int($start)) {
            return;
        }

        $event->getResponse()->headers->set(
            self::HEADER,
            (string) round((microtime(true) - (float) $start) * 1000, 1),
        );
    }
}
