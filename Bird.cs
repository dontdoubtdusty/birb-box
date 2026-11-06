using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Raylib_cs;

public class Bird : Entity
{
    // IDENTITY
    public string Name { get; private set; }
    public bool IsNamed { get; private set; }      // the player renamed it
    public int Visits { get; private set; } = 1;   // 2 and up: a regular that came back
    public Species Species { get; private set; }       // its kind (see Birds.cs)
    public BirdLook Look { get; private set; }         // its shape and base size: built in, or a Birb Designer save
    public BirbShape Shape => Look.Shape;
    public BoundingBox Bounds { get; private set; }    // around the shape, before Size is applied

    // PERSONALITY (see Traits.cs): rolled on its first arrival, kept for life, saved.
    public IReadOnlyList<Trait> Personality => personality;
    private List<Trait> personality = new();
    public TraitEffects Nature { get; private set; } = new();    // the numbers its traits change
    public SeedType FavoriteSeed { get; private set; }           // a mild pull toward piles that have it
    private const float FavoritePull = 1.5f;                     // its favorite seed counts as this many times closer

    // LOOKS: rolled once when it's first made, saved, kept by regulars, so no two birds of a kind are quite alike.
    public float Build { get; private set; } = 1f;         // times its look's size
    public float Shade { get; private set; } = 1f;         // how bright its feathers are (1 as designed, lower is darker)
    public float Warmth { get; private set; }              // -1 cooler (a little less red) to 1 warmer (a little less blue)
    public Color Plumage { get; private set; } = Color.White;    // Shade and Warmth as a tint on every part (see WorldDrawer)
    private const float BuildSpread = 0.1f;                // size up to this share bigger or smaller, before its traits' lean
    private const float ShadeMin = 0.84f;
    private const float WarmthStrength = 0.12f;            // how much of the other end's color a fully warm or cool bird loses

    // BODY
    public float Size { get; private set; }        // birb units to world units (its look's size times Build)
    public float Speed { get; private set; }       // walking speed, units per second
    public float Heading { get; set; }             // radians, 0 = facing +Z
    public float HopPhase { get; set; }
    public float GroundY { get; private set; }     // Y that puts the feet on the dirt

    // LIFE & HUNGER
    public float Age { get; private set; }         // seconds of simulation time
    public float Lifespan { get; private set; }
    public float Hunger { get; private set; } = 1f;     // 1 = full, 0 = starving (it gets hungry below Nature.HungryAt)
    private const float StarveLimit = 25f;         // seconds at zero hunger before leaving
    private float starveTimer = 0f;
    private const float OldSaveLifespan = 3600f;   // saves from before 4.54 had shorter lifespans; those birds get a new one
    // Lifespan, losing interest, feather chance and call delay are tunable: see SimTuning.
    private const float RegularFeatherBonus = 1.5f;    // regulars and birds you've named drop feathers this many times as often

    // NEEDS: thirst and rest, alongside hunger (5.10). Each is 1 when met and runs down. Below its line the bird goes to
    // see to it (water, or a nap); with more than one, the need furthest below its line goes first (SeeToNeeds).
    // At zero for NeedLimit seconds it leaves, like hunger. How fast they run down: SimTuning.ThirstRate, RestRate.
    public float Thirst { get; private set; } = 1f;     // 1 = quenched, 0 = parched
    public float Rest { get; private set; } = 1f;       // 1 = rested, 0 = worn out
    public bool Napping { get; private set; }           // a short sleep while the others are up (see StartNap)
    public bool Asleep => Roosting || Napping;
    private const float ThirstyAt = 0.35f;              // goes for water below this
    private const float TiredAt = 0.3f;                 // takes a nap below this
    private const float RestedAt = 0.95f;               // a nap ends here
    private const float NapRest = 1f / 60f;             // rest gained per second napping (empty to full in a minute)
    private const float RoostRest = 1f / 90f;           // and per second roosting
    private const float UrgentNeed = 0.1f;              // hunger or thirst this low wakes a napper, and hunger this low ends a trip to the water
    private const float NeedLimit = 40f;                // seconds at zero thirst or rest before leaving
    private float thirstTimer = 0f;
    private float tiredTimer = 0f;

    // HOME: the patch a bird keeps coming back to (see BirdMover.HomeForce). Set when it arrives, moved after each meal.
    public Vector3 Home { get; private set; }
    public bool HasHome { get; private set; }
    private const float HomeEdgeMargin = 8f;       // a home is never closer than this to the world's edge

    // LOSING INTEREST: now and then a wandering bird just leaves, sooner when there's nothing it likes to eat.
    private const float InterestCheckInterval = 15f;
    private const float ScarceFoodFactor = 4f;         // how much sooner when there's no seed it can eat
    private float interestTimer;
    private bool returnsLater = true;                  // false once it leaves for good (old age)

    // NIGHT (see WorldClock): at bedtime a bird either stays the night (Roosting) or flies off, and is back first
    // thing in the morning. Night Owls keep the other hours: their bedtime is the middle of the day (5.10; they used to
    // never sleep). Tunable: SimTuning.RoostChance, MaxRoosting.
    public bool Roosting { get; private set; }
    private bool nightDecided;                         // has made its choice for tonight
    private bool leftForNight;                         // flew off at bedtime (BirdSpawner brings these back first at dawn)
    private readonly float bedJitter = Rng.Range(-0.3f, 0.3f);    // hours: not every bird keeps quite the same hours
    private const float RoostHunger = 0.05f;           // times as fast it gets hungry while roosting
    private const float DawnChorusCalls = 0.3f;        // times the call delay during the dawn chorus (more calls)
    private const float DawnChorusIgnoreHush = 0.4f;   // and this much likelier to call over a neighbor
    private const float EveningCalls = 2.5f;           // times the call delay toward dusk (fewer calls)
    private const float NightOwlNightCalls = 1.5f;     // a Night Owl at night calls a little less than by day

    // RAIN (see Weather.cs): in steady rain a bird that isn't Rain-proud calls less and tires of the garden sooner.
    // These are at full rain; drizzle does a share of it.
    private const float RainCalls = 2.5f;              // times the call delay
    private const float RainLeave = 2.5f;              // times as likely to lose interest
    public bool MindsRain => !Nature.RainProud && Weather.Instance.IsRaining;

    // STATE
    public BirdState State { get; set; } = BirdState.Wandering;
    public BirdPose Pose { get; set; } = BirdPose.Idle;    // set by the mover, drawn by BirbAnimator
    public Gait Gait => Shape.Gait;
    public Food? TargetFood { get; set; }
    private Vector3? perchSpot;                     // where it stands while eating at a feeder
    public Vector3 FlightTarget { get; set; }
    public bool ArrivingByAir { get; set; }
    public bool IsFlying => Position.Y > GroundY + 0.05f;
    public BirdMover Mover { get; private set; }

    // TIMERS
    private float eatTimer = 0f;
    private const float BiteInterval = 1.5f;
    private float needTimer;                        // a bird sees to its needs (food, water, a nap) only when this runs out
    private const float NeedInterval = 0.5f;

    // VOICE
    public float VoicePitch { get; private set; }  // each bird sounds a little higher or lower than the rest
    private float callTimer;                        // seconds until it calls out on its own
    private const float HushRetryMin = 4f, HushRetryMax = 12f;     // wait after holding a call for a nearby one
    private bool? wasFlying;                        // null until the first update, so a bird spawned in the air doesn't flap

    private bool removed = false;

    // STORYTELLER EVENTS (see Storyteller.cs)
    public bool FlockMember { get; private set; }      // passing through with a murmuration: stays hungry, no feathers, never a regular
    public bool Quiet { get; set; }                     // gets no "entered / left the chat" label (flock birds, birds scattered by the hawk)
    private float frozenTimer;                          // > 0: keeping dead still (the Hawk's Shadow)
    public bool Frozen => frozenTimer > 0f;
    // RECORDS: what it lived through ("Fled the Hawk's Shadow, day 12"), newest last; saved and carried with regulars
    private const int MaxRecords = 8;
    private readonly List<string> records = new();
    public IReadOnlyList<string> Records => records;

    public Bird(Species type, string name)
    {
        Species = type;
        Name = name;
        SetPersonality(Traits.Roll(type));
        FavoriteSeed = Seeds.RollFavorite(type);
        RollLooks();
        Speed = Rng.Range(0.45f, 1f);
        Lifespan = NewLifespan();
        Hunger = Rng.Range(0.5f, 1f);
        Thirst = Rng.Range(0.5f, 1f);
        Rest = Rng.Range(0.6f, 1f);
        Heading = Rng.Range(0f, MathF.PI * 2f);
        HopPhase = Rng.Range(0f, 10f);
        needTimer = Rng.Range(0f, NeedInterval);    // staggered so birds don't all search on one frame
        VoicePitch = Rng.Range(0.92f, 1.08f);
        callTimer = NextCallDelay();
        interestTimer = Rng.Range(0f, InterestCheckInterval);
        Mover = new BirdMover(this);

        UseLook(Species.Looks[Rng.Next(Species.Looks.Count)]);
    }

    // Takes on a look and works out its size and how high it stands.
    // The origin is not at the feet, so the bottom of the box tells how high to lift the bird.
    [MemberNotNull(nameof(Look))]
    public void UseLook(BirdLook look)
    {
        Look = look;
        Bounds = Shape.GetBounds();
        Size = look.Size * Build;
        GroundY = -Bounds.Min.Y * Size;
    }

    // After a design is saved again in the Designer: a bird wearing it switches to the new version.
    // A bird standing on the ground stays on the ground, even if the new version stands taller or shorter.
    public void RefreshLook()
    {
        if (Look.File == null) return;
        BirdLook? fresh = Species.Looks.Find(look => Birds.SameFile(look.File, Look.File));
        if (fresh == null || fresh == Look) return;

        bool onGround = !IsFlying;
        UseLook(fresh);
        if (onGround) Position = new Vector3(Position.X, GroundY, Position.Z);
    }

    // The look a save names: its Designer file if that still belongs to this species, otherwise a built-in look by number.
    // A design that was deleted or moved to another species gives a random look of this species instead.
    private BirdLook SavedLook(string? file, int index)
    {
        if (file != null)
        {
            return Species.Looks.Find(look => Birds.SameFile(look.File, file)) ?? Species.Looks[Rng.Next(Species.Looks.Count)];
        }
        List<BirdLook> builtIn = Species.BuiltInLooks;
        if (builtIn.Count == 0) return Species.Looks[Rng.Next(Species.Looks.Count)];    // a Designer species has only designs
        return builtIn[Math.Clamp(index, 0, builtIn.Count - 1)];
    }

    // A size, shade and warmth of its own. Its traits can lean the size (Trencherbirds bigger, Dainty birds smaller).
    private void RollLooks()
    {
        float build = Rng.Range(1f - BuildSpread, 1f + BuildSpread) * Nature.BuildLean;
        SetLooks(build, Rng.Range(ShadeMin, 1f), Rng.Range(-1f, 1f));
    }

    // Sets the build and the feather tint. Call UseLook afterward, since its size and how high it stands depend on Build.
    private void SetLooks(float build, float shade, float warmth)
    {
        Build = build;
        Shade = Math.Clamp(shade, 0f, 1f);
        Warmth = Math.Clamp(warmth, -1f, 1f);

        float red = Shade * (1f - WarmthStrength * MathF.Max(0f, -Warmth));
        float blue = Shade * (1f - WarmthStrength * MathF.Max(0f, Warmth));
        Plumage = new Color((int)(red * 255f), (int)(Shade * 255f), (int)(blue * 255f), 255);
    }

    // Takes on a set of traits and works out the numbers they change.
    private void SetPersonality(IEnumerable<Trait> traits)
    {
        personality = Traits.Clean(traits);
        Nature = Traits.EffectsOf(personality);
    }

    // Dev only (tools panel): a fresh personality and favorite seed, as if it had just arrived for the first time.
    public void RerollPersonality()
    {
        SetPersonality(Traits.Roll(Species));
        FavoriteSeed = Seeds.RollFavorite(Species);
        callTimer = NextCallDelay();
    }

    // ---------------- SAVING (see SaveGame.cs) ----------------

    public BirdSave ToSave() => new()
    {
        Type = Species.Key,
        Name = Name,
        ShapeIndex = Math.Max(0, Species.BuiltInLooks.IndexOf(Look)),
        ShapeFile = Look.File,
        Position = Position,
        Speed = Speed,
        Heading = Heading,
        HopPhase = HopPhase,
        Age = Age,
        Lifespan = Lifespan,
        Hunger = Hunger,
        State = State,
        FlightTarget = FlightTarget,
        ArrivingByAir = ArrivingByAir,
        VoicePitch = VoicePitch,
        IsNamed = IsNamed,
        Visits = Visits,
        HasHome = HasHome,
        Home = Home,
        Traits = new(personality),
        FavoriteSeed = FavoriteSeed,
        Build = Build,
        Shade = Shade,
        Warmth = Warmth,
        Roosting = Roosting,
        NightAway = leftForNight,
        Thirst = Thirst,
        Rest = Rest,
        Napping = Napping,
        Records = records.Count > 0 ? new(records) : null,
        Flock = FlockMember,
    };

    // A bird that was heading for or eating some seed goes back to wandering; it finds its food again in a moment.
    // One that was up on a feeder flutters down first, like after any meal there.
    // A save from before the long lives gets a new lifespan in the new range.
    // A save from before personalities (4.85) keeps the traits and favorite seed rolled when it was made here,
    // and one from before looks (4.88) keeps the size and tint rolled here.
    // The save's species must be there and have a look (SaveGame drops birds whose species is gone).
    public static Bird FromSave(BirdSave save)
    {
        Species species = Birds.Find(save.Type) ?? throw new InvalidDataException($"no species {save.Type}");
        Bird bird = new Bird(species, save.Name);
        if (save.Build > 0f) bird.SetLooks(save.Build, save.Shade, save.Warmth);
        bird.UseLook(bird.SavedLook(save.ShapeFile, save.ShapeIndex));
        bird.Position = save.Position;
        bird.Speed = save.Speed;
        bird.Heading = save.Heading;
        bird.HopPhase = save.HopPhase;
        bird.Age = save.Age;
        bird.Lifespan = save.Lifespan >= OldSaveLifespan ? save.Lifespan : NewLifespan();
        bird.Hunger = save.Hunger;
        if (save.Thirst is float thirst) bird.Thirst = thirst;    // saves from before 5.10 keep the level rolled above
        if (save.Rest is float rest) bird.Rest = rest;
        bird.State = save.State is BirdState.SeekingFood or BirdState.Eating ? BirdState.Wandering : save.State;
        bird.FlightTarget = save.FlightTarget;
        bird.ArrivingByAir = save.ArrivingByAir;
        bird.VoicePitch = save.VoicePitch;
        bird.IsNamed = save.IsNamed;
        bird.Visits = Math.Max(1, save.Visits);
        bird.HasHome = save.HasHome;
        bird.Home = save.Home;
        if (save.Traits != null) bird.SetPersonality(save.Traits);
        if (save.FavoriteSeed is SeedType favorite && species.Eats(favorite)) bird.FavoriteSeed = favorite;
        bird.leftForNight = save.NightAway;
        if (save.Records != null) bird.records.AddRange(save.Records.TakeLast(MaxRecords));
        if (save.Flock) bird.JoinFlock();    // a flock saved mid-visit; Storyteller.AfterLoad sends it off
        if (save.Roosting && bird.State == BirdState.Wandering)
        {
            bird.Roosting = true;
            bird.nightDecided = true;
            bird.Mover.ResumeRoost();    // back on the rock it was sleeping on, if it was on one
        }
        else if (save.Napping && bird.State == BirdState.Wandering)
        {
            bird.Napping = true;
            bird.Mover.ResumeRoost();
        }
        bird.callTimer = bird.NextCallDelay();
        return bird;
    }

    public override void Update(float dt)
    {
        Age += dt;
        TickDownNeeds(dt);

        if (State != BirdState.Leaving)
        {
            if (Age >= Lifespan)
            {
                StartLeaving($"{Name} has grown old and flew off for good.", returns: false);
            }
            else if (starveTimer >= StarveLimit)
            {
                StartLeaving($"{Name} got too hungry and left to find food elsewhere.");
            }
            else if (thirstTimer >= NeedLimit)
            {
                StartLeaving($"{Name} grew parched and left to find water elsewhere.");
            }
            else if (tiredTimer >= NeedLimit)
            {
                StartLeaving($"{Name} was worn out and flew off to find a quiet roost.");
            }
            else
            {
                CheckInterest(dt);
                UpdateNight();
            }
        }

        // Frozen under the hawk's shadow: it keeps dead still where it is, head up, then carries on with what it was doing
        if (frozenTimer > 0f && State is not (BirdState.Arriving or BirdState.Leaving))
        {
            frozenTimer -= dt;
            Pose = BirdPose.LookAround;
            return;
        }

        switch (State)
        {
            case BirdState.Arriving:
                Mover.UpdateArriving(dt);
                break;

            case BirdState.Wandering:
                if (Napping) UpdateNap();
                // Not while up on a feeder or a rock (it gets down first, see BirdMover), asleep, or on its way to the water
                if (!IsFlying && !Asleep && !Mover.OnWaterTrip)
                {
                    needTimer -= dt;
                    if (needTimer <= 0f)
                    {
                        needTimer = NeedInterval;
                        SeeToNeeds();
                    }
                }
                Mover.UpdateWalking(dt);
                break;

            case BirdState.SeekingFood:
                UpdateSeeking(dt);
                break;

            case BirdState.Eating:
                UpdateEating(dt);
                break;

            case BirdState.Leaving:
                Mover.UpdateLeaving(dt);
                break;
        }

        UpdateVoice(dt);

        // A bird that walks past the world edge has wandered off to somewhere else (rare: BirdMover turns them back).
        if (State != BirdState.Arriving && !World.Instance.IsInside(Position, 3f))
        {
            if (State != BirdState.Leaving)
            {
                EventLog.Add($"{Name} wandered off past the edge of the world.");
            }
            Remove();
        }
    }

    // Removes the bird from the world once, even if two things ask in the same frame.
    // Unless it left for good, it joins the regulars who may come back later (see BirdSpawner).
    public void Remove()
    {
        if (removed) return;
        removed = true;
        World.Instance.RemoveEntity(this);
        if (returnsLater) BirdSpawner.Instance.AddRegular(this);
    }

    // Every so often a wandering bird may lose interest and leave. Much likelier when nothing it eats is lying about.
    private void CheckInterest(float dt)
    {
        if (State != BirdState.Wandering || IsFlying || Asleep || FlockMember) return;    // a flock leaves together (Murmuration)

        interestTimer -= dt;
        if (interestTimer > 0f) return;
        interestTimer = InterestCheckInterval;

        bool scarce = !World.Instance.GetAllEntities<Food>().Any(food => !food.IsFullyEaten && CanEat(food));
        float chance = InterestCheckInterval / (SimTuning.Now.LoseInterestMinutes * 60f) * (scarce ? ScarceFoodFactor : 1f) * Nature.LoseInterest;
        bool rain = MindsRain;
        if (rain) chance *= float.Lerp(1f, RainLeave, Weather.Instance.Rain);
        if (!Rng.Chance(chance)) return;

        if (rain) StartLeaving($"{Name} went looking for shelter from the rain.");
        else StartLeaving(scarce ? $"{Name} found the pickings slim and flew off." : $"{Name} lost interest and flew off.");
    }

    // Moves its home patch to here, kept well inside the world.
    public void SetHome(Vector3 spot)
    {
        Home = World.Instance.PullInside(new Vector3(spot.X, 0f, spot.Z), HomeEdgeMargin);
        HasHome = true;
    }

    // A regular coming back for another visit (see BirdSpawner.TakeRegular).
    // countVisit: false for a bird coming back after the hawk scared it off, which is still the same visit.
    public void StartNewVisit(bool countVisit = true)
    {
        if (countVisit) Visits++;
        returnsLater = true;
        starveTimer = 0f;
        thirstTimer = 0f;
        tiredTimer = 0f;
        Rest = leftForNight ? 1f : MathF.Max(Rest, Rng.Range(0.7f, 1f));    // slept while it was away
        Thirst = MathF.Max(Thirst, Rng.Range(0.5f, 1f));                      // and drank somewhere
        leftForNight = false;
        Roosting = false;
        Napping = false;
        nightDecided = false;
    }

    // ---------------- NIGHT ----------------

    // At bedtime (its own hours: WorldClock.IsBedtime, moved by an Early Riser's shift and a little jitter),
    // a wandering bird either stays the night or flies off to roost out of view. Most fly off; a few stay
    // (Hearthbound ones more often, never a hungry one, and only up to MaxRoosting). At its waking hour a
    // roosting bird gets up and calls. A Night Owl does the same through the middle of the day (WorldClock.IsOwlBedtime).
    // A bird napping when bedtime comes either sleeps on where it is or wakes and flies off.
    private void UpdateNight()
    {
        bool owl = Nature.NightOwl;
        WorldClock clock = WorldClock.Instance;
        bool bedtime = owl ? clock.IsOwlBedtime(bedJitter) : clock.IsBedtime(Nature.HourShift + bedJitter);
        if (Roosting)
        {
            if (!bedtime) WakeUp();
            return;
        }
        if (!bedtime)
        {
            nightDecided = false;
            return;
        }
        if (nightDecided || State != BirdState.Wandering || (IsFlying && !Mover.IsPerched)) return;
        nightDecided = true;

        bool stays = !IsHungry()
            && Rng.Chance(SimTuning.Now.RoostChance * Nature.RoostChance)
            && RoostingCount() < SimTuning.Now.MaxRoosting;
        if (stays)
        {
            if (!Napping) Mover.StartRoost();    // a napper sleeps on where it is
            Napping = false;
            Roosting = true;
            EventLog.Add(owl ? $"{Name} has settled in to sleep the day away." : $"{Name} has settled in for the night.");
        }
        else
        {
            leftForNight = true;
            StartLeaving(owl ? $"{Name} flew off to sleep the day away." : $"{Name} flew off to roost for the night.");
        }
    }

    private void WakeUp()
    {
        Roosting = false;
        Mover.EndRoost();
        callTimer = Rng.Range(0.5f, 6f);    // a call soon after waking: the dawn chorus
    }

    private static int RoostingCount() => World.Instance.GetAllEntities<Bird>().Count(bird => bird.Roosting);

    // Takes away its Night Owl trait, if it has one: a new bird that comes by day isn't one (see BirdSpawner.SpawnArrival).
    public void BecomeDayBird()
    {
        if (!Nature.NightOwl) return;
        SetPersonality(Traits.AsDayBird(personality, Species));
        callTimer = NextCallDelay();
    }

    // Makes it a Night Owl: every bird that arrives at night is one (see BirdSpawner.SpawnArrival).
    public void BecomeNightOwl()
    {
        SetPersonality(Traits.AsNightOwl(personality));
        callTimer = NextCallDelay();
    }

    // Makes it Rain-proud: some birds that arrive in the rain are (see BirdSpawner.SpawnArrival).
    public void BecomeRainProud()
    {
        SetPersonality(Traits.AsRainProud(personality));
        callTimer = NextCallDelay();
    }

    // Calls out now and then, and flaps when it leaves or touches the ground.
    // If another bird called close by a moment ago, it holds its call and tries again a little later
    // (a Town Crier sometimes calls anyway).
    private void UpdateVoice(float dt)
    {
        if (!Asleep) callTimer -= dt;    // a sleeping bird is quiet
        if (callTimer <= 0f)
        {
            float ignoreHush = Nature.IgnoreHush + (WorldClock.Instance.IsDawnChorus ? DawnChorusIgnoreHush : 0f);
            if (Sounds.CalledNearby(Position) && !Rng.Chance(ignoreHush)) callTimer = Rng.Range(HushRetryMin, HushRetryMax);
            else Call();
        }

        bool flying = IsFlying;
        if (wasFlying != null && flying != wasFlying) Sounds.PlayFlap(Position, takeoff: flying);
        wasFlying = flying;
    }

    // Also used when the player clicks the bird.
    public void Call()
    {
        Sounds.PlayCall(this);
        callTimer = NextCallDelay();
    }

    private float NextCallDelay() => Rng.Range(SimTuning.Now.CallDelayMin, SimTuning.Now.CallDelayMax) * Nature.CallDelay * CallsByTimeOfDay() * CallsByWeather();

    // Out in the rain most birds keep quiet; a Rain-proud one sings as usual.
    private float CallsByWeather() => Nature.RainProud ? 1f : float.Lerp(1f, RainCalls, Weather.Instance.Rain);

    // The dawn chorus fills the garden with calls; toward dusk it goes quiet. A Night Owl keeps calling at night.
    private float CallsByTimeOfDay()
    {
        WorldClock clock = WorldClock.Instance;
        if (Nature.NightOwl && clock.Phase == DayPhase.Night) return NightOwlNightCalls;
        if (clock.IsDawnChorus) return DawnChorusCalls;
        if (clock.IsQuietEvening) return EveningCalls;
        return 1f;
    }

    // Seconds this bird will live, from the tuned range in minutes.
    private static float NewLifespan() => Rng.Range(SimTuning.Now.LifespanMinMinutes, SimTuning.Now.LifespanMaxMinutes) * 60f;

    // Blank names are ignored, so the bird keeps its old one.
    public void Rename(string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0 || newName == Name) return;

        EventLog.Add($"{Name} shall henceforth be known as {newName}.");
        Name = newName;
        IsNamed = true;
    }

    // ---------------- NEEDS ----------------

    // Hunger and thirst run down (slowly while asleep). Rest runs down while awake and comes back while asleep.
    // Each one's timer counts the seconds it has sat at zero (see Update: the bird leaves at the limit).
    private void TickDownNeeds(float dt)
    {
        // The slider values are "seconds per 5%".
        float sleepy = Asleep ? RoostHunger : 1f;
        Hunger = MathF.Max(0f, Hunger - 0.05f / SimTuning.Now.HungerRate * Nature.HungerSpeed * sleepy * dt);
        Thirst = MathF.Max(0f, Thirst - 0.05f / SimTuning.Now.ThirstRate * Nature.ThirstSpeed * sleepy * dt);
        if (Napping) Rest = MathF.Min(1f, Rest + NapRest * dt);
        else if (Roosting) Rest = MathF.Min(1f, Rest + RoostRest * dt);
        else Rest = MathF.Max(0f, Rest - 0.05f / SimTuning.Now.RestRate * Nature.RestSpeed * dt);

        starveTimer = Hunger <= 0f ? starveTimer + dt : 0f;
        thirstTimer = Thirst <= 0f ? thirstTimer + dt : 0f;
        tiredTimer = Rest <= 0f ? tiredTimer + dt : 0f;
    }

    public bool IsHungry() => Hunger < Nature.HungryAt;
    public bool IsThirsty() => Thirst < ThirstyAt;
    public bool IsTired() => Rest < TiredAt;

    // A sip or a bath (see BirdMover's WATER section).
    public void Drink(float amount) => Thirst = MathF.Min(1f, Thirst + amount);

    // Hunger or thirst so low it can't wait (a trip to the water gives way to food at this point, and a napper wakes).
    public bool IsStarving() => Hunger < UrgentNeed;

    // Sees to whichever need is furthest below its line: food, water or a nap. If that one can't be met right now
    // (no seed it eats in reach, no water it can reach), it tries the next.
    private void SeeToNeeds()
    {
        List<(float urgency, Func<bool> act)> needs = new();
        if (IsHungry()) needs.Add((Hunger / Nature.HungryAt, TryFindFood));
        if (IsThirsty()) needs.Add((Thirst / ThirstyAt, Mover.StartThirstVisit));
        if (IsTired()) needs.Add((Rest / TiredAt, StartNap));
        foreach ((float _, Func<bool> act) in needs.OrderBy(need => need.urgency))
        {
            if (act()) return;
        }
    }

    // A nap: it settles down where it is, or on a rock close by (the same spots as a night's roost), in the Roost pose.
    private bool StartNap()
    {
        Napping = true;
        Mover.StartRoost();
        return true;
    }

    // It wakes once rested, or when hunger or thirst gets too pressing to sleep through.
    private void UpdateNap()
    {
        if (Rest < RestedAt && Hunger >= UrgentNeed && Thirst >= UrgentNeed) return;
        Napping = false;
        Mover.EndRoost();
    }

    // Birds drawn in by seed arrive ready to eat it.
    public void ArriveHungry()
    {
        Hunger = MathF.Max(0.05f, Nature.HungryAt - Rng.Range(0.05f, 0.15f));
    }

    // A bird only eats seed it has a taste for (see the seed table in Seeds.cs).
    public bool CanEat(Food food) => food.FavoriteFor(Species) != null;

    // True when it found some and set off for it.
    private bool TryFindFood()
    {
        Food? nearest = Mover.FindNearestFood(10f * Nature.FoodRange, FavoriteSeed, FavoritePull);
        if (nearest == null) return false;
        TargetFood = nearest;
        State = BirdState.SeekingFood;
        return true;
    }

    private void UpdateSeeking(float dt)
    {
        if (TargetFood == null || !CanEat(TargetFood))
        {
            TargetFood = null;
            State = BirdState.Wandering;
            return;
        }

        // Close enough to a feeder to flutter up onto it, or to a pile to start pecking (just outside a big heap)
        Feeder? feeder = TargetFood.Feeder;
        float reach = feeder != null ? feeder.Info.PerchRadius + 0.5f : MathF.Max(0.6f, TargetFood.HeapRadius + 0.3f);
        if (Mover.IsCloseTo(TargetFood.Position, reach))
        {
            perchSpot = feeder?.TakePerch(this);
            if (feeder != null && perchSpot == null)
            {
                ForgetFood();    // every perch got taken on the way; it looks again in a moment
                return;
            }
            State = BirdState.Eating;
            eatTimer = 0f;
            return;
        }

        Mover.UpdateWalking(dt);
    }

    private void UpdateEating(float dt)
    {
        if (TargetFood == null || !CanEat(TargetFood))
        {
            TargetFood = null;
            State = BirdState.Wandering;
            return;
        }

        // At a feeder, it flies up to its perch before it starts eating
        if (perchSpot != null && !Mover.FlyToward(perchSpot.Value, dt)) return;

        Mover.PeckAt(TargetFood.Position, dt, perched: perchSpot != null);

        eatTimer += dt;
        if (eatTimer >= BiteInterval * Nature.BiteTime)
        {
            eatTimer = 0f;
            if (TargetFood.TakeBite(Species, FavoriteSeed))
            {
                Hunger = MathF.Min(1f, Hunger + Food.NutritionPerBite * Nature.Nourish);
            }

            if (Hunger >= Nature.FullAt)
            {
                TargetFood = null;
                State = BirdState.Wandering;
                SetHome(Position);
                if (FlockMember)
                {
                    ArriveHungry();    // a passing flock strips the seed: it never stays full, and leaves no feathers
                    return;
                }
                if (Rng.Chance(SimTuning.Now.FeatherDropChance * Nature.FeatherDrop * (IsNamed || Visits > 1 ? RegularFeatherBonus : 1f)))
                {
                    World.Instance.AddEntity(new Feather(Position + new Vector3(0, 0.3f, 0), Species));
                }
            }
        }
    }

    // Stops going for its food, e.g. when the feeder it was eating from gets picked up.
    public void ForgetFood()
    {
        TargetFood = null;
        if (State == BirdState.SeekingFood || State == BirdState.Eating) State = BirdState.Wandering;
    }

    // returns: false when it's gone for good (old age), so it never comes back as a regular.
    // reason: the log line, or null to leave quietly (an event writes its own line). A flock bird never comes back.
    public void StartLeaving(string? reason, bool returns = true)
    {
        if (State == BirdState.Leaving) return;

        returnsLater = returns && !FlockMember;
        State = BirdState.Leaving;
        Roosting = false;
        Napping = false;
        frozenTimer = 0f;
        TargetFood = null;
        Mover.PickLeavingTarget();
        if (reason != null) EventLog.Add(reason);
    }

    // ---------------- STORYTELLER EVENTS ----------------

    // Keeps dead still for this long (the Hawk's Shadow). Needs and age still run.
    public void Freeze(float seconds) => frozenTimer = MathF.Max(frozenTimer, seconds);

    // Something gave it a fright: a napping bird wakes up.
    public void Startle()
    {
        if (!Napping) return;
        Napping = false;
        Mover.EndRoost();
    }

    // Becomes one of a passing flock (Murmuration): unnamed, unlabeled, wide awake and not thirsty, never a regular.
    public void JoinFlock()
    {
        FlockMember = true;
        Quiet = true;
        returnsLater = false;
        Thirst = 1f;
        Rest = 1f;
    }

    // Adds a line to what it lived through, with the day: "Fled the Hawk's Shadow, day 12". Flock birds keep none.
    public void AddRecord(string what)
    {
        if (FlockMember) return;
        records.Add($"{what}, day {WorldClock.Instance.DayNumber}");
        if (records.Count > MaxRecords) records.RemoveAt(0);
    }

    // How far the tip of its beak sticks out in front of its middle (for sitting the right distance from a feeding port).
    public float BeakReach => Bounds.Max.Z * Size;

    // Roughly how far the bird reaches out from its middle when seen from above (for the selection ring).
    public float FootprintRadius => MathF.Max(Bounds.Max.X - Bounds.Min.X, Bounds.Max.Z - Bounds.Min.Z) * 0.5f * Size;

    public string FormatAge()
    {
        return $"{FormatTime(Age)} of {FormatTime(Lifespan)}";
    }

    // "1h 05m" past an hour, "12m 30s" below it.
    private static string FormatTime(float seconds)
    {
        if (seconds >= 3600f) return $"{(int)(seconds / 3600)}h {(int)(seconds % 3600 / 60):00}m";
        return $"{(int)(seconds / 60)}m {(int)(seconds % 60):00}s";
    }
}
