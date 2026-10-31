# FF4 Jobs

An experimental main-job/subjob mod (0.2.0 alpha candidate) for Final Fantasy IV Pixel Remaster on PC.

[Download the experimental alpha](https://github.com/charlesmclement/ff4-jobs/releases/tag/v0.2.0-alpha1). Requires the verified Steam build and BepInEx loader described in [INSTALL.txt](release/INSTALL.txt). Back up saves and use a separate slot. Early gameplay checks pass; a complete campaign playtest is still outstanding.

## Project requirements

The local development build adds **hold LT/L2 for 4x fast-forward**, including dialogue confirmations. Release to restore normal speed. Menus/choices stay at normal speed; release and hold again after a choice. **LT + Start** opens native scene-skip confirmation only where the game provides it. Keyboard: **F6**, or **F6 + F7** for skip confirmation. In `BepInEx/config/ff4jobs.mainandsub.cfg`, `[Controls] UseLeftTrigger = false` selects RT/R2 instead. This update is not included in the linked alpha1 download yet.

- Preserve FF4's original story, cast, character identities, party departures/returns, and Cecil's transformation.
- Mix FF4 and FF5 classes. Main job controls combat specialties and stats; subjob provides learned commands without a second set of stat bonuses.
- Unlock jobs at story milestones; keep unlocks through departures. Kain leaving at Mist unlocks Thief for both slots; Dragoon waits until the escape from Zot. Dark Knight is restricted to Cecil and Kain. A separate all-jobs test toggle does not grant story progress.
- Store ABP separately for each character and job. Returning characters keep their progress.
- Keep original story jobs and character IDs intact. Do not turn the cast into generic adventurers.
- Use the installation and development folder on `New Volume`.

## Status

Development prototype, **not a finished or campaign-tested mod**. The installed game is Steam build 20265023. Its IL2CPP metadata and master tables have been inspected locally. BepInEx 6 be.735 loads under Proton. No original game assets have been overwritten.

Native job-switching interfaces exist in this build, but FF4's job rows have empty command/stat overrides. Their presence alone does not supply an FF5 job system. This mod must supply its own rules and integrate them with combat and saving.

The alpha implementation uses existing FF4 combat actions. Advanced FF5-exclusive mechanics require separate implementation and validation. Class names do not imply those mechanics are already implemented.

## Development

Source and scripts are under `src/` and `scripts/`. Local game metadata, extracted tables, loader files and third-party tools stay ignored under `research/` and `tools/`; they are not release content. The game is at `../SteamLibrary/steamapps/common/FINAL FANTASY IV PR`.

The NTFS drive rejects Proton's `c:` link, so the temporary test prefix lives in RAM at `/dev/shm/ff4-jobs-proton`. It contains separate test saves. Code, builds and logs stay on this drive.

References: [Memoria.FFPR](https://github.com/Albeoris/Memoria.FFPR), [BepInEx](https://github.com/BepInEx/BepInEx), [Il2CppDumper](https://github.com/Perfare/Il2CppDumper). These projects provide tooling/reference, not an existing completed job-system mod.

## Controls and story unlocks

Select Jobs in the regular camp menu. The controller flow is character portraits → Main Job/Subjob → available jobs. Confirm uses the game's configured Enter action (normally A); cancel uses its Cancel action (normally B). D-pad/stick navigation repeats while held. Keyboard controls use the same configured actions, with Escape also available. The menu uses native Unity UI, the active FF4 font and native border sprite, plus an opaque blue backing. Main-party class labels show main/subjob over an opaque blue mask without changing the underlying native class text or character identity.

Testing overrides are stored in `BepInEx/config/ff4jobs.mainandsub.cfg`: `AllJobsAvailable` and `AllSkillsAvailable`. Both default false; both are off for the current story-progression playtest. They do not award ABP or story flags. Disabling them restores normal eligibility. Untaught subjob selections saved during testing contribute no unearned commands/spells without the override.

Unlocks use verified native ScenarioFlag1 event flags, synchronized only after control returns to the field/menu. Ordinary recruitment no longer grants jobs. The original classes remain usable throughout, including story characters' native spell lists.

| Milestone | Job unlock | Flag |
|---|---|---:|
| Start | Dark Knight for Cecil only (no unlock dialog) | — |
| Kain leaves at Mist (after the village incident) | Thief for both slots; Dark Knight also becomes available to Kain only | 7 |
| Defeat Octomammoth | Black Mage | 13 |
| Defeat Antlion | Bard | 16 |
| Cure Rosa in Kaipo | Ranger | 17 |
| Complete the defense of Fabul | Monk | 20 |
| Cecil becomes a Paladin / Tellah remembers Meteo | Paladin | 28 |
| Escape Baron aboard the Enterprise, after the twins' sacrifice | White Mage | 107 |
| Escape the Tower of Zot after Barbariccia | Dragoon, Red Mage, Time Mage | 43 |
| Adult Rydia returns and Golbez is defeated | Summoner | 46 |
| Cid seals the underworld passage | Engineer | 49 |
| Defeat Rubicante | Ninja | 53 |
| Complete the Giant of Babil | Sage | 68 |

Story-unlocked jobs can be selected in either slot immediately, except Original (main only) and the currently selected main job. An untrained subjob contributes no skills. The menu displays ABP numbers, a progress bar toward the next rank, and the remaining ABP for the next command. Main jobs earn full ABP; the equipped sub job earns 25%, including with Original as the main job. Fractional ABP is shown in the menu and saved per character/job. Sub jobs can be trained all the way to mastery; the same skill thresholds and story gates apply. Battle ABP is 1 + floor(boosted battle EXP / 200), with no per-battle cap.

A native FF4 dialog announces each story milestone once acknowledged and saved. Dark Knight never triggers a job announcement. Older saves receive catch-up notices for unlocked jobs. New skill/spell unlocks also use native dialogs, grouped by character and job, with Main/Sub availability indicated. Their saved journal prevents repeat announcements; existing skills are seeded silently when upgrading an old save.

A main job starts with its signature command. Additional commands unlock at 10 and 30 ABP; their subjob use unlocks at 10, 30 and 70 ABP. Progress belongs to each character. Cecil retains progress through his transformation.

Job-granted magic requires **both story progress and ABP**, plus the existing school and MP-cost tier limits. Grinding cannot bypass the story gates. Spell eligibility uses an explicit player-spell allowlist; enemy/event spell copies, including zero-MP versions, are excluded even in testing.

- Fire waits for Rydia's Mt. Hobs fire lesson (flag 18).
- Fira/Blizzara/Thundara wait for the Fabul defense and 30 ABP.
- Curaga, Esuna, Haste, Stop, Osmose, Bio, Break and Drain require Tellah's Meteo scene (flag 28) and 70 ABP.
- Curaja, Reflect, Firaga/Blizzaga/Thundaga, Tornado, Quake and Death require the completed Tower of Zot (flag 43), Tellah's scene and 150 ABP.
- Full-Life, Holy, Flare and Meteor require the completed Giant (flag 68), Tellah's scene and 310 ABP. Only Black Mage/Sage can gain Meteor through this system.
- Summoner unlocks for Main and Sub Job when adult Rydia returns and Golbez is defeated (flag 46). Story-earned summons are shared party knowledge; each character needs their own Summoner ABP to cast them. Unearned summons stay locked.


Black Magic uses the equipped character's cumulative Black Mage ABP, never Rydia's level. Main jobs earn full ABP; Sub Jobs earn 25%. Winning battles trains the job; casting a spell is not required.

| Black Mage ABP | New spells | Story requirement |
|---|---|---|
| 0 (Main) / 10 (Sub) | Blizzard, Thunder, Poison, Warp, Pig | Black Mage unlocked |
| 0 (Main) / 10 (Sub) | Fire | Rydia's Mt. Hobs fire lesson |
| 10 | Toad | Black Mage unlocked |
| 30 | Sleep | Black Mage unlocked |
| 30 | Fira, Blizzara, Thundara | Defense of Fabul complete |
| 70 | Stop, Osmose, Bio, Break, Drain | Tellah remembers Meteo |
| 150 | Firaga, Blizzaga, Thundaga, Tornado, Quake, Death | After Zot and Tellah's scene |
| 310 | Flare, Meteor | After the Giant and Tellah's scene |

Summoner ABP requirements (also require the party to have actually learned the summon): 0 Goblin/Chocobo; 10 Bomb; 30 Cockatrice/Mind Flayer/Mist Dragon; 70 Shiva/Ramuh/Ifrit/Sylph; 150 Titan/Odin; 310 Leviathan/Asura/Bahamut. Sub Job Summon starts at 10 ABP. Original mode keeps native FF4 acquisition.

The battle command list scrolls inside FF4's original five-row window. White native triangles at the top-right/bottom-right indicate commands above/below; each hides at its corresponding end.

Before becoming a Paladin, Cecil casts job-granted spells with HP: 4 HP per normal MP cost (Fire: 20 HP). Costs stop at 1 HP, even if he cannot pay the full amount. Other characters use MP. His transformation restores MP casting. Spell ABP and story requirements still apply.

These restrictions govern the mod's granted abilities. Original/native spell acquisition and scripted scenes are preserved; a full campaign and exploit audit remains outstanding. Main-job equipment policies and initial stat tradeoffs are implemented in 0.2.0. Broader campaign balance and equipment edge cases still need playtesting.

Story order cross-checks: [Gamer Corner walkthrough](https://guides.gamercorner.net/ffiv/walkthrough/) and [FF Compendium](https://ffcompendium.com/h/ff4story.shtml). Exact flag IDs were verified locally in this installed game's event scripts, not inferred from a walkthrough.

The playtest launcher defaults to borderless fullscreen. Set `FF4JOBS_WINDOWED=1` for the old 1280×720 debugging window. Older pre-Zot Dragoon selections revert to Original (main) or None (sub), retaining earned ABP and any other valid slot. Kain retains native Jump in Original mode.

## Alpha candidate release pass

See [review choices](docs/ALPHA-REVIEW.md), [validation evidence](docs/VALIDATION.md), and [installation/rollback instructions](release/INSTALL.txt). Build with `scripts/build.sh`; create the allowlisted archive with `python3 scripts/package-alpha.py`. Nothing is automatically published.

Main jobs now define weapon/armor access. Subjobs provide learned actions without equipment or stat bonuses. Incompatible worn gear is returned using FF4's own unequip handling. Original preserves native equipment access. Dragoon/Paladin have -3 Agility; Engineer has -4. The job picker shows equipment and stat adjustments.

Trained Thief actions use a 35% equal-level starting detection chance, +5 percentage points per Thief rank and +2 per level advantage (or -2 per level disadvantage), clamped to 15–75%. Item selection and native loot availability remain authoritative. Original Edge without a Thief assignment retains native odds.

## Building from this repository

Install a .NET SDK capable of building the net8.0 tests and net6.0 plugin, and the .NET 6 targeting pack if required by your SDK. Install the verified game and BepInEx loader, then launch once to generate the game's interop assemblies. These game and loader assemblies are local build references and are never included in this repository.

Set `FF4JOBS_GAME_PATH` to the FF4 Pixel Remaster installation if it is not at the default sibling SteamLibrary path. Run `bash scripts/build.sh`; set `FF4JOBS_DOTNET` if `dotnet` is not on PATH. Core checks can be run independently with `dotnet run --project src/FF4Jobs.Tests/FF4Jobs.Tests.csproj`. The scripts use `verified-game.json` for the approved game build.

The separate `src/FF4Iso.Plugin` source is a parked courtyard experiment. It is not part of the jobs build or release archive. Local research and validation screenshots referenced by the audit notes are intentionally excluded from GitHub.

### Red Mage — Dualcast

At 310 personal Red Mage ABP, main or sub job gains Dualcast. Choose two learned white/black spells and targets in the native spell window. Both charge together for the slower spell's normal wait, then execute in selection order without charging again. Each spell pays its normal MP cost (Cecil's pre-Paladin HP casting still applies). Second-spell selection reserves the first spell's MP. Dualcast does not unlock higher-tier magic or bypass story gates. Native status, targeting, reflection and counter rules still apply.
