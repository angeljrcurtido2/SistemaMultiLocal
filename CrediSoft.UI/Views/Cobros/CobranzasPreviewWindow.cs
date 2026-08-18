using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Cobros;

public class CobranzasPreviewWindow : Window
{
    private readonly CobranzasPagina _params;
    private ScrollViewer _scroll = null!;

    public CobranzasPreviewWindow(CobranzasPagina p)
    {
        _params               = p;
        Title                 = "Vista previa — Historial Cobranzas";
        // Height fijo excedía el área de trabajo en pantallas de 1366x768 — ver comentario
        // equivalente en ClienteFichaImpresora.cs.
        Width                 = 1260;
        Height                = System.Math.Min(820, SystemParameters.WorkArea.Height - 20);
        MinWidth              = 900;
        MinHeight             = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = new SolidColorBrush(Color.FromRgb(55, 71, 79));
        FontFamily            = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += (_, _) => RenderPaginas();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

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
            Text              = "Historial Cobranzas",
            Foreground        = Brushes.White,
            FontSize          = 13,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(20, 0, 0, 0)
        });
        toolbar.Child = row;
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

    private void RenderPaginas()
    {
        // Landscape A4: 1169 × 827
        const int pgW = 1169, pgH = 827;

        var panel = new StackPanel
        {
            Orientation         = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0, 16, 0, 16)
        };

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(_params.LogoPath))
            try { logo = System.Drawing.Image.FromFile(_params.LogoPath); } catch { }

        try
        {
            int totalPages = CobranzasDibujador.ContarPaginas(_params);
            int filaOffset = 0;
            int pageNum    = 0;

            while (filaOffset <= _params.Filas.Count)
            {
                pageNum++;

                var bmp = new System.Drawing.Bitmap(pgW, pgH);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.Clear(System.Drawing.Color.White);
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                bool hasMore = CobranzasDibujador.DibujarPagina(g, _params, logo, ref filaOffset, pageNum, totalPages);

                var src = BmpToSource(bmp);
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
                        Source  = src,
                        Width   = pgW * 0.88,
                        Height  = pgH * 0.88,
                        Stretch = Stretch.Uniform
                    }
                };
                panel.Children.Add(shadow);

                if (!hasMore) break;
                if (pageNum > 500) break;
            }
        }
        catch (Exception ex)
        {
            panel.Children.Add(new TextBlock
            {
                Text         = $"Error: {ex.Message}\n{ex.StackTrace}",
                Foreground   = Brushes.Red,
                Margin       = new Thickness(20),
                FontSize     = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }
        finally
        {
            logo?.Dispose();
        }

        _scroll.Content = panel;
    }

    private void OnImprimir(object sender, RoutedEventArgs e)
    {
        Close();
        CobranzasImpresora.Imprimir(_params);
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
