using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Transferencias;

// ══════════════════════════════════════════════════════════════════════════════
//  Modelos locales
// ══════════════════════════════════════════════════════════════════════════════

internal class LineaRemito
{
    public int     IdArt    { get; set; }
    public string  Codigo   { get; set; } = string.Empty;
    public string  Nombre   { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal Pcosto   { get; set; }
    public decimal Pcontado { get; set; }
    public decimal Subtotal => Cantidad * Pcontado;
}

internal class CabRemito
{
    public int      IdRemitoTmp   { get; set; }
    public string   NumeroRemTmp  { get; set; } = string.Empty;
    public byte     IdOrigenTmp   { get; set; }
    public byte     IdDestinoTmp  { get; set; }
    public byte     Estado        { get; set; }
    public DateTime Fecha         { get; set; }
    public decimal  TotalCosto    { get; set; }
    public decimal  TotalContado  { get; set; }
    public int      Idu           { get; set; }
    public string   LocalOrigen   { get; set; } = string.Empty;
    public string   LocalDestino  { get; set; } = string.Empty;
}

internal class ArticuloParaRemito
{
    public int    Id  { get; set; }
    public string Ca  { get; set; } = string.Empty;
    public string D   { get; set; } = string.Empty;
    public decimal S  { get; set; }
    public decimal Pc { get; set; }
    public decimal Pv { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════
//  NUEVA TRANSFERENCIA
// ══════════════════════════════════════════════════════════════════════════════

public class NuevaTransferenciaWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly IArticuloRepository  _artRepo;

    private ComboBox  _cboOrigen = null!, _cboDestino = null!;
    private TextBox   _txtBuscarArt = null!, _txtCantidad = null!, _txtNota = null!;
    private DataGrid  _gridArticulos = null!, _gridDetalle = null!;
    private TextBlock _lblTotal = null!;
    private Button    _btnConfirmar = null!;

    private readonly List<LineaRemito> _items = new();
    private List<Local> _locales = new();

    public NuevaTransferenciaWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _artRepo = App.Services.GetRequiredService<IArticuloRepository>();
        Title    = "Nueva Transferencia entre Locales";
        Width    = 1000; Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await CargarLocalesAsync();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        var hdr = Hdr("Nueva Transferencia entre Locales");
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        _btnConfirmar = Btn("✔ Confirmar", "#27AE60"); _btnConfirmar.Click += OnConfirmar; _btnConfirmar.IsEnabled = false;
        var btnCancelar = Btn("✖ Cancelar", "#E74C3C"); btnCancelar.Click += (_, _) => Close();
        bottom.Children.Add(_btnConfirmar); bottom.Children.Add(btnCancelar);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        var main = new Grid();
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // LEFT
        var left = new StackPanel { Margin = new Thickness(12) };

        left.Children.Add(Lbl("Local Origen"));
        _cboOrigen = new ComboBox { Margin = new Thickness(0, 0, 0, 6) };
        left.Children.Add(_cboOrigen);

        left.Children.Add(Lbl("Local Destino"));
        _cboDestino = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
        left.Children.Add(_cboDestino);

        left.Children.Add(Lbl("🔍 Buscar Artículo"));
        var artRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        _txtBuscarArt = new TextBox { Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 0, 4, 0) };
        _txtBuscarArt.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await BuscarArticuloAsync(); };
        var btnBuscar = Btn("Buscar", "#FF8C00", 60); btnBuscar.Click += async (_, _) => await BuscarArticuloAsync();
        DockPanel.SetDock(btnBuscar, Dock.Right); artRow.Children.Add(btnBuscar); artRow.Children.Add(_txtBuscarArt);
        left.Children.Add(artRow);

        _gridArticulos = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, Height = 160,
            SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.FloralWhite, Margin = new Thickness(0, 0, 0, 4)
        };
        _gridArticulos.Columns.Add(new DataGridTextColumn { Header = "Cód", Binding = new System.Windows.Data.Binding("Ca"), Width = 60 });
        _gridArticulos.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("D"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridArticulos.Columns.Add(new DataGridTextColumn { Header = "Stock", Binding = new System.Windows.Data.Binding("S"), Width = 50 });
        _gridArticulos.Columns.Add(new DataGridTextColumn { Header = "P.Costo", Binding = new System.Windows.Data.Binding("Pc"), Width = 70 });
        left.Children.Add(_gridArticulos);

        var cantRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        cantRow.Children.Add(Lbl("Cant:"));
        _txtCantidad = new TextBox { Width = 70, Padding = new Thickness(4, 3, 4, 3), Text = "1", Margin = new Thickness(4, 0, 8, 0) };
        var btnAdd = Btn("+ Agregar", "#2980B9"); btnAdd.Click += OnAgregar;
        cantRow.Children.Add(_txtCantidad); cantRow.Children.Add(btnAdd);
        left.Children.Add(cantRow);

        left.Children.Add(Lbl("Nota / Observación"));
        _txtNota = new TextBox { Padding = new Thickness(4, 3, 4, 3), Height = 50, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        left.Children.Add(_txtNota);

        var totalBorder = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FFF3E0")!,
            BorderBrush = System.Windows.Media.Brushes.Orange, BorderThickness = new Thickness(1),
            Padding = new Thickness(8), Margin = new Thickness(0, 8, 0, 0)
        };
        _lblTotal = new TextBlock { Text = "Total contado: 0", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.DarkOrange };
        totalBorder.Child = _lblTotal;
        left.Children.Add(totalBorder);

        var scrollLeft = new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(scrollLeft, 0); main.Children.Add(scrollLeft);

        // RIGHT — detalle
        var right = new DockPanel { Margin = new Thickness(8) };
        var rightHdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF8C00")!,
            Padding = new Thickness(8, 4, 8, 4)
        };
        rightHdr.Child = new TextBlock { Text = "Artículos a transferir", Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(rightHdr, Dock.Top); right.Children.Add(rightHdr);

        var quitarBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        var btnQuitar = Btn("Quitar seleccionado", "#E74C3C"); btnQuitar.Click += OnQuitar;
        quitarBar.Children.Add(btnQuitar);
        DockPanel.SetDock(quitarBar, Dock.Bottom); right.Children.Add(quitarBar);

        _gridDetalle = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.FloralWhite };
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Código",   Binding = new System.Windows.Data.Binding("Codigo"),   Width = 70 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Artículo", Binding = new System.Windows.Data.Binding("Nombre"),   Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Cant",     Binding = new System.Windows.Data.Binding("Cantidad"), Width = 55 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P.Costo",  Binding = new System.Windows.Data.Binding("Pcosto"),   Width = 70 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P.Contado",Binding = new System.Windows.Data.Binding("Pcontado"), Width = 80 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Subtotal", Binding = new System.Windows.Data.Binding("Subtotal"), Width = 80 });
        right.Children.Add(_gridDetalle);
        Grid.SetColumn(right, 1); main.Children.Add(right);

        root.Children.Add(main);
        Content = root;
    }

    private async Task CargarLocalesAsync()
    {
        using var conn = _db.Create();
        _locales = (await conn.QueryAsync<Local>(
            "SELECT ID_LOCAL as IdLocal, NOMBRE as NombreLocal FROM LOCALES ORDER BY NOMBRE")).ToList();

        var local = SessionService.Instance.LocalActual;
        foreach (var l in _locales)
        {
            _cboOrigen.Items.Add(new ComboBoxItem { Content = l.NombreLocal, Tag = l.IdLocal });
            _cboDestino.Items.Add(new ComboBoxItem { Content = l.NombreLocal, Tag = l.IdLocal });
        }
        // Preseleccionar local actual como origen
        for (int i = 0; i < _cboOrigen.Items.Count; i++)
        {
            if (((ComboBoxItem)_cboOrigen.Items[i]).Tag is int id && id == local?.IdLocal)
            { _cboOrigen.SelectedIndex = i; break; }
        }
        if (_cboDestino.Items.Count > 0) _cboDestino.SelectedIndex = 0;
    }

    private async Task BuscarArticuloAsync()
    {
        var term = _txtBuscarArt.Text.Trim();
        if (term.Length < 2) return;

        var origenLocal = GetLocalId(_cboOrigen);
        using var conn = _db.Create();

        // CARGAR_ARTICULOS_REMITO_CS(@Id, @msg) — devuelve artículos del local
        var p = new DynamicParameters();
        p.Add("@Id", (byte)origenLocal);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        var rows = await conn.QueryAsync<dynamic>("CARGAR_ARTICULOS_REMITO_CS", p, commandType: CommandType.StoredProcedure);

        var lista = rows
            .Where(r => ((string)r.D).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        ((string)r.CA).Contains(term, StringComparison.OrdinalIgnoreCase))
            .Select(r => new ArticuloParaRemito
            {
                Id = (int)r.ID,
                Ca = (string)r.CA,
                D  = (string)r.D,
                S  = (decimal)r.S,
                Pc = (decimal)r.PC,
                Pv = (decimal)r.PV
            }).ToList();

        _gridArticulos.ItemsSource = lista;
    }

    private void OnAgregar(object sender, RoutedEventArgs e)
    {
        if (_gridArticulos.SelectedItem is not ArticuloParaRemito art) { MessageBox.Show("Seleccione un artículo."); return; }
        if (!decimal.TryParse(_txtCantidad.Text, out var cant) || cant <= 0) { MessageBox.Show("Cantidad inválida."); return; }

        var existente = _items.FirstOrDefault(x => x.IdArt == art.Id);
        if (existente != null) existente.Cantidad += cant;
        else _items.Add(new LineaRemito { IdArt = art.Id, Codigo = art.Ca, Nombre = art.D, Cantidad = cant, Pcosto = art.Pc, Pcontado = art.Pv });

        RefrescarGrid();
    }

    private void OnQuitar(object sender, RoutedEventArgs e)
    {
        if (_gridDetalle.SelectedItem is LineaRemito l) { _items.Remove(l); RefrescarGrid(); }
    }

    private void RefrescarGrid()
    {
        _gridDetalle.ItemsSource = null;
        _gridDetalle.ItemsSource = _items.ToList();
        var total = _items.Sum(x => x.Subtotal);
        _lblTotal.Text = $"Total contado: {total:N0}";
        _btnConfirmar.IsEnabled = _items.Count > 0;
    }

    private async void OnConfirmar(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0) return;
        var origen  = GetLocalId(_cboOrigen);
        var destino = GetLocalId(_cboDestino);
        if (origen == destino) { MessageBox.Show("Origen y destino no pueden ser el mismo."); return; }

        var sesion = SessionService.Instance;
        if (sesion.UsuarioActual == null) return;

        var confirm = MessageBox.Show($"¿Confirmar transferencia de {_items.Count} artículo(s) de Local {origen} a Local {destino}?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        _btnConfirmar.IsEnabled = false;
        try
        {
            using var conn = _db.Create();
            var nroRem = $"REM-{DateTime.Now:yyyyMMddHHmmss}";
            var totalCosto   = _items.Sum(x => x.Cantidad * x.Pcosto);
            var totalContado = _items.Sum(x => x.Subtotal);

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                var p = new DynamicParameters();
                p.Add("@AGENTE",              "CS");
                p.Add("@id_remito_tmp",       0);
                p.Add("@numero_rem_tmp",      nroRem);
                p.Add("@idorigentmp",         (byte)origen);
                p.Add("@iddestinotmp",        (byte)destino);
                p.Add("@totalcosto",          totalCosto);
                p.Add("@totalcontado",        totalContado);
                p.Add("@idu",                 sesion.UsuarioActual.IdUsuario);
                p.Add("@estado",              (byte)0);
                p.Add("@nota",                _txtNota.Text);
                p.Add("@id_det_remito_tmp",   0);
                p.Add("@orden",               (byte)(i + 1));
                p.Add("@idart",               it.IdArt);
                p.Add("@cant",                it.Cantidad);
                p.Add("@pcosto",              it.Pcosto);
                p.Add("@pcontado",            it.Pcontado);
                p.Add("@IDMOVART",            0);
                p.Add("@MOV",                 (byte)1);
                p.Add("@MOD",                 (byte)3);
                p.Add("@STINI",               0m);
                p.Add("@PCANT",               0m);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
                await conn.ExecuteAsync("REMITO_TMP_CS", p, commandType: CommandType.StoredProcedure);
            }

            MessageBox.Show($"Transferencia '{nroRem}' creada.\nPendiente de aceptación por el local destino.",
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _btnConfirmar.IsEnabled = true;
        }
    }

    private static int GetLocalId(ComboBox cbo)
        => cbo.SelectedItem is ComboBoxItem item && item.Tag is int id ? id : 1;

    private static Border Hdr(string txt)
    {
        var b = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF8C00")!,
            Padding = new Thickness(12, 6, 12, 6)
        };
        b.Child = new TextBlock { Text = txt, Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold };
        return b;
    }
    private static Button Btn(string t, string hex, int w = 120) => new Button {
        Content = t, Height = 30, Width = w, Margin = new Thickness(0, 0, 6, 0),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand
    };
    private static TextBlock Lbl(string t) => new TextBlock
        { Text = t, FontSize = 11, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 5, 4, 1) };
}

// ══════════════════════════════════════════════════════════════════════════════
//  ACEPTAR TRANSFERENCIA
// ══════════════════════════════════════════════════════════════════════════════

public class AceptarTransferenciaWindow : Window
{
    private readonly IDbConnectionFactory _db;

    private DataGrid  _gridRemitos  = null!;
    private DataGrid  _gridDetalle  = null!;
    private TextBlock _lblInfo      = null!;
    private Button    _btnAceptar   = null!;

    private List<CabRemito> _remitos = new();
    private CabRemito?      _seleccionado;

    public AceptarTransferenciaWindow()
    {
        _db   = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Aceptar Transferencia";
        Width = 940; Height = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await CargarRemitosAsync();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2980B9")!,
            Padding = new Thickness(12, 6, 12, 6)
        };
        hdr.Child = new TextBlock { Text = "Aceptar Transferencia Pendiente", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 15, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        _btnAceptar = new Button { Content = "✔ Aceptar transferencia seleccionada", Height = 30, Padding = new Thickness(14, 0, 14, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#27AE60")!,
            Foreground = System.Windows.Media.Brushes.White, IsEnabled = false, Margin = new Thickness(0, 0, 8, 0),
            Cursor = System.Windows.Input.Cursors.Hand };
        _btnAceptar.Click += OnAceptar;
        var btnRefresh = new Button { Content = "↺ Refrescar", Height = 30, Padding = new Thickness(10, 0, 10, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2980B9")!,
            Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 8, 0),
            Cursor = System.Windows.Input.Cursors.Hand };
        btnRefresh.Click += async (_, _) => await CargarRemitosAsync();
        var btnCerrar = new Button { Content = "Cerrar", Height = 30, Padding = new Thickness(10, 0, 10, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        bottom.Children.Add(_btnAceptar); bottom.Children.Add(btnRefresh); bottom.Children.Add(btnCerrar);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        var main = new Grid();
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Top: transferencias pendientes
        var topPanel = new DockPanel { Margin = new Thickness(8, 8, 8, 0) };
        var topHdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2980B9")!,
            Padding = new Thickness(8, 4, 8, 4)
        };
        topHdr.Child = new TextBlock { Text = "Transferencias pendientes de aceptación", Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(topHdr, Dock.Top); topPanel.Children.Add(topHdr);

        _gridRemitos = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue };
        _gridRemitos.Columns.Add(new DataGridTextColumn { Header = "N° Remito",  Binding = new System.Windows.Data.Binding("NumeroRemTmp"),  Width = 160 });
        _gridRemitos.Columns.Add(new DataGridTextColumn { Header = "Origen",     Binding = new System.Windows.Data.Binding("LocalOrigen"),   Width = 130 });
        _gridRemitos.Columns.Add(new DataGridTextColumn { Header = "Destino",    Binding = new System.Windows.Data.Binding("LocalDestino"),  Width = 130 });
        _gridRemitos.Columns.Add(new DataGridTextColumn { Header = "Total",      Binding = new System.Windows.Data.Binding("TotalContado"),  Width = 90 });
        _gridRemitos.Columns.Add(new DataGridTextColumn { Header = "Fecha",
            Binding = new System.Windows.Data.Binding("Fecha") { StringFormat = "dd/MM/yyyy HH:mm" }, Width = 130 });
        _gridRemitos.SelectionChanged += OnRemitoSeleccionado;
        topPanel.Children.Add(_gridRemitos);
        Grid.SetRow(topPanel, 0); main.Children.Add(topPanel);

        // Info panel
        _lblInfo = new TextBlock { Margin = new Thickness(8, 2, 8, 2), Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 };
        Grid.SetRow(_lblInfo, 1); main.Children.Add(_lblInfo);

        // Bottom: detalle
        var botPanel = new DockPanel { Margin = new Thickness(8, 0, 8, 0) };
        var botHdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#607D8B")!,
            Padding = new Thickness(8, 4, 8, 4)
        };
        botHdr.Child = new TextBlock { Text = "Artículos de la transferencia seleccionada", Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(botHdr, Dock.Top); botPanel.Children.Add(botHdr);

        _gridDetalle = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue };
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Artículo",    Binding = new System.Windows.Data.Binding("Nombre"),     Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Cantidad",    Binding = new System.Windows.Data.Binding("Cantidad"),   Width = 80 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P.Costo",     Binding = new System.Windows.Data.Binding("Pcosto"),     Width = 80 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P.Contado",   Binding = new System.Windows.Data.Binding("Pcontado"),   Width = 90 });
        botPanel.Children.Add(_gridDetalle);
        Grid.SetRow(botPanel, 2); main.Children.Add(botPanel);

        root.Children.Add(main);
        Content = root;
    }

    private async Task CargarRemitosAsync()
    {
        _seleccionado = null;
        _btnAceptar.IsEnabled = false;
        _gridDetalle.ItemsSource = null;

        using var conn = _db.Create();
        var p = new DynamicParameters();
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        var rows = (await conn.QueryAsync<dynamic>("CARGAR_REV_REMITO_CS", p, commandType: CommandType.StoredProcedure)).ToList();

        // Cargar nombres de locales
        var locales = (await conn.QueryAsync<dynamic>("SELECT ID_LOCAL, NOMBRE FROM LOCALES")).ToDictionary(r => (int)r.ID_LOCAL, r => (string)r.NOMBRE);

        _remitos = rows.Select(r => new CabRemito
        {
            IdRemitoTmp  = (int)r.ID_REMITO_TMP,
            NumeroRemTmp = (string)r.NUMERO_REM_TMP,
            IdOrigenTmp  = (byte)r.IDORIGENTMP,
            IdDestinoTmp = (byte)r.IDDESTINOTMP,
            Estado       = (byte)r.ESTADO,
            Fecha        = (DateTime)r.FECHA,
            TotalCosto   = (decimal)r.TOTALCOSTO,
            TotalContado = (decimal)r.TOTALCONTADO,
            Idu          = (int)r.IDU,
            LocalOrigen  = locales.TryGetValue((int)r.IDORIGENTMP, out var o) ? o : r.IDORIGENTMP.ToString(),
            LocalDestino = locales.TryGetValue((int)r.IDDESTINOTMP, out var d) ? d : r.IDDESTINOTMP.ToString()
        }).ToList();

        _gridRemitos.ItemsSource = _remitos;
        _lblInfo.Text = $"{_remitos.Count} transferencias pendientes de aceptación.";
    }

    private async void OnRemitoSeleccionado(object sender, SelectionChangedEventArgs e)
    {
        if (_gridRemitos.SelectedItem is not CabRemito rem) return;
        _seleccionado = rem;
        _btnAceptar.IsEnabled = true;

        using var conn = _db.Create();
        var p = new DynamicParameters();
        p.Add("@elID", rem.IdRemitoTmp);
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        var rows = await conn.QueryAsync<dynamic>("BUSCAR_DETREMITO_TMP_CS", p, commandType: CommandType.StoredProcedure);

        var detalles = rows.Select(r => new LineaRemito
        {
            IdArt    = (int)r.IDART,
            Nombre   = (string)r.D,
            Cantidad = (decimal)r.CANT,
            Pcosto   = (decimal)r.PCOSTO,
            Pcontado = (decimal)r.PCONTADO
        }).ToList();
        _gridDetalle.ItemsSource = detalles;
    }

    private async void OnAceptar(object sender, RoutedEventArgs e)
    {
        if (_seleccionado == null) return;

        var sesion = SessionService.Instance;
        if (sesion.UsuarioActual == null) return;

        var rem = _seleccionado;
        var confirm = MessageBox.Show(
            $"¿Aceptar transferencia '{rem.NumeroRemTmp}'?\nOrigen: {rem.LocalOrigen} → Destino: {rem.LocalDestino}\nTotal: {rem.TotalContado:N0}",
            "Confirmar aceptación", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        _btnAceptar.IsEnabled = false;
        try
        {
            using var conn = _db.Create();

            // Cargar detalle del remito
            var pdet = new DynamicParameters();
            pdet.Add("@elID", rem.IdRemitoTmp);
            pdet.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var detalles = (await conn.QueryAsync<dynamic>("BUSCAR_DETREMITO_TMP_CS", pdet, commandType: CommandType.StoredProcedure)).ToList();

            for (int i = 0; i < detalles.Count; i++)
            {
                var det = detalles[i];
                var p = new DynamicParameters();
                p.Add("@AGENTE",          "CS");
                p.Add("@idremito",        rem.IdRemitoTmp);
                p.Add("@numeroremtmp",    rem.NumeroRemTmp);
                p.Add("@idorigen",        rem.IdOrigenTmp);
                p.Add("@iddestino",       rem.IdDestinoTmp);
                p.Add("@totalcosto",      rem.TotalCosto);
                p.Add("@totalcontado",    rem.TotalContado);
                p.Add("@iduenvio",        rem.Idu);
                p.Add("@idurecibo",       sesion.UsuarioActual.IdUsuario);
                p.Add("@iddetremito",     (int)det.IDDETREMITOTMP);
                p.Add("@orden",           (byte)(i + 1));
                p.Add("@idart",           (int)det.IDART);
                p.Add("@cant",            (decimal)det.CANT);
                p.Add("@pcosto",          (decimal)det.PCOSTO);
                p.Add("@pcontado",        (decimal)det.PCONTADO);
                p.Add("@IDMOVART",        0);
                p.Add("@MOV",             (byte)2);
                p.Add("@MOD",             (byte)3);
                p.Add("@STINI",           0m);
                p.Add("@PCANT",           0m);
                p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
                await conn.ExecuteAsync("REMITO_ACEPTADO_CS", p, commandType: CommandType.StoredProcedure);
            }

            MessageBox.Show("Transferencia aceptada correctamente. El stock fue actualizado.",
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            await CargarRemitosAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _btnAceptar.IsEnabled = true;
        }
    }
}
