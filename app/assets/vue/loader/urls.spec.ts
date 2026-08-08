import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import {
    BUILD_WARM_PATH,
    destinationForSwitch,
    metaContent,
    parseStreamPayload,
    pathWithoutVersion,
    prepareUrl,
    resolveVersionAndLang,
    resourcesFor,
    versionFromPath,
    warmKey,
} from './urls'

const LATEST = '16.14.1'

function setLocation(href: string): void {
    window.history.replaceState({}, '', href)
}

beforeEach(() => {
    document.head.innerHTML = ''
    setLocation('/')
})

afterEach(() => {
    document.head.innerHTML = ''
})

describe('version prefix', () => {
    it('reads a dotted patch segment and strips it from the route', () => {
        expect(versionFromPath('/15.14.1/champions')).toBe('15.14.1')
        expect(versionFromPath('/champions')).toBe('')
        expect(pathWithoutVersion('/15.14.1/champions')).toBe('/champions')
        expect(pathWithoutVersion('/15.14.1')).toBe('/15.14.1') // no trailing segment
    })
})

describe('resourcesFor', () => {
    it('names the batches of the streaming routes, version prefix aside', () => {
        expect(resourcesFor('/')).toEqual(['champions', 'items', 'runes', 'summoners'])
        expect(resourcesFor('/15.14.1/objects/')).toEqual(['items'])
        expect(resourcesFor('/RUNES')).toEqual(['runes'])
        expect(resourcesFor(BUILD_WARM_PATH)).toEqual(['champions', 'items', 'runes'])
    })

    it('returns nothing for a route with no image batch', () => {
        expect(resourcesFor('/champion/Ahri')).toEqual([])
        expect(resourcesFor('/profile')).toEqual([])
    })
})

describe('resolveVersionAndLang', () => {
    it('prefers the path segment, then the query, then the page meta', () => {
        document.head.innerHTML = '<meta name="dd-version" content="16.0.1">'
            + '<meta name="dd-lang" content="fr_FR">'

        expect(resolveVersionAndLang('/15.14.1/champions?version=9.9.9'))
            .toEqual({ version: '15.14.1', lang: 'fr_FR' })
        expect(resolveVersionAndLang('/champions?version=9.9.9&lang=en_US'))
            .toEqual({ version: '9.9.9', lang: 'en_US' })
        expect(resolveVersionAndLang('/champions'))
            .toEqual({ version: '16.0.1', lang: 'fr_FR' })
    })

    it('honours an explicit override without touching the URL', () => {
        expect(resolveVersionAndLang('/15.14.1/champions', { version: '1.2.3', lang: 'ko_KR' }))
            .toEqual({ version: '1.2.3', lang: 'ko_KR' })
    })

    it('reads a missing meta as empty', () => {
        expect(metaContent('dd-version')).toBe('')
    })
})

describe('warm identity', () => {
    it('keys a warm by selection, path AND pagination', () => {
        const base = warmKey('/champions', LATEST, 'fr_FR')
        expect(base).toBe(`${LATEST}|fr_FR|/champions||`)
        expect(warmKey('/champions?numpage=2&itemperpage=48', LATEST, 'fr_FR'))
            .toBe(`${LATEST}|fr_FR|/champions|2|48`)
        expect(warmKey('/champions?numpage=2', LATEST, 'fr_FR')).not.toBe(base)
    })

    it('carries the pagination into the prepare stream URL', () => {
        expect(prepareUrl('/champions', LATEST, 'fr_FR'))
            .toBe(`/api/loader/prepare?path=%2Fchampions&version=${LATEST}&lang=fr_FR`)
        expect(prepareUrl('/champions?numpage=3&itemperpage=24', LATEST, 'fr_FR'))
            .toBe(
                `/api/loader/prepare?path=%2Fchampions&version=${LATEST}`
                + '&lang=fr_FR&numpage=3&itemperpage=24',
            )
    })
})

describe('destinationForSwitch', () => {
    it('pins a non-latest patch in the path of a resource route', () => {
        setLocation('/champions')
        expect(destinationForSwitch('15.14.1', 'fr_FR', LATEST))
            .toBe('/15.14.1/champions?lang=fr_FR')
    })

    it('drops the path segment when switching back to the latest patch', () => {
        setLocation('/15.14.1/champions')
        expect(destinationForSwitch(LATEST, 'fr_FR', LATEST)).toBe('/champions?lang=fr_FR')
    })

    it('falls back to the query outside the versioned routes', () => {
        setLocation('/')
        expect(destinationForSwitch('15.14.1', 'en_US', LATEST))
            .toBe('/?lang=en_US&version=15.14.1')
    })

    it('keeps the reader on their page across the switch', () => {
        setLocation('/champions?numpage=4&itemperpage=48')
        expect(destinationForSwitch('15.14.1', 'fr_FR', LATEST))
            .toBe('/15.14.1/champions?lang=fr_FR&numpage=4&itemperpage=48')
    })
})

describe('parseStreamPayload', () => {
    it('parses a JSON frame and degrades a malformed one to an empty payload', () => {
        expect(parseStreamPayload(new MessageEvent('item', { data: '{"index":3}' })))
            .toEqual({ index: 3 })
        expect(parseStreamPayload(new MessageEvent('item', { data: 'not json' }))).toEqual({})
    })
})
