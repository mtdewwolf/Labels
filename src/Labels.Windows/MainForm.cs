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
    private readonly NumericUpDown copies = new() { Minimum = 1, Maximum = 1000, Value = 1, Dock = DockStyle.Fill };
    private readonly ComboBox printers = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label message = new() { AutoSize = true, MaximumSize = new Size(380, 0), ForeColor = Color.FromArgb(70, 82, 100) };
    private readonly LabelPreview preview = new() { Dock = DockStyle.Fill };
    private readonly Button print = new() { Text = "Print labels…", AutoSize = true, Padding = new Padding(14, 8, 14, 8) };
    private readonly Button export = new() { Text = "Export PNG", AutoSize = true };
    private readonly Button save = new() { Text = "Save label…", AutoSize = true };

    public MainForm()
    {
        Text = "Labels — Brother QL-810W";
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);
        MinimumSize = new Size(840, 640);
        ClientSize = new Size(960, 700);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 2, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = new Label { Text = "Create a price label\nBrother QL-810W · DK-1221 · 23 × 23 mm", AutoSize = true, Font = new Font("Segoe UI", 15, FontStyle.Bold) };
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
        Field("Number of labels", copies);
        Field("Brother printer", printers);
        var refresh = new Button { Text = "Refresh printers", AutoSize = true };
        refresh.Click += (_, _) => RefreshPrinters();
        Add(refresh);
        var files = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var open = new Button { Text = "Open label…", AutoSize = true };
        files.Controls.AddRange([open, save, export]);
        Add(files);
        Add(print);
        Add(message);
        root.Controls.Add(fields, 0, 1);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        right.Controls.Add(preview, 0, 0);
        right.Controls.Add(new Label { Text = "Label preview (enlarged)\nPreview, PNG, and print use the same artwork.\nUse a short item ID and test with your scanner.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
        root.Controls.Add(right, 1, 1);
        Controls.Add(root);
        product.TextChanged += (_, _) => UpdatePreview();
        barcode.TextChanged += (_, _) => UpdatePreview();
        price.ValueChanged += (_, _) => UpdatePreview();
        printers.SelectedIndexChanged += (_, _) => UpdatePreview();
        export.Click += (_, _) => Run(Export);
        save.Click += (_, _) => Run(Save);
        open.Click += (_, _) => Run(Open);
        print.Click += (_, _) => Run(() => LabelPrinter.Print(this, Design(), printers.Text, (int)copies.Value));
        RefreshPrinters();
        UpdatePreview();
    }

    private LabelDesign Design() => new(product.Text, barcode.Text, price.Value);

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
            export.Enabled = save.Enabled = true;
            print.Enabled = printers.SelectedIndex >= 0;
            message.ForeColor = Color.FromArgb(70, 82, 100);
            message.Text = print.Enabled
                ? "Ready. Load DK-1221 labels; confirm 23 × 23 mm media in the print dialog."
                : "No Brother QL-810 printer found. Install the Brother Windows driver, then refresh. You can still create and export labels.";
        }
        catch (ArgumentException ex)
        {
            preview.SetImage(null);
            print.Enabled = export.Enabled = save.Enabled = false;
            message.ForeColor = Color.Firebrick;
            message.Text = ex.Message;
        }
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
