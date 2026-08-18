using CrediSoft.Data.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Pagos;

// ── Dialog: cálculo generado exitosamente ────────────────────────────────────
public class CalculoGeneradoDialog : Window
{
    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public CalculoGeneradoDialog(string nombreFuncionario, decimal neto)
    {
        Title                 = "Cálculo generado";
        Width                 = 440;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#F5F7FA");
        FontFamily            = new FontFamily("Segoe UI");

        var root = new DockPanel();

        // Header azul
        var header = new Border { Background = B("#1565C0"), Padding = new Thickness(24, 18, 24, 18) };
        var hDp = new DockPanel();
        var icono = new Border {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(23),
            Background = B("#1E88E5"), Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center };
        icono.Child = new TextBlock {
            Text = "✓", FontSize = 24, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };
        DockPanel.SetDock(icono, Dock.Left);
        hDp.Children.Add(icono);
        var hTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hTxt.Children.Add(new TextBlock {
            Text = "CÁLCULO GENERADO", Foreground = Brushes.White,
            FontSize = 15, FontWeight = FontWeights.Bold });
        hTxt.Children.Add(new TextBlock {
            Text = nombreFuncionario,
            Foreground = B("#90CAF9"), FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0) });
        hDp.Children.Add(hTxt);
        header.Child = hDp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // Cuerpo
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 0) };

        // Caja neto
        var cajaNetoB = new Border {
            Background = B("#0E2F44"), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 14, 20, 14), Margin = new Thickness(0, 0, 0, 16),
            Effect = new DropShadowEffect { BlurRadius = 10, Opacity = 0.12, ShadowDepth = 3, Direction = 270, Color = Colors.Black } };
        var cajaNetoG = new Grid();
        cajaNetoG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cajaNetoG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var lNeto = new StackPanel();
        lNeto.Children.Add(new TextBlock { Text = "NETO A PAGAR", Foreground = B("#90CAF9"), FontSize = 9, FontWeight = FontWeights.Bold });
        lNeto.Children.Add(new TextBlock { Text = "Guaraníes", Foreground = B("#BBDEFB"), FontSize = 11 });
        Grid.SetColumn(lNeto, 0);
        cajaNetoG.Children.Add(lNeto);
        var vNeto = new TextBlock {
            Text = $"Gs. {neto:N0}", Foreground = Brushes.White,
            FontSize = 22, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(vNeto, 1);
        cajaNetoG.Children.Add(vNeto);
        cajaNetoB.Child = cajaNetoG;
        body.Children.Add(cajaNetoB);

        // Nota informativa
        var info = new Border {
            Background = B("#E3F2FD"), BorderBrush = B("#BBDEFB"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 20) };
        info.Child = new TextBlock {
            Text = "Revisá los valores en pantalla y presioná GUARDAR PAGO para registrar el movimiento en caja.",
            Foreground = B("#1565C0"), FontSize = 11, TextWrapping = TextWrapping.Wrap, LineHeight = 18 };
        body.Children.Add(info);

        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        // Footer
        var footer = new Border {
            Background = Brushes.White, BorderBrush = B("#BBDEFB"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 14, 24, 14) };
        var btn = new Button {
            Content = "Entendido", Height = 40, Width = 180,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = B("#1565C0"), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand };
        btn.Click += (_, _) => Close();
        footer.Child = btn;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Escape) Close(); };
    }
}

// ── Dialog: confirmar pago (YesNo) ───────────────────────────────────────────
public class ConfirmarPagoDialog : Window
{
    public bool Confirmado { get; private set; }

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public ConfirmarPagoDialog(string nombre, string local, decimal ingresos, decimal descuentos, decimal neto, string metodo,
        decimal salario = 0, decimal comisionVenta = 0, decimal comisionCobranza = 0)
    {
        Title                 = "Confirmar pago";
        Width                 = 460;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#F5F7FA");
        FontFamily            = new FontFamily("Segoe UI");

        var root = new DockPanel();

        // Header
        var header = new Border { Background = B("#0E2F44"), Padding = new Thickness(24, 18, 24, 18) };
        var hDp = new DockPanel();
        var icono = new Border {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(23),
            Background = B("#1565C0"), Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center };
        icono.Child = new TextBlock {
            Text = "&#xE74E;", FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 20, Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };
        DockPanel.SetDock(icono, Dock.Left);
        hDp.Children.Add(icono);
        var hTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hTxt.Children.Add(new TextBlock {
            Text = "CONFIRMAR PAGO DE SALARIO", Foreground = Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold });
        hTxt.Children.Add(new TextBlock {
            Text = "Revisá el resumen antes de confirmar",
            Foreground = B("#90CAF9"), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
        hDp.Children.Add(hTxt);
        header.Child = hDp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // Cuerpo
        var body = new StackPanel { Margin = new Thickness(24, 18, 24, 0) };

        // Tarjeta datos
        var card = new Border {
            Background = Brushes.White, BorderBrush = B("#BBDEFB"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 14),
            Effect = new DropShadowEffect { BlurRadius = 8, Opacity = 0.07, ShadowDepth = 2, Direction = 270, Color = Colors.Black } };

        var cardSp = new StackPanel();
        cardSp.Children.Add(new TextBlock {
            Text = "RESUMEN DEL PAGO", Foreground = B("#1565C0"),
            FontSize = 9, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });
        cardSp.Children.Add(Fila("Funcionario",     nombre,                   resaltar: true));
        cardSp.Children.Add(Sep());
        cardSp.Children.Add(Fila("Local de pago",   local));
        cardSp.Children.Add(Sep());
        // Desglose de ingresos — antes solo se veía el total, sin distinguir cuánto era salario
        // fijo y cuánto comisión, lo cual dejaba dudas sobre si la comisión realmente se estaba
        // pagando o no.
        if (salario > 0) cardSp.Children.Add(Fila("Salario fijo",       $"Gs. {salario:N0}"));
        if (comisionVenta > 0) cardSp.Children.Add(Fila("Comisión venta",     $"Gs. {comisionVenta:N0}"));
        if (comisionCobranza > 0) cardSp.Children.Add(Fila("Comisión cobranza", $"Gs. {comisionCobranza:N0}"));
        if (salario > 0 || comisionVenta > 0 || comisionCobranza > 0) cardSp.Children.Add(Sep());
        cardSp.Children.Add(Fila("Total ingresos",  $"Gs. {ingresos:N0}"));
        cardSp.Children.Add(Sep());
        cardSp.Children.Add(Fila("Total descuentos",$"Gs. {descuentos:N0}"));
        cardSp.Children.Add(Sep());
        cardSp.Children.Add(Fila("Método de pago",  metodo));
        card.Child = cardSp;
        body.Children.Add(card);

        // Caja neto
        var cajaN = new Border {
            Background = B("#0E2F44"), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 20) };
        var cajaG = new Grid();
        cajaG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cajaG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var lN = new TextBlock { Text = "NETO A PAGAR Gs.", Foreground = B("#90CAF9"), FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lN, 0);
        cajaG.Children.Add(lN);
        var vN = new TextBlock { Text = $"Gs. {neto:N0}", Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold };
        Grid.SetColumn(vN, 1);
        cajaG.Children.Add(vN);
        cajaN.Child = cajaG;
        body.Children.Add(cajaN);

        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        // Footer — 2 botones
        var footer = new Border {
            Background = Brushes.White, BorderBrush = B("#BBDEFB"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 14, 24, 14) };
        var footerG = new Grid();
        footerG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        footerG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var btnNo = MakeBtn("✖  Cancelar", "#546E7A");
        btnNo.Click += (_, _) => { Confirmado = false; Close(); };
        Grid.SetColumn(btnNo, 0);
        footerG.Children.Add(btnNo);

        var btnSi = MakeBtn("  Confirmar pago", "#1565C0");
        btnSi.FontWeight = FontWeights.Bold;
        btnSi.Click += (_, _) => { Confirmado = true; Close(); };
        Grid.SetColumn(btnSi, 2);
        footerG.Children.Add(btnSi);

        footer.Child = footerG;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { Confirmado = false; Close(); } };
    }

    private static UIElement Fila(string lbl, string val, bool resaltar = false)
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var l = new TextBlock { Text = lbl, Foreground = B("#78909C"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(l, 0); g.Children.Add(l);
        var v = new TextBlock {
            Text = val, FontSize = resaltar ? 13 : 11,
            FontWeight = resaltar ? FontWeights.Bold : FontWeights.Normal,
            Foreground = resaltar ? B("#0E2F44") : B("#212121"),
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(v, 1); g.Children.Add(v);
        return g;
    }
    private static Border Sep() => new() { Height = 1, Background = B("#EEF4FB"), Margin = new Thickness(0, 2, 0, 2) };
    private static Button MakeBtn(string txt, string bg) => new() {
        Content = txt, Height = 42, HorizontalAlignment = HorizontalAlignment.Stretch,
        Background = B(bg), Foreground = Brushes.White,
        BorderThickness = new Thickness(0), FontSize = 13,
        FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand };
}

// ── Dialog: pago exitoso ─────────────────────────────────────────────────────
public class PagoExitosoDialog : Window
{
    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public PagoExitosoDialog(string nombreFuncionario, decimal neto)
    {
        Title                 = "Pago registrado";
        Width                 = 400;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#F5F7FA");
        FontFamily            = new FontFamily("Segoe UI");

        var root = new DockPanel();

        // Header verde éxito
        var header = new Border { Background = B("#1B5E20"), Padding = new Thickness(24, 20, 24, 20) };
        var hDp = new DockPanel();
        var icono = new Border {
            Width = 50, Height = 50, CornerRadius = new CornerRadius(25),
            Background = B("#2E7D32"), Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center };
        icono.Child = new TextBlock {
            Text = "✓", FontSize = 26, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };
        DockPanel.SetDock(icono, Dock.Left);
        hDp.Children.Add(icono);
        var hTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hTxt.Children.Add(new TextBlock { Text = "PAGO REGISTRADO", Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold });
        hTxt.Children.Add(new TextBlock { Text = "Movimiento acreditado en caja", Foreground = B("#A5D6A7"), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
        hDp.Children.Add(hTxt);
        header.Child = hDp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // Cuerpo
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 0) };

        var card = new Border {
            Background = Brushes.White, BorderBrush = B("#C8E6C9"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(0, 0, 0, 16),
            Effect = new DropShadowEffect { BlurRadius = 8, Opacity = 0.07, ShadowDepth = 2, Direction = 270, Color = Colors.Black } };
        var cardSp = new StackPanel();
        cardSp.Children.Add(new TextBlock { Text = "DETALLE", Foreground = B("#2E7D32"), FontSize = 9, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });
        cardSp.Children.Add(FilaVerde("Funcionario", nombreFuncionario, negrita: true));
        cardSp.Children.Add(new Border { Height = 1, Background = B("#E8F5E9"), Margin = new Thickness(0, 6, 0, 6) });
        cardSp.Children.Add(FilaVerde("Neto pagado", $"Gs. {neto:N0}", grande: true));
        card.Child = cardSp;
        body.Children.Add(card);

        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        // Footer
        var footer = new Border {
            Background = Brushes.White, BorderBrush = B("#C8E6C9"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 14, 24, 14) };
        var btn = new Button {
            Content = "Aceptar", Height = 40, Width = 160,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = B("#2E7D32"), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand };
        btn.Click += (_, _) => Close();
        footer.Child = btn;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Escape) Close(); };
    }

    private static UIElement FilaVerde(string lbl, string val, bool negrita = false, bool grande = false)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var l = new TextBlock { Text = lbl, Foreground = B("#546E7A"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(l, 0); g.Children.Add(l);
        var v = new TextBlock {
            Text = val, FontSize = grande ? 15 : 12,
            FontWeight = negrita || grande ? FontWeights.Bold : FontWeights.Normal,
            Foreground = grande ? B("#1B5E20") : B("#212121"),
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(v, 1); g.Children.Add(v);
        return g;
    }
}

// ── Dialog: detalle de ventas para comisión ──────────────────────────────────
public class DetalleVentasDialog : Window
{
    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public DetalleVentasDialog(string nombreFuncionario, IEnumerable<DetalleVentaItem> items, decimal porcComision)
    {
        Title                 = "Detalle de ventas — Comisión";
        Width                 = 640;
        Height                = 500;
        MinHeight             = 360;
        ResizeMode            = ResizeMode.CanResizeWithGrip;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#EEF4FB");
        FontFamily            = new FontFamily("Segoe UI");

        var lista = items.ToList();
        // DetalleVentaItem.Total ahora trae ENTREGANORMAL (ver GetDetalleVentasAsync) —
        // la comisión de venta a crédito se calcula sobre la entrega inicial, no sobre el
        // precio total del producto.
        decimal totalEntregas = lista.Sum(v => v.Total);
        decimal comision      = totalEntregas * porcComision / 100m;

        var root = new DockPanel();

        // ── Header ──
        var header = new Border { Background = B("#1565C0"), Padding = new Thickness(22, 16, 22, 16) };
        var hDp = new DockPanel();
        var icono = new Border {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(21),
            Background = B("#1E88E5"), Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center };
        icono.Child = new TextBlock {
            Text = "🛒", FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };
        DockPanel.SetDock(icono, Dock.Left);
        hDp.Children.Add(icono);
        var hTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hTxt.Children.Add(new TextBlock { Text = "VENTAS DEL PERÍODO", Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold });
        hTxt.Children.Add(new TextBlock { Text = nombreFuncionario, Foreground = B("#90CAF9"), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        hDp.Children.Add(hTxt);
        header.Child = hDp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Footer con totales ──
        var footer = new Border {
            Background = Brushes.White, BorderBrush = B("#BBDEFB"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 14, 20, 14) };
        var footerG = new Grid();
        footerG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var totalesSp = new StackPanel();
        totalesSp.Children.Add(ResumenFila($"{lista.Count} venta(s)  ·  Total entregas:", $"Gs. {totalEntregas:N0}", "#455A64", "#0E2F44"));
        totalesSp.Children.Add(ResumenFila($"Comisión ({porcComision:N2}%):", $"Gs. {comision:N0}", "#1565C0", "#1565C0", bold: true));
        Grid.SetColumn(totalesSp, 0);
        footerG.Children.Add(totalesSp);

        var btnCerrar = new Button {
            Content = "Cerrar", Height = 38, Width = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Background = B("#546E7A"), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        Grid.SetColumn(btnCerrar, 1);
        footerG.Children.Add(btnCerrar);
        footer.Child = footerG;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        // ── DataGrid ──
        var grid = BuildGrid();
        if (lista.Count == 0)
        {
            var vacio = new Border {
                Background = Brushes.White, Padding = new Thickness(0, 40, 0, 40) };
            vacio.Child = new TextBlock {
                Text = "No se encontraron ventas en el período seleccionado.",
                Foreground = B("#90A4AE"), FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center };
            root.Children.Add(vacio);
        }
        else
        {
            grid.ItemsSource = lista;
            root.Children.Add(grid);
        }

        Content = root;
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }

    private static DataGrid BuildGrid()
    {
        var txL = new Style(typeof(TextBlock));
        txL.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6, 0, 6, 0)));
        txL.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        txL.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.5));

        var txR = new Style(typeof(TextBlock), txL);
        txR.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        txR.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Consolas")));

        var hdrStyle = new Style(typeof(DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"))));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        hdrStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));

        var dg = new DataGrid {
            AutoGenerateColumns  = false,
            IsReadOnly           = true,
            GridLinesVisibility  = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBDEFB")),
            RowBackground        = Brushes.White,
            AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F9FF")),
            SelectionMode        = DataGridSelectionMode.Single,
            ColumnHeaderStyle    = hdrStyle,
            BorderThickness      = new Thickness(0),
            Margin               = new Thickness(0) };

        dg.Columns.Add(new DataGridTextColumn { Header = "N° Venta",  Binding = new Binding("NVenta"),        Width = 80,  ElementStyle = txL });
        dg.Columns.Add(new DataGridTextColumn { Header = "Fecha",     Binding = new Binding("FechaFmt"),      Width = 90,  ElementStyle = txL });
        dg.Columns.Add(new DataGridTextColumn { Header = "Cliente",   Binding = new Binding("ClienteNombre"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = txL });
        dg.Columns.Add(new DataGridTextColumn { Header = "Entrega Gs.", Binding = new Binding("TotalFmt"),    Width = 120, ElementStyle = txR });

        return dg;
    }

    private static UIElement ResumenFila(string lbl, string val, string colorLbl, string colorVal, bool bold = false)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 20, 2) };
        sp.Children.Add(new TextBlock { Text = lbl, Foreground = B(colorLbl), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        sp.Children.Add(new TextBlock { Text = val, Foreground = B(colorVal), FontSize = bold ? 13 : 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }
}

// ── Dialog: detalle de cobranzas para comisión ────────────────────────────────
public class DetalleCobranzasDialog : Window
{
    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public DetalleCobranzasDialog(string nombreFuncionario, IEnumerable<DetalleCobranzaItem> items, decimal porcComision)
    {
        Title                 = "Detalle de cobranzas — Comisión";
        Width                 = 680;
        Height                = 500;
        MinHeight             = 360;
        ResizeMode            = ResizeMode.CanResizeWithGrip;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#EEF4FB");
        FontFamily            = new FontFamily("Segoe UI");

        var lista = items.ToList();
        decimal totalCobrado = lista.Sum(c => c.Total);
        decimal comision     = totalCobrado * porcComision / 100m;

        var root = new DockPanel();

        // ── Header ──
        var header = new Border { Background = B("#0E2F44"), Padding = new Thickness(22, 16, 22, 16) };
        var hDp = new DockPanel();
        var icono = new Border {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(21),
            Background = B("#1565C0"), Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center };
        icono.Child = new TextBlock {
            Text = "💰", FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };
        DockPanel.SetDock(icono, Dock.Left);
        hDp.Children.Add(icono);
        var hTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hTxt.Children.Add(new TextBlock { Text = "COBRANZAS DEL PERÍODO", Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold });
        hTxt.Children.Add(new TextBlock { Text = nombreFuncionario, Foreground = B("#90CAF9"), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        hDp.Children.Add(hTxt);
        header.Child = hDp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Footer con totales ──
        var footer = new Border {
            Background = Brushes.White, BorderBrush = B("#BBDEFB"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 14, 20, 14) };
        var footerG = new Grid();
        footerG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var totalesSp = new StackPanel();
        totalesSp.Children.Add(ResumenFila($"{lista.Count} cuota(s) cobrada(s)  ·  Total cobrado:", $"Gs. {totalCobrado:N0}", "#455A64", "#0E2F44"));
        totalesSp.Children.Add(ResumenFila($"Comisión ({porcComision:N2}%):", $"Gs. {comision:N0}", "#1565C0", "#1565C0", bold: true));
        Grid.SetColumn(totalesSp, 0);
        footerG.Children.Add(totalesSp);

        var btnCerrar = new Button {
            Content = "Cerrar", Height = 38, Width = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Background = B("#546E7A"), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        Grid.SetColumn(btnCerrar, 1);
        footerG.Children.Add(btnCerrar);
        footer.Child = footerG;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        // ── DataGrid ──
        var grid = BuildGrid();
        if (lista.Count == 0)
        {
            var vacio = new Border {
                Background = Brushes.White, Padding = new Thickness(0, 40, 0, 40) };
            vacio.Child = new TextBlock {
                Text = "No se encontraron cobranzas en el período seleccionado.",
                Foreground = B("#90A4AE"), FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center };
            root.Children.Add(vacio);
        }
        else
        {
            grid.ItemsSource = lista;
            root.Children.Add(grid);
        }

        Content = root;
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }

    private static DataGrid BuildGrid()
    {
        var txL = new Style(typeof(TextBlock));
        txL.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6, 0, 6, 0)));
        txL.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        txL.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.5));

        var txR = new Style(typeof(TextBlock), txL);
        txR.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        txR.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Consolas")));

        var hdrStyle = new Style(typeof(DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0E2F44"))));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        hdrStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));

        var dg = new DataGrid {
            AutoGenerateColumns  = false,
            IsReadOnly           = true,
            GridLinesVisibility  = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBDEFB")),
            RowBackground        = Brushes.White,
            AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F9FF")),
            SelectionMode        = DataGridSelectionMode.Single,
            ColumnHeaderStyle    = hdrStyle,
            BorderThickness      = new Thickness(0),
            Margin               = new Thickness(0) };

        var txTipo = new Style(typeof(TextBlock), txL);
        txTipo.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));

        dg.Columns.Add(new DataGridTextColumn { Header = "Fecha cobro",  Binding = new Binding("FechaFmt"),      Width = 95,  ElementStyle = txL });
        dg.Columns.Add(new DataGridTextColumn { Header = "Cliente",      Binding = new Binding("ClienteNombre"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = txL });
        dg.Columns.Add(new DataGridTextColumn { Header = "Cuota N°",     Binding = new Binding("NCuotaTexto"),   Width = 65,  ElementStyle = txR });
        // Pago parcial o completo — sin esto no se distinguía si el monto cobrado era la
        // cuota entera o solo un abono (el resto se cobra después, en otra fila aparte).
        dg.Columns.Add(new DataGridTextColumn { Header = "Tipo",         Binding = new Binding("TipoPagoTexto"), Width = 80,  ElementStyle = txTipo });
        dg.Columns.Add(new DataGridTextColumn { Header = "Monto Gs.",    Binding = new Binding("MontoFmt"),      Width = 110, ElementStyle = txR });

        dg.LoadingRow += (_, e) =>
        {
            if (e.Row.Item is DetalleCobranzaItem item && item.EsPagoParcial)
            {
                e.Row.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"));
                e.Row.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8C4B00"));
            }
        };

        return dg;
    }

    private static UIElement ResumenFila(string lbl, string val, string colorLbl, string colorVal, bool bold = false)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 20, 2) };
        sp.Children.Add(new TextBlock { Text = lbl, Foreground = B(colorLbl), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        sp.Children.Add(new TextBlock { Text = val, Foreground = B(colorVal), FontSize = bold ? 13 : 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }
}
