/** Symfony's translator for a message without plural: `strtr` of every `%param%`. */
export function renderLikeSymfony(source, params) {
  return Object.entries(params).reduce(
    (message, [name, value]) => message.replaceAll(`%${name}%`, String(value)),
    source,
  );
}
