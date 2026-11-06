using System.Numerics;
using Raylib_cs;

// Chat-style labels that float inside the hologram:
//  - "<name> has entered the chat" above a bird that arrived; it follows the bird and drifts up.
//  - "<name> has left the chat" where a bird took off to leave for good; it stays put, sinks, and is dimmer.
//  - "<name> has rejoined the chat" instead, for a regular coming back.
//  - name tags: just the name, floating over a bird for as long as it's in view (which birds: NameLabels setting).
//  - sleeping z's: little z's drifting up from every roosting bird, in a pale hologram tint, so sleepers read in the dark.
//  - event labels ("A hawk's shadow passes over"): a Storyteller event happening in view, in plain text, where it centers.
// Quiet birds (a passing flock, birds the hawk scattered) get no entered / left labels; flock birds get no name tags.
// Labels fade out, and dim near the hologram's edge like everything else in the fog.
// Each label's text is drawn once into its own small texture, then shown on a flat rectangle
// that always faces the camera. To stay readable over grass, dirt and birds, every label sits on a
// faint dark plate, its letters have a thin dark outline, the bird's color is lightened, and labels
// grow a little as the camera pulls back.
public enum NameLabelMode { Off, Named, All }

public class ChatMessages
{
    // NAME TAGS (setting in the Esc menu; not saved, like the other settings)
    public NameLabelMode NameLabels = NameLabelMode.Named;
    private const float NameLabelWidth = 1.8f;      // box units, same text scale as the chat labels
    private const float NameLift = 0.3f;            // above the top of the bird's head, in box units
    private const float NameAlpha = 0.85f;
    private const int TexScale = 2;                // label textures are drawn this many times bigger than the base layout, so close-ups stay sharp
    private const int NameTextureWidth = 256 * TexScale;

    private class NameTag
    {
        public string Name = "";
        public RenderTexture2D Texture;
    }
    private readonly Dictionary<int, NameTag> nameTags = new();     // by bird EntityID
    private readonly List<Bird> labeledBirds = new();                // shown this frame
    private readonly HashSet<int> taggableBirds = new();             // allowed a tag right now, in view or not

    private const float Lifetime = 5f;
    private const float FadeIn = 0.3f;
    private const float FadeOut = 1.5f;
    private const float StartHeight = 1.0f;       // above the bird's feet
    private const float RiseSpeed = 0.15f;        // units per second
    private const float LabelWidth = 3.6f;        // in world units
    private const int MaxMessages = 6;
    private const string JoinText = " has entered the chat";
    private const string RejoinText = " has rejoined the chat";
    private const string LeaveText = " has left the chat";

    private const int TextureWidth = 512 * TexScale, TextureHeight = 48 * TexScale;

    // READABILITY (all labels)
    private const float NameLighten = 0.45f;          // how far a bird's color is mixed toward white for its name
    private const int OutlineWidth = 2 * TexScale;               // pixels in the label texture
    private static readonly Color OutlineColor = new Color(6, 9, 14, 255);
    private static readonly Color PlateColor = new Color(6, 9, 14, 100);    // the faint plate behind the text
    private const int PlatePadding = 12 * TexScale;              // plate reaches this far past the text on each side
    private const int PlateMargin = 4 * TexScale;                // gap above and below the plate inside the texture
    private const float NearDistance = 14f;           // closer than this, labels keep their base size
    private const float DistanceGrowth = 0.5f;        // 0: fixed in the box, 1: same size on screen at any distance
    private const float MaxGrowth = 1.8f;

    // SLEEPING Z'S (every roosting bird, once it has settled; sizes in box units, before the distance growth)
    private const float SleepCycle = 3.6f;        // seconds from a z appearing to it being gone
    private const int SleepZs = 3;                // z's on their way up at once, evenly spaced through the cycle
    private const float SleepRise = 0.55f;        // how far a z climbs over its life
    private const float SleepDrift = 0.25f;       // how far it drifts sideways (to the screen's right)
    private const float SleepWobble = 0.04f;      // its gentle sway side to side
    private const float SleepStartSize = 0.06f;   // half a z's height as it appears...
    private const float SleepEndSize = 0.11f;     // ...and as it fades away
    private const float SleepGap = 0f;            // the first z starts this far above the head's standing height (or above its name tag);
                                                  // the roost pose sits lower, so 0 already leaves a small gap
    private const float SleepAlpha = 0.85f;
    private const float SleepLighten = 0.55f;     // how far the hologram color is mixed toward white
    private const float SleepFadeTime = 0.8f;     // seconds for a bird's z's to fade in as it settles, and out as it wakes
    private const int ZTextureSize = 64 * TexScale;

    private RenderTexture2D zTexture;                                // one z, drawn once in white and tinted when shown
    private readonly Dictionary<int, float> sleepFade = new();      // by bird EntityID: 0 awake, 1 fully asleep
    private float sleepClock;                                        // seconds; stands still while the game is paused

    private class Message
    {
        public Bird? Bird;                // null for an event's label
        public string? Text;              // an event's label text ("A hawk's shadow passes over"); null for a bird's
        public bool Leaving;              // a "left the chat" label
        public Vector3 LastPosition;      // kept so the label can finish fading if the bird is gone
        public float Age;
        public float Lifetime = ChatMessages.Lifetime;
        public RenderTexture2D Texture;   // Id 0 until PrepareTextures draws it

        public Message(Bird bird, bool leaving)
        {
            Bird = bird;
            Leaving = leaving;
            LastPosition = bird.Position;
        }

        public Message(string text, Vector3 position)
        {
            Text = text;
            LastPosition = position;
            Lifetime = EventLifetime;
        }
    }

    // EVENT LABELS (the Storyteller's events, when they happen in view): stay where they started, rise a little, last longer
    private const float EventLifetime = 7f;
    private const float EventLift = 0.8f;         // they start this much higher than a bird's label, above the birds

    private readonly List<Message> messages = new();
    private readonly List<Message> finished = new();     // waiting for their textures to be freed
    private readonly HashSet<int> announcedBirds = new();
    private readonly HashSet<int> departedBirds = new();
    private bool firstUpdate = true;

    // ---------------- UPDATE (no drawing) ----------------

    // Announce birds that arrived after the game started, the first time they enter the view,
    // and birds that start leaving while in view.
    // sleepDt moves the sleeping z's on: the frame's time, or 0 while the game is paused.
    public void Update(ViewWindow view, float dt, float sleepDt)
    {
        IReadOnlyList<Bird> birds = World.Instance.GetAllEntities<Bird>();

        // Birds already here on the first frame are residents: mark them as seen without a message.
        if (firstUpdate)
        {
            foreach (Bird bird in birds)
            {
                announcedBirds.Add(bird.EntityID);
                if (bird.State == BirdState.Leaving) departedBirds.Add(bird.EntityID);
            }
            firstUpdate = false;
        }

        // Quiet birds (a passing flock, birds scattered by the hawk) never get one
        foreach (Bird bird in birds)
        {
            if (announcedBirds.Contains(bird.EntityID)) continue;
            if (!view.Contains(bird.Position)) continue;

            announcedBirds.Add(bird.EntityID);
            if (!bird.Quiet) messages.Add(new Message(bird, leaving: false));
        }

        // Checked the moment a bird starts leaving, so one that is already on its way out never gets a label later
        foreach (Bird bird in birds)
        {
            if (bird.State != BirdState.Leaving || departedBirds.Contains(bird.EntityID)) continue;

            departedBirds.Add(bird.EntityID);
            if (view.Contains(bird.Position) && !bird.Quiet) messages.Add(new Message(bird, leaving: true));
        }

        foreach (Message msg in messages)
        {
            msg.Age += dt;
            if (!msg.Leaving && msg.Bird != null && World.Instance.Contains(msg.Bird)) msg.LastPosition = msg.Bird.Position;
        }

        while (messages.Count > MaxMessages) Finish(messages[0]);
        foreach (Message msg in messages.ToList())
        {
            if (msg.Age >= msg.Lifetime) Finish(msg);
        }

        UpdateNameLabels(view, birds);
        UpdateSleepers(birds, sleepDt);
    }

    // Each bird's z's ease in once it has settled into its roost (not while it's still flying there),
    // and ease out when it wakes or is gone.
    private void UpdateSleepers(IReadOnlyList<Bird> birds, float sleepDt)
    {
        sleepClock += sleepDt;
        float step = sleepDt / SleepFadeTime;

        foreach (Bird bird in birds)
        {
            bool asleep = bird.Asleep && bird.Pose == BirdPose.Roost;
            sleepFade.TryGetValue(bird.EntityID, out float fade);
            if (!asleep && fade <= 0f) continue;
            sleepFade[bird.EntityID] = Math.Clamp(fade + (asleep ? step : -step), 0f, 1f);
        }

        foreach (int id in sleepFade.Keys.ToList())
        {
            bool gone = !birds.Any(bird => bird.EntityID == id);
            if (gone || sleepFade[id] <= 0f) sleepFade.Remove(id);
        }
    }

    // Which birds get a name tag: the ones the setting allows, in view, not leaving,
    // and not showing an "entered the chat" label right now.
    private void UpdateNameLabels(ViewWindow view, IReadOnlyList<Bird> birds)
    {
        labeledBirds.Clear();
        taggableBirds.Clear();
        if (NameLabels == NameLabelMode.Off) return;

        foreach (Bird bird in birds)
        {
            if (NameLabels == NameLabelMode.Named && !bird.IsNamed) continue;
            if (bird.State == BirdState.Leaving) continue;
            taggableBirds.Add(bird.EntityID);

            if (!view.IsShown(bird.Position, 0.5f)) continue;
            if (messages.Any(msg => !msg.Leaving && msg.Bird == bird)) continue;
            if (bird.FlockMember) continue;    // a passing flock has no names worth showing
            labeledBirds.Add(bird);
        }
    }

    // An event's label at this world spot (the Storyteller only sends ones that happen in view).
    public void Announce(string text, Vector3 position)
    {
        messages.Add(new Message(text, position));
    }

    private void Finish(Message msg)
    {
        messages.Remove(msg);
        finished.Add(msg);
    }

    // ---------------- DRAW (no simulation changes) ----------------

    // Draws any new labels into their textures and frees old ones.
    // Must be called outside any other BeginTextureMode, since Raylib can't nest them.
    public void PrepareTextures()
    {
        foreach (Message msg in finished)
        {
            if (msg.Texture.Id != 0) Raylib.UnloadRenderTexture(msg.Texture);
        }
        finished.Clear();

        foreach (Message msg in messages)
        {
            if (msg.Texture.Id != 0) continue;
            msg.Texture = Raylib.LoadRenderTexture(TextureWidth, TextureHeight);
            Raylib.SetTextureFilter(msg.Texture.Texture, TextureFilter.Trilinear);
            if (msg.Bird != null) RenderLabel(msg.Texture, msg.Bird.Name, msg.Bird.Species.Color, msg.Leaving, msg.Bird.Visits > 1);
            else RenderEventLabel(msg.Texture, msg.Text ?? "");
        }

        PrepareNameTags();
        if (zTexture.Id == 0) RenderZ();
    }

    // Fills in the smaller copies of a finished label texture, so it stays clean when drawn small (needs the Trilinear filter).
    private static void BuildMipmaps(RenderTexture2D target)
    {
        Texture2D texture = target.Texture;
        Raylib.GenTextureMipmaps(ref texture);
    }

    // The sleeping z, drawn once in white with a dark outline: a top bar, a diagonal and a bottom bar, with rounded joints.
    // Drawn as lines rather than a letter so it sits exactly in the middle of its texture.
    private void RenderZ()
    {
        zTexture = Raylib.LoadRenderTexture(ZTextureSize, ZTextureSize);
        Raylib.SetTextureFilter(zTexture.Texture, TextureFilter.Trilinear);

        float s = ZTextureSize;
        Vector2[] corners = { new(s * 0.28f, s * 0.26f), new(s * 0.72f, s * 0.26f), new(s * 0.28f, s * 0.74f), new(s * 0.72f, s * 0.74f) };
        Raylib.BeginTextureMode(zTexture);
            Raylib.ClearBackground(Color.Blank);
            DrawZStrokes(corners, s * 0.11f + OutlineWidth * 2, OutlineColor);
            DrawZStrokes(corners, s * 0.11f, Color.White);
        Raylib.EndTextureMode();
        BuildMipmaps(zTexture);
    }

    // The three strokes of a z through its corners (top left, top right, bottom left, bottom right).
    private static void DrawZStrokes(Vector2[] c, float thickness, Color color)
    {
        Raylib.DrawLineEx(c[0], c[1], thickness, color);
        Raylib.DrawLineEx(c[1], c[2], thickness, color);
        Raylib.DrawLineEx(c[2], c[3], thickness, color);
        foreach (Vector2 corner in c) Raylib.DrawCircleV(corner, thickness / 2f, color);
    }

    // A tag's texture is made the first time its bird is labeled, redrawn after a rename,
    // and freed once the bird is gone or no longer allowed a tag.
    private void PrepareNameTags()
    {
        foreach (int id in nameTags.Keys.ToList())
        {
            if (taggableBirds.Contains(id)) continue;
            Raylib.UnloadRenderTexture(nameTags[id].Texture);
            nameTags.Remove(id);
        }

        foreach (Bird bird in labeledBirds)
        {
            if (nameTags.TryGetValue(bird.EntityID, out NameTag? tag) && tag.Name == bird.Name) continue;

            if (tag == null)
            {
                tag = new NameTag { Texture = Raylib.LoadRenderTexture(NameTextureWidth, TextureHeight) };
                Raylib.SetTextureFilter(tag.Texture.Texture, TextureFilter.Trilinear);
                nameTags[bird.EntityID] = tag;
            }
            tag.Name = bird.Name;
            RenderName(tag.Texture, bird.Name, bird.Species.Color);
        }
    }

    // Just the name, in a lightened bird color, on its plate, centered and shrunk to fit.
    private static void RenderName(RenderTexture2D target, string name, Color color)
    {
        int size = 34 * TexScale;
        while (size > 10 * TexScale && UiText.Measure(name, size) > NameTextureWidth - PlatePadding * 2 - 4 * TexScale) size -= 2 * TexScale;
        int width = UiText.Measure(name, size);
        int x = (NameTextureWidth - width) / 2;
        int y = (TextureHeight - size) / 2;

        Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Color.Blank);
            DrawPlate(x, width);
            DrawOutlined(name, x, y, size, Lighten(color));
        Raylib.EndTextureMode();
        BuildMipmaps(target);
    }

    // The name in a lightened bird color, the rest in white, on one plate, centered and shrunk to fit.
    // Leaving labels use a darker name color and gray text.
    private static void RenderLabel(RenderTexture2D target, string name, Color nameColor, bool leaving, bool returning)
    {
        string text = leaving ? LeaveText : returning ? RejoinText : JoinText;
        nameColor = Lighten(nameColor);
        if (leaving) nameColor = Raylib.ColorBrightness(nameColor, -0.3f);
        Color textColor = leaving ? Palette.DimText : Palette.Text;

        int size = 36 * TexScale;
        while (size > 10 * TexScale && UiText.Measure(name + text, size) > TextureWidth - PlatePadding * 2 - 4 * TexScale) size -= 2 * TexScale;
        int nameWidth = UiText.Measure(name, size);
        int width = UiText.Measure(name + text, size);
        int x = (TextureWidth - width) / 2;
        int y = (TextureHeight - size) / 2;

        Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Color.Blank);
            DrawPlate(x, width);
            DrawOutlined(name, x, y, size, nameColor);
            DrawOutlined(text, x + nameWidth, y, size, textColor);
        Raylib.EndTextureMode();
        BuildMipmaps(target);
    }

    // An event's line in plain light text on its plate, centered and shrunk to fit.
    private static void RenderEventLabel(RenderTexture2D target, string text)
    {
        int size = 36 * TexScale;
        while (size > 10 * TexScale && UiText.Measure(text, size) > TextureWidth - PlatePadding * 2 - 4 * TexScale) size -= 2 * TexScale;
        int width = UiText.Measure(text, size);
        int x = (TextureWidth - width) / 2;
        int y = (TextureHeight - size) / 2;

        Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Color.Blank);
            DrawPlate(x, width);
            DrawOutlined(text, x, y, size, Palette.Text);
        Raylib.EndTextureMode();
        BuildMipmaps(target);
    }

    // A faint dark rounded plate behind a line of text, a little wider than the text.
    // Drawn with premultiplied blending so the texture keeps the plate's alpha as given
    // (normal blending onto a clear texture would square it and leave the plate fainter).
    private static void DrawPlate(int textX, int textWidth)
    {
        Rectangle plate = new Rectangle(textX - PlatePadding, PlateMargin, textWidth + PlatePadding * 2, TextureHeight - PlateMargin * 2);
        Raylib.BeginBlendMode(BlendMode.AlphaPremultiply);
            Raylib.DrawRectangleRounded(plate, 1f, 12, PlateColor);
        Raylib.EndBlendMode();
    }

    // Text with a thin dark outline around every letter: the text is stamped in the outline color
    // at eight small offsets, then once on top in its own color.
    private static void DrawOutlined(string text, int x, int y, int size, Color color)
    {
        for (int dx = -OutlineWidth; dx <= OutlineWidth; dx += OutlineWidth)
        {
            for (int dy = -OutlineWidth; dy <= OutlineWidth; dy += OutlineWidth)
            {
                if (dx != 0 || dy != 0) UiText.Draw(text, x + dx, y + dy, size, OutlineColor);
            }
        }
        UiText.Draw(text, x, y, size, color);
    }

    // The bird's color moved toward white, so the hue stays but it stands out from the hologram's tint.
    private static Color Lighten(Color color) => Palette.Mix(color, Color.White, NameLighten);

    // Called inside the 3D scene. Positions are turned with the hologram by hand (BoxToScene),
    // so the label can be built facing the camera in scene space.
    // Depth writes are off, so the clear parts of a label don't hide things behind it.
    // holoColor: the hologram's color, which the sleeping z's take on (lightened).
    public void Draw(Camera3D camera, ViewWindow view, Vector3 holoColor)
    {
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Cross(right, forward);
        float halfWidth = LabelWidth / 2f;
        float halfHeight = halfWidth * TextureHeight / TextureWidth;

        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableDepthMask();
        DrawSleepers(camera, view, right, up, holoColor);
        DrawNameTags(camera, view, right, up);
        foreach (Message msg in messages)
        {
            if (msg.Texture.Id == 0) continue;

            float alpha = 1f;
            if (msg.Age < FadeIn) alpha = msg.Age / FadeIn;
            else if (msg.Age > msg.Lifetime - FadeOut) alpha = (msg.Lifetime - msg.Age) / FadeOut;

            float drift = msg.Leaving ? -msg.Age * RiseSpeed : msg.Age * RiseSpeed;    // leaving labels sink
            float lift = msg.Bird == null ? StartHeight + EventLift : StartHeight;
            Vector3 local = view.ToLocal(msg.LastPosition) + new Vector3(0, lift + drift, 0);
            alpha *= view.FogFade(local);
            if (alpha <= 0.01f) continue;

            Vector3 center = view.BoxToScene(local);
            float grow = DistanceScale(camera, center);
            DrawFacingQuad(msg.Texture.Texture, center, right * halfWidth * grow, up * halfHeight * grow, alpha);
        }
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableDepthMask();
    }

    // Each tag floats a little above the top of its bird's head and follows it.
    // A grown tag rises by its extra height so its bottom edge stays the same gap above the head.
    private void DrawNameTags(Camera3D camera, ViewWindow view, Vector3 right, Vector3 up)
    {
        float halfWidth = NameLabelWidth / 2f;
        float halfHeight = halfWidth * TextureHeight / NameTextureWidth;

        foreach (Bird bird in labeledBirds)
        {
            if (!nameTags.TryGetValue(bird.EntityID, out NameTag? tag)) continue;

            Vector3 head = new Vector3(bird.Position.X, bird.Position.Y + bird.Bounds.Max.Y * bird.Size, bird.Position.Z);
            Vector3 headLocal = view.ToLocal(head);
            float grow = DistanceScale(camera, view.BoxToScene(headLocal));
            Vector3 local = headLocal + new Vector3(0, NameLift + halfHeight * grow, 0);
            float alpha = NameAlpha * view.FogFade(local);
            if (alpha <= 0.01f) continue;

            DrawFacingQuad(tag.Texture.Texture, view.BoxToScene(local), right * halfWidth * grow, up * halfHeight * grow, alpha);
        }
    }

    // Each sleeping bird's z's climb from just above its head (above its name tag, if it has one showing),
    // growing and drifting off to one side as they go, fading in at the start and out at the top.
    // Birds are out of step with each other, so a row of sleepers doesn't pulse together.
    private void DrawSleepers(Camera3D camera, ViewWindow view, Vector3 right, Vector3 up, Vector3 holoColor)
    {
        if (zTexture.Id == 0 || sleepFade.Count == 0) return;

        Color tint = Palette.Mix(Palette.ToColor(holoColor), Color.White, SleepLighten);
        float tagHeight = NameLabelWidth * TextureHeight / NameTextureWidth;    // a name tag's full height at its base size

        foreach (Bird bird in World.Instance.GetAllEntities<Bird>())
        {
            if (!sleepFade.TryGetValue(bird.EntityID, out float fade)) continue;

            Vector3 head = new Vector3(bird.Position.X, bird.Position.Y + bird.Bounds.Max.Y * bird.Size, bird.Position.Z);
            Vector3 headLocal = view.ToLocal(head);
            float grow = DistanceScale(camera, view.BoxToScene(headLocal));
            float above = labeledBirds.Contains(bird) ? NameLift + tagHeight * grow : 0f;
            Vector3 start = headLocal + new Vector3(0, above + SleepGap * grow, 0);
            float offset = bird.EntityID * 0.618034f % 1f;

            for (int i = 0; i < SleepZs; i++)
            {
                float life = (sleepClock / SleepCycle + offset + (float)i / SleepZs) % 1f;    // 0 as a z appears, 1 as it's gone
                Vector3 local = start + new Vector3(0, life * SleepRise * grow, 0);
                float alpha = SleepAlpha * fade * MathEx.Smooth(0f, 0.15f, life) * (1f - MathEx.Smooth(0.5f, 0.95f, life)) * view.FogFade(local);
                if (alpha <= 0.01f) continue;

                float sideways = (life * SleepDrift + MathF.Sin(life * MathF.Tau) * SleepWobble) * grow;
                float half = (SleepStartSize + (SleepEndSize - SleepStartSize) * life) * grow;
                Vector3 center = view.BoxToScene(local) + right * sideways;
                DrawFacingQuad(zTexture.Texture, center, right * half, up * half, alpha, tint);
            }
        }
    }

    // How much bigger a label is drawn at this distance from the camera: 1 up close, growing as the
    // camera pulls back so labels shrink on screen more slowly than the birds do.
    private static float DistanceScale(Camera3D camera, Vector3 center)
    {
        float distance = Vector3.Distance(camera.Position, center);
        return Math.Clamp(MathF.Pow(distance / NearDistance, DistanceGrowth), 1f, MaxGrowth);
    }

    // Render textures are stored upside down, so the top of the quad uses v = 1.
    // tint colors the texture (white leaves it as drawn).
    private static void DrawFacingQuad(Texture2D texture, Vector3 center, Vector3 right, Vector3 up, float alpha) =>
        DrawFacingQuad(texture, center, right, up, alpha, Color.White);

    private static void DrawFacingQuad(Texture2D texture, Vector3 center, Vector3 right, Vector3 up, float alpha, Color tint)
    {
        Vector3 topLeft = center - right + up, bottomLeft = center - right - up;
        Vector3 bottomRight = center + right - up, topRight = center + right + up;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(tint.R, tint.G, tint.B, (byte)(alpha * 255));
            Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(topLeft.X, topLeft.Y, topLeft.Z);
            Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
            Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(bottomRight.X, bottomRight.Y, bottomRight.Z);
            Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(topRight.X, topRight.Y, topRight.Z);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    public void Unload()
    {
        foreach (Message msg in messages.Concat(finished))
        {
            if (msg.Texture.Id != 0) Raylib.UnloadRenderTexture(msg.Texture);
        }
        messages.Clear();
        finished.Clear();
        foreach (NameTag tag in nameTags.Values) Raylib.UnloadRenderTexture(tag.Texture);
        nameTags.Clear();
        if (zTexture.Id != 0) Raylib.UnloadRenderTexture(zTexture);
        zTexture = default;
        sleepFade.Clear();
    }
}
