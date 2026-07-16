<?php

declare(strict_types=1);

namespace App\EventSubscriber;

use App\Service\Client\ClientManager;
use App\Service\I18n\UiLocaleResolver;
use Symfony\Component\EventDispatcher\EventSubscriberInterface;
use Symfony\Component\HttpKernel\Event\RequestEvent;
use Symfony\Component\HttpKernel\KernelEvents;

/**
 * Drives the interface locale from the visitor's selected Data Dragon language
 * (session or signed "remember" cookie). The domain TLD only seeds the default
 * used until a language has been picked: ".fr" -> fr, anything else -> en.
 *
 * Runs before Symfony's LocaleListener (priority 16) so the explicitly set
 * request locale is the one the translator picks up.
 */
final class LocaleSubscriber implements EventSubscriberInterface
{
    public function __construct(
        private readonly ClientManager $clientManager,
        private readonly UiLocaleResolver $resolver,
    ) {}

    public static function getSubscribedEvents(): array
    {
        return [KernelEvents::REQUEST => [['onKernelRequest', 20]]];
    }

    public function onKernelRequest(RequestEvent $event): void
    {
        if (!$event->isMainRequest()) {
            return;
        }

        $request = $event->getRequest();
        $domainDefault = str_ends_with($request->getHost(), '.fr') ? 'fr' : 'en';

        $selected = $this->clientManager->getSelectedLocale();
        $request->setLocale($this->resolver->resolve($selected, $domainDefault));
    }
}
