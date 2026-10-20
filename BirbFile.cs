using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

// One saved birb: its shape plus what kind of species it is. Each birb is one .json file in resources/birbs/.
// The file is plain text, so it can be read (and carefully hand-edited) in any text editor.
public class BirbFile
{
    public int FormatVersion = 1;                                // bumped if the layout of these files ever changes
    public float Size = 0.33f;                                   // birb units to world units, like BirdInfo.Size
    public Color MapColor = new Color(200, 200, 200, 255);       // name text, map dots and pedestal LEDs
    public bool Common = true;                                   // false = only special seed calls it (not used yet)
    public BirbShape Shape = new BirbShape("New Birb");
}

// Finding, saving and loading birb files.
public static class BirbFiles
{
    // Vector3 and Color keep their numbers in fields, not properties, so IncludeFields is needed.
    // Enums are written as words ("Wing", "Hop") so the files are easy to read.
    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    // Saves belong in the project's resources/birbs, so they survive rebuilds and the build copies them into the game.
    // With no project around, the game's own resources folder is used.
    public static string Folder { get; } = Assets.ProjectFolder != null
        ? Path.Combine(Assets.ProjectFolder, "resources", "birbs")
        : Assets.PathTo("birbs");

    // The saved birbs' file names, without ".json", in A-Z order.
    public static List<string> List()
    {
        if (!Directory.Exists(Folder)) return new List<string>();
        return Directory.GetFiles(Folder, "*.json")
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Order()
            .ToList();
    }

    // A birb's name with anything Windows won't allow in a file name taken out. Blank if nothing is left.
    public static string FileNameFor(string birbName)
    {
        return string.Concat(birbName.Trim().Split(Path.GetInvalidFileNameChars()));
    }

    public static bool Exists(string fileName) => File.Exists(PathFor(fileName));

    // Returns false (and says why in the log) if the file couldn't be written.
    public static bool Save(BirbFile birb, string fileName)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathFor(fileName), JsonSerializer.Serialize(birb, Options));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            EventLog.Add($"Couldn't save {fileName}.json: {e.Message}");
            return false;
        }
    }

    // Returns null (and says why in the log) if the file is missing or can't be read as a birb.
    public static BirbFile? Load(string fileName)
    {
        try
        {
            BirbFile? birb = JsonSerializer.Deserialize<BirbFile>(File.ReadAllText(PathFor(fileName)), Options);
            if (birb?.Shape == null)
            {
                EventLog.Add($"{fileName}.json has no birb shape in it.");
                return null;
            }

            // Hand-edited files might leave things out; fill the gaps instead of crashing later.
            birb.Shape.Parts ??= new List<BirbPart>();
            birb.Shape.Style ??= new MotionStyle();
            birb.Shape.Name ??= fileName;
            foreach (BirbPart part in birb.Shape.Parts)
            {
                part.Name ??= "Part";
                part.Mesh ??= PartLibrary.Sphere;
            }
            return birb;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            EventLog.Add($"Couldn't open {fileName}.json: {e.Message}");
            return null;
        }
    }

    private static string PathFor(string fileName) => Path.Combine(Folder, fileName + ".json");
}
