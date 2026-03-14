# Entity Blueprint Editor

## Entry
- Menu: `PurgeLine/Entity Blueprint Editor`

## Main Workflow
1. Click `New` to create an empty blueprint.
2. Add components from left library (`Favorites` / `Category`).
3. Edit fields in component cards.
4. Click `Save` and choose `.entitybp` path.
5. Click `Create Instance`:
   - Play mode: instantiate directly in current ECS world.
   - Edit mode: create `EntityBlueprintAuthoring` object for SubScene baking.

## Quick Test
- Click `Run Test`:
  1. Save current blueprint.
  2. Enter Play Mode.
  3. Instantiate entity at `(0,0,0)`.
  4. Move Scene view camera to origin.

## Validation
- Click `Validate Data` to run serialization round-trip and open diff window.

## Notes
- Supported field types: `int`, `float`, `bool`, `Vector2/3/4`, `float2/3/4`, `FixedString64Bytes`, `Entity`.
- Auto-save: every 20s when document is dirty and has a file path.

