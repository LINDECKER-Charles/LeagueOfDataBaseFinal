<?php
declare(strict_types=1);

namespace App\Controller\Concern;

use App\Entity\Build;
use Symfony\Component\HttpFoundation\Response;

/**
 * The two gates every write on a build passes: it must belong to the signed-in
 * owner, and minting public content is reserved to confirmed accounts. Shared by
 * the authoring and import controllers, which must apply them identically.
 *
 * For controllers using {@see ResolvesCurrentUser} that inject a
 * {@see \App\Repository\BuildRepository} as `$builds` and a
 * {@see \Symfony\Contracts\Translation\TranslatorInterface} as `$translator`.
 */
trait OwnsBuilds
{
    /** 404 (never 403) when the build does not exist OR belongs to someone else. */
    private function ownedOr404(int $id): Build
    {
        $build = $this->builds->find($id);
        if ($build === null || $build->getOwner()?->getId() !== $this->currentUser()->getId()) {
            throw $this->createNotFoundException('Build not found.');
        }

        return $build;
    }

    /**
     * Build creation is the one write reserved to confirmed accounts (anti-spam
     * of public content). Returns a redirect to bounce unverified users, or null
     * to let the caller proceed.
     */
    private function requireVerifiedEmail(): ?Response
    {
        if ($this->currentUser()->isVerified()) {
            return null;
        }

        $this->addFlash('warning', $this->translator->trans('auth.verify.gate_build'));

        return $this->redirectToRoute('app_builds', status: Response::HTTP_SEE_OTHER);
    }
}
