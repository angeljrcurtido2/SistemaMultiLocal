using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Cobros;

// ─────────────────────────────────────────────────────────────────────────────
// Aviso de caja cerrada al intentar cobrar, vender, o cualquier otra operación que
// requiera caja abierta. Ofrece ir directo a Apertura de Caja en vez de solo
// informar el error y dejar al usuario buscar el módulo a mano. Se reutiliza desde
// varios módulos (Cobros, Venta Contado, Venta a Crédito, etc.) — "accion" describe
// puntualmente qué se estaba intentando hacer, para que el texto no diga siempre
// "cobrar una cuota" aunque en realidad se esté vendiendo.
// ─────────────────────────────────────────────────────────────────────────────
public class CajaCerradaDialog : Window
{
    public bool IrAAbrirCaja { get; private set; } = false;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public CajaCerradaDialog(string nombreLocal, string accion = "cobrar una cuota")
    {
        Title                 = "Caja cerrada";
        Width                 = 420;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#F5F7FA");
        FontFamily            = new FontFamily("Segoe UI");

        Effect = new DropShadowEffect {
            BlurRadius = 24, Opacity = 0.3, ShadowDepth = 8,
            Direction = 270, Color = Colors.Black };

        var root = new DockPanel();

        // ── Encabezado ───────────────────────────────────────────────────────
        var header = new Border {
            Background = B("#C62828"),
            Padding    = new Thickness(24, 18, 24, 18) };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "🔒", FontSize = 22, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0) });
        var headerTxt = new StackPanel();
        headerTxt.Children.Add(new TextBlock {
            Text = "CAJA CERRADA", Foreground = Brushes.White,
            FontSize = 16, FontWeight = FontWeights.Bold });
        headerTxt.Children.Add(new TextBlock {
            Text = $"No hay caja abierta en {nombreLocal}", Foreground = B("#FFCDD2"), FontSize = 11 });
        headerSp.Children.Add(headerTxt);
        header.Child = headerSp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Cuerpo ───────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 4) };
        body.Children.Add(new TextBlock {
            Text = $"Para poder {accion}, primero hay que abrir la caja de este local.",
            Foreground = B("#37474F"), FontSize = 13, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8) });
        body.Children.Add(new TextBlock {
            Text = "¿Deseás ir al módulo de Apertura de Caja ahora?",
            Foreground = B("#0E2F44"), FontSize = 13, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        // ── Barra de botones ─────────────────────────────────────────────────
        var footer = new Border {
            Background      = B("#F5F7FA"),
            BorderBrush     = B("#E0E6EF"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding         = new Thickness(24, 14, 24, 14) };
        var footerSp = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };

        var btnNo = new Button {
            Content         = "No, cerrar",
            Width           = 120, Height = 38,
            Margin          = new Thickness(0, 0, 10, 0),
            Background      = B("#546E7A"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.SemiBold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnNo.Click += (_, _) => { IrAAbrirCaja = false; Close(); };

        var btnSi = new Button {
            Content         = "Sí, abrir caja",
            Width           = 140, Height = 38,
            Background      = B("#1565C0"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.Bold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnSi.Click += (_, _) => { IrAAbrirCaja = true; Close(); };

        KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Enter)  { IrAAbrirCaja = true;  Close(); }
            if (e.Key == System.Windows.Input.Key.Escape) { IrAAbrirCaja = false; Close(); }
        };

        footerSp.Children.Add(btnNo);
        footerSp.Children.Add(btnSi);
        footer.Child = footerSp;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
    }
}
