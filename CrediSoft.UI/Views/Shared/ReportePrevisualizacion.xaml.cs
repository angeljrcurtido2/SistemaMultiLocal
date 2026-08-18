using System.Drawing.Printing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Shared;

public class ReportePrevisualizacion : Window
{
    private readonly PrintDocument _doc;
    private readonly string        _impresora;
    private readonly bool          _landscape;
    private ScrollViewer           _scroll = null!;

    public ReportePrevisualizacion(PrintDocument doc, string impresora)
    {
        _doc       = doc;
        _impresora = impresora;
        try { _landscape = doc.DefaultPageSettings.Landscape; } catch { _landscape = false; }

        Title                 = $"Vista previa — {doc.DocumentName}";
        // Height fijo excedía el área de trabajo en pantallas de 1366x768 — ver comentario
        // equivalente en ClienteFichaImpresora.cs.
        Width                 = 1050;
        Height                = System.Math.Min(800, SystemParameters.WorkArea.Height - 20);
        MinWidth              = 700;
        MinHeight             = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = new SolidColorBrush(Color.FromRgb(55, 71, 79));
        FontFamily            = new FontFamily("Segoe UI");

        BuildUI();
        Loaded += (_, _) => RenderizarPaginas();
    }

    private void BuildUI()
    {
        var root    = new DockPanel();
        var toolbar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(38, 50, 56)),
            Padding    = new Thickness(14, 8, 14, 8)
        };
        DockPanel.SetDock(toolbar, Dock.Top);

        var tbRow       = new StackPanel { Orientation = Orientation.Horizontal };
        var btnImprimir = MkBtn("🖨  Imprimir", "#1B5E20");
        var btnCerrar   = MkBtn("✕  Cerrar",   "#B71C1C");
        btnImprimir.Click += OnImprimir;
        btnCerrar.Click   += (_, _) => Close();

        tbRow.Children.Add(btnImprimir);
        tbRow.Children.Add(btnCerrar);
        tbRow.Children.Add(new TextBlock
        {
            Text              = _doc.DocumentName,
            Foreground        = Brushes.White,
            FontSize          = 13,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(20, 0, 0, 0)
        });
        toolbar.Child = tbRow;
        root.Children.Add(toolbar);

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(69, 90, 100))
        };
        root.Children.Add(_scroll);
        Content = root;
    }

    private static Button MkBtn(string txt, string hex)
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
            Margin          = new Thickness(0, 0, 8, 0)
        };
    }

    private void RenderizarPaginas()
    {
        var panel = new StackPanel
        {
            Orientation         = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0, 16, 0, 16)
        };

        try
        {
            int pgW = _landscape ? 1169 : 827;
            int pgH = _landscape ? 827  : 1169;

            // Usar el evento PrintPage directamente sin reflection de OnPrintPage
            bool hasMore = true;
            int  pageNum = 0;

            // Resetear el contador del doc invocando BeginPrint
            _doc.BeginPrint += DocBeginPrint;
            var onBeginPrint = typeof(PrintDocument).GetMethod("OnBeginPrint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            onBeginPrint?.Invoke(_doc, new object[] { new PrintEventArgs() });
            _doc.BeginPrint -= DocBeginPrint;

            var onPrintPage = typeof(PrintDocument).GetMethod("OnPrintPage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            while (hasMore && pageNum < 500)
            {
                // Bitmap en memoria — NO usar SetResolution para evitar problemas de DPI
                var bmp = new System.Drawing.Bitmap(pgW, pgH);
                var gfx = System.Drawing.Graphics.FromImage(bmp);
                gfx.Clear(System.Drawing.Color.White);
                gfx.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
                gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                var pageRect   = new System.Drawing.Rectangle(0, 0, pgW, pgH);
                var marginRect = new System.Drawing.Rectangle(20, 20, pgW - 40, pgH - 40);

                // PageSettings vacío — no accedemos a propiedades que requieran impresora
                var pageArgs = new PrintPageEventArgs(gfx, marginRect, pageRect,
                    new System.Drawing.Printing.PageSettings());

                onPrintPage?.Invoke(_doc, new object[] { pageArgs });

                hasMore = pageArgs.HasMorePages;
                pageNum++;

                var imgSrc = BmpToSource(bmp);
                gfx.Dispose();
                bmp.Dispose();

                var shadow = new Border
                {
                    Margin = new Thickness(0, 0, 6, 16),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black, BlurRadius = 10, Opacity = 0.5, ShadowDepth = 4
                    }
                };
                shadow.Child = new Border
                {
                    Background      = Brushes.White,
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    BorderThickness = new Thickness(1),
                    Child           = new System.Windows.Controls.Image
                    {
                        Source  = imgSrc,
                        Width   = pgW * 0.78,
                        Height  = pgH * 0.78,
                        Stretch = Stretch.Uniform
                    }
                };
                panel.Children.Add(shadow);
            }
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            var msg   = $"{inner.GetType().Name}: {inner.Message}\n\n{inner.StackTrace}";
            try { System.IO.File.WriteAllText(@"C:\Temp\preview_error.txt", msg); } catch { }
            panel.Children.Add(new TextBlock
            {
                Text         = msg,
                Foreground   = Brushes.Red,
                Margin       = new Thickness(20),
                FontSize     = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }

        _scroll.Content = panel;
    }

    private static void DocBeginPrint(object sender, PrintEventArgs e) { }

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

    private void OnImprimir(object sender, RoutedEventArgs e)
    {
        Close();
        TicketPrinter.ImprimirConConfig(_doc, _impresora);
    }
}
