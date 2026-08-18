using CrediSoft.UI.Views.Shared;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Cobros;

// ─────────────────────────────────────────────────────────────────────────────
// Vista previa del comprobante de cobro antes de imprimir.
// Renderiza el mismo DrawingVisual que el PrintDocument imprime,
// escalado para caber en la ventana.  Formatos: 0=A4, 1=58mm, 2=40mm, 3=80mm.
// ─────────────────────────────────────────────────────────────────────────────
public class ComprobantePreviaWindow : Window
{
    private readonly DatosTicketCobro _datos;
    private readonly byte             _formato;

    // Canvas en píxeles lógicos. Ticket: usamos el ancho real del papel (la escala visual se ajusta aparte).
    private const int AltoTicket = 1200;
    private int AnchoPapel => _formato switch { 1 => 210, 2 => 145, 3 => 300, _ => 794 };
    private int AltoPapel  => _formato == 0 ? 1123 : AltoTicket;
    private static int AnchoMmDe(byte formato) => formato switch { 1 => 58, 2 => 40, 3 => 80, _ => 210 };

    private static SolidColorBrush B(string h) =>
        new((Color)ColorConverter.ConvertFromString(h));

    public ComprobantePreviaWindow(DatosTicketCobro datos, byte formato)
    {
        _datos   = datos;
        _formato = formato;

        var fmtLabel = formato switch { 1 => "Ticket 58 mm", 2 => "Ticket 40 mm", 3 => "Ticket 80 mm", _ => "A4 Normal" };
        Title                 = $"Vista previa — Comprobante de Cobro ({fmtLabel})";

        // Tamaño acotado a la resolucion de trabajo (min. 1280x720 en la mayoria de los equipos):
        // se calcula contra el area de trabajo real de la pantalla (SystemParameters.WorkArea,
        // que ya descuenta la barra de tareas), nunca un valor fijo que pueda no entrar.
        var maxW = SystemParameters.WorkArea.Width  * 0.85;
        var maxH = SystemParameters.WorkArea.Height * 0.85;
        Width                 = Math.Min(formato switch { 0 => 900, 3 => 620, _ => 480 }, maxW);
        Height                = Math.Min(760, maxH);
        MinWidth              = 360; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background            = B("#263238");
        FontFamily            = new FontFamily("Segoe UI");
        BuildUI();
        Loaded += (_, _) => Renderizar();
    }

    private Image   _imgPrevia  = null!;
    private TextBlock _lblFmt   = null!;

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── Barra superior ───────────────────────────────────────────────
        var bar = new Border {
            Background = B("#1A237E"), Padding = new Thickness(14, 10, 14, 10) };
        var barDp = new DockPanel();

        _lblFmt = new TextBlock {
            Foreground = B("#E3F2FD"), FontSize = 13, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(_lblFmt, Dock.Left); barDp.Children.Add(_lblFmt);

        var barBtns = new StackPanel { Orientation = Orientation.Horizontal };
        var btnImp = MakeBtn("🖨  Imprimir", "#1565C0");
        var btnPdf = MakeBtn("📄  Guardar PDF", "#37474F");
        var btnCer = MakeBtn("✖  Cerrar",  "#546E7A");
        btnImp.Click += async (_, _) => { await TicketPrinter.ImprimirCobroAsync(_datos); Close(); };
        btnPdf.Click += async (_, _) => { await ImprimirPdf(); };
        btnCer.Click += (_, _) => Close();
        barBtns.Children.Add(btnImp);
        barBtns.Children.Add(btnPdf);
        barBtns.Children.Add(btnCer);
        DockPanel.SetDock(barBtns, Dock.Right); barDp.Children.Add(barBtns);
        bar.Child = barDp;
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);

        // ── Área de preview ──────────────────────────────────────────────
        var scroll = new ScrollViewer {
            Background = B("#37474F"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto };

        _imgPrevia = new Image {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Top,
            Stretch = Stretch.Uniform };

        // Sombra alrededor del papel
        var sombra = new Border {
            Effect = new System.Windows.Media.Effects.DropShadowEffect {
                BlurRadius = 18, Opacity = 0.55, ShadowDepth = 6, Direction = 270,
                Color = Colors.Black },
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Top,
            Child = _imgPrevia };

        scroll.Content = sombra;
        root.Children.Add(scroll);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }

    private void Renderizar()
    {
        // Supersampling 3× para nitidez al reducir
        const double SS = 3.0;

        int pw = AnchoPapel;   // píxeles lógicos del papel
        int ph = AltoPapel;
        int bw = (int)(pw * SS);
        int bh = (int)(ph * SS);

        using var bmp = new System.Drawing.Bitmap(bw, bh);
        using var gfx = System.Drawing.Graphics.FromImage(bmp);
        gfx.ScaleTransform((float)SS, (float)SS);
        gfx.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        gfx.Clear(System.Drawing.Color.White);

        // Dibujamos sin anchoOverride: usamos el mismo código que al imprimir en papel real.
        // El escalado visual se hace al mostrar la imagen, no en el dibujo.
        switch (_formato)
        {
            case 1:
            case 2:
            case 3: TicketPrinter.DibujarCobroTicketPublic(gfx, _datos, AnchoMmDe(_formato)); break;
            default: TicketPrinter.DibujarCobroA4Public(gfx, _datos); break;
        }

        using var ms = new System.IO.MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Seek(0, System.IO.SeekOrigin.Begin);
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption  = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();

        // Ticket: mostramos a escala fija ~2× para que se vea como un ticket real pero legible.
        // A4: reducir para caber en ventana, sin pasar de 1:1.
        double dispW  = Math.Max(ActualWidth - 96, 200);
        double escVis = _formato == 0
            ? Math.Min(1.0, dispW / pw)
            : Math.Min(2.0, dispW / pw);

        _imgPrevia.Source = bi;
        _imgPrevia.Width  = pw * escVis;
        _imgPrevia.Height = ph * escVis;
        RenderOptions.SetBitmapScalingMode(_imgPrevia, BitmapScalingMode.HighQuality);

        var fmtLabel = _formato switch { 1 => "Ticket 58 mm", 2 => "Ticket 40 mm", 3 => "Ticket 80 mm", _ => "A4 Normal" };
        _lblFmt.Text = $"🖨  Vista previa — {fmtLabel}";
    }

    private async Task ImprimirPdf()
    {
        // Fuerza salida a PDF sin preguntar
        var doc = new System.Drawing.Printing.PrintDocument { DocumentName = "Comprobante_Cobro" };
        doc.PrintPage += (_, e) => {
            e.HasMorePages = false;
            switch (_formato) {
                case 1:
                case 2:
                case 3: TicketPrinter.DibujarCobroTicketPublic(e.Graphics!, _datos, AnchoMmDe(_formato)); break;
                default: TicketPrinter.DibujarCobroA4Public(e.Graphics!, _datos);                          break;
            }
        };
        await Task.Run(() => TicketPrinter.ImprimirComoPdf(doc));
        Close();
    }

    private static Button MakeBtn(string txt, string bg) => new Button {
        Content = txt, Height = 32, Padding = new Thickness(16,0,16,0),
        Margin = new Thickness(6,0,0,0), Background = B(bg),
        Foreground = Brushes.White, BorderThickness = new Thickness(0),
        Cursor = System.Windows.Input.Cursors.Hand,
        FontSize = 12, FontWeight = FontWeights.SemiBold };
}
