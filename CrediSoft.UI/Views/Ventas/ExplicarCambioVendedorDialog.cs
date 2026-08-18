using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Ventas;

// ─────────────────────────────────────────────────────────────────────────────
// Se muestra al pulsar "CAMBIAR" en el badge "VENDIDO POR" de Venta Contado —
// antes de pedir las credenciales del vendedor externo. Explica con claridad qué
// pasa con la comisión y con el efectivo, para que no quede como una acción
// misteriosa: mismo motivo que llevó a explicar el botón equivalente en Cobros.
// ─────────────────────────────────────────────────────────────────────────────
public class ExplicarCambioVendedorDialog : Window
{
    public bool QuiereContinuar { get; private set; } = false;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public ExplicarCambioVendedorDialog()
    {
        Title                 = "Vender a nombre de otro vendedor";
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
            Text = "🧑‍💼", FontSize = 22, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        var headerTextSp = new StackPanel();
        headerTextSp.Children.Add(new TextBlock {
            Text       = "¿Vender a nombre de otro vendedor?",
            Foreground = Brushes.White,
            FontSize   = 15,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin     = new Thickness(0, 0, 0, 4) });
        headerTextSp.Children.Add(new TextBlock {
            Text       = "Útil cuando un vendedor externo no tiene caja propia abierta",
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
            Text = "Al elegir otro vendedor para esta venta, se separan dos cosas:",
            Foreground = B("#37474F"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,0,0,14)
        });

        body.Children.Add(Punto("💰", "La COMISIÓN va para el vendedor que ingreses",
            "El total de esta venta se suma a las ventas de ESE vendedor a fin de mes, no a las tuyas.",
            "#1565C0"));

        body.Children.Add(Punto("🗄️", "El EFECTIVO queda en tu propia caja",
            "El dinero sigue entrando en la caja de este local, la que vos abriste — no se abre " +
            "ni se mueve ninguna caja del vendedor que ingreses. Vos seguís siendo responsable de " +
            "ese efectivo en el arqueo.",
            "#2E7D32"));

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
            Content         = "Entendido, continuar",
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
