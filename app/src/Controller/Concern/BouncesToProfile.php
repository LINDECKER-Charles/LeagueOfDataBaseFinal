<?php
declare(strict_types=1);

namespace App\Controller\Concern;

use Symfony\Component\Form\FormInterface;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Response;

/**
 * The profile panels post to their own endpoints and render no view of their
 * own: every outcome lands back on /profile with a flash. Single home of that
 * bounce, shared by the two controllers serving the summoner's chamber.
 *
 * For controllers extending {@see \Symfony\Bundle\FrameworkBundle\Controller\AbstractController}
 * that inject a {@see \Symfony\Contracts\Translation\TranslatorInterface} as `$translator`.
 */
trait BouncesToProfile
{
    private function backToProfileWithError(string $messageKey): RedirectResponse
    {
        $this->addFlash('error', $this->translator->trans($messageKey));

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }

    /** Form errors are already translated (messages domain, see PasswordFieldOptions). */
    private function backToProfileWithFormErrors(FormInterface $form): RedirectResponse
    {
        $hasFlashed = false;
        foreach ($form->getErrors(true) as $error) {
            $this->addFlash('error', $error->getMessage());
            $hasFlashed = true;
        }
        if (!$hasFlashed) {
            return $this->backToProfileWithError('profile.flash.csrf');
        }

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }
}
