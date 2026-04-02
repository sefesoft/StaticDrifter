# Game Design Document: Static Drift

## 1. Executive Summary
* **Title:** Static Drift
* **Genre:** Asteroids Roguelike (Action Roguelite)
* **Platform:** Mobile (iOS/Android) & PC
* **Aesthetic:** Industrial Minimalism (High-contrast, metallic, neon-wireframe)
* **Engine:** Unity 6 (URP)

## 2. Core Gameplay Loop
1. **Pilot:** Navigate a wraparound sector with inertia-driven thrust and rotation.
2. **Break:** Destroy asteroids and hostiles while avoiding collision pressure.
3. **Draft:** Choose one upgrade during wave interludes to specialize your run.
4. **Escalate:** Survive denser waves and periodic elite wave spikes.
5. **Persist:** Bank scrap and score locally for long-term progression.

## 3. Mechanics & Systems

### 3.1 The Card Synergy System
Power is derived from **Tag Families**. Having multiple cards of the same tag in your active deck triggers global passive bonuses.

**Synergy Formula:**
The effectiveness of a tag family ($T$) is calculated using tiered thresholds:
$$Effectiveness(T) = \begin{cases} 0 & \text{if } Count < 3 \\ Tier_1 & \text{if } 3 \le Count < 6 \\ Tier_2 & \text{if } 6 \le Count < 9 \\ Tier_3 & \text{if } Count \ge 9 \end{cases}$$

**Tag Families:**
* **Volt (Frequency):** Buffs attack speed and adds chain-lightning effects.
* **Kinetic (Force):** Buffs knockback, piercing, and impact explosions.
* **Thermal (Area):** Buffs AoE radius and adds Damage-over-Time (Burn).
* **Static (Utility):** Buffs movement speed and Core shield health.

### 3.2 Deck Logic
* **Limited Slots:** Players have 8 slots. Powerful "Heavy" cards occupy 2 slots.
* **The Purge:** During a run, players can destroy an active card to free a slot for a higher-tier discovery.
* **Interlude Drafting (Current MVP):** During wave breaks, pick 1 of 3 upgrade offers from Volt/Kinetic/Thermal/Static families.
* **Daily Calibration (Future):** Fixed decks and global leaderboard challenge runs.

## 4. Visual & Audio Direction
* **Visuals:** Dark grey/matte black backgrounds, brushed metal textures, and vibrant neon accents for projectiles and UI. Minimalist 3D models with high-contrast outlines.
* **Audio:** Heavy, rhythmic alternative rock and post-punk basslines. Mechanical, "crunchy" sound effects for combat.

## 5. Technical Architecture
* **Entity Management:** Object pooling for projectiles, asteroids, and enemy spawns.
* **Data:** Card tags and enemy stats are ScriptableObject driven; draft system currently uses runtime-defined offers for rapid iteration.
* **UI:** Runtime-authored HUD and flow UI with TextMeshPro + Unity UI.
* **Optimization:** Targeted for 60 FPS on mobile; avoid LINQ and high-allocation code in `Update()` loops.