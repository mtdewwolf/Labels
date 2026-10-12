using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Text.Json;
using Labels.Core;

namespace Labels.Windows;

internal sealed class MainForm : Form
{
    private readonly TextBox product = new() { Text = "Sample item", MaxLength = 40, Dock = DockStyle.Fill };
    private readonly TextBox barcode = new() { Text = "12345678", MaxLength = 40, Dock = DockStyle.Fill };
    private readonly NumericUpDown price = new() { DecimalPlaces = 2, Maximum = 9999.99m, Value = 9.99m, Dock = DockStyle.Fill };
    private readonly NumericUpDown copies = new() { Minimum = 1, Maximum = PrintList.MaxLineQuantity, Value = 1, Dock = DockStyle.Fill };
    private readonly ComboBox printers = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label message = new() { AutoSize = true, MaximumSize = new Size(380, 0), ForeColor = Color.FromArgb(70, 82, 100) };
    private readonly Label listSummary = new() { AutoSize = true, MaximumSize = new Size(760, 0), ForeColor = Color.FromArgb(70, 82, 100) };
    private readonly LabelPreview preview = new() { Dock = DockStyle.Fill };
    private readonly ListView staged = new()
    {
        View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false,
        MultiSelect = false, Dock = DockStyle.Fill, HeaderStyle = ColumnHeaderStyle.Nonclickable
    };
    private readonly NumericUpDown lineQuantity = new() { Minimum = 1, Maximum = PrintList.MaxLineQuantity, Value = 1, Width = 80 };
    private readonly PrintList job = new();
    private readonly Button print = new() { Text = "Print this label…", AutoSize = true };
    private readonly Button printJob = new() { Text = "Print list…", AutoSize = true, Padding = new Padding(14, 8, 14, 8) };
    private readonly Button addToList = new() { Text = "Add to print list", AutoSize = true, Padding = new Padding(14, 8, 14, 8) };
    private readonly Button export = new() { Text = "Export PNG", AutoSize = true };
    private readonly Button save = new() { Text = "Save label…", AutoSize = true };
    private readonly Button removeLine = new() { Text = "Remove", AutoSize = true };
    private readonly Button clearList = new() { Text = "Clear list", AutoSize = true };
    private readonly Button moveUp = new() { Text = "Move up", AutoSize = true };
    private readonly Button moveDown = new() { Text = "Move down", AutoSize = true };
    private bool adjustingList;

    public MainForm()
    {
        Text = "Labels — Brother QL-810W";
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);
        MinimumSize = new Size(920, 780);
        ClientSize = new Size(1020, 900);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        staged.Columns.Add("Product", 280);
        staged.Columns.Add("Barcode", 180);
        staged.Columns.Add("Price", 90);
        staged.Columns.Add("Qty", 70);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 2, RowCount = 3 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        var heading = new Label { Text = "Create price labels\nBrother QL-810W · DK-1221 · 23 × 23 mm", AutoSize = true, Font = new Font("Segoe UI", 15, FontStyle.Bold) };
        root.Controls.Add(heading, 0, 0);
        root.SetColumnSpan(heading, 2);

        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 0, Padding = new Padding(0, 0, 24, 0) };
        void Add(Control control)
        {
            fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Margin = new Padding(0, 3, 0, 8);
            fields.Controls.Add(control, 0, fields.RowCount - 1);
        }
        void Field(string label, Control control) { Add(new Label { Text = label, AutoSize = true }); Add(control); }
        Field("Product name (optional)", product);
        Field("Barcode value — Code 128", barcode);
        Field("Price (USD)", price);
        Field("Quantity to add", copies);
        Field("Brother printer", printers);
        var refresh = new Button { Text = "Refresh printers", AutoSize = true };
        refresh.Click += (_, _) => RefreshPrinters();
        Add(refresh);
        var files = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var open = new Button { Text = "Open label…", AutoSize = true };
        files.Controls.AddRange([open, save, export]);
        Add(files);
        Add(addToList);
        Add(print);
        Add(message);
        root.Controls.Add(fields, 0, 1);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        right.Controls.Add(preview, 0, 0);
        right.Controls.Add(new Label { Text = "Label preview (enlarged)\nPreview, PNG, and print use the same artwork.\nUse a short item ID and test with your scanner.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
        root.Controls.Add(right, 1, 1);

        var listPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0, 12, 0, 0) };
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listPanel.Controls.Add(new Label { Text = "Print list", AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), Margin = new Padding(0, 0, 0, 2) }, 0, 0);
        listSummary.Margin = new Padding(0, 0, 0, 6);
        listPanel.Controls.Add(listSummary, 0, 1);
        listPanel.Controls.Add(staged, 0, 2);
        var commands = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var flow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        flow.Controls.Add(new Label { Text = "Selected quantity", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
        lineQuantity.Margin = new Padding(0, 3, 12, 0);
        flow.Controls.Add(lineQuantity);
        flow.Controls.AddRange([moveUp, moveDown, removeLine, clearList]);
        commands.Controls.Add(flow, 0, 0);
        printJob.Anchor = AnchorStyles.Right;
        commands.Controls.Add(printJob, 1, 0);
        listPanel.Controls.Add(commands, 0, 3);
        root.Controls.Add(listPanel, 0, 2);
        root.SetColumnSpan(listPanel, 2);
        Controls.Add(root);

        product.TextChanged += (_, _) => UpdatePreview();
        barcode.TextChanged += (_, _) => UpdatePreview();
        price.ValueChanged += (_, _) => UpdatePreview();
        printers.SelectedIndexChanged += (_, _) => UpdatePreview();
        export.Click += (_, _) => Run(Export);
        save.Click += (_, _) => Run(Save);
        open.Click += (_, _) => Run(Open);
        print.Click += (_, _) => Run(() => LabelPrinter.Print(this, Design(), printers.Text, (int)copies.Value));
        addToList.Click += (_, _) => Run(AddCurrent);
        printJob.Click += (_, _) => Run(() => LabelPrinter.PrintJob(this, printers.Text, job.Entries.ToArray()));
        removeLine.Click += (_, _) => RemoveSelected();
        clearList.Click += (_, _) => ClearJob();
        moveUp.Click += (_, _) => MoveSelected(-1);
        moveDown.Click += (_, _) => MoveSelected(1);
        lineQuantity.ValueChanged += (_, _) => ChangeSelectedQuantity();
        staged.SelectedIndexChanged += (_, _) => { if (!adjustingList) SyncLineQuantity(); };
        staged.DoubleClick += (_, _) => LoadSelected();
        staged.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Delete) return;
            RemoveSelected();
            e.Handled = true;
        };
        RefreshPrinters();
        UpdatePreview();
        SyncLineQuantity();
    }

    private LabelDesign Design() => new(product.Text, barcode.Text, price.Value);

    private void AddCurrent()
    {
        int index = job.Add(Design(), (int)copies.Value);
        BindStaged(index);
    }

    private void ChangeSelectedQuantity()
    {
        if (adjustingList || staged.SelectedIndices.Count != 1) return;
        int index = staged.SelectedIndices[0];
        try
        {
            job.SetQuantity(index, (int)lineQuantity.Value);
            staged.Items[index].SubItems[3].Text = lineQuantity.Value.ToString();
            SyncLineQuantity();
            UpdateJobButtons();
        }
        catch (Exception ex)
        {
            adjustingList = true;
            lineQuantity.Value = job.Entries[index].Quantity;
            adjustingList = false;
            MessageBox.Show(this, ex.Message, "Labels", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RemoveSelected()
    {
        if (staged.SelectedIndices.Count != 1) return;
        int index = staged.SelectedIndices[0];
        job.RemoveAt(index);
        int next = Math.Min(index, job.Entries.Count - 1);
        BindStaged(next >= 0 ? next : null);
    }

    private void MoveSelected(int delta)
    {
        if (staged.SelectedIndices.Count != 1) return;
        int index = staged.SelectedIndices[0];
        int destination = index + delta;
        if (destination < 0 || destination >= job.Entries.Count) return;
        job.Move(index, destination);
        BindStaged(destination);
    }

    private void ClearJob()
    {
        if (job.Entries.Count == 0) return;
        if (MessageBox.Show(this, "Remove all labels from the print list?", "Labels", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        job.Clear();
        BindStaged(null);
    }

    private void LoadSelected()
    {
        if (staged.SelectedIndices.Count != 1) return;
        var entry = job.Entries[staged.SelectedIndices[0]];
        product.Text = entry.Design.Name;
        barcode.Text = entry.Design.Barcode;
        price.Value = entry.Design.Price;
        copies.Value = entry.Quantity;
        product.Focus();
        product.SelectAll();
    }

    private void BindStaged(int? selectIndex)
    {
        adjustingList = true;
        staged.BeginUpdate();
        staged.Items.Clear();
        foreach (var entry in job.Entries)
        {
            var item = new ListViewItem(entry.Design.Name.Length == 0 ? "(no name)" : entry.Design.Name);
            item.SubItems.Add(entry.Design.Barcode);
            item.SubItems.Add(entry.Design.PriceText);
            item.SubItems.Add(entry.Quantity.ToString());
            staged.Items.Add(item);
        }
        staged.EndUpdate();
        if (selectIndex is int index && index >= 0 && index < staged.Items.Count)
        {
            staged.Items[index].Selected = true;
            staged.Items[index].Focused = true;
            staged.EnsureVisible(index);
        }
        adjustingList = false;
        SyncLineQuantity();
        UpdateJobButtons();
    }

    private void SyncLineQuantity()
    {
        bool selected = staged.SelectedIndices.Count == 1;
        lineQuantity.Enabled = removeLine.Enabled = selected;
        moveUp.Enabled = moveDown.Enabled = false;
        if (!selected) return;
        int index = staged.SelectedIndices[0];
        var entry = job.Entries[index];
        int room = PrintList.MaxLabels - (job.TotalLabels - entry.Quantity);
        adjustingList = true;
        lineQuantity.Maximum = PrintList.MaxLineQuantity;
        lineQuantity.Value = entry.Quantity;
        lineQuantity.Maximum = Math.Min(PrintList.MaxLineQuantity, room);
        adjustingList = false;
        moveUp.Enabled = index > 0;
        moveDown.Enabled = index < job.Entries.Count - 1;
    }

    private void UpdateJobButtons()
    {
        printJob.Enabled = job.Entries.Count > 0 && printers.SelectedIndex >= 0;
        clearList.Enabled = job.Entries.Count > 0;
        listSummary.Text = job.Entries.Count == 0
            ? "Nothing staged. Set a quantity and add the current label. Double-click a row to load it back into the editor."
            : $"{job.TotalLabels} {(job.TotalLabels == 1 ? "label" : "labels")} in this job. Leave Copies at 1 in the print dialog; these quantities are what prints.";
    }

    private void RefreshPrinters()
    {
        string previous = printers.Text;
        printers.Items.Clear();
        Run(() =>
        {
            foreach (string name in PrinterSettings.InstalledPrinters)
                if (name.Contains("QL-810", StringComparison.OrdinalIgnoreCase)) printers.Items.Add(name);
            if (printers.Items.Contains(previous)) printers.SelectedItem = previous;
            else if (printers.Items.Count > 0) printers.SelectedIndex = 0;
        });
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        try
        {
            preview.SetImage(LabelRenderer.Render(Design()));
            export.Enabled = save.Enabled = addToList.Enabled = true;
            print.Enabled = printers.SelectedIndex >= 0;
            message.ForeColor = Color.FromArgb(70, 82, 100);
            message.Text = print.Enabled
                ? "Ready. Add labels to the print list for one job, or print this label now. Load DK-1221 and confirm 23 × 23 mm media."
                : "No Brother QL-810 printer found. Install the Brother Windows driver, then refresh. You can still create, stage, and export labels.";
        }
        catch (ArgumentException ex)
        {
            preview.SetImage(null);
            print.Enabled = export.Enabled = save.Enabled = addToList.Enabled = false;
            message.ForeColor = Color.Firebrick;
            message.Text = ex.Message;
        }
        UpdateJobButtons();
    }

    private void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = "label.png" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        using var bitmap = LabelRenderer.Render(Design());
        bitmap.Save(dialog.FileName, ImageFormat.Png);
    }

    private void Save()
    {
        using var dialog = new SaveFileDialog { Filter = "Label file (*.label.json)|*.label.json", FileName = "item.label.json" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(Design(), new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Open()
    {
        using var dialog = new OpenFileDialog { Filter = "Label file (*.label.json)|*.label.json|JSON files (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (new FileInfo(dialog.FileName).Length > 64 * 1024) throw new ArgumentException("This label file is too large.");
        var design = JsonSerializer.Deserialize<LabelDesign>(File.ReadAllText(dialog.FileName))
            ?? throw new ArgumentException("This file does not contain a label.");
        using var check = LabelRenderer.Render(design); // Validate before changing the editor.
        product.Text = design.Name;
        barcode.Text = design.Barcode;
        price.Value = design.Price;
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Labels", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

internal sealed class LabelPreview : Control
{
    private Bitmap? bitmap;
    public LabelPreview() { DoubleBuffered = true; BackColor = Color.FromArgb(231, 235, 241); }
    public void SetImage(Bitmap? next) { var old = bitmap; bitmap = next; old?.Dispose(); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (bitmap is null) return;
        int size = Math.Max(1, Math.Min(Width - 48, Height - 48));
        int x = (Width - size) / 2, y = (Height - size) / 2;
        e.Graphics.FillRectangle(Brushes.LightGray, x + 5, y + 5, size, size);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(bitmap, new Rectangle(x, y, size, size));
    }
    protected override void Dispose(bool disposing) { if (disposing) bitmap?.Dispose(); base.Dispose(disposing); }
}
