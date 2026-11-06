Despite the marketing term "AI Storyteller," RimWorld's storytellers (Cassandra Classic, Phoebe Chillax, and Randy Random) do not use machine learning, neural networks, or deep narrative logic. Under the hood, they are deterministic C# classes driven by probability curves, cooldown timers, math formulas, and weighted event pools.

The underlying code, math, and architecture governing how they operate consist of several core systems:

---

### 1. The Core Architecture (`StorytellerWorker`)

In the decompiled C# source code (`RimWorld.StorytellerWorker`), storytellers inherit from a base class that periodically executes a ticking function.

Every few in-game hours, the game engine asks the active storyteller: *"Should I fire an incident right now?"* The storyteller evaluates a queue of available incidents (raids, trading ships, animal migrations, manhunter packs) and filters them based on:

* **Category tags:** Whether an incident is classified as a *ThreatBig*, *Ally*, *Misc*, or *OrbitalTrader*.
* **Global and category-specific cooldowns:** Ensuring the game doesn't drop three major raids in a single day.
* **Population Intent:** A background math calculation tracking how many colonists you have versus soft/hard caps, which scales the probability of recruitment events vs. population-reducing threats.

### 2. Major Threat Scheduling & Math

The primary difference between Cassandra, Phoebe, and Randy comes down to math curves and probability distribution for **Major Threats** (predominantly raids):

* **Cassandra Classic:** Operates on a strict, escalating wave pattern. Her code forces a major threat on a predictable timeline (roughly every 1.5 to 3 days on standard difficulty), building up to a crescendo, followed by a mandatory cooldown to let the player recover before the next predictable spike.
* **Phoebe Chillax:** Uses the exact same mathematical logic for threat scaling as Cassandra, but her frequency multiplier is stretched far apart (roughly one major threat every 13 to 16 days). Because her intervals are long, players accumulate massive amounts of colony wealth in the interim, leading to devastatingly large raids when she finally triggers one.
* **Randy Random:** Bypasses the strict scheduling curves entirely. Instead of checking a weighted progression curve, Randy evaluates incident triggers using a flat, randomized probability roll combined with an extra multiplier.

#### The Randy RNG Multiplier

When Randy decides to fire a major threat, his code applies a random multiplier of **$0.5 \times$ to $1.5 \times$** to the final calculated threat points. More famously, because his event selection is untethered from a pacing tracker, his code allows for "clustering"—meaning he can roll zero threats for a year, or three major raids in 24 hours.

---

### 3. Threat Point Calculation (The Real "Difficulty" Math)

When a storyteller *does* decide to launch a raid or hostile event, it doesn't just pull a random enemy group. It calculates an exact budget called **Raid Points** using a formula combining several variables:

$$\text{Raid Points} = \text{Base Wealth Multiplier} \times \text{Colonist Count Scaling} \times \text{Difficulty Multiplier} \times \text{Adaptation Factor}$$

* **Wealth-Scaling:** Total colony wealth (buildings, items, stockpiles, and pawns) is the heaviest weight in the equation. This implements the game's core design philosophy: growth invites conflict.
* **Colonist Count & Point Penalties:** Each living colonist adds points, but the scaling has diminishing returns so larger colonies don't instantly break the math. Downed or kidnapped colonists temporarily lower the point calculation.
* **The Adaptation Factor (Survival/Downtime Tracking):** The game tracks how well you are doing over time. If you breeze through recent fights without injuries or property damage, the adaptation factor creeps upward, making future raids harder. If your colonists are frequently downed or dying, the adaptation score drops, temporarily giving you breathing room (often summarized by the community as the game "taking pity" on you).

---

### 4. Incident Worker Weights

Every individual event has a C# class inheriting from `IncidentWorker`. When a storyteller triggers an event category (e.g., *ThreatBig*), it queries all valid `IncidentWorkers` currently allowed by the game state (e.g., you can't get a mech cluster if tech levels or game stages don't permit it).

Each worker has a **Base Chance Weight** defined in the XML game files. The storyteller tallies up the weights of all valid workers, rolls a random number between zero and the total sum (weighted random selection), and picks the winning incident.