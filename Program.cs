using System.Runtime.InteropServices;

public class Program
{
    static void Main(string[] args)
    {
        try
        {
            GameEngine gameEngine = new GameEngine();
            gameEngine.Run();
        }
        catch (Exception e)
        {
            ReportCrash(e);
        }
    }

    // The game has no console window, so a crash would otherwise close it without a word.
    // This saves the error to crash.txt next to the game and shows it in a Windows message box.
    private static void ReportCrash(Exception e)
    {
        string file = Path.Combine(AppContext.BaseDirectory, "crash.txt");
        string report = $"{GameEngine.Version} crashed at {DateTime.Now}\n\n{e}";
        try
        {
            File.WriteAllText(file, report);
        }
        catch (Exception)
        {
            file = "(could not be saved)";    // the game's folder might not be writable
        }

        const uint ErrorIcon = 0x10;
        MessageBoxW(IntPtr.Zero, $"{e.Message}\n\nThe details were saved to:\n{file}", "Birb Box crashed", ErrorIcon);
    }

    // Windows' own message box, so no extra UI library is needed
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
