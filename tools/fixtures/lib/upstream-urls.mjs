// URLs of the recorded upstream documents, spelled as the ingestion requests them
// (src/LoDb.Ingestion/Ddragon/DdragonUrls.cs and CommunityDragonUrls.cs).

const DDRAGON = 'https://ddragon.leagueoflegends.com';
const CDN = `${DDRAGON}/cdn`;
const CDRAGON = 'https://raw.communitydragon.org';
const CDRAGON_GAME_DATA = 'plugins/rcp-be-lol-game-data/global/default';

/** Alias CommunityDragon serves its most recent patch under. */
export const CDRAGON_LATEST = 'latest';

export const versionsUrl = () => `${DDRAGON}/api/versions.json`;

export const languagesUrl = () => `${CDN}/languages.json`;

export const datasetUrl = (version, language, file) =>
  `${CDN}/${version}/data/${language}/${file}.json`;

export const championDetailUrl = (version, language, championId) =>
  `${CDN}/${version}/data/${language}/champion/${championId}.json`;

/** An image path relative to the CDN root, as LoDb.Domain's DdragonImagePath builds it. */
export const imageUrl = (relativePath) => `${CDN}/${relativePath}`;

export const skinsUrl = (patch) => `${CDRAGON}/${patch}/${CDRAGON_GAME_DATA}/v1/skins.json`;

/** "15.13.1" → "15.13": CommunityDragon cuts one directory per major.minor patch. */
export const cdragonPatch = (version) => version.split('.').slice(0, 2).join('.');
