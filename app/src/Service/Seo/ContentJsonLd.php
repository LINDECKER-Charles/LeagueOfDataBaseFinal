<?php
declare(strict_types=1);

namespace App\Service\Seo;

/**
 * schema.org nodes for the editorial pages that describe the project itself:
 * FAQPage (question/answer pairs), AboutPage, and the Dataset node stating what
 * the site publishes, from which upstream source and under which terms.
 *
 * The Dataset node is what lets an answer engine describe the project without
 * having to infer it from the resource pages: it names the corpus, its version,
 * its provenance and its languages explicitly.
 */
final class ContentJsonLd
{
    /** Data Dragon is the upstream corpus; naming it states provenance, not ownership. */
    public const DATA_SOURCE_URL = 'https://developer.riotgames.com/docs/lol#data-dragon';

    public function __construct(
        private readonly JsonLdEncoder $encoder,
    ) {}

    /**
     * @param list<array{question:string, answer:string}> $entries
     * @return array<string,mixed>
     */
    public function faqPage(array $entries, string $url): array
    {
        $questions = [];
        foreach ($entries as $entry) {
            $question = trim((string) ($entry['question'] ?? ''));
            $answer   = trim(strip_tags((string) ($entry['answer'] ?? '')));
            if ($question === '' || $answer === '') {
                continue;
            }

            $questions[] = [
                '@type'          => 'Question',
                'name'           => $question,
                'acceptedAnswer' => ['@type' => 'Answer', 'text' => $answer],
            ];
        }

        return $this->encoder->prune([
            '@context'   => JsonLdEncoder::SCHEMA_CONTEXT,
            '@type'      => 'FAQPage',
            '@id'        => $url . '#faq',
            'url'        => $url,
            'mainEntity' => $questions,
        ]);
    }

    /**
     * @param array{name:string, url:string, description?:?string, inLanguage?:?string} $fields
     * @return array<string,mixed>
     */
    public function aboutPage(array $fields): array
    {
        return $this->encoder->prune([
            '@context'    => JsonLdEncoder::SCHEMA_CONTEXT,
            '@type'       => 'AboutPage',
            '@id'         => $fields['url'] . '#about',
            'url'         => $fields['url'],
            'name'        => $fields['name'],
            'description' => isset($fields['description']) ? trim((string) $fields['description']) : null,
            'inLanguage'  => $fields['inLanguage'] ?? null,
        ]);
    }

    /**
     * Dataset node for the published Data Dragon corpus.
     *
     * @param array{
     *   name:string, url:string, description:string, version?:?string,
     *   languages?:list<string>, keywords?:list<string>, creatorId?:?string
     * } $fields
     * @return array<string,mixed>
     */
    public function dataset(array $fields): array
    {
        return $this->encoder->prune([
            '@context'      => JsonLdEncoder::SCHEMA_CONTEXT,
            '@type'         => 'Dataset',
            '@id'           => $fields['url'] . '#dataset',
            'url'           => $fields['url'],
            'name'          => $fields['name'],
            'description'   => trim($fields['description']),
            'version'       => $fields['version'] ?? null,
            'inLanguage'    => array_values($fields['languages'] ?? []),
            'keywords'      => array_values($fields['keywords'] ?? []),
            'isBasedOn'     => self::DATA_SOURCE_URL,
            'creator'       => isset($fields['creatorId']) ? ['@id' => $fields['creatorId']] : null,
            'isAccessibleForFree' => true,
        ]);
    }
}
