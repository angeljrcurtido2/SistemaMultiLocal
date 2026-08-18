using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Maestros;

public class ArticulosPreviewWindow : Window
{
    private readonly ArticulosPagina _params;
    private ScrollViewer _scroll  = null!;
    private TextBlock    _lblLoad = null!;

    public ArticulosPreviewWindow(ArticulosPagina p)
    {
        _params               = p;
        Title                 = "Vista previa — Artículos";
        Width                 = 1020;
        Height                = 680;
        MinWidth              = 700;
        MinHeight             = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = new SolidColorBrush(Color.FromRgb(55, 71, 79));
        FontFamily            = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await RenderPaginasAsync();
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

        var row  = new StackPanel { Orientation = Orientation.Horizontal };
        var btnP = Btn("Imprimir", "#1B5E20");
        var btnC = Btn("Cerrar",   "#B71C1C");
        btnP.Click += OnImprimir;
        btnC.Click += (_, _) => Close();
        row.Children.Add(btnP);
        row.Children.Add(btnC);
        row.Children.Add(new TextBlock
        {
            Text              = "Artículos / Mercaderías",
            Foreground        = Brushes.White,
            FontSize          = 13,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(20, 0, 0, 0)
        });
        toolbar.Child = row;
        root.Children.Add(toolbar);

        // Indicador de carga centrado
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
            Margin          = new Thickness(0, 0, 8, 0)
        };
    }

    private async Task RenderPaginasAsync()
    {
        const int pgW = 827, pgH = 1169;

        // Dibujar bitmaps en thread background para no bloquear el UI
        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(_params.LogoPath))
            try { logo = System.Drawing.Image.FromFile(_params.LogoPath); } catch { }

        List<BitmapSource> paginas;
        try
        {
            var p = _params;
            paginas = await Task.Run(() =>
            {
                var result     = new List<BitmapSource>();
                int totalPages = ArticulosDibujador.ContarPaginas(p);
                int filaOffset = 0;
                int pageNum    = 0;

                while (filaOffset <= p.Filas.Count)
                {
                    pageNum++;
                    var bmp = new System.Drawing.Bitmap(pgW, pgH);
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.Clear(System.Drawing.Color.White);
                    g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                    bool hasMore = ArticulosDibujador.DibujarPagina(g, p, logo, ref filaOffset, pageNum, totalPages);

                    result.Add(BmpToSource(bmp));
                    bmp.Dispose();

                    if (!hasMore) break;
                    if (pageNum > 500) break;
                }
                return result;
            });
        }
        finally
        {
            logo?.Dispose();
        }

        // Construir el panel en el UI thread (ya estamos en Dispatcher por el await)
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
                    Width   = pgW * 0.78,
                    Height  = pgH * 0.78,
                    Stretch = Stretch.Uniform
                }
            };
            panel.Children.Add(shadow);
        }

        _scroll.Content    = panel;
        _scroll.Visibility = Visibility.Visible;
        _lblLoad.Visibility = Visibility.Collapsed;
    }

    private void OnImprimir(object sender, RoutedEventArgs e)
    {
        var owner = Owner;
        Close();
        ArticulosImpresora.Imprimir(_params, owner);
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
