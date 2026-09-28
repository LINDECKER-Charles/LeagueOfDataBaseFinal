import type { RecipeStep } from '../../../../../core/api/generated/models/recipe-step';
import type { RecipeNode } from './recipe-node';

function nodeOf(step: RecipeStep, isRoot: boolean): RecipeNode {
  const components = step.components.map((component) => nodeOf(component, false));
  return {
    id: step.id,
    name: step.name || step.id,
    image: step.image,
    canonicalPath: isRoot ? null : step.canonicalPath,
    gold: step.gold,
    combine: components.length > 0 && step.combine > 0 ? step.combine : null,
    components,
  };
}

/**
 * The crafting tree of an item page, from the item down to the base items: the root is the
 * page itself and links nowhere, every component links to its own page, and the combine
 * cost only shows where components are combined. An item built from nothing has no tree.
 */
export function recipeTreeOf(recipe: RecipeStep | null | undefined): RecipeNode | null {
  return recipe && recipe.components.length > 0 ? nodeOf(recipe, true) : null;
}
