using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Shared;

// Modal de selección de local con buscador y tarjetas — mismo diseño ya usado en
// VentaContadoWindow.AbrirBuscadorLocal, extraído acá para reutilizar en cualquier
// pantalla que necesite un selector de local más rico que un ComboBox plano
// (relevante con 15+ locales, donde filtrar por texto es más rápido que desplazar).
public static class SelectorLocalBuscadorModal
{
    public record ResultadoLocal(int Id, string Nombre);

    public static ResultadoLocal? Mostrar(
        Window owner, List<(int Id, string Nombre)> locales, int? idActual, string titulo = "Seleccionar local")
    {
        var BrPrim    = new SolidColorBrush(Color.FromRgb(0x0E, 0x2F, 0x44));
        var BrPrimDark= new SolidColorBrush(Color.FromRgb(0x1A, 0x4F, 0x6E));
        var BrClaro   = new SolidColorBrush(Color.FromRgb(176, 212, 236));
        var BrTarjeta = new SolidColorBrush(Color.FromRgb(229, 231, 235));
        var BrHoverBg = new SolidColorBrush(Color.FromRgb(238, 244, 251));
        var BrFondo   = new SolidColorBrush(Color.FromRgb(244, 247, 250));
        var BrBlanco  = Brushes.White;
        var BrGrisTxt = new SolidColorBrush(Color.FromRgb(107, 114, 128));
        var BrGris    = new SolidColorBrush(Color.FromRgb(107, 114, 128));

        ResultadoLocal? resultado = null;

        var dlg = new Window
        {
            Title = titulo, Width = 440, Height = 560,
            MinWidth = 380, MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner,
            ResizeMode = ResizeMode.CanResize, Background = BrFondo,
            FontFamily = new FontFamily("Segoe UI"),
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Header con buscador ──────────────────────────────────────────────
        var hdr = new Border { Background = BrPrim, Padding = new Thickness(16, 14, 16, 14) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock
        {
            Text = "🏬  " + titulo, FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = BrBlanco, Margin = new Thickness(0, 0, 0, 10),
        });
        var searchBorder = new Border
        {
            Background = BrPrimDark, CornerRadius = new CornerRadius(6),
            BorderBrush = BrClaro, BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 0, 10, 0),
        };
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        searchRow.Children.Add(new TextBlock
        {
            Text = "🔎", FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = BrClaro, Margin = new Thickness(0, 0, 8, 0),
        });
        var txtBuscar = new TextBox
        {
            Height = 32, FontSize = 12.5,
            Background = Brushes.Transparent, Foreground = BrBlanco, CaretBrush = BrBlanco,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Width = 360,
        };
        searchRow.Children.Add(txtBuscar);
        searchBorder.Child = searchRow;
        hdrSp.Children.Add(searchBorder);
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── Lista de tarjetas ─────────────────────────────────────────────────
        var scroll = new ScrollViewer { Padding = new Thickness(10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var lista = new StackPanel();
        scroll.Content = lista;
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);

        int? seleccionadoId = idActual;

        void Renderizar(string filtro)
        {
            lista.Children.Clear();
            var filtrados = string.IsNullOrWhiteSpace(filtro)
                ? locales
                : locales.Where(l =>
                    l.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                    l.Id.ToString().Contains(filtro, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filtrados.Count == 0)
            {
                lista.Children.Add(new TextBlock
                {
                    Text = "Sin resultados", FontStyle = FontStyles.Italic, Foreground = BrGrisTxt,
                    HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0),
                });
                return;
            }

            foreach (var (id, nombre) in filtrados)
            {
                bool esActual = id == idActual;
                bool esSeleccionado = id == seleccionadoId;

                var card = new Border
                {
                    Background      = esSeleccionado ? BrHoverBg : BrBlanco,
                    BorderBrush     = esSeleccionado ? BrPrim : BrTarjeta,
                    BorderThickness = new Thickness(esSeleccionado ? 2 : 1),
                    CornerRadius    = new CornerRadius(8),
                    Padding         = new Thickness(14, 10, 14, 10),
                    Margin          = new Thickness(0, 0, 0, 8),
                    Cursor          = Cursors.Hand,
                    Tag             = id,
                };

                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icono = new Border
                {
                    Width = 34, Height = 34, CornerRadius = new CornerRadius(17),
                    Background = esSeleccionado ? BrPrim : BrFondo,
                    Margin = new Thickness(0, 0, 12, 0),
                    Child = new TextBlock
                    {
                        Text = id.ToString(), FontSize = 12, FontWeight = FontWeights.Bold,
                        Foreground = esSeleccionado ? Brushes.White : BrPrim,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                };
                Grid.SetColumn(icono, 0); row.Children.Add(icono);

                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textStack.Children.Add(new TextBlock
                {
                    Text = nombre, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = BrPrim,
                });
                if (esActual)
                    textStack.Children.Add(new TextBlock
                    {
                        Text = "Local actual de la sesión", FontSize = 10, Foreground = BrGrisTxt,
                        Margin = new Thickness(0, 2, 0, 0),
                    });
                Grid.SetColumn(textStack, 1); row.Children.Add(textStack);

                if (esSeleccionado)
                {
                    var chip = new TextBlock
                    {
                        Text = "✔", FontSize = 15, FontWeight = FontWeights.Bold,
                        Foreground = BrPrim, VerticalAlignment = VerticalAlignment.Center,
                    };
                    Grid.SetColumn(chip, 2); row.Children.Add(chip);
                }

                card.Child = row;
                card.MouseLeftButtonDown += (_, e) =>
                {
                    seleccionadoId = id;
                    Renderizar(txtBuscar.Text);
                    if (e.ClickCount == 2) ConfirmarSeleccion(id, nombre);
                };
                lista.Children.Add(card);
            }
        }

        void ConfirmarSeleccion(int idSel, string nomSel)
        {
            resultado = new ResultadoLocal(idSel, nomSel);
            dlg.DialogResult = true;
            dlg.Close();
        }

        txtBuscar.TextChanged += (_, _) => Renderizar(txtBuscar.Text);
        Renderizar("");

        // ── Footer ───────────────────────────────────────────────────────────
        var ftr = new Border
        {
            Background = BrBlanco, Padding = new Thickness(14, 10, 14, 10),
            BorderBrush = BrTarjeta, BorderThickness = new Thickness(0, 1, 0, 0),
        };
        var btnSel = new Button
        {
            Content = "✔  Seleccionar", Height = 34, Padding = new Thickness(16, 0, 16, 0),
            Background = BrPrim, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, FontSize = 12.5, Cursor = Cursors.Hand,
        };
        var btnCan = new Button
        {
            Content = "✕  Cancelar", Height = 34, Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(8, 0, 0, 0),
            Background = BrGris, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, FontSize = 12.5, Cursor = Cursors.Hand,
        };
        btnCan.Click += (_, _) => dlg.Close();
        btnSel.Click += (_, _) =>
        {
            if (seleccionadoId is not int idSel) return;
            var nomSel = locales.FirstOrDefault(l => l.Id == idSel).Nombre ?? "";
            ConfirmarSeleccion(idSel, nomSel);
        };
        var ftrSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        ftrSp.Children.Add(btnSel); ftrSp.Children.Add(btnCan);
        ftr.Child = ftrSp;
        Grid.SetRow(ftr, 2); root.Children.Add(ftr);

        dlg.Content = root;
        dlg.Loaded += (_, _) => txtBuscar.Focus();
        dlg.ShowDialog();

        return resultado;
    }
}
