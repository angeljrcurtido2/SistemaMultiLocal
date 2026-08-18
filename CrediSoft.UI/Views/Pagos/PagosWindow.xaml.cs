using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Shared;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Pagos;

public partial class PagosWindow : Window
{
    private readonly IPagoRepository   _pagos;
    private readonly ILocalRepository  _locales;
    private readonly ISessionService   _session;
    private readonly ICajaRepository   _cajas;

    private FuncionarioInfo? _funcionario;
    private Local?           _localSeleccionado;
    private int  _idGpsTemp  = 0;
    // Base real de venta/cobranza del período (lo que hay que multiplicar por el % de
    // comisión) — se guarda aparte de TxtTotalVenta/TxtTotalCobranza porque esos TextBox
    // ahora SÍ muestran el subtotal de comisión ya calculado (antes mostraban la base sin
    // multiplicar por el %, y esa base terminaba pasando por "Comisión venta" como si fuera
    // la comisión misma — confuso, un vendedor con 2% y 2.350.000 en ventas veía "2.350.000"
    // junto a "Comisión venta" en vez de los 47.000 reales).
    private decimal _baseVentaPeriodo    = 0;
    private decimal _baseCobranzaPeriodo = 0;
    private bool    _calculando    = false;
    private TextBox? _campoEnFoco  = null;

    public PagosWindow()
    {
        InitializeComponent();
        _pagos   = App.Services.GetRequiredService<IPagoRepository>();
        _locales = App.Services.GetRequiredService<ILocalRepository>();
        _session = SessionService.Instance;
        _cajas   = App.Services.GetRequiredService<ICajaRepository>();

        // Período por defecto: mes actual
        var hoy = DateTime.Today;
        DpDesde.SelectedDate = new DateTime(hoy.Year, hoy.Month, 1);
        DpHasta.SelectedDate = hoy;

        InicializarCampos();
        Loaded += async (_, _) =>
        {
            await CargarLocales();
            _todosFuncionarios = FiltrarFuncionariosPorPermiso((await _pagos.ListarFuncionariosAsync()).ToList());
            RegistrarFormatoMiles(
                TxtSalario, TxtPlus, TxtHorasExtras, TxtBonificacion, TxtOtrasComisiones,
                TxtIps, TxtCuotas, TxtMultas, TxtOtros, TxtEquis);
        };
    }

    private void RegistrarFormatoMiles(params TextBox[] campos)
    {
        foreach (var tb in campos)
        {
            tb.GotFocus  += OnMontoGotFocus;
            tb.LostFocus += OnMontoLostFocus;
        }
    }

    // ──────────────────────────────────────────────
    //  INICIALIZAR
    // ──────────────────────────────────────────────
    private void InicializarCampos()
    {
        _calculando = true;
        TxtSalario.Text          = "0";
        TxtPorcVenta.Text        = "0";
        TxtTotalVenta.Text       = "0";
        TxtPorcCobranza.Text     = "0";
        TxtTotalCobranza.Text    = "0";
        TxtPlus.Text             = "0";
        TxtHorasExtras.Text      = "0";
        TxtBonificacion.Text     = "0";
        TxtOtrasComisiones.Text  = "0";
        TxtTotalIngresos.Text    = "0";
        TxtAusencias.Text        = "0";
        TxtAdelantos.Text        = "0";
        TxtIps.Text              = "0";
        TxtCuotas.Text           = "0";
        TxtMultas.Text           = "0";
        TxtOtros.Text            = "0";
        TxtEquis.Text            = "0";
        TxtTotalEgresos.Text     = "0";
        TxtNeto.Text             = "0";
        TxtNotaAsignacion.Text   = "";
        TxtNotaEgreso.Text       = "";
        _calculando = false;
    }

    // ──────────────────────────────────────────────
    //  LOCALES
    // ──────────────────────────────────────────────
    private List<Local>           _todosLocales       = new();
    private List<FuncionarioInfo> _todosFuncionarios  = new();

    private async Task CargarLocales()
    {
        _todosLocales = (await _locales.ListarTodosAsync()).ToList();

        // Pre-seleccionar el local de sesión
        var localSesion = _session.LocalActual;
        if (localSesion != null)
            SetLocalSeleccionado(_todosLocales.FirstOrDefault(l => l.IdLocal == localSesion.IdLocal));
        else if (_todosLocales.Count > 0)
            SetLocalSeleccionado(_todosLocales[0]);

        // Solo un ADMINISTRADOR (o el usuario con excepción puntual, ver
        // Usuario.PuedeVerTodosLosLocales) puede pagar desde un local distinto al propio — un
        // usuario normal queda fijo en el local de SU sesión (de donde sale el efectivo), sin
        // poder elegir otro. El botón ni se renderiza (TxtLocalFijo lo reemplaza, mismo texto
        // pero sin apariencia de control clickeable) para no sugerir una opción que no tiene.
        var puedeVerTodos = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;
        BtnSeleccionarLocal.Visibility = puedeVerTodos ? Visibility.Visible   : Visibility.Collapsed;
        TxtLocalFijo.Visibility        = puedeVerTodos ? Visibility.Collapsed : Visibility.Visible;
        TxtAyudaLocal.Visibility       = puedeVerTodos ? Visibility.Visible   : Visibility.Collapsed;
    }

    private void OnSeleccionarLocal(object s, RoutedEventArgs e)
    {
        var explic = new ExplicarCambioLocalPagoDialog { Owner = this };
        explic.ShowDialog();
        if (!explic.QuiereContinuar) return;

        var modal = new SeleccionarLocalModal(_todosLocales, _localSeleccionado) { Owner = this };
        if (modal.ShowDialog() != true || modal.LocalSeleccionado == null) return;

        SetLocalSeleccionado(modal.LocalSeleccionado);

        if (_funcionario != null)
        {
            EvaluarAlertaLocal();
            _ = CalcularComisionesPeriodo();
        }
    }

    private void SetLocalSeleccionado(Local? local)
    {
        _localSeleccionado = local;
        var texto = local != null
            ? $"[{local.IdLocal}] {local.NombreLocal}"
            : "Seleccionar local...";
        TxtLocalSeleccionado.Text = texto;
        TxtLocalFijo.Text         = texto;
    }

    private void EvaluarAlertaLocal()
    {
        if (_funcionario == null || _localSeleccionado == null) return;

        bool diferente = _funcionario.IdLocal != _localSeleccionado.IdLocal;
        if (diferente)
        {
            PanelFuncionario.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xFF, 0xF3, 0xE0));
            AlertaLocal.Visibility = Visibility.Visible;
            TxtAlertaLocal.Text    = $"Func. pertenece al local {_funcionario.IdLocal} — seleccionado: {_localSeleccionado.IdLocal}";
        }
        else
        {
            PanelFuncionario.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE8, 0xF5, 0xE9));
            AlertaLocal.Visibility = Visibility.Collapsed;
        }
    }

    // ──────────────────────────────────────────────
    //  SELECCIONAR FUNCIONARIO
    // ──────────────────────────────────────────────
    private async void OnListarFuncionarios(object s, RoutedEventArgs e)
    {
        if (_todosFuncionarios.Count == 0)
            _todosFuncionarios = FiltrarFuncionariosPorPermiso((await _pagos.ListarFuncionariosAsync()).ToList());

        var modal = new SeleccionarFuncionarioModal(_todosFuncionarios, _funcionario) { Owner = this };
        if (modal.ShowDialog() != true || modal.FuncionarioSeleccionado == null) return;

        _funcionario = modal.FuncionarioSeleccionado;
        await CargarDatosFuncionario();
    }

    // Solo el administrador o el usuario con código "67" (mismo criterio ya usado en
    // Caja > Gastos/Historial, ver Usuario.PuedeVerTodosLosLocales) pueden ver funcionarios
    // de cualquier local — un usuario normal solo debe ver a los funcionarios del local con
    // el que inició sesión (LocalActual, igual que CajaGastosWindow), no de toda la empresa.
    private List<FuncionarioInfo> FiltrarFuncionariosPorPermiso(List<FuncionarioInfo> todos)
    {
        if (_session.UsuarioActual?.PuedeVerTodosLosLocales == true) return todos;

        var idLocalSesion = _session.LocalActual?.IdLocal;
        return todos.Where(f => f.IdLocal == idLocalSesion).ToList();
    }

    // _funcionario ya está seteado antes de llamar a este método
    private async Task CargarDatosFuncionario()
    {
        if (_funcionario == null) return;

        TxtNombre.Text      = _funcionario.Nombre;
        TxtLocal.Text       = $"[{_funcionario.IdLocal}] {_funcionario.NombreLocal}";
        TxtSalarioBase.Text = _funcionario.Salario.ToString("N0") + " Gs.";
        TxtPorcs.Text       = $"{_funcionario.PorcVenta:N1}% / {_funcionario.PorcCobranza:N1}%";

        PanelFuncionario.Visibility = Visibility.Visible;
        _idGpsTemp = 0;

        // Auto-seleccionar el local del funcionario — SOLO si el usuario logueado puede cambiar
        // de local (ver CargarLocales). Para un usuario normal, el local de pago debe quedar
        // siempre fijo en el de SU propia sesión (de ahí sale el efectivo real) sin importar en
        // qué local trabaje el funcionario — antes esta línea lo pisaba sin condición, dejando
        // seleccionado el local del funcionario aunque el usuario logueado estuviera en otro.
        if (_session.UsuarioActual?.PuedeVerTodosLosLocales == true)
        {
            var localFun = _todosLocales.FirstOrDefault(l => l.IdLocal == _funcionario.IdLocal);
            if (localFun != null) SetLocalSeleccionado(localFun);
        }

        EvaluarAlertaLocal();

        // Precargar salario y % comisiones del funcionario
        _calculando = true;
        TxtSalario.Text      = _funcionario.Salario.ToString("N", _fmtGs);
        TxtPorcVenta.Text    = _funcionario.PorcVenta.ToString("0.##");
        TxtPorcCobranza.Text = _funcionario.PorcCobranza.ToString("0.##");
        _calculando = false;

        // Calcular comisiones del período automáticamente
        await CalcularComisionesPeriodo();
        RecalcularTotales();
    }

    // ──────────────────────────────────────────────
    //  CALCULAR VENTAS / COBRANZAS DEL PERÍODO
    // ──────────────────────────────────────────────
    private void OnPeriodoChanged(object s, SelectionChangedEventArgs e)
    {
        if (_funcionario != null)
            _ = CalcularComisionesPeriodo();
    }

    // Abre "Multas a funcionarios" sin salir de Pago de Salarios (pedido explícito: antes
    // había que cerrar esta pantalla, cargar la multa en el otro módulo, y volver a abrir
    // Pago de Salarios seleccionando el funcionario de nuevo para que TxtMultas se
    // actualizara). Se usa ShowDialog (modal) para poder recalcular apenas se cierra, sin
    // depender de que el usuario haga algo más — mismo criterio de acceso restringido que
    // MultasWindow (administrador o código 67), MultasWindow ya valida esto por su cuenta.
    private async void OnAgregarMulta(object s, RoutedEventArgs e)
    {
        var win = new CrediSoft.UI.Views.Retiros.MultasWindow { Owner = this };
        win.ShowDialog();
        if (_funcionario != null)
            await CalcularComisionesPeriodo();
    }

    private async Task CalcularComisionesPeriodo()
    {
        if (_funcionario == null || DpDesde.SelectedDate == null || DpHasta.SelectedDate == null) return;

        var desde = DpDesde.SelectedDate.Value;
        var hasta = DpHasta.SelectedDate.Value.AddDays(1).AddSeconds(-1);

        using var conn = App.Services.GetRequiredService<CrediSoft.Data.IDbConnectionFactory>().Create();

        var p = new { IdU = _funcionario.IdUsuario, IdLocal = _funcionario.IdLocal, Desde = desde, Hasta = hasta };

        // Sin filtro de local en venta/cobranza: un vendedor puede vender o cobrar "como
        // externo" en un local distinto al suyo (CABECERA_SALES.ID_USUARIO / GENERADAS.IDU
        // quedan a su nombre igual, para que la comisión sea de quien realmente vendió/cobró),
        // así que la comisión debe sumar TODO lo suyo sin importar en qué local ocurrió —
        // antes, filtrar por ID_LOCAL=LOCAL_USUARIO (su local de base) dejaba afuera esas
        // operaciones hechas en otro local.
        // Pedido explícito: la comisión de venta a crédito se calcula sobre la ENTREGA
        // inicial cobrada al momento de la venta, no sobre el precio total del producto —
        // antes SUM(TOTAL) inflaba la base de comisión con todo el saldo a crédito, que el
        // vendedor todavía no cobró (lo cobra el cajero cuota a cuota, con su propia
        // comisión de cobranza aparte).
        var totalVenta = await conn.ExecuteScalarAsync<decimal?>(
            @"SELECT ISNULL(SUM(ENTREGANORMAL),0) FROM CABECERA_SALES
              WHERE ID_USUARIO=@IdU AND FECHA BETWEEN @Desde AND @Hasta", p) ?? 0;

        // Antes esto sumaba GENERADAS.TOTAL con ESTADO=1 (cuota COMPLETAMENTE pagada) — un
        // abono PARCIAL no cambia ESTADO hasta terminar de pagar la cuota, así que la
        // comisión de ese abono no se sumaba todavía, aunque el cobrador ya lo hubiera
        // cobrado. Ahora se suma directo de CAJA_DETALLE (mismo criterio que
        // GetDetalleCobranzasAsync): una fila por cada pago real, completo o parcial, con su
        // monto exacto. SUBTIPO='COBRO_SISTEMA' ya excluye la entrega de la venta (se graba
        // con SUBTIPO='VENTA' en VentasWindows), así que no hace falta el filtro NCUOTA<>1
        // que evitaba duplicarla con totalVenta (arriba, SUM(ENTREGANORMAL)).
        var totalCobranza = await conn.ExecuteScalarAsync<decimal?>(
            @"SELECT ISNULL(SUM(MONTO),0) FROM CAJA_DETALLE
              WHERE ID_VENDEDOR=@IdU AND SUBTIPO='COBRO_SISTEMA' AND ESTADO_REG='V'
                AND FECHA_HORA BETWEEN @Desde AND @Hasta", p) ?? 0;

        // Ausencias y adelantos SÍ quedan filtrados por local: son movimientos administrativos
        // del funcionario en SU local de base (MOV_FUNCIONARIOS.ID_LOCAL), no relacionados a en
        // qué local vendió/cobró como externo.
        var ausencias = await conn.ExecuteScalarAsync<decimal?>(
            @"SELECT ISNULL(SUM(MONTO),0) FROM MOV_FUNCIONARIOS
              WHERE IDU=@IdU AND ID_LOCAL=@IdLocal AND TIPO=0 AND FECHA BETWEEN @Desde AND @Hasta", p) ?? 0;

        var adelantosMovFunc = await conn.ExecuteScalarAsync<decimal?>(
            @"SELECT ISNULL(SUM(MONTO),0) FROM MOV_FUNCIONARIOS
              WHERE IDU=@IdU AND ID_LOCAL=@IdLocal AND TIPO=1 AND FECHA BETWEEN @Desde AND @Hasta", p) ?? 0;

        // En la práctica, los anticipos de sueldo casi nunca se cargan desde "Retiro Libre"
        // (la única pantalla que escribe en MOV_FUNCIONARIOS) — se cargan desde "Ingresar
        // movimiento" (Movimientos > Registro de caja) como Salida/SubTipo=ANTICIPO, que va
        // a CAJA_DETALLE, no a MOV_FUNCIONARIOS. Esa pantalla no vincula el anticipo al
        // funcionario por ID (no hay FK — ID_ENTIDAD ahí queda igual a ID_CAJERO, quien lo
        // cargó, no quien lo recibe), solo por el nombre libre en el concepto
        // ("Anticipo de haberes: [NOMBRE] fecha") — se suma también por ese texto para que
        // el cálculo automático de Pagos refleje los anticipos reales del período.
        // CHARINDEX en vez de LIKE: '[' y ']' son caracteres especiales de patrón en LIKE
        // (definen una clase de caracteres, no literales) — un LIKE '%[' + @Nombre + ']%'
        // no busca el texto "[Nombre]" tal cual, sino que matchea con cualquier concepto
        // que contenga alguno de esos caracteres sueltos, trayendo TODOS los anticipos del
        // período en vez de solo los del funcionario (confirmado con datos reales: sumaba
        // Gs. 4.959.750 de 8 funcionarios distintos en vez de los Gs. 1.000.000 reales de uno).
        var adelantosCaja = await conn.ExecuteScalarAsync<decimal?>(
            @"SELECT ISNULL(SUM(MONTO),0) FROM CAJA_DETALLE
              WHERE SUBTIPO='ANTICIPO' AND ESTADO_REG='V'
                AND CHARINDEX('[' + @NombreFuncionario + ']', CONCEPTO) > 0
                AND FECHA_HORA BETWEEN @Desde AND @Hasta",
            new { p.Desde, p.Hasta, NombreFuncionario = _funcionario.Nombre }) ?? 0;

        var adelantos = adelantosMovFunc + adelantosCaja;

        // Multas: se imputan al mes calendario de DpDesde (vigencia mensual, sin arrastre —
        // ver MultaRepository). No afectan CAJA_DETALLE, a diferencia de los adelantos de
        // arriba, así que no aparecen en ninguna de las dos queries anteriores. TxtMultas
        // pasa a ser de solo lectura acá abajo (ver InicializarCampos/BuildUI de esta misma
        // clase) — antes era un campo libre que cualquiera podía tipear sin quedar
        // registrado en ningún lado; ahora refleja lo cargado en el módulo "Multas a
        // funcionarios" (acceso restringido a administrador + código 67).
        var multasRepo = App.Services.GetRequiredService<CrediSoft.Data.Repositories.IMultaRepository>();
        var multas = await multasRepo.ObtenerTotalMesAsync(
            _funcionario.IdUsuario, (byte)desde.Month, (short)desde.Year);

        _baseVentaPeriodo    = totalVenta;
        _baseCobranzaPeriodo = totalCobranza;

        _calculando = true;
        TxtAusencias.Text = ausencias.ToString("N", _fmtGs);
        TxtAdelantos.Text = adelantos.ToString("N", _fmtGs);
        TxtMultas.Text    = multas.ToString("N", _fmtGs);
        _calculando = false;

        RecalcularTotales();
    }

    // ──────────────────────────────────────────────
    //  DETALLE VENTAS / COBRANZAS
    // ──────────────────────────────────────────────
    private async void OnVerDetalleVentas(object s, MouseButtonEventArgs e)
    {
        if (_funcionario == null || DpDesde.SelectedDate == null || DpHasta.SelectedDate == null) return;
        var desde = DpDesde.SelectedDate.Value;
        var hasta = DpHasta.SelectedDate.Value.AddDays(1).AddSeconds(-1);
        var idLocal = _localSeleccionado?.IdLocal ?? _funcionario.IdLocal;
        var items = await _pagos.GetDetalleVentasAsync(_funcionario.IdUsuario, idLocal, desde, hasta);
        decimal porc = ParseGs(TxtPorcVenta.Text);
        new DetalleVentasDialog(_funcionario.Nombre, items, porc) { Owner = this }.ShowDialog();
    }

    private async void OnVerDetalleCobranzas(object s, MouseButtonEventArgs e)
    {
        if (_funcionario == null || DpDesde.SelectedDate == null || DpHasta.SelectedDate == null) return;
        var desde = DpDesde.SelectedDate.Value;
        var hasta = DpHasta.SelectedDate.Value.AddDays(1).AddSeconds(-1);
        var idLocal = _localSeleccionado?.IdLocal ?? _funcionario.IdLocal;
        var items = await _pagos.GetDetalleCobranzasAsync(_funcionario.IdUsuario, idLocal, desde, hasta);
        decimal porc = ParseGs(TxtPorcCobranza.Text);
        new DetalleCobranzasDialog(_funcionario.Nombre, items, porc) { Owner = this }.ShowDialog();
    }

    // ──────────────────────────────────────────────
    //  RECÁLCULO EN TIEMPO REAL
    // ──────────────────────────────────────────────
    private void OnAsignacionChanged(object s, TextChangedEventArgs e) { if (!_calculando) { FormatearEnVivo(s); RecalcularTotales(); } }
    private void OnDescuentoChanged(object s,  TextChangedEventArgs e) { if (!_calculando) { FormatearEnVivo(s); RecalcularTotales(); } }

    private void FormatearEnVivo(object s)
    {
        if (s is not TextBox tb) return;
        var raw = tb.Text.Replace(".", "").Replace(",", "").Trim();
        if (!decimal.TryParse(raw, out var val)) return;
        var formatted = val.ToString("N", _fmtGs);          // ej: "1.500.000"
        if (tb.Text == formatted) return;
        _calculando = true;
        var caret = tb.CaretIndex;
        var dotsBefore = tb.Text[..Math.Min(caret, tb.Text.Length)].Count(c => c == '.');
        tb.Text = formatted;
        var dotsAfter  = formatted[..Math.Min(formatted.Length, Math.Max(0, caret + formatted.Length - raw.Length))].Count(c => c == '.');
        var newCaret   = Math.Clamp(caret + (dotsAfter - dotsBefore), 0, formatted.Length);
        tb.CaretIndex  = newCaret;
        _calculando = false;
    }

    private static readonly System.Globalization.NumberFormatInfo _fmtGs = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberDecimalDigits = 0
    };

    private void OnMontoGotFocus(object s, RoutedEventArgs e)
    {
        if (s is not TextBox tb) return;
        _campoEnFoco = tb;
        tb.SelectAll();
    }

    private void OnMontoLostFocus(object s, RoutedEventArgs e)
    {
        if (s is not TextBox tb) return;
        _campoEnFoco = null;
        var val = ParseGs(tb.Text);
        _calculando = true;
        tb.Text = val.ToString("N", _fmtGs);
        _calculando = false;
        RecalcularTotales();
    }
    private void OnNotaAsignacionChanged(object s, TextChangedEventArgs e) { }
    private void OnNotaEgresoChanged(object s, TextChangedEventArgs e) { }

    private void RecalcularTotales()
    {
        if (_calculando) return;
        _calculando = true;

        // Mientras el usuario edita un campo sin formato, ParseRaw lo lee directamente
        decimal salario         = ParseCampo(TxtSalario);
        decimal porcVenta       = ParseCampo(TxtPorcVenta);
        decimal porcCobranza    = ParseCampo(TxtPorcCobranza);
        decimal plus            = ParseCampo(TxtPlus);
        decimal horasExtras     = ParseCampo(TxtHorasExtras);
        decimal bonificacion    = ParseCampo(TxtBonificacion);
        decimal otras           = ParseCampo(TxtOtrasComisiones);

        // Comisión = % × base real del período (_baseVentaPeriodo/_baseCobranzaPeriodo, cargada
        // en CalcularComisionesPeriodo) — TxtTotalVenta/TxtTotalCobranza muestran el RESULTADO
        // de este cálculo, no la base, así que no se leen como entrada acá.
        var comVenta    = Math.Round(_baseVentaPeriodo    * porcVenta    / 100m, 0);
        var comCobranza = Math.Round(_baseCobranzaPeriodo * porcCobranza / 100m, 0);
        TxtTotalVenta.Text    = comVenta.ToString("N", _fmtGs);
        TxtTotalCobranza.Text = comCobranza.ToString("N", _fmtGs);

        var totalIngresos = salario + comVenta + comCobranza + plus + horasExtras + bonificacion + otras;

        decimal ausencias = ParseGs(TxtAusencias.Text);  // solo lectura
        decimal adelantos = ParseGs(TxtAdelantos.Text);  // solo lectura
        decimal ips       = ParseCampo(TxtIps);
        decimal cuotas    = ParseCampo(TxtCuotas);
        decimal multas    = ParseCampo(TxtMultas);
        decimal otros2    = ParseCampo(TxtOtros);
        decimal equis     = ParseCampo(TxtEquis);

        var totalEgresos = ausencias + adelantos + ips + cuotas + multas + otros2 + equis;
        var neto = totalIngresos - totalEgresos;

        TxtTotalIngresos.Text = totalIngresos.ToString("N", _fmtGs);
        TxtTotalEgresos.Text  = totalEgresos.ToString("N", _fmtGs);
        TxtNeto.Text          = neto.ToString("N", _fmtGs);
        TxtNeto.Foreground    = System.Windows.Media.Brushes.White;

        _calculando = false;
    }

    private decimal ParseCampo(TextBox tb) => ParseGs(tb.Text);

    private static decimal ParseGs(string? txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return 0;
        // Formato Gs.: punto como separador de miles (es-PY), sin decimales
        // Formato %: sin separador de miles, puede tener punto o coma decimal
        // Estrategia: si hay coma → es-PY (coma=decimal, punto=miles)
        //             si no hay coma y hay punto → InvariantCulture (punto=decimal)
        //             si no hay ninguno → número entero
        if (txt.Contains(','))
        {
            var s = txt.Replace(".", "").Replace(",", ".");
            return decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
        // Sin coma: eliminar puntos solo si son separadores de miles
        // (3 dígitos exactos después del punto → separador de miles)
        var limpio = System.Text.RegularExpressions.Regex.IsMatch(txt, @"\.\d{3}($|\s)")
            ? txt.Replace(".", "")
            : txt;
        return decimal.TryParse(limpio, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0;
    }

    // ──────────────────────────────────────────────
    //  MÉTODO DE PAGO
    // ──────────────────────────────────────────────
    private void OnMetodoChanged(object s, SelectionChangedEventArgs e)
    {
        if (TxtReferencia == null) return;
        var m = (CboMetodo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        TxtReferencia.IsEnabled = m != "EFECTIVO";
    }

    // ──────────────────────────────────────────────
    //  GENERAR (guardar borrador en GENERAR_PAGOSALARIO)
    // ──────────────────────────────────────────────
    private async void OnGenerar(object s, RoutedEventArgs e)
    {
        if (_funcionario == null) return;

        var prm = ArmarParams();

        if (_idGpsTemp > 0)
        {
            // Ya existe borrador — editarlo
            var ok = await _pagos.EditarPagoTempAsync(_idGpsTemp, prm);
            if (!ok) { MessageBox.Show("Error al actualizar el cálculo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        }
        else
        {
            _idGpsTemp = await _pagos.GenerarPagoTempAsync(prm);
            if (_idGpsTemp == 0) { MessageBox.Show("Error al generar el cálculo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        }

        new CalculoGeneradoDialog(_funcionario!.Nombre, ParseGs(TxtNeto.Text)) { Owner = this }.ShowDialog();
    }

    // ──────────────────────────────────────────────
    //  GUARDAR PAGO (definitivo → HISTORIALPAGOFUN + CAJA)
    // ──────────────────────────────────────────────
    private async void OnGuardar(object s, RoutedEventArgs e)
    {
        if (_funcionario == null) return;

        // Si todavía no se generó el borrador temporal, crearlo ahora
        if (_idGpsTemp == 0)
        {
            var prm0 = ArmarParams();
            _idGpsTemp = await _pagos.GenerarPagoTempAsync(prm0);
            if (_idGpsTemp == 0)
            {
                MessageBox.Show("Error al preparar el cálculo. Intente de nuevo.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        else
        {
            // Actualizar el borrador existente con los valores actuales
            await _pagos.EditarPagoTempAsync(_idGpsTemp, ArmarParams());
        }

        var neto = ParseGs(TxtNeto.Text);
        if (neto <= 0)
        {
            MessageBox.Show("El neto a pagar debe ser mayor a cero.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Requiere autorización de administrador
        var db   = App.Services.GetRequiredService<CrediSoft.Data.IDbConnectionFactory>();
        var auth = await CrediSoft.UI.Views.Shared.PermisoUsuariosModal.MostrarAsync(this, db);
        if (auth == null) return;

        // Seleccionar caja de egreso — usa el local de PAGO (_localSeleccionado, de donde sale
        // el efectivo real), no el local del funcionario, que puede ser otro. nombreLocalPago se
        // reutiliza más abajo tanto en el aviso de caja cerrada como en el resumen de
        // confirmación, para que ambos muestren siempre el mismo local.
        var idLocal = (byte)(_localSeleccionado?.IdLocal ?? _funcionario.IdLocal);
        var nombreLocalPago = _localSeleccionado?.NombreLocal ?? _funcionario.NombreLocal;
        var cajasAbiertas = (await _cajas.ListarCajasAbiertasAsync())
            .Where(c => c.IdLocal == idLocal)
            .ToList();

        CajaMaster? cajaElegida = null;
        if (cajasAbiertas.Count == 0)
        {
            // Un pago de sueldo/comisión SIEMPRE debe salir de una caja real — no se permite
            // continuar "sin afectar caja": eso dejaba el pago sin ningún egreso en CAJA_DETALLE,
            // invisible en cualquier arqueo o cierre. Bloquea igual que Venta Contado/Cobros
            // cuando no hay caja abierta en el local.
            var dlgCaja = new CrediSoft.UI.Views.Cobros.CajaCerradaDialog(
                nombreLocalPago, "registrar el pago") { Owner = this };
            dlgCaja.ShowDialog();
            if (dlgCaja.IrAAbrirCaja)
                new CrediSoft.UI.Views.Caja.CajaAperturaWindow().Show();
            return;
        }
        else if (cajasAbiertas.Count == 1)
        {
            cajaElegida = cajasAbiertas[0];
        }
        else
        {
            var modalCaja = new SeleccionarCajaModal(cajasAbiertas, neto) { Owner = this };
            if (modalCaja.ShowDialog() != true) return;
            cajaElegida = modalCaja.CajaSeleccionada;
        }

        var metodo = (CboMetodo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EFECTIVO";
        var dlgConfirm = new ConfirmarPagoDialog(
            _funcionario.Nombre,
            nombreLocalPago,
            ParseGs(TxtTotalIngresos.Text),
            ParseGs(TxtTotalEgresos.Text),
            neto,
            metodo,
            salario: ParseGs(TxtSalario.Text),
            comisionVenta: ParseGs(TxtTotalVenta.Text),
            comisionCobranza: ParseGs(TxtTotalCobranza.Text)) { Owner = this };
        dlgConfirm.ShowDialog();
        if (!dlgConfirm.Confirmado) return;

        try
        {
            BtnGuardar.IsEnabled = false;

            var prm = ArmarParams();
            var gp = new GuardarPagoParams(
                IdGps:          _idGpsTemp,
                IdUsuario:      _funcionario.IdUsuario,
                IdLocal:        idLocal,
                Salario:        prm.Salario,
                Venta:          prm.Venta,
                PorcVenta:      prm.PorcVenta,
                TotalVenta:     prm.TotalVenta,
                Cobranza:       prm.Cobranza,
                PorcCobranza:   prm.PorcCobranza,
                TotalCobranza:  prm.TotalCobranza,
                Plus:           prm.Plus,
                HorasExtras:    prm.HorasExtras,
                Bonificacion:   prm.Bonificacion,
                OtrasComisiones:prm.OtrasComisiones,
                TotalIngresos:  prm.TotalIngresos,
                Ausencias:      prm.Ausencias,
                Adelantos:      prm.Adelantos,
                Ips:            prm.Ips,
                Cuotas:         prm.Cuotas,
                Multas:         prm.Multas,
                Otros:          prm.Otros,
                Equis:          prm.Equis,
                TotalEgresos:   prm.TotalEgresos,
                Nombre:         _session.UsuarioActual!.NombreUsuario,
                Fecha:          DateTime.Now,
                NotaAsignacion: prm.NotaAsignacion,
                NotaEgreso:     prm.NotaEgreso,
                FormaPago:      metodo,
                MontoCaja:      neto,
                IdCajero:       _session.UsuarioActual.IdUsuario,
                IdCajaFisica:   cajaElegida?.IdCajaFisica ?? 0,
                IdMaster:       cajaElegida?.IdMaster ?? 0,
                Referencia:     TxtReferencia.Text.Trim());

            var ok = await _pagos.GuardarPagoAsync(gp);

            if (ok)
            {
                new PagoExitosoDialog(_funcionario.Nombre, neto) { Owner = this }.ShowDialog();
                _funcionario = null;
                _idGpsTemp   = 0;
                PanelFuncionario.Visibility = Visibility.Collapsed;
                InicializarCampos();
            }
            else
            {
                MessageBox.Show("Error al registrar el pago.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error inesperado:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ──────────────────────────────────────────────
    //  HELPERS
    // ──────────────────────────────────────────────
    private GenerarPagoParams ArmarParams()
    {
        // La base real de venta/cobranza vive en _baseVentaPeriodo/_baseCobranzaPeriodo —
        // TxtTotalVenta.Text/TxtTotalCobranza.Text ahora muestran el SUBTOTAL de comisión ya
        // calculado (% × base), no la base en sí (ver RecalcularTotales).
        var totalVentaBase = _baseVentaPeriodo;
        var totalCobBase   = _baseCobranzaPeriodo;
        var porcV = ParseGs(TxtPorcVenta.Text);
        var porcC = ParseGs(TxtPorcCobranza.Text);

        return new GenerarPagoParams(
            IdUsuario:       _funcionario!.IdUsuario,
            IdLocal:         (byte)(_localSeleccionado?.IdLocal ?? _funcionario.IdLocal),
            Salario:         ParseGs(TxtSalario.Text),
            Venta:           totalVentaBase,
            PorcVenta:       porcV,
            TotalVenta:      Math.Round(totalVentaBase * porcV / 100m, 0),
            Cobranza:        totalCobBase,
            PorcCobranza:    porcC,
            TotalCobranza:   Math.Round(totalCobBase * porcC / 100m, 0),
            Plus:            ParseGs(TxtPlus.Text),
            HorasExtras:     ParseGs(TxtHorasExtras.Text),
            Bonificacion:    ParseGs(TxtBonificacion.Text),
            OtrasComisiones: ParseGs(TxtOtrasComisiones.Text),
            TotalIngresos:   ParseGs(TxtTotalIngresos.Text),
            Ausencias:       ParseGs(TxtAusencias.Text),
            Adelantos:       ParseGs(TxtAdelantos.Text),
            Ips:             ParseGs(TxtIps.Text),
            Cuotas:          ParseGs(TxtCuotas.Text),
            Multas:          ParseGs(TxtMultas.Text),
            Otros:           ParseGs(TxtOtros.Text),
            Equis:           ParseGs(TxtEquis.Text),
            TotalEgresos:    ParseGs(TxtTotalEgresos.Text),
            Nombre:          _session.UsuarioActual!.NombreUsuario,
            NotaAsignacion:  TxtNotaAsignacion.Text.Trim(),
            NotaEgreso:      TxtNotaEgreso.Text.Trim());
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F5 && BtnGuardar.IsEnabled) OnGuardar(this, new RoutedEventArgs());
        if (e.Key == Key.Escape) Close();
    }

    private void OnCerrar(object s, RoutedEventArgs e)
    {
        // Si hay un borrador pendiente, limpiarlo
        if (_idGpsTemp > 0)
        {
            if (MessageBox.Show("Hay un cálculo pendiente sin guardar. ¿Desea descartar y cerrar?",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _ = _pagos.EliminarPagoTempAsync(_idGpsTemp);
        }
        Close();
    }
}
