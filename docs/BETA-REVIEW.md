# FF4 JOBS+ 0.4.0 beta 1

Beta 1 is prepared for campaign playtesting with existing saves. It is not
a claim that the whole campaign or every combination has been played through.

## Completed release work

- All requested job stages are implemented: Ranger Rapid Fire, Red Mage
  Dualcast, Ninja Dual Wield, Monk fists/claws and personal stat mastery.
- Rapid Fire now scales freshly calculated damage; its previous small helper
  hook could be bypassed by native inlining. Barehand's 5% modifier shares
  the calculation boundary and cannot stack onto Rapid Fire.
- Dualcast restores both spell/target entries after FF4 overwrites its
  one-action auto-battle history. The shared charge still uses the slower spell.
- Monk's menu description matches the current fist/claw rules.
- Release build: zero warnings/errors. Core suite: 1,550 checks passed.
- Real Proton startup: all 78 patched methods and both UI/control components
  loaded successfully. No initialization errors; both testing overrides false.
- Startup exited before loading any campaign. The complete current save folder
  was byte-for-byte unchanged, including the user's after-Rosa progress.
- Package contains only our two DLLs, the verified game hash, documentation
  and a file-hash manifest. Packaging rejects stale source/build combinations.
- Added core-check CI, a bug-report template, and a prioritized playtest guide.

## Remaining beta playtesting

These checks are not represented as passed: live advanced-command selection
and combat (including targets dying between hits, counters, statuses and
auto-battle), physical controller behavior, the full story route, all equipment
edge cases, native Windows and a clean Proton-prefix installation.

Earlier live approvals for the menu, scrolling, Cecil's HP casting, story
unlocks and saving remain recorded in [VALIDATION.md](VALIDATION.md).
The new loader check proves patch installation, not combat outcomes.

Continue the original campaign with the existing shortcut and normal story
gates. No new game, save replacement, all-jobs unlock or ABP reset is required.
See [TESTING.txt](../release/TESTING.txt) and [KNOWN-ISSUES.txt](../release/KNOWN-ISSUES.txt).

No Nexus upload or new public GitHub release has been made by this pass.
