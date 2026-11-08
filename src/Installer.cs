// Star Wars Episode I: Battle for Naboo (PC) mod installer.
// Applies any combination of the optional patches in PatchData.cs to a copy of the
// untouched data_pc\bfn.exe. The original is always kept as "bfn_original.exe", so
// every option can be changed or removed later. The Borvo ceiling is a 4-byte edit
// of data.bin, put back to the stock value when the option is removed.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

class Run {
    public readonly int Offset; public readonly byte[] Old, New;
    public Run(int offset, byte[] old, byte[] nw) { Offset = offset; Old = old; New = nw; }
}

class Patch {
    public readonly string Key; public readonly Run[] Runs;
    public Patch(string key, Run[] runs) { Key = key; Runs = runs; }
    public bool IsIn(byte[] exe) {
        foreach (var r in Runs) {
            if (r.Offset + r.New.Length > exe.Length) return false;
            for (int i = 0; i < r.New.Length; i++) if (exe[r.Offset + i] != r.New[i]) return false;
        }
        return true;
    }
}

class Option {
    public string Key, Title, Info;
    public CheckBox Box;
}

class InstallerForm : Form {
    const string ExeName = "bfn.exe";
    const string BackupName = "bfn_original.exe";
    const string DataName = "data.bin";
    const string IniName = @"plugins\ChipResolution.ini";
    static readonly string[] ExeKeys = { "widescreen", "aimassist", "platinum", "highfps", "mquick", "msilent", "gunsight" };   // options that patch bfn.exe, in build order

    static readonly int[][] Resolutions = {
        new[]{1920,1080}, new[]{2560,1440}, new[]{3840,2160}, new[]{1600,900}, new[]{1366,768}, new[]{1280,720},
        new[]{1920,1200}, new[]{1680,1050}, new[]{2560,1080}, new[]{3440,1440}, new[]{5120,1440} };

    readonly TextBox folder = new TextBox();
    readonly Label status = new Label();
    readonly TextBox info = new TextBox();
    readonly ComboBox res = new ComboBox();
    readonly List<int[]> resList = new List<int[]>();
    readonly List<Option> options = new List<Option>();
    readonly Button install = new Button(), restore = new Button();
    byte[] baseExe;
    string dataDir;
    int borvo;   // 0 = stock, 1 = raised, -1 = data.bin missing or not recognised

    Option Opt(string key) { return options.First(o => o.Key == key); }
    bool On(string key) { return Opt(key).Box.Checked; }
    void Set(string key, bool v) { Opt(key).Box.Checked = v; }

    InstallerForm() {
        Text = "Star Wars Episode I: Battle for Naboo - Mod Installer";
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10);

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
        Controls.Add(root);

        // game folder
        var fl = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
        fl.Controls.Add(new Label { Text = "Game folder:", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });
        folder.Width = 470;
        folder.Margin = new Padding(0, 3, 4, 0);
        folder.Leave += (s, e) => LoadFolder();
        fl.Controls.Add(folder);
        var browse = new Button { Text = "Browse...", AutoSize = true };
        browse.Click += (s, e) => {
            using (var d = new FolderBrowserDialog { Description = "Select the Battle for Naboo folder (the one containing \"data_pc\", or data_pc itself)" }) {
                if (Directory.Exists(folder.Text)) d.SelectedPath = folder.Text;
                if (d.ShowDialog(this) == DialogResult.OK) { folder.Text = d.SelectedPath; LoadFolder(); }
            }
        };
        fl.Controls.Add(browse);
        root.Controls.Add(fl);
        status.AutoSize = true; status.MaximumSize = new Size(640, 0); status.Margin = new Padding(0, 4, 0, 6);
        root.Controls.Add(status);

        var g1 = Group(root, "Widescreen");
        var wsRow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
        Check(wsRow, "widescreen", "Hor+ widescreen",
            "The game hard-codes a 4:3 picture, so at a widescreen resolution everything is stretched sideways and the " +
            "sky dome smears and seems to slide when you turn.\r\n\r\n" +
            "Changes the 3D view to your screen's shape by adding width at the sides (Hor+); the vertical view is the " +
            "same as the original. Includes:\r\n- level culling widened, so distant terrain and water no longer vanish " +
            "at the screen edges\r\n- terrain and water generated across the full width (no jagged missing strips)\r\n" +
            "- the radar map put back inside its ring\r\n\r\n" +
            "Pick your resolution: the patch is built for its aspect ratio. If Chip's resolution plugin is installed " +
            "(data_pc\\plugins\\ChipResolution.ini), the installer also sets the game to that resolution.\r\n\r\n" +
            "Known limits: the 2D HUD and menus are still drawn at 4:3 and stretched.\r\n\r\n" +
            "Status: tested at 1920x1080 (16:9). 16:10, 21:9 and 32:9 were built the same way but not flown.");
        res.DropDownStyle = ComboBoxStyle.DropDownList;
        res.Margin = new Padding(12, 2, 0, 0);
        res.Width = 200;
        wsRow.Controls.Add(new Label { Text = "Resolution:", AutoSize = true, Margin = new Padding(16, 6, 0, 0) });
        wsRow.Controls.Add(res);
        Panel(g1).Controls.Add(wsRow);

        var g2 = Group(root, "Gameplay");
        Check(g2, "aimassist", "Remove laser aim assist from flying craft",
            "Every craft bends its lasers toward any enemy inside a small cone around the nose.\r\n\r\n" +
            "Sets that cone to zero for the flying craft (N-1 starfighter, police cruiser, bomber and the other " +
            "flyers), so their shots go where the crosshair points. Ground and water craft keep their stock aim " +
            "assist, which helps make up for them bobbing on the terrain. Missile and torpedo lock-on are " +
            "unchanged.\r\n\r\nStatus: tested.");
        Check(g2, "gunsight", "Crosshair and laser aim for flying craft (replaces the option above)",
            "The flying craft's crosshair is two blue rings: a big one close to the craft that the lasers never go " +
            "through, and a small one further out. From the chase camera, shots land above the rings at a distance " +
            "and below them up close, and drift up and down as you speed up or slow down.\r\n\r\n" +
            "- The big ring is removed; the crosshair is the small ring only, now in solid orange.\r\n" +
            "- Lasers from the flying craft (N-1 starfighter, police cruiser, bomber and the other flyers) are aimed " +
            "along the camera's line through the centre of the crosshair. When there's a target near the crosshair " +
            "they meet that line at the target's distance, so what's in the crosshair gets hit; with nothing targeted " +
            "they meet it 400 units out, allowing for your own speed.\r\n" +
            "- This is not aim assist: shots never bend towards a target, the target only gives the distance. The " +
            "game's target finder for flyers is widened (3 degrees, 900 units) just for that.\r\n\r\n" +
            "Ground and water craft keep their stock aim and aim assist. Missile and torpedo lock-on are unchanged.\r\n\r\n" +
            "Includes everything the option above does for flying craft, so ticking this one unticks it.\r\n\r\n" +
            "Status: tested in the N-1 starfighter and one other flyer. Unlocked shots at terrain much nearer or further than 400 units " +
            "still land slightly low or high, because the game has no distance for terrain.");
        Check(g2, "borvo", "Higher flight ceiling on Borvo the Hutt",
            "The Borvo the Hutt mission's ceiling is far lower than every other mission's, so you hit an invisible " +
            "lid only just above the ground.\r\n\r\n" +
            "Raises it from -70 to -200 (in this game more negative is higher), in line with the other missions; you can " +
            "now climb about 2.5x as high. This edits 4 bytes of data_pc\\data.bin; removing the option puts the " +
            "stock value back.\r\n\r\nStatus: tested.");
        Check(g2, "highfps", "Fix vehicles stuck at a standstill above 60 fps",
            "Above 60 fps your speeder or ship can sit still at full throttle, sometimes for 10 seconds or more, and " +
            "only gets going after a stutter. The game zeroes any velocity that would move a vehicle less than 0.001 " +
            "units in one frame, and at high frame rates the first push of thrust is smaller than that.\r\n\r\n" +
            "Above about 61 fps that cut-off is skipped; at 60 fps and below the game is unchanged. With this on you " +
            "no longer need to cap the game at 60 (in Chip's port, set OverrideRefreshRate = 0 in " +
            "data_pc\\plugins\\dxwrapper.ini to run at your monitor's refresh rate).\r\n\r\nStatus: tested at 119 fps.");
        var g3 = Group(root, "Menus");
        Check(g3, "platinum", "Show the platinum requirements once you have gold",
            "After a mission, the stock game only shows the \"NEEDED FOR ...\" column while your best medal is below " +
            "gold, so there's no way to see what platinum needs.\r\n\r\n" +
            "Once you hold gold or platinum on a mission, it now shows the platinum requirements under a \"NEEDED FOR " +
            "PLATINUM\" heading.\r\n\r\nStatus: tested.");
        Check(g3, "mquick", "Mission Select: one mission per key press",
            "Mission Select opens on your latest mission, and each step plays a 0.8-second slide during which the game " +
            "ignores Left and Right, so getting back to mission 1 takes a while.\r\n\r\n" +
            "Now every press moves one mission straight away, even while a slide is still playing, so you can tap " +
            "through the list quickly. Holding a key still moves only once.\r\n\r\nStatus: tested.");
        Check(g3, "msilent", "Mission Select: no sound when changing mission",
            "Removes the page-turn sound that plays on every step through the mission list. The sound at either end of " +
            "the list is kept.\r\n\r\nStatus: not yet tried in game.");

        info.Multiline = true; info.ReadOnly = true; info.ScrollBars = ScrollBars.Vertical;
        info.Size = new Size(640, 150); info.BackColor = SystemColors.Info;
        info.Text = "Point at an option to see what it does.";
        root.Controls.Add(info);

        var bl = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 8, 0, 0) };
        install.Text = "Install selected"; install.AutoSize = true;
        install.Click += (s, e) => DoInstall();
        restore.Text = "Restore original game"; restore.AutoSize = true;
        restore.Click += (s, e) => DoRestore();
        var close = new Button { Text = "Close", AutoSize = true };
        close.Click += (s, e) => Close();
        bl.Controls.AddRange(new Control[] { install, restore, close });
        root.Controls.Add(bl);

        Opt("widescreen").Box.CheckedChanged += (s, e) => UpdateEnabled();
        Opt("gunsight").Box.CheckedChanged += (s, e) => UpdateEnabled();

        string here = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        if (FindDataDir(here) != null) folder.Text = here;
        LoadFolder();
    }

    GroupBox Group(Control parent, string title) {
        var g = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Width = 640, MinimumSize = new Size(640, 0), Padding = new Padding(8, 4, 8, 6) };
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Location = new Point(8, 20) };
        g.Controls.Add(p);
        parent.Controls.Add(g);
        return g;
    }
    Control Panel(Control g) { return g is GroupBox ? g.Controls[0] : g; }

    void Check(Control g, string key, string title, string text) {
        var c = new CheckBox { Text = title, AutoSize = true };
        Panel(g).Controls.Add(c);
        var o = new Option { Key = key, Title = title, Info = text, Box = c };
        c.MouseEnter += (s, e) => info.Text = o.Title + "\r\n\r\n" + o.Info;
        c.GotFocus += (s, e) => info.Text = o.Title + "\r\n\r\n" + o.Info;
        options.Add(o);
    }
    void UpdateEnabled() {
        bool ok = baseExe != null;
        res.Enabled = ok && On("widescreen");
        Opt("borvo").Box.Enabled = ok && borvo >= 0;
        // the gunsight option rewrites the same flyer cone table as the aim-assist option
        if (On("gunsight")) Set("aimassist", false);
        Opt("aimassist").Box.Enabled = ok && !On("gunsight");
    }

    static string Md5(byte[] b) {
        using (var m = MD5.Create()) return BitConverter.ToString(m.ComputeHash(b)).Replace("-", "").ToLowerInvariant();
    }
    static Patch PatchFor(string key) { return PatchData.Groups.First(p => p.Key == key); }

    // accepts the install folder or data_pc itself
    static string FindDataDir(string dir) {
        if (dir.Length == 0) return null;
        if (File.Exists(Path.Combine(dir, ExeName))) return dir;
        string d = Path.Combine(dir, "data_pc");
        return File.Exists(Path.Combine(d, ExeName)) ? d : null;
    }

    // widescreen float immediate i for a w x h screen (stock value x aspect / (4/3))
    static float WsFloat(int i, int w, int h) {
        return (float)(PatchData.WsFloatStock[i] * ((double)w / h) / (4.0 / 3.0));
    }

    // ChipResolution.ini "Screen=WxH", or null
    int[] IniResolution() {
        string ini = Path.Combine(dataDir, IniName);
        if (!File.Exists(ini)) return null;
        foreach (var line in File.ReadAllLines(ini)) {
            var t = line.Trim();
            if (!t.StartsWith("Screen=", StringComparison.OrdinalIgnoreCase)) continue;
            var p = t.Substring(7).Split('x', 'X');
            int w, h;
            if (p.Length == 2 && int.TryParse(p[0].Trim(), out w) && int.TryParse(p[1].Trim(), out h) && w > 0 && h > 0) return new[] { w, h };
        }
        return null;
    }
    bool WriteIniResolution(int w, int h) {
        string ini = Path.Combine(dataDir, IniName);
        if (!File.Exists(ini)) return false;
        var lines = File.ReadAllLines(ini).ToList();
        int i = lines.FindIndex(l => l.Trim().StartsWith("Screen=", StringComparison.OrdinalIgnoreCase));
        if (i >= 0) lines[i] = "Screen=" + w + "x" + h;
        else {
            int sec = lines.FindIndex(l => l.Trim().Equals("[BattleForNaboo]", StringComparison.OrdinalIgnoreCase));
            if (sec < 0) { lines.Add("[BattleForNaboo]"); sec = lines.Count - 1; }
            lines.Insert(sec + 1, "Screen=" + w + "x" + h);
        }
        File.WriteAllLines(ini, lines.ToArray());
        return true;
    }

    // Borvo ceiling float in data.bin: 0 stock, 1 raised, -1 not recognised
    int ReadBorvo() {
        string p = Path.Combine(dataDir, DataName);
        if (!File.Exists(p)) return -1;
        try {
            using (var fs = File.OpenRead(p)) {
                if (fs.Length < PatchData.BorvoOffset + 0x24) return -1;
                var b = new byte[0x24];
                fs.Position = PatchData.BorvoOffset;
                if (fs.Read(b, 0, b.Length) != b.Length) return -1;
                if (Encoding.ASCII.GetString(b, 0x10, 20) != "player_StartPosition" || BitConverter.ToSingle(b, 4) != 99999f) return -1;
                float v = BitConverter.ToSingle(b, 0);
                return v == PatchData.BorvoStock ? 0 : v == PatchData.BorvoNew ? 1 : -1;
            }
        } catch { return -1; }
    }
    void WriteBorvo(bool raised) {
        using (var fs = new FileStream(Path.Combine(dataDir, DataName), FileMode.Open, FileAccess.ReadWrite)) {
            fs.Position = PatchData.BorvoOffset;
            fs.Write(BitConverter.GetBytes(raised ? PatchData.BorvoNew : PatchData.BorvoStock), 0, 4);
        }
    }

    void FillResolutions(int[] current, int[] ini) {
        res.Items.Clear(); resList.Clear();
        var all = Resolutions.ToList();
        foreach (var extra in new[] { ini, current })
            if (extra != null && !all.Any(r => r[0] == extra[0] && r[1] == extra[1])) all.Insert(0, extra);
        foreach (var r in all) {
            resList.Add(r);
            int g = Gcd(r[0], r[1]);
            string aspect = r[0] / g + ":" + r[1] / g;
            if (aspect == "8:5") aspect = "16:10";
            else if (aspect == "64:27" || aspect == "43:18") aspect = "21:9";
            else if (aspect == "683:384") aspect = "16:9";
            res.Items.Add(r[0] + " x " + r[1] + "  (" + aspect + (r[0] == 1920 && r[1] == 1080 ? ", tested)" : ")"));
        }
        var pick = current ?? ini ?? Resolutions[0];
        res.SelectedIndex = resList.FindIndex(r => r[0] == pick[0] && r[1] == pick[1]);
    }
    static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return a; }

    void LoadFolder() {
        baseExe = null; dataDir = null; borvo = -1;
        string dir = folder.Text.Trim().Trim('"');
        install.Enabled = restore.Enabled = false;
        foreach (var o in options) o.Box.Enabled = false;
        res.Enabled = false;
        dataDir = FindDataDir(dir);
        if (dataDir == null) {
            status.ForeColor = Color.DarkRed;
            status.Text = dir.Length == 0 ? "Choose the folder where Battle for Naboo is installed."
                                          : "\"data_pc\\" + ExeName + "\" was not found in that folder.";
            return;
        }
        string exe = Path.Combine(dataDir, ExeName), bak = Path.Combine(dataDir, BackupName);
        byte[] cur;
        try { cur = File.ReadAllBytes(exe); }
        catch (Exception ex) { status.ForeColor = Color.DarkRed; status.Text = "Can't read the exe: " + ex.Message; return; }

        if (Md5(cur) == PatchData.OriginalMd5) {
            baseExe = cur;
            status.ForeColor = Color.DarkGreen;
            status.Text = "Found the original Battle for Naboo exe. It will be backed up as \"" + BackupName + "\" before patching.";
        } else if (File.Exists(bak) && Md5(File.ReadAllBytes(bak)) == PatchData.OriginalMd5) {
            baseExe = File.ReadAllBytes(bak);
            status.ForeColor = Color.DarkGreen;
            status.Text = "The exe is already modified. The installer will rebuild it from your backup \"" + BackupName + "\". The ticked options show what's installed now.";
        } else {
            status.ForeColor = Color.DarkRed;
            status.Text = "This bfn.exe isn't the unmodified Battle for Naboo exe this installer was made for (MD5 " + PatchData.OriginalMd5.ToUpper() +
                          "), and there's no matching \"" + BackupName + "\". Restore the original exe, then try again.";
            return;
        }
        borvo = ReadBorvo();
        foreach (var o in options) o.Box.Enabled = true;
        install.Enabled = true;
        bool clean = ReferenceEquals(baseExe, cur);
        restore.Enabled = !clean || borvo == 1;

        // reflect what is installed now (defaults for a clean exe)
        foreach (var k in ExeKeys) Set(k, clean || PatchFor(k).IsIn(cur));
        Set("borvo", borvo == 1 || (clean && borvo == 0));
        int[] current = null, ini = IniResolution();
        if (!clean && PatchFor("widescreen").IsIn(cur)) {
            // the installed aspect; prefer the ini's resolution when it has the same shape
            float a = BitConverter.ToSingle(cur, PatchData.WsFloatOffsets[0]);
            Func<int[], bool> same = r => r != null && WsFloat(0, r[0], r[1]) == a;
            current = same(ini) ? ini : Resolutions.FirstOrDefault(r => same(r));
        }
        FillResolutions(current, ini);
        if (borvo < 0) status.Text += "\r\n\r\n\"data_pc\\" + DataName + "\" wasn't recognised, so the Borvo ceiling option is unavailable.";
        UpdateEnabled();
    }

    byte[] Build(List<string> applied) {
        var img = (byte[])baseExe.Clone();
        foreach (var k in ExeKeys) {
            if (!On(k)) continue;
            if (k == "aimassist" && On("gunsight")) continue;   // superseded (same cone table)
            foreach (var r in PatchFor(k).Runs) {
                for (int i = 0; i < r.Old.Length; i++)
                    if (img[r.Offset + i] != r.Old[i]) throw new InvalidOperationException("Unexpected bytes at 0x" + r.Offset.ToString("X") + " for " + k);
                Buffer.BlockCopy(r.New, 0, img, r.Offset, r.New.Length);
            }
            string t = Opt(k).Title;
            if (k == "widescreen") {
                var rs = resList[res.SelectedIndex];
                for (int i = 0; i < PatchData.WsFloatOffsets.Length; i++)
                    Buffer.BlockCopy(BitConverter.GetBytes(WsFloat(i, rs[0], rs[1])), 0, img, PatchData.WsFloatOffsets[i], 4);
                t += " for " + rs[0] + " x " + rs[1];
            }
            applied.Add(t);
        }
        return img;
    }

    void Write(byte[] img, bool raiseBorvo, string done, int[] resolution) {
        string exe = Path.Combine(dataDir, ExeName), bak = Path.Combine(dataDir, BackupName);
        try {
            // a file already using the backup name that isn't the original exe is moved aside, never trusted or overwritten
            if (File.Exists(bak) && Md5(File.ReadAllBytes(bak)) != PatchData.OriginalMd5) {
                string old = Path.Combine(dataDir, "bfn_original-old.exe");
                for (int i = 2; File.Exists(old); i++) old = Path.Combine(dataDir, "bfn_original-old" + i + ".exe");
                File.Move(bak, old);
                done += "\r\n\r\nThe file that was already called \"" + BackupName + "\" wasn't the original exe, so it was renamed to \"" +
                        Path.GetFileName(old) + "\".";
            }
            if (!File.Exists(bak)) File.WriteAllBytes(bak, baseExe);
            File.WriteAllBytes(exe + ".tmp", img);
            File.Copy(exe + ".tmp", exe, true);
            File.Delete(exe + ".tmp");
            if (borvo >= 0 && (borvo == 1) != raiseBorvo) WriteBorvo(raiseBorvo);
            if (resolution != null) {
                if (WriteIniResolution(resolution[0], resolution[1]))
                    done += "\r\n\r\nThe game's resolution was set to " + resolution[0] + " x " + resolution[1] + " in \"data_pc\\" + IniName + "\".";
                else
                    done += "\r\n\r\nChip's resolution plugin (\"data_pc\\" + IniName + "\") wasn't found, so set the game to " +
                            resolution[0] + " x " + resolution[1] + " yourself (see the README).";
            }
        } catch (IOException ex) {
            try { File.Delete(exe + ".tmp"); } catch { }
            MessageBox.Show(this, "Couldn't write the game files. Close Battle for Naboo if it's running and try again.\r\n\r\n" + ex.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        } catch (UnauthorizedAccessException ex) {
            MessageBox.Show(this, "Windows denied access to the game folder. Right-click the installer and choose \"Run as administrator\".\r\n\r\n" + ex.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        MessageBox.Show(this, done, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        LoadFolder();
    }

    void DoInstall() {
        if (baseExe == null) return;
        var applied = new List<string>();
        byte[] img;
        try { img = Build(applied); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        bool b = borvo >= 0 && On("borvo");
        if (b) applied.Add(Opt("borvo").Title);
        string msg = applied.Count == 0 ? "No options were selected, so the game is now the original."
                                        : "Installed:\r\n  - " + string.Join("\r\n  - ", applied);
        msg += "\r\n\r\nYour untouched exe is saved as \"data_pc\\" + BackupName + "\".";
        Write(img, b, msg, On("widescreen") ? resList[res.SelectedIndex] : null);
    }

    void DoRestore() {
        if (baseExe == null) return;
        if (MessageBox.Show(this, "Put the original, unmodified exe back (and the stock Borvo ceiling)?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Write(baseExe, false, "The original game has been restored. Every mod is removed.", null);
    }

    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }
}
