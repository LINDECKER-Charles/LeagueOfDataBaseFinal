import type { JsonLdNode } from '../core/json-ld-node';
import { plainText } from '../core/plain-text';
import { pruneJsonLd } from '../core/prune-json-ld';
import { SCHEMA_ORG } from '../core/schema-org';

/** The FAQ's questions and answers; a pair missing either side is left out. */
export function faqPage(
  entries: readonly { readonly question: string; readonly answer: string }[],
  url: string,
): JsonLdNode {
  const questions = entries.flatMap((entry) => {
    const question = plainText(entry.question);
    const answer = plainText(entry.answer);
    return question === null || answer === null
      ? []
      : [
          {
            '@type': 'Question',
            name: question,
            acceptedAnswer: { '@type': 'Answer', text: answer },
          },
        ];
  });
  return pruneJsonLd({
    '@context': SCHEMA_ORG,
    '@type': 'FAQPage',
    '@id': `${url}#faq`,
    url,
    mainEntity: questions,
  });
}
