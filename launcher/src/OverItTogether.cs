// OverItTogether.exe - starts Getting Over It with the Over It Together mod.
// It copies the mod loader into the game folder, starts the game through Steam, waits until you quit,
// then removes the loader's entry file again so a normal Steam launch stays unmodded.
// Build: build.bat (uses the C# compiler that ships with Windows, no install needed).
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

static class Launcher
{
    const string AppId = "240720";
    const string ExeName = "GettingOverIt";
    // files that switch the mod loader on; removed again after the game closes
    static readonly string[] LoaderFiles = { "winhttp.dll", "doorstop_config.ini", ".doorstop_version" };

    static int Main(string[] args)
    {
        Console.Title = "Over It Together";
        Say("Over It Together");
        Say("");
        try
        {
            string game = FindGame();
            if (game == null) return Fail("Couldn't find Getting Over It. Is it installed through Steam?");
            if (args.Length > 0 && args[0] == "--check") { Say("Game found: " + game); return 0; }
            string payload = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mod");
            if (!Directory.Exists(payload)) return Fail("The 'mod' folder is missing. Unzip the whole download, then run this again.");
            if (Process.GetProcessesByName(ExeName).Length > 0) return Fail("Getting Over It is already running. Close it first, then run this again.");

            Say("Adding the mod...");
            CopyDir(payload, game);

            Say("Starting the game through Steam...");
            Process.Start(new ProcessStartInfo("steam://rungameid/" + AppId) { UseShellExecute = true });

            Process gameProc = WaitForGame(TimeSpan.FromSeconds(120));
            if (gameProc == null) { Cleanup(game); return Fail("The game didn't start. Try again, and make sure Steam is running."); }
            Say("Game running. Keep this window open - it tidies up when you quit.");
            gameProc.WaitForExit();
            Thread.Sleep(1500);
            Cleanup(game);
            Say("Done. Your game is back to normal.");
            Thread.Sleep(1500);
            return 0;
        }
        catch (Exception e)
        {
            return Fail("Something went wrong: " + e.Message);
        }
    }

    static Process WaitForGame(TimeSpan timeout)
    {
        var until = DateTime.Now + timeout;
        while (DateTime.Now < until)
        {
            var p = Process.GetProcessesByName(ExeName);
            if (p.Length > 0) return p[0];
            Thread.Sleep(500);
        }
        return null;
    }

    static void Cleanup(string game)
    {
        foreach (var f in LoaderFiles)
        {
            var path = Path.Combine(game, f);
            for (int i = 0; i < 10 && File.Exists(path); i++)
            {
                try { File.Delete(path); } catch { Thread.Sleep(500); }
            }
        }
    }

    static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
        {
            var target = Path.Combine(dst, Path.GetFileName(file));
            // keep the player's own settings
            if (target.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase) && File.Exists(target)) continue;
            File.Copy(file, target, true);
        }
        foreach (var dir in Directory.GetDirectories(src)) CopyDir(dir, Path.Combine(dst, Path.GetFileName(dir)));
    }

    static string FindGame()
    {
        var libs = new System.Collections.Generic.List<string>();
        var steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (steam != null)
        {
            libs.Add(steam.Replace('/', '\\'));
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
        }
        foreach (var lib in libs)
        {
            var dir = Path.Combine(lib, "steamapps", "common", "Getting Over It");
            if (File.Exists(Path.Combine(dir, ExeName + ".exe"))) return dir;
        }
        return null;
    }

    static void Say(string s) { Console.WriteLine(s); }

    static int Fail(string s)
    {
        Console.WriteLine();
        Console.WriteLine(s);
        Console.WriteLine();
        Console.WriteLine("Press any key to close.");
        try { Console.ReadKey(true); } catch { }
        return 1;
    }
}
