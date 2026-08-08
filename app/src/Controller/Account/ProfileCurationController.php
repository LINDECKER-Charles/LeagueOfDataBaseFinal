<?php
declare(strict_types=1);

namespace App\Controller\Account;

use App\Controller\AbstractPageController;
use App\Controller\Concern\BouncesToProfile;
use App\Controller\Concern\PinsProfileVersion;
use App\Controller\Concern\ResolvesCurrentUser;
use App\Entity\User;
use App\Service\Audit\Model\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Profile\ChampionSkins;
use App\Service\Profile\FavoriteSelectionSanitizer;
use App\Service\Profile\FavoriteSlot;
use App\Service\Profile\FavoriteSlots;
use App\Service\Profile\ProfileVersionResolver;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Every write the owner performs on their chamber: the four favorite slots, the
 * showcased skin, profile visibility and the pinned patch. Renders nothing —
 * each outcome lands back on /profile, either as JSON for the auto-saving island
 * or as a flash + redirect for the no-JS form.
 *
 * Split from {@see ProfileController}, which only reads: validating a submission
 * needs the sanitizer, the ORM and the audit journal, none of which the page
 * render has any business with.
 */
final class ProfileCurationController extends AbstractPageController
{
    use BouncesToProfile;
    use PinsProfileVersion;
    use ResolvesCurrentUser;

    private const CSRF_TOKEN_ID = 'submit';
    private const SKIN_ID_MAX_LENGTH = 64;

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly EntityManagerInterface $entityManager,
        private readonly FavoriteSlots $favoriteSlots,
        private readonly FavoriteSelectionSanitizer $sanitizer,
        private readonly ProfileVersionResolver $profileVersion,
        private readonly ChampionSkins $skins,
        private readonly TranslatorInterface $translator,
        private readonly AuditLogger $audit,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/profile', name: 'app_profile_save', methods: ['POST'])]
    public function save(Request $request): Response
    {
        // The favorites panel auto-saves via fetch (XHR → JSON); a no-JS submit
        // keeps the flash + redirect round-trip.
        $isXhr = $request->isXmlHttpRequest();
        if ($rejected = $this->csrfRejection($request, $isXhr)) {
            return $rejected;
        }

        $user = $this->currentUser();
        // Validate favorites against the pinned patch, not the volatile browsing
        // one — otherwise a save from an old version would wipe a valid favorite.
        $selection = $this->pinnedSelection($user);

        try {
            $sanitized = $this->sanitizeSubmission($request, $user, $selection);
        } catch (\Throwable $e) {
            // Data layer down: refuse the save rather than silently wiping favorites.
            return $this->saveError(
                $isXhr,
                $this->dataError($selection, $e),
                Response::HTTP_SERVICE_UNAVAILABLE
            );
        }

        $this->persistCuration($user, $request, $sanitized);

        return $this->saveSuccess($isXhr, $user, $sanitized['invalid']);
    }

    #[Route('/profile/version', name: 'app_profile_version', methods: ['POST'])]
    public function preferredVersion(Request $request): RedirectResponse
    {
        if (!$this->isCsrfTokenValid(
            self::CSRF_TOKEN_ID,
            (string) $request->request->get('_token')
        )) {
            return $this->backToProfileWithError('profile.flash.csrf');
        }

        $user = $this->currentUser();
        // Empty = clear the pin (follow the browsing version). A stale/unknown
        // version is ignored rather than persisted, keeping resolution sound.
        $selected = trim((string) $request->request->get('preferredVersion', ''));
        $user->setPreferredVersion(
            $selected !== '' && $this->versionManager->versionExists($selected) ? $selected : null,
        );
        $this->entityManager->flush();
        $this->audit->log(AuditAction::ProfileUpdate, metadata: ['section' => 'preferred_version']);

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }

    /** Null when the POSTed token is valid; otherwise the refusal to answer with. */
    private function csrfRejection(Request $request, bool $isXhr): ?Response
    {
        $token = (string) $request->request->get('_token');
        if ($this->isCsrfTokenValid(self::CSRF_TOKEN_ID, $token)) {
            return null;
        }

        return $this->saveError(
            $isXhr,
            $this->translator->trans('profile.flash.csrf'),
            Response::HTTP_FORBIDDEN
        );
    }

    /**
     * Validates the submitted curation against the pinned patch: the favorite
     * slots go through the sanitizer (a slot the data layer cannot resolve is
     * dropped, an unsubmitted one is preserved), the skin id through its format
     * gate. Throws when the data layer is down, so nothing is wiped on an outage.
     *
     * @param array{version: string, lang: string} $selection
     * @return array{values: array<string, ?string>, skin: ?string,
     *               invalid: array{favorites: list<FavoriteSlot>, skin: bool}}
     */
    private function sanitizeSubmission(Request $request, User $user, array $selection): array
    {
        ['version' => $version, 'lang' => $lang] = $selection;
        $favorites = $this->sanitizer->sanitize(
            $this->submittedFavorites($request),
            $this->favoriteSlots->storedIds($user),
            fn (FavoriteSlot $slot, string $id): bool =>
                $this->favoriteSlots->resolve($slot, $id, $version, $lang) !== null,
        );
        $skin = $this->sanitizeSkinFavorite($request);

        return [
            'values' => $favorites['values'],
            'skin' => $skin['value'],
            'invalid' => ['favorites' => $favorites['invalid'], 'skin' => $skin['invalid']],
        ];
    }

    /** @param array{values: array<string, ?string>, skin: ?string} $sanitized */
    private function persistCuration(User $user, Request $request, array $sanitized): void
    {
        $this->favoriteSlots->apply($user, $sanitized['values']);
        $user->setFavoriteSkinId($sanitized['skin']);
        $user->setIsPublicProfile($request->request->getBoolean('isPublicProfile'));
        $this->entityManager->flush();
        $this->audit->log(AuditAction::ProfileUpdate, metadata: ['section' => 'favorites']);
    }

    /**
     * A save that could not happen, in the shape the caller expects: a real
     * failure status + JSON for the auto-saving island, a flash + redirect for
     * the no-JS form.
     */
    private function saveError(bool $isXhr, string $message, int $status): Response
    {
        if ($isXhr) {
            return new JsonResponse(['ok' => false, 'error' => $message], $status);
        }

        $this->addFlash('error', $message);

        return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
    }

    /**
     * A persisted save, reporting whatever the submission had to drop.
     *
     * @param array{favorites: list<FavoriteSlot>, skin: bool} $invalid
     */
    private function saveSuccess(bool $isXhr, User $user, array $invalid): Response
    {
        if (!$isXhr) {
            $this->flashSaveOutcome($invalid['favorites'], $invalid['skin']);

            return $this->redirectToRoute('app_profile', status: Response::HTTP_SEE_OTHER);
        }

        return new JsonResponse([
            'ok' => true,
            'invalidFavorites' => array_map(
                static fn (FavoriteSlot $slot): string => $slot->value,
                $invalid['favorites']
            ),
            'skinInvalid' => $invalid['skin'],
            'isPublicProfile' => $user->isPublicProfile(),
        ]);
    }

    /** @return array<string, ?string> raw submitted id keyed by slot value */
    private function submittedFavorites(Request $request): array
    {
        $submitted = [];
        foreach (FavoriteSlot::cases() as $slot) {
            $value = $request->request->get($slot->fieldName());
            $submitted[$slot->value] = $value === null ? null : (string) $value;
        }

        return $submitted;
    }

    /**
     * Skin banner favorite: art is CDN-hotlinked and self-validating (a bad id
     * 404s and the hero's onerror hides it), so a format + length gate is enough
     * — no data-layer existence check, which keeps the save outage-proof.
     *
     * @return array{value: ?string, invalid: bool}
     */
    private function sanitizeSkinFavorite(Request $request): array
    {
        $raw = trim((string) $request->request->get('favoriteSkinId', ''));
        if ($raw === '') {
            return ['value' => null, 'invalid' => false];
        }
        if (mb_strlen($raw) > self::SKIN_ID_MAX_LENGTH || !$this->skins->isWellFormed($raw)) {
            return ['value' => null, 'invalid' => true];
        }

        return ['value' => $raw, 'invalid' => false];
    }

    /** @param list<FavoriteSlot> $invalid */
    private function flashSaveOutcome(array $invalid, bool $skinInvalid): void
    {
        foreach ($invalid as $slot) {
            $this->addFlash('warning', $this->translator->trans(
                'profile.flash.favorite_unavailable',
                ['%slot%' => $this->translator->trans('profile.slot.'.$slot->value)]
            ));
        }
        if ($skinInvalid) {
            $this->addFlash('warning', $this->translator->trans('profile.flash.skin_unavailable'));
        }
        $this->addFlash('success', $this->translator->trans('profile.flash.saved'));
    }
}
