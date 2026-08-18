using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Ventas;

// ─────────────────────────────────────────────────────────────────────────────
// Se muestra al entrar por primera vez a los campos Recargo/Descuento durante
// una Venta Contado — mismo patrón que ConfirmarReajusteDialog (Cobros), creado
// por el mismo motivo: un vendedor puede confundir estos campos con el precio
// final o con el efectivo recibido y tipear ahí por error, alterando sin darse
// cuenta cuánto termina pagando el cliente.
// ─────────────────────────────────────────────────────────────────────────────
public class ConfirmarAjustePrecioDialog : Window
{
    public bool QuiereAjustar { get; private set; } = false;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public ConfirmarAjustePrecioDialog(bool esDescuento)
    {
        var titulo    = esDescuento ? "¿Querés aplicar un descuento?" : "¿Querés aplicar un recargo?";
        var sub       = esDescuento ? "Este campo (descuento) baja el precio del artículo" : "Este campo (recargo) sube el precio del artículo";
        var colorHdr  = esDescuento ? "#C62828" : "#1565C0";
        var colorSub  = esDescuento ? "#FFCDD2" : "#90CAF9";

        Title                 = esDescuento ? "Descuento de artículo" : "Recargo de artículo";
        Width                 = 460;
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
            Background = B(colorHdr),
            Padding    = new Thickness(24, 18, 24, 18) };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = esDescuento ? "🏷️" : "💰", FontSize = 22, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        var headerTextSp = new StackPanel();
        headerTextSp.Children.Add(new TextBlock {
            Text       = titulo,
            Foreground = Brushes.White,
            FontSize   = 15,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin     = new Thickness(0, 0, 0, 4) });
        headerTextSp.Children.Add(new TextBlock {
            Text       = sub,
            Foreground = B(colorSub),
            FontSize   = 11 });
        headerSp.Children.Add(headerTextSp);
        header.Child = headerSp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        body.Children.Add(new TextBlock {
            Text = esDescuento
                ? "El Descuento NO es el campo para anotar el precio final del artículo — se " +
                  "escribe cuánto se RESTA (en Gs.) al precio contado que ya aparece cargado. " +
                  "El precio final se recalcula solo y se muestra arriba, en \"Precio contado-contado\"."
                : "El Recargo NO es el campo para anotar el precio final del artículo — se " +
                  "escribe cuánto se SUMA (en Gs.) al precio contado que ya aparece cargado. " +
                  "El precio final se recalcula solo y se muestra arriba, en \"Precio contado-contado\".",
            Foreground = B("#37474F"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
            LineHeight = 19, Margin = new Thickness(0, 0, 0, 14) });
        body.Children.Add(new TextBlock {
            Text = esDescuento
                ? "¿Estás seguro de que necesitás bajar el precio de este artículo?"
                : "¿Estás seguro de que necesitás subir el precio de este artículo?",
            Foreground = B(colorHdr), FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap, LineHeight = 19 });

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

        var btnNo = new Button {
            Content         = "No, dejar el precio normal",
            Height          = 38,
            Padding         = new Thickness(14, 0, 14, 0),
            Margin          = new Thickness(0, 0, 10, 0),
            Background      = B("#1565C0"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.Bold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnNo.Click += (_, _) => { QuiereAjustar = false; Close(); };

        var btnSi = new Button {
            Content         = esDescuento ? "Sí, quiero descontar" : "Sí, quiero recargar",
            Height          = 38,
            Padding         = new Thickness(14, 0, 14, 0),
            Background      = B("#546E7A"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.SemiBold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnSi.Click += (_, _) => { QuiereAjustar = true; Close(); };

        KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Escape) { QuiereAjustar = false; Close(); }
        };

        footerSp.Children.Add(btnSi);
        footerSp.Children.Add(btnNo);
        footer.Child = footerSp;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
    }
}
