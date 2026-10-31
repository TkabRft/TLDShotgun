# Ammunition source authoring

code-synced: 2026-10-31

The editable [Ammunition.blend](Ammunition.blend) contains one isolated asset scene, five named asset collections, six mesh objects, four packed textures, and an icon-lighting setup. The separate models are cast lead buckshot, finer lead birdshot, irregular cut scrap metal, an unmarked grey shell, and an unprinted improvised cardboard carton. The carton is open at one end with a bent-back flap and **six actual shell meshes** visible inside. No added lettering or floating text is present.

The single shell retains the original shell geometry, UVs, and proportions. The carton uses the original box dimensions as its baseline, with new thin folded panels, creases, rough edges, and six copies of the unchanged shell geometry. Small dimensional differences come from cardboard thickness and dents. The original FBX/textures and existing asset GUIDs remain unchanged; only the five new model paths are replaced. The displayed pile counts are decorative and independent of material quantities and combat pellet settings.

See [the box preview](previews/ShotgunScrapshotBox.png) and the other five-model previews in `previews/`. FBX files are Unity source inputs under `Assets/Shotgun/Imports/`; corresponding GLBs in `exports/` are self-contained inputs for the Unity-free builder. The six inventory/HUD PNGs are transparent 256 x 256 renders. Explicit opaque Standard materials under `Assets/Shotgun/Materials/` reference the four new base-colour textures. Both build paths assign materials by exact slot name, including the carton and shell slots.

## Live authoring phases

Use the workspace Blender MCP instructions. `author_ammunition.py` is now a safe loader, not an automatic background scene rebuild. Importing the builder changes nothing. Load `source/ammunition_assets.py` into a namespace and keep that namespace in `bpy.app.driver_namespace`. The functions select only `TLDShotgun_Ammo_Workshop`; unrelated scenes are preserved.

For a fresh task scene, deliberately call `texture_phase()`, `pile_phase()`, and `shell_box_phase()` in order, inspecting the results between phases. Existing populated collections reject accidental rebuilding. To resume a saved project, load the module and attach to the named scene/collections; do not rerun geometry constructors. The source builder intentionally contains no added lettering pipeline.

Call `render_asset(name)` for a 512 px preview and `render_asset(name, 256, True)` for its inventory icon. `export_phase()` exports explicit mesh selections with metre units to FBX and GLB. `roundtrip_phase()` imports each format into temporary scenes and checks fresh objects, dimensions, triangle counts, UVs, and texture loading. Temporary imports are removed without touching the source scene.

`save_library(path)` stages only the task scene and its dependencies. In a separate Blender process, `source/verify_blend.py -- --normalize` converts that staged library into the normal standalone `Ammunition.blend`, reopens it, and verifies scene scope, finite geometry/UVs, closed meshes, packed-texture hashes, and six distinct shells inside the carton. Without `--normalize`, the verifier only reopens/checks the final file. Normalization is an intentional overwrite step, not a connection check.

`source/prepare_art_materials.py` writes only the four new Unity materials and their texture metadata. It preserves existing metadata GUIDs. It neither invokes Unity nor builds a package.

## Validation and limits

`asset-geometry.json`, `asset-roundtrip.json`, and `asset-blend-validation.json` record the final checks. All source meshes have zero nonmanifold edges. Triangle counts are 3,960 / 8,500 / 440 for the three portions, 552 for a single shell, and 6,784 for the open carton with six shells. All ten FBX/GLB round trips passed; maximum dimension error was below 0.000001 m. Four packed texture hashes match the source PNGs. The six-shell carton visual does not create extra inventory items or change its 240 g stack weight.

Run `python source/validate_art.py` to verify current file hashes, material/texture GUID references, PNG headers, and the recorded geometry/interchange reports. These checks do not establish Unity shader appearance or game acceptance.

`prepare_ammunition_data.py` regenerates the new material definitions, updated ammo packaging JSON, six optional recipe templates, localization additions, and `ammunition-definitions.json`. It also updates existing source-prefab weights and replaces the old Bullet references for buckshot/birdshot. Existing recipe names, prefab IDs, and translations are preserved. Edit the generator when changing recipe data, then regenerate and review the result.

Run `python validate_ammunition.py` for non-compiling recipe/material/weight/artwork checks and boundary cases evaluated from the pure spawn expressions in the C# source. This does not replace compilation or in-game tests.

## Unity-free package build

`no-unity-build.json` is the declarative build input. It pins the immutable checked-in `DoubleBarrel.modcomponent` by SHA-256 and selects compatible object/material/catalog templates. The workspace ModBuild pipeline consumes the five GLBs, four textures, six icons and current ModComponent JSON directly. No Unity Editor, Hub, editor module or container is used or downloaded. The earlier SDK editor utility is retained as source-authoring guidance; it is not part of this build path.

The generated package adds five prefabs and 60 native Unity objects, including a separate HUD Sprite. It preserves all 244 original non-container objects byte-for-byte, including shaders' references, existing clips and sounds. New GameObjects have mesh renderers, filters and bounds-derived box colliders. Textures use BC1 for opaque materials and BC3 for transparent icons. Original GUIDs and game IDs are unchanged; deterministic 64-bit internal path IDs have collision checks.

The builder replaces entire owned data sections with the current material, recipe, harvest, localization, spawn and weight definitions, using lowercase filenames. It rejects case collisions, resolves all 18 gear definitions using ModComponent's lookup rules, and checks nine current default blueprints without legacy alternatives. It updates Addressables entries, preload dependencies, size, hash and nonzero CRC, then reopens the package and checks local/external references against both bundles. The runtime DLL embeds the generated package under the existing resource name. The checked-in template is left unchanged and is never silently substituted for a missing generated result.

Structural validation, real JSON-consumer lookup and an isolated .NET 6/Harmony lifecycle test pass. Earlier checks missed case-sensitive lookup and retained legacy data; the first generated package failed registration in game. The corrected runtime streams its package directly into ModComponent and leaves only the framework's standard working cache. Known old standalone archives are removed after successful registration. In game, inspect orientation, collider/interaction behaviour, shader appearance and carton contents; check crafting/disassembly, inventory/HUD icons, weights, firing/reloading, save/load, spawn persistence and milling restoration. See [embedded asset loading](../../docs/ASSET-LOADING.md) for lifecycle and migration details.
