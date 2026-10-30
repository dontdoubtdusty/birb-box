using System.Numerics;
using ImGuiNET;
using Raylib_cs;

// The Birb Designer: build a birb out of parts and watch it move.
// While it's open the world pauses and the hologram shows only the birb being designed, standing in the middle.
// Dragging still turns the hologram, so the birb can be seen from every side.
// It edits a copy, so birds already in the world keep their shape until the design is saved.
// Designs are saved as .json files in resources/birbs/ (see BirbFile.cs). Each save is one more look for its
// species in the world, and birds already wearing it switch to the saved version (see Birds.AddDesign).
// Release saves the design and sends one bird wearing it into the middle of the view.
// Opened with B or from the Esc menu.
public class BirbDesigner
{
    public bool IsOpen { get; private set; }

    // The birb being designed: its shape plus species details (species, size; map color and common for later).
    private BirbFile design = FromBird(BirdType.Emberwing, Birds.Get(BirdType.Emberwing).BuiltInLooks[0]);
    public BirbShape Shape => design.Shape;

    // SAVING
    private string? currentFile;          // the file this design was opened from or last saved to
    private string? pendingReplace;       // waiting for "Replace?" to be answered for this file name
    private string status = "Not saved yet.";
    private bool releasePending;          // Release was pressed: release once the save goes through
    private BirdLook? releaseLook;        // saved and ready to release (handled in DrawWindow, which has the engine)

    // PREVIEW
    private float previewZoom = 4f;       // extra magnification so small birbs are easy to see; 1 = true size
    private bool highlightSelected = true;
    private BirdPose testPose = BirdPose.Idle;
    private float clock;                  // preview animation time
    private float phase;                  // stands in for a bird's HopPhase

    // How fast the phase runs in each pose, about what BirdMover gives a bird of middling speed.
    private const float HopRate = 8.4f;
    private const float WalkRate = 5.6f;
    private const float PeckRate = 6f;

    // PART EDITING
    private int selected = 0;             // index into Shape.Parts, -1 for none
    private string nameBuffer = "";       // the part name being typed, applied when the box is left
    private int nameBufferFor = -1;       // which part nameBuffer was filled from

    private bool wasPaused;               // the pause state from before the Designer opened

    private BirbPart? SelectedPart => selected >= 0 && selected < Shape.Parts.Count ? Shape.Parts[selected] : null;

    // ---------------- OPEN / CLOSE ----------------

    public void Toggle(GameEngine engine)
    {
        if (IsOpen) Close(engine);
        else Open(engine);
    }

    public void Open(GameEngine engine)
    {
        if (IsOpen) return;
        IsOpen = true;
        wasPaused = engine.Paused;
        engine.Paused = true;
        engine.SelectedBird = null;
        engine.HoveredBird = null;
    }

    public void Close(GameEngine engine)
    {
        if (!IsOpen) return;
        IsOpen = false;
        engine.Paused = wasPaused;
    }

    // ---------------- UPDATE (no drawing) ----------------

    public void Update(float dt)
    {
        clock += dt;
        switch (testPose)
        {
            case BirdPose.Hop: phase += dt * HopRate; break;
            case BirdPose.Walk: phase += dt * WalkRate; break;
            case BirdPose.Peck: phase += dt * PeckRate; break;
            case BirdPose.Sip: phase += dt * BirdMover.SipRate; break;
            case BirdPose.Bathe: phase += dt * BirdMover.BathRate; break;
        }
    }

    // ---------------- PREVIEW (draw only) ----------------

    // Draws the birb standing in the middle of the hologram. Called by HologramRenderer inside its 3D pass.
    public void DrawPreview(ViewWindow view)
    {
        BirbMotion motion = BirbAnimator.PoseMotion(testPose, clock, phase, Shape.Style);
        float magnify = view.Zoom * previewZoom;
        float scale = design.Size * magnify;

        // Feet on the floor, like Bird.GroundY, plus the hop
        BoundingBox bounds = Shape.GetBounds();
        Vector3 position = new Vector3(0f, -bounds.Min.Y * scale + motion.Bounce * magnify, 0f);

        BirbPart? highlight = highlightSelected ? SelectedPart : null;
        BirbBuilder.Draw(Shape, position, view.Yaw, scale, Color.White, motion, highlight);
    }

    // ---------------- WINDOW ----------------

    public void DrawWindow(GameEngine engine)
    {
        if (!IsOpen) return;

        ImGui.SetNextWindowPos(new Vector2(12, 60), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(380, 680), ImGuiCond.FirstUseEver);
        bool stayOpen = true;
        ImGui.Begin("Birb Designer", ref stayOpen);

        DrawTopSection();
        if (releaseLook != null) Release(engine, releaseLook);
        if (ImGui.CollapsingHeader("Preview", ImGuiTreeNodeFlags.DefaultOpen)) DrawPreviewSection();
        if (ImGui.CollapsingHeader("Species")) DrawSpeciesSection();
        if (ImGui.CollapsingHeader("Movement")) DrawMovementSection();
        if (ImGui.CollapsingHeader("Parts", ImGuiTreeNodeFlags.DefaultOpen)) DrawPartsSection();

        ImGui.End();
        if (!stayOpen) Close(engine);
    }

    private void DrawTopSection()
    {
        ImGui.InputText("Birb name", ref Shape.Name, 40);

        // Starting over from one of the birbs built into the box
        if (ImGui.BeginCombo("Start from", "Pick a birb..."))
        {
            foreach (BirdType type in Birds.All)
            {
                foreach (BirdLook starter in Birds.Get(type).BuiltInLooks)
                {
                    if (ImGui.Selectable(starter.Shape.Name)) UseDesign(FromBird(type, starter), null);
                }
            }
            ImGui.EndCombo();
        }

        // Opening a saved birb. The folder is read only while the list is open.
        if (ImGui.BeginCombo("Open saved", "Pick a file..."))
        {
            List<string> files = BirbFiles.List();
            if (files.Count == 0) ImGui.TextDisabled("No saved birbs yet.");
            foreach (string file in files)
            {
                if (ImGui.Selectable(file)) OpenFile(file);
            }
            ImGui.EndCombo();
        }

        DrawSaveButtons();
        ImGui.TextDisabled(status);
    }

    // Save and Release, or Replace / Cancel while waiting for an answer.
    // Asks only when the file belongs to a different birb, not when saving back to the file this one came from.
    // Release saves first, so the released bird's look is in a file and survives the world being saved.
    private void DrawSaveButtons()
    {
        string fileName = BirbFiles.FileNameFor(Shape.Name);
        if (pendingReplace != null && pendingReplace != fileName)    // the name changed
        {
            pendingReplace = null;
            releasePending = false;
        }

        if (pendingReplace != null)
        {
            ImGui.Text($"Replace {pendingReplace}.json?");
            ImGui.SameLine();
            if (ImGui.Button("Replace")) SaveAs(pendingReplace);
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                pendingReplace = null;
                releasePending = false;
            }
            return;
        }

        bool save = ImGui.Button("Save");
        ImGui.SameLine();
        bool release = ImGui.Button("Release into the Cube");
        if (!save && !release) return;

        releasePending = release;
        if (fileName.Length == 0)
        {
            status = "Give the birb a name first.";
            releasePending = false;
        }
        else if (fileName != currentFile && BirbFiles.Exists(fileName)) pendingReplace = fileName;
        else SaveAs(fileName);
    }

    // Writes the file, then puts the design among its species' looks and updates birds already wearing it.
    private void SaveAs(string fileName)
    {
        pendingReplace = null;
        if (!BirbFiles.Save(design, fileName))
        {
            status = "Couldn't save. See the Bird Log (Tab) for why.";
            releasePending = false;
            return;
        }
        currentFile = fileName;
        status = $"Saved {fileName}.json";
        EventLog.Add($"{Shape.Name} was saved to {Path.Combine(BirbFiles.Folder, fileName)}.json");

        BirdLook look = Birds.AddDesign(design, fileName);
        foreach (Bird bird in World.Instance.GetAllEntities<Bird>()) bird.RefreshLook();

        if (releasePending) releaseLook = look;
        releasePending = false;
    }

    // Closes the Designer and sends one bird wearing the saved look down into the middle of the view.
    private void Release(GameEngine engine, BirdLook look)
    {
        releaseLook = null;
        Close(engine);
        Vector3 middle = new Vector3(engine.View.Center.X, 0f, engine.View.Center.Y);
        engine.BirdSpawner.Release(design.Species, look, middle);
    }

    private void OpenFile(string fileName)
    {
        BirbFile? loaded = BirbFiles.Load(fileName);
        if (loaded == null)
        {
            status = $"Couldn't open {fileName}.json. See the Bird Log (Tab) for why.";
            return;
        }
        UseDesign(loaded, fileName);
        status = $"Opened {fileName}.json";
    }

    // Swaps in a different design and resets the part selection. file is null for an unsaved design.
    private void UseDesign(BirbFile newDesign, string? file)
    {
        design = newDesign;
        currentFile = file;
        pendingReplace = null;
        if (file == null) status = "Not saved yet.";
        selected = 0;
        nameBufferFor = -1;
    }

    // A built-in birb as a design: a copy of its shape plus its species details from the bird table.
    private static BirbFile FromBird(BirdType type, BirdLook look)
    {
        BirdInfo info = Birds.Get(type);
        return new BirbFile
        {
            Species = type, Size = look.Size, MapColor = info.Color, Common = info.Common, Shape = look.Shape.Copy(),
        };
    }

    private void DrawPreviewSection()
    {
        EnumCombo("Test pose", ref testPose);
        ImGui.SliderFloat("Zoom", ref previewZoom, 1f, 8f, "%.1fx");
        ImGui.Checkbox("Highlight selected part", ref highlightSelected);

        BoundingBox b = Shape.GetBounds();
        Vector3 size = (b.Max - b.Min) * design.Size;
        ImGui.TextDisabled($"In the world: {size.X:0.00} wide, {size.Y:0.00} tall, {size.Z:0.00} long");
    }

    // Which kind of bird wears this look in the world, and how big it is.
    // Picking a species starts the size at that species' own size.
    // Map color and Common belong to the species for now; they're kept in the file for new species later.
    private void DrawSpeciesSection()
    {
        if (ImGui.BeginCombo("Species##SpeciesCombo", Birds.Get(design.Species).Name))
        {
            foreach (BirdType type in Birds.All)
            {
                if (!ImGui.Selectable(Birds.Get(type).Name, type == design.Species) || type == design.Species) continue;
                design.Species = type;
                design.Size = Birds.Get(type).Size;
            }
            ImGui.EndCombo();
        }

        ImGui.SliderFloat("Size", ref design.Size, 0.1f, 0.6f, "%.2f");
        ImGui.TextDisabled($"{Birds.Get(design.Species).Name} size is {Birds.Get(design.Species).Size:0.00}.");

        ImGui.BeginDisabled();
        Vector3 color = ToVector(design.MapColor);
        ImGui.ColorEdit3("Map color", ref color);
        ImGui.Checkbox("Common (lives here, no special seed)", ref design.Common);
        ImGui.EndDisabled();
        ImGui.TextDisabled("Map color and Common come from the species for now.");
    }

    private void DrawMovementSection()
    {
        EnumCombo("Gait", ref Shape.Gait);

        MotionStyle s = Shape.Style;
        ImGui.SliderFloat("Hop height", ref s.HopHeight, 0f, 0.4f, "%.2f");
        ImGui.SliderFloat("Head bob", ref s.HeadBob, 0f, 0.4f, "%.2f");
        ImGui.SliderFloat("Step swing", ref s.StepSwing, 0f, 45f, "%.0f deg");
        ImGui.SliderFloat("Wing spread", ref s.WingSpread, 0f, 120f, "%.0f deg");
        ImGui.SliderFloat("Flap angle", ref s.FlapAngle, 0f, 90f, "%.0f deg");
        ImGui.SliderFloat("Flap speed", ref s.FlapSpeed, 2f, 40f, "%.0f");
        ImGui.SliderFloat("Look angle", ref s.LookAngle, 0f, 90f, "%.0f deg");
        ImGui.SliderFloat("Look step", ref s.LookStep, 0.2f, 2f, "%.2f s");
        ImGui.SliderFloat("Tail flick", ref s.TailFlick, 0f, 90f, "%.0f deg");
    }

    // ---------------- PARTS ----------------

    private void DrawPartsSection()
    {
        if (ImGui.BeginListBox("##parts", new Vector2(-1, 170)))
        {
            for (int i = 0; i < Shape.Parts.Count; i++)
            {
                BirbPart p = Shape.Parts[i];
                ImGui.PushID(i);
                if (ImGui.Selectable($"{p.Name}   ({p.Mesh}, {p.Role})", i == selected)) selected = i;
                ImGui.PopID();
            }
            ImGui.EndListBox();
        }

        if (ImGui.Button("Add")) AddPart();
        ImGui.SameLine();
        ImGui.BeginDisabled(SelectedPart == null);
        if (ImGui.Button("Duplicate")) DuplicateSelected();
        ImGui.SameLine();
        if (ImGui.Button("Mirror")) MirrorSelected();
        ImGui.SameLine();
        if (ImGui.Button("Remove")) RemoveSelected();
        ImGui.EndDisabled();
        ImGui.TextDisabled("Mirror makes or refreshes the twin: Left Wing <-> Right Wing.");

        BirbPart? part = SelectedPart;
        if (part == null) return;

        ImGui.SeparatorText(part.Name);
        DrawPartEditor(part);
    }

    private void DrawPartEditor(BirbPart part)
    {
        // The name is applied when you leave the box, so children are re-pointed once, not per letter.
        if (nameBufferFor != selected)
        {
            nameBuffer = part.Name;
            nameBufferFor = selected;
        }
        ImGui.InputText("Name", ref nameBuffer, 40);
        if (ImGui.IsItemDeactivatedAfterEdit()) RenamePart(part, nameBuffer);

        if (ImGui.BeginCombo("Mesh", part.Mesh))
        {
            foreach (string mesh in PartLibrary.Names)
            {
                if (ImGui.Selectable(mesh, mesh == part.Mesh)) part.Mesh = mesh;
            }
            ImGui.EndCombo();
        }

        EnumCombo("Role", ref part.Role);
        DrawParentCombo(part);

        ImGui.DragFloat3("Joint", ref part.Offset, 0.01f);
        ImGui.DragFloat3("Scale", ref part.Scale, 0.01f, 0.01f, 3f);
        ImGui.DragFloat3("Turn", ref part.Rotation, 1f, -360f, 360f, "%.0f");

        Vector3 color = ToVector(part.Color);
        if (ImGui.ColorEdit3("Color", ref color)) part.Color = ToColor(color);
        ImGui.TextDisabled("Joint and turn are from the birb's middle. +Z forward, +Y up, +X left.");
    }

    // A part can't follow itself or anything that already follows it (that would make a loop).
    private void DrawParentCombo(BirbPart part)
    {
        if (!ImGui.BeginCombo("Follows", part.Parent ?? "(nothing)")) return;

        if (ImGui.Selectable("(nothing)", part.Parent == null)) part.Parent = null;
        foreach (BirbPart other in Shape.Parts)
        {
            if (other == part || HangsFrom(other, part)) continue;
            if (ImGui.Selectable(other.Name, other.Name == part.Parent)) part.Parent = other.Name;
        }
        ImGui.EndCombo();
    }

    private void AddPart()
    {
        BirbPart part = new BirbPart(UniqueName("New Part"), PartLibrary.Sphere,
            new Vector3(0f, 0.5f, 0.5f), new Vector3(0.2f, 0.2f, 0.2f), Vector3.Zero, Color.White);
        if (Shape.Find("Body") != null) part.Parent = "Body";

        Shape.Parts.Add(part);
        selected = Shape.Parts.Count - 1;
    }

    private void DuplicateSelected()
    {
        BirbPart copy = SelectedPart!.Copy();
        copy.Name = UniqueName(copy.Name + " Copy");
        Shape.Parts.Insert(selected + 1, copy);
        selected++;
    }

    // Makes the part's twin on the other side, or refreshes it if it's already there.
    // The twin follows the matching part on its own side when there is one (a right foot follows the right leg).
    private void MirrorSelected()
    {
        BirbPart part = SelectedPart!;
        string twinName = SwapSide(part.Name);
        if (twinName == part.Name) twinName = UniqueName(part.Name + " Mirror");

        string? twinParent = part.Parent;
        if (twinParent != null && Shape.Find(SwapSide(twinParent)) != null) twinParent = SwapSide(twinParent);

        BirbPart twin = part.Mirrored(twinName, twinParent);
        int existing = Shape.Parts.FindIndex(p => p.Name == twinName);
        if (existing >= 0) Shape.Parts[existing] = twin;
        else Shape.Parts.Insert(selected + 1, twin);
    }

    // Parts that followed the removed one now follow whatever it followed.
    private void RemoveSelected()
    {
        BirbPart part = SelectedPart!;
        foreach (BirbPart p in Shape.Parts)
        {
            if (p.Parent == part.Name) p.Parent = part.Parent;
        }
        Shape.Parts.RemoveAt(selected);
        selected = Math.Min(selected, Shape.Parts.Count - 1);
        nameBufferFor = -1;
    }

    // Blank or already-used names are refused and the box goes back to the old name.
    private void RenamePart(BirbPart part, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0 || newName == part.Name || Shape.Find(newName) != null)
        {
            nameBuffer = part.Name;
            return;
        }
        foreach (BirbPart p in Shape.Parts)
        {
            if (p.Parent == part.Name) p.Parent = newName;
        }
        part.Name = newName;
    }

    // ---------------- HELPERS ----------------

    // True if 'other' follows 'part', directly or through a chain of parents.
    private bool HangsFrom(BirbPart other, BirbPart part)
    {
        string? parent = other.Parent;
        for (int steps = 0; parent != null && steps < Shape.Parts.Count; steps++)
        {
            if (parent == part.Name) return true;
            parent = Shape.Find(parent)?.Parent;
        }
        return false;
    }

    // "Left Wing" -> "Right Wing" and back. Names with neither side come back unchanged.
    private static string SwapSide(string name)
    {
        if (name.Contains("Left")) return name.Replace("Left", "Right");
        if (name.Contains("Right")) return name.Replace("Right", "Left");
        return name;
    }

    // "New Part", then "New Part 2", "New Part 3"... until one isn't taken.
    private string UniqueName(string name)
    {
        string tryName = name;
        for (int n = 2; Shape.Find(tryName) != null; n++) tryName = $"{name} {n}";
        return tryName;
    }

    private static void EnumCombo<T>(string label, ref T value) where T : struct, Enum
    {
        if (!ImGui.BeginCombo(label, value.ToString())) return;
        foreach (T option in Enum.GetValues<T>())
        {
            if (ImGui.Selectable(option.ToString(), option.Equals(value))) value = option;
        }
        ImGui.EndCombo();
    }

    // ImGui's color pickers use 0..1 numbers; Raylib colors use 0..255.
    private static Vector3 ToVector(Color c) => new Vector3(c.R, c.G, c.B) / 255f;
    private static Color ToColor(Vector3 v) => new Color(ToByte(v.X), ToByte(v.Y), ToByte(v.Z), 255);
    private static int ToByte(float x) => (int)MathF.Round(Math.Clamp(x, 0f, 1f) * 255f);
}
