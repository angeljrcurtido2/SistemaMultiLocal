using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Cobros;

public class CobroExitosoDialog : Window
{
    public bool Imprimir { get; private set; } = false;

    // esParcial + saldoPendiente: si el pago no cerró la cuota, se muestra "Saldo Pendiente"
    // en vez de vuelto. totalACobrar/efectivoRecibido/vuelto: solo se completan cuando el
    // cajero cargó "Efectivo recibido" y entregó más de lo que se cobró — permite mostrar el
    // desglose Total Entregado / Total a Cobrar / Vuelto en vez de solo el monto acreditado.
    public CobroExitosoDialog(
        string nombreCliente, int nCuota, decimal total, string metodo,
        bool esParcial = false, decimal saldoPendiente = 0,
        decimal totalACobrar = 0, decimal efectivoRecibido = 0, decimal vuelto = 0)
    {
        Title                 = "Cobro Exitoso";
        Width                 = 480;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#F5F7FA");
        FontFamily            = new FontFamily("Segoe UI");

        var root = new DockPanel();

        // ── Banda superior azul oscura ───────────────────────────────────────
        var header = new Border {
            Background = B("#0E2F44"),
            Padding    = new Thickness(28, 22, 28, 22) };

        var headerDp = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

        // Círculo con check
        var circulo = new Border {
            Width        = 52, Height = 52,
            CornerRadius = new CornerRadius(26),
            Background   = B("#1565C0"),
            Margin       = new Thickness(0, 0, 18, 0),
            VerticalAlignment = VerticalAlignment.Center };
        circulo.Child = new TextBlock {
            Text      = "✓", FontSize = 26, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };
        DockPanel.SetDock(circulo, Dock.Left);
        headerDp.Children.Add(circulo);

        var txtSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        txtSp.Children.Add(new TextBlock {
            Text       = "COBRO REGISTRADO",
            Foreground = Brushes.White,
            FontSize   = 17,
            FontWeight = FontWeights.Bold });
        txtSp.Children.Add(new TextBlock {
            Text       = "La operación fue procesada exitosamente",
            Foreground = B("#90CAF9"),
            FontSize   = 11,
            Margin     = new Thickness(0, 4, 0, 0) });
        headerDp.Children.Add(txtSp);
        header.Child = headerDp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Cuerpo ───────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(28, 22, 28, 0) };

        // Tarjeta cliente
        var cardCliente = new Border {
            Background      = Brushes.White,
            BorderBrush     = B("#BBDEFB"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Padding         = new Thickness(18, 14, 18, 14),
            Margin          = new Thickness(0, 0, 0, 12),
            Effect          = new DropShadowEffect {
                BlurRadius = 8, Opacity = 0.08, ShadowDepth = 2,
                Direction  = 270, Color = Colors.Black } };

        var cardSp = new StackPanel();
        cardSp.Children.Add(new TextBlock {
            Text       = "DETALLE DEL COBRO",
            Foreground = B("#1565C0"),
            FontSize   = 9,
            FontWeight = FontWeights.Bold,
            Margin     = new Thickness(0, 0, 0, 12) });
        cardSp.Children.Add(FilaGrid("Cliente",  nombreCliente, resaltar: true));
        cardSp.Children.Add(Separador());
        cardSp.Children.Add(FilaGrid("Cuota N°", nCuota.ToString()));
        cardSp.Children.Add(Separador());
        cardSp.Children.Add(FilaGrid("Método",   metodo));

        // Desglose adicional: solo si hay algo más que contar aparte del monto acreditado
        // (vuelto en efectivo, o el pago dejó la cuota con saldo pendiente).
        if (totalACobrar > 0 || (esParcial && saldoPendiente > 0))
            cardSp.Children.Add(Separador());
        if (totalACobrar > 0)
            cardSp.Children.Add(FilaGrid("Total a Cobrar", $"Gs. {totalACobrar:N0}"));
        if (efectivoRecibido > 0)
        {
            cardSp.Children.Add(Separador());
            cardSp.Children.Add(FilaGrid("Total Entregado", $"Gs. {efectivoRecibido:N0}"));
        }
        if (esParcial && saldoPendiente > 0)
            cardSp.Children.Add(FilaGrid("Saldo Pendiente", $"Gs. {saldoPendiente:N0}", resaltar: true));

        cardCliente.Child = cardSp;
        body.Children.Add(cardCliente);

        // Caja total — "TOTAL COBRADO" es siempre lo acreditado a la deuda. Si hubo vuelto en
        // efectivo, se agrega una segunda caja con ese monto para que quede tan visible como
        // el total cobrado, no perdido entre las filas de detalle de arriba.
        var totalBorder = new Border {
            Background      = B("#0E2F44"),
            CornerRadius    = new CornerRadius(8),
            Padding         = new Thickness(20, 16, 20, 16),
            Margin          = new Thickness(0, 0, 0, vuelto > 0 ? 10 : 24) };

        var totalGrid = new Grid();
        totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lblTotalTitulo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lblTotalTitulo.Children.Add(new TextBlock {
            Text = "TOTAL COBRADO", Foreground = B("#90CAF9"),
            FontSize = 10, FontWeight = FontWeights.Bold });
        lblTotalTitulo.Children.Add(new TextBlock {
            Text = metodo, Foreground = B("#64B5F6"),
            FontSize = 10, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(lblTotalTitulo, 0);
        totalGrid.Children.Add(lblTotalTitulo);

        var lblMonto = new TextBlock {
            Text                = $"Gs. {total:N0}",
            Foreground          = Brushes.White,
            FontSize            = 22,
            FontWeight          = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Center };
        Grid.SetColumn(lblMonto, 1);
        totalGrid.Children.Add(lblMonto);

        totalBorder.Child = totalGrid;
        body.Children.Add(totalBorder);

        if (vuelto > 0)
        {
            var vueltoBorder = new Border {
                Background   = B("#2E7D32"),
                CornerRadius = new CornerRadius(8),
                Padding      = new Thickness(20, 14, 20, 14),
                Margin       = new Thickness(0, 0, 0, 24) };
            var vueltoGrid = new Grid();
            vueltoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            vueltoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lblVueltoTitulo = new TextBlock {
                Text = "VUELTO PARA EL CLIENTE", Foreground = B("#C8E6C9"),
                FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblVueltoTitulo, 0);
            vueltoGrid.Children.Add(lblVueltoTitulo);
            var lblVueltoMonto = new TextBlock {
                Text = $"Gs. {vuelto:N0}", Foreground = Brushes.White,
                FontSize = 18, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblVueltoMonto, 1);
            vueltoGrid.Children.Add(lblVueltoMonto);
            vueltoBorder.Child = vueltoGrid;
            body.Children.Add(vueltoBorder);
        }

        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        // ── Footer con botones ───────────────────────────────────────────────
        var footer = new Border {
            Background      = Brushes.White,
            BorderBrush     = B("#BBDEFB"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding         = new Thickness(28, 14, 28, 14) };

        // Grid 2 columnas iguales para que ambos botones tengan el mismo ancho
        var footerGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); // gap
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var btnCerrar = MakeBtn("✖  Cerrar", "#546E7A");
        btnCerrar.Click += (_, _) => { Imprimir = false; Close(); };
        Grid.SetColumn(btnCerrar, 0);
        footerGrid.Children.Add(btnCerrar);

        var btnImprimir = MakeBtn("🖨  Imprimir comprobante", "#1565C0");
        btnImprimir.FontWeight = FontWeights.Bold;
        btnImprimir.Click += (_, _) => { Imprimir = true; Close(); };
        Grid.SetColumn(btnImprimir, 2);
        footerGrid.Children.Add(btnImprimir);

        footer.Child = footerGrid;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;

        KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Enter)  { Imprimir = true;  Close(); }
            if (e.Key == System.Windows.Input.Key.Escape) { Imprimir = false; Close(); }
        };
    }

    // Fila con Grid 2 cols: label fijo 110px, valor ocupa el resto alineado a la derecha
    private static UIElement FilaGrid(string label, string valor, bool resaltar = false)
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lbl = new TextBlock {
            Text = label, Foreground = B("#78909C"),
            FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lbl, 0);
        g.Children.Add(lbl);

        var val = new TextBlock {
            Text         = valor,
            Foreground   = resaltar ? B("#0D47A1") : B("#212121"),
            FontSize     = resaltar ? 12 : 11,
            FontWeight   = resaltar ? FontWeights.Bold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment           = TextAlignment.Right,
            HorizontalAlignment     = HorizontalAlignment.Right,
            VerticalAlignment       = VerticalAlignment.Center };
        Grid.SetColumn(val, 1);
        g.Children.Add(val);

        return g;
    }

    private static Border Separador() => new() {
        Height = 1, Background = B("#EEF4FB"),
        Margin = new Thickness(0, 4, 0, 4) };

    private static Button MakeBtn(string txt, string bg) => new() {
        Content         = txt, Height = 40,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Background      = B(bg), Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        FontSize        = 12, FontWeight = FontWeights.SemiBold,
        Cursor          = System.Windows.Input.Cursors.Hand };

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));
}
