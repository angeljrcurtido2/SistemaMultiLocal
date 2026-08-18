using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Informes;

public record FilaProformaCuota(int NCuota, DateTime Vencimiento, decimal Monto, decimal PctRecargo);

public class ProformaModal : Window
{
    private static SolidColorBrush B(Color c) => new(c);
    private static readonly Color C1 = Color.FromRgb(21, 101, 192);   // azul principal
    private static readonly Color C2 = Color.FromRgb(30, 58, 95);     // azul oscuro (header grid)
    private static readonly Color CBg = Color.FromRgb(245, 247, 250);
    private static readonly Color CS = Color.FromRgb(90, 100, 115);   // texto secundario

    public ProformaModal(
        string codigo, string descripcion,
        decimal precioLista, decimal pctDescuento, decimal descuento, decimal valorNeto,
        decimal entrega, decimal saldo, decimal pctRecargo, decimal recargoTotal,
        decimal totalCuotas, decimal totalAPagar,
        IEnumerable<FilaProformaCuota> cuotas)
    {
        var lista = cuotas.ToList();

        Title = "Proforma de plan de pagos";
        Width = 620; Height = 640; MinWidth = 520; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = B(CBg);
        FontFamily = new FontFamily("Segoe UI"); FontSize = 12;
        ShowInTaskbar = false;

        var root = new DockPanel();

        // ── Header ──────────────────────────────────────────────────────────
        var header = new Border { Background = B(C1), Padding = new Thickness(16, 10, 16, 10) };
        var hSp = new StackPanel();
        hSp.Children.Add(new TextBlock {
            Text = "PROFORMA — PLAN DE PAGOS", FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
        });
        hSp.Children.Add(new TextBlock {
            Text = $"{codigo}  —  {descripcion}", FontSize = 11,
            Foreground = B(Color.FromRgb(187, 222, 251)),
        });
        header.Child = hSp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Resumen (descuento / entrega / recargo) ─────────────────────────
        var resumen = new Border {
            Background = Brushes.White, Margin = new Thickness(12, 10, 12, 0),
            BorderBrush = B(Color.FromRgb(220, 220, 220)), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 10, 14, 10),
        };
        var resGrid = new Grid();
        for (int i = 0; i < 3; i++) resGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resGrid.RowDefinitions.Add(new RowDefinition());
        resGrid.RowDefinitions.Add(new RowDefinition());

        UIElement Campo(string lbl, string val, int col, int row, Color? color = null)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, row == 0 ? 8 : 0) };
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 9.5, Foreground = B(CS) });
            sp.Children.Add(new TextBlock {
                Text = val, FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = B(color ?? Color.FromRgb(30, 30, 30)),
            });
            Grid.SetColumn(sp, col); Grid.SetRow(sp, row);
            return sp;
        }

        resGrid.Children.Add(Campo("PRECIO LISTA",  $"Gs. {precioLista:N0}", 0, 0));
        resGrid.Children.Add(Campo($"DESCUENTO ({pctDescuento:N1}%)", $"Gs. {descuento:N0}", 1, 0));
        resGrid.Children.Add(Campo("VALOR NETO",     $"Gs. {valorNeto:N0}", 2, 0, C1));
        resGrid.Children.Add(Campo("ENTREGA",        $"Gs. {entrega:N0}", 0, 1));
        resGrid.Children.Add(Campo("SALDO A FINANCIAR", $"Gs. {saldo:N0}", 1, 1));
        resGrid.Children.Add(Campo($"RECARGO TOTAL ({pctRecargo:N1}% x cuota)", $"Gs. {recargoTotal:N0}", 2, 1, Color.FromRgb(198, 40, 40)));

        resumen.Child = resGrid;
        DockPanel.SetDock(resumen, Dock.Top);
        root.Children.Add(resumen);

        // ── Footer (totales + cerrar) ────────────────────────────────────────
        var footer = new Border {
            Background = B(C2), Padding = new Thickness(16, 10, 16, 10),
        };
        var footGrid = new Grid();
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var footSp = new StackPanel();
        // lista incluye la fila de Entrega (NCuota=1) — se resta acá para que "X cuota(s)"
        // siga contando solo las cuotas reales pactadas, igual que antes.
        var cantCuotasReales = lista.Count(c => c.NCuota != 1);
        footSp.Children.Add(new TextBlock {
            Text = $"{cantCuotasReales} cuota(s)  —  Total a cuotas: Gs. {totalCuotas:N0}",
            FontSize = 11, Foreground = B(Color.FromRgb(200, 210, 225)),
        });
        footSp.Children.Add(new TextBlock {
            Text = $"COSTO TOTAL DEL ARTÍCULO (entrega + cuotas): Gs. {totalAPagar:N0}",
            FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
        });
        Grid.SetColumn(footSp, 0);
        footGrid.Children.Add(footSp);

        var btnCerrar = new Button {
            Content = "✕  Cerrar", Padding = new Thickness(16, 7, 16, 7),
            Background = B(Color.FromRgb(92, 107, 192)), Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
        };
        btnCerrar.Click += (_, __) => Close();
        Grid.SetColumn(btnCerrar, 1);
        footGrid.Children.Add(btnCerrar);

        footer.Child = footGrid;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        // ── Grid de cuotas ────────────────────────────────────────────────────
        var dg = new DataGrid {
            IsReadOnly = true, AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            CanUserResizeRows = false, RowHeight = 34, ColumnHeaderHeight = 34,
            Margin = new Thickness(12, 10, 12, 10),
            BorderThickness = new Thickness(1), BorderBrush = B(Color.FromRgb(220, 220, 220)),
            Background = Brushes.White, AlternatingRowBackground = B(Color.FromRgb(243, 244, 246)),
            FontSize = 12,
        };
        var hdrStyle = new Style(typeof(DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, B(C2)));
        hdrStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));
        hdrStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(10, 6, 4, 6)));
        dg.ColumnHeaderStyle = hdrStyle;

        var items = lista.Select(c => new {
            // NCuota=1 es siempre la fila de la Entrega (ver comentario en MostrarProforma) —
            // se muestra "Entrega" en vez de "1" para que coincida con NCuotaTexto (Cuota.cs),
            // el mismo criterio que ya usa la grilla de Cobrar Cuota.
            NCuota = c.NCuota == 1 ? "Entrega" : c.NCuota.ToString(),
            Vencimiento = c.Vencimiento.ToString("dd/MM/yyyy"),
            MontoFmt = c.Monto.ToString("N0"),
            PctRecargoFmt = c.PctRecargo > 0 ? $"{c.PctRecargo:N1}%" : "Sin recargo",
        }).ToList();
        dg.ItemsSource = items;

        dg.Columns.Add(new DataGridTextColumn { Header = "N° Cuota",     Binding = new System.Windows.Data.Binding("NCuota"),        Width = 80 });
        dg.Columns.Add(new DataGridTextColumn { Header = "Vencimiento estimado", Binding = new System.Windows.Data.Binding("Vencimiento"), Width = 150 });
        dg.Columns.Add(new DataGridTextColumn { Header = "% Recargo",    Binding = new System.Windows.Data.Binding("PctRecargoFmt"), Width = 110 });
        dg.Columns.Add(new DataGridTextColumn { Header = "Monto Gs.",    Binding = new System.Windows.Data.Binding("MontoFmt"),      Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

        root.Children.Add(dg);

        Content = root;
    }
}
