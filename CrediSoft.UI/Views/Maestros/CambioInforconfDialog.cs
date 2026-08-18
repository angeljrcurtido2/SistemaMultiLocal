using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Maestros;

// ─────────────────────────────────────────────────────────────────────────────
// Confirmacion al cambiar CLIENTES.INFORCOM desde la ficha del cliente. Explica
// el efecto real del cambio (que el cajero vera una pregunta al cobrar cuotas
// de este cliente) antes de guardar — evita que se marque/desmarque sin saber
// que activa un flujo distinto en Cobros.
// ─────────────────────────────────────────────────────────────────────────────
public class CambioInforconfDialog : Window
{
    public bool Confirmado { get; private set; } = false;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public CambioInforconfDialog(string nombreCliente, bool pasaARegistrado, decimal montoCargo = 0)
    {
        // Azul corporativo para ambas direcciones del cambio — consistente con el resto
        // del sistema (naranja quedaba fuera de la paleta usada en Cobros/Caja/etc.).
        const string colorPrincipal = "#0E2F44";
        const string colorClaro     = "#B0D4EC";
        const string colorFondo     = "#EEF4FB";
        const string colorBorde     = "#BBDEFB";

        Title                 = "Cambio de estado Informconf";
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

        // ── Encabezado ───────────────────────────────────────────────────────
        var header = new Border {
            Background = B(colorPrincipal),
            Padding    = new Thickness(24, 18, 24, 18) };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = pasaARegistrado ? "⚠" : "✓", FontSize = 22, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0) });
        var headerTxt = new StackPanel();
        headerTxt.Children.Add(new TextBlock {
            Text = pasaARegistrado ? "MARCAR COMO REPORTADO" : "LIBERAR DE INFORMCONF",
            Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        headerTxt.Children.Add(new TextBlock {
            Text = nombreCliente, Foreground = B(colorClaro), FontSize = 11 });
        headerSp.Children.Add(headerTxt);
        header.Child = headerSp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Cuerpo ───────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 4) };

        var efectoBorder = new Border {
            Background      = B(colorFondo),
            BorderBrush     = B(colorBorde),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(6),
            Padding         = new Thickness(16, 12, 16, 12),
            Margin          = new Thickness(0, 0, 0, 14) };
        var efectoSp = new StackPanel();
        efectoSp.Children.Add(new TextBlock {
            Text = "QUÉ VA A PASAR", Foreground = B(colorPrincipal),
            FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
        efectoSp.Children.Add(new TextBlock {
            Text = pasaARegistrado
                ? "Al cobrarle una cuota, el sistema va a preguntar si corresponde aplicar el cargo de gestión:"
                : "Ya no se va a preguntar por el cargo de gestión al cobrarle cuotas.",
            Foreground = B("#37474F"), FontSize = 13, TextWrapping = TextWrapping.Wrap });

        if (pasaARegistrado && montoCargo > 0)
        {
            var montoDp = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            montoDp.Children.Add(new TextBlock {
                Text = "Monto del cargo", Foreground = B(colorPrincipal),
                FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var lblMonto = new TextBlock {
                Text = $"Gs. {montoCargo:N0}", Foreground = B(colorPrincipal),
                FontSize = 18, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(lblMonto, Dock.Right);
            montoDp.Children.Insert(0, lblMonto);
            efectoSp.Children.Add(montoDp);
        }

        efectoBorder.Child = efectoSp;
        body.Children.Add(efectoBorder);

        body.Children.Add(new TextBlock {
            Text = "¿Confirmás el cambio?",
            Foreground = B("#0E2F44"), FontSize = 13, FontWeight = FontWeights.SemiBold,
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
            Content         = "Cancelar",
            Width           = 110, Height = 38,
            Margin          = new Thickness(0, 0, 10, 0),
            Background      = B("#546E7A"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.SemiBold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnNo.Click += (_, _) => { Confirmado = false; Close(); };

        var btnSi = new Button {
            Content         = "Confirmar",
            Width           = 130, Height = 38,
            Background      = B(colorPrincipal),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.Bold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnSi.Click += (_, _) => { Confirmado = true; Close(); };

        KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Enter)  { Confirmado = true;  Close(); }
            if (e.Key == System.Windows.Input.Key.Escape) { Confirmado = false; Close(); }
        };

        footerSp.Children.Add(btnNo);
        footerSp.Children.Add(btnSi);
        footer.Child = footerSp;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
    }
}
