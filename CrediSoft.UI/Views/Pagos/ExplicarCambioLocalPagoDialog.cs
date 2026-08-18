using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Pagos;

// ─────────────────────────────────────────────────────────────────────────────
// Se muestra al pulsar "LOCAL DE PAGO" en Pago de Salarios y Comisiones — antes
// de abrir el selector de local. Explica con claridad qué implica cambiarlo: de
// qué caja física sale el efectivo, y que puede no coincidir con el local de base
// del funcionario (ej. vendió como externo en otra sucursal). Mismo patrón que
// ExplicarCambioVendedorDialog en Venta Contado.
// ─────────────────────────────────────────────────────────────────────────────
public class ExplicarCambioLocalPagoDialog : Window
{
    public bool QuiereContinuar { get; private set; } = false;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public ExplicarCambioLocalPagoDialog()
    {
        Title                 = "Cambiar local de pago";
        Width                 = 480;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#F5F7FA");
        FontFamily            = new FontFamily("Segoe UI");

        Effect = new DropShadowEffect {
            BlurRadius = 24, Opacity = 0.3, ShadowDepth = 8,
            Direction = 270, Color = Colors.Black };

        var root = new DockPanel();

        var header = new Border {
            Background = B("#1565C0"),
            Padding    = new Thickness(24, 18, 24, 18) };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "🏢", FontSize = 22, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        var headerTextSp = new StackPanel();
        headerTextSp.Children.Add(new TextBlock {
            Text       = "¿Cambiar el local de pago?",
            Foreground = Brushes.White,
            FontSize   = 15,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin     = new Thickness(0, 0, 0, 4) });
        headerTextSp.Children.Add(new TextBlock {
            Text       = "Define de qué caja física sale el efectivo de este pago",
            Foreground = B("#90CAF9"),
            FontSize   = 11, TextWrapping = TextWrapping.Wrap });
        headerSp.Children.Add(headerTextSp);
        header.Child = headerSp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        Border Punto(string icono, string titulo, string detalle, string colorTitulo)
        {
            var b = new Border {
                Background = Brushes.White, BorderBrush = B("#E0E0E0"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14,10,14,10), Margin = new Thickness(0,0,0,10)
            };
            var sp = new StackPanel();
            var hdrSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,4) };
            hdrSp.Children.Add(new TextBlock { Text = icono, FontSize = 14, Margin = new Thickness(0,0,8,0) });
            hdrSp.Children.Add(new TextBlock { Text = titulo, FontSize = 12.5, FontWeight = FontWeights.Bold,
                Foreground = B(colorTitulo) });
            sp.Children.Add(hdrSp);
            sp.Children.Add(new TextBlock { Text = detalle, FontSize = 12, Foreground = B("#546E7A"),
                TextWrapping = TextWrapping.Wrap, LineHeight = 18 });
            b.Child = sp;
            return b;
        }

        body.Children.Add(new TextBlock {
            Text = "Este botón NO cambia el local del funcionario ni sus datos — solo elige de " +
                   "qué caja sale el efectivo de este pago puntual:",
            Foreground = B("#37474F"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,0,0,14)
        });

        body.Children.Add(Punto("🗄️", "El EFECTIVO sale de la caja del local que elijas",
            "Se registra un egreso real en esa caja (Explorador de Caja / Cierre de Caja la " +
            "van a ver ahí), sea o no el local donde el funcionario trabaja habitualmente.",
            "#2E7D32"));

        body.Children.Add(Punto("👤", "Usalo cuando el funcionario vendió/cobró en otro local",
            "Por ejemplo, si vendió como vendedor externo en una sucursal distinta a la suya y " +
            "esa es la caja que está abierta — la comisión se calcula igual sin importar el " +
            "local, pero el pago necesita una caja real para salir.",
            "#1565C0"));

        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        var footer = new Border {
            Background = B("#EEF4FB"),
            BorderBrush = B("#BBDEFB"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 14, 24, 14) };
        var footerSp = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };

        var btnCancelar = new Button {
            Content         = "Cancelar",
            Height          = 38,
            Padding         = new Thickness(14, 0, 14, 0),
            Margin          = new Thickness(0, 0, 10, 0),
            Background      = Brushes.Transparent,
            Foreground      = B("#546E7A"),
            BorderThickness = new Thickness(1),
            BorderBrush     = B("#B0BEC5"),
            FontSize        = 13,
            FontWeight      = FontWeights.SemiBold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnCancelar.Click += (_, _) => { QuiereContinuar = false; Close(); };

        var btnContinuar = new Button {
            Content         = "Entendido, elegir local",
            Height          = 38,
            Padding         = new Thickness(14, 0, 14, 0),
            Background      = B("#1565C0"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.Bold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnContinuar.Click += (_, _) => { QuiereContinuar = true; Close(); };

        KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Escape) { QuiereContinuar = false; Close(); }
        };

        footerSp.Children.Add(btnContinuar);
        footerSp.Children.Add(btnCancelar);
        footer.Child = footerSp;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
    }
}
