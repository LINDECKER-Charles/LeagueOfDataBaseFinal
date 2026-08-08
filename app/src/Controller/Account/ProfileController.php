<?php
declare(strict_types=1);

namespace App\Controller\Account;

use App\Controller\AbstractPageController;
use App\Controller\Concern\PinsProfileVersion;
use App\Controller\Concern\ResolvesCurrentUser;
use App\Entity\User;
use App\Form\SetPasswordFormType;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Profile\ChampionSkins;
use App\Service\Profile\FavoriteSlots;
use App\Service\Profile\ProfilePresenter;
use App\Service\Profile\ProfileVersionResolver;
use App\Service\Profile\PublicProfileView;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * The summoner's chamber as the owner sees it: the private page and the preview
 * of the public card. Read-only — every write the owner performs there goes to
 * {@see ProfileCurationController}, which needs an entirely different set of
 * services (sanitizer, ORM, audit journal) and changes for other reasons.
 *
 * Identity, password and erasure live in {@see AccountSecurityController}.
 */
final class ProfileController extends AbstractPageController
{
    use PinsProfileVersion;
    use ResolvesCurrentUser;

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly FavoriteSlots $favoriteSlots,
        private readonly ProfileVersionResolver $profileVersion,
        private readonly ChampionSkins $skins,
        private readonly PublicProfileView $publicView,
        private readonly ProfilePresenter $presenter,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/profile', name: 'app_profile', methods: ['GET'])]
    public function index(Request $request): Response
    {
        $user = $this->currentUser();
        // Favorites resolve at the pinned patch when set, so a favorite absent
        // from the browsing version neither disappears nor gets wiped on save.
        ['version' => $version, 'lang' => $lang] = $this->pinnedSelection($user);
        $panels = $this->curationPanel($user, $version, $lang)
            + $this->identityPanel($user, $request->getLocale());

        return $this->render('profile/index.html.twig', $panels + [
            'client' => $this->clientData(),
            'user' => $user,
            'version' => $version,
            'versions' => $this->versionManager->getVersions(),
            'preferredVersion' => $user->getPreferredVersion(),
            'lang' => $lang,
        ]);
    }

    #[Route('/profile/preview', name: 'app_profile_preview', methods: ['GET'])]
    public function preview(Request $request): Response
    {
        $user = $this->currentUser();
        ['version' => $version, 'lang' => $lang] = $this->pinnedSelection($user);

        // The owner sees their own public card verbatim — even while private —
        // rendered from the same builder the /u/{username} route uses.
        return $this->render('profile/public.html.twig', [
            'client' => $this->clientData(),
            'preview' => true,
        ] + $this->publicView->build($user, $version, $lang, $request->getLocale()));
    }

    /**
     * What the owner curates: the showcased skin, the four favorite slots and the
     * personal-page backdrop — which is the favorite CHAMPION, never the skin (the
     * skin is the public showcase). `heroBackground(null, …)` yields exactly the
     * champion art, or null when no champion favorite is set.
     *
     * @return array<string, mixed>
     */
    private function curationPanel(User $user, string $version, string $lang): array
    {
        return [
            'skinBanner' => $this->skins->resolveBanner(
                $user->getFavoriteSkinId(),
                $version,
                $lang
            ),
            'favorites' => $this->favoriteSlots->resolveAll($user, $version, $lang),
            'personalBackground' => $this->skins->heroBackground(
                null,
                $user->getFavoriteChampionId()
            ),
        ];
    }

    /**
     * The account facts the page states about itself.
     *
     * @return array<string, mixed>
     */
    private function identityPanel(User $user, string $locale): array
    {
        return [
            'maskedEmail' => $this->presenter->maskEmail($user->getEmail()),
            'memberSince' => $this->presenter->memberSince($user->getCreatedAt(), $locale),
            // OAuth-only accounts get the "set a password" panel.
            'passwordForm' => $user->hasPassword()
                ? null
                : $this->createForm(SetPasswordFormType::class),
        ];
    }
}
