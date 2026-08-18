using System.Drawing.Printing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Informes;

// ── Vista previa ──────────────────────────────────────────────────────────────
public class TransferenciasPreviewWindow : Window
{
    private readonly TransferenciasPagina _p;
    private ScrollViewer _scroll  = null!;
    private TextBlock    _lblLoad = null!;

    public TransferenciasPreviewWindow(TransferenciasPagina p)
    {
        _p                    = p;
        Title                 = "Vista previa — Historial de Transferencias";
        Width                 = 760;
        Height                = 680;
        MinWidth              = 600;
        MinHeight             = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = new SolidColorBrush(Color.FromRgb(10, 25, 60));
        FontFamily            = new FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await RenderAsync();
    }

    private void BuildUI()
    {
        var root    = new DockPanel();
        var toolbar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(13, 71, 161)),
            Padding    = new Thickness(14, 8, 14, 8)
        };
        DockPanel.SetDock(toolbar, Dock.Top);

        var row  = new StackPanel { Orientation = Orientation.Horizontal };
        var btnP = Btn("🖨 Imprimir", "#1B5E20");
        var btnC = Btn("✕ Cerrar",   "#37474F");
        btnP.Click += (_, _) => { var o = Owner; Close(); TransferenciasImpresora.Imprimir(_p, o); };
        btnC.Click += (_, _) => Close();
        row.Children.Add(btnP);
        row.Children.Add(btnC);
        row.Children.Add(new TextBlock
        {
            Text              = "Historial de Transferencias",
            Foreground        = Brushes.White,
            FontSize          = 13,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(20, 0, 0, 0)
        });
        toolbar.Child = row;
        root.Children.Add(toolbar);

        _lblLoad = new TextBlock
        {
            Text                = "Generando vista previa...",
            Foreground          = Brushes.White,
            FontSize            = 15,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        var loadGrid = new Grid();
        loadGrid.Children.Add(_lblLoad);

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(20, 40, 90)),
            Visibility = Visibility.Collapsed,
        };

        var body = new Grid();
        body.Children.Add(loadGrid);
        body.Children.Add(_scroll);
        root.Children.Add(body);
        Content = root;
    }

    private async Task RenderAsync()
    {
        const int pgW = 827, pgH = 1169;

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(_p.LogoPath))
            try { logo = System.Drawing.Image.FromFile(_p.LogoPath); } catch { }

        var panel = new StackPanel
        {
            Orientation         = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0, 16, 0, 16)
        };

        try
        {
            int totalPages = TransferenciasDibujador.ContarPaginas(_p);
            int filaOffset = 0, pageNum = 0;

            while (true)
            {
                pageNum++;
                int capturedOffset = filaOffset;
                int capturedPage   = pageNum;
                var src = await Task.Run(() =>
                {
                    int localOffset = capturedOffset;
                    var bmp = new System.Drawing.Bitmap(pgW, pgH);
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.Clear(System.Drawing.Color.White);
                    g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                    TransferenciasDibujador.DibujarPagina(g, _p, logo, ref localOffset, capturedPage, totalPages);
                    filaOffset = localOffset;
                    var r = BmpToSource(bmp);
                    bmp.Dispose();
                    return r;
                });

                var shadow = new Border
                {
                    Margin = new Thickness(0, 0, 0, 16),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                        { Color = Colors.Black, BlurRadius = 10, Opacity = 0.5, ShadowDepth = 4 },
                };
                shadow.Child = new Border
                {
                    Background      = Brushes.White,
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    BorderThickness = new Thickness(1),
                    Child           = new System.Windows.Controls.Image
                        { Source = src, Width = pgW * 0.72, Height = pgH * 0.72, Stretch = Stretch.Uniform }
                };
                panel.Children.Add(shadow);

                if (filaOffset >= _p.Filas.Count || pageNum > 500) break;
            }
        }
        finally { logo?.Dispose(); }

        _scroll.Content     = panel;
        _scroll.Visibility  = Visibility.Visible;
        _lblLoad.Visibility = Visibility.Collapsed;
    }

    private static Button Btn(string txt, string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        return new Button
        {
            Content         = txt,
            Padding         = new Thickness(16, 6, 16, 6),
            FontSize        = 13,
            FontWeight      = FontWeights.SemiBold,
            Foreground      = Brushes.White,
            Background      = new SolidColorBrush(c),
            BorderThickness = new Thickness(0),
            Cursor          = System.Windows.Input.Cursors.Hand,
            Margin          = new Thickness(0, 0, 8, 0),
        };
    }

    private static BitmapSource BmpToSource(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);
        var bi = new BitmapImage();
        bi.BeginInit(); bi.StreamSource = ms; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit();
        bi.Freeze();
        return bi;
    }
}

// ── Impresora ─────────────────────────────────────────────────────────────────
public static class TransferenciasImpresora
{
    public static void Imprimir(TransferenciasPagina p, Window? owner = null)
        => _ = ImprimirAsync(p, owner);

    private static async Task ImprimirAsync(TransferenciasPagina p, Window? owner)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "Guardar PDF — Historial de Transferencias",
            Filter           = "Archivo PDF (*.pdf)|*.pdf",
            DefaultExt       = "pdf",
            FileName         = $"Transferencias_{DateTime.Now:yyyyMMdd_HHmm}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (sfd.ShowDialog() != true) return;
        var pdfRuta = sfd.FileName;

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(p.LogoPath))
            try { logo = System.Drawing.Image.FromFile(p.LogoPath); } catch { }

        const int pgW = 827, pgH = 1169;
        int totalPages = TransferenciasDibujador.ContarPaginas(p);
        var bitmaps    = new List<System.Drawing.Bitmap>();

        await Task.Run(() =>
        {
            int filaOffset = 0, pageNum = 0;
            while (true)
            {
                pageNum++;
                var bmp = new System.Drawing.Bitmap(pgW, pgH);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.Clear(System.Drawing.Color.White);
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                TransferenciasDibujador.DibujarPagina(g, p, logo, ref filaOffset, pageNum, totalPages);
                bitmaps.Add(bmp);
                if (filaOffset >= p.Filas.Count || pageNum > 500) break;
            }
        });
        logo?.Dispose();

        var doc = new PrintDocument { DocumentName = "Historial de Transferencias" };
        doc.DefaultPageSettings.Landscape = false;
        doc.PrinterSettings.PrinterName   = "Microsoft Print to PDF";
        doc.PrinterSettings.PrintFileName = pdfRuta;
        doc.PrinterSettings.PrintToFile   = true;

        int idx = 0;
        string? errorMsg = null;

        await Task.Run(() =>
        {
            doc.PrintPage += (_, e) =>
            {
                if (idx >= bitmaps.Count) return;
                var g  = e.Graphics!;
                var pb = e.PageBounds;
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmaps[idx], pb.X, pb.Y, pb.Width, pb.Height);
                idx++;
                e.HasMorePages = idx < bitmaps.Count;
            };
            doc.EndPrint += (_, _) => { foreach (var b in bitmaps) b.Dispose(); bitmaps.Clear(); };
            try   { doc.Print(); }
            catch (Exception ex) { errorMsg = ex.Message; }
        });

        if (errorMsg != null)
        {
            MessageBox.Show($"Error al imprimir:\n{errorMsg}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try { System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(pdfRuta) { UseShellExecute = true }); } catch { }
    }
}
