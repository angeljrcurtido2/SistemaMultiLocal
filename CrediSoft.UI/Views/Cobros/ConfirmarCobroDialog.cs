using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Cobros;

// ─────────────────────────────────────────────────────────────────────────────
// Modal de confirmación de cobro — diseño profesional en paleta azul corporativa
// ─────────────────────────────────────────────────────────────────────────────
public class ConfirmarCobroDialog : Window
{
    public bool Confirmado { get; private set; } = false;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public ConfirmarCobroDialog(
        string nombreCliente, int nCuota, string comprobante,
        decimal capital, decimal punitorio, decimal reajuste,
        decimal entregaAnterior, decimal total, string metodo)
    {
        Title                 = "Confirmar Cobro";
        Width                 = 460;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#F5F7FA");
        FontFamily            = new FontFamily("Segoe UI");

        // ── Sombra exterior ──────────────────────────────────────────────────
        Effect = new DropShadowEffect {
            BlurRadius = 24, Opacity = 0.3, ShadowDepth = 8,
            Direction = 270, Color = Colors.Black };

        var root = new DockPanel();

        // ── Encabezado azul ──────────────────────────────────────────────────
        var header = new Border {
            Background = B("#0D47A1"),
            Padding    = new Thickness(24, 18, 24, 18) };
        var headerSp = new StackPanel();

        headerSp.Children.Add(new TextBlock {
            Text       = "RESUMEN DE COBRO",
            Foreground = Brushes.White,
            FontSize   = 16,
            FontWeight = FontWeights.Bold,
            Margin     = new Thickness(0, 0, 0, 4) });
        headerSp.Children.Add(new TextBlock {
            Text       = "Verifique los datos antes de confirmar",
            Foreground = B("#90CAF9"),
            FontSize   = 11 });

        header.Child = headerSp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Cuerpo ───────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 0) };

        // Sección: datos del cliente
        body.Children.Add(SeccionTitulo("CLIENTE"));
        body.Children.Add(FilaDato("Cliente",      nombreCliente, bold: true));
        body.Children.Add(FilaDato("Cuota N°",     nCuota.ToString()));
        body.Children.Add(FilaDato("Comprobante",  comprobante));
        body.Children.Add(new Border { Height = 1, Background = B("#E3EAF5"), Margin = new Thickness(0, 12, 0, 12) });

        // Sección: desglose
        body.Children.Add(SeccionTitulo("DESGLOSE"));
        body.Children.Add(FilaDato("Capital",      $"Gs. {capital:N0}"));
        body.Children.Add(FilaDato("Punitorio",    $"Gs. {punitorio:N0}"));
        if (reajuste != 0)
            body.Children.Add(FilaDato("Reajuste", $"Gs. {reajuste:N0}"));
        if (entregaAnterior != 0)
            body.Children.Add(FilaDato("Ent. Anterior", $"Gs. {entregaAnterior:N0}"));
        body.Children.Add(new Border { Height = 1, Background = B("#E3EAF5"), Margin = new Thickness(0, 12, 0, 0) });

        // Aviso destacado si el reajuste es grande respecto al capital — un cajero llegó a
        // tipear el efectivo recibido en el campo Reajuste por error, sumando ese monto entero
        // al total real de la cuota (971.548 en vez de 490.000). Este umbral (>15% del capital)
        // no distingue "correcto" de "error": solo frena al cajero a confirmar conscientemente
        // que el ajuste es intencional antes de cobrar.
        if (capital > 0 && reajuste > capital * 0.15m)
        {
            var avisoBorder = new Border {
                Background      = B("#FFF8E1"),
                BorderBrush     = B("#FFB300"),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(14, 10, 14, 10),
                Margin          = new Thickness(0, 12, 0, 0) };
            var avisoSp = new StackPanel();
            avisoSp.Children.Add(new TextBlock {
                Text = "⚠ REAJUSTE INUSUALMENTE ALTO",
                Foreground = B("#8D6E00"), FontSize = 11, FontWeight = FontWeights.Bold });
            avisoSp.Children.Add(new TextBlock {
                Text = $"El reajuste (Gs. {reajuste:N0}) es un monto ADICIONAL que se suma al capital " +
                       "de la cuota — no es el efectivo recibido. Verifique que sea correcto antes de confirmar.",
                Foreground = B("#6D4C00"), FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0) });
            avisoBorder.Child = avisoSp;
            body.Children.Add(avisoBorder);
        }

        // ── Total destacado ──────────────────────────────────────────────────
        var totalBox = new Border {
            Background    = B("#0D47A1"),
            CornerRadius  = new CornerRadius(6),
            Padding       = new Thickness(16, 14, 16, 14),
            Margin        = new Thickness(0, 12, 0, 12) };
        var totalDp = new DockPanel();
        totalDp.Children.Add(new TextBlock {
            Text       = "TOTAL A COBRAR",
            Foreground = B("#90CAF9"),
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center });
        var lblTotal = new TextBlock {
            Text       = $"Gs. {total:N0}",
            Foreground = Brushes.White,
            FontSize   = 20,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(lblTotal, Dock.Right);
        totalDp.Children.Insert(0, lblTotal);
        totalBox.Child = totalDp;
        body.Children.Add(totalBox);

        // Método de pago
        var metodoBorder = new Border {
            Background   = B("#EEF4FB"),
            BorderBrush  = B("#BBDEFB"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(16, 10, 16, 10),
            Margin       = new Thickness(0, 0, 0, 20) };
        var metodoDp = new DockPanel();
        metodoDp.Children.Add(new TextBlock {
            Text       = "Método de pago",
            Foreground = B("#1565C0"),
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center });
        var lblMetodo = new TextBlock {
            Text       = metodo,
            Foreground = B("#0D47A1"),
            FontSize   = 12,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(lblMetodo, Dock.Right);
        metodoDp.Children.Insert(0, lblMetodo);
        metodoBorder.Child = metodoDp;
        body.Children.Add(metodoBorder);

        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        // ── Barra de botones ─────────────────────────────────────────────────
        var footer = new Border {
            Background = B("#EEF4FB"),
            BorderBrush = B("#BBDEFB"),
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
        btnNo.Click += (_, _) => { Confirmado = false; Close(); };

        var btnSi = new Button {
            Content         = "✔  Confirmar Cobro",
            Width           = 160, Height = 38,
            Background      = B("#1565C0"),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize        = 13,
            FontWeight      = FontWeights.Bold,
            Cursor          = System.Windows.Input.Cursors.Hand };
        btnSi.Click += (_, _) => { Confirmado = true; Close(); };

        // Enter confirma, Escape cancela
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

    private static TextBlock SeccionTitulo(string texto) => new() {
        Text       = texto,
        Foreground = B("#1565C0"),
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
            Foreground = bold ? B("#0D47A1") : B("#212121"),
            FontSize   = 12,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment       = TextAlignment.Right };
        DockPanel.SetDock(valTb, Dock.Right);
        dp.Children.Insert(0, valTb);
        return new Border { Child = dp };
    }
}
