using System.Drawing.Printing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Maestros;

// ── Vista previa ─────────────────────────────────────────────────────────────
public class ProveedoresPreviewWindow : Window
{
    private readonly ProveedoresPagina _pagina;
    private ScrollViewer _scroll  = null!;
    private TextBlock    _lblLoad = null!;

    public ProveedoresPreviewWindow(ProveedoresPagina pagina)
    {
        _pagina               = pagina;
        Title                 = "Vista previa — Proveedores";
        // Height fijo excedía el área de trabajo en pantallas de 1366x768 — ver comentario
        // equivalente en ClienteFichaImpresora.cs.
        Width                 = 880;
        Height                = System.Math.Min(760, SystemParameters.WorkArea.Height - 20);
        MinWidth              = 600;
        MinHeight             = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = new SolidColorBrush(Color.FromRgb(55, 71, 79));
        FontFamily            = new FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await RenderAsync();
    }

    private void BuildUI()
    {
        var root    = new DockPanel();
        var toolbar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(38, 50, 56)),
            Padding    = new Thickness(14, 8, 14, 8),
        };
        DockPanel.SetDock(toolbar, Dock.Top);

        var row  = new StackPanel { Orientation = Orientation.Horizontal };
        var btnP = Btn("Imprimir", "#1B5E20");
        var btnC = Btn("Cerrar",   "#B71C1C");
        btnP.Click += (_, _) => { var o = Owner; Close(); ProveedoresImpresora.Imprimir(_pagina, o); };
        btnC.Click += (_, _) => Close();
        row.Children.Add(btnP);
        row.Children.Add(btnC);
        row.Children.Add(new TextBlock
        {
            Text              = "Proveedores",
            Foreground        = Brushes.White,
            FontSize          = 13,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(20, 0, 0, 0),
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
            Background = new SolidColorBrush(Color.FromRgb(69, 90, 100)),
            Visibility = Visibility.Collapsed,
        };

        var body = new Grid();
        body.Children.Add(loadGrid);
        body.Children.Add(_scroll);
        root.Children.Add(body);
        Content = root;
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

    private async Task RenderAsync()
    {
        const int pgW = 827, pgH = 1169;
        int totalPages = ProveedoresDibujador.ContarPaginas(_pagina);

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(_pagina.LogoPath))
            try { logo = System.Drawing.Image.FromFile(_pagina.LogoPath); } catch { }

        var paginas = new List<BitmapSource>();
        try
        {
            var p = _pagina;
            paginas = await Task.Run(() =>
            {
                var result   = new List<BitmapSource>();
                int area     = pgH - 42 - (92 + 24 + 3);
                int porPag   = area / 26;
                for (int pn = 1; pn <= totalPages; pn++)
                {
                    int offset = (pn - 1) * porPag;
                    var bmp = new System.Drawing.Bitmap(pgW, pgH);
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.Clear(System.Drawing.Color.White);
                    g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                    ProveedoresDibujador.DibujarPagina(g, p, logo, offset, pn, totalPages);
                    result.Add(BmpToSource(bmp));
                    bmp.Dispose();
                }
                return result;
            });
        }
        finally { logo?.Dispose(); }

        var panel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var src in paginas)
        {
            var shadow = new Border
            {
                Margin = new Thickness(0, 16, 0, 0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { Color = Colors.Black, BlurRadius = 10, Opacity = 0.5, ShadowDepth = 4 },
            };
            shadow.Child = new Border
            {
                Background      = Brushes.White,
                BorderBrush     = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Child           = new System.Windows.Controls.Image
                    { Source = src, Width = pgW * 0.88, Height = pgH * 0.88, Stretch = Stretch.Uniform },
            };
            panel.Children.Add(shadow);
        }
        panel.Children.Add(new Border { Height = 16 });

        _scroll.Content     = panel;
        _scroll.Visibility  = Visibility.Visible;
        _lblLoad.Visibility = Visibility.Collapsed;
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
public static class ProveedoresImpresora
{
    public static void Imprimir(ProveedoresPagina p, Window? owner = null)
        => _ = ImprimirAsync(p, owner);

    private static async Task ImprimirAsync(ProveedoresPagina p, Window? owner)
    {
        var doc = new PrintDocument { DocumentName = "Listado de Proveedores" };
        doc.PrinterSettings.PrinterName = "Microsoft Print to PDF";

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "Guardar PDF",
            Filter           = "Archivo PDF (*.pdf)|*.pdf",
            DefaultExt       = "pdf",
            FileName         = $"Proveedores_{DateTime.Now:yyyyMMdd_HHmm}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (sfd.ShowDialog() != true) return;
        var pdfRuta = sfd.FileName;
        doc.PrinterSettings.PrintFileName = pdfRuta;
        doc.PrinterSettings.PrintToFile   = true;

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(p.LogoPath))
            try { logo = System.Drawing.Image.FromFile(p.LogoPath); } catch { }

        int totalPages = ProveedoresDibujador.ContarPaginas(p);
        const int pgW  = 827, pgH = 1169;

        var paginas = new List<System.Drawing.Bitmap>();
        await Task.Run(() =>
        {
            int area   = pgH - 42 - (92 + 24 + 3);
            int porPag = area / 26;
            for (int pn = 1; pn <= totalPages; pn++)
            {
                int offset = (pn - 1) * porPag;
                var bmp = new System.Drawing.Bitmap(pgW, pgH);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.Clear(System.Drawing.Color.White);
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                ProveedoresDibujador.DibujarPagina(g, p, logo, offset, pn, totalPages);
                paginas.Add(bmp);
            }
        });
        logo?.Dispose();

        string? errorMsg = null;
        await Task.Run(() =>
        {
            int pn = 0;
            doc.PrintPage += (_, e) =>
            {
                var g  = e.Graphics!;
                var pb = e.PageBounds;
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(paginas[pn], pb.X, pb.Y, pb.Width, pb.Height);
                pn++;
                e.HasMorePages = pn < paginas.Count;
            };
            doc.EndPrint += (_, _) => { foreach (var b in paginas) b.Dispose(); paginas.Clear(); };
            try   { doc.Print(); }
            catch (Exception ex) { errorMsg = ex.Message; }
        });

        if (errorMsg != null)
            MessageBox.Show($"Error al imprimir:\n{errorMsg}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            try { System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(pdfRuta) { UseShellExecute = true }); } catch { }
    }
}
