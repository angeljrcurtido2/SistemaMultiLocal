using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CrediSoft.UI.Views.Shared;

// Modal de historial crediticio completo: créditos reales del cliente (CABECERA_SALES, no
// solicitudes) con saldo, garante, y detalle de cuotas al seleccionar un crédito. Extraído de
// VentaCreditoWindow.OnVerHistorialCliente para reutilizarlo también desde la verificación de
// solicitud (VentasWindows) — antes esa pantalla mostraba un historial mucho más pobre, armado
// sobre CAB_SOL_SALES (solicitudes, no ventas), sin cuotas ni saldo ni garante.
public static class HistorialCrediticioModal
{
    public static async void Mostrar(Window owner, IDbConnectionFactory db, int idCliente, string nombreCliente)
    {
        try
        {
            using var conn = db.Create();
            var historial = await conn.QueryAsync<dynamic>(
                "SELECT TOP 30" +
                "  cb.IDCAB         AS IdCab," +
                "  cb.NVENTA        AS NVenta," +
                "  cb.FECHA         AS Fecha," +
                "  l.NOMBRE         AS Local," +
                "  u.NOMBRE_USUARIO AS Vendedor," +
                "  cb.DEBE          AS Total," +
                "  cb.HABER         AS Pagado," +
                "  (cb.DEBE-cb.HABER) AS Saldo," +
                "  CASE cb.ESTADO WHEN 1 THEN 'Activo' WHEN 0 THEN 'Cancelado' ELSE 'Otro' END AS Estado," +
                "  CASE cb.FORMA_DE_VENTA WHEN 2 THEN 'Crédito' WHEN 1 THEN 'Contado' ELSE 'Otro' END AS TipoVenta," +
                "  CASE WHEN cb.ID_GARANTE > 0 THEN ISNULL(g.NOMBRE_CLIENTE, '') ELSE '' END AS Garante" +
                " FROM CABECERA_SALES cb" +
                " LEFT JOIN LOCALES   l ON cb.ID_LOCAL   = l.ID_LOCAL" +
                " LEFT JOIN USUARIOS  u ON cb.ID_USUARIO = u.ID_USUARIO" +
                " LEFT JOIN CLIENTES  g ON g.ID_CLIENTE = cb.ID_GARANTE AND cb.ID_GARANTE > 0" +
                " WHERE cb.ID_CLIENTE = @id AND cb.FORMA_DE_VENTA = 2" +
                " ORDER BY cb.FECHA DESC",
                new { id = idCliente });

            // ── Parsear filas ──────────────────────────────────────────────
            var rows = historial.Select(r => {
                var d = (IDictionary<string, object>)r;
                string V(string k)   => d.TryGetValue(k, out var v) && v != null ? v.ToString()! : "";
                int Int(string k)    => d.TryGetValue(k, out var v) && v != null ? Convert.ToInt32(v) : 0;
                string Dec(string k) => d.TryGetValue(k, out var v) && v != null ? Convert.ToDecimal(v).ToString("N0") : "0";
                string Dt(string k)  => d.TryGetValue(k, out var v) && v is DateTime dt ? dt.ToString("dd/MM/yyyy") : "";
                return (IdCab: Int("IdCab"), NVenta: V("NVenta"), Fecha: Dt("Fecha"), Local: V("Local"),
                        Vendedor: V("Vendedor"), Total: Dec("Total"), Pagado: Dec("Pagado"),
                        Saldo: Dec("Saldo"), Estado: V("Estado"),
                        TipoVenta: V("TipoVenta"), Garante: V("Garante"));
            }).ToList();

            System.Windows.Media.Brush Br(System.Windows.Media.Color c)
                => new System.Windows.Media.SolidColorBrush(c);

            // ── Ventana ────────────────────────────────────────────────────
            // Owner=owner + Show() (no ShowDialog): con Show(), Owner no bloquea nada — pero
            // SÍ hace falta para que Windows no la trate como ventana "huérfana" del mismo
            // proceso y la minimice sola apenas otra ventana (ej. un módulo abierto desde el
            // menú) toma el foco (mismo bug real ya corregido en DetalleSolicitudWindow, ver
            // AbrirDetalle). Costo conocido: con Owner, este historial queda siempre por
            // encima de su Owner en el Z-order — aceptado a cambio de que no se minimice sola.
            var win = new Window {
                Title = $"Historial Crediticio — {nombreCliente}",
                Width = 1030, Height = 640, MinWidth = 820, MinHeight = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner,
                ResizeMode = ResizeMode.CanResize,
                Tag = App.PermitirMinimizarTag,
                Background = Br(System.Windows.Media.Color.FromRgb(245, 245, 250))
            };

            var root = new DockPanel();

            // Cabecera azul
            var header = new Border {
                Background = Br(System.Windows.Media.Color.FromRgb(21, 101, 192)),
                Padding = new Thickness(16, 10, 16, 10)
            };
            var hSp = new StackPanel();
            hSp.Children.Add(new TextBlock {
                Text = "HISTORIAL CREDITICIO", FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White
            });
            hSp.Children.Add(new TextBlock {
                Text = nombreCliente, FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(187, 222, 251))
            });
            header.Child = hSp;
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            // Totalizador inferior
            decimal totalDeuda = rows.Where(r => r.Estado == "Activo")
                .Sum(r => decimal.TryParse(r.Saldo.Replace(".", "").Replace(",",""), out var x) ? x : 0);
            var footer = new Border {
                Background = Br(System.Windows.Media.Color.FromRgb(30, 30, 40)),
                Padding = new Thickness(16, 8, 16, 8)
            };
            footer.Child = new TextBlock {
                Text = $"Créditos activos: {rows.Count(r => r.Estado == "Activo")}   |   Saldo total activo: {totalDeuda:N0} Gs.",
                Foreground = System.Windows.Media.Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold
            };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            // ── Grid principal: fila superior = créditos, fila inferior = cuotas del crédito seleccionado ──
            var mainGrid = new Grid { Margin = new Thickness(8) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.8, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star) });

            var dgCreditos = new DataGrid {
                IsReadOnly = true, AutoGenerateColumns = false,
                SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                CanUserResizeRows = false, RowHeight = 30, ColumnHeaderHeight = 32,
                BorderThickness = new Thickness(1), BorderBrush = Br(System.Windows.Media.Color.FromRgb(220,220,220)),
                Background = System.Windows.Media.Brushes.White,
                AlternatingRowBackground = Br(System.Windows.Media.Color.FromRgb(243, 244, 246)),
                FontSize = 11
            };
            var hdrStyleCred = new Style(typeof(DataGridColumnHeader));
            hdrStyleCred.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, Br(System.Windows.Media.Color.FromRgb(30, 58, 95))));
            hdrStyleCred.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, System.Windows.Media.Brushes.White));
            hdrStyleCred.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
            hdrStyleCred.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 6, 4, 6)));
            dgCreditos.ColumnHeaderStyle = hdrStyleCred;

            dgCreditos.Columns.Add(new DataGridTextColumn { Header = "N° Venta", Binding = new System.Windows.Data.Binding("NVenta"), Width = 80 });
            dgCreditos.Columns.Add(new DataGridTextColumn { Header = "Fecha", Binding = new System.Windows.Data.Binding("Fecha"), Width = 90 });
            dgCreditos.Columns.Add(new DataGridTextColumn { Header = "Local", Binding = new System.Windows.Data.Binding("Local"), Width = 130 });
            dgCreditos.Columns.Add(new DataGridTextColumn { Header = "Vendedor", Binding = new System.Windows.Data.Binding("Vendedor"), Width = 120 });
            dgCreditos.Columns.Add(new DataGridTextColumn { Header = "Tipo", Binding = new System.Windows.Data.Binding("TipoVenta"), Width = 70 });
            dgCreditos.Columns.Add(new DataGridTextColumn { Header = "Garante", Binding = new System.Windows.Data.Binding("GaranteFmt"), Width = 140 });
            dgCreditos.Columns.Add(new DataGridTextColumn { Header = "Total Gs.", Binding = new System.Windows.Data.Binding("Total"), Width = 90 });
            dgCreditos.Columns.Add(new DataGridTextColumn { Header = "Pagado Gs.", Binding = new System.Windows.Data.Binding("Pagado"), Width = 90 });
            dgCreditos.Columns.Add(new DataGridTextColumn { Header = "Saldo Gs.", Binding = new System.Windows.Data.Binding("Saldo"), Width = 90 });
            dgCreditos.Columns.Add(new DataGridTextColumn { Header = "Estado", Binding = new System.Windows.Data.Binding("Estado"), Width = 80 });

            var itemsCreditos = rows.Select(r => new {
                r.IdCab, r.NVenta, r.Fecha, r.Local, r.Vendedor, r.TipoVenta,
                GaranteFmt = string.IsNullOrEmpty(r.Garante) ? "—" : r.Garante,
                r.Total, r.Pagado, r.Saldo, r.Estado
            }).ToList();
            dgCreditos.ItemsSource = itemsCreditos;

            Grid.SetRow(dgCreditos, 0);
            mainGrid.Children.Add(dgCreditos);

            // Etiqueta + grid de cuotas (detalle del crédito seleccionado arriba)
            var cuotasPanel = new DockPanel();

            var cuotasHeaderBar = new DockPanel { Margin = new Thickness(2, 2, 2, 8) };

            var lblCuotas = new TextBlock {
                Text = "Seleccioná un crédito arriba para ver el detalle de sus cuotas.",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = Br(System.Windows.Media.Color.FromRgb(60, 60, 60)),
                VerticalAlignment = VerticalAlignment.Center
            };
            cuotasHeaderBar.Children.Add(lblCuotas);

            var badgesSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(badgesSp, Dock.Right);

            Border MkBadge(out TextBlock valueTb, string label)
            {
                var sp = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
                sp.Children.Add(new TextBlock {
                    Text = label, FontSize = 10, FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White, HorizontalAlignment = HorizontalAlignment.Center
                });
                valueTb = new TextBlock {
                    FontSize = 16, FontWeight = FontWeights.ExtraBold,
                    Foreground = System.Windows.Media.Brushes.White, HorizontalAlignment = HorizontalAlignment.Center
                };
                sp.Children.Add(valueTb);
                return new Border {
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 5, 14, 6),
                    Child = sp
                };
            }

            var btnVerArticulos = new Button {
                Content = "📦 Ver Artículos", Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.SemiBold,
                Background = Br(System.Windows.Media.Color.FromRgb(30, 58, 95)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
            };
            badgesSp.Children.Add(btnVerArticulos);

            var badgeDias = MkBadge(out var tbDias, "DÍAS DE ATRASO TOTAL");
            var badgeCuotas = MkBadge(out var tbCuotas, "CUOTAS ATRASADAS");
            badgesSp.Children.Add(badgeDias);
            badgesSp.Children.Add(badgeCuotas);
            cuotasHeaderBar.Children.Add(badgesSp);

            DockPanel.SetDock(cuotasHeaderBar, Dock.Top);
            cuotasPanel.Children.Add(cuotasHeaderBar);

            var dgCuotas = new DataGrid {
                IsReadOnly = true, AutoGenerateColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                CanUserResizeRows = false, RowHeight = 40, ColumnHeaderHeight = 36,
                BorderThickness = new Thickness(1), BorderBrush = Br(System.Windows.Media.Color.FromRgb(220,220,220)),
                Background = System.Windows.Media.Brushes.White,
                AlternatingRowBackground = Br(System.Windows.Media.Color.FromRgb(243, 244, 246)),
                FontSize = 12
            };
            var hdrStyleCuo = new Style(typeof(DataGridColumnHeader));
            hdrStyleCuo.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, Br(System.Windows.Media.Color.FromRgb(66, 133, 199))));
            hdrStyleCuo.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, System.Windows.Media.Brushes.White));
            hdrStyleCuo.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
            hdrStyleCuo.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 6, 4, 6)));
            dgCuotas.ColumnHeaderStyle = hdrStyleCuo;

            // Resalta en rojo tenue las cuotas que tuvieron atraso (Mora > 0)
            var rowStyleCuo = new Style(typeof(DataGridRow));
            var triggerMora = new DataTrigger { Binding = new System.Windows.Data.Binding("TieneMora"), Value = true };
            triggerMora.Setters.Add(new Setter(DataGridRow.BackgroundProperty, Br(System.Windows.Media.Color.FromRgb(255, 235, 238))));
            triggerMora.Setters.Add(new Setter(DataGridRow.ForegroundProperty, Br(System.Windows.Media.Color.FromRgb(198, 40, 40))));
            rowStyleCuo.Triggers.Add(triggerMora);
            dgCuotas.RowStyle = rowStyleCuo;

            dgCuotas.Columns.Add(new DataGridTextColumn { Header = "N° Cuota",     Binding = new System.Windows.Data.Binding("NCuota"),  Width = 80 });
            dgCuotas.Columns.Add(new DataGridTextColumn { Header = "Monto Gs.",    Binding = new System.Windows.Data.Binding("MontoFmt"), Width = 100 });
            dgCuotas.Columns.Add(new DataGridTextColumn { Header = "Vencimiento",  Binding = new System.Windows.Data.Binding("Vto"),      Width = 100 });
            dgCuotas.Columns.Add(new DataGridTextColumn { Header = "Estado",      Binding = new System.Windows.Data.Binding("Estado"),   Width = 90 });
            dgCuotas.Columns.Add(new DataGridTextColumn { Header = "Mora (días)", Binding = new System.Windows.Data.Binding("Mora"),      Width = 90 });
            dgCuotas.Columns.Add(new DataGridTextColumn { Header = "Fecha de pago", Binding = new System.Windows.Data.Binding("FechaPago"), Width = 150 });
            dgCuotas.Columns.Add(new DataGridTextColumn { Header = "Obs.",        Binding = new System.Windows.Data.Binding("Obs"),       Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

            cuotasPanel.Children.Add(dgCuotas);
            Grid.SetRow(cuotasPanel, 2);
            mainGrid.Children.Add(cuotasPanel);

            var cuotaRepo = App.Services.GetRequiredService<ICuotaRepository>();
            int? idCabSeleccionado = null;
            dgCreditos.SelectionChanged += async (_, _) =>
            {
                if (dgCreditos.SelectedItem == null) return;
                dynamic sel = dgCreditos.SelectedItem;
                int idCab = sel.IdCab;
                idCabSeleccionado = idCab;

                // NCUOTA=1 en GENERADAS es siempre la ENTREGA inicial, no una cuota real —
                // ver comentario en Cuota.NCuotaVisible.
                var cuotas = (await cuotaRepo.ObtenerHistorialAsync(idCab)).Select(c => new {
                    NCuota = c.NCuota == 1 ? "Entrega" : (c.NCuota - 1).ToString(),
                    MontoFmt = c.Monto.ToString("N0"), c.Vto, c.Estado, c.Mora,
                    FechaPago = c.FechaPago ?? "—", c.Obs, TieneMora = c.Mora > 0
                }).ToList();
                dgCuotas.ItemsSource = cuotas;

                lblCuotas.Text = $"Cuotas del crédito N° venta {sel.NVenta}:";

                int cuotasAtrasadas = cuotas.Count(c => c.TieneMora);
                int diasAtrasoTotal = cuotas.Sum(c => c.Mora);
                var colorAtraso = cuotasAtrasadas > 0
                    ? System.Windows.Media.Color.FromRgb(211, 47, 47)   // rojo
                    : System.Windows.Media.Color.FromRgb(46, 125, 50);  // verde
                badgeDias.Background = Br(colorAtraso);
                badgeCuotas.Background = Br(colorAtraso);
                tbDias.Text = diasAtrasoTotal.ToString();
                tbCuotas.Text = cuotasAtrasadas.ToString();
                badgesSp.Visibility = Visibility.Visible;
            };
            badgesSp.Visibility = Visibility.Collapsed;

            btnVerArticulos.Click += async (_, _) =>
            {
                if (idCabSeleccionado == null) return;
                var arts = await cuotaRepo.ObtenerArticulosAsync(idCabSeleccionado.Value);
                var items = arts.Select(a => new CrediSoft.UI.Views.Cobros.ArticuloVenta(a.Descripcion, a.Cantidad, a.PVenta));
                new CrediSoft.UI.Views.Cobros.ArticulosModal(idCabSeleccionado.Value, items) { Owner = win }.ShowDialog();
            };

            root.Children.Add(mainGrid);

            win.Content = root;
            win.Show();
        }
        catch (Exception ex) { MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
