using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Cobros;

public class EliminarCreditoDialog : Window
{
    public bool Confirmado { get; private set; } = false;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public EliminarCreditoDialog(string nombreCliente, int idCredito)
    {
        Title                 = "Eliminar Crédito";
        Width                 = 460;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#F5F7FA");
        FontFamily            = new FontFamily("Segoe UI");

        var root = new DockPanel();

        // ── Header rojo peligro ──────────────────────────────────────────────
        var header = new Border {
            Background = B("#B71C1C"),
            Padding    = new Thickness(28, 20, 28, 20) };

        var headerDp = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

        var icono = new Border {
            Width = 50, Height = 50,
            CornerRadius = new CornerRadius(25),
            Background   = B("#C62828"),
            Margin       = new Thickness(0, 0, 18, 0),
            VerticalAlignment = VerticalAlignment.Center };
        icono.Child = new TextBlock {
            Text     = "⚠", FontSize = 24,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };
        DockPanel.SetDock(icono, Dock.Left);
        headerDp.Children.Add(icono);

        var txtSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        txtSp.Children.Add(new TextBlock {
            Text       = "ELIMINAR CRÉDITO",
            Foreground = Brushes.White,
            FontSize   = 17,
            FontWeight = FontWeights.Bold });
        txtSp.Children.Add(new TextBlock {
            Text       = "Esta acción no puede deshacerse",
            Foreground = B("#FFCDD2"),
            FontSize   = 11,
            Margin     = new Thickness(0, 4, 0, 0) });
        headerDp.Children.Add(txtSp);
        header.Child = headerDp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Cuerpo ───────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(28, 22, 28, 0) };

        // Tarjeta datos
        var card = new Border {
            Background      = Brushes.White,
            BorderBrush     = B("#FFCDD2"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Padding         = new Thickness(18, 14, 18, 14),
            Margin          = new Thickness(0, 0, 0, 14),
            Effect = new DropShadowEffect {
                BlurRadius = 8, Opacity = 0.08,
                ShadowDepth = 2, Direction = 270, Color = Colors.Black } };

        var cardSp = new StackPanel();
        cardSp.Children.Add(new TextBlock {
            Text       = "DATOS DEL CRÉDITO",
            Foreground = B("#C62828"),
            FontSize   = 9, FontWeight = FontWeights.Bold,
            Margin     = new Thickness(0, 0, 0, 12) });
        cardSp.Children.Add(FilaGrid("Cliente",  nombreCliente, resaltar: true));
        cardSp.Children.Add(Sep());
        cardSp.Children.Add(FilaGrid("Crédito",  $"#{idCredito}"));
        card.Child = cardSp;
        body.Children.Add(card);

        // Advertencia
        var warn = new Border {
            Background      = B("#FFF8E1"),
            BorderBrush     = B("#FFE082"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Padding         = new Thickness(16, 12, 16, 12),
            Margin          = new Thickness(0, 0, 0, 22) };
        warn.Child = new TextBlock {
            Text         = "⚠  Se eliminarán el crédito y TODAS sus cuotas asociadas. Esta operación requiere autorización de administrador.",
            Foreground   = B("#5D4037"),
            FontSize     = 11,
            TextWrapping = TextWrapping.Wrap,
            LineHeight   = 18 };
        body.Children.Add(warn);

        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        // ── Footer ───────────────────────────────────────────────────────────
        var footer = new Border {
            Background      = Brushes.White,
            BorderBrush     = B("#FFCDD2"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding         = new Thickness(28, 14, 28, 14) };

        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var btnNo = MakeBtn("✖  Cancelar", "#546E7A");
        btnNo.Click += (_, _) => { Confirmado = false; Close(); };
        Grid.SetColumn(btnNo, 0);
        footerGrid.Children.Add(btnNo);

        var btnSi = MakeBtn("🗑  Eliminar crédito", "#C62828");
        btnSi.FontWeight = FontWeights.Bold;
        btnSi.Click += (_, _) => { Confirmado = true; Close(); };
        Grid.SetColumn(btnSi, 2);
        footerGrid.Children.Add(btnSi);

        footer.Child = footerGrid;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;

        KeyDown += (_, e) => {
            if (e.Key == System.Windows.Input.Key.Escape) { Confirmado = false; Close(); }
        };
    }

    private static UIElement FilaGrid(string label, string valor, bool resaltar = false)
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lbl = new TextBlock {
            Text = label, Foreground = B("#78909C"), FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lbl, 0);
        g.Children.Add(lbl);
        var val = new TextBlock {
            Text = valor,
            Foreground = resaltar ? B("#B71C1C") : B("#212121"),
            FontSize   = resaltar ? 12 : 11,
            FontWeight = resaltar ? FontWeights.Bold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextWrapping        = TextWrapping.Wrap,
            TextAlignment       = TextAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Center };
        Grid.SetColumn(val, 1);
        g.Children.Add(val);
        return g;
    }

    private static Border Sep() => new() {
        Height = 1, Background = B("#FFEBEE"),
        Margin = new Thickness(0, 4, 0, 4) };

    private static Button MakeBtn(string txt, string bg) => new() {
        Content             = txt, Height = 40,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Background          = B(bg), Foreground = Brushes.White,
        BorderThickness     = new Thickness(0),
        FontSize            = 12, FontWeight = FontWeights.SemiBold,
        Cursor              = System.Windows.Input.Cursors.Hand };
}
