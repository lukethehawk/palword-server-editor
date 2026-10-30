using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PalworldServerEditor
{
    /// <summary>One setting: name and description on the left, editor on the right, undo button at the far right.</summary>
    sealed class SettingRow : Panel
    {
        static readonly string[] KnownPlatforms = { "Steam", "Xbox", "PS5", "Mac" };
        const TextFormatFlags TextFlags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        readonly Kind kind;
        readonly string subtitle;
        readonly int editorWidth = Theme.S(260);

        Control editor;
        SafeNumeric numeric;
        ToggleSwitch toggle;
        SafeCombo combo;
        TextBox textBox;
        CheckBox showPassword;
        readonly List<CheckBox> platformBoxes = new List<CheckBox>();
        bool platformTwoLines;
        List<string> platformExtras = new List<string>();

        readonly LinkLabel defaultLink;
        readonly Button undoButton;
        bool ready, modified, defaultShown;
        int textRight;

        public SettingRow(SettingDef def, string raw, ToolTip tip)
        {
            Def = def;
            OriginalRaw = raw;
            kind = EffectiveKind(def, raw);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            Height = Theme.S(60);
            Margin = new Padding(0, 0, 0, 1);   // the 1px gap shows the card colour as a separator

            subtitle = def.Label == def.Key
                ? def.Help ?? ""
                : string.IsNullOrEmpty(def.Help) ? def.Key : def.Key + "  ·  " + def.Help;

            SuspendLayout();
            BuildEditor();

            defaultLink = new LinkLabel
            {
                AutoSize = true,
                Font = Theme.Small,
                LinkColor = Theme.Accent,
                ActiveLinkColor = Theme.AccentPressed,
                LinkBehavior = LinkBehavior.HoverUnderline,
                TabStop = false,
                Visible = false,
            };
            if (def.Default != null)
            {
                string value = DisplayValue(def.Default);
                string text = "Predefinito: " + value;
                defaultLink.Text = TextRenderer.MeasureText(text, Theme.Small).Width > Theme.S(170) ? "Usa predefinito" : text;
                defaultLink.LinkClicked += (s, e) => SetFromRaw(Def.Default);
                tip.SetToolTip(defaultLink, "Imposta il valore predefinito di Palworld: " + value);
            }
            Controls.Add(defaultLink);

            undoButton = new Button
            {
                Text = Theme.UndoGlyph,
                Font = Theme.Icons,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = Theme.Card,
                ForeColor = Theme.Muted,
                TabStop = false,
                Cursor = Cursors.Hand,
                Visible = false,
            };
            undoButton.FlatAppearance.BorderSize = 0;
            undoButton.FlatAppearance.MouseOverBackColor = Theme.Hover;
            undoButton.FlatAppearance.MouseDownBackColor = Theme.Pressed;
            undoButton.Click += (s, e) => SetFromRaw(OriginalRaw);
            Controls.Add(undoButton);

            string tipText = def.Label + (def.Label == def.Key ? "" : "  (" + def.Key + ")") +
                             (string.IsNullOrEmpty(def.Help) ? "" : "\n" + def.Help);
            tip.SetToolTip(this, tipText);
            if (textBox != null)
                tip.SetToolTip(textBox, tipText);
            tip.SetToolTip(undoButton, "Annulla la modifica (torna al valore del file)");

            SetFromRaw(raw);
            ResumeLayout(false);
            ready = true;
            RefreshState();
            PerformLayout();
        }

        public SettingDef Def { get; }

        /// <summary>The value currently in the file.</summary>
        public string OriginalRaw { get; private set; }

        public bool IsModified => modified;

        public event EventHandler ValueChanged;

        /// <summary>Value to write. Untouched values keep their exact original text.</summary>
        public string CurrentRaw()
        {
            string built = BuildRaw();
            return Same(OriginalRaw, built) ? OriginalRaw : built;
        }

        /// <summary>Applies a number still being typed (NumericUpDown validates it when Value is read).</summary>
        public void CommitEdit()
        {
            if (numeric != null)
                _ = numeric.Value;
        }

        public void MarkSaved()
        {
            OriginalRaw = CurrentRaw();
            RefreshState();
        }

        /// <summary>Error message if the current value cannot be saved, otherwise null.</summary>
        public string Validate()
        {
            if (kind == Kind.Platforms && !platformBoxes.Any(b => b.Checked) && platformExtras.Count == 0)
                return "Seleziona almeno una piattaforma.";
            if (kind == Kind.Raw && !IniValue.IsWellFormed(textBox.Text))
                return "Valore non valido: controlla parentesi e virgolette (le virgole sono ammesse solo tra parentesi).";
            return null;
        }

        public bool Matches(string query)
        {
            CompareInfo ci = CultureInfo.CurrentCulture.CompareInfo;
            const CompareOptions opt = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            return ci.IndexOf(Def.Label, query, opt) >= 0 ||
                   ci.IndexOf(Def.Key, query, opt) >= 0 ||
                   (Def.Help != null && ci.IndexOf(Def.Help, query, opt) >= 0);
        }

        public void FocusEditor()
        {
            (textBox ?? editor)?.Focus();
        }

        static Kind EffectiveKind(SettingDef def, string raw)
        {
            switch (def.Kind)
            {
                case Kind.Float:
                case Kind.Int:
                    return IniValue.TryParseNumber(raw, out _) ? def.Kind : Catalog.Infer(def.Key, raw).Kind;
                case Kind.Bool:
                    return IniValue.IsBool(raw) ? def.Kind : Catalog.Infer(def.Key, raw).Kind;
                case Kind.Platforms:
                    return raw.Trim().StartsWith("(") ? def.Kind : Kind.Raw;
                default:
                    return def.Kind;
            }
        }

        void BuildEditor()
        {
            switch (kind)
            {
                case Kind.Float:
                case Kind.Int:
                    numeric = new SafeNumeric
                    {
                        DecimalPlaces = kind == Kind.Int ? 0 : Def.Decimals,
                        Increment = kind == Kind.Int ? 1m : Def.Step,
                        ThousandsSeparator = false,
                        Width = Theme.S(130),
                    };
                    decimal min = Def.Min, max = Def.Max;
                    if (Def.Kind != kind)   // inferred from the value: wide range
                    {
                        min = -1000000000m;
                        max = 1000000000m;
                    }
                    if (IniValue.TryParseNumber(OriginalRaw, out decimal current))
                    {
                        // Never clamp what is already in the file.
                        min = Math.Min(min, current);
                        max = Math.Max(max, current);
                    }
                    numeric.Maximum = max;
                    numeric.Minimum = min;
                    numeric.ValueChanged += (s, e) => OnEdited();
                    editor = numeric;
                    break;

                case Kind.Bool:
                    toggle = new ToggleSwitch { Font = Theme.Ui };
                    toggle.CheckedChanged += (s, e) => OnEdited();
                    editor = toggle;
                    break;

                case Kind.Choice:
                    combo = new SafeCombo { Width = editorWidth, Font = Theme.Ui };
                    combo.Items.AddRange(Def.Choices.Cast<object>().ToArray());
                    combo.SelectedIndexChanged += (s, e) => OnEdited();
                    editor = combo;
                    break;

                case Kind.Secret:
                    textBox = new TextBox { UseSystemPasswordChar = true, Font = Theme.Ui };
                    textBox.TextChanged += (s, e) => OnEdited();
                    textBox.KeyPress += BlockQuotes;
                    showPassword = new CheckBox { Text = "Mostra", AutoSize = true, Font = Theme.Ui, TabStop = false, Cursor = Cursors.Hand };
                    showPassword.CheckedChanged += (s, e) => textBox.UseSystemPasswordChar = !showPassword.Checked;
                    Controls.Add(showPassword);
                    editor = textBox;
                    break;

                case Kind.Platforms:
                    foreach (string p in KnownPlatforms)
                    {
                        var box = new CheckBox { Text = p, AutoSize = true, Font = Theme.Ui, BackColor = Theme.Card, Cursor = Cursors.Hand };
                        box.CheckedChanged += (s, e) => OnEdited();
                        platformBoxes.Add(box);
                        Controls.Add(box);
                    }
                    editor = platformBoxes[0];
                    // One line if the four boxes fit, otherwise a 2x2 grid (large fonts / high DPI).
                    int lineWidth = platformBoxes.Sum(b => b.PreferredSize.Width) + Theme.S(12) * (platformBoxes.Count - 1);
                    platformTwoLines = lineWidth > editorWidth;
                    if (platformTwoLines)
                        Height = Math.Max(Height, platformBoxes[0].PreferredSize.Height * 2 + Theme.S(24));
                    break;

                default: // Text, Raw
                    textBox = new TextBox { Width = editorWidth, Font = Theme.Ui };
                    textBox.TextChanged += (s, e) => OnEdited();
                    if (kind == Kind.Text)
                        textBox.KeyPress += BlockQuotes;
                    editor = textBox;
                    break;
            }
            if (editor.Parent != this)
                Controls.Add(editor);
        }

        static void BlockQuotes(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '"')
                e.Handled = true;
        }

        string BuildRaw()
        {
            switch (kind)
            {
                case Kind.Float: return IniValue.FormatFloat(numeric.Value);
                case Kind.Int: return IniValue.FormatInt(numeric.Value);
                case Kind.Bool: return toggle.Checked ? "True" : "False";
                case Kind.Choice: return (combo.SelectedItem as ChoiceItem)?.Value ?? OriginalRaw;
                case Kind.Text:
                case Kind.Secret: return IniValue.Quote(textBox.Text);
                case Kind.Platforms:
                    var list = KnownPlatforms.Where((p, i) => platformBoxes[i].Checked).Concat(platformExtras);
                    return "(" + string.Join(",", list) + ")";
                default: return textBox.Text.Trim();
            }
        }

        bool Same(string a, string b)
        {
            if (a == null || b == null)
                return a == b;
            switch (kind)
            {
                case Kind.Float:
                case Kind.Int:
                    return IniValue.TryParseNumber(a, out decimal x) && IniValue.TryParseNumber(b, out decimal y)
                        ? x == y
                        : a.Trim() == b.Trim();
                case Kind.Bool:
                    return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
                case Kind.Text:
                case Kind.Secret:
                    return IniValue.Unquote(a) == IniValue.Unquote(b);
                case Kind.Platforms:
                    var pa = IniValue.ParseList(a);
                    var pb = IniValue.ParseList(b);
                    return pa.Count == pb.Count && !pa.Except(pb, StringComparer.OrdinalIgnoreCase).Any();
                default:
                    return a.Trim() == b.Trim();
            }
        }

        void SetFromRaw(string raw)
        {
            switch (kind)
            {
                case Kind.Float:
                case Kind.Int:
                    if (IniValue.TryParseNumber(raw, out decimal d))
                        numeric.Value = Math.Max(numeric.Minimum, Math.Min(numeric.Maximum, d));
                    break;
                case Kind.Bool:
                    toggle.Checked = raw.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
                    break;
                case Kind.Choice:
                    string value = raw.Trim();
                    ChoiceItem item = combo.Items.Cast<ChoiceItem>().FirstOrDefault(c => string.Equals(c.Value, value, StringComparison.OrdinalIgnoreCase));
                    if (item == null)
                    {
                        item = new ChoiceItem(value, value);
                        combo.Items.Add(item);
                    }
                    combo.SelectedItem = item;
                    break;
                case Kind.Text:
                case Kind.Secret:
                    textBox.Text = IniValue.Unquote(raw);
                    break;
                case Kind.Platforms:
                    var values = IniValue.ParseList(raw);
                    for (int i = 0; i < KnownPlatforms.Length; i++)
                        platformBoxes[i].Checked = values.Contains(KnownPlatforms[i], StringComparer.OrdinalIgnoreCase);
                    platformExtras = values.Where(v => !KnownPlatforms.Contains(v, StringComparer.OrdinalIgnoreCase)).ToList();
                    break;
                default:
                    textBox.Text = raw.Trim();
                    break;
            }
            RefreshState();
        }

        string DisplayValue(string raw)
        {
            switch (kind)
            {
                case Kind.Float:
                case Kind.Int:
                    return IniValue.TryParseNumber(raw, out decimal d) ? d.ToString("0.##", CultureInfo.CurrentCulture) : raw;
                case Kind.Bool:
                    return raw.Trim().Equals("True", StringComparison.OrdinalIgnoreCase) ? "Sì" : "No";
                case Kind.Choice:
                    return Def.Choices?.FirstOrDefault(c => c.Value == raw)?.Label ?? raw;
                default:
                    return raw;
            }
        }

        void OnEdited()
        {
            if (!ready)
                return;
            RefreshState();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        void RefreshState()
        {
            if (!ready)
                return;
            string built = BuildRaw();
            bool nowModified = !Same(OriginalRaw, built);
            bool nowDefaultShown = Def.Default != null && kind == Def.Kind && !Same(Def.Default, built);
            bool relayout = nowDefaultShown != defaultShown;
            modified = nowModified;
            defaultShown = nowDefaultShown;
            undoButton.Visible = modified;
            defaultLink.Visible = defaultShown;
            if (relayout)
                PerformLayout();
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (!ready)
                return;
            int w = ClientSize.Width, h = ClientSize.Height;
            int us = Theme.S(30);
            undoButton.SetBounds(w - Theme.S(12) - us, (h - us) / 2, us, us);

            int edLeft = undoButton.Left - Theme.S(8) - editorWidth;
            if (kind == Kind.Secret)
            {
                Size cb = showPassword.PreferredSize;
                textBox.SetBounds(edLeft, (h - textBox.Height) / 2, editorWidth - cb.Width - Theme.S(8), textBox.Height);
                showPassword.SetBounds(textBox.Right + Theme.S(8), (h - cb.Height) / 2, cb.Width, cb.Height);
            }
            else if (kind == Kind.Platforms)
            {
                int bh = platformBoxes[0].PreferredSize.Height, gap = Theme.S(4);
                int x = edLeft, y = platformTwoLines ? (h - bh * 2 - gap) / 2 : (h - bh) / 2;
                for (int i = 0; i < platformBoxes.Count; i++)
                {
                    CheckBox box = platformBoxes[i];
                    Size ps = box.PreferredSize;
                    if (platformTwoLines)
                        box.SetBounds(edLeft + (i % 2) * (editorWidth / 2), y + (i / 2) * (bh + gap), ps.Width, bh);
                    else
                    {
                        box.SetBounds(x, y, ps.Width, bh);
                        x += ps.Width + Theme.S(12);
                    }
                }
            }
            else
            {
                editor.Location = new Point(edLeft, (h - editor.Height) / 2);
            }

            textRight = edLeft - Theme.S(14);
            if (defaultShown)
            {
                Size ls = defaultLink.PreferredSize;
                defaultLink.Location = new Point(textRight - ls.Width, (h - ls.Height) / 2);
                textRight = defaultLink.Left - Theme.S(10);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (modified)
                using (var b = new SolidBrush(Theme.Modified))
                    g.FillRectangle(b, 0, 0, Theme.S(3), Height);

            int x = Theme.S(18);
            int width = Math.Max(0, textRight - x);
            int titleH = Theme.UiSemibold.Height;
            int subH = Theme.Small.Height;
            bool hasSub = subtitle.Length > 0;
            int y = (Height - (hasSub ? titleH + Theme.S(2) + subH : titleH)) / 2;
            TextRenderer.DrawText(g, Def.Label, Theme.UiSemibold, new Rectangle(x, y, width, titleH), Theme.Text, TextFlags);
            if (hasSub)
                TextRenderer.DrawText(g, subtitle, Theme.Small, new Rectangle(x, y + titleH + Theme.S(2), width, subH), Theme.Muted, TextFlags);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            FocusEditor();
        }
    }
}
