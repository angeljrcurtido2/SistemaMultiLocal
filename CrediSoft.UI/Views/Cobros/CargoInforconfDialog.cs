using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Cobros;

// ─────────────────────────────────────────────────────────────────────────────
// El cliente ya esta reportado en Informconf (CLIENTES.INFORCOM = 1). Pregunta
// si corresponde aplicar el cargo fijo de gestion en esta cuota, en vez de
// asumirlo automaticamente — confirmado con evidencia historica que esto es
// una decision del cajero caso por caso, no una regla automatica del sistema.
// ─────────────────────────────────────────────────────────────────────────────
public class CargoInforconfDialog : Window
{
    public bool AplicarCargo { get; private set; } = false;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public CargoInforconfDialog(string nombreCliente, decimal montoCargo)
    {
        Title                 = "Cliente reportado en Informconf";
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
            Background = B("#E65100"),
            Padding    = new Thickness(24, 18, 24, 18) };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "⚠", FontSize = 22, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0) });
        var headerTxt = new StackPanel();
        headerTxt.Children.Add(new TextBlock {
            Text = "CLIENTE REPORTADO", Foreground = Brushes.White,
            FontSize = 16, FontWeight = FontWeights.Bold });
        headerTxt.Children.Add(new TextBlock {
            Text = nombreCliente, Foreground = B("#FFE0B2"), FontSize = 11 });
        headerSp.Children.Add(headerTxt);
        header.Child = headerSp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Cuerpo ───────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 4) };
        body.Children.Add(new TextBlock {
            Text = "Este cliente ya está reportado en Informconf.",
            Foreground = B("#37474F"), FontSize = 13, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12) });

        var montoBorder = new Border {
            Background   = B("#FFF3E0"),
            BorderBrush  = B("#FFCC80"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(16, 12, 16, 12),
            Margin       = new Thickness(0, 0, 0, 12) };
        var montoDp = new DockPanel();
        montoDp.Children.Add(new TextBlock {
            Text = "¿Aplicar cargo de gestión?", Foreground = B("#E65100"),
            FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var lblMonto = new TextBlock {
            Text = $"Gs. {montoCargo:N0}", Foreground = B("#BF360C"),
            FontSize = 17, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(lblMonto, Dock.Right);
        montoDp.Children.Insert(0, lblMonto);
        montoBorder.Child = montoDp;
        body.Children.Add(montoBorder);

        body.Children.Add(new TextBlock {
            Text = "Si aceptás, el monto se suma al Reajuste de esta cuota (podés ajustarlo después).",
            Foreground = B("#78909C"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8) });
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
            Content         = "No aplicar",
            Width           = 120, Height = 38,
            Margin          = new Thickness(0, 0, 10, 0),
            Background      = B("#546E7A"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.SemiBold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnNo.Click += (_, _) => { AplicarCargo = false; Close(); };

        var btnSi = new Button {
            Content         = "Sí, aplicar cargo",
            Width           = 150, Height = 38,
            Background      = B("#E65100"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.Bold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnSi.Click += (_, _) => { AplicarCargo = true; Close(); };

        KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Escape) { AplicarCargo = false; Close(); }
        };

        footerSp.Children.Add(btnNo);
        footerSp.Children.Add(btnSi);
        footer.Child = footerSp;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
    }
}
