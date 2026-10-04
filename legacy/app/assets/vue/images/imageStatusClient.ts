/**
 * Client of `GET /api/images/{type}` — the read-only manifest poll behind the
 * in-page refresh of a cold list (App\Controller\Api\ImageStatusController).
 */
export interface ImageCandidates {
    src: string
    webp: string | null
}

export interface ImageStatus {
    /** Settled names: browser-ready paths, or null for a definitive absence. */
    images: Record<string, ImageCandidates | null>
    /** Names still absent from storage. */
    pending: string[]
}

export interface ImageStatusQuery {
    type: string
    version: string
    names: readonly string[]
    /** The caller's last attempt — the only one the server may re-queue work on. */
    isLastAttempt: boolean
}

export async function fetchImageStatus(
    query: ImageStatusQuery,
    signal: AbortSignal,
): Promise<ImageStatus> {
    const params = new URLSearchParams({ version: query.version, names: query.names.join(',') })
    if (query.isLastAttempt) params.set('retry', '1')

    const response = await fetch(`/api/images/${encodeURIComponent(query.type)}?${params}`, {
        signal,
        headers: { Accept: 'application/json' },
    })
    if (!response.ok) {
        throw new Error(`image status ${response.status}`)
    }
    return (await response.json()) as ImageStatus
}
