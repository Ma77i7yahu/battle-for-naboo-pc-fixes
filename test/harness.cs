// Drives the installer form without showing it: points it at a test folder, ticks options,
// and compares Build() with the hand-patched exes. Usage: harness.exe <installer.exe> <testdir> <data_pc with tested exes>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

class Harness {
    static Type T; static object F;
    static object Get(string n) { return T.GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(F); }
    static object Call(string n, params object[] a) { return T.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(F, a); }
    static void Opt(string key, bool v) { Call("Set", key, v); }
    static void Res(int w, int h) {
        var list = (List<int[]>)Get("resList");
        ((ComboBox)Get("res")).SelectedIndex = list.FindIndex(r => r[0] == w && r[1] == h);
    }
    static int fails;
    static void Check(string what, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    [STAThread]
    static int Main(string[] a) {
        var asm = Assembly.LoadFrom(a[0]);
        T = asm.GetType("InstallerForm");
        F = T.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null).Invoke(null);
        ((TextBox)Get("folder")).Text = a[1];
        Call("LoadFolder");
        Console.WriteLine("status: " + ((Label)Get("status")).Text);
        Console.WriteLine("res items: " + string.Join(" | ", ((ComboBox)Get("res")).Items.Cast<object>()));
        var variants = new[] { new object[] { "16x9", 1920, 1080 }, new object[] { "16x10", 1920, 1200 }, new object[] { "21x9", 2560, 1080 }, new object[] { "32x9", 5120, 1440 } };
        foreach (var v in variants) {
            foreach (var k in new[] { "widescreen", "aimassist", "platinum", "highfps", "mquick", "msilent" }) Opt(k, true);
            Res((int)v[1], (int)v[2]);
            var img = (byte[])Call("Build", new List<string>());
            // tested exes = all options + ground convergence (not packaged)
            Check("all options " + v[0], img.SequenceEqual(File.ReadAllBytes(Path.Combine(a[2], "bfn_widescreen_" + v[0] + ".before-ground-convergence.exe"))));
            Opt("msilent", false);
            img = (byte[])Call("Build", new List<string>());
            Check("all but msilent " + v[0], img.SequenceEqual(File.ReadAllBytes(Path.Combine(a[2], "bfn_widescreen_" + v[0] + ".before-mission-silent.exe"))));
            Opt("mquick", false);
            img = (byte[])Call("Build", new List<string>());
            Check("all but menus " + v[0], img.SequenceEqual(File.ReadAllBytes(Path.Combine(a[2], "bfn_widescreen_" + v[0] + ".before-mission-wrap.exe"))));
            Opt("highfps", false);
            img = (byte[])Call("Build", new List<string>());
            Check("all but highfps " + v[0], img.SequenceEqual(File.ReadAllBytes(Path.Combine(a[2], "bfn_widescreen_" + v[0] + ".before-fps-fix.exe"))));
            Opt("platinum", false);
            img = (byte[])Call("Build", new List<string>());
            Check("widescreen+aim " + v[0], img.SequenceEqual(File.ReadAllBytes(Path.Combine(a[2], "bfn_widescreen_" + v[0] + ".before-platinum-reqs.exe"))));
        }
        Opt("aimassist", false); Res(1920, 1080);
        Check("widescreen only", ((byte[])Call("Build", new List<string>())).SequenceEqual(File.ReadAllBytes(Path.Combine(a[2], "bfn_before_aimfix.exe"))));
        Opt("widescreen", false);
        Check("nothing = original", ((byte[])Call("Build", new List<string>())).SequenceEqual(File.ReadAllBytes(Path.Combine(a[2], "bfn_original.exe"))));

        // real install: 21:9 at 3440x1440 with everything, then reload and check what it detects
        string dp = Path.Combine(a[1], "data_pc");
        var timer = new Timer { Interval = 300 };
        timer.Tick += (s, e) => CloseDialogs();
        timer.Start();
        foreach (var k in new[] { "widescreen", "aimassist", "platinum", "highfps", "mquick", "msilent", "borvo" }) Opt(k, true);
        Res(3440, 1440);
        Call("DoInstall");
        Check("backup written", File.ReadAllBytes(Path.Combine(dp, "bfn_original.exe")).SequenceEqual(File.ReadAllBytes(Path.Combine(a[2], "bfn_original.exe"))));
        Check("ini set", File.ReadAllText(Path.Combine(dp, @"plugins\ChipResolution.ini")).Contains("Screen=3440x1440"));
        Check("borvo raised", (int)Get("borvo") == 1);
        Check("reload ticks", new[] { "widescreen", "aimassist", "platinum", "highfps", "mquick", "msilent", "borvo" }.All(k => (bool)Call("On", k)));
        var cb = (ComboBox)Get("res");
        Check("reload resolution = " + cb.SelectedItem, ((string)cb.SelectedItem).StartsWith("3440 x 1440"));
        // change: platinum + highfps off, 16:9 1920x1080, borvo off -> should equal the tested widescreen+aim 16:9 exe
        Opt("platinum", false); Opt("highfps", false); Opt("mquick", false); Opt("msilent", false); Opt("borvo", false); Res(1920, 1080);
        Call("DoInstall");
        Check("rebuilt from backup", File.ReadAllBytes(Path.Combine(dp, "bfn.exe")).SequenceEqual(File.ReadAllBytes(Path.Combine(a[2], "bfn_widescreen_16x9.before-platinum-reqs.exe"))));
        Check("borvo stock again", (int)Get("borvo") == 0);
        Check("platinum unticked", !(bool)Call("On", "platinum"));
        Check("highfps unticked", !(bool)Call("On", "highfps"));
        // highfps alone on the original exe, reloaded
        Opt("widescreen", false); Opt("aimassist", false); Opt("highfps", true);
        Call("DoInstall");
        Check("highfps alone detected", (bool)Call("On", "highfps") && !(bool)Call("On", "widescreen") && !(bool)Call("On", "aimassist"));
        Opt("borvo", true); Call("DoInstall");
        Call("DoRestore");
        Check("restored exe", File.ReadAllBytes(Path.Combine(dp, "bfn.exe")).SequenceEqual(File.ReadAllBytes(Path.Combine(a[2], "bfn_original.exe"))));
        Check("restored borvo", (int)Get("borvo") == 0);
        timer.Stop();
        return fails;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnumThreadWindows(int tid, EnumProc f, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr c, string cls, string t);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern int GetCurrentThreadId();
    // answers every message box: logs its text, Yes/OK
    static void CloseDialogs() {
        EnumThreadWindows(GetCurrentThreadId(), (h, l) => {
            var sb = new System.Text.StringBuilder(256);
            GetClassName(h, sb, 256);
            if (sb.ToString() != "#32770") return true;
            var txt = FindWindowEx(h, IntPtr.Zero, "Static", null);
            txt = FindWindowEx(h, txt, "Static", null);   // first Static is the icon
            var m = new System.Text.StringBuilder(2048);
            GetWindowText(txt, m, 2048);
            Console.WriteLine("  [dialog] " + m.ToString().Replace("\r\n", " / "));
            PostMessage(h, 0x0111, (IntPtr)6, IntPtr.Zero);   // WM_COMMAND IDYES
            PostMessage(h, 0x0111, (IntPtr)1, IntPtr.Zero);   // WM_COMMAND IDOK
            return true;
        }, IntPtr.Zero);
    }
}
