using System.Drawing.Printing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Maestros;

public class ArticuloFichaPreviewWindow : Window
{
    private readonly ArticuloFicha _ficha;
    private ScrollViewer _scroll  = null!;
    private TextBlock    _lblLoad = null!;

    public ArticuloFichaPreviewWindow(ArticuloFicha ficha)
    {
        _ficha                = ficha;
        Title                 = "Vista previa — Ficha de artículo";
        // Height fijo excedía el área de trabajo en pantallas de 1366x768 — ver comentario
        // equivalente en ClienteFichaImpresora.cs.
        Width                 = 1050;
        Height                = System.Math.Min(820, SystemParameters.WorkArea.Height - 20);
        MinWidth              = 700;
        MinHeight             = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = new SolidColorBrush(Color.FromRgb(55, 71, 79));
        FontFamily            = new System.Windows.Media.FontFamily("Segoe UI");
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
        btnP.Click += OnImprimir;
        btnC.Click += (_, _) => Close();
        row.Children.Add(btnP);
        row.Children.Add(btnC);
        row.Children.Add(new TextBlock
        {
            Text              = "Ficha de Artículo",
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
            Visibility          = Visibility.Visible,
        };
        var loadOverlay = new Grid();
        loadOverlay.Children.Add(_lblLoad);

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background  = new SolidColorBrush(Color.FromRgb(69, 90, 100)),
            Visibility  = Visibility.Collapsed,
        };

        var body = new Grid();
        body.Children.Add(loadOverlay);
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

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(_ficha.LogoPath))
            try { logo = System.Drawing.Image.FromFile(_ficha.LogoPath); } catch { }

        BitmapSource pagina;
        try
        {
            var f = _ficha;
            pagina = await Task.Run(() =>
            {
                var bmp = new System.Drawing.Bitmap(pgW, pgH);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.Clear(System.Drawing.Color.White);
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                ArticuloFichaDibujador.DibujarFicha(g, f, logo);
                var src = BmpToSource(bmp);
                bmp.Dispose();
                return src;
            });
        }
        finally
        {
            logo?.Dispose();
        }

        var shadow = new Border
        {
            Margin = new Thickness(0, 16, 0, 16),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = 10, Opacity = 0.5, ShadowDepth = 4,
            },
        };
        shadow.Child = new Border
        {
            Background      = Brushes.White,
            BorderBrush     = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            BorderThickness = new Thickness(1),
            Child           = new System.Windows.Controls.Image
            {
                Source  = pagina,
                Width   = pgW * 0.78,
                Height  = pgH * 0.78,
                Stretch = Stretch.Uniform,
            },
        };

        var panel = new StackPanel
        {
            Orientation         = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        panel.Children.Add(shadow);

        _scroll.Content     = panel;
        _scroll.Visibility  = Visibility.Visible;
        _lblLoad.Visibility = Visibility.Collapsed;
    }

    private void OnImprimir(object sender, RoutedEventArgs e)
    {
        var owner = Owner;
        Close();
        ImprimirFicha(_ficha, owner);
    }

    public static void ImprimirFicha(ArticuloFicha f, Window? owner = null)
        => _ = ImprimirFichaAsync(f, owner);

    private static async Task ImprimirFichaAsync(ArticuloFicha f, Window? owner)
    {
        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(f.LogoPath))
            try { logo = System.Drawing.Image.FromFile(f.LogoPath); } catch { }

        var doc = new PrintDocument { DocumentName = "Ficha Artículo" };
        doc.DefaultPageSettings.Landscape = false;

        bool esPdf = string.IsNullOrEmpty(f.Impresora)
                  || f.Impresora.Contains("PDF", StringComparison.OrdinalIgnoreCase)
                  || f.Impresora.Contains("XPS", StringComparison.OrdinalIgnoreCase);

        string? pdfRuta = null;
        if (esPdf)
        {
            doc.PrinterSettings.PrinterName = "Microsoft Print to PDF";
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title            = "Guardar PDF",
                Filter           = "Archivo PDF (*.pdf)|*.pdf",
                DefaultExt       = "pdf",
                FileName         = $"FichaArticulo_{f.Codigo}_{DateTime.Now:yyyyMMdd_HHmm}",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            if (sfd.ShowDialog() != true) { logo?.Dispose(); return; }
            pdfRuta = sfd.FileName;
            doc.PrinterSettings.PrintFileName = pdfRuta;
            doc.PrinterSettings.PrintToFile   = true;
        }
        else
        {
            doc.PrinterSettings.PrinterName = f.Impresora;
        }

        string? errorMsg = null;
        await Task.Run(() =>
        {
            doc.PrintPage += (_, e) =>
            {
                var g = e.Graphics!;
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
                ArticuloFichaDibujador.DibujarFicha(g, f, logo);
                e.HasMorePages = false;
            };
            doc.EndPrint += (_, _) => logo?.Dispose();
            try   { doc.Print(); }
            catch (Exception ex) { errorMsg = ex.Message; }
        });

        if (errorMsg != null)
            MessageBox.Show($"Error al imprimir:\n{errorMsg}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        else if (pdfRuta is { Length: > 0 })
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfRuta) { UseShellExecute = true }); } catch { }
    }

    private static BitmapSource BmpToSource(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.StreamSource = ms;
        bi.CacheOption  = BitmapCacheOption.OnLoad;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }
}
