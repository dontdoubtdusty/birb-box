# Birb Cube Vision

Status tags: [Built] [In progress] [Planned] [Undecided]. This doc describes the game, not the builds: when something is built, change its tag and keep the description current. It is the one home for design; build sketches, questions and checks are in `next-session-plan.md`, status is on the board. Version history is in the session log and `claude/archive/`.

## The concept
The player sits in a small cabin on a spaceship, at a table with Birb Cube: an in-world holographic toy showing a small patch of a living bird world, like a sci-fi tabletop terrarium. It's less a game than a vibey simulation.

The player has hands and interacts directly. Anything that changes the hologram is done through physical buttons, dials and switches. Information outside the hologram lives somewhere in the room (a journal, a log, readout windows).

## The player's hands
- [Built] Two hands, built from simple shapes for now, each ending in a closed brass cuff at the wrist, no arms. They stay out of view until needed, then fade in just short of the control and ease in: a finger presses buttons (the logo, the rocker, the radio's buttons), thumb and index pinch and turn knobs, a hand rests on the compass ring and slides with it while you turn the box (right or left hand, by which way you start dragging). They linger a moment, then fade out where they are.
- [Built] Entering the Hologram View, both hands push the cube apart; leaving it, they press it back together.
- [Built] Hand color: a slider of alien blue-green shades, or Groening yellow, in the Esc menu's Settings page, where the right hand is held up to show it.
- [Planned] Low-poly hand models from Blender.
- [Undecided] Where the color setting lives in the room later (a dial?).

## The hologram volume
- [Built] A glowing-edged volume with no solid walls. Ground, seed, food, birds.
- [Built] The volume is a cube that floats just above a hollow pedestal. It turns with its contents like a turntable; the birds and the store share one turn, so the ring never jumps on a scene switch.
- [Built] The pedestal is a dark varnished wood cabinet. Only the compass ring on top (N, W, S, E and the build name) is polished brass, the shiniest thing on it; small brass fittings (buttons, logo plaque, window frames) are set into the wood.
- [Built] A lens behind the pedestal's logo throws four thin beams up to the cube's floor corners. The logo is the power button: it presses into the wood with a click, then the projector whirs up. The box starts off, and switching it on raises the cube out of the light.
- [Built] One BIRB / SHOP rocker switch in the wood band under the ring, above the logo: black housing, brass bezel, amber cap, the lit side's word glowing in brass lettering. The hand presses it and it clicks over. It stays there (the FEATHERS window becomes the day dial instead).
- [Planned] The day dial: the FEATHERS readout window (it shows the feather total today) becomes a drum dial, a painted band that scrolls sideways behind the glass past a fixed center pointer, like the date wheel of an old odometer. The band runs night blue, dawn rose, day gold, dusk amber, with the sun and moon riding on it, so the time of day reads from position and picture, not color alone. With the BIRBS window on the left, the logo in the middle and the dial on the right, the front of the pedestal stays balanced. Feather counts already show in the HUD, so the window can change at any time. Built as a scrolling texture behind the existing window glass plus a pointer.
- [Planned] A smaller pedestal under the same-size cube, after the day dial (both rework the same band).
- [Built] The ground fills the cube out to its square sides and corners. A thin fog right at the walls fades things out, so birds walking out of view melt away instead of popping.
- [Built] In the room view, the sun and moon ride under the cube's ceiling: one wheel turning once a day along the garden's east-west line, the sun crossing by day and the moon (showing its phase) by night, each fading at the glass like a bird walking into the fog.
- [Built] The Hologram View, the observer mode: double-click the box while it shows the birds, the hands push the cube apart, and the camera dives through its wall to stand low inside the garden, looking across it. The dive starts slow and rushes at the end, the view widening a little on the way. Inside, you see the same patch of garden as in the room view: the whole cube is clear, and the garden carries on past the walls for about another half-cube, fading into mist, so no wall or corner shows as an edge. The cube's glowing edges fade out and the room fades into a sky with a misty treeline, whose horizon color the mist takes on. The side columns slide away and peek back when the mouse nears a screen edge. The birds and rain come forward, the hum and the radio step back. Drag up/down to tilt from near the birds' eye level to looking down, sideways to turn the garden around you; the wheel zooms the bird world. The room's controls don't respond while you're inside. Double-click again to pull back out to the room view as it was. Only for the birds, not the store.
- [Undecided] An idle drift for the observer mode: after a while without input, the camera slowly drifts or settles on a bird by itself, like a screensaver.
- [Built] The store (Gilded Granary) stays round inside the cube. At night its frosted bulb switches on and the shop is warm and lamp-lit.
- [Built] Labels in the hologram (chat lines, name tags) stay readable over grass, dirt and birds: a faint dark plate, a thin outline, the bird's color lightened, and a little bigger when the camera is far back.
- [Built] Hologram quality (High, Medium, Low) in the Esc menu's Settings: how smooth the hologram's edges are, traded against speed.
- Decided: no corner brackets or ticks on the cube.
- Decided: no clock, panel or widget on the cube's walls. Anything pinned to a face makes it read as solid and the birds as boxed in, and since the cube turns, "the far wall" keeps changing. Readouts belong on the pedestal or in the room.
- Style rule: subtle. No heavy scanlines, flicker or strong tint unless I ask.

## The garden
The world inside should feel like a zen garden crossed with a real (slightly exaggerated) bird feeder area: a small, lived-in patch you peep into and watch birds and critters come and go. Not big and empty.
- [Built] Grass and bare dirt patches, fixed in the world. Grass wears bare where birds feed and grows back.
- [Built] A few rocks (two big, a few small), new ones for each new world. Birds walk around them and sometimes hop up and perch. Seed rolls off them. The grass grows lusher in a ring around each rock, and soft moss grows on their tops.
- [Built] Feeders are real-world sized next to the birds, a tad larger (3x their store model). The tube feeder has two perches on one rod.
- [Built] Where a heap of seed has rotted away, a grassy sprout sometimes comes up: it puts up seedheads of the seed it came from, birds peck at them, it sheds a little seed, then browns and dies back (about 15 minutes).
- [Built] A small pool, one per world: a lobed pond about four units across, placed with the rocks and saved. Drawn as part of the floor: a dark glassy surface, lighter in the shallows and deep teal in the middle, damp ground and lusher grass along the shore, a soft sun or moon glint, a few slow sparkles, and a faint hologram-colored waterline. Ripple rings spread where something touches the water.
- [Built] Birds use the pool: they sip at the shore, or wade in and bathe (wings fluttering, splashes), then walk out and shake off. Up to 3 bathe at once; everyone else walks around the water. Seed that lands in it sinks with a plip; feathers wash up on the shore; feeders and sprouts never go in it. A thirsty bird heads for the nearest water from anywhere, flying over if it's far, and sips until quenched or bathes. Passing birds still stop for a sip or a bath now and then.
- [Built] Rain puddles: in rain, up to six form on dirt worn bare by birds and seed, fill as the ground soaks, and dry after. Slate-gray and glassy. Birds sip from them (no bathing); a thirsty bird uses one if it's nearer than the pool.
- [Planned] The stream, the uneven ground and the water touches: see World Beautification below.
- [Planned] Trees that grow over time: saplings that grow over several in-game days into small trees about half the cube's height, never up to the ceiling, so they don't wall in the view. Birds perch in them, roost in them at night and shelter under them in storms. A few per world, placed clear of the pool, stream, rocks and feeders; saved, growth included. Comes after the uneven ground, so trees are placed on the final ground.
- [Planned] Critters that come and go alongside the birds.
- [Undecided] More zen touches: raked gravel, moss on the ground, logs. (The water touches are under World Beautification.)

## World Beautification
Decided Nov 5: the garden stops being a flat floor. The ground comes first, then the pool and the stream are carved into it, so nothing is built twice.
- [Built 5.23, untested] Gentle uneven ground: low swells and dips across the world, the highest a few bird-heights tall, never hills. Low enough that the fog still hides where the ground meets the cube's walls and no bird hides behind a rise. Made fresh for each new world and saved. The cabin, the store and the Designer stay flat.
- [Planned] Slopes are lit by the time of day like everything else, so dawn and dusk light catches the swells.
- [Planned] Everything on the ground sits on it: birds walk, land and hop at the ground's height; seed, feathers, rocks, feeders and sprouts rest on the slope; rain splashes, the Hawk's Shadow and labels follow it; the Hologram View's eye stands on the ground.
- [Planned] Seed rolls a little way downhill before it settles. Rain puddles gather in the hollows instead of on worn dirt.
- [Planned] The pool sits in a real dip: a flat water surface with banks sloping down into it, shallow at the shore and deeper in the middle.
- [Planned] A stream, one per world: it rises from a small spring inside the garden and runs downhill along a carved channel into the pool. Its surface visibly flows downstream, with glints and a damp, lusher bank. Shallow at the edges, so birds sip and bathe along it; narrow enough in places that birds hop across, wider stretches they fly over. Seed that lands in it drifts with the current and sinks. A soft trickle sound when near. Shown on the minimap.
- [Planned] Reeds or rushes along the banks of the pool and the stream, and lily pads on the pool.
- [Planned] A few stepping stones across the stream or into the shallows, where birds stand to drink.
- Decided: old saved worlds are not converted. An older save loads flat and offers a New world to get the new ground (its birds, regulars and feathers stay behind with it).
- [Undecided] Whether rain raises the stream; whether birds perch on reeds; a waterfall or rapids where the stream steps down (not picked for now).

## Feeding and feathers
Decided Oct 25. Feeding was the weakest part of the game: seed came out too fast, most of it rotted into gunk that did nothing but need cleaning up, natural drops alone outpaced what a full garden eats, and coins made seed nearly free. The loop was a treadmill with no payoff. Kept as they are: the feeders, the bucket and how it looks when stirred, and the store.
- [Built] Pouring: hold right mouse, slow, with small piles. Pouring in one spot grows one mound, newer seed on top of older, a few grains bouncing around its foot; falling seed comes down as a stream and each grain hops to rest. Dragging leaves a trail.
- [Built] Seed is scarcer: natural drops cut to about a third.
- [Built] No gunk: uneaten seed darkens, slumps and fades into the dirt, or becomes a sprout.
- [Built] Rain spoils ground seed faster (seed in feeders and on seedheads stays fresh), so feeders matter more in wet weather.
- Decided: feeders are the main way to feed (seed in them stays fresh); seed on the ground is the small extra.
- [Built] Feathers replace coins entirely, one kind per species: Ember Plume (Emberwing), Dusk Down (Dusk Dove), Gild Quill (Gildfinch), Rime Feather (Frostcrest, rare), and "<Name> Feather" for each Designer species. A bird that finishes a full meal sometimes drops one of its own kind (about 1 meal in 4.5; regulars and named birds half again as often, Open-handed birds more often still). It pops out, floats down swaying, lies flat and fades after about a minute (half as long out of view). Clicking one picks it up and says whose it was.
- [Built] Feathers are only for spending: never kept or shown as objects in the room. A collection grid in the right column HUD shows a colored dot and a count per species, each appearing after its first feather; the pedestal's FEATHERS window shows the total. Counts are saved.
- [Built] The store has one shelf per species, shown once you hold that species' first feather (four to a page, arrows to turn), selling the seeds that species eats, priced in that species' own feathers. No chain between species. The shopkeeper's ledger book on the counter opens a panel that trades 3 feathers of one kind for 1 of another you already hold. The crate by the door sells a rare species' lure seed for feathers of any kind until you hold that species' feather. The feeders are paid in a matching species' feather. Prices are first guesses, tuned by play.
- Decided: a species roughly pays for its own food, and growth takes some care (the target numbers are in the plan's feeding numbers).
- [Undecided] Mulch (what rotted seed could become instead of disappearing).
- [Undecided] Comfort items on each species' shelf (a dust bath for doves, a thistle sock for finches, a sunning stone for Emberwings) that change how long birds stay and how often they come back. A bird bath, placed like a feeder, is the likely first one.
- [Undecided] More weather tie-ins: rain helping sprouts, wind carrying seed sideways, snow burying ground seed so feeders become the only food, more bathers after rain stops.

## Birds as individuals
The player should be able to get attached to birds. Most birds you see should come back, not show up once and vanish.
- [Built] Each bird has a home patch where it last finished a meal. It roams near home and turns back before the world's edge.
- [Built] Birds live 1 to 3 hours of sim time, then leave for good of old age.
- [Built] Now and then a bird loses interest and flies off, much sooner when there's nothing it likes to eat.
- [Built] Regulars: a bird that leaves (not of old age) may come back later with the same name, look and home. About half of arrivals are a returning regular when one of that kind is away. The bird card counts visits.
- [Built] Naming means something: renamed birds can show a floating name tag (Esc menu: birds you named, all birds, or off). Regulars "rejoin the chat".
- [Built] Birds call every 30-90 seconds, and hold their call if a neighbor just called, so the garden never sounds crowded.
- [Built] Birds feel alive when standing about: they blink, squash and stretch as they hop and land, and now and then preen, scratch, fluff up or stretch a wing; wandering birds sometimes sit still for a spell.
- [Built] Three needs: hunger, thirst and rest. Each runs down; below its line the bird goes to see to it, the most pressing first: food, water, or a nap (a short sleep in the roost pose, usually once in the afternoon). A night's roost (or a Night Owl's day) fills rest too. A need sitting at zero long enough sends the bird off ("grew parched", "was worn out"), but water is always reachable and a nap can be taken anywhere, so that's rare. The bird card shows Food, Water and Rest bars and what the bird is doing.
- [Built] Personality, RimWorld style: each bird gets 2 or 3 traits when it first arrives, on scales where it holds at most one degree each: Nerve (Faint-hearted, Wary, Stout-hearted), Appetite (Dainty, Big Eater), Roaming (Hearthbound, Wayfarer), Voice (Hushed, Town Crier), Hours (Early Riser, Night Owl), and six scales with a single, rarer trait: Weather (Rain-proud), Bathing (Dabbler), Perching (Watchman), Taste (Particular), Loyalty (Oath-keeper, never together with Wayfarer) and Giving (Open-handed). Most of a bird is average; only what its traits name stands out. Traits never change, are saved, and come back with a regular. They nudge existing numbers: space kept from other birds and rock sharing, how often and how long it sits on rocks, how much it eats and how soon it's hungry or thirsty, whether it holds out for a pile with its favorite seed, how far it roams and how soon it loses interest, how likely it is to be the regular that returns, how often it calls, when it sleeps and tires, whether rain bothers it, how much it bathes, how often it drops feathers. Each species leans its birds a certain way. Each bird also has a favorite seed. Shown on the bird card as a slider per scale with the trait word and a flavor line on hover, plus "Fond of" its favorite seed.
- [Planned] Birds sometimes dance when the radio is on. Not yet decided: how often, which birds (a trait?), what the dance looks like, whether it follows the beat. (See the DJ dance party.)
- [Planned] More traits that need new behavior or later systems: Flock-kin and Loner, Quarrelsome (shooing others off feeders), freezing or fleeing in the Hawk's Shadow. Draft list in the plan's Step 12.
- [Undecided] Mood (made from needs, weather and crowding).
- [Undecided] Later: bonds (pals that travel together, rivals that chase each other off feeders), pairs that nest, chicks that follow a parent and inherit looks and traits. Memory of feeding spots, rivals and the player's attention. A chronicle: a journal in the cabin recording notable moments in the house voice (firsts, rare visitors, last visits, pairs, hatchings), doubling as a life list of species seen and collecting the birds' records.

## Species and the Birb Designer
- [Built] Every Birb Designer save becomes one more look for its species; new birds of that species pick a look at random. "Release into the Cube" saves the design and sends one bird wearing it into the middle of the view. Designer saves made in the Release copy land in that copy's `resources/birbs`.
- [Built] Designer-made species: a design can be a species fully its own, with its own name, map color, size, common or rare, seed tastes, trait leans, feeders and voice, set in the Designer's Species section and saved as a file. A new species copies its details from one you pick. The four built-in species live in code; Designer species come from their files; each has a stable key separate from its shown name. A common Designer species gets its own share of arrivals, drawn by the seeds it likes. A rare species has a lure seed: until one is seen in the view, only that seed draws it (the Frostcrest and Blue Barley work the same way). A Designer species borrows a built-in species' calls at its own pitch, unless you add its own call files. Delete a species' file and its birds leave for good. The pedestal's LEDs grow a second row past 8 species. Its feather is named "<Name> Feather" automatically.
- [Planned] Migration: some species are seasonal and only come in certain seasons (each species says which), the rest stay all year. At a season's turn a big passing flock of a migrating species stops over, strips the seed and moves on within a few minutes. The stopover is paced by the Storyteller, like the Murmuration.
- [Planned] A species randomizer, once the pieces above are in: it makes a whole new species at random, with a house-style name, colors, size, tastes, leans, voice and a body put together from the part library, saved as a Designer species file you can then edit.
- [Undecided] A custom feather name per Designer species, instead of "<Name> Feather".

## Time, weather and events
- Cozy rule: nothing punishes the player for being away, but paying attention gets you more. Events never need a response.
- [Built] Day and night: dawn chorus, quiet dusk, birds roost at night (most fly off and are back first thing in the morning, a few sleep on a rock or the ground). Night Owls keep the opposite hours: they settle down or fly off half an hour after sunrise and are back half an hour before sunset, so they share dusk and dawn with the day birds; Dusk Doves are the night crowd. A full day is 15 minutes of sim time, with seasons of 5 days changing how long the days are. Days happen only inside the cube; the cabin's light and the starfield window stay as they are. The time of day shows in the right column HUD for now. Time doesn't pass while the game is closed.
- [Built] How the day looks: the light follows the sun. Dawn and dusk go through a rose glow into warm gold that reaches the ground, with cool blue-purple shade on the sides of birds and rocks turned away from the sun, and the fog takes the sky's own color instead of the hologram's teal. The blue hour is a deep glowing blue, never darker than night. Night is a moonlit stage: silver-blue, colors a little quieter, but bright enough to watch and click birds, their hologram outlines glowing a little stronger. Night reads as night by its color, not by darkness. Under rain clouds the light stays flat and gray. No shadow system for now.
- [Planned] The time of day moves onto the pedestal's day dial. The HUD clock stays until the dial reads well on its own. The season could get a small glyph on the dial later (sprig, sun, leaf, snowflake).
- [Built] Rain: random dry and rain spells (dry spells shorter in spring, longer in summer), some only drizzle. Clouds gather and the light dims and grays, the rain builds, eases off and the sky clears; the wet ground darkens and dries over a few hours. Thin streaks fall through the cube with tiny splashes, rings dot the pool and puddles, and a soft rain sound follows. Birds hunch, call less and leave sooner; fewer arrive, and many that do are Rain-proud, who carry on as usual. Nobody bathes in the rain. The weather word shows in the HUD under the time. Saved.
- [Planned] Thunderstorms: a heavier kind of rain spell, darker clouds and harder rain, with thunder rolling in from far off and closer. Lightning is a soft glow swelling for a moment inside the clouds and the fog, lighting the garden a little, never a hard flash; no bolts in the cube. In the Hologram View, faint bolts sometimes show far off over the treeline. A Settings switch softens or turns off the glow. Birds take cover (under trees once there are trees), call even less, and almost nobody arrives but the Rain-proud.
- [Planned] Rainbows, only when real conditions meet: the sun is up and lower than 42 degrees, and rain is still falling on the side of the sky opposite it, which mostly happens as a storm or rain spell clears late or early in the day. The bow is an arc around the point opposite the sun, red on the outside, now and then with a fainter second bow outside it, colors reversed. It fades as the rain moves off or the sun climbs. Shown in the Hologram View's sky only, since from the room view you look down into a tabletop cube and there's no sky for it.
- [Planned] Wind (seed drifts, birds fly slower against it) and snow (ground goes white, tracks show). Mostly tint and particles, fitting the fog-and-glow style.
- [Undecided] Seasons beyond the light and the rain: what birds eat, how fast grass grows. (Which species come is migration, under Species.)
- [Built] Storyteller: one pacer, built the way RimWorld's storytellers are (plain rules, no AI), with the cozy rule flipped where theirs punishes. Events fire from the Tab panel too, show a log line and a label in view, and have no sounds yet.
  - Events come in three categories, each with its own cooldown: Big (Hawk's Shadow, Murmuration, DJ party, migration stopover), Gift (Windfall, Gilded Hour, Rare visitor) and Small (Squabble, Lost fledgling). A big event never follows a big one closely, and small ones fill the gaps.
  - Each event says when it's allowed (no Hawk at night, Murmuration only with seed on the ground, Lost fledgling only while a regular of its kind is away) and how likely it is right now. The pacer rolls among the allowed ones by weight.
  - Weighted by time of day, weather and how busy the garden is: a quiet garden leans toward visitors and gifts, a crowded one toward the Hawk or a Murmuration.
  - A steady rhythm: a quiet stretch, a small beat, quiet, a bigger beat, then a guaranteed rest. A little randomness in the gaps, never clusters.
  - Never harsher as the garden grows. An event's size follows what's there (a Murmuration's flock with the seed on the ground, the Gilded Hour's feathers with the birds present), never the player's progress.
  - No pity or difficulty tracking, since there is nothing to fail. Attention steers instead: while you watch, events lean toward the patch in view; while you're away or the box is off, they still happen and are logged.
- [Built] Events cast real birds: their traits decide what they do (the Watchman calls the alarm first, the Faint-hearted scatter, a Stout-hearted bird holds its feeder). Each bird keeps a short record of what it lived through, shown on its bird card ("Froze under the Hawk's Shadow"), saved and brought back with a regular.
- [Planned] Events that pick particular birds, preferring named birds and regulars (a Squabble picks two regulars).
- [Built] The Hawk's Shadow (birds freeze or scatter into the fog and creep back; the only tension, nobody gets hurt), Murmuration (a big flock sweeps through and strips the seed), Windfall (a gust drops a few piles of seed the birds here like). [Undecided] Berries in a Windfall.
- [Planned] More events: Rare visitor (a species that only comes under certain conditions), Squabble (two birds fight over a feeder, the loser sulks on a rock), Lost fledgling (a young bird alone, collected later by a regular of its kind), Gilded Hour (rare golden light, extra feathers).
- [Planned] The DJ dance party: a rare storyteller event where DJ Songbird and DJ Blue-J turn up and the garden breaks into a dance for about a song, then settles back. Toned down to stay inside the style rule: a soft drift of color through the light and fog and birds bobbing together, no strobing, flashing or hard pulses. Still to settle: whether it needs the radio on (and follows its beat) or brings its own tune, who the two DJs are (special one-off visitors, a species of their own, or ordinary birds given the part), what they look like, how rare it is, which birds join, what the dance looks like, and how it differs from the everyday radio dance.

## World and simulation
- [Built] The world is larger than the hologram volume, which is a window onto one patch of it: 56x56, only a little bigger than the widest view (about 40x40), so you can zoom all the way out and still move a bit.
- [Built] World edge: fog only. Panning stops when the view reaches the edge, so you never see past it. In the Hologram View a flat apron of ground carries on past the edge, fading into the mist and sky, so the world reads as a patch of something bigger.
- [Built] Birds keep simulating outside the window. They wander into the fog and new ones arrive from it; nothing spawns or despawns in view.
- [Built] Birds keep living while the box is switched off.
- [Built] Only what's inside the window is drawn (in the Hologram View, the window plus its misty margin).
- [Built] The window has a movable center. Drawing, fog and click-to-throw all work relative to it, never (0,0,0).
- [Built] The terrarium is saved: one autosaved world that's waiting when you come back, including the regulars who are away, the feathers you hold and the weather. "New world" in the Esc menu starts over. The world never catches up on time passed while the game was closed: you pick up at the same time of day you left.
- [Built] Settings are saved in their own file, so New world keeps them: volumes, radio volume, fullscreen, name tags, hand color, hologram quality, and the tools panel's numbers.

## Camera and controls
- [Built] Camera sits in front of the pedestal. Side-to-side drag turns the box contents; up/down drag tilts. Zooming in past the framed view leans you in toward the glass.
- [Built] Double-click the box to step into the garden (the Hologram View), and again to come back out.
- [Built] WASD pans the window across the world. Minimap: click or drag to jump the window.
- [Built] Pour seed into the hologram, click birds, feeders or feathers to select or pick them up. Double-click a bird (or its name in the tracker) to follow it.
- [Built] Time keys: P or Space pauses, 1 / 2 / 3 set 1x / 2x / 4x, - and + step the speed.
- [Built] The seed bucket (left panel) is drawn as a see-through hologram bucket that fills with seed. Meant as an in-world object later.
- [Planned] Leave the table and walk around the cabin.

## The cabin
- [Built] A table with a mug, a child's drawing, a tablet, books and a candle, plus the radio beside the pedestal.
- [Built] The radio plays your own music folders and live web stations; a station finder (clicking the radio's dial) searches for stations and puts them on the dial. It's an ImGui stand-in for now; its in-world home later is the tablet.
- [Built] A big window behind the table looking out at a starfield (your own picture), drifting very slowly and shifting a little as you move, with a faint twinkle and a painted planet in front.

## Flavor
The company behind Birb Cube is from a future society that built its brand on its loose idea of Ye Olden medieval times. Birds, seeds, shops and messages all get that coloring.
Examples: Emberwing, Dusk Dove, Gildfinch, Frostcrest, Verdewing, Gilded Granary, Ember Plume, Rime Feather, "shall henceforth be known as...", trait names like Faint-hearted, Hearthbound, Town Crier, Rain-proud, Dabbler, Watchman, Oath-keeper and Open-handed.

**Why the company does it: a cure for hearthsickness.** Long-haul spacers get homesick for a planet most of them never lived on. The company's research found the cure wasn't realism but rootedness: hearths, seasons, handmade things, small places where the same faces come back. The era their culture remembers as most rooted is a half-remembered Middle Ages, so the whole product line is built on it.
- This explains the hardware too: brass buttons, dials and a wood cabinet are what you'd build for a ship far from any supply chain (tactile, repairable, they fail gracefully).
- It explains the cozy rules: birds come back, nothing punishes you, time keeps its own clock. That's the cure, built into the product.
- Their history is a little off (a Tudor word beside a Viking one). An occasional gentle joke is fine; it's never the point. No snark.
- The lore leaks through objects and names. No text dumps.

**Readability rule:** flavor goes in nouns, headers and pictures. Instructions, numbers and grammar stay plain. "Gilded Granary" as a header, "Sunflower seed: 2 Ember Plume" on the shelf. No thee/thou in anything the player acts on. Blackletter type only for single capitals or titles, never body text. Hologram chat lines are the one place a whole sentence gets flavored, since the player never has to act on them.
- Avoid: parchment textures behind panels, "Ye" in front of things, puns in item names.
- No flavor work on ImGui panels (they're temporary). Settings and the Esc menu stay plain on purpose: they belong to the player, not the company.

Flavor ideas (the order they'll be done in is the plan's Medieval flair pass):
- [Planned] The weather words (Fair, Clouding over, Drizzle, Rain, Clearing, and a storm word) in the house style, and a name for the pool and one for the stream (nothing on screen names the pool yet). First flair pass.
- [Undecided] Shop wording: a house-style unit for a jar and a sack, a title for the ledger panel.
- [Undecided] Species heraldry: a simple device per species (a flame for Emberwing, a crescent for Dusk Dove, a wheat sheaf for Gildfinch, a snowflake for Frostcrest), used on map dots, pedestal LEDs, feather counts and store shelves. Shape plus color tells species apart, which helps colorblind players. Designer species would pick one.
- [Undecided] Canonical hours on the day dial: Matins, Lauds, Prime, Terce, Sext, None, Vespers, Compline, as small marks along the band's edge, matched to the day-and-night beats (Lauds = dawn chorus, Compline = roosting). Never in place of the sun and moon. The dial itself is the company's nod to medieval astronomical clocks.
- [Undecided] The company's name. Ideas: Hearthward & Wren, Wrenhallow Provisioners, The Worshipful Company of Aviary Wrights.

## Open decisions
- Controls when walking the cabin: WASD currently pans the window and would clash with walking.
- Order of replacing ImGui panels with in-world controls.
- Which critters, and how they interact with birds, feeders, the pool and puddles; whether they have needs like birds. No critter events in the storyteller until this is decided.
- Which weather comes after thunderstorms (wind or snow), and whether a forecast ever shows in the room (a barometer was offered and not picked).
