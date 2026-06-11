using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.UI.Views.Compras;
using Dapper;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Ventas;

// Artículo enriquecido con precio del local para el selector
internal class ArticuloConPrecio
{
    public int Id { get; set; }
    public string Ca { get; set; } = string.Empty;
    public string D { get; set; } = string.Empty;
    public string MarcaNombre { get; set; } = string.Empty;
    public decimal PventaLocal { get; set; }
    public decimal Pc { get; set; }
    public decimal Iva { get; set; }
}

// Línea del carrito — todos los campos visibles en la grilla
internal class LineaDetalle
{
    public int     IdArt          { get; set; }
    public string  ArticuloCodigo { get; set; } = string.Empty;
    public string  ArticuloNombre { get; set; } = string.Empty;
    public decimal Cantidad       { get; set; }
    public decimal Pv             { get; set; }
    public decimal EntregaLinea   { get; set; }
    public int     CuotasLinea    { get; set; }
    public decimal CostoMensual   { get; set; }
    public decimal ValorFinal     { get; set; }
    public decimal Iva            { get; set; }
    public decimal Pc             { get; set; }
    public decimal Subtotal       => Cantidad * Pv;
}

public partial class VentaCreditoWindow : Window
{
    private readonly IArticuloRepository _artRepo;
    private readonly IVentaRepository    _ventaRepo;
    private readonly IDbConnectionFactory _db;

    private PersonaItem? _clienteActual;
    private PersonaItem? _garanteActual;
    private int _idRef1, _idRef2;
    private byte _idLocalForm;   // local seleccionado en el form (botón Local)
    private ArticuloConPrecio? _articuloActual;
    private readonly List<LineaDetalle> _carrito = new();

    public VentaCreditoWindow()
    {
        InitializeComponent();
        var svc = App.Services;
        _artRepo  = svc.GetRequiredService<IArticuloRepository>();
        _ventaRepo= svc.GetRequiredService<IVentaRepository>();
        _db       = svc.GetRequiredService<IDbConnectionFactory>();

        DtpSolicitud.SelectedDate = DateTime.Today;
        DtpFigurar.SelectedDate   = DateTime.Today;


        Loaded += async (_, _) => await GenerarNumeroSolicitudAsync();
    }

    private async Task GenerarNumeroSolicitudAsync()
    {
        var nSol = await _ventaRepo.ObtenerNumeroSolicitudAsync();
        TxtNroSolicitud.Text = nSol.ToString();
        TxtNumero.Text       = nSol.ToString().PadLeft(15, '0');
    }

    // ── Buscar solicitud existente ────────────────────────────────────────────
    private void OnBuscarSolicitud(object sender, RoutedEventArgs e)
    {
        // Por implementar: buscar solicitud por número
    }

    // ── Local / Vendedor ──────────────────────────────────────────────────────
    private async void OnSeleccionarLocal(object sender, RoutedEventArgs e)
    {
        try
        {
            var localRepo = App.Services.GetRequiredService<ILocalRepository>();
            var locales   = (await localRepo.ListarTodosAsync()).ToList();
            if (locales.Count == 0) { MessageBox.Show("No hay locales registrados.", "Aviso"); return; }

            // Modal de selección
            var win = new Window {
                Title = "Seleccionar Local", Width = 340, Height = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.White
            };
            Local? seleccionado = null;
            var sp = new StackPanel { Margin = new Thickness(12) };
            sp.Children.Add(new TextBlock { Text = "Seleccione un local:", FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) });
            var lb = new ListBox { Height = 260 };
            foreach (var l in locales)
                lb.Items.Add(new ListBoxItem { Content = $"{l.IdLocal} — {l.NombreLocal}", Tag = l });
            sp.Children.Add(lb);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,8,0,0) };
            var btnOk = new Button { Content = "Aceptar", Width = 80, Height = 26, Margin = new Thickness(0,0,6,0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,100,180)),
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
            var btnCx = new Button { Content = "Cancelar", Width = 80, Height = 26 };
            btnOk.Click += (_, _) => { if (lb.SelectedItem is ListBoxItem li && li.Tag is Local loc) seleccionado = loc; win.Close(); };
            btnCx.Click += (_, _) => win.Close();
            lb.MouseDoubleClick += (_, _) => { if (lb.SelectedItem is ListBoxItem li && li.Tag is Local loc) seleccionado = loc; win.Close(); };
            bar.Children.Add(btnOk); bar.Children.Add(btnCx);
            sp.Children.Add(bar);
            win.Content = sp;
            win.ShowDialog();

            if (seleccionado != null)
            {
                _idLocalForm        = (byte)seleccionado.IdLocal;
                TxtLocal.Text       = seleccionado.IdLocal.ToString();
                TxtLocalNombre.Text = seleccionado.NombreLocal;
            }
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error"); }
    }

    private async void OnBuscarVendedor(object sender, RoutedEventArgs e)
    {
        try
        {
            var usuarioRepo = App.Services.GetRequiredService<IUsuarioRepository>();
            var usuarios    = (await usuarioRepo.ListarTodosAsync()).ToList();
            if (usuarios.Count == 0) { MessageBox.Show("No hay usuarios registrados.", "Aviso"); return; }

            var win = new Window {
                Title = "Seleccionar Vendedor", Width = 380, Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.White
            };
            Usuario? seleccionado = null;
            var sp = new StackPanel { Margin = new Thickness(12) };
            sp.Children.Add(new TextBlock { Text = "Seleccione un vendedor:", FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) });
            var lb = new ListBox { Height = 280 };
            foreach (var u in usuarios)
                lb.Items.Add(new ListBoxItem { Content = $"{u.CodigoUsuario} — {u.NombreUsuario}", Tag = u });
            sp.Children.Add(lb);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,8,0,0) };
            var btnOk = new Button { Content = "Aceptar", Width = 80, Height = 26, Margin = new Thickness(0,0,6,0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0,100,0)),
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
            var btnCx = new Button { Content = "Cancelar", Width = 80, Height = 26 };
            btnOk.Click += (_, _) => { if (lb.SelectedItem is ListBoxItem li && li.Tag is Usuario u) seleccionado = u; win.Close(); };
            btnCx.Click += (_, _) => win.Close();
            lb.MouseDoubleClick += (_, _) => { if (lb.SelectedItem is ListBoxItem li && li.Tag is Usuario u) seleccionado = u; win.Close(); };
            bar.Children.Add(btnOk); bar.Children.Add(btnCx);
            sp.Children.Add(bar);
            win.Content = sp;
            win.ShowDialog();

            if (seleccionado != null)
                TxtVendedor.Text = seleccionado.NombreUsuario;
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error"); }
    }

    // ── CLIENTE ───────────────────────────────────────────────────────────────

    private async void OnBuscarCliente(object sender, RoutedEventArgs e)
    {
        var win = new BuscadorPersonaWindow(_db) { Owner = this };
        if (win.ShowDialog() == true && win.PersonaSeleccionada != null)
        {
            _clienteActual = win.PersonaSeleccionada;
            RellenarCamposCliente(_clienteActual);
        }
        await Task.CompletedTask;
    }

    private void RellenarCamposCliente(PersonaItem c)
    {
        TxtClienteNombre.Text       = c.Nombre;
        TxtClienteCI.Text           = c.Ci;
        TxtClienteRUC.Text          = c.Ruc;
        TxtClienteDireccion.Text    = c.Direccion;
        TxtClienteSexo.Text         = c.Sexo;
        TxtClienteCelular.Text      = c.Telefono;
        TxtClienteCiudad.Text       = c.Ciudad;
        TxtClienteEstado.Text       = c.EstadoTexto;
        TxtClienteECV.Text          = c.Ecv;
        TxtClienteInformconf.Text   = c.Inforconf;
        TxtClienteLugarTrabajo.Text = c.Empresa;
        TxtClienteTelLab.Text       = c.TelLaboral;
        TxtClienteCondicion.Text    = c.Condicion;
        TxtClienteTipo.Text         = c.Tipo;
        TxtClienteAntiguedad.Text   = c.Antiguedad;
        TxtClienteCredMax.Text      = c.CredMax > 0 ? c.CredMax.ToString("N0") : "";
        TxtClienteSaldo.Text        = c.SaldoActivo > 0 ? c.SaldoActivo.ToString("N0") : "0";
        TxtClienteConyuge.Text      = c.Conyuge;
        TxtClienteVencCI.Text       = c.VencCI;
    }

    private async void OnVerHistorialCliente(object sender, RoutedEventArgs e)
    {
        if (_clienteActual == null) { MessageBox.Show("Primero seleccione un cliente.", "Aviso"); return; }
        try
        {
            using var conn = _db.Create();
            var historial = await conn.QueryAsync<dynamic>(
                "SELECT TOP 30" +
                "  cb.NVENTA        AS NVenta," +
                "  cb.FECHA         AS Fecha," +
                "  l.NOMBRE         AS Local," +
                "  u.NOMBRE_USUARIO AS Vendedor," +
                "  cb.DEBE          AS Total," +
                "  cb.HABER         AS Pagado," +
                "  (cb.DEBE-cb.HABER) AS Saldo," +
                "  CASE cb.ESTADO WHEN 1 THEN 'Activo' WHEN 0 THEN 'Cancelado' ELSE 'Otro' END AS Estado" +
                " FROM CABECERA_SALES cb" +
                " LEFT JOIN LOCALES   l ON cb.ID_LOCAL   = l.ID_LOCAL" +
                " LEFT JOIN USUARIOS  u ON cb.ID_USUARIO = u.ID_USUARIO" +
                " WHERE cb.ID_CLIENTE = @id AND cb.FORMA_DE_VENTA = 2" +
                " ORDER BY cb.FECHA DESC",
                new { id = _clienteActual.Id });

            // ── Parsear filas ──────────────────────────────────────────────
            var rows = historial.Select(r => {
                var d = (IDictionary<string, object>)r;
                string V(string k)   => d.TryGetValue(k, out var v) && v != null ? v.ToString()! : "";
                string Dec(string k) => d.TryGetValue(k, out var v) && v != null ? Convert.ToDecimal(v).ToString("N0") : "0";
                string Dt(string k)  => d.TryGetValue(k, out var v) && v is DateTime dt ? dt.ToString("dd/MM/yyyy") : "";
                return (NVenta: V("NVenta"), Fecha: Dt("Fecha"), Local: V("Local"),
                        Vendedor: V("Vendedor"), Total: Dec("Total"), Pagado: Dec("Pagado"),
                        Saldo: Dec("Saldo"), Estado: V("Estado"));
            }).ToList();

            // ── Helpers de color ───────────────────────────────────────────
            System.Windows.Media.Color ColEst(string est) => est switch {
                "Activo"    => System.Windows.Media.Color.FromRgb(220, 38, 38),   // rojo
                "Cancelado" => System.Windows.Media.Color.FromRgb(22, 163, 74),   // verde
                _           => System.Windows.Media.Color.FromRgb(180, 83, 9)     // naranja
            };
            System.Windows.Media.Brush Br(System.Windows.Media.Color c)
                => new System.Windows.Media.SolidColorBrush(c);

            // ── Ventana ────────────────────────────────────────────────────
            var win = new Window {
                Title = $"Historial Crediticio — {_clienteActual.Nombre}",
                Width = 820, Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                ResizeMode = ResizeMode.CanResize,
                Background = Br(System.Windows.Media.Color.FromRgb(245, 245, 250))
            };

            var root = new DockPanel();

            // Cabecera naranja
            var header = new Border {
                Background = Br(System.Windows.Media.Color.FromRgb(234, 88, 12)),
                Padding = new Thickness(16, 10, 16, 10)
            };
            var hSp = new StackPanel();
            hSp.Children.Add(new TextBlock {
                Text = "HISTORIAL CREDITICIO", FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White
            });
            hSp.Children.Add(new TextBlock {
                Text = _clienteActual.Nombre, FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 220, 180))
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

            // Grid de encabezados de columna
            string[] hdrs = { "N° Venta", "Fecha", "Local", "Vendedor", "Total Gs.", "Pagado Gs.", "Saldo Gs.", "Estado" };
            double[] widths = { 80, 90, 130, 130, 90, 90, 90, 90 };

            Grid MkGrid()
            {
                var g = new Grid { Margin = new Thickness(0) };
                for (int i = 0; i < widths.Length; i++)
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i]) });
                return g;
            }

            var hdrGrid = MkGrid();
            hdrGrid.Background = Br(System.Windows.Media.Color.FromRgb(55, 65, 81));
            for (int i = 0; i < hdrs.Length; i++)
            {
                var tb = new TextBlock {
                    Text = hdrs[i], Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.Bold, FontSize = 11,
                    Padding = new Thickness(8, 6, 4, 6), VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(tb, i); hdrGrid.Children.Add(tb);
            }

            // Filas de datos
            var rowsPanel = new StackPanel();
            for (int ri = 0; ri < rows.Count; ri++)
            {
                var row = rows[ri];
                var estColor = ColEst(row.Estado);
                var bg = ri % 2 == 0
                    ? System.Windows.Media.Color.FromRgb(255, 255, 255)
                    : System.Windows.Media.Color.FromRgb(243, 244, 246);
                var rg = MkGrid();
                rg.Background = Br(bg);

                string[] vals = { row.NVenta, row.Fecha, row.Local, row.Vendedor,
                                  row.Total, row.Pagado, row.Saldo, row.Estado };
                for (int ci = 0; ci < vals.Length; ci++)
                {
                    UIElement cell;
                    if (ci == vals.Length - 1)
                    {
                        // Badge de estado
                        var badge = new Border {
                            Background = Br(estColor), CornerRadius = new CornerRadius(10),
                            Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(4, 4, 4, 4),
                            HorizontalAlignment = HorizontalAlignment.Left
                        };
                        badge.Child = new TextBlock {
                            Text = vals[ci], Foreground = System.Windows.Media.Brushes.White,
                            FontSize = 10, FontWeight = FontWeights.Bold
                        };
                        cell = badge;
                    }
                    else
                    {
                        cell = new TextBlock {
                            Text = vals[ci], FontSize = 11,
                            Padding = new Thickness(8, 5, 4, 5), VerticalAlignment = VerticalAlignment.Center,
                            Foreground = ci >= 4
                                ? Br(System.Windows.Media.Color.FromRgb(30, 30, 30))
                                : Br(System.Windows.Media.Color.FromRgb(30, 30, 30))
                        };
                    }
                    Grid.SetColumn(cell, ci); rg.Children.Add(cell);
                }

                // Borde izquierdo de color según estado
                var rowBorder = new Border {
                    BorderBrush = Br(estColor), BorderThickness = new Thickness(4, 0, 0, 0),
                    Child = rg, Margin = new Thickness(0, 0, 0, 1)
                };
                rowsPanel.Children.Add(rowBorder);
            }

            if (rows.Count == 0)
                rowsPanel.Children.Add(new TextBlock {
                    Text = "Sin ventas a crédito registradas.", FontSize = 13,
                    Foreground = Br(System.Windows.Media.Color.FromRgb(120, 120, 120)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 0)
                });

            var bodyPanel = new StackPanel();
            bodyPanel.Children.Add(hdrGrid);
            bodyPanel.Children.Add(rowsPanel);

            var scroll = new ScrollViewer {
                Content = bodyPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(8)
            };
            root.Children.Add(scroll);

            win.Content = root;
            win.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void OnVerCedulaCliente(object sender, RoutedEventArgs e)
    {
        if (_clienteActual == null) { MessageBox.Show("Primero seleccione un cliente.", "Aviso"); return; }
        var ci = _clienteActual.Ci;
        var id = _clienteActual.Id;

        byte[]? datos = null;
        try
        {
            using var conn = _db.Create();
            datos = await conn.QueryFirstOrDefaultAsync<byte[]>(
                "SELECT TOP 1 DATOS FROM FOTOS WHERE CI = @ci OR IDCLIE = @id ORDER BY IDFOTO DESC",
                new { ci, id });
        }
        catch (Exception ex) { MessageBox.Show("Error al consultar la foto: " + ex.Message, "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Error); return; }

        if (datos == null || datos.Length == 0) {
            MessageBox.Show($"No se encontró foto de cédula para CI: {ci}.", "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        System.Windows.Media.Imaging.BitmapImage bmp;
        try
        {
            bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(datos);
            bmp.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 500;
            bmp.EndInit();
            bmp.Freeze();
        }
        catch (Exception ex) { MessageBox.Show("No se pudo cargar la imagen: " + ex.Message, "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Error); return; }

        var img = new System.Windows.Controls.Image {
            Source = bmp, Stretch = System.Windows.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Margin = new Thickness(8)
        };
        var win2 = new Window {
            Title = $"Cédula — CI: {ci}", Width = 580, Height = 500,
            MinWidth = 420, MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.CanResize,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30))
        };
        win2.Content = new ScrollViewer {
            Content = img,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30))
        };
        win2.ShowDialog();
    }

    // ── GARANTE ───────────────────────────────────────────────────────────────

    private async void OnBuscarGarante(object sender, RoutedEventArgs e)
    {
        var win = new BuscadorPersonaWindow(_db) { Owner = this };
        if (win.ShowDialog() == true && win.PersonaSeleccionada != null)
        {
            _garanteActual = win.PersonaSeleccionada;
            RellenarCamposGarante(_garanteActual);
        }
        await Task.CompletedTask;
    }

    private void RellenarCamposGarante(PersonaItem g)
    {
        TxtGaranteNombre.Text       = g.Nombre;
        TxtGaranteCI.Text           = g.Ci;
        TxtGaranteDireccion.Text    = g.Direccion;
        TxtGaranteTelefono.Text     = g.Telefono;
        TxtGaranteLugarTrabajo.Text = g.Empresa;
        TxtGaranteTelLab.Text       = g.TelLaboral;
        TxtGaranteAntiguedad.Text   = "";
        TxtGaranteVencCI.Text       = "";
        TxtGaranteECV.Text          = g.Ecv;
        TxtGaranteConyuge.Text      = "";
    }

    // ── REFERENCIAS ──────────────────────────────────────────────────────────

    private async void OnBuscarRef1(object sender, RoutedEventArgs e)
    {
        var ref1 = await BuscarReferenciaAsync();
        if (ref1 == null) return;
        _idRef1          = ref1.Id;
        TxtRef1Nom.Text  = ref1.Nombre;
        TxtRef1Tel.Text  = ref1.Telefono;
        TxtRef1Trab.Text = ref1.Trabajo;
    }

    private async void OnBuscarRef2(object sender, RoutedEventArgs e)
    {
        var ref2 = await BuscarReferenciaAsync();
        if (ref2 == null) return;
        _idRef2          = ref2.Id;
        TxtRef2Nom.Text  = ref2.Nombre;
        TxtRef2Tel.Text  = ref2.Telefono;
        TxtRef2Trab.Text = ref2.Trabajo;
    }

    private record ReferenciaItem(int Id, string Ci, string Nombre, string Telefono, string Trabajo);

    private async Task<ReferenciaItem?> BuscarReferenciaAsync()
    {
        List<ReferenciaItem> lista;
        try
        {
            using var conn = _db.Create();
            lista = (await conn.QueryAsync<ReferenciaItem>(
                "SELECT ID_REFERENCIA as Id, CI as Ci, NOMBRE as Nombre," +
                " TELEFONO as Telefono, TRABAJO as Trabajo" +
                " FROM REFERENCIAS ORDER BY NOMBRE")).ToList();
        }
        catch (Exception ex) { MessageBox.Show("Error: " + ex.Message, "Error"); return null; }

        if (lista.Count == 0) { MessageBox.Show("No hay referencias registradas.", "Aviso"); return null; }

        ReferenciaItem? seleccionada = null;

        var win = new Window {
            Title = "Lista de referencias", Width = 860, Height = 680,
            MinWidth = 700, MinHeight = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.CanResize, Background = System.Windows.Media.Brushes.White
        };

        var root = new DockPanel { LastChildFill = true };

        // Cabecera
        var hdr = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 80, 0)),
            Padding = new Thickness(14, 12, 14, 12)
        };
        var hdrGrid = new Grid();
        hdrGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        hdrGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        hdrGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Título + ícono
        var titleSp = new StackPanel { Orientation = Orientation.Horizontal };
        titleSp.Children.Add(new TextBlock {
            Text = "🔍", FontSize = 16, Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        titleSp.Children.Add(new TextBlock {
            Text = "Lista de Referencias",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 15
        });
        titleSp.Children.Add(new TextBlock {
            Text = "  —  busque por nombre o C.I.",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,220,180)),
            FontSize = 11, VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetRow(titleSp, 0); hdrGrid.Children.Add(titleSp);

        // Cuadro de búsqueda con ícono
        var searchBox = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 120, 30)),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 160, 80)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 0, 10, 0)
        };
        var searchInner = new StackPanel { Orientation = Orientation.Horizontal };
        searchInner.Children.Add(new TextBlock {
            Text = "🔎", FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,220,180))
        });
        var txtBuscar = new TextBox {
            Height = 34, MinWidth = 400, FontSize = 13,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.White,
            CaretBrush = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        searchInner.Children.Add(txtBuscar);
        searchBox.Child = searchInner;
        Grid.SetRow(searchBox, 2); hdrGrid.Children.Add(searchBox);

        hdr.Child = hdrGrid;
        DockPanel.SetDock(hdr, Dock.Top);
        root.Children.Add(hdr);

        // Botones inferiores
        var btnBar = new Border {
            Padding = new Thickness(8, 6, 8, 6),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200))
        };
        var btnSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnAceptar  = new Button { Content = "Aceptar",  Width = 80, Height = 26, Margin = new Thickness(0,0,6,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,140,60)),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
        var btnCancelar = new Button { Content = "Cerrar", Width = 80, Height = 26 };
        btnSp.Children.Add(btnAceptar);
        btnSp.Children.Add(btnCancelar);
        btnBar.Child = btnSp;
        DockPanel.SetDock(btnBar, Dock.Bottom);
        root.Children.Add(btnBar);

        // DataGrid
        var dg = new DataGrid {
            IsReadOnly = true, AutoGenerateColumns = false,
            SelectionMode = DataGridSelectionMode.Single,
            FontSize = 12, Margin = new Thickness(0),
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            CanUserResizeRows = false, RowHeight = 40, ColumnHeaderHeight = 40,
            BorderThickness = new Thickness(0), RowHeaderWidth = 0,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            EnableRowVirtualization = true,
            CanUserResizeColumns = true
        };
        // Estilo de filas alternas
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(DataGridRow.MinHeightProperty, 28.0));
        dg.RowStyle = rowStyle;

        dg.Columns.Add(new DataGridTextColumn { Header = "ID",
            Binding = new System.Windows.Data.Binding("Id"), MinWidth = 45, Width = 50 });
        dg.Columns.Add(new DataGridTextColumn { Header = "C.I.",
            Binding = new System.Windows.Data.Binding("Ci"), MinWidth = 100, Width = 120 });
        dg.Columns.Add(new DataGridTextColumn { Header = "NOMBRE Y APELLIDO",
            Binding = new System.Windows.Data.Binding("Nombre"),
            MinWidth = 180, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        dg.Columns.Add(new DataGridTextColumn { Header = "Teléfono",
            Binding = new System.Windows.Data.Binding("Telefono"), MinWidth = 110, Width = 120 });
        dg.Columns.Add(new DataGridTextColumn { Header = "Trabajo",
            Binding = new System.Windows.Data.Binding("Trabajo"), MinWidth = 110, Width = 140 });

        var listaFiltrada = new System.Collections.ObjectModel.ObservableCollection<ReferenciaItem>(lista);
        dg.ItemsSource = listaFiltrada;

        txtBuscar.TextChanged += (_, _) => {
            var term = txtBuscar.Text.Trim().ToLower();
            listaFiltrada.Clear();
            foreach (var r in lista.Where(r =>
                string.IsNullOrEmpty(term) ||
                r.Nombre.ToLower().Contains(term) ||
                r.Ci.ToLower().Contains(term)))
                listaFiltrada.Add(r);
        };

        void Aceptar() {
            if (dg.SelectedItem is ReferenciaItem r) { seleccionada = r; win.Close(); }
        }
        dg.MouseDoubleClick += (_, _) => Aceptar();
        btnAceptar.Click    += (_, _) => Aceptar();
        btnCancelar.Click   += (_, _) => win.Close();

        root.Children.Add(dg);
        win.Content = root;
        win.ShowDialog();
        return seleccionada;
    }

    // ── ARTÍCULOS ─────────────────────────────────────────────────────────────

    private async void OnBuscarArticulo(object sender, RoutedEventArgs e)
    {
        var term    = TxtCodigoArticulo.Text.Trim();
        var session = SessionService.Instance;
        byte idLocal = _idLocalForm > 0 ? _idLocalForm : (byte)(session.LocalActual?.IdLocal ?? 0);

        if (string.IsNullOrEmpty(term))
        {
            var win = new BuscadorArticuloModal(_db, idLocal) { Owner = this };
            if (win.ShowDialog() == true && win.ArticuloSeleccionado is ArticuloResumen r)
            {
                await SeleccionarArticuloAsync(new ArticuloConPrecio {
                    Id          = r.IdArt,
                    Ca          = r.Codigo,
                    D           = r.Descripcion,
                    PventaLocal = r.PrecioVenta,
                    Pc          = r.PrecioCosto,
                    Iva         = 0
                });
            }
        }
        else
        {
            await BuscarArticuloPorCodigoAsync(term);
        }
    }

    private async void OnCodigoArticuloKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await BuscarArticuloPorCodigoAsync(TxtCodigoArticulo.Text.Trim());
    }

    private async Task BuscarArticuloPorCodigoAsync(string codigo)
    {
        if (string.IsNullOrEmpty(codigo)) return;
        var session = SessionService.Instance;
        var art = await _artRepo.BuscarPorCodigoAsync(codigo);
        if (art == null)
        {
            MessageBox.Show("No se encontró artículo con ese código.", "Buscar",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        int idLocalPrecio = _idLocalForm > 0 ? _idLocalForm : (session.LocalActual?.IdLocal ?? 0);
        var precio = idLocalPrecio > 0 ? await _artRepo.ObtenerPrecioLocalAsync(art.Id, idLocalPrecio) : null;
        await SeleccionarArticuloAsync(new ArticuloConPrecio
        {
            Id = art.Id, Ca = art.Ca, D = art.D,
            PventaLocal = precio?.Pventa ?? 0,
            Pc          = precio?.Pc     ?? 0,
            Iva         = art.Iva
        });
    }

    private async Task SeleccionarArticuloAsync(ArticuloConPrecio art)
    {
        _articuloActual = art;
        TxtCodigoArticulo.Text  = art.Ca;
        TxtDescArticulo.Text    = art.D;
        TxtPrecioArticulo.Text  = art.PventaLocal.ToString("N0").Replace(",", ".");
        TxtEntregaArticulo.Text = "0";
        TxtCantidad.Text        = "1";
        RecalcularLinea();
        await Task.CompletedTask;
    }

    private void OnArticuloCalcular(object sender, TextChangedEventArgs e) => RecalcularLinea();
    private void OnCuotasChanged(object sender, SelectionChangedEventArgs e) => RecalcularLinea();

    private void RecalcularLinea()
    {
        if (TxtPrecioArticulo == null || TxtEntregaArticulo == null || CboCuotas == null ||
            TxtCostoMensual  == null || TxtValorFinal      == null) return;

        var precio   = ParseMiles(TxtPrecioArticulo.Text);
        var entrega  = ParseMiles(TxtEntregaArticulo.Text);
        var cuotasTag = ((CboCuotas?.SelectedItem as ComboBoxItem)?.Tag?.ToString()) ?? "6";
        int.TryParse(cuotasTag, out var cuotas);
        if (cuotas <= 0) cuotas = 6;

        var saldo       = Math.Max(0, precio - entrega);
        var costoMens   = cuotas > 0 ? Math.Ceiling(saldo / cuotas) : 0;
        var valorFinal  = entrega + costoMens * cuotas;

        TxtCostoMensual.Text = costoMens.ToString("N0").Replace(",", ".");
        TxtValorFinal.Text   = valorFinal.ToString("N0").Replace(",", ".");
    }

    private void OnAgregarArticulo(object sender, RoutedEventArgs e)
    {
        if (_articuloActual == null)
        {
            MessageBox.Show("Primero busque y seleccione un artículo.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!decimal.TryParse(TxtCantidad.Text, out var cantidad) || cantidad <= 0)
        {
            MessageBox.Show("Ingrese una cantidad válida.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var precio    = ParseMiles(TxtPrecioArticulo.Text);
        var entrega   = ParseMiles(TxtEntregaArticulo.Text);
        var costoMens = ParseMiles(TxtCostoMensual.Text);
        var valFinal  = ParseMiles(TxtValorFinal.Text);
        var cuotasTag = ((CboCuotas?.SelectedItem as ComboBoxItem)?.Tag?.ToString()) ?? "6";
        int.TryParse(cuotasTag, out var cuotas);

        var existente = _carrito.FirstOrDefault(x => x.IdArt == _articuloActual.Id);
        if (existente != null)
            existente.Cantidad += cantidad;
        else
            _carrito.Add(new LineaDetalle
            {
                IdArt          = _articuloActual.Id,
                ArticuloCodigo = _articuloActual.Ca,
                ArticuloNombre = _articuloActual.D,
                Cantidad       = cantidad,
                Pv             = precio,
                EntregaLinea   = entrega,
                CuotasLinea    = cuotas,
                CostoMensual   = costoMens,
                ValorFinal     = valFinal,
                Iva            = _articuloActual.Iva,
                Pc             = _articuloActual.Pc
            });

        RefrescarCarrito();
        LimpiarLineaArticulo();
    }

    private void OnGridDetalleDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridDetalle.SelectedItem is LineaDetalle linea)
        {
            _carrito.Remove(linea);
            RefrescarCarrito();
        }
    }

    private void RefrescarCarrito()
    {
        GridDetalle.ItemsSource = null;
        GridDetalle.ItemsSource = _carrito.ToList();
        var total = _carrito.Sum(x => x.Subtotal);
        TxtTotal.Text = total.ToString("N0").Replace(",", ".");
    }

    private void LimpiarLineaArticulo()
    {
        _articuloActual         = null;
        TxtCodigoArticulo.Text  = "";
        TxtDescArticulo.Text    = "";
        TxtPrecioArticulo.Text  = "";
        TxtEntregaArticulo.Text = "0";
        TxtCostoMensual.Text    = "";
        TxtValorFinal.Text      = "";
        TxtCantidad.Text        = "1";
        TxtCodigoArticulo.Focus();
    }

    // ── INGRESOS / EGRESOS ────────────────────────────────────────────────────

    private void OnIngresoEgresoChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtISalario == null || TxtIHonorario == null || TxtIConyuge == null || TxtIOtros == null ||
            TxtEGasto   == null || TxtECuota    == null || TxtEAlquiler == null || TxtEOtros  == null ||
            TxtITotal   == null || TxtETotal    == null) return;

        var iTotal = ParseMiles(TxtISalario.Text) + ParseMiles(TxtIHonorario.Text)
                   + ParseMiles(TxtIConyuge.Text)  + ParseMiles(TxtIOtros.Text);
        var eTotal = ParseMiles(TxtEGasto.Text)   + ParseMiles(TxtECuota.Text)
                   + ParseMiles(TxtEAlquiler.Text) + ParseMiles(TxtEOtros.Text);
        TxtITotal.Text = iTotal.ToString("N0").Replace(",", ".");
        TxtETotal.Text = eTotal.ToString("N0").Replace(",", ".");
    }

    // ── GUARDAR SOLICITUD ─────────────────────────────────────────────────────

    private async void OnConfirmarVenta(object sender, RoutedEventArgs e)
    {
        if (_clienteActual == null)
        {
            MessageBox.Show("Debe seleccionar un cliente.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_carrito.Count == 0)
        {
            MessageBox.Show("Debe agregar al menos un artículo.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirmar = MessageBox.Show(
            $"¿Guardar solicitud de crédito para {_clienteActual.Nombre}?",
            "Confirmar solicitud",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmar != MessageBoxResult.Yes) return;

        var session = SessionService.Instance;
        if (session.UsuarioActual == null || session.LocalActual == null) return;

        BtnGuardar.IsEnabled = false;
        try
        {
            int.TryParse(TxtNroSolicitud.Text, out var nSol);
            var numeroStr = TxtNumero.Text.PadLeft(15, '0');

            var total      = ParseMiles(TxtTotal.Text);
            var totalEnt   = _carrito.Sum(x => x.EntregaLinea * x.Cantidad);
            var cuotasTag  = ((CboCuotas?.SelectedItem as ComboBoxItem)?.Tag?.ToString()) ?? "6";
            int.TryParse(cuotasTag, out var cuotas);
            var montoCuota = _carrito.Sum(x => x.CostoMensual);
            var fechaCobro = DtpFigurar.SelectedDate ?? DateTime.Today.AddMonths(1);

            // Ingresos / Egresos
            var iSal  = ParseMiles(TxtISalario.Text);   var iHon  = ParseMiles(TxtIHonorario.Text);
            var iCon  = ParseMiles(TxtIConyuge.Text);    var iOtr  = ParseMiles(TxtIOtros.Text);
            var iTotal= ParseMiles(TxtITotal.Text);
            var eGas  = ParseMiles(TxtEGasto.Text);      var eCuo  = ParseMiles(TxtECuota.Text);
            var eAlq  = ParseMiles(TxtEAlquiler.Text);   var eOtr  = ParseMiles(TxtEOtros.Text);
            var eTotal= ParseMiles(TxtETotal.Text);

            // Referencias
            var ref1Nom = TxtRef1Nom.Text; var ref1Tel = TxtRef1Tel.Text; var ref1Trab = TxtRef1Trab.Text;
            var ref2Nom = TxtRef2Nom.Text; var ref2Tel = TxtRef2Tel.Text; var ref2Trab = TxtRef2Trab.Text;
            var rc1Nom  = TxtRefC1Nom.Text; var rc1Tel  = TxtRefC1Tel.Text;
            var rc2Nom  = TxtRefC2Nom.Text; var rc2Tel  = TxtRefC2Tel.Text;

            // SOLICITUD_CS: AGENTE=1 crea CAB_SOL_SALES (devuelve @IDCABSOL),
            //               AGENTE>1 agrega solo DET_SOL_SALES (requiere @IDCABSOL del primer insert)
            long idCabSolGenerado = 0;
            for (int i = 0; i < _carrito.Count; i++)
            {
                var linea     = _carrito[i];
                var esPrimero = i == 0;

                var prm = new SolicitudParams(
                    Agente:         esPrimero ? 1 : 2,
                    IdCabSol:       esPrimero ? 0 : idCabSolGenerado,
                    Numero:         numeroStr,
                    IdLocal:        (byte)session.LocalActual.IdLocal,
                    IdUsuario:      session.UsuarioActual.IdUsuario,
                    IdCliente:      _clienteActual.Id,
                    IdGarante:      _garanteActual?.Id ?? 0,
                    IdRef1: _idRef1, IdRef2: _idRef2,
                    NomRef1: ref1Nom,  TelRef1: ref1Tel,  TrabRef1: ref1Trab,
                    NomRef2: ref2Nom,  TelRef2: ref2Tel,  TrabRef2: ref2Trab,
                    NomRc1: rc1Nom,    TelRc1: rc1Tel,    TrabRc1: "",
                    NomRc2: rc2Nom,    TelRc2: rc2Tel,    TrabRc2: "",
                    ISalario: iSal, IHonorario: iHon, IConyuge: iCon, IOtros: iOtr, ITotal: iTotal,
                    EGasto: eGas, ECuota: eCuo, EAlquiler: eAlq, EOtros: eOtr, ETotal: eTotal,
                    TotalSale:       total,
                    TotalEntrega:    totalEnt,
                    FechaCobro:      fechaCobro,
                    CantCuotas:      (byte)cuotas,
                    TotalMontoCuota: montoCuota,
                    Nota:            "",
                    Estado:          0,
                    IdDetSol:        0,
                    IdSolicitud:     nSol,
                    IdArt:           linea.IdArt,
                    Ca:              linea.ArticuloCodigo,
                    D:               linea.ArticuloNombre,
                    Precio:          linea.Pv,
                    Entrega:         linea.EntregaLinea,
                    CantCuotasDet:   (byte)linea.CuotasLinea,
                    CostoMensual:    linea.CostoMensual,
                    ValorFinal:      linea.ValorFinal,
                    Cant:            linea.Cantidad,
                    Subtotal:        linea.Subtotal);

                var idCabSolResultado = await _ventaRepo.GuardarSolicitudAsync(prm);
                if (esPrimero)
                    idCabSolGenerado = idCabSolResultado;
            }

            MessageBox.Show(
                $"Solicitud guardada.\nNúmero: {numeroStr}\n\nPendiente de aprobación.",
                "Solicitud enviada",
                MessageBoxButton.OK, MessageBoxImage.Information);

            OnNuevo(sender, e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar la solicitud: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            BtnGuardar.IsEnabled = true;
        }
    }

    // ── Nuevo / Eliminar ──────────────────────────────────────────────────────

    private async void OnNuevo(object sender, RoutedEventArgs e)
    {
        _clienteActual  = null;
        _garanteActual  = null;
        _articuloActual = null;
        _carrito.Clear();

        TxtClienteNombre.Text = ""; TxtClienteCI.Text = "";
        TxtClienteRUC.Text = ""; TxtClienteDireccion.Text = "";
        TxtClienteSexo.Text = ""; TxtClienteCelular.Text = "";
        TxtClienteCiudad.Text = ""; TxtClienteEstado.Text = "";
        TxtClienteECV.Text = ""; TxtClienteInformconf.Text = "";
        TxtClienteLugarTrabajo.Text = ""; TxtClienteTelLab.Text = "";
        TxtClienteCondicion.Text = ""; TxtClienteTipo.Text = "";
        TxtClienteAntiguedad.Text = ""; TxtClienteCredMax.Text = "";
        TxtClienteSaldo.Text = ""; TxtClienteConyuge.Text = "";
        TxtClienteVencCI.Text = "";

        TxtGaranteNombre.Text = ""; TxtGaranteCI.Text = "";
        TxtGaranteDireccion.Text = ""; TxtGaranteTelefono.Text = "";
        TxtGaranteLugarTrabajo.Text = ""; TxtGaranteTelLab.Text = "";
        TxtGaranteAntiguedad.Text = ""; TxtGaranteVencCI.Text = "";
        TxtGaranteECV.Text = ""; TxtGaranteConyuge.Text = "";

        TxtRef1Nom.Text=""; TxtRef1Tel.Text=""; TxtRef1Trab.Text="";
        TxtRef2Nom.Text=""; TxtRef2Tel.Text=""; TxtRef2Trab.Text="";
        TxtRefC1Nom.Text=""; TxtRefC1Tel.Text="";
        TxtRefC2Nom.Text=""; TxtRefC2Tel.Text="";

        TxtISalario.Text="0"; TxtIHonorario.Text="0"; TxtIConyuge.Text="0"; TxtIOtros.Text="0";
        TxtEGasto.Text="0";   TxtECuota.Text="0";     TxtEAlquiler.Text="0"; TxtEOtros.Text="0";

        RefrescarCarrito();
        LimpiarLineaArticulo();
        BtnGuardar.IsEnabled = true;
        DtpSolicitud.SelectedDate = DateTime.Today;
        DtpFigurar.SelectedDate   = DateTime.Today;
        if (FindName("TxtEstado") is System.Windows.Controls.TextBlock tbEst) tbEst.Text = "NUEVO";
        await GenerarNumeroSolicitudAsync();
    }

    private void OnEliminar(object sender, RoutedEventArgs e)
    {
        if (GridDetalle.SelectedItem is LineaDetalle linea)
        {
            _carrito.Remove(linea);
            RefrescarCarrito();
        }
    }

    private void OnCancelar(object sender, RoutedEventArgs e)
    {
        if (_carrito.Count > 0 || _clienteActual != null)
        {
            var r = MessageBox.Show("¿Cerrar sin guardar?", "Cerrar",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
        }
        Close();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static decimal ParseMiles(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var digits = new string(s.Where(c => char.IsDigit(c)).ToArray());
        return decimal.TryParse(digits, out var v) ? v : 0;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            OnCancelar(sender, new RoutedEventArgs());
    }
}

// ── Ventana buscador de artículos para crédito ────────────────────────────────
internal class BuscadorArticuloWindow : Window
{
    private readonly IArticuloRepository _artRepo;
    public ArticuloConPrecio? ArticuloSeleccionado { get; private set; }

    private TextBox   _txtBuscar = null!;
    private DataGrid  _grid      = null!;

    public BuscadorArticuloWindow()
    {
        _artRepo = App.Services.GetRequiredService<IArticuloRepository>();
        Title  = "Buscar Artículo";
        Width  = 780; Height = 620;
        MinWidth = 600; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel { LastChildFill = true };

        // ── Cabecera naranja ──────────────────────────────────────────────
        var hdr = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 80, 0)),
            Padding = new Thickness(14, 12, 14, 12)
        };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "🔍  Buscar Artículo",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 15,
            Margin = new Thickness(0, 0, 0, 8)
        });
        var searchBox = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 120, 30)),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 160, 80)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 0, 6, 0)
        };
        var searchInner = new StackPanel { Orientation = Orientation.Horizontal };
        searchInner.Children.Add(new TextBlock {
            Text = "🔎", FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,220,180)),
            Margin = new Thickness(0, 0, 8, 0)
        });
        _txtBuscar = new TextBox {
            Height = 34, MinWidth = 380, FontSize = 13,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.White,
            CaretBrush  = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        _txtBuscar.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await BuscarAsync(); };
        var btnBuscar = new Button {
            Content = "Buscar", Height = 34, Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(8, 0, 0, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 160, 0)),
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0)
        };
        btnBuscar.Click += async (_, _) => await BuscarAsync();
        searchInner.Children.Add(_txtBuscar);
        searchInner.Children.Add(btnBuscar);
        searchBox.Child = searchInner;
        hdrSp.Children.Add(searchBox);
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top);
        root.Children.Add(hdr);

        // ── Botón inferior ────────────────────────────────────────────────
        var btnBar = new Border {
            Padding = new Thickness(10, 8, 10, 8),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220))
        };
        var btnOk = new Button {
            Content = "Seleccionar", Height = 34, Padding = new Thickness(20, 0, 20, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 0)),
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0)
        };
        btnOk.Click += (_, _) => Seleccionar();
        btnBar.Child = btnOk;
        DockPanel.SetDock(btnBar, Dock.Bottom);
        root.Children.Add(btnBar);

        // ── DataGrid ──────────────────────────────────────────────────────
        _grid = new DataGrid {
            Margin = new Thickness(0),
            IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
            AutoGenerateColumns = false,
            RowHeight = 40, ColumnHeaderHeight = 38,
            RowHeaderWidth = 0,
            BorderThickness = new Thickness(0),
            FontSize = 12,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            CanUserResizeColumns = true
        };
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Código", Binding = new System.Windows.Data.Binding("Ca"),
            MinWidth = 80, Width = 100 });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Descripción", Binding = new System.Windows.Data.Binding("D"),
            MinWidth = 200, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "P. Venta", Binding = new System.Windows.Data.Binding("PventaLocal") { StringFormat = "N0" },
            MinWidth = 90, Width = 110 });
        _grid.MouseDoubleClick += (_, _) => Seleccionar();
        root.Children.Add(_grid);

        Content = root;
        _txtBuscar.Focus();
    }

    private async Task BuscarAsync()
    {
        var term = _txtBuscar.Text.Trim();
        if (string.IsNullOrEmpty(term)) return;
        try
        {
            var session  = SessionService.Instance;
            int idLocal  = session.LocalActual?.IdLocal ?? 0;
            var resultados = await _artRepo.BuscarAsync(term);
            var lista = new List<ArticuloConPrecio>();
            foreach (var art in resultados)
            {
                Price? precio = idLocal > 0
                    ? await _artRepo.ObtenerPrecioLocalAsync(art.Id, idLocal)
                    : null;
                lista.Add(new ArticuloConPrecio {
                    Id          = art.Id,
                    Ca          = art.Ca,
                    D           = art.D,
                    PventaLocal = precio?.Pventa ?? 0,
                    Pc          = precio?.Pc     ?? 0,
                    Iva         = art.Iva
                });
            }
            _grid.ItemsSource = lista;
            if (lista.Count == 0)
                MessageBox.Show("No se encontraron artículos.", "Buscar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al buscar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Seleccionar()
    {
        if (_grid.SelectedItem is ArticuloConPrecio art)
        {
            ArticuloSeleccionado = art;
            DialogResult = true;
        }
    }
}
