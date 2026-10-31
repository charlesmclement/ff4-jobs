# FF4 Jobs 0.2.0 alpha candidate — validation

Validated locally on 2026-10-31 with Steam build 20265023, Unity 2019.4.26f1, BepInEx Unity IL2CPP x64 be.735 and Proton Experimental. This records observed checks, not full-campaign certification.

| Check | Result | Evidence |
|---|---|---|
| Release build | Pass, zero warnings/errors | `scripts/build.sh` |
| Core progression/save/balance checks | 830 pass | `src/FF4Jobs.Tests/Program.cs` |
| Native equipment eligibility hook against master equipment | 2,640 cases pass | `research/release-pass/native-equipment-probe.json` |
| Native probe leaves inventory counts unchanged | Pass | Same probe JSON |
| Untrained caster subjob leaves native max MP unchanged | Pass | Same probe JSON |
| Jobs character picker | Names and portraits separated; native font, blue backing, native border | `research/release-pass/final-review.png` |
| Jobs detail screen | Available jobs, ABP numbers/bar, equipment/stat descriptions render | `research/release-pass/jobs-detail.png` |
| Keyboard confirm/cancel/navigation | Tested through faces, slots and job selection using native legacy bindings | Live session |
| Controller actions | Native configured controller path retained; physical A/B/stick validation pending | `MenuInput.cs`; acceptance pending |
| Unlock popup | One OK; Thief only at Mist; keyboard Cancel dismisses and restores field input | `research/release-pass/popup.png` |
| Main-job equipment reconciliation | Rydia Thief returns rod; Cecil Thief returns five incompatible slots via native unequip | `research/release-pass/verified-live.log` |
| Steal failure in real battle | Rydia 27% against stronger enemy; valid native Miss and game continues | Same live log |
| Steal success in real battle | Cecil 53% against Goblin; native Hit; screen shows Stole Potion ×1 | `research/release-pass/steal-success.png`; same live log |
| File 4 native save/reload | Both characters retain Thief; Cecil retains 14 Dragoon ABP; locked Dragoon does not become selectable | `research/release-pass/file4-reloaded.json` |
| Cross-slot isolation | Load File 1: Original/None, empty ABP, no story flags/announcements | `research/release-pass/file1-isolation.json` |
| Return to user progress | File 3, waterway, three members; both test overrides false | `research/release-pass/file3-restored.json` |
| Existing saves | Existing Files 1–3 not overwritten; test created in previously empty File 4 | Save UI; `research/release-pass/save-files-audit.json`; pre-test backup |
| Isometric exclusion | DLL disabled locally; archive allowlist has no iso files | Installed plugin folder; package script |
| Archive integrity | Exact allowlist, byte equality, CRC and SHA-256 checks in packaging script | `scripts/package-alpha.py`; `dist/*.zip.sha256` |

The native equipment probe checks that the real hook returns the intended policy for real master items; it does not test every UI/Optimize/story-equipment flow. The core flag checks test policy boundaries; they do not execute every story script.

## Not yet certified

Full campaign through all unlocks, magic gates, scripted battles, departures/returns and Cecil's transformation; physical-controller use; late-game Jump stacking and boss balance; all equipment/inventory edge cases; native Windows and a fresh Proton installation. These are explicit alpha acceptance items, detailed in ALPHA-REVIEW.md and the shipped known-issues file.

No Nexus upload has been made. The package is a reviewable alpha candidate.

## Red Mage implementation stage

- Built against the installed FF4 PR interop, zero warnings/errors; core suite covers personal 310 ABP main/sub gates, combined MP reservation/overflow and shared charge timing.
- Native NumberSelect=2 loops the spell/target windows. ATB SetActData normally creates separate chant entries; the patch synchronizes their wait to the slower spell and suppresses the second chanting effect. Native costs, effects and queue completion are retained.
- This is source/build verification, not a live controller acceptance pass. Check two different targets, mixed healing/damage, cancel at each selection step, insufficient MP, Silence, reflection and auto-battle in playtesting.

## Ninja and equipment stage

- Core checks cover personal main/sub passive thresholds, losing the active passive, main-job weapon restrictions and hand-pair validation, including duplicate bows/arrows and two-handed conflicts.
- Native HasTwoSwordStyle is a unique routine (not a shared trivial getter). FF4 BattleUtility.IsTwoSwords already checks the actual two equipped weapons. The offensive-power provider has a native-identity branch; trained characters are routed through its existing Ninja helper.
- Equipment validation runs before public SetEquip inventory bookkeeping. The shared GetEquipItems filter covers manual selection and Optimize; reconciliation uses native unequip calls.
- Native UpdateEquipParameter sums item values across all equipment dictionary entries and merges each item's elemental resistances. No whole-character defense multiplier or persistent base-stat mutation is used.
- Live equipment preview/Optimize, inventory counts, both-hand animation and departing/returning-character acceptance remain for playtesting.

## Monk implementation stage

Core checks cover the 0 main / 70 sub ABP passive, Yang's innate access, own-level per-fist power (2*level+3), arithmetic bounds, loss of unmastered passive after switching, and all combinations of training/empty hands/normal Attack eligibility. Normal Attack expands into two native queue entries with fresh calculation state for the second fist, retaining native accuracy and defense for each. Expanded actions cannot recursively expand. Jump, Kick, Focus, counters and Rapid Fire are excluded. The unique native FightActFunctionFF4.GetAttackCount routine is overridden only for marked fists to avoid the original two cosmetic swings per calculation. Markers are held until native battle Clear or campaign reset. Build passes; live two-strike animation, target-death/counter behavior and damage comparison against claws remain playtest acceptance items.

## Stat mastery / 0.3.0 stage

Core checks cover every ABP rank, negative tradeoffs, best-per-stat inheritance, active vs Original passives, caster eligibility, personal progress, canonical Cecil identity, save round trips, legacy saves, story/character restrictions and repeated job-switch idempotence. Primary stats use derived bonuses; native saved base stats are never changed. The mod ID and save key remain unchanged under the FF4 JOBS+ display name. New advanced-job runtime acceptance remains outstanding; no game was launched and no user save was modified during this stage.
