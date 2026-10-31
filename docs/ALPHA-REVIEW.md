# FF4 JOBS+ 0.3.0 playtest — review notes

This is an experimental public alpha for playtesting. It is not a campaign-certified release.

## Choices made

- Keep the approved story schedule. Thief follows Mist; transferable Dragoon waits until the escape from Zot. Native Kain keeps Jump. Dark Knight remains Cecil/Kain only and never announces an unlock.
- Keep ABP thresholds at 10/30/70/150/310. Main jobs earn full ABP; sub jobs earn 25%, with saved fractional carry and no reduced mastery cap. Rewards are 1 + floor(boosted battle EXP / 200), with no per-battle cap; living eligible result members receive them. Original does not earn ABP.
- Main signature command at 0 ABP, later commands at 10/30. Subjob commands at 10/30/70; Rapid Fire and Dualcast use their later special thresholds. An untrained subjob gives no commands, magic, equipment access or extra MP.
- Steal detection success: clamp(35 + 5 × Thief rank + 2 × level difference, 15, 75) percent. Equal-level beginner 35%; mastered 60%. Each level of disadvantage subtracts two points. Applies to an active Thief main or trained Thief subjob. Original Edge without a Thief assignment retains native odds. Native item tables, selection and failure consequences remain in place; a passed detection roll cannot invent missing loot.
- Main job determines equipment; subjob grants learned actions and trained passives. Original retains native equipment rules. Switching returns incompatible equipment through native unequip handling. No items are added or deleted by the policy.
- Preserve Cecil's exclusive holy-sword category, native restrictions on Gold Hairpin/Minerva equipment, native slot/two-hand rules, magnetic effects and inventory counts. Ninja learns Dual Wield at main 70 / sub 150 personal ABP, using the native equipment and attack systems.
- Dragoon: STR +6, VIT +3, AGI -3. Paladin: STR +3, VIT +6, AGI -3, SPR +6. Engineer: STR +4, VIT +8, AGI -4. Other existing main-job adjustments retained. These starting modifiers grow by +1 per ABP rank on positive stats; mastered jobs add bounded inheritance. Primary stats remain capped at 1–99.
- Keep original character identities, native level-up growth and scripted story behavior. Transferable Dragoon remains late-game, with no party-wide Jump limit.
- Red Mage Dualcast is implemented at 310 ABP in either slot, with two native spell/target selections and one shared charge. Monk Barehand now gives two independently calculated normal Attack strikes, with per-fist power from the acting character's level and both hands empty; Yang has innate access. Personal stat growth and mastery inheritance are implemented; see the README for exact rules. The latest local playtest shares only story-earned summons, gated by each character's own Summoner ABP. Later-story live acceptance remains pending.
- Park the isometric prototype. It is disabled locally and excluded from the jobs archive. Keyboard remapping and DXVK/VSync configuration are also excluded.

## Verification scope

Core checks cover story flags, command thresholds, magic gates, character restrictions, ABP saturation/rewards, save round trips, equipment policy, canonical Cecil progress and test-override behavior. Rydia test fixtures now use her actual status ID 4; Rosa is 3.

Native checks and UI evidence are recorded in `docs/VALIDATION.md`. A simulated flag check is not a playthrough of the corresponding story scene. Do not describe this build as fully campaign-tested.

## Remaining campaign and platform acceptance

- Human controller check: A/B, stick/D-pad repeat, held buttons during menu entry, every back-navigation level.
- Full story route through Fabul, Ordeals/Tellah, Baron/twins, Zot, adult Rydia, Cid, Rubicante and the Giant. Check each announcement, ability tier and returning character's saved progress.
- Multi-Dragoon encounter balance, boss targeting, queued Jump and scripted battles.
- Equipment edge cases: Optimize, two-handed weapons/bows/arrows, full inventory, character departures, forced story equipment, magnetic cave and uninstall after cross-character gear use.
- Native Windows installation and a clean Proton-prefix installation. Current live validation uses the existing Linux/Proton test installation.
- Public screenshots and final listing review. No Nexus upload has been performed.
