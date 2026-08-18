using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Shared;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Pagos;

// Registro temporal pendiente de confirmación
internal sealed record PagoTempVM(
    int     IdGps,
    string  Nombre,
    DateTime Fecha,
    decimal Salario,
    decimal Venta,      decimal PorcVenta,    decimal TotalVenta,
    decimal Cobranza,   decimal PorcCobranza, decimal TotalCobranza,
    decimal Plus,       decimal HorasExtras,  decimal Bonificacion, decimal OtrasComisiones,
    decimal TotalIngresos,
    decimal Ausencias,  decimal Adelantos,    decimal Ips,
    decimal Cuotas,     decimal Multas,       decimal Otros,  decimal Equis,
    decimal TotalEgresos,
    string  NotaAsignacion, string NotaEgreso)
{
    public decimal Neto       => TotalIngresos - TotalEgresos;
    public string  FechaFmt   => Fecha.ToString("dd/MM/yyyy");
    public string  SalarioFmt => Salario.ToString("N0");
    public string  IngresosFmt=> TotalIngresos.ToString("N0");
    public string  EgresosFmt => TotalEgresos.ToString("N0");
    public string  NetoFmt    => Neto.ToString("N0");
}

public partial class PagoRemuneracionesWindow : Window
{
    private readonly IPagoRepository    _pagos;
    private readonly ICajaRepository    _cajas;
    private readonly ISessionService    _session;
    private FuncionarioInfo?            _funcionario;
    private PagoTempVM?                 _seleccionado;
    private List<FuncionarioInfo>       _todosFuncionarios = new();

    private static readonly NumberFormatInfo _fmtGs = new()
    {
        NumberGroupSeparator   = ".",
        NumberDecimalSeparator = ",",
        NumberDecimalDigits    = 0
    };

    public PagoRemuneracionesWindow()
    {
        InitializeComponent();
        _pagos   = App.Services.GetRequiredService<IPagoRepository>();
        _cajas   = App.Services.GetRequiredService<ICajaRepository>();
        _session = SessionService.Instance;

        var hoy = DateTime.Today;
        DpDesde.SelectedDate = new DateTime(hoy.Year - 1, hoy.Month, 1);
        DpHasta.SelectedDate = hoy;

        LimpiarDetalle();
        Loaded += async (_, _) =>
        {
            _todosFuncionarios = (await _pagos.ListarFuncionariosAsync()).ToList();
            await CargarTodos();
        };
    }

    // ── BÚSQUEDA ──────────────────────────────────
    private void OnCiKeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) OnBuscar(s, new RoutedEventArgs()); }

    private async void OnBuscar(object s, RoutedEventArgs e)
    {
        var ci = TxtCi.Text.Trim();
        if (string.IsNullOrEmpty(ci))
        {
            _funcionario = null;
            TxtNombreFuncionario.Text = "";
            await CargarTodos();
            return;
        }

        _funcionario = await _pagos.BuscarFuncionarioPorCiAsync(ci);
        if (_funcionario == null)
        {
            MessageBox.Show("Funcionario no encontrado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        TxtNombreFuncionario.Text = _funcionario.Nombre;
        await CargarPendientes(_funcionario.IdUsuario);
    }

    private async void OnListar(object s, RoutedEventArgs e)
    {
        if (_todosFuncionarios.Count == 0)
            _todosFuncionarios = (await _pagos.ListarFuncionariosAsync()).ToList();

        var modal = new SeleccionarFuncionarioModal(_todosFuncionarios, _funcionario) { Owner = this };
        if (modal.ShowDialog() != true || modal.FuncionarioSeleccionado == null) return;

        _funcionario = modal.FuncionarioSeleccionado;
        TxtCi.Text   = _funcionario.Ci;
        TxtNombreFuncionario.Text = _funcionario.Nombre;
        await CargarPendientes(_funcionario.IdUsuario);
    }

    private async void OnFiltroChanged(object s, SelectionChangedEventArgs e)
    {
        if (_funcionario != null)
            await CargarPendientes(_funcionario.IdUsuario);
        else
            await CargarTodos();
    }

    // ── CARGA DE DATOS ────────────────────────────
    private async Task CargarTodos()
    {
        var items = await ObtenerPendientesAsync(null);
        GridPendientes.ItemsSource = items;
        TxtContador.Text = $"{items.Count} pendiente(s)";
        LimpiarDetalle();
    }

    private async Task CargarPendientes(int idUsuario)
    {
        var items = await ObtenerPendientesAsync(idUsuario);
        GridPendientes.ItemsSource = items;
        TxtContador.Text = $"{items.Count} pendiente(s)";
        LimpiarDetalle();
    }

    private async Task<List<PagoTempVM>> ObtenerPendientesAsync(int? idUsuario)
    {
        if (DpDesde.SelectedDate == null || DpHasta.SelectedDate == null) return new();

        using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();

        var sql = @"SELECT G.IDGPS, G.IDU, G.SALARIO,
                           G.VENTA, G.PORCVENTA, G.TOTALVENTA,
                           G.COBRANZA, G.PORCCOBRANZA, G.TOTALCOBRANZA,
                           G.PLUS, G.HORASEXTRAS, G.BONIFICACION, G.OTRASCOMISIONES, G.TOTALINGRESOS,
                           G.AUSENCIAS, G.ADELANTOS, G.IPS, G.CUOTAS, G.MULTAS, G.OTROS, G.EQUIS,
                           G.TOTALEGRESOS, G.NOMBRE, G.FECHA,
                           ISNULL(G.NOTAASIGNACION,'') AS NOTAASIGNACION,
                           ISNULL(G.NOTAEGRESO,'') AS NOTAEGRESO
                    FROM GENERAR_PAGOSALARIO G
                    WHERE G.FECHA >= @Desde AND G.FECHA <= @Hasta";

        var p = new { Desde = DpDesde.SelectedDate.Value,
                      Hasta = DpHasta.SelectedDate.Value.AddDays(1).AddSeconds(-1) };

        if (idUsuario.HasValue)
        {
            sql += " AND G.IDU = @IdU";
            var rows = await conn.QueryAsync<dynamic>(sql + " ORDER BY G.FECHA DESC",
                new { p.Desde, p.Hasta, IdU = idUsuario.Value });
            return MapRows(rows);
        }
        else
        {
            var rows = await conn.QueryAsync<dynamic>(sql + " ORDER BY G.FECHA DESC", p);
            return MapRows(rows);
        }
    }

    private static List<PagoTempVM> MapRows(IEnumerable<dynamic> rows) =>
        rows.Select(r => new PagoTempVM(
            IdGps:          Convert.ToInt32(r.IDGPS),
            Nombre:         r.NOMBRE?.ToString() ?? "",
            Fecha:          Convert.ToDateTime(r.FECHA),
            Salario:        Convert.ToDecimal(r.SALARIO),
            Venta:          Convert.ToDecimal(r.VENTA),
            PorcVenta:      Convert.ToDecimal(r.PORCVENTA),
            TotalVenta:     Convert.ToDecimal(r.TOTALVENTA),
            Cobranza:       Convert.ToDecimal(r.COBRANZA),
            PorcCobranza:   Convert.ToDecimal(r.PORCCOBRANZA),
            TotalCobranza:  Convert.ToDecimal(r.TOTALCOBRANZA),
            Plus:           Convert.ToDecimal(r.PLUS),
            HorasExtras:    Convert.ToDecimal(r.HORASEXTRAS),
            Bonificacion:   Convert.ToDecimal(r.BONIFICACION),
            OtrasComisiones:Convert.ToDecimal(r.OTRASCOMISIONES),
            TotalIngresos:  Convert.ToDecimal(r.TOTALINGRESOS),
            Ausencias:      Convert.ToDecimal(r.AUSENCIAS),
            Adelantos:      Convert.ToDecimal(r.ADELANTOS),
            Ips:            Convert.ToDecimal(r.IPS),
            Cuotas:         Convert.ToDecimal(r.CUOTAS),
            Multas:         Convert.ToDecimal(r.MULTAS),
            Otros:          Convert.ToDecimal(r.OTROS),
            Equis:          Convert.ToDecimal(r.EQUIS),
            TotalEgresos:   Convert.ToDecimal(r.TOTALEGRESOS),
            NotaAsignacion: r.NOTAASIGNACION?.ToString() ?? "",
            NotaEgreso:     r.NOTAEGRESO?.ToString() ?? "")).ToList();

    // ── SELECCIÓN EN GRILLA ────────────────────────
    private void OnPendienteSeleccionado(object s, SelectionChangedEventArgs e)
    {
        if (GridPendientes.SelectedItem is not PagoTempVM item) return;
        MostrarDetalle(item);
    }

    private void MostrarDetalle(PagoTempVM item)
    {
        _seleccionado = item;

        TxtHeaderDetalle.Text   = $"DETALLE — {item.Nombre}  ·  {item.FechaFmt}  ·  ID #{item.IdGps}";

        TxtSalario.Text         = item.Salario.ToString("N", _fmtGs);
        TxtPorcVenta.Text       = item.PorcVenta.ToString("0.##");
        TxtTotalVenta.Text      = item.TotalVenta.ToString("N", _fmtGs);
        TxtPorcCobranza.Text    = item.PorcCobranza.ToString("0.##");
        TxtTotalCobranza.Text   = item.TotalCobranza.ToString("N", _fmtGs);
        TxtPlus.Text            = item.Plus.ToString("N", _fmtGs);
        TxtHorasExtras.Text     = item.HorasExtras.ToString("N", _fmtGs);
        TxtBonificacion.Text    = item.Bonificacion.ToString("N", _fmtGs);
        TxtOtrasComisiones.Text = item.OtrasComisiones.ToString("N", _fmtGs);
        TxtTotalIngresos.Text   = item.TotalIngresos.ToString("N", _fmtGs);

        TxtAusencias.Text       = item.Ausencias.ToString("N", _fmtGs);
        TxtAdelantos.Text       = item.Adelantos.ToString("N", _fmtGs);
        TxtIps.Text             = item.Ips.ToString("N", _fmtGs);
        TxtCuotas.Text          = item.Cuotas.ToString("N", _fmtGs);
        TxtMultas.Text          = item.Multas.ToString("N", _fmtGs);
        TxtOtros.Text           = item.Otros.ToString("N", _fmtGs);
        TxtEquis.Text           = item.Equis.ToString("N", _fmtGs);
        TxtTotalEgresos.Text    = item.TotalEgresos.ToString("N", _fmtGs);

        TxtNeto.Text            = item.Neto.ToString("N", _fmtGs);
        TxtNotaAsignacion.Text  = item.NotaAsignacion;
        TxtNotaEgreso.Text      = item.NotaEgreso;

        BtnGuardar.IsEnabled    = true;
    }

    // ── CONFIRMAR PAGO ────────────────────────────
    private async void OnGuardar(object s, RoutedEventArgs e)
    {
        if (_seleccionado == null || _session.UsuarioActual == null) return;

        // Obtener IDU e IDLocal directamente desde la BD para este GPS
        var (idU, idLocal) = await BuscarIduLocalPorGpsAsync(_seleccionado.IdGps);
        if (idU == 0)
        {
            MessageBox.Show("No se pudo determinar el funcionario del registro.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Verificar caja abierta en el local del funcionario
        var caja = await _cajas.ObtenerCajaAbiertaAsync(idLocal);
        if (caja == null)
        {
            MessageBox.Show(
                $"No hay caja abierta en el local del funcionario (Local ID {idLocal}).\nAbra la caja antes de confirmar el pago.",
                "Caja cerrada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var metodo = (CboMetodo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EFECTIVO";

        var auth = new AutorizacionAdminModal(
            $"Confirmar pago — {_seleccionado.Nombre}  ·  Neto Gs. {_seleccionado.Neto:N0}") { Owner = this };
        if (auth.ShowDialog() != true) return;

        var resumen =
            $"CONFIRMAR PAGO DE REMUNERACIÓN\n\n" +
            $"Funcionario:      {_seleccionado.Nombre}\n" +
            $"Total ingresos:   Gs. {_seleccionado.TotalIngresos:N0}\n" +
            $"Total descuentos: Gs. {_seleccionado.TotalEgresos:N0}\n" +
            $"────────────────────────────────\n" +
            $"NETO A PAGAR:     Gs. {_seleccionado.Neto:N0}\n\n" +
            $"Método: {metodo}\n\n" +
            $"¿Confirma el pago?";

        if (MessageBox.Show(resumen, "Confirmar pago", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        BtnGuardar.IsEnabled = false;
        try
        {
            var prm = new GuardarPagoParams(
                IdGps:           _seleccionado.IdGps,
                IdUsuario:       idU,
                IdLocal:         (byte)idLocal,
                Salario:         _seleccionado.Salario,
                Venta:           _seleccionado.Venta,
                PorcVenta:       _seleccionado.PorcVenta,
                TotalVenta:      _seleccionado.TotalVenta,
                Cobranza:        _seleccionado.Cobranza,
                PorcCobranza:    _seleccionado.PorcCobranza,
                TotalCobranza:   _seleccionado.TotalCobranza,
                Plus:            _seleccionado.Plus,
                HorasExtras:     _seleccionado.HorasExtras,
                Bonificacion:    _seleccionado.Bonificacion,
                OtrasComisiones: _seleccionado.OtrasComisiones,
                TotalIngresos:   _seleccionado.TotalIngresos,
                Ausencias:       _seleccionado.Ausencias,
                Adelantos:       _seleccionado.Adelantos,
                Ips:             _seleccionado.Ips,
                Cuotas:          _seleccionado.Cuotas,
                Multas:          _seleccionado.Multas,
                Otros:           _seleccionado.Otros,
                Equis:           _seleccionado.Equis,
                TotalEgresos:    _seleccionado.TotalEgresos,
                Nombre:          _session.UsuarioActual.NombreUsuario,
                Fecha:           _seleccionado.Fecha,
                NotaAsignacion:  _seleccionado.NotaAsignacion,
                NotaEgreso:      _seleccionado.NotaEgreso,
                FormaPago:       metodo,
                MontoCaja:       _seleccionado.Neto,
                IdCajero:        _session.UsuarioActual.IdUsuario,
                IdCajaFisica:    caja.IdCajaFisica,
                IdMaster:        caja.IdMaster,
                Referencia:      "");

            var ok = await _pagos.GuardarPagoAsync(prm);
            if (ok)
            {
                MessageBox.Show($"El pago de {_seleccionado.Nombre} se realizó correctamente.\nNeto: Gs. {_seleccionado.Neto:N0}",
                    "Pago confirmado", MessageBoxButton.OK, MessageBoxImage.Information);
                if (_funcionario != null)
                    await CargarPendientes(_funcionario.IdUsuario);
                else
                    await CargarTodos();
            }
            else
            {
                MessageBox.Show("Error al confirmar el pago. Verifique que la caja esté abierta.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnGuardar.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error inesperado:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            BtnGuardar.IsEnabled = true;
        }
    }

    private async Task<(int idU, int idLocal)> BuscarIduLocalPorGpsAsync(int idGps)
    {
        using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();
        var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT IDU, IDLOCAL FROM GENERAR_PAGOSALARIO WHERE IDGPS=@Id", new { Id = idGps });
        if (row == null) return (0, 0);
        return (Convert.ToInt32(row.IDU), Convert.ToInt32(row.IDLOCAL));
    }

    // ── HELPERS ───────────────────────────────────
    private void LimpiarDetalle()
    {
        _seleccionado = null;
        TxtHeaderDetalle.Text = "DETALLE — seleccioná un registro de la lista";
        foreach (var tb in new[] { TxtSalario, TxtPorcVenta, TxtTotalVenta, TxtPorcCobranza,
                                   TxtTotalCobranza, TxtPlus, TxtHorasExtras, TxtBonificacion,
                                   TxtOtrasComisiones, TxtTotalIngresos, TxtAusencias, TxtAdelantos,
                                   TxtIps, TxtCuotas, TxtMultas, TxtOtros, TxtEquis,
                                   TxtTotalEgresos, TxtNeto })
            tb.Text = "—";
        TxtNotaAsignacion.Text = "";
        TxtNotaEgreso.Text     = "";
        BtnGuardar.IsEnabled   = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F5 && BtnGuardar.IsEnabled) OnGuardar(this, new RoutedEventArgs());
        if (e.Key == Key.Escape) Close();
    }

    private void OnCerrar(object s, RoutedEventArgs e) => Close();
}
