using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Cobros;

public class CuotaCobradaItem
{
    public int      NCuota      { get; set; }
    public string   Comprobante { get; set; } = "";
    public decimal  Monto       { get; set; }
    public DateTime FechaPago   { get; set; }
}

public class ErrorEliminacionDialog : Window
{
    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public ErrorEliminacionDialog(string nombreCliente, int idCredito, IEnumerable<CuotaCobradaItem> cuotas)
    {
        Title                 = "No se puede eliminar";
        Width                 = 520;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#F5F7FA");
        FontFamily            = new FontFamily("Segoe UI");

        var root = new DockPanel();

        // ── Header rojo ──────────────────────────────────────────────────────
        var header = new Border {
            Background = B("#B71C1C"),
            Padding    = new Thickness(24, 18, 24, 18) };
        var hDp = new DockPanel();
        var icono = new Border {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(23),
            Background = B("#C62828"), Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center };
        icono.Child = new TextBlock {
            Text = "✕", FontSize = 22, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };
        DockPanel.SetDock(icono, Dock.Left);
        hDp.Children.Add(icono);
        var hTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hTxt.Children.Add(new TextBlock {
            Text = "NO SE PUEDE ELIMINAR", Foreground = Brushes.White,
            FontSize = 16, FontWeight = FontWeights.Bold });
        hTxt.Children.Add(new TextBlock {
            Text = $"El crédito #{idCredito} tiene cuotas cobradas",
            Foreground = B("#FFCDD2"), FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0) });
        hDp.Children.Add(hTxt);
        header.Child = hDp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Cuerpo ───────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(24, 18, 24, 0) };

        // Info cliente
        body.Children.Add(new TextBlock {
            Text = $"Cliente: {nombreCliente}",
            Foreground = B("#37474F"), FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14) });

        // Título cuotas
        body.Children.Add(new TextBlock {
            Text = "CUOTAS COBRADAS", Foreground = B("#C62828"),
            FontSize = 9, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8) });

        // Encabezado tabla
        var lista = cuotas.ToList();
        var encabezado = new Border {
            Background = B("#0E2F44"),
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Padding = new Thickness(12, 8, 12, 8) };
        var encGrid = MakeGrid();
        encGrid.Children.Add(Col("Cuota",       0, Brushes.White, bold: true));
        encGrid.Children.Add(Col("Comprobante", 1, B("#90CAF9"), bold: true));
        encGrid.Children.Add(Col("Monto",       2, B("#90CAF9"), bold: true, right: true));
        encGrid.Children.Add(Col("Fecha pago",  3, B("#90CAF9"), bold: true, right: true));
        encabezado.Child = encGrid;
        body.Children.Add(encabezado);

        // Filas
        var scroll = new ScrollViewer {
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var rowsSp = new StackPanel();
        for (int i = 0; i < lista.Count; i++) {
            var q = lista[i];
            var row = new Border {
                Background = i % 2 == 0 ? Brushes.White : B("#F5F7FA"),
                BorderBrush = B("#FFCDD2"), BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8) };
            var rg = MakeGrid();
            rg.Children.Add(Col($"N° {q.NCuota}",               0, B("#212121")));
            rg.Children.Add(Col(q.Comprobante,                   1, B("#455A64")));
            rg.Children.Add(Col($"Gs. {q.Monto:N0}",            2, B("#B71C1C"), bold: true, right: true));
            rg.Children.Add(Col(q.FechaPago.ToString("dd/MM/yy"),3, B("#455A64"), right: true));
            row.Child = rg;
            rowsSp.Children.Add(row);
        }
        scroll.Content = rowsSp;

        var scrollBorder = new Border {
            BorderBrush = B("#FFCDD2"), BorderThickness = new Thickness(1, 0, 1, 1),
            CornerRadius = new CornerRadius(0, 0, 6, 6),
            Margin = new Thickness(0, 0, 0, 16),
            Child = scroll };
        body.Children.Add(scrollBorder);

        // Advertencia
        var warn = new Border {
            Background = B("#FFF8E1"), BorderBrush = B("#FFE082"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 20) };
        warn.Child = new TextBlock {
            Text = "Para poder eliminar este crédito, primero debe revertir los cobros de las cuotas listadas.",
            Foreground = B("#5D4037"), FontSize = 11, TextWrapping = TextWrapping.Wrap };
        body.Children.Add(warn);

        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        // ── Footer ───────────────────────────────────────────────────────────
        var footer = new Border {
            Background = Brushes.White, BorderBrush = B("#FFCDD2"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 14, 24, 14) };
        var btnOk = new Button {
            Content = "Entendido", Height = 40, Width = 160,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = B("#0E2F44"), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand };
        btnOk.Click += (_, _) => Close();
        footer.Child = btnOk;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.Enter) Close(); };
    }

    private static Grid MakeGrid()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        return g;
    }

    private static TextBlock Col(string txt, int col, Brush fg, bool bold = false, bool right = false)
    {
        var tb = new TextBlock {
            Text = txt, Foreground = fg,
            FontSize = 11, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
            HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(tb, col);
        return tb;
    }
}
