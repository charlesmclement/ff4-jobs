# FF4 JOBS+ — validation history

## Current: 0.4.2 beta 3 (2026-10-31)

- Jobs input now reads logical Action/Cancel/direction keys solely through the game's InputListener. Native inspection confirms its gamepad path resolves the current KeyConfig mapping through GetInputActionType before polling the controller action.
- Removed the competing raw action reads and hard-coded Escape fallback. The footer no longer assumes A/B or fixed keyboard keys.
- Release build passed with zero warnings/errors; all 4,298 existing core checks passed. These do not simulate physical controller input.
- Controller acceptance: swap Confirm/Back in FF4 settings; open Jobs, traverse characters/slots/jobs, confirm and back out using the assigned buttons. Repeat after changing bindings again. Physical-controller retest remains pending.
- No game restart forced and no campaign/save/configuration files changed by this fix.

## Previous: 0.4.1 beta 2 (2026-10-31)

- Fixed the native equipment-list hard exit: IL2CPP materializes the source into a native list; managed code reads it by index. The old generic/non-generic enumerator crossing reproduced a hard process exit while reading Yang's equipment choices.
- Release build: zero warnings/errors; 4,298 core checks pass.
- Disposable native Yang probe under Proton: equipment-list generation/enumeration and six alternating equip/unequip cycles pass. The fixture uses native `OwnedCharacterData.SetEquip`, followed by parameter updates and list rebuilding; it does not register a live campaign inventory or exercise the UI client commit.
- At level 10 / strength 12: Fire Claw attack 25 and accuracy 82 both match the native unmodified calculation; barehand attack is 27 and accuracy 99. Repeated swaps do not accumulate power changes.
- Removed shared claw accuracy suppression and the 5% post-defense bonus. Claws use native Monk power/accuracy; fully empty hands add 2 attack and use the best native claw accuracy basis (99).
- The user's current post-Rosa/Mt. Hobs save folder is backed up separately and remains byte-for-byte unchanged after the probes. No campaign loaded, restored or saved. The older after-Rosa backup was retained.
- Evidence: private `research/yang-unequip/` logs and save hashes. Probe runs only with `FF4JOBS_EQUIPMENT_PROBE=1`, creates a detached character, and quits.
- Still pending: the user's live Yang equipment-menu reproduction, inventory bookkeeping/Optimize acceptance and battle balance. Passing this regression probe is not a full campaign or UI certification.

## Previous: 0.4.0 beta 1 (2026-10-31)

- Release build: zero warnings/errors; 1,550 core checks pass.
- Real installed-game startup under Proton: all 78 Harmony-patched methods loaded successfully; JobWindow and FastForward components registered; normal test toggles both false.
- Smoke check quits before loading any campaign. The entire current save folder matches its pre-check SHA-256 snapshot byte-for-byte.
- Rapid Fire scaling moved from an inlined helper to the freshly calculated physical result. Dualcast auto-history now preserves both spells. Monk UI wording matches the implemented claw/barehand rules.
- These checks do not certify advanced live combat, controller behavior, a full campaign, or other platforms. See [beta review](BETA-REVIEW.md).
- Local evidence: `research/beta-pass/startup-result.json`, `startup-smoke.log`, `pre-smoke-save-hashes.json`; private and excluded from releases.

## Historical validation

### FF4 Jobs 0.2.0 alpha candidate

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

Core checks cover the 0 main / 70 sub ABP passive, Yang's innate access, own-level power (2*level+3), shared trained accuracy (50+level/4), and fist/claw/empty/mixed/shield/bow hand combinations. Normal Attack expands into two native queue entries with fresh calculation state for the second strike. Expanded actions cannot recursively expand. Jump, Kick, Focus, counters and Rapid Fire are excluded. The native FightActFunctionFF4.GetAttackCount and IsRepertEffect routines suppress duplicate cosmetic swings only for marked strikes.

The native SetUpFunctions routine calls the normal-attack calculation synchronously. A scoped context identifies marked barehand actions; the FuncitonNormalAttack.Calc postfix applies 5% to the newly calculated damage before display/HP updates. It does not patch result reads, so repeated display queries do not compound the bonus. The tiny GetSumCalcResult helper is inlined at native callers and is not used for this adjustment. Fists and claws share the offensive-power provider override; Ninja's provider patch yields to this profile. The confirmed-accuracy getter temporarily excludes the equipment accuracy cache, restores it in a finalizer, and preserves native temporary accuracy and final limits. No equipped item or elemental/status data is changed.

Manual equipment/Optimize selection and commit validation allow offhand claws for trained Monks within main-job equipment access, without enabling arbitrary dual weapons. Live two-strike animation, two-claw selection/Optimize, target-death/counter behavior, blind status and elemental/status acceptance remain playtest items.

## Stat mastery / 0.3.0 stage

Core checks cover every ABP rank, negative tradeoffs, best-per-stat inheritance, active vs Original passives, caster eligibility, personal progress, canonical Cecil identity, save round trips, legacy saves, story/character restrictions and repeated job-switch idempotence. Primary stats use derived bonuses; native saved base stats are never changed. The mod ID and save key remain unchanged under the FF4 JOBS+ display name. New advanced-job runtime acceptance remains outstanding; no game was launched and no user save was modified during this stage.

### Monk / Mastery integration follow-up

The suite passes 1,174 checks with a clean plugin build (zero warnings/errors). Added a reward-to-save-to-reload scenario crossing Monk mastery from 309.75 to 310 ABP through sub-job training, including Cecil identity continuity. Original keeps the two-strike Barehand passive with the sub slot empty; another character receives neither its stats nor passive. The mastery notice remains acknowledged across reloads. The protected, persistent and live after-Rosa save sets each match all nine protected hashes; no save restore or game launch was performed. Native combat/controller acceptance remains pending.

### Claw / barehand balance follow-up

Build and 1,514 core checks pass with zero warnings/errors. Checks include every combination of fist/claw/unrelated-weapon/shield/bow eligibility, levels 1–99 for shared power/accuracy, five-percent post-defense scaling, zero/negative damage, tiny-hit rounding and integer overflow. These are implementation checks, not live combat acceptance. All nine live and persistent campaign saves still match the protected after-Rosa backup.
