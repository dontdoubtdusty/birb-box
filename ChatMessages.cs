using System.Numerics;
using Raylib_cs;

// Chat-style labels that float inside the hologram:
//  - "<name> has entered the chat" above a bird that arrived; it follows the bird and drifts up.
//  - "<name> has left the chat" where a bird took off to leave for good; it stays put, sinks, and is dimmer.
// Labels fade out, and dim near the hologram's edge like everything else in the fog.
// Each label's text is drawn once into its own small texture, then shown on a flat rectangle
// that always faces the camera.
public class ChatMessages
{
    private const float Lifetime = 5f;
    private const float FadeIn = 0.3f;
    private const float FadeOut = 1.5f;
    private const float StartHeight = 1.0f;       // above the bird's feet
    private const float RiseSpeed = 0.15f;        // units per second
    private const float LabelWidth = 3.6f;        // in world units
    private const int MaxMessages = 6;
    private const string JoinText = " has entered the chat";
    private const string LeaveText = " has left the chat";

    private const int TextureWidth = 512, TextureHeight = 48;

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
            RenderLabel(msg.Texture, msg.Bird.Name, msg.Bird.Info.Color, msg.Leaving);
        }
    }

    // The name in the bird's color, the rest in white, centered and shrunk to fit.
    // Leaving labels use a darker name color and gray text.
    private static void RenderLabel(RenderTexture2D target, string name, Color nameColor, bool leaving)
    {
        string text = leaving ? LeaveText : JoinText;
        if (leaving) nameColor = Raylib.ColorBrightness(nameColor, -0.35f);
        Color textColor = leaving ? Palette.DimText : Palette.Text;

        int size = 36;
        while (size > 10 && UiText.Measure(name + text, size) > TextureWidth - 16) size -= 2;
        int nameWidth = UiText.Measure(name, size);
        int x = (TextureWidth - UiText.Measure(name + text, size)) / 2;
        int y = (TextureHeight - size) / 2;

        Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Color.Blank);
            UiText.Draw(name, x, y, size, nameColor);
            UiText.Draw(text, x + nameWidth, y, size, textColor);
        Raylib.EndTextureMode();
    }

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

        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableDepthMask();
        foreach (Message msg in messages)
        {
            if (msg.Texture.Id == 0) continue;

            float alpha = 1f;
            if (msg.Age < FadeIn) alpha = msg.Age / FadeIn;
            else if (msg.Age > Lifetime - FadeOut) alpha = (Lifetime - msg.Age) / FadeOut;

            float drift = msg.Leaving ? -msg.Age * RiseSpeed : msg.Age * RiseSpeed;    // leaving labels sink
            Vector3 local = view.ToLocal(msg.LastPosition) + new Vector3(0, StartHeight + drift, 0);
            alpha *= EdgeFade(local, view);
            if (alpha <= 0.01f) continue;

            DrawFacingQuad(msg.Texture.Texture, view.BoxToScene(local), right * halfWidth, up * halfHeight, alpha);
        }
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableDepthMask();
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

    // Same fade as the fog in hologram.fs: full strength in the middle, gone at the walls.
    private static float EdgeFade(Vector3 local, ViewWindow view)
    {
        float edge = ViewWindow.EdgeAmount(local.X, local.Z, view.HalfWidth);
        float t = Math.Clamp((edge - view.FogStart) / (1f - view.FogStart), 0f, 1f);
        return 1f - t * t * (3f - 2f * t);
    }

    public void Unload()
    {
        foreach (Message msg in messages.Concat(finished))
        {
            if (msg.Texture.Id != 0) Raylib.UnloadRenderTexture(msg.Texture);
        }
        messages.Clear();
        finished.Clear();
    }
}
