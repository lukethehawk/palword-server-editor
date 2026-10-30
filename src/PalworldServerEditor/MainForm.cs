using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PalworldServerEditor
{
    sealed class MainForm : Form
    {
        const string AppName = "Palworld Server Editor";
        const string BackupFolderSuffix = " - backup";
        const int BackupsToKeep = 15;

        sealed class SectionView
        {
            public SectionDef Def;
            public SectionHeader Header;
            public StackPanel Card;
            public NavItem Nav;
            public readonly List<SettingRow> Rows = new List<SettingRow>();
        }

        readonly ToolTip tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 400, ReshowDelay = 100 };
        readonly List<SectionView> sections = new List<SectionView>();
        readonly List<SettingRow> rows = new List<SettingRow>();

        SettingsFile file;

        // Sidebar
        TextBox searchBox;
        StackPanel navList;

        // Content
        Label statusLabel, changesLabel;
        string toolbarTitle = "Nessun file aperto", toolbarSubtitle = "";
        FlatButton openButton, reloadButton, folderButton, saveButton;
        Panel toolbar, emptyState;
        ScrollPanel scroll;
        StackPanel page;
        Label noResults;

        readonly Timer navTimer = new Timer { Interval = 150 };
        readonly Timer searchTimer = new Timer { Interval = 200 };
        int lastScrollY = -1, programmaticScrollY = -1;

        public MainForm(string pathFromCommandLine)
        {
            Text = AppName;
            Font = Theme.Ui;
            BackColor = Theme.Background;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Theme.S(1080), Theme.S(760));
            MinimumSize = new Size(Theme.S(860), Theme.S(560));
            KeyPreview = true;
            AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            BuildLayout();

            navTimer.Tick += (s, e) => UpdateActiveNav();
            searchTimer.Tick += (s, e) => { searchTimer.Stop(); ApplyFilter(); };
            Shown += (s, e) => OpenInitialFile(pathFromCommandLine);
        }

        bool HasChanges => rows.Any(r => r.IsModified);

        #region Layout

        void BuildLayout()
        {
            SuspendLayout();

            // ---- Content area (fill) ----
            var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };

            scroll = new ScrollPanel { Dock = DockStyle.Fill, BackColor = Theme.Background, Visible = false };
            page = new StackPanel { Dock = DockStyle.Top, BackColor = Theme.Background };
            scroll.Controls.Add(page);
            scroll.Resize += (s, e) => UpdatePagePadding();

            emptyState = BuildEmptyState();

            var status = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(30), BackColor = Theme.Card };
            status.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, status.Width, 0); };
            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.Muted,
                Font = Theme.Small,
                AutoEllipsis = true,
                Padding = new Padding(Theme.S(14), 0, 0, 0),
                Text = "Apri il file PalWorldSettings.ini per iniziare.",
            };
            changesLabel = new Label
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Theme.Modified,
                Font = Theme.UiSemibold,
                Padding = new Padding(0, Theme.S(6), Theme.S(14), 0),
            };
            status.Controls.Add(statusLabel);
            status.Controls.Add(changesLabel);

            toolbar = BuildToolbar();

            content.Controls.Add(scroll);
            content.Controls.Add(emptyState);
            content.Controls.Add(status);
            content.Controls.Add(toolbar);

            Controls.Add(content);
            Controls.Add(BuildSidebar());

            ResumeLayout(true);
        }

        Control BuildSidebar()
        {
            var sidebar = new Panel { Dock = DockStyle.Left, Width = Theme.S(230), BackColor = Theme.Sidebar };

            var header = new Panel { Dock = DockStyle.Top, Height = Theme.S(84), BackColor = Theme.Sidebar };
            header.Controls.Add(new Label
            {
                Text = "PALWORLD",
                ForeColor = Theme.Accent,
                Font = Theme.UiSemibold,
                AutoSize = true,
                Location = new Point(Theme.S(20), Theme.S(18)),
            });
            header.Controls.Add(new Label
            {
                Text = "Server Editor",
                ForeColor = Color.White,
                Font = Theme.AppTitle,
                AutoSize = true,
                Location = new Point(Theme.S(18), Theme.S(18) + Theme.UiSemibold.Height),
            });

            var searchHost = new Panel { Dock = DockStyle.Top, Height = Theme.S(54), Padding = new Padding(Theme.S(14), Theme.S(4), Theme.S(14), Theme.S(14)), BackColor = Theme.Sidebar };
            var searchBg = new Panel { Dock = DockStyle.Fill, BackColor = Theme.SearchBg, Padding = new Padding(Theme.S(8), 0, Theme.S(8), 0) };
            var searchIcon = new Label
            {
                Text = Theme.SearchGlyph,
                Font = Theme.Icons,
                ForeColor = Theme.SidebarMuted,
                Dock = DockStyle.Left,
                Width = Theme.S(22),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            searchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.SearchBg,
                ForeColor = Color.White,
                Font = Theme.Ui,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
            };
            Theme.SetCueBanner(searchBox, "Cerca impostazione…  (Ctrl+F)");
            searchBox.TextChanged += (s, e) => { searchTimer.Stop(); searchTimer.Start(); };
            searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { searchBox.Clear(); e.SuppressKeyPress = true; }
                if (e.KeyCode == Keys.Enter) { searchTimer.Stop(); ApplyFilter(); e.SuppressKeyPress = true; }
            };
            searchBg.Resize += (s, e) =>
            {
                searchBox.Width = searchBg.ClientSize.Width - Theme.S(22) - searchBg.Padding.Horizontal;
                searchBox.Location = new Point(searchBg.Padding.Left + Theme.S(22), (searchBg.ClientSize.Height - searchBox.Height) / 2);
            };
            searchBg.Click += (s, e) => searchBox.Focus();
            searchBg.Controls.Add(searchBox);
            searchBg.Controls.Add(searchIcon);
            searchHost.Controls.Add(searchBg);

            navList = new StackPanel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar };

            var footer = new Label
            {
                Dock = DockStyle.Bottom,
                Height = Theme.S(54),
                ForeColor = Theme.SidebarMuted,
                Font = Theme.Small,
                Padding = new Padding(Theme.S(20), 0, Theme.S(10), Theme.S(10)),
                TextAlign = ContentAlignment.BottomLeft,
                Text = "Ctrl+S salva   ·   F5 ricarica\nv" + Application.ProductVersion,
            };

            sidebar.Controls.Add(navList);
            sidebar.Controls.Add(footer);
            sidebar.Controls.Add(searchHost);
            sidebar.Controls.Add(header);
            return sidebar;
        }

        Panel BuildToolbar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = Theme.S(70), BackColor = Theme.Card };
            int textRight = 0;
            bar.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                using (var p = new Pen(Theme.Border))
                    g.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1);
                int pad = Theme.S(20);
                int nameH = Theme.Section.Height, pathH = Theme.Small.Height;
                int ty = (bar.Height - nameH - pathH) / 2;
                int w = Math.Max(0, textRight - pad);
                TextRenderer.DrawText(g, toolbarTitle, Theme.Section, new Rectangle(pad - Theme.S(1), ty, w, nameH), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(g, toolbarSubtitle, Theme.Small, new Rectangle(pad, ty + nameH, w, pathH), Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            };


            openButton = new FlatButton("Apri…");
            reloadButton = new FlatButton("Ricarica");
            folderButton = new FlatButton("Cartella");
            saveButton = new FlatButton("Salva modifiche");

            openButton.Click += (s, e) => PickFile();
            reloadButton.Click += (s, e) => Reload();
            folderButton.Click += (s, e) => ShowInExplorer();
            saveButton.Click += (s, e) => Save();

            SetToolbarTips();

            bar.Controls.AddRange(new Control[] { openButton, reloadButton, folderButton, saveButton });
            bar.Layout += (s, e) =>
            {
                int pad = Theme.S(20), gap = Theme.S(8);
                int x = bar.ClientSize.Width - pad;
                int by = (bar.Height - saveButton.Height) / 2;
                foreach (Button b in new Button[] { saveButton, folderButton, reloadButton, openButton })
                {
                    x -= b.Width;
                    b.Location = new Point(x, by);
                    x -= gap;
                }
                textRight = x - Theme.S(8);
                bar.Invalidate();
            };
            UpdateToolbarState();
            return bar;
        }

        Panel BuildEmptyState()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            var title = new Label
            {
                Text = "Apri il tuo PalWorldSettings.ini",
                Font = Theme.Section,
                ForeColor = Theme.Text,
                AutoSize = true,
            };
            var text = new Label
            {
                Text = "Trascina qui il file, oppure sceglilo dal disco.\n" +
                       "Il file usato dal server si trova in PalServer\\Pal\\Saved\\Config\\WindowsServer.",
                ForeColor = Theme.Muted,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            var button = new FlatButton("Apri file…", true);
            button.Click += (s, e) => PickFile();
            panel.Controls.AddRange(new Control[] { title, text, button });
            panel.Layout += (s, e) =>
            {
                int cx = panel.ClientSize.Width / 2;
                int total = title.Height + Theme.S(8) + text.Height + Theme.S(20) + button.Height;
                int y = Math.Max(Theme.S(20), (panel.ClientSize.Height - total) / 2 - Theme.S(30));
                title.Location = new Point(cx - title.Width / 2, y);
                text.Location = new Point(cx - text.Width / 2, title.Bottom + Theme.S(8));
                button.Location = new Point(cx - button.Width / 2, text.Bottom + Theme.S(20));
            };
            return panel;
        }

        void UpdatePagePadding()
        {
            // Keep lines readable on wide screens: the content is at most ~980px wide and centred.
            int max = Theme.S(980);
            int side = Theme.S(28) + Math.Max(0, scroll.ClientSize.Width - Theme.S(56) - max) / 2;
            var padding = new Padding(side, Theme.S(6), side, Theme.S(36));
            if (page.Padding != padding)
                page.Padding = padding;
        }

        #endregion

        #region Building the page

        void BuildPage()
        {
            scroll.SuspendLayout();
            page.SuspendLayout();
            navList.SuspendLayout();

            foreach (Control c in page.Controls.Cast<Control>().ToList())
                c.Dispose();
            page.Controls.Clear();
            foreach (Control c in navList.Controls.Cast<Control>().ToList())
                c.Dispose();
            navList.Controls.Clear();
            sections.Clear();
            rows.Clear();
            tip.RemoveAll();
            SetToolbarTips();

            if (file.LooksLikeSample)
                page.Controls.Add(BuildSampleBanner());

            var present = new HashSet<string>(file.Entries.Select(e => e.Key), StringComparer.OrdinalIgnoreCase);
            foreach (SectionDef sd in Catalog.Sections)
            {
                var view = new SectionView { Def = sd };
                foreach (SettingDef def in Catalog.Settings.Where(d => d.Section == sd.Id))
                {
                    if (file.TryGet(def.Key, out string raw))
                        view.Rows.Add(new SettingRow(def, raw, tip));
                }
                if (sd.Id == Catalog.Advanced)
                {
                    foreach (var kv in file.Entries.Where(e => !Catalog.IsDefined(e.Key)))
                        view.Rows.Add(new SettingRow(Catalog.Infer(kv.Key, kv.Value), kv.Value, tip));
                }
                if (view.Rows.Count == 0)
                    continue;

                view.Header = new SectionHeader(sd.Title, sd.Subtitle) { Margin = new Padding(0, sections.Count == 0 ? Theme.S(14) : Theme.S(30), 0, Theme.S(10)) };
                view.Card = new StackPanel { BackColor = Theme.Border, Padding = new Padding(1, 1, 1, 0) };
                view.Card.SuspendLayout();
                foreach (SettingRow row in view.Rows)
                {
                    row.ValueChanged += (s, e) => OnRowChanged();
                    view.Card.Controls.Add(row);
                    rows.Add(row);
                }
                view.Card.ResumeLayout(false);

                page.Controls.Add(view.Header);
                page.Controls.Add(view.Card);

                view.Nav = new NavItem(sd.Title);
                SectionView target = view;
                view.Nav.Click += (s, e) => ScrollToSection(target);
                navList.Controls.Add(view.Nav);
                sections.Add(view);
            }

            noResults = new Label
            {
                Text = "Nessuna impostazione corrisponde alla ricerca.",
                ForeColor = Theme.Muted,
                Height = Theme.S(80),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            page.Controls.Add(noResults);
            page.SetShown(noResults, false);

            UpdatePagePadding();
            navList.ResumeLayout(true);
            page.ResumeLayout(true);
            scroll.ResumeLayout(true);
            scroll.AutoScrollPosition = Point.Empty;

            if (searchBox.TextLength > 0)
                ApplyFilter();
            if (sections.Count > 0)
                SetActiveNav(sections[0]);
        }

        Control BuildSampleBanner()
        {
            var banner = new Panel
            {
                BackColor = Theme.WarnBorder,
                Padding = new Padding(1),
                Height = Theme.S(64),
                Margin = new Padding(0, Theme.S(16), 0, 0),
            };
            var label = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.WarnBg,
                ForeColor = Theme.WarnText,
                Padding = new Padding(Theme.S(12), Theme.S(8), Theme.S(12), Theme.S(8)),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Questo file ha l'intestazione del file di esempio. Il server legge solo " +
                       "Pal\\Saved\\Config\\WindowsServer\\PalWorldSettings.ini: dopo aver salvato, " +
                       "copia lì questo file (o il suo contenuto nel pannello del tuo hosting).",
            };
            banner.Controls.Add(label);
            banner.Resize += (s, e) =>
            {
                int textWidth = Math.Max(Theme.S(100), banner.Width - 2 - label.Padding.Horizontal);
                Size needed = TextRenderer.MeasureText(label.Text, label.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak);
                int height = needed.Height + label.Padding.Vertical + 2 + Theme.S(4);
                if (banner.Height != height)
                    banner.Height = height;
            };
            return banner;
        }

        void SetToolbarTips()
        {
            tip.SetToolTip(openButton, "Apri un altro PalWorldSettings.ini (Ctrl+O)");
            tip.SetToolTip(reloadButton, "Rileggi il file dal disco, scartando le modifiche non salvate (F5)");
            tip.SetToolTip(folderButton, "Mostra il file e i backup in Esplora file");
            tip.SetToolTip(saveButton, "Salva nel file e crea un backup della versione precedente (Ctrl+S)");
        }

        #endregion

        #region Search and navigation

        void ApplyFilter()
        {
            if (file == null)
                return;
            string query = searchBox.Text.Trim();
            page.SuspendLayout();
            int total = 0;
            foreach (SectionView s in sections)
            {
                s.Card.SuspendLayout();
                int visible = 0;
                foreach (SettingRow row in s.Rows)
                {
                    bool match = query.Length == 0 || row.Matches(query);
                    s.Card.SetShown(row, match);
                    if (match)
                        visible++;
                }
                s.Card.ResumeLayout(true);
                page.SetShown(s.Header, visible > 0);
                page.SetShown(s.Card, visible > 0);
                navList.SetShown(s.Nav, visible > 0);
                total += visible;
            }
            page.SetShown(noResults, total == 0);
            page.ResumeLayout(true);
            scroll.AutoScrollPosition = Point.Empty;
            lastScrollY = -1;
            UpdateActiveNav();
        }

        void ScrollToSection(SectionView s)
        {
            int y = Math.Max(0, s.Header.Top - Theme.S(4));
            scroll.AutoScrollPosition = new Point(0, y);
            programmaticScrollY = -scroll.AutoScrollPosition.Y;
            SetActiveNav(s);
        }

        void UpdateActiveNav()
        {
            if (sections.Count == 0)
                return;
            int y = -scroll.AutoScrollPosition.Y;
            if (y == lastScrollY)
                return;
            lastScrollY = y;
            if (y == programmaticScrollY)
                return;   // keep the section the user clicked, even if it cannot reach the top
            programmaticScrollY = -1;

            SectionView active = null;
            foreach (SectionView s in sections)
            {
                if (!page.IsShown(s.Header))
                    continue;
                if (active == null || s.Header.Top <= y + Theme.S(90))
                    active = s;
            }
            if (active != null)
                SetActiveNav(active);
        }

        void SetActiveNav(SectionView active)
        {
            foreach (SectionView s in sections)
                s.Nav.Selected = s == active;
        }

        #endregion

        #region State

        void OnRowChanged()
        {
            UpdateChangeIndicators();
        }

        void UpdateChangeIndicators()
        {
            int count = 0;
            foreach (SectionView s in sections)
            {
                int n = s.Rows.Count(r => r.IsModified);
                s.Nav.Badge = n;
                count += n;
            }
            changesLabel.Text = count == 0 ? "" : count == 1 ? "1 modifica non salvata" : count + " modifiche non salvate";
            saveButton.Primary = count > 0;
            string name = file == null ? null : Path.GetFileName(file.FilePath);
            Text = (count > 0 ? "● " : "") + (name == null ? AppName : name + " — " + AppName);
        }

        void UpdateToolbarState()
        {
            bool loaded = file != null;
            reloadButton.Enabled = loaded;
            folderButton.Enabled = loaded;
            saveButton.Enabled = loaded;
            toolbarTitle = loaded ? Path.GetFileName(file.FilePath) : "Nessun file aperto";
            toolbarSubtitle = loaded ? Path.GetDirectoryName(file.FilePath) : "Apri il file o trascinalo nella finestra";
            toolbar?.Invalidate();
            scroll.Visible = loaded;
            emptyState.Visible = !loaded;
        }

        void SetStatus(string text, bool error = false)
        {
            statusLabel.ForeColor = error ? Theme.Error : Theme.Muted;
            statusLabel.Text = text;
        }

        #endregion

        #region Open / reload

        void OpenInitialFile(string fromCommandLine)
        {
            navTimer.Start();
            foreach (string candidate in InitialCandidates(fromCommandLine))
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                {
                    OpenFile(candidate);
                    return;
                }
            }
            SetStatus("Nessun PalWorldSettings.ini trovato accanto al programma o sul Desktop: aprilo con «Apri…».");
        }

        static IEnumerable<string> InitialCandidates(string fromCommandLine)
        {
            yield return fromCommandLine;
            yield return LastFile.Read();
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PalWorldSettings.ini");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PalWorldSettings.ini");
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(profile, "Desktop", "PalWorldSettings.ini");
            yield return Path.Combine(profile, "OneDrive", "Desktop", "PalWorldSettings.ini");
        }

        bool OpenFile(string path)
        {
            SettingsFile loaded;
            try
            {
                loaded = SettingsFile.Load(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Impossibile aprire il file:\n" + path + "\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            Cursor = Cursors.WaitCursor;
            try
            {
                file = loaded;
                UpdateToolbarState();
                BuildPage();
                UpdateChangeIndicators();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
            LastFile.Write(path);
            SetStatus("Caricate " + rows.Count + " impostazioni. Modifica i valori e premi «Salva modifiche».");
            return true;
        }

        void PickFile()
        {
            if (!ConfirmDiscard())
                return;
            using (var dialog = new OpenFileDialog
            {
                Title = "Apri PalWorldSettings.ini",
                Filter = "Impostazioni Palworld (PalWorldSettings*.ini)|PalWorldSettings*.ini|File INI (*.ini)|*.ini|Tutti i file (*.*)|*.*",
                InitialDirectory = file != null
                    ? Path.GetDirectoryName(file.FilePath)
                    : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    OpenFile(dialog.FileName);
            }
        }

        void Reload()
        {
            if (file == null)
                return;
            if (HasChanges && MessageBox.Show(this, "Le modifiche non salvate andranno perse. Ricaricare il file dal disco?",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            int y = -scroll.AutoScrollPosition.Y;
            if (OpenFile(file.FilePath))
            {
                scroll.AutoScrollPosition = new Point(0, y);
                SetStatus("File ricaricato dal disco.");
            }
        }

        void ShowInExplorer()
        {
            if (file == null)
                return;
            try { Process.Start("explorer.exe", "/select,\"" + file.FilePath + "\""); }
            catch (Exception ex) { SetStatus("Impossibile aprire Esplora file: " + ex.Message, true); }
        }

        /// <summary>Asks what to do with unsaved changes. Returns false if the user cancels.</summary>
        bool ConfirmDiscard()
        {
            foreach (SettingRow row in rows)
                row.CommitEdit();
            int count = rows.Count(r => r.IsModified);
            if (count == 0)
                return true;
            DialogResult answer = MessageBox.Show(this,
                (count == 1 ? "C'è 1 modifica non salvata." : "Ci sono " + count + " modifiche non salvate.") + "\n\nVuoi salvarle?",
                AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (answer == DialogResult.Yes)
                return Save();
            return answer == DialogResult.No;
        }

        #endregion

        #region Save

        bool Save()
        {
            if (file == null)
                return false;
            foreach (SettingRow row in rows)
                row.CommitEdit();

            SettingRow invalid = rows.FirstOrDefault(r => r.Validate() != null);
            if (invalid != null)
            {
                RevealRow(invalid);
                MessageBox.Show(this, invalid.Def.Label + ": " + invalid.Validate(), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                invalid.FocusEditor();
                return false;
            }

            List<SettingRow> changed = rows.Where(r => r.IsModified).ToList();
            if (changed.Count == 0)
            {
                SetStatus("Nessuna modifica da salvare.");
                return true;
            }

            string path = file.FilePath;
            SettingsFile onDisk;
            try
            {
                onDisk = SettingsFile.Load(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Impossibile rileggere il file prima di salvare:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            bool changedOutside = onDisk.Text != file.Text;
            if (changedOutside && MessageBox.Show(this,
                    "Il file è stato modificato da un altro programma dopo l'apertura.\n\n" +
                    "Vuoi applicare comunque le tue " + changed.Count + " modifiche alla versione attuale del file? " +
                    "Le altre modifiche esterne verranno mantenute.",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return false;

            var missing = new List<string>();
            foreach (SettingRow row in changed)
            {
                if (!onDisk.Set(row.Def.Key, row.CurrentRaw()))
                    missing.Add(row.Def.Key);
            }

            string backup;
            try
            {
                backup = CreateBackup(path);
                file = onDisk.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Salvataggio non riuscito:\n\n" + ex.Message +
                    "\n\nSe il file è aperto in un altro programma o è di sola lettura, chiudilo e riprova.",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (changedOutside)
            {
                // Show what is really in the file now.
                int y = -scroll.AutoScrollPosition.Y;
                BuildPage();
                scroll.AutoScrollPosition = new Point(0, y);
            }
            else
            {
                foreach (SettingRow row in changed)
                    row.MarkSaved();
            }
            UpdateChangeIndicators();

            int saved = changed.Count - missing.Count;
            string message = "Salvato alle " + DateTime.Now.ToString("HH:mm:ss") + " (" +
                             (saved == 1 ? "1 modifica" : saved + " modifiche") + "). " +
                             (backup != null ? "Backup in «" + Path.GetFileName(Path.GetDirectoryName(backup)) + "». " : "") +
                             "Riavvia il server per applicare.";
            if (missing.Count > 0)
                message += " Non più presenti nel file: " + string.Join(", ", missing) + ".";
            SetStatus(message);
            return true;
        }

        /// <summary>Copies the current file into "PalWorldSettings - backup" next to it and keeps the newest copies.</summary>
        static string CreateBackup(string path)
        {
            if (!File.Exists(path))
                return null;
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            string backupDir = Path.Combine(dir, name + BackupFolderSuffix);
            Directory.CreateDirectory(backupDir);
            string target = Path.Combine(backupDir, name + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ext);
            File.Copy(path, target, true);

            try
            {
                foreach (FileInfo old in new DirectoryInfo(backupDir).GetFiles(name + "_*" + ext)
                             .OrderByDescending(f => f.Name, StringComparer.Ordinal)
                             .Skip(BackupsToKeep))
                    old.Delete();
            }
            catch (Exception) { /* pruning is best effort */ }
            return target;
        }

        void RevealRow(SettingRow row)
        {
            if (searchBox.TextLength > 0)
            {
                searchBox.Clear();
                searchTimer.Stop();
                ApplyFilter();
            }
            scroll.ScrollControlIntoView(row);
        }

        #endregion

        #region Window events

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.S) { Save(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.O) { PickFile(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.F) { searchBox.Focus(); searchBox.SelectAll(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F5) { Reload(); e.SuppressKeyPress = true; }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1)
                e.Effect = DragDropEffects.Copy;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1 && ConfirmDiscard())
                OpenFile(files[0]);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!e.Cancel && !ConfirmDiscard())
                e.Cancel = true;
        }

        #endregion
    }

    /// <summary>Remembers the last opened file (in %APPDATA%\PalworldServerEditor).</summary>
    static class LastFile
    {
        static string StorePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PalworldServerEditor", "lastfile.txt");

        public static string Read()
        {
            try { return File.Exists(StorePath) ? File.ReadAllText(StorePath).Trim() : null; }
            catch (Exception) { return null; }
        }

        public static void Write(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
                File.WriteAllText(StorePath, path);
            }
            catch (Exception) { /* not important */ }
        }
    }
}
