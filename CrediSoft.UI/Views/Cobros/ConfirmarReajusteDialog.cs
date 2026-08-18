using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Cobros;

// ─────────────────────────────────────────────────────────────────────────────
// Se muestra al entrar por primera vez al campo Reajuste durante un cobro — un
// cajero llegó a tipear ahí el efectivo recibido por error, sumando ese monto
// entero al total real de la cuota. Pregunta con tono amigable, no acusatorio,
// si realmente quiere aumentar la cuota o si buscaba el campo de pago.
// ─────────────────────────────────────────────────────────────────────────────
public class ConfirmarReajusteDialog : Window
{
    public bool QuiereReajustar { get; private set; } = false;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public ConfirmarReajusteDialog()
    {
        Title                 = "Reajuste de cuota";
        Width                 = 440;
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
            Text = "💬", FontSize = 22, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        var headerTextSp = new StackPanel();
        headerTextSp.Children.Add(new TextBlock {
            Text       = "¿Querés hacer un reajuste?",
            Foreground = Brushes.White,
            FontSize   = 15,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin     = new Thickness(0, 0, 0, 4) });
        headerTextSp.Children.Add(new TextBlock {
            Text       = "Este campo (recargo) se usa para aumentar el monto de la cuota",
            Foreground = B("#90CAF9"),
            FontSize   = 11 });
        headerSp.Children.Add(headerTextSp);
        header.Child = headerSp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        body.Children.Add(new TextBlock {
            Text = "El Reajuste NO es el campo para anotar el efectivo que recibiste — " +
                   "eso va en \"Efectivo recibido\", a la derecha (resaltado en verde). " +
                   "Este campo se suma al total de la cuota y aumenta lo que el cliente debe pagar.",
            Foreground = B("#37474F"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
            LineHeight = 19, Margin = new Thickness(0, 0, 0, 14) });
        body.Children.Add(new TextBlock {
            Text = "¿Estás seguro de que no querías ingresar un pago y realmente necesitás " +
                   "aumentar el monto de esta cuota?",
            Foreground = B("#0D47A1"), FontSize = 12.5, FontWeight = FontWeights.SemiBold,
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
            Content         = "No, quiero ingresar un pago",
            Height          = 38,
            Padding         = new Thickness(14, 0, 14, 0),
            Margin          = new Thickness(0, 0, 10, 0),
            Background      = B("#1565C0"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.Bold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnNo.Click += (_, _) => { QuiereReajustar = false; Close(); };

        var btnSi = new Button {
            Content         = "Sí, quiero reajustar",
            Height          = 38,
            Padding         = new Thickness(14, 0, 14, 0),
            Background      = B("#546E7A"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.SemiBold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnSi.Click += (_, _) => { QuiereReajustar = true; Close(); };

        KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Escape) { QuiereReajustar = false; Close(); }
        };

        footerSp.Children.Add(btnSi);
        footerSp.Children.Add(btnNo);
        footer.Child = footerSp;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
    }
}
