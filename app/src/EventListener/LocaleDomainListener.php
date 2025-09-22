<?php

namespace App\EventListener;

use Symfony\Component\HttpKernel\Event\RequestEvent;

class LocaleDomainListener
{
    public function onKernelRequest(RequestEvent $event): void
    {
        $request = $event->getRequest();
        $host = $request->getHost(); // ex: "monsite.fr" ou "monsite.com"

        if (str_ends_with($host, '.fr')) {
            $request->setLocale('fr');
        } else {
            $request->setLocale('en'); // fallback par défaut
        }
    }
}
