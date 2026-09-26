import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';

/** Largest page the API serves (PageRequest.MaxSize); a larger one asks for the whole list. */
const MAX_PAGE_SIZE = 200;

/** The page part of a list call: one page, or nothing for the whole list. */
export function pageRequestOf(page: number, size: number): { page?: number; size?: number } {
  return size === PAGE_SIZE_ALL || size > MAX_PAGE_SIZE ? {} : { page, size };
}
