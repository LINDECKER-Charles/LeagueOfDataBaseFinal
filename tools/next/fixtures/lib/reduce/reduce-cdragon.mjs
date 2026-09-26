// Reduction of CommunityDragon's v1/skins.json (several MB): the skins of the recorded
// champions, slimmed to the fields the chroma normalization reads.

// Skin ids are the champion key times 1000 plus the skin number ("103001").
const SKINS_PER_CHAMPION = 1000;

/** @param {Iterable<string>} championKeys numeric champion keys ("103") */
export function reduceSkins(skinsById, championKeys) {
  const kept = new Set([...championKeys].map(Number));
  const document = {};
  for (const [id, skin] of Object.entries(skinsById)) {
    if (kept.has(Math.floor(Number(id) / SKINS_PER_CHAMPION))) {
      document[id] = slimSkin(skin);
    }
  }
  return {
    document,
    reduction: 'skins of the recorded champions; id, name and chromas fields only',
  };
}

function slimSkin(skin) {
  const slim = { id: skin.id, name: skin.name };
  if (Array.isArray(skin.chromas)) {
    slim.chromas = skin.chromas.map((chroma) => ({
      id: chroma.id,
      name: chroma.name,
      chromaPath: chroma.chromaPath,
      colors: chroma.colors,
    }));
  }
  return slim;
}
