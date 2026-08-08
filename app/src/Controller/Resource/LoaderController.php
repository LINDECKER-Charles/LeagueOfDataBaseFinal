<?php
declare(strict_types=1);

namespace App\Controller\Resource;

use App\Service\API\DatasetRef;
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
        $version = trim((string) $request->query->get('version', ''));
        $lang    = trim((string) $request->query->get('lang', ''));
        $steps   = $this->plannedSteps($request, $version, $lang);
        $dataset = new DatasetRef($version, $lang);

        $response = new StreamedResponse(function () use ($steps, $dataset): void {
            $this->streamWarm($steps, $dataset);
        });

        $response->headers->set('Content-Type', 'text/event-stream');
        $response->headers->set('Cache-Control', 'no-cache');
        $response->headers->set('Connection', 'keep-alive');
        // Tell nginx not to buffer the FastCGI response (keeps SSE frames flowing).
        $response->headers->set('X-Accel-Buffering', 'no');

        return $response;
    }

    /**
     * Warm plan for the destination page. Pagination comes straight off this SSE
     * request's query and is passed explicitly, so `loaderSteps()` stays a pure
     * function of its arguments.
     *
     * Validation is query-only (cache-backed, no session): an unknown
     * version/lang simply warms nothing and lets the real visit handle the
     * redirect-to-setup.
     *
     * @return list<array{type: string, perPage: int, page: int}>
     */
    private function plannedSteps(Request $request, string $version, string $lang): array
    {
        if (
            !$this->versionManager->versionExists($version)
            || !$this->versionManager->languageExists($lang)
        ) {
            return [];
        }

        $page = $request->query->has('numpage') ? (int) $request->query->get('numpage') : null;
        $perPage = $request->query->has('itemperpage')
            ? (int) $request->query->get('itemperpage')
            : null;

        return $this->pageContext->loaderSteps(
            (string) $request->query->get('path', ''),
            $page,
            $perPage,
        );
    }

    /**
     * The stream body: datasets first (so the total is known), then the images.
     *
     * @param list<array{type: string, perPage: int, page: int}> $steps
     */
    private function streamWarm(array $steps, DatasetRef $dataset): void
    {
        // Release the session lock LocaleSubscriber acquired at kernel.request,
        // so the multi-second stream doesn't block the user's next request.
        if (\session_status() === \PHP_SESSION_ACTIVE) {
            \session_write_close();
        }

        ['plans' => $plans, 'categories' => $categories, 'total' => $total]
            = $this->collectPlans($steps, $dataset);

        $this->emit('start', ['total' => $total, 'categories' => $categories]);
        $stored = $this->ingest($plans, $dataset->version, $total);
        $this->emit('done', ['stored' => $stored, 'total' => $total]);
    }

    /**
     * Phase A — datasets (cold JSON is fetched here) → per-type plans + total.
     * A type whose plan cannot be collected is reported and skipped, never fatal.
     *
     * @param list<array{type: string, perPage: int, page: int}> $steps
     * @return array{plans: list<array{0: WarmableManagerInterface, 1: string, 2: array<mixed>}>,
     *               categories: array<string, int>, total: int}
     */
    private function collectPlans(array $steps, DatasetRef $dataset): array
    {
        $plans = [];
        $categories = [];
        $total = 0;

        foreach ($steps as $step) {
            $manager = $this->managers[$step['type']] ?? null;
            if ($manager === null) {
                continue;
            }
            $this->emit('phase', ['category' => $step['type']]);
            try {
                $plan = $manager->collectPlan($dataset, $step['perPage'], $step['page']);
            } catch (\Throwable $e) {
                $this->emit('error', ['category' => $step['type'], 'message' => $e->getMessage()]);
                continue;
            }
            $plans[] = [$manager, $step['type'], $plan['entries']];
            $categories[$step['type']] = $plan['missing'];
            $total += $plan['missing'];
        }

        return ['plans' => $plans, 'categories' => $categories, 'total' => $total];
    }

    /**
     * Phase B — images, streamed as each one lands in object storage.
     *
     * @param list<array{0: WarmableManagerInterface, 1: string, 2: array<mixed>}> $plans
     * @return int images actually stored (one progress frame each)
     */
    private function ingest(array $plans, string $version, int $total): int
    {
        $stored = 0;

        foreach ($plans as [$manager, $category, $entries]) {
            echo ': warming ', $category, "\n\n"; // keepalive before the blocking batch fetch
            @\flush();
            try {
                $manager->ingest(
                    $version,
                    $entries,
                    function (string $name) use (&$stored, $category, $total): void {
                        ++$stored;
                        $this->emit('item', [
                            'name' => $name,
                            'category' => $category,
                            'index' => $stored,
                            'total' => $total,
                        ]);
                    },
                );
            } catch (\Throwable $e) {
                $this->emit('error', ['category' => $category, 'message' => $e->getMessage()]);
            }
        }

        return $stored;
    }

    /**
     * One SSE frame, flushed past every buffering layer we control — the client
     * reads progress live, so a buffered frame is a frozen bar.
     *
     * @param array<string, mixed> $data
     */
    private function emit(string $event, array $data): void
    {
        echo 'event: ', $event, "\n";
        echo 'data: ',
            json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
            "\n\n";
        if (\ob_get_level() > 0) {
            @\ob_flush();
        }
        @\flush();
    }
}
