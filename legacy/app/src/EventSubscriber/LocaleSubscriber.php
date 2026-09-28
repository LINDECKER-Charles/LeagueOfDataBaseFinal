<?php

declare(strict_types=1);

namespace App\EventSubscriber;

use App\Service\Client\ClientManager;
use App\Service\I18n\UiLocaleResolver;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\EventDispatcher\EventSubscriberInterface;
use Symfony\Component\HttpKernel\Event\RequestEvent;
use Symfony\Component\HttpKernel\KernelEvents;

/**
 * Drives the interface locale from the visitor's selected Data Dragon language
 * (session or signed "remember" cookie), falling back to the application default
 * until a language has been picked.
 *
 * The domain deliberately plays no part: every host serves the same content in
 * the same language, so the selection is the single source of truth and a given
 * URL renders identically whichever domain it was reached through.
 *
 * Runs before Symfony's LocaleListener (priority 16) so the explicitly set
 * request locale is the one the translator picks up.
 */
final class LocaleSubscriber implements EventSubscriberInterface
{
    public function __construct(
        private readonly ClientManager $clientManager,
        private readonly UiLocaleResolver $resolver,
        #[Autowire('%kernel.default_locale%')]
        private readonly string $defaultLocale,
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

        $selected = $this->clientManager->getSelectedLocale();
        $event->getRequest()->setLocale($this->resolver->resolve($selected, $this->defaultLocale));
    }
}
