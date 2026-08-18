using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Retiros;

// ── ViewModel de fila en la grilla de artículos ──────────────────────────────
public sealed class ArticuloRetiroVM
{
    public int     Id          { get; init; }
    public string  Ca          { get; init; } = "";
    public string  D           { get; init; } = "";
    public string  MarcaNombre { get; init; } = "";
    public decimal StockRaw    { get; init; }
    public string  StockLocal  => StockRaw > 0 ? StockRaw.ToString("0.##") : "—";
    public bool    SinStock    => StockRaw <= 0;
}

// ─────────────────────────────────────────────────────────────────────────────
public partial class RetiroLibreWindow : Window
{
    private readonly IArticuloRepository  _arts;
    private readonly ILocalRepository     _locales;
    private readonly IDbConnectionFactory _factory;

    private List<Local>  _todosLocales = new();
    private Local?       _localActual;
    private Articulo?    _articuloActual;
    private decimal      _stockActual;

    private ObservableCollection<ArticuloRetiroVM> _filas = new();

    public RetiroLibreWindow()
    {
        InitializeComponent();
        _arts    = App.Services.GetRequiredService<IArticuloRepository>();
        _locales = App.Services.GetRequiredService<ILocalRepository>();
        _factory = App.Services.GetRequiredService<IDbConnectionFactory>();

        GridArticulos.ItemsSource = _filas;
        Loaded  += OnLoaded;
        KeyDown += OnGlobalKeyDown;
    }

    // ── Inicialización ───────────────────────────────────────────────────────

    private async void OnLoaded(object s, RoutedEventArgs e)
    {
        var sesion = Core.Services.SessionService.Instance;
        TxtUsuarioSesion.Text = sesion.UsuarioActual?.NombreUsuario ?? "—";

        _todosLocales = (await _locales.ListarTodosAsync()).ToList();

        // Si el usuario tiene local asignado en sesión, pre-cargarlo
        if (sesion.LocalActual is Local loc)
        {
            _localActual = loc;
            TxtLocalId.Text     = loc.IdLocal.ToString();
            TxtLocalNombre.Text = loc.NombreLocal;
        }

        TxtBusqueda.Focus();
        await CargarArticulosAsync();
    }

    // ── Locales ──────────────────────────────────────────────────────────────

    private void OnLocalIdKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab)
        {
            _ = BuscarLocalPorIdAsync();
            e.Handled = (e.Key == Key.Enter);
        }
        if (e.Key == Key.F8) _ = AbrirSelectorLocalAsync();
    }

    private async Task BuscarLocalPorIdAsync()
    {
        if (!int.TryParse(TxtLocalId.Text.Trim(), out var id)) return;
        var local = _todosLocales.FirstOrDefault(l => l.IdLocal == id)
                    ?? await _locales.ObtenerPorIdAsync(id);
        if (local is null)
        {
            TxtLocalNombre.Text = "(no encontrado)";
            _localActual = null;
        }
        else
        {
            _localActual = local;
            TxtLocalNombre.Text = local.NombreLocal;
        }
        // Limpiar artículo seleccionado al cambiar de local
        _articuloActual = null;
        _stockActual    = 0;
        ActualizarDetalle();
        await CargarArticulosAsync();
    }

    private void OnBuscarLocal(object s, RoutedEventArgs e)
        => _ = AbrirSelectorLocalAsync();

    private async Task AbrirSelectorLocalAsync()
    {
        var dlg = new LocalSelectorDialog(_todosLocales) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedLocal is Local sel)
        {
            _localActual = sel;
            TxtLocalId.Text     = sel.IdLocal.ToString();
            TxtLocalNombre.Text = sel.NombreLocal;
            _articuloActual = null;
            _stockActual    = 0;
            ActualizarDetalle();
            await CargarArticulosAsync();
        }
    }

    // ── Búsqueda de artículos ────────────────────────────────────────────────

    private void OnBusquedaKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.F2)
        {
            _ = CargarArticulosAsync();
            e.Handled = true;
        }
    }

    private void OnBusquedaChanged(object s, System.Windows.Controls.TextChangedEventArgs e)
    {
        TxtPlaceholder.Visibility = string.IsNullOrEmpty(TxtBusqueda.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        if (TxtBusqueda.Text.Length >= 3 || TxtBusqueda.Text.Length == 0)
            _ = CargarArticulosAsync();
    }

    private void OnBuscarArticulo(object s, RoutedEventArgs e)
        => _ = CargarArticulosAsync();

    private async Task CargarArticulosAsync()
    {
        var termino = TxtBusqueda.Text.Trim();
        var like    = string.IsNullOrEmpty(termino) ? "%" : $"%{termino}%";

        using var conn = _factory.Create();

        IEnumerable<dynamic> rows;

        if (_localActual is not null)
        {
            // Solo artículos registrados en PRICES para el local seleccionado
            rows = await conn.QueryAsync<dynamic>(
                "SELECT TOP 500 a.ID, CAST(a.CA AS VARCHAR(50)) AS CA, a.D, " +
                "ISNULL(m.MARCA,'') AS MarcaNombre, " +
                "ISNULL(p.S, 0) AS StockRaw " +
                "FROM ARTICULOS a " +
                "INNER JOIN PRICES p ON p.IDART = a.ID AND p.IDLOCAL = @Local AND p.DELETADO = 0 " +
                "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
                "WHERE a.ES = 1 AND (a.CA LIKE @T OR a.D LIKE @T) " +
                "ORDER BY p.S DESC, a.D",
                new { Local = _localActual.IdLocal, T = like });
        }
        else
        {
            // Sin local: mostrar todos (el stock será "—")
            rows = await conn.QueryAsync<dynamic>(
                "SELECT TOP 500 a.ID, CAST(a.CA AS VARCHAR(50)) AS CA, a.D, " +
                "ISNULL(m.MARCA,'') AS MarcaNombre, " +
                "CAST(0 AS DECIMAL(18,2)) AS StockRaw " +
                "FROM ARTICULOS a " +
                "LEFT JOIN MARCAS m ON a.IDM = m.ID_MARCA " +
                "WHERE a.ES = 1 AND (a.CA LIKE @T OR a.D LIKE @T) ORDER BY a.D",
                new { T = like });
        }

        _filas.Clear();
        foreach (var r in rows)
        {
            _filas.Add(new ArticuloRetiroVM
            {
                Id          = Convert.ToInt32(r.ID),
                Ca          = (string)(r.CA  ?? ""),
                D           = (string)(r.D   ?? ""),
                MarcaNombre = (string)(r.MarcaNombre ?? ""),
                StockRaw    = Convert.ToDecimal(r.StockRaw)
            });
        }

        var localInfo = _localActual is not null ? $" en {_localActual.NombreLocal}" : "";
        TxtContadorArts.Text = $"{_filas.Count} artículo(s){localInfo}";
    }

    // ── Selección de artículo ────────────────────────────────────────────────

    private void OnArticuloSeleccionado(object s, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GridArticulos.SelectedItem is not ArticuloRetiroVM vm) return;
        _ = CargarDetalleAsync(vm.Id, vm.Ca, vm.D, vm.MarcaNombre);
    }

    private void OnArticuloDobleClick(object s, MouseButtonEventArgs e)
    {
        if (GridArticulos.SelectedItem is ArticuloRetiroVM)
        {
            TxtCantidad.Focus();
            TxtCantidad.SelectAll();
        }
    }

    private async Task CargarDetalleAsync(int id, string ca, string desc, string marca)
    {
        // Guardar artículo actual
        _articuloActual = new Articulo { Id = id, Ca = ca, D = desc, MarcaNombre = marca };

        // Campos de detalle
        TxtDetCodigo.Text      = ca;
        TxtDetDescripcion.Text = desc;
        TxtDetMarca.Text       = string.IsNullOrWhiteSpace(marca) ? "—" : marca;

        // Stock en local actual
        if (_localActual is not null)
        {
            var price = await _arts.ObtenerPrecioLocalAsync(id, _localActual.IdLocal);
            _stockActual = price?.S ?? 0;
        }
        else
        {
            _stockActual = 0;
        }

        // Stock en todos los locales (panel derecho inferior)
        var todos = await _arts.ObtenerStockTodosLocalesAsync(id);
        var listaLocales = todos.OrderByDescending(p => p.S).ToList();
        GridStockLocales.ItemsSource = listaLocales;
        TxtTotalLocales.Text = $"{listaLocales.Count} local(es)";

        ActualizarDetalle();
        ValidarCantidad();
    }

    private void ActualizarDetalle()
    {
        if (_articuloActual is null)
        {
            PanelSinSeleccion.Visibility = Visibility.Visible;
            PanelStockOK.Visibility      = Visibility.Collapsed;
            PanelStockCero.Visibility    = Visibility.Collapsed;
            PanelCantidad.IsEnabled      = false;
            BtnAceptar.IsEnabled         = false;
            TxtResumenOp.Text            = "Seleccioná un artículo para registrar el retiro";
            return;
        }

        PanelSinSeleccion.Visibility = Visibility.Collapsed;

        if (_localActual is null)
        {
            PanelStockOK.Visibility   = Visibility.Collapsed;
            PanelStockCero.Visibility = Visibility.Collapsed;
            PanelCantidad.IsEnabled   = false;
            BtnAceptar.IsEnabled      = false;
            TxtResumenOp.Text         = "Seleccioná un local para continuar";
            return;
        }

        if (_stockActual > 0)
        {
            TxtStockLocal.Text        = _stockActual.ToString("0.##");
            PanelStockOK.Visibility   = Visibility.Visible;
            PanelStockCero.Visibility = Visibility.Collapsed;
        }
        else
        {
            PanelStockOK.Visibility   = Visibility.Collapsed;
            PanelStockCero.Visibility = Visibility.Visible;
        }

        PanelCantidad.IsEnabled = true;
        BtnAceptar.IsEnabled    = true;

        var cant = ParseCantidad();
        TxtResumenOp.Text = cant > 0
            ? $"{_articuloActual.Ca}  ·  {cant:0.##} unid.  →  {_localActual.NombreLocal}"
            : "Ingresá la cantidad a retirar";
    }

    // ── Cantidad ─────────────────────────────────────────────────────────────

    private void OnCantFocus(object s, RoutedEventArgs e)
        => TxtCantidad.SelectAll();

    private void OnCantidadKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.F5) _ = GuardarAsync();
        ValidarCantidad();
    }

    private void OnMas(object s, RoutedEventArgs e)
    {
        var v = ParseCantidad() + 1;
        TxtCantidad.Text = v.ToString("0.##");
        ValidarCantidad();
    }

    private void OnMenos(object s, RoutedEventArgs e)
    {
        var v = Math.Max(1, ParseCantidad() - 1);
        TxtCantidad.Text = v.ToString("0.##");
        ValidarCantidad();
    }

    private decimal ParseCantidad()
    {
        decimal.TryParse(TxtCantidad.Text.Replace(",", "."),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var v);
        return v;
    }

    private void ValidarCantidad()
    {
        var cant = ParseCantidad();
        if (_articuloActual is not null && _localActual is not null && cant > _stockActual && _stockActual > 0)
        {
            PanelAdvertencia.Visibility = Visibility.Visible;
            TxtAdvertencia.Text = $"⚠  La cantidad ({cant:0.##}) supera el stock disponible ({_stockActual:0.##}).";
        }
        else
        {
            PanelAdvertencia.Visibility = Visibility.Collapsed;
        }

        // Actualizar resumen en pie
        if (_articuloActual is not null && _localActual is not null && cant > 0)
            TxtResumenOp.Text = $"{_articuloActual.Ca}  ·  {cant:0.##} unid.  →  {_localActual.NombreLocal}";
    }

    // ── Guardar ───────────────────────────────────────────────────────────────

    private void OnAceptar(object s, RoutedEventArgs e)
        => _ = GuardarAsync();

    private async Task GuardarAsync()
    {
        if (_articuloActual is null || _localActual is null) return;

        var cant = ParseCantidad();
        if (cant <= 0)
        {
            MessageBox.Show("Ingresá una cantidad válida mayor a cero.",
                "Cantidad inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCantidad.Focus(); TxtCantidad.SelectAll();
            return;
        }

        if (_stockActual > 0 && cant > _stockActual)
        {
            var resp = MessageBox.Show(
                $"La cantidad solicitada ({cant:0.##}) supera el stock disponible ({_stockActual:0.##}).\n¿Continuar de todas formas?",
                "Stock insuficiente", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (resp != MessageBoxResult.Yes) return;
        }

        // Autorización de administrador
        var auth = new Views.Shared.AutorizacionAdminModal(
            $"Retiro libre: [{_articuloActual.Ca}] {_articuloActual.D}  ×{cant:0.##}  —  {_localActual.NombreLocal}",
            "PERMISO_ELIMINAR_FACTURA") { Owner = this };
        if (auth.ShowDialog() != true) return;

        var confirm = MessageBox.Show(
            $"¿Confirmar el siguiente retiro libre?\n\n" +
            $"   Local:      {_localActual.IdLocal} – {_localActual.NombreLocal}\n" +
            $"   Artículo:  [{_articuloActual.Ca}]  {_articuloActual.D}\n" +
            $"   Cantidad:  {cant:0.##} unidades",
            "Confirmar retiro", MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var conn = _factory.Create();
            var p = new DynamicParameters();
            p.Add("@IDRET",    0);                          // SP lo sobreescribe con MAX+1
            p.Add("@IDART",    _articuloActual.Id);
            p.Add("@ID_LOCAL", (byte)_localActual.IdLocal);
            p.Add("@CANT",     cant);
            p.Add("@msg", dbType: System.Data.DbType.String,
                          direction: System.Data.ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("ACTUALIZAR_RETIRO_LIBRE_CS", p,
                commandType: CommandType.StoredProcedure);

            MessageBox.Show(
                $"Retiro registrado exitosamente.\n\n" +
                $"Artículo: {_articuloActual.D}\nCantidad: {cant:0.##}",
                "Retiro registrado", MessageBoxButton.OK, MessageBoxImage.Information);

            // Limpiar selección y recargar grilla (stock actualizado)
            _articuloActual = null;
            _stockActual    = 0;
            TxtCantidad.Text = "1";
            TxtDetCodigo.Text       = "";
            TxtDetDescripcion.Text  = "";
            TxtDetMarca.Text        = "";
            PanelStockOK.Visibility   = Visibility.Collapsed;
            PanelStockCero.Visibility = Visibility.Collapsed;
            PanelSinSeleccion.Visibility = Visibility.Visible;
            PanelCantidad.IsEnabled  = false;
            BtnAceptar.IsEnabled     = false;
            GridStockLocales.ItemsSource = null;
            TxtTotalLocales.Text = "";
            TxtResumenOp.Text = "Seleccioná un artículo para registrar el retiro";
            GridArticulos.SelectedItem = null;
            PanelAdvertencia.Visibility = Visibility.Collapsed;

            await CargarArticulosAsync();
            TxtBusqueda.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al registrar el retiro:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Cerrar + teclas globales ──────────────────────────────────────────────

    private void OnCerrar(object s, RoutedEventArgs e) => Close();

    private void OnGlobalKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)                    { Close(); e.Handled = true; }
        if (e.Key == Key.F5 && BtnAceptar.IsEnabled){ _ = GuardarAsync(); e.Handled = true; }
        if (e.Key == Key.F8)                        { _ = AbrirSelectorLocalAsync(); e.Handled = true; }
    }
}
