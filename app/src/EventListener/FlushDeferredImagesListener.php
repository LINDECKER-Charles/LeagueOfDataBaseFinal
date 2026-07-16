<?php
declare(strict_types=1);

namespace App\EventListener;

use App\Service\Storage\DeferredImageIngestor;
use Symfony\Component\EventDispatcher\Attribute\AsEventListener;
use Symfony\Component\HttpKernel\Event\TerminateEvent;

/**
 * After the response is sent, flush any image ingestion a cold render deferred
 * ({@see DeferredImageIngestor}). The user already got a fast page; the images
 * land in object storage so the next visit is warm. php-fpm's
 * fastcgi_finish_request means this runs off the request's critical path.
 */
#[AsEventListener(event: TerminateEvent::class)]
final class FlushDeferredImagesListener
{
    public function __construct(private readonly DeferredImageIngestor $ingestion) {}

    public function __invoke(TerminateEvent $event): void
    {
        $this->ingestion->flush();
    }
}
