using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Informes;

// Vista previa del reporte "tabla plana, todos los locales, horizontal" — mismo mecanismo
// que ArticulosListadoPreviewWindow, adaptado a página apaisada (1169x827) y al dibujador
// ArticulosListadoTablaDibujador en vez de ArticulosListadoDibujador.
public class ArticulosListadoTablaPreviewWindow : Window
{
    private readonly ArticulosListadoTablaPagina _params;
    private ScrollViewer _scroll  = null!;
    private TextBlock    _lblLoad = null!;

    public ArticulosListadoTablaPreviewWindow(ArticulosListadoTablaPagina p)
    {
        _params               = p;
        Title                 = "Vista previa — Ver Artículos (todos los locales)";
        Width                 = 1100;
        Height                = 700;
        MinWidth              = 700;
        MinHeight             = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = new SolidColorBrush(Color.FromRgb(60, 72, 84));
        FontFamily            = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await RenderPaginasAsync();
    }

    private void BuildUI()
    {
        var root    = new DockPanel();
        var toolbar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x2F, 0x44)),
            Padding    = new Thickness(14, 8, 14, 8)
        };
        DockPanel.SetDock(toolbar, Dock.Top);

        var row  = new StackPanel { Orientation = Orientation.Horizontal };
        var btnP = Btn("🖨  Imprimir", "#1565C0");
        var btnC = Btn("✕  Cerrar",   "#546E7A");
        btnP.Click += OnImprimir;
        btnC.Click += (_, _) => Close();
        row.Children.Add(btnP);
        row.Children.Add(btnC);
        row.Children.Add(new TextBlock
        {
            Text              = "Ver Artículos — Todos los locales",
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
            Visibility          = Visibility.Visible,
        };

        var loadOverlay = new Grid();
        loadOverlay.Children.Add(_lblLoad);

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background  = new SolidColorBrush(Color.FromRgb(74, 90, 106)),
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
            Margin          = new Thickness(0, 0, 8, 0)
        };
    }

    // Mismo límite y mismo motivo que ArticulosListadoPreviewWindow.MaxPaginasPreview.
    private const int MaxPaginasPreview = 60;

    private async Task RenderPaginasAsync()
    {
        int pgW = ArticulosListadoTablaDibujador.PgWDe(_params.TodosLosLocales);
        int pgH = ArticulosListadoTablaDibujador.PgHDe(_params.TodosLosLocales);

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(_params.LogoPath))
            try { logo = System.Drawing.Image.FromFile(_params.LogoPath); } catch { }

        List<BitmapSource> paginas;
        int totalPaginasReales;
        bool truncado;
        try
        {
            var p = _params;
            (paginas, totalPaginasReales, truncado) = await Task.Run(() =>
            {
                var result     = new List<BitmapSource>();
                int totalPages = ArticulosListadoTablaDibujador.ContarPaginas(p);
                int filaOffset = 0;
                int pageNum    = 0;
                bool seTrunco  = false;

                while (filaOffset <= p.Filas.Count)
                {
                    pageNum++;
                    var bmp = new System.Drawing.Bitmap(pgW, pgH);
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.Clear(System.Drawing.Color.White);
                    g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                    bool hasMore = ArticulosListadoTablaDibujador.DibujarPagina(g, p, logo, ref filaOffset, pageNum, totalPages);

                    result.Add(BmpToSource(bmp));
                    bmp.Dispose();

                    if (!hasMore) break;
                    if (pageNum >= MaxPaginasPreview) { seTrunco = hasMore; break; }
                }
                return (result, totalPages, seTrunco);
            });
        }
        finally
        {
            logo?.Dispose();
        }

        var panel = new StackPanel
        {
            Orientation         = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0, 16, 0, 16)
        };

        foreach (var src in paginas)
        {
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
                    Source  = src,
                    Width   = pgW * 0.7,
                    Height  = pgH * 0.7,
                    Stretch = Stretch.Uniform
                }
            };
            panel.Children.Add(shadow);
        }

        if (truncado)
        {
            panel.Children.Add(new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(0x0E, 0x2F, 0x44)),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(20, 14, 20, 14),
                Width           = pgW * 0.7,
                Margin          = new Thickness(0, 4, 6, 16),
                Child = new TextBlock
                {
                    Text = $"Vista previa limitada a las primeras {MaxPaginasPreview} de {totalPaginasReales} páginas " +
                           "para no consumir memoria en exceso. Usá \"Imprimir\" para generar el listado completo.",
                    Foreground   = Brushes.White,
                    FontSize     = 12.5,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                },
            });
        }

        _scroll.Content    = panel;
        _scroll.Visibility = Visibility.Visible;
        _lblLoad.Visibility = Visibility.Collapsed;
    }

    private void OnImprimir(object sender, RoutedEventArgs e)
    {
        var owner = Owner;
        Close();
        ArticulosListadoTablaImpresora.Imprimir(_params, owner);
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
