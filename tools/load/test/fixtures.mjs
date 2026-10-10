// A stack's answers as the k6 setup reads them, cut down: /api/meta and the latest en_US
// lists, enough for every scenario to walk.

export const META = {
  latest: '16.20.1',
  versions: ['16.21.1', '16.20.1', '16.19.1', '15.13.1', '9.3.1', '0.151.2'],
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
    { locale: 'zh-hans', language: 'zh_CN' },
  ],
  languages: ['en_US', 'fr_FR', 'ko_KR', 'zh_CN'],
};

export const LISTS = {
  champions: {
    entries: [
      { id: 'Ahri', canonicalPath: 'champions/Ahri' },
      { id: 'MonkeyKing', canonicalPath: 'champions/MonkeyKing' },
    ],
  },
  items: {
    entries: [
      { id: '1036', canonicalPath: 'items/1036-long-sword' },
      { id: '3031', canonicalPath: 'items/3031-infinity-edge' },
      { id: '771036', canonicalPath: 'items/771036-long-sword' },
    ],
  },
  runes: {
    entries: [{ id: 8112, canonicalPath: 'runes/8100-domination' }],
    trees: [
      { id: 8100, key: 'Domination', canonicalPath: 'runes/8100-domination' },
      { id: 8000, key: 'Precision', canonicalPath: 'runes/8000-precision' },
    ],
  },
  summoners: {
    entries: [{ id: 'SummonerFlash', canonicalPath: 'summoners/SummonerFlash' }],
  },
};
