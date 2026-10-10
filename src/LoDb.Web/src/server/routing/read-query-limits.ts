import { readPositiveInteger } from '../read-positive-integer';
import type { QueryLimits } from './query-limits';

type Environment = Readonly<Record<string, string | undefined>>;

// Far above a real list URL (a few facets and a search of some words), far below what a
// cache key should hold.
const DEFAULT_MAX_PARAMS = 16;
const DEFAULT_MAX_PARAM_LENGTH = 200;

/**
 * Reads the bounds of a catalogue page's query from `LODB_QUERY_MAX_PARAMS` and
 * `LODB_QUERY_MAX_PARAM_LENGTH`, each optional.
 */
export function readQueryLimits(env: Environment): QueryLimits {
  return {
    maxParams: readPositiveInteger(env, 'LODB_QUERY_MAX_PARAMS', DEFAULT_MAX_PARAMS),
    maxParamLength: readPositiveInteger(
      env,
      'LODB_QUERY_MAX_PARAM_LENGTH',
      DEFAULT_MAX_PARAM_LENGTH,
    ),
  };
}
