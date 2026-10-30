using System.Numerics;
using Raylib_cs;

// Chat-style labels that float inside the hologram:
//  - "<name> has entered the chat" above a bird that arrived; it follows the bird and drifts up.
//  - "<name> has left the chat" where a bird took off to leave for good; it stays put, sinks, and is dimmer.
//  - "<name> has rejoined the chat" instead, for a regular coming back.
//  - name tags: just the name, floating over a bird for as long as it's in view (which birds: NameLabels setting).
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
    private const int NameTextureWidth = 256;

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

    private const int TextureWidth = 512, TextureHeight = 48;

    // READABILITY (all labels)
    private const float NameLighten = 0.45f;          // how far a bird's color is mixed toward white for its name
    private const int OutlineWidth = 2;               // pixels in the label texture
    private static readonly Color OutlineColor = new Color(6, 9, 14, 255);
    private static readonly Color PlateColor = new Color(6, 9, 14, 100);    // the faint plate behind the text
    private const int PlatePadding = 12;              // plate reaches this far past the text on each side
    private const int PlateMargin = 4;                // gap above and below the plate inside the texture
    private const float NearDistance = 14f;           // closer than this, labels keep their base size
    private const float DistanceGrowth = 0.5f;        // 0: fixed in the box, 1: same size on screen at any distance
    private const float MaxGrowth = 1.8f;

    private class Message
    {
        public Bird Bird;
        public bool Leaving;              // a "left the chat" label
        public Vector3 LastPosition;      // kept so the label can finish fading if the bird is gone
        public float Age;
        public RenderTexture2D Texture;   // Id 0 until PrepareTextures draws it

        public Message(Bird bird, bool leaving)
        {
            Bird = bird;
            Leaving = leaving;
            LastPosition = bird.Position;
        }
    }

    private readonly List<Message> messages = new();
    private readonly List<Message> finished = new();     // waiting for their textures to be freed
    private readonly HashSet<int> announcedBirds = new();
    private readonly HashSet<int> departedBirds = new();
    private bool firstUpdate = true;

    // ---------------- UPDATE (no drawing) ----------------

    // Announce birds that arrived after the game started, the first time they enter the view,
    // and birds that start leaving while in view.
    public void Update(ViewWindow view, float dt)
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

        foreach (Bird bird in birds)
        {
            if (announcedBirds.Contains(bird.EntityID)) continue;
            if (!view.Contains(bird.Position)) continue;

            announcedBirds.Add(bird.EntityID);
            messages.Add(new Message(bird, leaving: false));
        }

        // Checked the moment a bird starts leaving, so one that is already on its way out never gets a label later
        foreach (Bird bird in birds)
        {
            if (bird.State != BirdState.Leaving || departedBirds.Contains(bird.EntityID)) continue;

            departedBirds.Add(bird.EntityID);
            if (view.Contains(bird.Position)) messages.Add(new Message(bird, leaving: true));
        }

        foreach (Message msg in messages)
        {
            msg.Age += dt;
            if (!msg.Leaving && World.Instance.Contains(msg.Bird)) msg.LastPosition = msg.Bird.Position;
        }

        while (messages.Count > MaxMessages) Finish(messages[0]);
        foreach (Message msg in messages.ToList())
        {
            if (msg.Age >= Lifetime) Finish(msg);
        }

        UpdateNameLabels(view, birds);
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

            if (!view.Contains(bird.Position, 0.5f)) continue;
            if (messages.Any(msg => !msg.Leaving && msg.Bird == bird)) continue;
            labeledBirds.Add(bird);
        }
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
            Raylib.SetTextureFilter(msg.Texture.Texture, TextureFilter.Bilinear);
            RenderLabel(msg.Texture, msg.Bird.Name, msg.Bird.Info.Color, msg.Leaving, msg.Bird.Visits > 1);
        }

        PrepareNameTags();
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
                Raylib.SetTextureFilter(tag.Texture.Texture, TextureFilter.Bilinear);
                nameTags[bird.EntityID] = tag;
            }
            tag.Name = bird.Name;
            RenderName(tag.Texture, bird.Name, bird.Info.Color);
        }
    }

    // Just the name, in a lightened bird color, on its plate, centered and shrunk to fit.
    private static void RenderName(RenderTexture2D target, string name, Color color)
    {
        int size = 34;
        while (size > 10 && UiText.Measure(name, size) > NameTextureWidth - PlatePadding * 2 - 4) size -= 2;
        int width = UiText.Measure(name, size);
        int x = (NameTextureWidth - width) / 2;
        int y = (TextureHeight - size) / 2;

        Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Color.Blank);
            DrawPlate(x, width);
            DrawOutlined(name, x, y, size, Lighten(color));
        Raylib.EndTextureMode();
    }

    // The name in a lightened bird color, the rest in white, on one plate, centered and shrunk to fit.
    // Leaving labels use a darker name color and gray text.
    private static void RenderLabel(RenderTexture2D target, string name, Color nameColor, bool leaving, bool returning)
    {
        string text = leaving ? LeaveText : returning ? RejoinText : JoinText;
        nameColor = Lighten(nameColor);
        if (leaving) nameColor = Raylib.ColorBrightness(nameColor, -0.3f);
        Color textColor = leaving ? Palette.DimText : Palette.Text;

        int size = 36;
        while (size > 10 && UiText.Measure(name + text, size) > TextureWidth - PlatePadding * 2 - 4) size -= 2;
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
    public void Draw(Camera3D camera, ViewWindow view)
    {
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Cross(right, forward);
        float halfWidth = LabelWidth / 2f;
        float halfHeight = halfWidth * TextureHeight / TextureWidth;

        Vector3 eye = view.SceneToBox(camera.Position);    // labels fade in the fog as seen from here (ViewWindow.FogFade)

        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableDepthMask();
        DrawNameTags(camera, view, right, up, eye);
        foreach (Message msg in messages)
        {
            if (msg.Texture.Id == 0) continue;

            float alpha = 1f;
            if (msg.Age < FadeIn) alpha = msg.Age / FadeIn;
            else if (msg.Age > Lifetime - FadeOut) alpha = (Lifetime - msg.Age) / FadeOut;

            float drift = msg.Leaving ? -msg.Age * RiseSpeed : msg.Age * RiseSpeed;    // leaving labels sink
            Vector3 local = view.ToLocal(msg.LastPosition) + new Vector3(0, StartHeight + drift, 0);
            alpha *= view.FogFade(local, eye);
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
    private void DrawNameTags(Camera3D camera, ViewWindow view, Vector3 right, Vector3 up, Vector3 eye)
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
            float alpha = NameAlpha * view.FogFade(local, eye);
            if (alpha <= 0.01f) continue;

            DrawFacingQuad(tag.Texture.Texture, view.BoxToScene(local), right * halfWidth * grow, up * halfHeight * grow, alpha);
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
    private static void DrawFacingQuad(Texture2D texture, Vector3 center, Vector3 right, Vector3 up, float alpha)
    {
        Vector3 topLeft = center - right + up, bottomLeft = center - right - up;
        Vector3 bottomRight = center + right - up, topRight = center + right + up;

        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(255, 255, 255, (byte)(alpha * 255));
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
    }
}
