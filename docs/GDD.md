# Game Design Document: Static Drift

## 1. Executive Summary
* **Title:** Static Drift
* **Genre:** Bullet Heaven Deckbuilder (Action Roguelite)
* **Platform:** Mobile (iOS/Android) & PC
* **Aesthetic:** Industrial Minimalism (High-contrast, metallic, neon-wireframe)
* **Engine:** Unity 6 (URP)

## 2. Core Gameplay Loop
1. **Survive:** Navigate the arena, auto-firing at mechanical swarms.
2. **Harvest:** Collect "Data Scrap" to trigger mid-run level-ups.
3. **Draft:** Choose cards from your pre-built pool to modify your current run.
4. **Extract:** Surpass the time limit or defeat the boss to earn permanent scrap.
5. **Build:** Use scrap to buy new "Core Cards" and refine your 8-slot deck.

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
* **Daily Calibration:** A daily challenge mode with fixed decks and global leaderboards.

## 4. Visual & Audio Direction
* **Visuals:** Dark grey/matte black backgrounds, brushed metal textures, and vibrant neon accents for projectiles and UI. Minimalist 3D models with high-contrast outlines.
* **Audio:** Heavy, rhythmic alternative rock and post-punk basslines. Mechanical, "crunchy" sound effects for combat.

## 5. Technical Architecture
* **Entity Management:** Use GPU Instancing for Swarmers and Object Pooling for all projectiles.
* **Data:** All Card, Enemy, and Wave data must be stored in `ScriptableObjects`.
* **UI:** Implemented via Unity **UI Toolkit** for responsive scaling.
* **Optimization:** Targeted for 60 FPS on mobile; avoid LINQ and high-allocation code in `Update()` loops.