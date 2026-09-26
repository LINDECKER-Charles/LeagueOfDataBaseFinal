import type { TrendsQuery } from './trends-query';

/** The three filters of the trends, the page left aside. */
export type TrendsFilters = Pick<TrendsQuery, 'champion' | 'mode' | 'language'>;
