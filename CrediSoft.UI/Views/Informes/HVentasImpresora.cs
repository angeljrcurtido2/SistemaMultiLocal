using System.Drawing.Printing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Informes;

// ═══════════════════════════════════════════════════════════════════════════
//  PREVIEW — Detalle (landscape)
// ═══════════════════════════════════════════════════════════════════════════
public class HVentasDetallePreviewWindow : Window
{
    private readonly HVentasPagina  _pagina;
    private readonly List<HVItem>   _items;
    private ScrollViewer _scroll  = null!;
    private TextBlock    _lblLoad = null!;

    public HVentasDetallePreviewWindow(HVentasPagina pagina)
    {
        _pagina               = pagina;
        _items                = HVentasDetalleDibujador.BuildItems(pagina);
        Title                 = "Vista previa — Historial de Ventas (Detalle)";
        Width                 = 1020;
        Height                = 680;
        MinWidth              = 700;
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
        var btnP = Btn("🖨  PDF",         "#1A5276");
        var btnX = Btn("📊  Excel",       "#1E6B2E");
        var btnC = Btn("Cerrar",          "#B71C1C");
        btnP.Click += (_, _) => { var o = Owner; Close(); HVentasImpresora.ImprimirDetalle(_pagina, o); };
        btnX.Click += (_, _) => HVentasExcel.ExportarDetalle(_pagina);
        btnC.Click += (_, _) => Close();
        row.Children.Add(btnP);
        row.Children.Add(btnX);
        row.Children.Add(btnC);
        row.Children.Add(new TextBlock
        {
            Text              = $"Detalle — {_pagina.Cantidad} venta(s)  |  Total: Gs. {_pagina.SumTotal:N0}  |  Saldo: Gs. {_pagina.SumSaldo:N0}",
            Foreground        = Brushes.White,
            FontSize          = 12,
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
            Padding         = new Thickness(14, 6, 14, 6),
            FontSize        = 12,
            FontWeight      = FontWeights.SemiBold,
            Foreground      = Brushes.White,
            Background      = new SolidColorBrush(c),
            BorderThickness = new Thickness(0),
            Cursor          = System.Windows.Input.Cursors.Hand,
            Margin          = new Thickness(0, 0, 6, 0),
        };
    }

    private async Task RenderAsync()
    {
        const int pgW = 1169, pgH = 827;
        int totalPages = HVentasDetalleDibujador.ContarPaginas(_items);

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(_pagina.LogoPath))
            try { logo = System.Drawing.Image.FromFile(_pagina.LogoPath); } catch { }

        var paginas = new List<BitmapSource>();
        try
        {
            var p     = _pagina;
            var items = _items;
            paginas = await Task.Run(() =>
            {
                var result = new List<BitmapSource>();
                int offset = 0;
                for (int pn = 1; pn <= totalPages; pn++)
                {
                    var bmp = new System.Drawing.Bitmap(pgW, pgH);
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.Clear(System.Drawing.Color.White);
                    g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                    HVentasDetalleDibujador.DibujarPagina(g, p, items, logo, pn, totalPages, ref offset);
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
                    { Source = src, Width = pgW * 0.82, Height = pgH * 0.82, Stretch = Stretch.Uniform },
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

// ═══════════════════════════════════════════════════════════════════════════
//  PREVIEW — Resumen (portrait)
// ═══════════════════════════════════════════════════════════════════════════
public class HVentasResumenPreviewWindow : Window
{
    private readonly HVentasPagina _pagina;
    private readonly List<HVItem>  _items;
    private ScrollViewer _scroll  = null!;
    private TextBlock    _lblLoad = null!;

    public HVentasResumenPreviewWindow(HVentasPagina pagina)
    {
        _pagina               = pagina;
        _items                = HVentasResumenDibujador.BuildItems(pagina);
        Title                 = "Vista previa — Historial de Ventas (Resumen)";
        Width                 = 900;
        Height                = 680;
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
        var btnP = Btn("🖨  PDF",       "#1A5276");
        var btnX = Btn("📊  Excel",     "#1E6B2E");
        var btnC = Btn("Cerrar",        "#B71C1C");
        btnP.Click += (_, _) => { var o = Owner; Close(); HVentasImpresora.ImprimirResumen(_pagina, o); };
        btnX.Click += (_, _) => HVentasExcel.ExportarResumen(_pagina);
        btnC.Click += (_, _) => Close();
        row.Children.Add(btnP);
        row.Children.Add(btnX);
        row.Children.Add(btnC);
        row.Children.Add(new TextBlock
        {
            Text              = $"Resumen — {_pagina.Cantidad} venta(s)  |  Total: Gs. {_pagina.SumTotal:N0}",
            Foreground        = Brushes.White,
            FontSize          = 12,
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
            Padding         = new Thickness(14, 6, 14, 6),
            FontSize        = 12,
            FontWeight      = FontWeights.SemiBold,
            Foreground      = Brushes.White,
            Background      = new SolidColorBrush(c),
            BorderThickness = new Thickness(0),
            Cursor          = System.Windows.Input.Cursors.Hand,
            Margin          = new Thickness(0, 0, 6, 0),
        };
    }

    private async Task RenderAsync()
    {
        const int pgW = 827, pgH = 1169;
        int totalPages = HVentasResumenDibujador.ContarPaginas(_items);

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(_pagina.LogoPath))
            try { logo = System.Drawing.Image.FromFile(_pagina.LogoPath); } catch { }

        var paginas = new List<BitmapSource>();
        try
        {
            var p     = _pagina;
            var items = _items;
            paginas = await Task.Run(() =>
            {
                var result = new List<BitmapSource>();
                int offset = 0;
                for (int pn = 1; pn <= totalPages; pn++)
                {
                    var bmp = new System.Drawing.Bitmap(pgW, pgH);
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.Clear(System.Drawing.Color.White);
                    g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                    HVentasResumenDibujador.DibujarPagina(g, p, items, logo, pn, totalPages, ref offset);
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

// ═══════════════════════════════════════════════════════════════════════════
//  IMPRESORA
// ═══════════════════════════════════════════════════════════════════════════
public static class HVentasImpresora
{
    public static void ImprimirDetalle(HVentasPagina p, Window? owner = null)
        => _ = ImprimirDetalleAsync(p);

    public static void ImprimirResumen(HVentasPagina p, Window? owner = null)
        => _ = ImprimirResumenAsync(p);

    private static async Task ImprimirDetalleAsync(HVentasPagina p)
    {
        var doc = new PrintDocument { DocumentName = "Historial de Ventas - Detalle" };
        doc.DefaultPageSettings.Landscape = true;

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "Guardar PDF — Historial de Ventas (Detalle)",
            Filter           = "Archivo PDF (*.pdf)|*.pdf",
            DefaultExt       = "pdf",
            FileName         = $"HVentas_Detalle_{DateTime.Now:yyyyMMdd_HHmm}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (sfd.ShowDialog() != true) return;
        doc.PrinterSettings.PrinterName   = "Microsoft Print to PDF";
        doc.PrinterSettings.PrintFileName = sfd.FileName;
        doc.PrinterSettings.PrintToFile   = true;

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(p.LogoPath))
            try { logo = System.Drawing.Image.FromFile(p.LogoPath); } catch { }

        const int pgW = 1169, pgH = 827;
        var items      = HVentasDetalleDibujador.BuildItems(p);
        int totalPages = HVentasDetalleDibujador.ContarPaginas(items);
        var bitmaps    = new List<System.Drawing.Bitmap>();

        await Task.Run(() =>
        {
            int offset = 0;
            for (int pn = 1; pn <= totalPages; pn++)
            {
                var bmp = new System.Drawing.Bitmap(pgW, pgH);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.Clear(System.Drawing.Color.White);
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                HVentasDetalleDibujador.DibujarPagina(g, p, items, logo, pn, totalPages, ref offset);
                bitmaps.Add(bmp);
            }
        });
        logo?.Dispose();

        await PrintBitmaps(doc, bitmaps, sfd.FileName);
    }

    private static async Task ImprimirResumenAsync(HVentasPagina p)
    {
        var doc = new PrintDocument { DocumentName = "Historial de Ventas - Resumen" };
        doc.DefaultPageSettings.Landscape = false;

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "Guardar PDF — Historial de Ventas (Resumen)",
            Filter           = "Archivo PDF (*.pdf)|*.pdf",
            DefaultExt       = "pdf",
            FileName         = $"HVentas_Resumen_{DateTime.Now:yyyyMMdd_HHmm}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (sfd.ShowDialog() != true) return;
        doc.PrinterSettings.PrinterName   = "Microsoft Print to PDF";
        doc.PrinterSettings.PrintFileName = sfd.FileName;
        doc.PrinterSettings.PrintToFile   = true;

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(p.LogoPath))
            try { logo = System.Drawing.Image.FromFile(p.LogoPath); } catch { }

        const int pgW = 827, pgH = 1169;
        var items      = HVentasResumenDibujador.BuildItems(p);
        int totalPages = HVentasResumenDibujador.ContarPaginas(items);
        var bitmaps    = new List<System.Drawing.Bitmap>();

        await Task.Run(() =>
        {
            int offset = 0;
            for (int pn = 1; pn <= totalPages; pn++)
            {
                var bmp = new System.Drawing.Bitmap(pgW, pgH);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.Clear(System.Drawing.Color.White);
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                HVentasResumenDibujador.DibujarPagina(g, p, items, logo, pn, totalPages, ref offset);
                bitmaps.Add(bmp);
            }
        });
        logo?.Dispose();

        await PrintBitmaps(doc, bitmaps, sfd.FileName);
    }

    private static async Task PrintBitmaps(PrintDocument doc,
        List<System.Drawing.Bitmap> bitmaps, string pdfRuta)
    {
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
                g.DrawImage(bitmaps[pn], pb.X, pb.Y, pb.Width, pb.Height);
                pn++;
                e.HasMorePages = pn < bitmaps.Count;
            };
            doc.EndPrint += (_, _) => { foreach (var b in bitmaps) b.Dispose(); bitmaps.Clear(); };
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
