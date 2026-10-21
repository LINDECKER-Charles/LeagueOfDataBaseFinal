import { prerenderReport } from '../../../../testing/fixtures/prerender-report';
import { epochProjection } from './epoch-projection';

const STORAGE = prerenderReport().storage;

describe('epochProjection', () => {
  it('weighs a new epoch as the served one, and costs as much', () => {
    expect(epochProjection(STORAGE)).toEqual({ bytes: 9.24 * 1e9, monthlyCost: 0.0742 });
  });

  it('projects nothing while the served epoch is empty, or missing', () => {
    const [served, retired] = STORAGE.epochs;
    const empty = { ...served, pages: 0, bytes: 0, monthlyCost: 0 };

    expect(epochProjection({ ...STORAGE, epochs: [empty, retired] })).toBeNull();
    expect(epochProjection({ ...STORAGE, epochs: [retired] })).toBeNull();
  });
});
