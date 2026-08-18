using CrediSoft.Core.Models;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Pagos;

public class SeleccionarCajaModal : Window
{
    public CajaMaster? CajaSeleccionada { get; private set; }

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public SeleccionarCajaModal(IEnumerable<CajaMaster> cajas, decimal neto)
    {
        Title                 = "Seleccionar caja de egreso";
        Width                 = 480;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#EEF4FB");
        FontFamily            = new FontFamily("Segoe UI");
        ShowInTaskbar         = false;

        var lista = cajas.ToList();
        var root  = new DockPanel();

        // ── Header ──────────────────────────────────────────────────────────
        var header = new Border { Background = B("#0E2F44"), Padding = new Thickness(22, 16, 22, 16) };
        var hRow   = new DockPanel();
        var icono  = new Border {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(21),
            Background = B("#1565C0"), Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock {
                Text = "🏦", FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center } };
        DockPanel.SetDock(icono, Dock.Left);
        hRow.Children.Add(icono);
        var hTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hTxt.Children.Add(new TextBlock { Text = "SELECCIONAR CAJA DE EGRESO",
            Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold });
        hTxt.Children.Add(new TextBlock {
            Text = $"Elegí la caja desde donde se registrará el pago de  Gs. {neto:N0}",
            Foreground = B("#90CAF9"), FontSize = 11, Margin = new Thickness(0, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap });
        hRow.Children.Add(hTxt);
        header.Child = hRow;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Footer ──────────────────────────────────────────────────────────
        var footer = new Border {
            Background = Brushes.White, BorderBrush = B("#BBDEFB"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 12, 20, 12) };
        var footRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var btnCancelar = MakeBtn("✕  Cancelar", "#546E7A");
        btnCancelar.Width = 130;
        btnCancelar.Click += (_, _) => { DialogResult = false; Close(); };

        var btnAceptar = MakeBtn("✔  Confirmar", "#1565C0");
        btnAceptar.Width = 140;
        btnAceptar.Margin = new Thickness(10, 0, 0, 0);

        footRow.Children.Add(btnCancelar);
        footRow.Children.Add(btnAceptar);
        footer.Child = footRow;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        // ── Body: lista de cajas ─────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(20, 18, 20, 18) };
        body.Children.Add(new TextBlock {
            Text = "CAJAS ABIERTAS", Foreground = B("#78909C"),
            FontSize = 10, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10) });

        Border? seleccionadoBorder = null;
        CajaMaster? seleccionado = lista.Count > 0 ? lista[0] : null;

        void Seleccionar(CajaMaster caja, Border card)
        {
            // Deseleccionar anterior
            if (seleccionadoBorder != null)
            {
                seleccionadoBorder.BorderBrush     = B("#BBDEFB");
                seleccionadoBorder.Background      = Brushes.White;
                seleccionadoBorder.BorderThickness = new Thickness(1);
                if (seleccionadoBorder.Child is Grid g)
                    foreach (var tb in FindTextBlocks(g))
                        if (tb.Tag is string t && t == "local")
                            tb.Foreground = B("#0E2F44");
            }
            // Seleccionar nuevo
            card.BorderBrush     = B("#1565C0");
            card.Background      = B("#E3F2FD");
            card.BorderThickness = new Thickness(2);
            if (card.Child is Grid g2)
                foreach (var tb in FindTextBlocks(g2))
                    if (tb.Tag is string t && t == "local")
                        tb.Foreground = B("#1565C0");
            seleccionadoBorder = card;
            seleccionado       = caja;
        }

        foreach (var caja in lista)
        {
            var card = new Border {
                Background = Brushes.White, BorderBrush = B("#BBDEFB"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { BlurRadius = 6, Opacity = 0.07, ShadowDepth = 1, Direction = 270, Color = Colors.Black } };

            var cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoSp = new StackPanel();
            var tbLocal = new TextBlock {
                Text = caja.LocalNombre, FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = B("#0E2F44"), Tag = "local" };
            var tbSub = new TextBlock {
                Text = $"Caja #{caja.IdCajaFisica}  ·  Abierta el {caja.FechaApertura:dd/MM/yyyy HH:mm}",
                FontSize = 11, Foreground = B("#78909C"), Margin = new Thickness(0, 3, 0, 0) };
            infoSp.Children.Add(tbLocal);
            infoSp.Children.Add(tbSub);
            Grid.SetColumn(infoSp, 0);
            cardGrid.Children.Add(infoSp);

            var badge = new Border {
                Background = B("#E8F5E9"), CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center };
            badge.Child = new TextBlock {
                Text = "ABIERTA", Foreground = B("#2E7D32"),
                FontSize = 10, FontWeight = FontWeights.Bold };
            Grid.SetColumn(badge, 1);
            cardGrid.Children.Add(badge);

            card.Child = cardGrid;

            var cajaRef = caja;
            card.MouseLeftButtonDown  += (_, _) => Seleccionar(cajaRef, card);
            card.MouseLeftButtonDown  += (_, e) => { if (e.ClickCount == 2) { Seleccionar(cajaRef, card); Confirmar(); } };

            body.Children.Add(card);

            // Preseleccionar la primera
            if (seleccionado == null)
                Seleccionar(caja, card);
        }

        void Confirmar()
        {
            if (seleccionado == null) return;
            CajaSeleccionada = seleccionado;
            DialogResult = true;
            Close();
        }

        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 380 };
        root.Children.Add(scroll);

        // Wire-up botón aceptar después de que Confirmar() esté definida
        btnAceptar.Click += (_, _) => Confirmar();

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
        KeyDown += (_, e) => { if (e.Key == Key.Enter) Confirmar(); };
    }

    private static Button MakeBtn(string txt, string bg) => new() {
        Content = txt, Height = 40, HorizontalAlignment = HorizontalAlignment.Stretch,
        Background = B(bg), Foreground = Brushes.White,
        BorderThickness = new Thickness(0), FontSize = 13,
        FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };

    private static IEnumerable<TextBlock> FindTextBlocks(DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock tb) yield return tb;
            foreach (var tb2 in FindTextBlocks(child)) yield return tb2;
        }
    }
}
