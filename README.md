# Static Drift: Asteroids Roguelike

A high-performance, minimalist **space-action roguelike** built in **Unity 6**.

## 1. Project Vision
*Static Drift* pivots to a modern Asteroids-style experience: momentum-based ship control, lethal asteroid fields, and high-pressure arena combat with roguelike buildcraft. Each run blends mechanical piloting skill (thrust, drift, rotation, dodge) with strategic upgrade decisions.

## 2. Core Gameplay Loop
1. **Pilot:** Enter a wraparound sector and survive with inertia-based controls.
2. **Break & Harvest:** Destroy asteroids and hostiles to generate score, scrap, and draft triggers.
3. **Draft:** Choose run upgrades that modify weapon behavior, utility, and survivability.
4. **Escalate:** Face denser asteroid belts, enemy waves, and elite encounters.
5. **Extract or Fall:** Reach run checkpoints/boss kill for meta rewards, then reinvest in future runs.

## 3. Core Pillars
* **Pilot Mastery:** Movement is expressive; momentum management and positioning are core skill tests.
* **Roguelike Buildcraft:** Mid-run choices create unique loadouts each run.
* **Readable Chaos:** High combat intensity with clear visual language and deterministic feedback.
* **Performance First:** Mobile-oriented architecture targeting stable 60 FPS under heavy spawn load.

## 4. Systems Direction (Proposal A)
Primary direction is **Proposal A: Pure Asteroids Roguelike**.

* **Combat Space:** Top-down wraparound arena with asteroid fragmentation chains.
* **Threat Mix:** Asteroids, drones, and periodic elite mini-bosses.
* **Run Growth:** Draft selections at score/time thresholds.
* **Synergy Families (carried forward):**
  * **Volt:** fire cadence and chain interactions.
  * **Kinetic:** impact force, ricochet, and split projectiles.
  * **Thermal:** burn effects and explosive area pressure.
  * **Static:** shields, evasion windows, and efficiency tools.

## 5. Technical Stack
* **Engine:** Unity 6 with URP.
* **Data Model:** ScriptableObject-driven content for upgrades, enemies, asteroids, and waves.
* **Entity Strategy:** Object pooling for projectiles/VFX/enemies; avoid runtime instantiate/destroy churn.
* **UI:** Match HUD and run flow UI with lightweight, scalable presentation.
* **Optimization Constraints:** No heavy allocations in frame loops; mobile-first CPU and memory budgets.

## 6. Visual & Audio Direction
* **Visual Identity:** Industrial minimalist space aesthetic, matte dark backgrounds, neon telemetry, clean silhouettes.
* **Combat Readability:** Strong contrast for projectile lanes, hazard edges, and damage states.
* **Audio:** Punchy retro-futurist impact palette with rhythm-forward combat layering.

## 7. Development Roadmap (Pivot)
- [x] **Phase 1: Flight Core** - Ship thrust/rotation/drift, wraparound space rules, shooting baseline.
- [x] **Phase 2: Asteroid Ecology** - Spawn bands, fragmentation behavior, hazard pacing.
- [x] **Phase 3: Roguelike Run Layer (MVP)** - Wave interlude drafts with Volt/Kinetic/Thermal/Static upgrades that modify live run combat stats.
- [x] **Phase 4: Encounter Structure (MVP)** - Escalating wave pressure with periodic elite wave profiles and interlude pacing.
- [x] **Phase 5: Meta Progression (MVP)** - Local persistent top scores plus persistent scrap total earned across runs.

## 8. Why This Pivot Works
Asteroids gameplay and roguelike progression are a strong fit: piloting depth keeps minute-to-minute action engaging, while run-based drafting prevents repetition and creates long-term replayability. This combination supports both high skill ceilings and broad build experimentation.

## 9. Risks and Mitigations
* **Risk:** Movement complexity may feel harsh on mobile.
  * **Mitigation:** Add assist layers (turn damping, aim assist, optional auto-fire presets).
* **Risk:** Visual clutter during high-density waves.
  * **Mitigation:** Strict VFX budgets, high-contrast telegraphs, capped simultaneous hazard classes.
* **Risk:** Roguelike upgrades overpower core piloting.
  * **Mitigation:** Keep movement mastery central; upgrades amplify style, not replace fundamentals.
* **Risk:** Scope drift while pivoting.
  * **Mitigation:** Lock Proposal A for MVP; postpone sector-routing/shop systems until post-core validation.

## 10. Current Playable Slice
The current build supports a full run loop: title -> gameplay -> escalating asteroid/drone waves -> interlude draft choices -> elite wave spikes -> game over with top scores and persistent scrap. Next extension focus is boss encounters and spendable meta progression modules.