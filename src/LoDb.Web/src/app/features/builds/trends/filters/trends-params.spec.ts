import { convertToParamMap } from '@angular/router';
import { trendsQueryOf } from './trends-query-of';
import { filterParamsOf, pageParamsOf } from './trends-params';

describe('trendsQueryOf', () => {
  it('reads the filters and the page of the URL', () => {
    const params = convertToParamMap({
      champion: 'MonkeyKing',
      mode: 'aram',
      language: 'fr_FR',
      page: '3',
    });

    expect(trendsQueryOf(params)).toEqual({
      champion: 'MonkeyKing',
      mode: 'aram',
      language: 'fr_FR',
      page: 3,
    });
  });

  it('reads blank or absent filters as no filter, on the first page', () => {
    const params = convertToParamMap({ champion: '  ', mode: '' });

    expect(trendsQueryOf(params)).toEqual({ champion: null, mode: null, language: null, page: 1 });
  });

  it('keeps a mode or a language it does not know, for the API to ignore', () => {
    const params = convertToParamMap({ mode: 'urf', language: 'xx_XX' });

    expect(trendsQueryOf(params)).toMatchObject({ mode: 'urf', language: 'xx_XX' });
  });

  it.each(['0', '-2', '1.5', 'two', '99999999999999999999'])(
    'reads the page %s as the first',
    (page) => {
      expect(trendsQueryOf(convertToParamMap({ page })).page).toBe(1);
    },
  );
});

describe('filterParamsOf', () => {
  it('sets the filters chosen and starts again at the first page', () => {
    expect(filterParamsOf({ champion: 'Ahri', mode: 'sr', language: 'en_US' })).toEqual({
      champion: 'Ahri',
      mode: 'sr',
      language: 'en_US',
      page: null,
    });
  });

  it('removes the filters left blank from the URL', () => {
    expect(filterParamsOf({ champion: '', mode: null, language: ' ' })).toEqual({
      champion: null,
      mode: null,
      language: null,
      page: null,
    });
  });
});

describe('pageParamsOf', () => {
  it('names the pages after the first, and leaves the first bare', () => {
    expect(pageParamsOf(2)).toEqual({ page: 2 });
    expect(pageParamsOf(1)).toEqual({ page: null });
  });
});
