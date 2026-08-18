using System.Drawing.Printing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Maestros;

// ── Preview ────────────────────────────────────────────────────────────────────
public class ClienteFichaPreviewWindow : Window
{
    private readonly ClienteFichaData _data;
    private ScrollViewer _scroll  = null!;
    private TextBlock    _lblLoad = null!;

    public ClienteFichaPreviewWindow(ClienteFichaData data)
    {
        _data                 = data;
        Title                 = $"Vista previa — {data.Cliente.NombreCliente}";
        // Height=820 fijo excedía el área de trabajo en pantallas de 1366x768 (~728-738px
        // útiles), dejando la ventana con su parte superior fuera de vista sin scroll que lo
        // compense — el ScrollViewer interno solo cubre el documento, no el encabezado/pie.
        Width                 = 920;
        Height                = System.Math.Min(820, SystemParameters.WorkArea.Height - 20);
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
        btnP.Click += (_, _) => { var o = Owner; Close(); ClienteFichaImpresora.Imprimir(_data, o); };
        btnC.Click += (_, _) => Close();
        row.Children.Add(btnP);
        row.Children.Add(btnC);
        row.Children.Add(new TextBlock
        {
            Text              = _data.Cliente.NombreCliente,
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
        BitmapSource? page = null;

        var d = _data;
        page = await Task.Run(() =>
        {
            var bmp = new System.Drawing.Bitmap(pgW, pgH);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            ClienteFichaDibujador.Dibujar(g, d);
            var src = BmpToSource(bmp);
            bmp.Dispose();
            return src;
        });

        var shadow = new Border
        {
            Margin = new Thickness(0, 16, 0, 16),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = Colors.Black, BlurRadius = 10, Opacity = 0.5, ShadowDepth = 4 },
        };
        shadow.Child = new Border
        {
            Background      = Brushes.White,
            BorderBrush     = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            BorderThickness = new Thickness(1),
            Child           = new System.Windows.Controls.Image
                { Source = page, Width = pgW * 0.78, Height = pgH * 0.78, Stretch = Stretch.Uniform },
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

// ── Impresora ──────────────────────────────────────────────────────────────────
public static class ClienteFichaImpresora
{
    public static void Imprimir(ClienteFichaData d, Window? owner = null)
        => _ = ImprimirAsync(d, owner);

    private static async Task ImprimirAsync(ClienteFichaData d, Window? owner)
    {
        var doc = new PrintDocument { DocumentName = $"FichaCliente_{d.Cliente.CiCliente}" };
        doc.DefaultPageSettings.Landscape = false;

        bool esPdf = true;   // siempre forzar PDF por defecto

        string? pdfRuta = null;
        if (esPdf)
        {
            doc.PrinterSettings.PrinterName = "Microsoft Print to PDF";
            var nombre = $"FichaCliente_{d.Cliente.CiCliente}_{DateTime.Now:yyyyMMdd_HHmm}";
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title            = "Guardar PDF",
                Filter           = "Archivo PDF (*.pdf)|*.pdf",
                DefaultExt       = "pdf",
                FileName         = nombre,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            if (sfd.ShowDialog() != true) return;
            pdfRuta = sfd.FileName;
            doc.PrinterSettings.PrintFileName = pdfRuta;
            doc.PrinterSettings.PrintToFile   = true;
        }

        // Renderizar a bitmap primero — idéntico al preview, inmune a DPI de impresora
        System.Drawing.Bitmap? pageBmp = null;
        await Task.Run(() =>
        {
            const int pgW = 827, pgH = 1169;
            pageBmp = new System.Drawing.Bitmap(pgW, pgH);
            using var g = System.Drawing.Graphics.FromImage(pageBmp);
            g.Clear(System.Drawing.Color.White);
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            ClienteFichaDibujador.Dibujar(g, d);
        });
        if (d.FotoDoc != null) d.FotoDoc.Dispose();

        string? errorMsg = null;
        await Task.Run(() =>
        {
            bool printed = false;
            doc.PrintPage += (_, e) =>
            {
                if (printed) { e.HasMorePages = false; return; }
                var g  = e.Graphics!;
                var pb = e.PageBounds;
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(pageBmp!, pb.X, pb.Y, pb.Width, pb.Height);
                printed = true;
                e.HasMorePages = false;
            };
            doc.EndPrint += (_, _) => { pageBmp?.Dispose(); pageBmp = null; };
            try   { doc.Print(); }
            catch (Exception ex) { errorMsg = ex.Message; }
        });

        if (errorMsg != null)
            MessageBox.Show($"Error al imprimir:\n{errorMsg}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        else if (pdfRuta is { Length: > 0 })
            try { System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(pdfRuta) { UseShellExecute = true }); } catch { }
    }
}
