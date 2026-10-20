using Raylib_cs;

// Every sound in the game is loaded and played from here.
public static class Sounds
{
    private static Sound dialClick;

    public static void Load()
    {
        Raylib.InitAudioDevice();
        dialClick = Raylib.LoadSound(Assets.PathTo("sounds/dial_click.wav"));
    }

    public static void Unload()
    {
        Raylib.UnloadSound(dialClick);
        Raylib.CloseAudioDevice();
    }

    // The bezel ring passing a notch (see Dial)
    public static void PlayDialClick() => Raylib.PlaySound(dialClick);
}
