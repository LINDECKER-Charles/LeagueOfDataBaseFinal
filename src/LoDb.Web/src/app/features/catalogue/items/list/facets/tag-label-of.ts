const CAMEL_CASE_BOUNDARY = /([a-z])([A-Z])/g;

/** The display form of a Data Dragon tag: `CriticalStrike` reads "Critical Strike". */
export function tagLabelOf(tag: string): string {
  return tag.replace(CAMEL_CASE_BOUNDARY, '$1 $2');
}
