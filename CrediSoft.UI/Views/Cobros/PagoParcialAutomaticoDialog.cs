using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Cobros;

// ─────────────────────────────────────────────────────────────────────────────
// Se muestra cuando el efectivo recibido no alcanza el total a cobrar — en vez
// de bloquear con un simple aviso, ofrece continuar automáticamente como abono
// parcial por el monto realmente entregado, evitando que el cajero tenga que
// saber de antemano que existe el toggle "Abono parcial".
// ─────────────────────────────────────────────────────────────────────────────
public class PagoParcialAutomaticoDialog : Window
{
    public bool ContinuarComoParcial { get; private set; } = false;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public PagoParcialAutomaticoDialog(
        string nombreCliente, int nCuota, decimal totalACobrar, decimal montoRecibido)
    {
        var saldoRestante = totalACobrar - montoRecibido;

        Title                 = "El monto no completa el pago";
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

        // ── Encabezado naranja (advertencia, no error bloqueante) ────────────
        var header = new Border {
            Background = B("#E65100"),
            Padding    = new Thickness(24, 18, 24, 18) };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "⚠", FontSize = 22, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        var headerTextSp = new StackPanel();
        headerTextSp.Children.Add(new TextBlock {
            Text       = "EL MONTO INGRESADO NO COMPLETA EL PAGO",
            Foreground = Brushes.White,
            FontSize   = 15,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin     = new Thickness(0, 0, 0, 4) });
        headerTextSp.Children.Add(new TextBlock {
            Text       = "¿Deseás continuar el cobro como pago parcial?",
            Foreground = B("#FFCCBC"),
            FontSize   = 11 });
        headerSp.Children.Add(headerTextSp);
        header.Child = headerSp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Cuerpo ───────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 0) };

        body.Children.Add(SeccionTitulo("CLIENTE"));
        body.Children.Add(FilaDato("Cliente",  nombreCliente, bold: true));
        body.Children.Add(FilaDato("Cuota N°", nCuota.ToString()));
        body.Children.Add(new Border { Height = 1, Background = B("#E3EAF5"), Margin = new Thickness(0, 12, 0, 12) });

        body.Children.Add(SeccionTitulo("DETALLE"));
        body.Children.Add(FilaDato("Total a Cobrar", $"Gs. {totalACobrar:N0}"));
        body.Children.Add(FilaDato("Pago Parcial",   $"Gs. {montoRecibido:N0}"));
        body.Children.Add(new Border { Height = 1, Background = B("#E3EAF5"), Margin = new Thickness(0, 12, 0, 0) });

        // ── Saldo restante destacado ─────────────────────────────────────────
        var saldoBox = new Border {
            Background    = B("#E65100"),
            CornerRadius  = new CornerRadius(6),
            Padding       = new Thickness(16, 14, 16, 14),
            Margin        = new Thickness(0, 12, 0, 20) };
        var saldoDp = new DockPanel();
        saldoDp.Children.Add(new TextBlock {
            Text       = "SALDO RESTANTE",
            Foreground = B("#FFCCBC"),
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center });
        var lblSaldo = new TextBlock {
            Text       = $"Gs. {saldoRestante:N0}",
            Foreground = Brushes.White,
            FontSize   = 20,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(lblSaldo, Dock.Right);
        saldoDp.Children.Insert(0, lblSaldo);
        saldoBox.Child = saldoDp;
        body.Children.Add(saldoBox);

        // Aclaración: la cuota NO se marca como cobrada — sigue pendiente por el saldo.
        var aviso = new Border {
            Background   = B("#FFF3E0"),
            BorderBrush  = B("#FFB74D"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(12, 8, 12, 8),
            Margin       = new Thickness(0, 0, 0, 20) };
        aviso.Child = new TextBlock {
            Text = "La cuota continuará con estado PENDIENTE hasta cobrar el saldo restante.",
            Foreground = B("#E65100"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center };
        body.Children.Add(aviso);

        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        // ── Barra de botones ─────────────────────────────────────────────────
        var footer = new Border {
            Background = B("#FFF3E0"),
            BorderBrush = B("#FFCC80"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 14, 24, 14) };
        var footerSp = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };

        var btnNo = new Button {
            Content         = "✖  Cancelar",
            Width           = 130, Height = 38,
            Margin          = new Thickness(0, 0, 10, 0),
            Background      = B("#546E7A"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.SemiBold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnNo.Click += (_, _) => { ContinuarComoParcial = false; Close(); };

        var btnSi = new Button {
            Content         = "✔  Continuar como parcial",
            Width           = 230, Height = 38,
            Padding         = new Thickness(8, 0, 8, 0),
            Background      = B("#E65100"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.Bold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnSi.Click += (_, _) => { ContinuarComoParcial = true; Close(); };

        KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Enter)  { ContinuarComoParcial = true;  Close(); }
            if (e.Key == System.Windows.Input.Key.Escape) { ContinuarComoParcial = false; Close(); }
        };

        footerSp.Children.Add(btnNo);
        footerSp.Children.Add(btnSi);
        footer.Child = footerSp;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
    }

    private static TextBlock SeccionTitulo(string texto) => new() {
        Text       = texto,
        Foreground = B("#E65100"),
        FontSize   = 10,
        FontWeight = FontWeights.Bold,
        Margin     = new Thickness(0, 0, 0, 6) };

    private static Border FilaDato(string label, string valor, bool bold = false)
    {
        var dp = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        dp.Children.Add(new TextBlock {
            Text       = label,
            Foreground = B("#546E7A"),
            FontSize   = 12,
            Width      = 130,
            VerticalAlignment = VerticalAlignment.Center });
        var valTb = new TextBlock {
            Text       = valor,
            Foreground = bold ? B("#E65100") : B("#212121"),
            FontSize   = 12,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment       = TextAlignment.Right };
        DockPanel.SetDock(valTb, Dock.Right);
        dp.Children.Insert(0, valTb);
        return new Border { Child = dp };
    }
}
