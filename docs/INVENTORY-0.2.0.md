# Full inventory and semantic display runtime

Build: Steam 26880829. Inventory: 48 files, 2,444 source entries. Accepted regression baseline: 0.1.6-rc.1. Next diagnostic candidate: 0.1.7-probe.1.

## Translation workflow
The production source register is `baseline/inventory.json`. `inventory-translations/` holds ParaTranz snapshots, including review stages; `inventory-import/` contains original-only files with empty translations. Existing 89 key/original pairs and the three remote file IDs are preserved. `villians/` deliberately retains the existing platform spelling. Legacy `translations/`, `baseline/registry.json` and `paratranz-project.json` are compatibility fixtures, not the expanded sync destination.

The 48 files comprise 12 heroes, 6 villains, 6 environments, 6 event files, 1 collection file, 8 UI files, 4 display-alias files and 5 dynamic-candidate files. Titles, rules, quotes and references remain beside their associated deck. New entries are untranslated; no diagnostic markers are present in production output.

`Import ParaTranz snapshot` has three manually dispatched operations:
- `plan`: preview missing files and added keys, with no remote writes.
- `migrate`: preflight every existing file, then append/update originals only. It never sends translation or stage fields. Existing file IDs, translations and stages are checked after migration. Checkpoints retain the prior snapshot if the server rejects a later operation. A partial migration is resumable; no remote file is deleted.
- `sync`: read all configured files, validate exact keys/originals and file identity, build a candidate, then open a snapshot PR. No automatic merge or release.

The first migration must run after this workflow exists on the default branch. Existing `PARATRANZ_TOKEN` secret is used only by Actions. Do not paste credentials into issues or files. Resolve any unexpected remote originals/keys instead of forcing the migration.

Online expansion completed on 2026-11-26: 37 existing files matched; 11 files containing 110 originals were added. The post-migration snapshot contains all 48 files and 2,444 keys with the existing translation/review states retained. The two visually accepted Baron Blade short lines were then applied separately by an exact, two-key guarded update; a second full pull confirms all 89 translations match the accepted local baseline. New 2,355 translations remain empty. All 48 remote file IDs are now pinned. Actions runs: 45849603351 (expansion), 45850652622 (approved short lines and final snapshot).

## Runtime boundary
The tested display surfaces now have semantic routes: card-scoped titles/subtitles/quotes/references, PowerTitle rich-text contents, comma-separated visible keywords, HUD/inline/styled phase labels, and PowerHeading. Phase translations must be complete names (for example 开始阶段), although the source inventory uses the enum label Start. Game enums, rule text storage, identifiers and damage calculations are never modified.

Quotes expand the authored `{BR}` to the observed CRLF display form and retain quote wrappers. Inline styles keep all markup; surrounding damage values, target names and other rule text are untouched. Unknown content remains English. A translated field affects only its registered card/object/style.

524 entries are held as inventory only: 520 unverified routes (including variants, events, collections and dynamic sentence fragments), plus 4 original-markup entries requiring review (including two potentially paired fragments). A translator may edit them, but the builder reports them as not emitted. This number is a route inventory, not a claim of 1,920 live-tested translations.

The 0.1.4 body replacement guard and TMP numeric-buffer/dirty-string exclusions are retained. No new native method is hooked. The probe capture class and all one-character test prefixes are absent. The 0.1.6-rc.1 Windows regression was accepted by the user on 2026-11-27: keyword/phase/Power labels, damage/healing HP display, save/load and returning to the menu. This is scoped acceptance, not coverage of every registered route.

## Validation
Run `python3 -m unittest discover -s tests -v`, then `python3 tools/inventory.py --out build`, then `dotnet run --project tests/runtime/RuntimeChecks.csproj -- build/sample-texts.json`.

To compile the game plugin locally, supply the matching extracted interop and BepInEx reference directories as `SotM2InteropDir` and `BepInExReferenceDir` MSBuild properties. These game/runtime dependencies are not committed. CI builds the pure display transformer tests without game binaries.

No artificial line-break edits were introduced. POWER prefix indentation and Chinese typography will be reviewed after actual translated labels are enabled. Advanced styled phase handling is implemented, but the general Advanced heading remains inventory-only until that label is separately registered and tested.

The optional downloadable term-test catalog uses only 9 frozen glossary entries (five phase names, Power label, One-Shot/Ongoing/Limited) to exercise semantic routes. These test translations are not uploaded to ParaTranz. Offline validation: 65 Python checks, 18 C# semantic checks, and 7 installer/restore checks; Windows verification of those optional terms and the stated regression scenarios passed. Hero variants are deferred because the user reports no accessible entry in the current EA build.
