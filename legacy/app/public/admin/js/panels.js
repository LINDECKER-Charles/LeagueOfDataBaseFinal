/**
 * Streams the heavy admin panels in after the shell.
 *
 * A dashboard aggregates reports that each cost a cold round trip (S3 deep
 * listing, NDJSON scan, service probes); rendering them inline made the whole
 * page wait on the slowest one. Every panel now ships as a skeleton plus its
 * fragment URL, and they resolve independently and in parallel.
 *
 * No-JavaScript clients never reach this module: the page head redirects them to
 * the `?sync=1` variant, which renders the very same fragments server-side.
 */

import { enhanceCharts } from './chart.js'

const REQUEST_TIMEOUT_MS = 30_000

export function loadPanels(root = document) {
    root.querySelectorAll('[data-panel-url]:not([data-panel-state])').forEach((shell) => {
        load(shell, shell.innerHTML)
    })
}

async function load(shell, skeleton) {
    shell.dataset.panelState = 'loading'
    try {
        shell.innerHTML = await fetchPanel(shell.dataset.panelUrl)
        shell.dataset.panelState = 'ready'
        enhanceCharts(shell)
    } catch (error) {
        shell.dataset.panelState = 'failed'
        shell.replaceChildren(failure(error, () => load(shell, skeleton), shell, skeleton))
    }
}

async function fetchPanel(url) {
    const response = await fetch(url, {
        credentials: 'same-origin',
        headers: { 'X-Requested-With': 'fetch' },
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    })
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
    }

    return response.text()
}

function failure(error, retry, shell, skeleton) {
    const alert = document.createElement('div')
    alert.className = 'alert'
    const text = document.createElement('span')
    text.textContent = 'Panneau indisponible ('
        + `${error.name === 'TimeoutError' ? 'délai dépassé' : error.message}). `
    const button = document.createElement('button')
    button.type = 'button'
    button.className = 'btn ghost small'
    button.textContent = 'Réessayer'
    button.addEventListener('click', () => {
        shell.innerHTML = skeleton
        retry()
    })
    alert.append(text, button)

    return alert
}
