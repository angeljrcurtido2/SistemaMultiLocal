using System.Drawing.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace CrediSoft.UI.Views.Maestros;

public static class ArticulosImpresora
{
    public static async Task ImprimirAsync(ArticulosPagina p, Window? owner = null)
    {
        var doc = new PrintDocument { DocumentName = "Artículos" };
        doc.DefaultPageSettings.Landscape = false;

        bool esPdf = string.IsNullOrEmpty(p.Impresora)
                  || p.Impresora.Contains("PDF", StringComparison.OrdinalIgnoreCase)
                  || p.Impresora.Contains("XPS", StringComparison.OrdinalIgnoreCase);

        string? pdfRuta = null;
        if (esPdf)
        {
            // Siempre usamos Microsoft Print to PDF para controlar el nombre del archivo.
            // Drivers de terceros (Brave, Chrome PDF) ignoran PrintFileName y abren su
            // propio diálogo, por lo que los evitamos por completo.
            doc.PrinterSettings.PrinterName = "Microsoft Print to PDF";

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title            = "Guardar PDF",
                Filter           = "Archivo PDF (*.pdf)|*.pdf",
                DefaultExt       = "pdf",
                FileName         = $"ReporteArticulosGral_{DateTime.Now:yyyyMMdd_HHmm}",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            if (sfd.ShowDialog() != true) return;
            pdfRuta = sfd.FileName;
            doc.PrinterSettings.PrintFileName = pdfRuta;
            doc.PrinterSettings.PrintToFile   = true;
        }
        else
        {
            doc.PrinterSettings.PrinterName = p.Impresora;
        }

        int totalPages = ArticulosDibujador.ContarPaginas(p);
        var dlg = new ImprimiendoDialog(totalPages) { Owner = owner };
        dlg.Show();

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(p.LogoPath))
            try { logo = System.Drawing.Image.FromFile(p.LogoPath); } catch { }

        string? errorMsg = null;

        await Task.Run(() =>
        {
            int filaOffset = 0;
            int pageNum    = 0;

            doc.EndPrint += (_, _) => logo?.Dispose();
            doc.PrintPage += (_, e) =>
            {
                pageNum++;
                dlg.Dispatcher.InvokeAsync(() => dlg.SetPagina(pageNum));

                var g = e.Graphics!;
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
                e.HasMorePages = ArticulosDibujador.DibujarPagina(g, p, logo, ref filaOffset, pageNum, totalPages);
            };

            try   { doc.Print(); }
            catch (Exception ex) { errorMsg = ex.Message; }
        });

        dlg.Close();

        if (errorMsg != null)
            MessageBox.Show($"Error al imprimir:\n{errorMsg}", "Error de impresión",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        else if (pdfRuta is { Length: > 0 })
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfRuta) { UseShellExecute = true }); } catch { }
    }

    public static void Imprimir(ArticulosPagina p, Window? owner = null)
        => _ = ImprimirAsync(p, owner);
}

// ── Diálogo de progreso con spinner y contador de páginas ─────────────────
internal class ImprimiendoDialog : Window
{
    private readonly int        _total;
    private TextBlock           _lblPagina  = null!;
    private TextBlock           _lblStatus  = null!;
    private ProgressBar         _bar        = null!;

    public ImprimiendoDialog(int totalPages)
    {
        _total                = totalPages;
        Title                 = "Imprimiendo";
        Width                 = 360;
        Height                = 180;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle           = WindowStyle.ToolWindow;
        Topmost               = true;
        Background            = new SolidColorBrush(Color.FromRgb(30, 42, 52));
        FontFamily            = new FontFamily("Segoe UI");
        Content               = BuildUI();
    }

    private UIElement BuildUI()
    {
        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Fila 0: spinner + título ─────────────────────────────────────────
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        headerRow.Children.Add(BuildSpinner());
        headerRow.Children.Add(new TextBlock
        {
            Text              = "Enviando a impresora",
            Foreground        = Brushes.White,
            FontSize          = 14,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(12, 0, 0, 0),
        });
        Grid.SetRow(headerRow, 0); root.Children.Add(headerRow);

        // ── Fila 1: contador de página ────────────────────────────────────────
        _lblPagina = new TextBlock
        {
            Text       = $"Página 0 de {_total}",
            Foreground = new SolidColorBrush(Color.FromRgb(144, 202, 249)),
            FontSize   = 12,
            Margin     = new Thickness(0, 0, 0, 8),
        };
        Grid.SetRow(_lblPagina, 1); root.Children.Add(_lblPagina);

        // ── Fila 2: barra de progreso ─────────────────────────────────────────
        _bar = new ProgressBar
        {
            Minimum    = 0,
            Maximum    = _total,
            Value      = 0,
            Height     = 8,
            Margin     = new Thickness(0, 0, 0, 8),
            Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 34)),
            Background = new SolidColorBrush(Color.FromRgb(55, 71, 79)),
            BorderThickness = new Thickness(0),
        };
        Grid.SetRow(_bar, 2); root.Children.Add(_bar);

        // ── Fila 3: status ────────────────────────────────────────────────────
        _lblStatus = new TextBlock
        {
            Text       = "Por favor espere, no cierre la ventana...",
            Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156)),
            FontSize   = 10,
            FontStyle  = FontStyles.Italic,
        };
        Grid.SetRow(_lblStatus, 3); root.Children.Add(_lblStatus);

        return root;
    }

    private static UIElement BuildSpinner()
    {
        // Arco giratorio con RotateTransform animado
        const double size = 28;
        var canvas = new Canvas { Width = size, Height = size };

        var arc = new Arc
        {
            Width           = size,
            Height          = size,
            StartAngle      = 0,
            EndAngle        = 300,
            Stroke          = new SolidColorBrush(Color.FromRgb(230, 126, 34)),
            StrokeThickness = 3,
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
        };

        var rot = new RotateTransform(0);
        arc.RenderTransform = rot;

        var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(0.9)))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        rot.BeginAnimation(RotateTransform.AngleProperty, anim);

        canvas.Children.Add(arc);
        return canvas;
    }

    public void SetPagina(int pagina)
    {
        _lblPagina.Text = $"Página {pagina} de {_total}";
        _bar.Value      = pagina;
        if (pagina >= _total)
            _lblStatus.Text = "Finalizando...";
    }
}

// Arc shape — dibuja un arco desde StartAngle hasta EndAngle
internal class Arc : Shape
{
    public static readonly DependencyProperty StartAngleProperty =
        DependencyProperty.Register(nameof(StartAngle), typeof(double), typeof(Arc),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EndAngleProperty =
        DependencyProperty.Register(nameof(EndAngle), typeof(double), typeof(Arc),
            new FrameworkPropertyMetadata(270.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double StartAngle { get => (double)GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public double EndAngle   { get => (double)GetValue(EndAngleProperty);   set => SetValue(EndAngleProperty, value); }

    protected override Geometry DefiningGeometry
    {
        get
        {
            double w = Width  > 0 ? Width  : ActualWidth;
            double h = Height > 0 ? Height : ActualHeight;
            if (w <= 0 || h <= 0) return Geometry.Empty;

            double rx = w / 2 - StrokeThickness / 2;
            double ry = h / 2 - StrokeThickness / 2;
            double cx = w / 2, cy = h / 2;

            double start = StartAngle * Math.PI / 180;
            double end   = EndAngle   * Math.PI / 180;

            var p1 = new System.Windows.Point(cx + rx * Math.Cos(start), cy + ry * Math.Sin(start));
            var p2 = new System.Windows.Point(cx + rx * Math.Cos(end),   cy + ry * Math.Sin(end));

            double sweep = EndAngle - StartAngle;
            bool   large = sweep > 180;

            var seg = new ArcSegment(p2, new Size(rx, ry), 0, large, SweepDirection.Clockwise, true);
            var fig = new PathFigure(p1, new[] { seg }, false);
            return new PathGeometry(new[] { fig });
        }
    }
}
