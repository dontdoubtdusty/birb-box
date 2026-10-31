using System.Globalization;
using System.Numerics;
using System.Reflection;

// Reaches any field, property or method in the game by a dotted path, public or private, so a script
// can set up a situation no command covers: "engine.Camera.Pitch", "SimTuning.Now.MaxBirds", "engine.Weather.Soak".
// A path starts at "engine" (the running GameEngine) or at a game class's name (its static members).
public static class MemberPath
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // The value at the path, as text.
    public static string Get(GameEngine engine, string path)
    {
        (object? owner, Type type, string last) = WalkToOwner(engine, path);
        return Format(ReadMember(owner, type, last, path));
    }

    // Writes a value (text converted to the member's type) to a field or settable property.
    public static void Set(GameEngine engine, string path, string text)
    {
        (object? owner, Type type, string last) = WalkToOwner(engine, path);
        if (type.GetField(last, Any) is FieldInfo field)
        {
            field.SetValue(owner, Convert(text, field.FieldType, path));
            return;
        }
        PropertyInfo? prop = type.GetProperty(last, Any);
        if (prop == null) throw new ScriptError($"{path}: {type.Name} has no field or property '{last}'");
        MethodInfo? setter = prop.GetSetMethod(nonPublic: true);
        if (setter != null)
        {
            setter.Invoke(owner, new[] { Convert(text, prop.PropertyType, path) });
            return;
        }
        // An auto-property with no setter at all still has a compiler-made backing field
        FieldInfo? backing = type.GetField($"<{last}>k__BackingField", Any);
        if (backing == null) throw new ScriptError($"{path}: '{last}' can't be set");
        backing.SetValue(owner, Convert(text, prop.PropertyType, path));
    }

    // Calls a method with the given arguments (converted to its parameter types) and prints what it returns, if anything.
    public static void Call(GameEngine engine, string path, string[] args)
    {
        (object? owner, Type type, string last) = WalkToOwner(engine, path);
        MethodInfo? method = type.GetMethods(Any).FirstOrDefault(m => m.Name == last && m.GetParameters().Count(p => !p.IsOptional) <= args.Length && m.GetParameters().Length >= args.Length);
        if (method == null) throw new ScriptError($"{path}: {type.Name} has no method '{last}' taking {args.Length} argument(s)");

        ParameterInfo[] parameters = method.GetParameters();
        object?[] values = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            values[i] = i < args.Length ? Convert(args[i], parameters[i].ParameterType, path) : parameters[i].DefaultValue;

        object? result = method.Invoke(owner, values);
        if (method.ReturnType != typeof(void)) Console.WriteLine($"  {path} returned {Format(result)}");
    }

    // ---------------- WALKING THE PATH ----------------

    // Follows every part of the path but the last, returning the object (null for static) and type that own the last part.
    private static (object? owner, Type type, string last) WalkToOwner(GameEngine engine, string path)
    {
        string[] parts = path.Split('.');
        object? current;
        Type type;
        if (parts[0] == "engine")
        {
            current = engine;
            type = typeof(GameEngine);
        }
        else
        {
            type = typeof(GameEngine).Assembly.GetType(parts[0]) ?? throw new ScriptError($"{path}: no game class named '{parts[0]}'");
            current = null;
        }
        if (parts.Length < 2) throw new ScriptError($"{path}: a path needs at least one member after '{parts[0]}'");

        for (int i = 1; i < parts.Length - 1; i++)
        {
            current = ReadMember(current, type, parts[i], path);
            if (current == null) throw new ScriptError($"{path}: '{parts[i]}' is null");
            type = current.GetType();
        }
        return (current, type, parts[^1]);
    }

    private static object? ReadMember(object? owner, Type type, string name, string path)
    {
        if (type.GetField(name, Any) is FieldInfo field) return field.GetValue(owner);
        if (type.GetProperty(name, Any) is PropertyInfo prop) return prop.GetValue(owner);
        throw new ScriptError($"{path}: {type.Name} has no field or property '{name}'");
    }

    // ---------------- TEXT TO VALUES AND BACK ----------------

    // Text to the member's type: numbers, true/false, enum names, strings, and "x,y" / "x,y,z" for vectors.
    private static object? Convert(string text, Type type, string path)
    {
        Type target = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (text == "null" && !target.IsValueType) return null;
            if (target == typeof(string)) return text;
            if (target.IsEnum) return System.Enum.Parse(target, text, ignoreCase: true);
            if (target == typeof(Vector2)) { float[] v = Floats(text, 2); return new Vector2(v[0], v[1]); }
            if (target == typeof(Vector3)) { float[] v = Floats(text, 3); return new Vector3(v[0], v[1], v[2]); }
            return System.Convert.ChangeType(text, target, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is not ScriptError)
        {
            throw new ScriptError($"{path}: '{text}' can't be turned into {target.Name}");
        }
    }

    private static float[] Floats(string text, int count)
    {
        float[] values = text.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        if (values.Length != count) throw new FormatException();
        return values;
    }

    private static string Format(object? value) => value switch
    {
        null => "null",
        float f => f.ToString("0.####", CultureInfo.InvariantCulture),
        double d => d.ToString("0.####", CultureInfo.InvariantCulture),
        System.Collections.ICollection c => $"{value.GetType().Name} ({c.Count} items)",
        _ => value.ToString() ?? "",
    };
}
