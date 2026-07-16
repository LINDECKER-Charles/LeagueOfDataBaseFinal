<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\API\WarmableManagerInterface;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\DependencyInjection\Attribute\AutowireIterator;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\StreamedResponse;
use Symfony\Component\Routing\Annotation\Route;

/**
 * Server-Sent Events endpoint that warms a destination page's DDragon images
 * inline while streaming live progress, so the loader overlay shows a real
 * determinate bar and names each resource as it lands. The client runs this to
 * completion, THEN performs the (now warm) Turbo visit — see ResourceLoader.vue.
 *
 * Unlike a user render (which defers cold image ingestion to kernel.terminate),
 * this path ingests synchronously via {@see WarmableManagerInterface::ingest()}
 * so progress can be observed. Version/lang come from the query only and the
 * session lock is released before streaming, so a multi-second warm never
 * starves the user's concurrent requests.
 */
final class LoaderController extends AbstractController
{
    /** @var array<string,WarmableManagerInterface> resource type => manager */
    private readonly array $managers;

    /**
     * @param iterable<WarmableManagerInterface> $managers
     */
    public function __construct(
        #[AutowireIterator('app.ddragon.manager')]
        iterable $managers,
        private readonly PageContextResolver $pageContext,
        private readonly VersionManager $versionManager,
    ) {
        $byType = [];
        foreach ($managers as $manager) {
            $byType[$manager->type()] = $manager;
        }
        $this->managers = $byType;
    }

    #[Route('/api/loader/prepare', name: 'api_loader_prepare', methods: ['GET'])]
    public function prepare(Request $request): StreamedResponse
    {
        $path    = (string) $request->query->get('path', '');
        $version = trim((string) $request->query->get('version', ''));
        $lang    = trim((string) $request->query->get('lang', ''));
        // Pagination comes straight off this SSE request's query and is passed
        // explicitly, so loaderSteps() stays a pure function of its arguments.
        $page    = $request->query->has('numpage') ? (int) $request->query->get('numpage') : null;
        $perPage = $request->query->has('itemperpage') ? (int) $request->query->get('itemperpage') : null;

        // Query-only validation (cache-backed, no session) → decide the plan up
        // front; an unknown version/lang simply warms nothing and lets the real
        // visit handle the redirect-to-setup.
        $valid = $this->versionManager->versionExists($version)
            && $this->versionManager->languageExists($lang);
        $steps = $valid ? $this->pageContext->loaderSteps($path, $page, $perPage) : [];

        $response = new StreamedResponse(function () use ($steps, $version, $lang): void {
            // Release the session lock LocaleSubscriber acquired at kernel.request,
            // so the multi-second stream doesn't block the user's next request.
            if (\session_status() === \PHP_SESSION_ACTIVE) {
                \session_write_close();
            }

            $emit = static function (string $event, array $data): void {
                echo 'event: ', $event, "\n";
                echo 'data: ', json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n\n";
                if (\ob_get_level() > 0) {
                    @\ob_flush();
                }
                @\flush();
            };

            // Phase A — datasets (cold JSON is fetched here) → per-type plans + total.
            $plans      = [];
            $total      = 0;
            $categories = [];
            foreach ($steps as $step) {
                $manager = $this->managers[$step['type']] ?? null;
                if ($manager === null) {
                    continue;
                }
                $emit('phase', ['category' => $step['type']]);
                try {
                    $plan = $manager->collectPlan($version, $lang, $step['perPage'], $step['page']);
                } catch (\Throwable $e) {
                    $emit('error', ['category' => $step['type'], 'message' => $e->getMessage()]);
                    continue;
                }
                $plans[]                     = [$manager, $step['type'], $plan['entries']];
                $categories[$step['type']]   = $plan['missing'];
                $total                      += $plan['missing'];
            }

            $emit('start', ['total' => $total, 'categories' => $categories]);

            // Phase B — images, streamed as each one lands in object storage.
            $index  = 0;
            $stored = 0;
            foreach ($plans as [$manager, $category, $entries]) {
                echo ": warming ", $category, "\n\n"; // keepalive before the blocking batch fetch
                @\flush();
                try {
                    $manager->ingest(
                        $version,
                        $entries,
                        static function (string $name) use (&$index, &$stored, $category, $total, $emit): void {
                            ++$index;
                            ++$stored;
                            $emit('item', ['name' => $name, 'category' => $category, 'index' => $index, 'total' => $total]);
                        },
                    );
                } catch (\Throwable $e) {
                    $emit('error', ['category' => $category, 'message' => $e->getMessage()]);
                }
            }

            $emit('done', ['stored' => $stored, 'total' => $total]);
        });

        $response->headers->set('Content-Type', 'text/event-stream');
        $response->headers->set('Cache-Control', 'no-cache');
        $response->headers->set('Connection', 'keep-alive');
        // Tell nginx not to buffer the FastCGI response (keeps SSE frames flowing).
        $response->headers->set('X-Accel-Buffering', 'no');

        return $response;
    }
}
