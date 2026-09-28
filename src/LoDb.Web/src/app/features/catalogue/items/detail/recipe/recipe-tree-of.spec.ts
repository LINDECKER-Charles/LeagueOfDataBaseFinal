import type { CatalogImage } from '../../../../../core/api/generated/models/catalog-image';
import type { RecipeStep } from '../../../../../core/api/generated/models/recipe-step';
import { recipeTreeOf } from './recipe-tree-of';

const IMAGE: CatalogImage = { status: 'present', url: '/cdn/blobs/a.png' };

function step(id: string, gold: number, components: RecipeStep[] = []): RecipeStep {
  return {
    id,
    name: `Item ${id}`,
    canonicalPath: `items/${id}`,
    image: IMAGE,
    gold,
    combine: 0,
    components,
  };
}

function combined(id: string, gold: number, components: RecipeStep[]): RecipeStep {
  const parts = components.reduce((sum, part) => sum + part.gold, 0);
  return { ...step(id, gold, components), combine: gold - parts };
}

describe('recipeTreeOf', () => {
  it('has no tree for an item built from nothing, nor without a recipe', () => {
    expect(recipeTreeOf(step('1036', 350))).toBeNull();
    expect(recipeTreeOf(null)).toBeNull();
    expect(recipeTreeOf(undefined)).toBeNull();
  });

  it('projects the whole tree, the root linking nowhere and every component to its page', () => {
    const pickaxe = combined('1037', 875, [step('1036', 350)]);
    const tree = recipeTreeOf(combined('3031', 3400, [pickaxe, step('1038', 1300)]));

    expect(tree?.canonicalPath).toBeNull();
    expect(tree?.combine).toBe(1225);
    expect(tree?.components.map((node) => node.canonicalPath)).toEqual([
      'items/1037',
      'items/1038',
    ]);
    expect(tree?.components[0]?.combine).toBe(525);
    expect(tree?.components[0]?.components[0]).toEqual({
      id: '1036',
      name: 'Item 1036',
      image: IMAGE,
      canonicalPath: 'items/1036',
      gold: 350,
      combine: null,
      components: [],
    });
  });

  it('shows no combine cost where nothing is combined, and names an unnamed item by its id', () => {
    const tree = recipeTreeOf(step('3070', 400, [{ ...step('1027', 300), name: '' }]));

    expect(tree?.combine).toBeNull();
    expect(tree?.components[0]?.name).toBe('1027');
  });
});
