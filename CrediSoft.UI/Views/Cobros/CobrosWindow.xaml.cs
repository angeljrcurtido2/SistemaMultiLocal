using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Caja;
using CrediSoft.UI.Views.Maestros;
using CrediSoft.UI.Views.Shared;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Cobros;

public partial class CobrosWindow : Window
{
    private readonly IClienteRepository  _clientes;
    private readonly ICuotaRepository    _cuotas;
    private readonly ISessionService     _session;

    private Cliente? _clienteActual;
    private Cuota?   _cuotaSeleccionada;
    private int?    _localFiltro   = null;
    private int?    _idCabActual   = null;
    // Todas las cuotas del cliente, de TODOS sus créditos, sin filtrar — el selector de
    // crédito (CboCredito) filtra sobre esta lista para armar GridCuotas. Sin este selector,
    // un cliente con más de un crédito mezclaba las cuotas de ambos en la misma grilla, y la
    // barra de info de arriba solo reflejaba uno de ellos — fácil de confundir al cobrar.
    private List<Cuota> _cuotasClienteActual = new();
    private bool _cargandoSelectorCredito = false;
    private decimal _tasaPunitorio = 0m;
    private decimal _valorInforconf = 0m;
    private List<Cuota> _todasCuotasPendientes = new();
    private bool _abrirModalAlCargar = false;
    private (int IdGeneradas, string ClienteCi)? _cuotaEspecificaAlCargar;
    private bool _formateandoMontoParcial = false;
    private bool _formateandoEfectivoRecibido = false;
    private bool _formateandoReajuste = false;
    // Descuento por Nota de Crédito pre-cargado (ver DescuentoCuotaWindow) para la cuota
    // actualmente en pantalla — null si no tiene ninguno pendiente. Se resta del total en
    // RecalcTotal como línea aparte (BannerDescuentoNc), nunca mezclado con Reajuste.
    private DescuentoCuotaRow? _descuentoNc = null;

    // Vendedor que realmente cobra esta cuota, si es distinto de _session.UsuarioActual —
    // no reemplaza la sesión (la caja/local siguen siendo los de quien está logueado), solo
    // cambia a nombre de quién queda registrado el cobro para que la comisión de fin de mes
    // se calcule correctamente. Null = cobra el usuario logueado (comportamiento normal).
    private (int Id, string Nombre)? _vendedorCobrador = null;

    public CobrosWindow()
    {
        InitializeComponent();
        _clientes = App.Services.GetRequiredService<IClienteRepository>();
        _cuotas   = App.Services.GetRequiredService<ICuotaRepository>();
        _session  = SessionService.Instance;

        // Mismo criterio que MainWindow.MnuAsignarCobrador (Usuario.PuedeAsignarCobradores) —
        // oculto por completo para quien no tiene permiso, no solo bloqueado al hacer clic.
        BtnAsignarCobrador.Visibility = _session.UsuarioActual?.PuedeAsignarCobradores == true
            ? Visibility.Visible : Visibility.Collapsed;

        // Tamaño proporcional al área de trabajo disponible (mismo criterio que el selector
        // de artículos, VerArticulosWindow) en vez de un ancho fijo — antes la ventana se veía
        // desproporcionada/vacía en el diseño de 2 columnas (grilla lateral + panel de cobro).
        var alto  = SystemParameters.WorkArea.Height - 20;
        var ancho = SystemParameters.WorkArea.Width - 40;
        Width  = Math.Min(1180, ancho); Height = Math.Min(700, alto);
        MinWidth = 980; MinHeight = Math.Min(600, alto);

        Loaded += async (_, _) =>
        {
            await CargarLocales();
            _tasaPunitorio  = await _cuotas.ObtenerTasaPunitiorioAsync();
            _valorInforconf = await _cuotas.ObtenerValorInforconfAsync();
            _todasCuotasPendientes = (await _cuotas.BuscarPendientesTodosAsync(_localFiltro)).ToList();
            if (_abrirModalAlCargar)
                await AbrirListadoCuotas();
            else if (_cuotaEspecificaAlCargar is { } destino)
                await AbrirCuotaEspecifica(destino.IdGeneradas, destino.ClienteCi);
        };
    }

    // Permite abrir la ventana ya con un cliente y su cuota autocompletados — usado desde
    // el panel "Cuotas próximas" del dashboard (doble clic) para ir directo al cobro sin
    // tener que volver a buscar al cliente por C.I./RUC.
    public void ConfigurarAbrirCuotaEspecifica(int idGeneradas, string clienteCi)
        => _cuotaEspecificaAlCargar = (idGeneradas, clienteCi);

    private async Task AbrirCuotaEspecifica(int idGeneradas, string clienteCi)
    {
        if (string.IsNullOrWhiteSpace(clienteCi)) return;

        var cuotasCliente = (await _cuotas.BuscarTodasPorCiAsync(clienteCi)).ToList();
        var cuota = cuotasCliente.FirstOrDefault(c => c.IdGeneradas == idGeneradas)
                    ?? cuotasCliente.FirstOrDefault();
        if (cuota == null) return;

        await CargarClienteYCuotas(cuota);
    }

    private async Task CargarLocales()
    {
        var locales = (await _clientes.ObtenerLocalesAsync()).ToList();
        CboLocal.ItemsSource = locales.Select(l => new LocalItem { Id = l.Id, Nombre = l.Nombre }).ToList();

        // Pedido explícito: un crédito puede haberse originado en un local distinto al que
        // el vendedor tiene asignado (cliente que se muda, gestión cruzada entre locales) —
        // antes solo un Admin podía ver "Todos" acá, dejando a un vendedor normal sin forma
        // de encontrar la cuota si no sabía en qué local buscarla. Ahora cualquier usuario
        // puede elegir "Todos" o filtrar por un local específico, igual que ya podía Admin.
    }

    // ──────────────────────────────────────────────
    //  FILTRO CLIENTES (Todos / Específico)
    // ──────────────────────────────────────────────
    private void OnFiltroClienteChanged(object s, RoutedEventArgs e)
    {
        if (PanelBusquedaCI == null || GridCuotas == null || PanelCliente == null) return;
        var esTodos = RbClienteTodos.IsChecked == true;
        PanelBusquedaCI.Visibility = esTodos ? Visibility.Collapsed : Visibility.Visible;
        PanelModoTodos.Visibility  = esTodos ? Visibility.Visible   : Visibility.Collapsed;
        if (esTodos) { _clienteActual = null; TxtCi.Text = ""; }
        PanelCliente.Visibility = Visibility.Collapsed;
        GridCuotas.ItemsSource  = null;
        LimpiarInfoCredito();
        OcultarPanelCobro();
    }

    // ──────────────────────────────────────────────
    //  FILTRO LOCAL
    // ──────────────────────────────────────────────
    private void OnFiltroLocalChanged(object s, RoutedEventArgs e)
    {
        if (CboLocal == null || BadgeLocal == null) return;
        var esTodos = RbTodos.IsChecked == true;
        CboLocal.IsEnabled = !esTodos;
        if (esTodos)
        {
            _localFiltro           = null;
            BadgeLocal.Visibility  = Visibility.Collapsed;
            CboLocal.SelectedIndex = -1;
        }
    }

    private void OnLocalComboChanged(object s, SelectionChangedEventArgs e)
    {
        if (CboLocal.SelectedItem is not LocalItem local) return;
        _localFiltro          = local.Id;
        TxtBadgeLocal.Text    = local.Nombre;
        BadgeLocal.Visibility = Visibility.Visible;
    }

    // ──────────────────────────────────────────────
    //  LISTAR CUOTAS (modal)
    // ──────────────────────────────────────────────
    private async void OnListarCuotas(object s, RoutedEventArgs e) => await AbrirListadoCuotas();

    public void ConfigurarAbrirModalAlCargar() => _abrirModalAlCargar = true;

    public async Task AbrirListadoCuotas()
    {
        _todasCuotasPendientes = (await _cuotas.BuscarPendientesTodosAsync(_localFiltro)).ToList();

        var modal = new ListaCuotasModal(_todasCuotasPendientes) { Owner = this };
        if (modal.ShowDialog() != true || modal.CuotaSeleccionada == null) return;

        await CargarClienteYCuotas(modal.CuotaSeleccionada);
    }

    private async Task CargarClienteYCuotas(Cuota cuotaOrigen)
    {
        _clienteActual = await _clientes.BuscarPorCiAsync(cuotaOrigen.ClienteCi);
        if (_clienteActual == null) return;

        TxtClienteNombre.Text   = _clienteActual.NombreCliente;
        TxtClienteCi.Text       = _clienteActual.CiCliente;
        TxtTelefono.Text        = _clienteActual.TelefonoCliente ?? "—";
        TxtCiudad.Text          = _clienteActual.CiudadCliente   ?? "—";
        TxtEstado.Text          = _clienteActual.EstadoTexto;
        TxtCredMax.Text         = _clienteActual.CredMax.ToString("N0") + " Gs.";
        PanelCliente.Visibility = Visibility.Visible;

        _cuotasClienteActual = (await _cuotas.BuscarTodasPorClienteAsync(_clienteActual.IdCliente)).ToList();

        PoblarSelectorCredito(preseleccionarIdCab: cuotaOrigen.IdCab);
        MostrarCuotasDelCredito(cuotaOrigen.IdCab);

        // Auto-abrir el panel si la cuota seleccionada es pendiente.
        // Asignar SelectedItem YA dispara SelectionChanged -> OnCuotaSeleccionada ->
        // CargarPanelCobro, asi que llamarlo de nuevo a mano duplicaba la carga del panel
        // completo (incluido el dialogo de Informconf, que aparecia dos veces seguidas).
        if (cuotaOrigen.Estado == 0)
        {
            var cuotasDelCredito = (GridCuotas.ItemsSource as IEnumerable<Cuota>)?.ToList() ?? new();
            var match = cuotasDelCredito.FirstOrDefault(c => c.IdGeneradas == cuotaOrigen.IdGeneradas);
            if (match != null)
                GridCuotas.SelectedItem = match;
        }
    }

    // Un mismo cliente puede tener varios créditos (ventas a crédito distintas) — este
    // selector lista uno por IdCab (más reciente primero) para que el cajero elija
    // explícitamente cuál está cobrando, en vez de ver todas las cuotas de todos sus créditos
    // mezcladas en una sola tabla sin distinción.
    private record CreditoItem(int IdCab, string Texto, decimal CabTotal, decimal CabHaber);

    private void PoblarSelectorCredito(int? preseleccionarIdCab = null)
    {
        _cargandoSelectorCredito = true;
        try
        {
            var creditos = _cuotasClienteActual
                .GroupBy(c => c.IdCab)
                .Select(g => new CreditoItem(
                    g.Key,
                    $"Crédito {g.Key}  —  {g.First().NSolicitud}  —  Debe Gs. {(g.First().CabTotal - g.First().CabHaber):N0}",
                    g.First().CabTotal, g.First().CabHaber))
                .OrderByDescending(c => c.IdCab)
                .ToList();

            CboCredito.ItemsSource = creditos;
            PanelSelectorCredito.Visibility = creditos.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

            if (creditos.Count == 0) return;
            var target = preseleccionarIdCab.HasValue
                ? creditos.FirstOrDefault(c => c.IdCab == preseleccionarIdCab.Value) ?? creditos[0]
                : creditos[0];
            CboCredito.SelectedItem = target;
        }
        finally { _cargandoSelectorCredito = false; }
    }

    private void OnCreditoSeleccionado(object s, SelectionChangedEventArgs e)
    {
        if (_cargandoSelectorCredito) return;
        if (CboCredito.SelectedItem is not CreditoItem sel) return;
        MostrarCuotasDelCredito(sel.IdCab);
        OcultarPanelCobro();
    }

    private void MostrarCuotasDelCredito(int idCab)
    {
        // Preservar qué cuota estaba seleccionada ANTES de reemplazar ItemsSource — sin esto,
        // tras un abono PARCIAL (que no completa la cuota y por lo tanto debe seguir
        // seleccionable) el refresco de MostrarClienteYCuotas() reemplazaba ItemsSource por
        // objetos Cuota nuevos, WPF deseleccionaba en silencio (el objeto viejo ya no está en
        // la lista) y SelectionChanged nunca volvía a dispararse — el panel de cobro (Saldo
        // restante, Total a Cobrar) se quedaba mostrando los valores del abono ANTERIOR en vez
        // de reflejar el ENTREGA/TOTAL recién actualizados en la base (bug real reportado:
        // tras dos abonos parciales sucesivos de Gs. 100.000, "Saldo restante" seguía
        // mostrando el monto de antes del último pago).
        var idGeneradasPrevio = (GridCuotas.SelectedItem as Cuota)?.IdGeneradas;

        var cuotasDelCredito = _cuotasClienteActual.Where(c => c.IdCab == idCab).ToList();
        GridCuotas.ItemsSource = cuotasDelCredito;

        if (idGeneradasPrevio.HasValue)
        {
            var match = cuotasDelCredito.FirstOrDefault(c => c.IdGeneradas == idGeneradasPrevio.Value);
            if (match != null) GridCuotas.SelectedItem = match;
        }

        _idCabActual = idCab;
        if (cuotasDelCredito.Count > 0)
        {
            var primera = cuotasDelCredito[0];
            TxtInfoId.Text         = idCab.ToString();
            TxtInfoDeudaTotal.Text = primera.CabTotal.ToString("N0") + " Gs.";
            TxtInfoHaber.Text      = primera.CabHaber.ToString("N0") + " Gs.";
            TxtInfoDebe.Text       = (primera.CabTotal - primera.CabHaber).ToString("N0") + " Gs.";
            var pendientes = cuotasDelCredito.Count(c => c.Estado == 0);
            TxtResumenDeuda.Text = $"Pendientes: {pendientes}  |  Deuda: {(primera.CabTotal - primera.CabHaber):N0} Gs.";
        }
        else
        {
            LimpiarInfoCredito();
        }
    }

    // ──────────────────────────────────────────────
    //  BÚSQUEDA DE CLIENTE ESPECÍFICO
    // ──────────────────────────────────────────────
    private async void OnBuscarCliente(object s, RoutedEventArgs e) => await CargarCliente();
    private async void OnCiKeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) await CargarCliente(); }

    private void OnAbrirBuscadorCliente(object s, RoutedEventArgs e)
    {
        var dlg = new BuscadorClienteModal(_clientes, soloConCuotas: true) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.ClienteSeleccionado != null)
        {
            // Usar el cliente directamente del modal (IdCliente correcto, sin re-buscar por CI)
            TxtCi.Text = dlg.ClienteSeleccionado.CiCliente;
            _ = CargarClienteDirecto(dlg.ClienteSeleccionado);
        }
    }

    private async Task CargarClienteDirecto(Cliente cliente)
    {
        _clienteActual = cliente;
        await MostrarClienteYCuotas();
    }

    private async Task CargarCliente()
    {
        var ci = TxtCi.Text.Trim();
        if (string.IsNullOrEmpty(ci)) return;

        _clienteActual = await _clientes.BuscarPorCiAsync(ci);
        if (_clienteActual == null)
        {
            MessageBox.Show("Cliente no encontrado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await MostrarClienteYCuotas();
    }

    private async Task MostrarClienteYCuotas()
    {
        if (_clienteActual == null) return;

        TxtClienteNombre.Text   = _clienteActual.NombreCliente;
        TxtClienteCi.Text       = _clienteActual.CiCliente;
        TxtTelefono.Text        = _clienteActual.TelefonoCliente ?? "—";
        TxtCiudad.Text          = _clienteActual.CiudadCliente   ?? "—";
        TxtEstado.Text          = _clienteActual.EstadoTexto;
        TxtCredMax.Text         = _clienteActual.CredMax.ToString("N0") + " Gs.";
        PanelCliente.Visibility = Visibility.Visible;

        // Buscar cuotas por IdCliente exacto (evita duplicados de CI en la BD)
        var cuotas = (await _cuotas.BuscarTodasPorClienteAsync(_clienteActual.IdCliente)).ToList();

        // Si no hay cuotas por IdCliente exacto, buscar por CI para cubrir duplicados
        if (cuotas.Count == 0 && !string.IsNullOrEmpty(_clienteActual.CiCliente))
            cuotas = (await _cuotas.BuscarTodasPorCiAsync(_clienteActual.CiCliente)).ToList();

        // Preservar el crédito que el cajero estaba viendo ANTES de este refresco — sin esto,
        // tras cobrar una cuota el combo saltaba silenciosamente al crédito más reciente del
        // cliente (PoblarSelectorCredito sin destino selecciona creditos[0]), y si el cajero
        // seguía escribiendo/clickeando en el panel de cobro sin fijarse en el combo, el
        // siguiente cobro terminaba acreditándose al crédito EQUIVOCADO (bug real detectado:
        // cobro de Gs. 729.085 pensado para el crédito 33486 quedó acreditado al 33484,
        // cliente Abelardo Espinola Cañete, 28/07/2026).
        var idCabPrevio = _idCabActual;

        _cuotasClienteActual = cuotas;
        OcultarPanelCobro();

        if (cuotas.Count > 0)
        {
            PoblarSelectorCredito(preseleccionarIdCab: idCabPrevio);
            if (CboCredito.SelectedItem is CreditoItem sel)
                MostrarCuotasDelCredito(sel.IdCab);
        }
        else
        {
            CboCredito.ItemsSource = null;
            PanelSelectorCredito.Visibility = Visibility.Collapsed;
            GridCuotas.ItemsSource = null;
            LimpiarInfoCredito();
            TxtResumenDeuda.Text = "Este cliente no tiene cuotas pendientes.";
        }
    }

    private void LimpiarInfoCredito()
    {
        TxtInfoId.Text         = "—";
        TxtInfoDeudaTotal.Text = "—";
        TxtInfoHaber.Text      = "—";
        TxtInfoDebe.Text       = "—";
        TxtResumenDeuda.Text   = "";
    }

    // ──────────────────────────────────────────────
    //  DOBLE CLICK EN CUOTA → panel cobro
    // ──────────────────────────────────────────────
    private async void OnCuotaSeleccionada(object s, SelectionChangedEventArgs e)
    {
        if (GridCuotas.SelectedItem is not Cuota c) return;
        if (c.Estado != 0)
        {
            await MostrarDetalleCobrada(c);
            return;
        }
        await CargarPanelCobro(c);
    }

    // Vista para ListDetPagos (ItemsControl) — formatea lo que ya trae PagoCuotaRow, sin
    // lógica propia, para no mezclar formato de UI dentro del repositorio.
    private record PagoCuotaVista(string FechaTexto, string MontoTexto, string FormaPago);

    // Cuota ya cobrada (Estado != 0): antes este caso solo ocultaba el panel de cobro sin
    // mostrar nada — el cajero no tenía forma de ver qué se cobró realmente, en particular
    // en cuotas con Reajuste (recargo, no necesariamente una exoneración — el mismo campo se
    // usa para ambos casos), donde ni la columna "Total Gs." de la grilla (Cuota.TotalCuota =
    // Monto+Punitorio, sin restar Reajuste) ni ningún otro lugar reflejaban el monto neto real
    // que entró a caja.
    private async Task MostrarDetalleCobrada(Cuota c)
    {
        _cuotaSeleccionada = null;
        PanelCobro.Visibility  = Visibility.Collapsed;
        PanelEmpty.Visibility  = Visibility.Collapsed;
        BtnCobrar.IsEnabled    = false;
        TxtHint.Visibility     = Visibility.Collapsed;

        TxtDetCobradaSub.Text = $"{_clienteActual?.NombreCliente} · Cuota {c.NCuotaTexto} · Comprobante {c.Comprobante}";

        // NCUOTA=1 es la ENTREGA inicial de la venta (ver Cuota.NCuotaTexto), no una cuota
        // real del plan de pagos — no acumula punitorio ni admite reajuste, y no pasa por
        // CobrarCuotaAsync (se registra al confirmar la venta, en otro flujo), así que ni el
        // desglose Capital/Punitorio/Reajuste ni la búsqueda de pagos en CAJA_DETALLE (que
        // busca el patrón "CUOTA N°:"/"ABONO PARCIAL CUOTA N°:" que ese flujo no genera)
        // tienen sentido acá. Layout reducido: solo título, fecha, monto y nota.
        if (c.NCuota == 1)
        {
            TxtDetTitulo.Text = "ENTREGA COBRADA";
            GridDetDesglose.Visibility     = Visibility.Collapsed;
            CardDetFechaEntrega.Visibility = Visibility.Visible;
            TxtDetFechaEntrega.Text = c.FechaCobrado?.ToString("dd/MM/yyyy HH:mm") ?? "—";
            TxtDetTotalLbl.Text = "MONTO DE ENTREGA";
            TxtDetTotal.Text = $"Gs. {c.Monto:N0}";
            TxtDetFormula.Visibility = Visibility.Collapsed;
            TxtDetObs.Visibility = string.IsNullOrWhiteSpace(c.Obs) ? Visibility.Collapsed : Visibility.Visible;
            TxtDetObs.Text = string.IsNullOrWhiteSpace(c.Obs) ? "" : $"Nota: {c.Obs}";
            PanelDetPagos.Visibility = Visibility.Collapsed;
            PanelDetalleCobrada.Visibility = Visibility.Visible;
            return;
        }

        TxtDetTitulo.Text = "CUOTA COBRADA";
        GridDetDesglose.Visibility     = Visibility.Visible;
        CardDetFechaEntrega.Visibility = Visibility.Collapsed;
        TxtDetTotalLbl.Text = "TOTAL COBRADO";

        // Tras cobrar, GENERADAS.ENTREGA queda pisado con el mismo valor que TOTAL (ver
        // CobrarCuotaAsync: "ENTREGA=@Total") — se pierde el dato de cuánto ya estaba
        // entregado ANTES de este cobro, así que el capital ya no se puede volver a separar
        // de forma confiable. Se muestra el Monto original de la cuota como "Capital" (lo
        // pactado, sin descontar entregas — ya no aplica una vez saldada) en vez de inventar
        // un cálculo que no se puede reconstruir con los datos que persiste el SP.
        TxtDetCapital.Text   = $"Gs. {c.Monto:N0}";
        TxtDetPunitorio.Text = $"Gs. {c.Punitorio:N0}";
        TxtDetFecha.Text     = c.FechaCobrado?.ToString("dd/MM/yyyy HH:mm") ?? "—";

        // GENERADAS.REAJUSTE mezcla en un solo campo el cargo automático de Inforconf y
        // cualquier ajuste manual del cajero al momento de cobrar (ver ObtenerReajusteTotal) —
        // pero GENERADAS.INFORCOM_APLICADO (Cuota.InforcomAplicado) SÍ queda marcado aparte
        // cuando ese cargo se aplicó, así que ahora se puede etiquetar sin adivinar. Bug real
        // reportado: la pantalla de detalle mostraba "+100.000 (reajuste aplicado)" sin
        // explicar el origen — quedaba como un cargo suelto sin justificación visible para
        // quien revisa el cobro después. Un reajuste NEGATIVO sigue sin poder atribuirse a
        // Inforconf (ese cargo siempre suma, nunca resta), así que ahí se mantiene el texto
        // genérico — no se asume el motivo de una exoneración manual.
        var reajusteNeto = c.Reajuste;
        TxtLblDetReajuste.Text = (reajusteNeto > 0 && c.InforcomAplicado) ? "CARGO INFORCONF" : "REAJUSTE / EXONERACIÓN";
        if (reajusteNeto == 0)
        {
            TxtDetReajuste.Text = "Gs. 0";
            TxtDetReajuste.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#263238"));
        }
        else if (reajusteNeto > 0)
        {
            TxtDetReajuste.Text = c.InforcomAplicado
                ? $"+ Gs. {reajusteNeto:N0} (cargo Inforconf)"
                : $"+ Gs. {reajusteNeto:N0} (reajuste aplicado)";
            TxtDetReajuste.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E65100"));
        }
        else
        {
            TxtDetReajuste.Text = $"− Gs. {Math.Abs(reajusteNeto):N0} (reajuste aplicado)";
            TxtDetReajuste.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2E7D32"));
        }

        // TOTAL COBRADO real = lo que quedó saldado en ENTREGA (== TOTAL tras el cobro,
        // ver arriba) — este es el monto neto que efectivamente cerró la cuota, ya con
        // Punitorio y Reajuste aplicados (coincide con "MONTO A COBRAR" que vio el cajero en
        // pantalla al confirmar).
        TxtDetTotal.Text = $"Gs. {c.Entrega:N0}";
        TxtDetFormula.Text = reajusteNeto != 0
            ? $"{c.Monto:N0} + {c.Punitorio:N0} {(reajusteNeto > 0 ? "+" : "−")} {Math.Abs(reajusteNeto):N0}"
            : "";
        TxtDetFormula.Visibility = reajusteNeto != 0 ? Visibility.Visible : Visibility.Collapsed;

        TxtDetObs.Visibility = string.IsNullOrWhiteSpace(c.Obs) ? Visibility.Collapsed : Visibility.Visible;
        TxtDetObs.Text = string.IsNullOrWhiteSpace(c.Obs) ? "" : $"Nota: {c.Obs}";

        // Pagos reales (CAJA_DETALLE) que componen esta cuota — puede haber más de uno si
        // hubo abonos parciales antes del pago que la terminó de saldar (ver
        // ObtenerPagosCuotaAsync). GENERADAS solo guarda el estado final, no este desglose.
        var pagos = (await _cuotas.ObtenerPagosCuotaAsync(c.Comprobante, c.NCuota)).ToList();
        if (pagos.Count > 0)
        {
            ListDetPagos.ItemsSource = pagos.Select(p => new PagoCuotaVista(
                p.Fecha.ToString("dd/MM/yyyy HH:mm"),
                $"Gs. {p.Monto:N0}",
                p.FormaPago)).ToList();
            PanelDetPagos.Visibility = Visibility.Visible;
        }
        else
        {
            PanelDetPagos.Visibility = Visibility.Collapsed;
        }

        PanelDetalleCobrada.Visibility = Visibility.Visible;
    }

    private void OnCuotaDobleClick(object s, MouseButtonEventArgs e) { }

    private async Task CargarPanelCobro(Cuota c)
    {
        _cuotaSeleccionada = c;
        _vendedorCobrador = null;
        ActualizarBadgeVendedorCobrador();

        // Descuento por Nota de Crédito pre-cargado por un administrador/código 67 para esta
        // cuota puntual — visible para CUALQUIER cajero de cualquier local que la cobre, no
        // solo quien lo creó (ver DescuentoCuotaWindow).
        _descuentoNc = await _cuotas.ObtenerDescuentoPendienteAsync(c.IdGeneradas);
        if (_descuentoNc != null)
        {
            TxtDescuentoNcMonto.Text = $"− Gs. {_descuentoNc.Monto:N0}";
            TxtDescuentoNcMotivo.Text = string.IsNullOrWhiteSpace(_descuentoNc.Motivo)
                ? (string.IsNullOrWhiteSpace(_descuentoNc.NroNotaCredito) ? "" : $"NC N° {_descuentoNc.NroNotaCredito}")
                : _descuentoNc.Motivo + (string.IsNullOrWhiteSpace(_descuentoNc.NroNotaCredito) ? "" : $" — NC N° {_descuentoNc.NroNotaCredito}");
            BannerDescuentoNc.Visibility = Visibility.Visible;
        }
        else
        {
            BannerDescuentoNc.Visibility = Visibility.Collapsed;
        }

        // Aviso temprano si la cuota (o su crédito) ya está asignada a otro cobrador — el
        // bloqueo real ocurre recién al tocar COBRAR (ver OnCobrar), esto solo evita que el
        // cajero llene todo el panel sin saber que no va a poder cobrar.
        var asignacion = await _cuotas.ObtenerAsignacionCuotaAsync(c.IdCab, c.IdGeneradas);
        if (asignacion != null && asignacion.IdCobrador != _session.UsuarioActual!.IdUsuario)
        {
            TxtAsignadaOtro.Text = asignacion.CobradorNombre;
            BadgeAsignadaOtro.Visibility = Visibility.Visible;
        }
        else
        {
            BadgeAsignadaOtro.Visibility = Visibility.Collapsed;
        }

        var diasMora  = c.DiasDeAtraso;
        var punitorio = diasMora > 0 ? await _cuotas.CalcularPunitoriocAsync(c.IdGeneradas) : 0m;
        c.Mora      = diasMora;
        c.Punitorio = punitorio;

        TxtCobroCliente.Text  = _clienteActual?.NombreCliente ?? "";
        TxtCobroId.Text       = c.IdCab.ToString();
        TxtDeudaTotal.Text    = $"Gs. {c.CabTotal:N0}";
        TxtDebe.Text          = $"Gs. {(c.CabTotal - c.CabHaber):N0}";

        // Usa el NCUOTA real (mismo criterio que GridCuotas, ver comentario en
        // Cuota.NCuotaTexto) para que el número mostrado en este panel coincida con el que
        // el cajero ve en la lista de "Cuotas del crédito" a la izquierda — antes usaba
        // NCuotaVisible (resta 1, pensado para Proforma/ticket) y mostraba un número de
        // cuota distinto al de la fila que el cajero seleccionó.
        TxtNCuota.Text        = c.NCuota.ToString();
        // CAPITAL debe mostrar el capital PENDIENTE (Monto - Entrega), no el monto original de
        // la cuota — mismo criterio que usa TOTAL DE LA CUOTA (RecalcTotal). El Punitorio es
        // aparte: se calcula siempre sobre el Monto original y no se ve afectado por Entrega
        // (ver CalcularPunitoriocAsync). Antes CAPITAL mostraba c.Monto sin descontar entregas
        // previas: tras un abono parcial de Gs. 10, CAPITAL seguía en 312.700 mientras el total
        // ya calculaba 312.690, una inconsistencia visual entre dos cards de la misma pantalla.
        var capitalPendiente = Math.Max(0, c.Monto - c.Entrega);
        TxtMontoCuota.Text    = $"Gs. {capitalPendiente:N0}";
        TxtEntregado.Text     = c.Entrega > 0 ? $"Gs. {c.Entrega:N0}" : "Gs. 0";
        TxtVencimiento.Text   = c.Vto.ToString("dd/MM/yyyy");
        TxtMora.Text          = diasMora.ToString();
        BadgeMora.Visibility  = diasMora > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtTasaPunitorio.Text = $"({_tasaPunitorio:N2}%)";
        TxtPunitorio.Text     = $"Gs. {punitorio:N0}";

        // GENERADAS.REAJUSTE ya viene de la base con lo que se aplicó en un abono anterior
        // (cargo de Inforconf y/o ajuste manual) — es PERMANENTE, no se re-evalúa en cada
        // apertura del panel. Si ya hay un reajuste guardado, se respeta tal cual (bug real
        // detectado: reseteaba a "0" e ignoraba el REAJUSTE ya persistido, haciendo que el
        // cargo de Inforconf "desapareciera" del total tras el primer abono parcial, aunque
        // seguía sumado en la base — el saldo pendiente quedaba $100.000 de menos en pantalla).
        // Solo si todavía no hay ningún reajuste (primera vez que se abre esta cuota en el
        // episodio) se evalúa si corresponde ofrecer el cargo automático de Inforconf.
        decimal inforconfAplicado;
        if (c.Reajuste > 0)
        {
            inforconfAplicado = c.Reajuste;
            TxtInforconf.Text = c.Reajuste.ToString("N0");
            BadgeInforconfCol.Visibility = Visibility.Visible;
        }
        else
        {
            // El cargo de Inforconf se cobra UNA vez por episodio de mora, automáticamente,
            // cuando la cuota atrasada más antigua del cliente supera los 90 días de mora —
            // concentrado en esa cuota, no en cada cuota atrasada, y sin depender del flag
            // CLIENTES.INFORCOM (ver CorrespondeCargoInforconfAsync). Va en un campo aparte
            // (TxtInforconf), separado del Reajuste manual, para no confundir un cargo del
            // sistema con un ajuste que decide el cajero.
            var corresponeCargo = diasMora > 0
                && await _cuotas.CorrespondeCargoInforconfAsync(_clienteActual!.IdCliente, c.IdGeneradas);

            inforconfAplicado = corresponeCargo ? _valorInforconf : 0m;
            TxtInforconf.Text = corresponeCargo ? _valorInforconf.ToString("N0") : "0";
            BadgeInforconfCol.Visibility = corresponeCargo ? Visibility.Visible : Visibility.Collapsed;
        }
        // Exoneración puntual autorizada por el negocio: cuota N°2 del crédito 25376 (Tito
        // Olmedo Arguello, IDGENERADAS=19718) se cobra solo el capital pendiente (300.000 Gs.),
        // sin punitorio ni inforconf. Se precarga automáticamente al abrir ESTA cuota exacta —
        // no depende de que el cajero tipee nada, y no afecta ninguna otra cuota del sistema.
        TxtReajuste.Text = c.IdGeneradas == IdGeneradasExoneracionTitoCuota2
            ? "-" + (punitorio + inforconfAplicado).ToString("N0").Replace(",", ".")
            : "0";
        ChkPagoParcial.IsChecked = false;
        OnPagoParcialToggled(ChkPagoParcial, new RoutedEventArgs()); // fuerza refresco visual de la card-toggle
        RecalcTotal();

        PanelCobro.Visibility = Visibility.Visible;

        PanelEmpty.Visibility = Visibility.Collapsed;
        PanelDetalleCobrada.Visibility = Visibility.Collapsed;
        BtnCobrar.IsEnabled   = true;
        TxtHint.Visibility    = Visibility.Collapsed;
    }

    private void OcultarPanelCobro()
    {
        PanelCobro.Visibility = Visibility.Collapsed;
        PanelEmpty.Visibility = Visibility.Visible;
        PanelDetalleCobrada.Visibility = Visibility.Collapsed;
        BtnCobrar.IsEnabled   = false;
        TxtHint.Visibility    = Visibility.Visible;
        _cuotaSeleccionada    = null;
    }

    private void ActualizarBadgeVendedorCobrador()
    {
        TxtVendedorCobrador.Text = _vendedorCobrador?.Nombre
            ?? _session.UsuarioActual?.NombreUsuario ?? "";
    }

    // Registra el cobro a nombre de OTRO vendedor (no reemplaza la sesión/caja, que siguen
    // siendo las de quien está logueado) — para que la comisión de cobranza de fin de mes se
    // calcule a nombre de quien realmente cobró la cuota. Ver CobrarCompletoAsync/
    // CobrarParcialAsync, donde _vendedorCobrador?.Id sustituye a _session.UsuarioActual.IdUsuario.
    private void OnCambiarVendedorCobrador(object s, MouseButtonEventArgs e)
    {
        var dlg = new SeleccionarVendedorCobradorDialog { Owner = this };
        if (dlg.ShowDialog() == true)
            _vendedorCobrador = (dlg.VendedorId, dlg.VendedorNombre);
        ActualizarBadgeVendedorCobrador();
    }

    private void OnAsignarCobranzas(object s, RoutedEventArgs e)
    {
        var w = new CobranzaAsignacionesWindow { Owner = this };
        w.ShowDialog();
    }

    // Se muestra CADA VEZ que el cajero entra al campo Reajuste — ver el bug real que motivó
    // esto: un cajero tipeó ahí el efectivo recibido por error, sumando ese monto entero al
    // total de la cuota (971.548 en vez de 490.000 reales). Si responde que no quería
    // reajustar, el foco se redirige al campo correcto.
    private void OnReajusteGotFocus(object s, RoutedEventArgs e)
    {
        var dlg = new ConfirmarReajusteDialog { Owner = this };
        dlg.ShowDialog();

        if (!dlg.QuiereReajustar)
        {
            if (PanelEfectivoRecibido.Visibility == Visibility.Visible)
                TxtEfectivoRecibido.Focus();
            else if (PanelMontoParcial.Visibility == Visibility.Visible)
                TxtMontoParcial.Focus();
            else
                BtnCobrar.Focus();
        }
    }

    // ──────────────────────────────────────────────
    //  RECÁLCULO TOTAL
    // ──────────────────────────────────────────────
    // CI habilitados para tipear un Reajuste NEGATIVO (exonerar punitorio/inforconf de una
    // cuota puntual, con autorización expresa del negocio caso por caso) — el campo sigue
    // siendo solo-suma para el resto de los clientes, para no reabrir el riesgo que motivó
    // ConfirmarReajusteDialog (cajero tipeando ahí el efectivo recibido por error).
    private static readonly HashSet<string> CisConReajusteNegativoHabilitado = new() { "7016503" };

    private bool PermiteReajusteNegativo =>
        _clienteActual != null && CisConReajusteNegativoHabilitado.Contains(_clienteActual.CiCliente?.Trim() ?? "");

    // GENERADAS.IDGENERADAS de la cuota N°2 del crédito 25376 (Tito Olmedo Arguello) — la
    // única cuota con exoneración automática de punitorio+inforconf autorizada por el negocio
    // (ver CargarPanelCobro). Puntual y verificado a mano; no es un mecanismo general.
    private const int IdGeneradasExoneracionTitoCuota2 = 19718;

    private void OnReajusteChanged(object s, TextChangedEventArgs e)
    {
        if (_formateandoReajuste) return;

        // Reformatea con separador de miles en cada tecleo, igual que TxtMontoParcial/
        // TxtEfectivoRecibido. Para los CI en CisConReajusteNegativoHabilitado se preserva un
        // signo "-" inicial (exoneración puntual autorizada por el negocio); para el resto el
        // campo sigue siendo solo-dígitos como siempre.
        _formateandoReajuste = true;
        var esNegativo = PermiteReajusteNegativo && TxtReajuste.Text.TrimStart().StartsWith("-");
        var digitos = new string(TxtReajuste.Text.Where(char.IsDigit).ToArray());
        decimal.TryParse(digitos, out var reajuste);
        if (esNegativo) reajuste = -reajuste;
        // Si el cajero recién tipeó "-" y todavía no hay dígitos, se preserva el signo solo
        // en pantalla (sin recortarlo a "0") para que pueda seguir escribiendo el monto —
        // antes esto se perdía porque reajuste==0 forzaba el texto a "0" en cada tecleo.
        TxtReajuste.Text = reajuste == 0
            ? (esNegativo && digitos.Length == 0 ? "-" : "0")
            : (esNegativo ? "-" : "") + Math.Abs(reajuste).ToString("N0").Replace(",", ".");
        TxtReajuste.CaretIndex = TxtReajuste.Text.Length;
        _formateandoReajuste = false;

        // Resalta la tarjeta en ámbar mientras el reajuste manual sea distinto de cero — un
        // cajero llegó a tipear acá el efectivo recibido por error, sumando ese monto entero
        // al total real de la cuota. El resaltado busca que note el extra antes de confirmar.
        if (reajuste != 0)
        {
            CardReajuste.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF8E1"));
            CardReajuste.BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFB300"));
        }
        else
        {
            CardReajuste.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FAFBFC"));
            CardReajuste.BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EEF1F3"));
        }

        RecalcTotal();
    }

    // Suma el reajuste manual (TxtReajuste, decisión del cajero) más el cargo automático
    // de Inforconf (TxtInforconf, ver CargarPanelCobro) — ambos son campos separados en
    // pantalla, pero conceptualmente ambos son "reajuste" a los fines del total a cobrar.
    private decimal ObtenerReajusteTotal()
    {
        // Se extraen solo los dígitos (igual que TxtMontoParcial/TxtEfectivoRecibido) en vez
        // de parsear el texto formateado con puntos directamente, para no depender de que
        // la cultura del hilo siga siendo es-PY en cada punto de lectura. El signo "-" se
        // preserva aparte (ver PermiteReajusteNegativo/OnReajusteChanged) porque char.IsDigit
        // lo descarta.
        var esNegativo = PermiteReajusteNegativo && TxtReajuste.Text.TrimStart().StartsWith("-");
        var digitosManual = new string(TxtReajuste.Text.Where(char.IsDigit).ToArray());
        var digitosInforconf = new string(TxtInforconf.Text.Where(char.IsDigit).ToArray());
        decimal.TryParse(digitosManual, out var manual);
        decimal.TryParse(digitosInforconf, out var inforconf);
        if (esNegativo) manual = -manual;
        return manual + inforconf;
    }

    private void RecalcTotal()
    {
        if (_cuotaSeleccionada == null) return;
        var reaj = ObtenerReajusteTotal();
        // El capital pendiente descuenta la entrega/abonos previos — a diferencia del
        // Punitorio (GENERADAS.PUNITORIO, ya persistido tal cual quedó calculado en el último
        // abono), que SIEMPRE se calcula sobre el capital ORIGINAL de la cuota y nunca se
        // recalcula restando entregas (ver CalcularPunitoriocAsync).
        var capitalPendiente = Math.Max(0, _cuotaSeleccionada.Monto - _cuotaSeleccionada.Entrega);
        // Bug real detectado: cuando varios abonos parciales suman más que el Monto (el
        // capital ya quedó cubierto y el resto se aplicó a Punitorio/Reajuste — mismo criterio
        // que usa el SP de cobro en la base), Math.Max(0, Monto-Entrega) arriba trunca el
        // excedente a 0 en vez de aplicarlo al resto de la deuda: el "TOTAL DE LA CUOTA" en
        // pantalla seguía mostrando Punitorio+Reajuste completos sin descontar ese excedente
        // (confirmado: crédito 98 cuota 3, tras entregar 600.000 sobre un capital de 300.000,
        // la pantalla seguía pidiendo 386.800 en vez de los 86.800 reales que ya reflejaba
        // GENERADAS.TOTAL). Se resta el excedente (Entrega - Monto, si es positivo) del resto
        // antes de sumarlo, para que coincida con lo que el SP ya calculó y persistió.
        var excedenteEntrega = Math.Max(0, _cuotaSeleccionada.Entrega - _cuotaSeleccionada.Monto);
        var restoDeuda = Math.Max(0, _cuotaSeleccionada.Punitorio + reaj - excedenteEntrega);
        // Descuento por Nota de Crédito (si esta cuota tiene uno pendiente) se resta al final,
        // sobre el total ya armado — línea aparte y visible (BannerDescuentoNc), no se mezcla
        // con Reajuste ni con ningún otro campo que el cajero pueda editar a mano.
        var totalCuota = Math.Max(0, capitalPendiente + restoDeuda - (_descuentoNc?.Monto ?? 0m));
        // "TOTAL DE LA CUOTA" (TxtSubTotal) debe reflejar TODO lo que hay que pagar por esta
        // cuota — Capital + Punitorio + Reajuste + Inforconf — no solo Capital+Punitorio.
        // Bug real detectado: mostraba 585.090 mientras "MONTO A COBRAR" (que sí sumaba el
        // reajuste) mostraba 685.090, una diferencia exacta de $100.000 (el cargo Inforconf)
        // que confundía al cajero sobre cuál era el total real de la cuota.
        TxtSubTotal.Text = $"Gs. {totalCuota:N0}";

        // Etiqueta y fórmula dinámicas: solo cuando hay recargo o exoneración (reaj != 0), para
        // dejar claro que el total mostrado ya incluye Inforconf y/o un reajuste manual, no solo
        // capital+punitorio — se actualiza en vivo con cada tecleo en Reajuste (OnReajusteChanged
        // llama a RecalcTotal en cada cambio).
        // La fórmula chica debajo del total no incluía el descuento por NC — el número grande
        // (TxtSubTotal) sí lo restaba correctamente, pero la fórmula visible seguía sumando
        // solo Capital+Punitorio+Reajuste, sin llegar al mismo total mostrado arriba. Eso hacía
        // parecer que el descuento NO se había aplicado (bug real reportado, verificado que en
        // realidad el número sí estaba descontado — solo la fórmula quedaba inconsistente).
        var hayDescuento = _descuentoNc != null && _descuentoNc.Monto > 0;
        if (reaj != 0 || hayDescuento)
        {
            TxtLblTotalCuota.Text = hayDescuento
                ? "TOTAL DE LA CUOTA (con descuento)"
                : reaj > 0 ? "TOTAL DE LA CUOTA + RECARGO" : "TOTAL DE LA CUOTA (con exoneración)";
            var signo = reaj > 0 ? "+" : "−";
            var formula = excedenteEntrega > 0
                ? $"{capitalPendiente:N0} + {_cuotaSeleccionada.Punitorio:N0} {signo} {Math.Abs(reaj):N0} − {excedenteEntrega:N0} (ya cubierto)"
                : $"{capitalPendiente:N0} + {_cuotaSeleccionada.Punitorio:N0} {signo} {Math.Abs(reaj):N0}";
            if (hayDescuento) formula += $" − {_descuentoNc!.Monto:N0} (desc. NC)";
            TxtFormulaTotalCuota.Text = formula;
            TxtFormulaTotalCuota.Visibility = Visibility.Visible;
        }
        else
        {
            TxtLblTotalCuota.Text = "TOTAL DE LA CUOTA";
            TxtFormulaTotalCuota.Visibility = Visibility.Collapsed;
        }

        if (ChkPagoParcial.IsChecked == true)
        {
            var digitos = new string(TxtMontoParcial.Text.Where(char.IsDigit).ToArray());
            decimal.TryParse(digitos, out var montoAbonar);
            RecalcSaldoParcial(totalCuota, montoAbonar);
        }
        else
        {
            // La entrega ya está descontada dentro de totalCuota (vía capitalPendiente),
            // por lo que acá NO se vuelve a restar — a diferencia del pago parcial, que sí
            // resta lo abonado HOY (montoAbonar) sobre este mismo totalCuota ya neto.
            TxtTotalCobrar.Text = totalCuota.ToString("N0");
        }
    }

    // ──────────────────────────────────────────────
    //  PAGO PARCIAL
    // ──────────────────────────────────────────────
    // ChkPagoParcial ya no tiene un control visual propio (el toggle "Abono parcial" se
    // quitó — ver comentario en el XAML) — el código lo sigue usando como estado interno
    // para reutilizar CobrarParcialAsync/PanelMontoParcial sin duplicar lógica, activándolo
    // programáticamente solo desde CobrarCompletoAsync cuando el efectivo no alcanza.
    private void OnPagoParcialToggled(object s, RoutedEventArgs e)
    {
        var activo = ChkPagoParcial.IsChecked == true;

        PanelMontoParcial.Visibility = activo ? Visibility.Visible : Visibility.Collapsed;
        if (!activo)
            CardSaldoRestante.Visibility = Visibility.Collapsed;

        TxtBtnCobrarLabel.Text = activo ? "ABONAR  (F5)" : "COBRAR  (F5)";
        if (activo)
            TxtMontoParcial.Text = "";

        ActualizarVisibilidadEfectivoRecibido();
        RecalcTotal();
    }

    private void OnMontoParcialChanged(object s, TextChangedEventArgs e)
    {
        if (_formateandoMontoParcial) return;

        // Reformatea con separador de miles en cada tecleo, igual que TxtEfectivo en VentasWindows.
        _formateandoMontoParcial = true;
        var digitos = new string(TxtMontoParcial.Text.Where(char.IsDigit).ToArray());
        decimal.TryParse(digitos, out var monto);
        TxtMontoParcial.Text = monto == 0 ? "" : monto.ToString("N0").Replace(",", ".");
        TxtMontoParcial.CaretIndex = TxtMontoParcial.Text.Length;
        _formateandoMontoParcial = false;

        if (_cuotaSeleccionada == null) return;
        var reaj = ObtenerReajusteTotal();
        // Mismo cálculo que RecalcTotal (ver ahí el motivo del excedenteEntrega): el capital
        // pendiente descuenta la entrega, y si la entrega ya superó el capital original, ese
        // excedente se descuenta del resto (Punitorio+Reajuste) en vez de perderse.
        var capitalPendiente = Math.Max(0, _cuotaSeleccionada.Monto - _cuotaSeleccionada.Entrega);
        var excedenteEntrega = Math.Max(0, _cuotaSeleccionada.Entrega - _cuotaSeleccionada.Monto);
        var restoDeuda = Math.Max(0, _cuotaSeleccionada.Punitorio + reaj - excedenteEntrega);
        // Descuento por Nota de Crédito (si esta cuota tiene uno pendiente) se resta al final,
        // sobre el total ya armado — línea aparte y visible (BannerDescuentoNc), no se mezcla
        // con Reajuste ni con ningún otro campo que el cajero pueda editar a mano.
        var totalCuota = Math.Max(0, capitalPendiente + restoDeuda - (_descuentoNc?.Monto ?? 0m));
        RecalcSaldoParcial(totalCuota, monto);
    }

    private void RecalcSaldoParcial(decimal totalCuota, decimal montoAbonar)
    {
        TxtTotalCobrar.Text = montoAbonar.ToString("N0");

        if (montoAbonar <= 0)
        {
            CardSaldoRestante.Visibility  = Visibility.Collapsed;
            CardVueltoParcial.Visibility  = Visibility.Collapsed;
            return;
        }

        // totalCuota ya llega neto de la entrega/abonos previos (ver los dos callers de
        // este método) — acá solo se resta lo que se abona HOY.
        var saldoRestante = totalCuota - montoAbonar;

        if (saldoRestante < 0)
        {
            // El "abono parcial" en realidad excede lo que se debe: por debajo se trata
            // igual que un pago completo (nunca se acredita mas que el total de la cuota
            // a la deuda del cliente) y se muestra el vuelto.
            CardSaldoRestante.Visibility = Visibility.Collapsed;
            CardVueltoParcial.Visibility = Visibility.Visible;
            TxtVueltoParcial.Text = $"Gs. {Math.Abs(saldoRestante):N0}";
            return;
        }

        CardVueltoParcial.Visibility = Visibility.Collapsed;
        CardSaldoRestante.Visibility = Visibility.Visible;
        TxtSaldoRestante.Text = saldoRestante == 0
            ? "✓ Este abono completa la cuota"
            : $"Gs. {saldoRestante:N0}";
    }

    // ──────────────────────────────────────────────
    //  MÉTODO DE PAGO
    // ──────────────────────────────────────────────
    private void OnMetodoChanged(object s, SelectionChangedEventArgs e)
    {
        if (TxtReferencia == null) return;
        var metodo = (CboMetodo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        TxtReferencia.IsEnabled = metodo != "EFECTIVO";
        ActualizarVisibilidadEfectivoRecibido();
    }

    // ──────────────────────────────────────────────
    //  MONTO RECIBIDO / VUELTO — antes exclusivo de EFECTIVO, ahora visible para cualquier
    //  método de pago (pedido explícito: habilitar pago parcial con Tarjeta/Transferencia/
    //  Cheque además de Efectivo). El campo pasa a llamarse "MONTO RECIBIDO" en general, y
    //  solo dice "EFECTIVO RECIBIDO" cuando el método realmente es efectivo — el resto de la
    //  lógica (comparar contra el total, faltante → pago parcial automático, vuelto) es
    //  idéntica sea cual sea el método, ya que CobrarParcialAsync/CobrarCuotaParcialParams
    //  siempre aceptaron cualquier FormaPago (nunca hubo una restricción real del lado del
    //  backend, solo la falta de un punto de entrada en la UI).
    // ──────────────────────────────────────────────
    private void ActualizarVisibilidadEfectivoRecibido()
    {
        if (PanelEfectivoRecibido == null) return;

        var metodo = (CboMetodo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        var esParcial = ChkPagoParcial.IsChecked == true;
        var corresponde = !esParcial;

        PanelEfectivoRecibido.Visibility = corresponde ? Visibility.Visible : Visibility.Collapsed;
        if (corresponde)
        {
            TxtLblMontoRecibido.Text = metodo == "EFECTIVO" ? "EFECTIVO RECIBIDO Gs." : "MONTO RECIBIDO Gs.";
            TxtEfectivoRecibido.Text = "";
            CardVuelto.Visibility    = Visibility.Collapsed;
            CardFaltante.Visibility  = Visibility.Collapsed;
        }
    }

    private void OnEfectivoRecibidoChanged(object s, TextChangedEventArgs e)
    {
        if (_formateandoEfectivoRecibido) return;

        _formateandoEfectivoRecibido = true;
        var digitos = new string(TxtEfectivoRecibido.Text.Where(char.IsDigit).ToArray());
        decimal.TryParse(digitos, out var recibido);
        TxtEfectivoRecibido.Text = recibido == 0 ? "" : recibido.ToString("N0").Replace(",", ".");
        TxtEfectivoRecibido.CaretIndex = TxtEfectivoRecibido.Text.Length;
        _formateandoEfectivoRecibido = false;

        // TxtTotalCobrar vive más abajo en el mismo árbol XAML (panel lateral) que
        // TxtEfectivoRecibido — con Text="0" fijado en XAML, InitializeComponent() dispara este
        // TextChanged mientras arma el árbol, ANTES de que el campo TxtTotalCobrar quede
        // asignado, dando null acá. CardVuelto/CardFaltante/BtnCobrar tienen el mismo riesgo.
        if (TxtTotalCobrar == null || CardVuelto == null || CardFaltante == null) return;

        decimal.TryParse(TxtTotalCobrar.Text, out var totalCobrar);

        if (recibido <= 0)
        {
            CardVuelto.Visibility   = Visibility.Collapsed;
            CardFaltante.Visibility = Visibility.Collapsed;
            return;
        }

        var diferencia = recibido - totalCobrar;
        if (diferencia > 0)
        {
            CardVuelto.Visibility   = Visibility.Visible;
            CardFaltante.Visibility = Visibility.Collapsed;
            TxtVuelto.Text = $"Gs. {diferencia:N0}";
        }
        else if (diferencia < 0)
        {
            CardVuelto.Visibility   = Visibility.Collapsed;
            CardFaltante.Visibility = Visibility.Visible;
            TxtFaltante.Text = $"Gs. {Math.Abs(diferencia):N0}";
        }
        else
        {
            CardVuelto.Visibility   = Visibility.Collapsed;
            CardFaltante.Visibility = Visibility.Collapsed;
        }
    }

    // ──────────────────────────────────────────────
    //  COBRAR
    // ──────────────────────────────────────────────
    private async void OnCobrar(object s, RoutedEventArgs e)
    {
        if (_cuotaSeleccionada == null || _clienteActual == null) return;

        // Si la cuota (o su crédito completo) está asignada a un cobrador distinto de a quién
        // se le va a atribuir ESTE cobro, no se deja cobrar — pedido explícito del dueño del
        // negocio: una vez que se asigna una cuota a un vendedor en "Asignaciones de
        // cobranza", nadie más debería poder registrar ese cobro. Se compara contra el
        // vendedor EFECTIVO (_vendedorCobrador si ya se aplicó un "Cambiar vendedor", si no el
        // usuario logueado) — así, tras aceptar el diálogo de abajo, un segundo click en
        // COBRAR ya pasa esta validación sin volver a preguntar.
        var idVendedorEfectivo = _vendedorCobrador?.Id ?? _session.UsuarioActual!.IdUsuario;
        var asignacion = await _cuotas.ObtenerAsignacionCuotaAsync(_cuotaSeleccionada.IdCab, _cuotaSeleccionada.IdGeneradas);
        if (asignacion != null && asignacion.IdCobrador != idVendedorEfectivo)
        {
            if (_session.UsuarioActual!.EsAdministrador)
            {
                var irAAsignaciones = MessageBox.Show(
                    $"Esta cuota está asignada a {asignacion.CobradorNombre}. Solo {asignacion.CobradorNombre} puede cobrarla.\n\n" +
                    "¿Abrir \"Asignaciones de cobranza\" para reasignarla o quitarle la asignación?",
                    "Cuota asignada a otro cobrador", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (irAAsignaciones == MessageBoxResult.Yes)
                    new CrediSoft.UI.Views.Cobros.CobranzaAsignacionesWindow { Owner = this }.Show();
            }
            else
            {
                // No administrador: en vez de solo bloquear, se ofrece aplicar automáticamente
                // "Cambiar vendedor" al cobrador ya asignado — la propia asignación previa en
                // el sistema es la autorización, no hace falta pedir su clave de nuevo (a
                // diferencia de OnCambiarVendedorCobrador/SeleccionarVendedorCobradorDialog,
                // que sí la exige para un cambio "libre"). El usuario logueado no cambia de
                // sesión ni de caja, solo se registra a quién se le atribuye la comisión.
                var dlg = new CuotaAsignadaOtroDialog(asignacion.CobradorNombre) { Owner = this };
                if (dlg.ShowDialog() == true && dlg.UsarComoVendedor)
                {
                    _vendedorCobrador = (asignacion.IdCobrador, asignacion.CobradorNombre);
                    ActualizarBadgeVendedorCobrador();
                    BadgeAsignadaOtro.Visibility = Visibility.Collapsed;
                }
            }
            return;
        }

        // Orden de pago FIFO por cuota: no se puede abonar/cobrar una cuota si otra ANTERIOR
        // del mismo crédito (N°Cuota menor) sigue pendiente — el sistema viejo no lo exigía,
        // pero permitía cuotas "salteadas" que confundían el atraso real del cliente.
        if (GridCuotas.ItemsSource is IEnumerable<Cuota> cuotasDelGrid)
        {
            var anteriorPendiente = cuotasDelGrid
                .Where(c => c.IdCab == _cuotaSeleccionada.IdCab
                            && c.Estado == 0
                            && c.NCuota < _cuotaSeleccionada.NCuota)
                .OrderBy(c => c.NCuota)
                .FirstOrDefault();

            if (anteriorPendiente != null)
            {
                MessageBox.Show(
                    $"Primero abone la cuota N° {anteriorPendiente.NCuota} (atrasada) para avanzar con esta cuota.",
                    "Cuota anterior pendiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var esParcial = ChkPagoParcial.IsChecked == true;
        if (esParcial)
        {
            await CobrarParcialAsync();
        }
        else
        {
            await CobrarCompletoAsync();
        }
    }

    // Muestra el aviso de caja cerrada con opción de ir directo a Apertura de Caja.
    // Si el usuario acepta, abre esa ventana — al volver, esta pantalla de Cobros
    // sigue abierta tal cual (con la cuota seleccionada), solo hay que reintentar cobrar.
    private void MostrarCajaCerrada()
    {
        var nombreLocal = _session.LocalActual?.NombreLocal ?? "este local";
        var dlg = new CajaCerradaDialog(nombreLocal) { Owner = this };
        dlg.ShowDialog();

        if (dlg.IrAAbrirCaja)
            new CajaAperturaWindow().Show();
    }

    private async Task CobrarCompletoAsync()
    {
        var c = _cuotaSeleccionada!;
        var reaj = ObtenerReajusteTotal();
        // "total" es el monto neto a cobrar HOY (lo que muestra la pantalla): capital
        // pendiente (Monto - Entrega) + Punitorio + Reajuste — ver RecalcTotal. Se usa acá
        // para las comparaciones con lo recibido en efectivo (vuelto, pago parcial automático).
        decimal.TryParse(TxtTotalCobrar.Text.Replace(",", ""), out var total);
        // Total BRUTO de la cuota (Monto + Punitorio + Reajuste, SIN restar entregas previas) —
        // esto es lo que espera @Total en el SP junto con @EntregaAnterior=c.Entrega real, igual
        // patrón que CobrarParcialAsync/totalCuotaBruto. Pasar el neto con EntregaAnterior=0
        // (como se hacía antes) hacía que GENERADAS.ENTREGA quedara en @Total en vez del total
        // bruto real de la cuota cuando ya había una entrega parcial previa — perdiendo esa
        // entrega anterior en el campo ENTREGA (mismo bug de fondo que en el SP parcial).
        var totalBruto = c.Monto + c.Punitorio + reaj;
        var metodo = (CboMetodo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EFECTIVO";
        var obs    = TxtNota.Text.Trim();
        var ref_   = TxtReferencia.Text.Trim();

        // Si el cobrador cargó cuánto recibió (con CUALQUIER método de pago, no solo efectivo
        // — pedido explícito 2026-08-11: habilitar pago parcial también con Tarjeta/
        // Transferencia/Cheque) y es menor al total, ya no se bloquea con un simple aviso (el
        // cajero tenía que saber de antemano que existía el toggle "Abono parcial" para poder
        // continuar, y varios no lo entendían). En su lugar se ofrece pasar automáticamente a
        // pago parcial por el monto realmente entregado.
        if (PanelEfectivoRecibido.Visibility == Visibility.Visible)
        {
            var digitosRecibido = new string(TxtEfectivoRecibido.Text.Where(char.IsDigit).ToArray());
            decimal.TryParse(digitosRecibido, out var recibido);
            if (recibido > 0 && recibido < total)
            {
                var dlgParcial = new PagoParcialAutomaticoDialog(
                    _clienteActual!.NombreCliente, c.NCuota, total, recibido) { Owner = this };
                dlgParcial.ShowDialog();
                if (!dlgParcial.ContinuarComoParcial) return;

                // Orden importa: OnPagoParcialToggled borra TxtMontoParcial.Text cuando se
                // activa (para que el cajero lo tipee de nuevo en el flujo manual normal),
                // así que el monto se asigna DESPUÉS de activar el toggle, no antes.
                ChkPagoParcial.IsChecked = true;
                OnPagoParcialToggled(ChkPagoParcial, new RoutedEventArgs());
                TxtMontoParcial.Text = recibido.ToString("N0").Replace(",", ".");
                // El usuario ya confirmó explícitamente en PagoParcialAutomaticoDialog que
                // quiere continuar como pago parcial — mostrar ConfirmarCobroDialog acá sería
                // una segunda confirmación redundante del mismo dato.
                await CobrarParcialAsync(omitirConfirmacion: true);
                return;
            }
        }

        // Si el cajero cargó cuánto recibió en efectivo y entregó más que el total, se
        // calcula el vuelto acá para mostrarlo en los modales de confirmación y éxito — igual
        // criterio que ImprimirTicketCobroAsync, nunca llega a la base de datos.
        decimal efectivoRecibido = 0, vuelto = 0;
        if (metodo == "EFECTIVO" && PanelEfectivoRecibido.Visibility == Visibility.Visible)
        {
            var digitos = new string(TxtEfectivoRecibido.Text.Where(char.IsDigit).ToArray());
            decimal.TryParse(digitos, out var recibidoTotal);
            if (recibidoTotal > total)
            {
                efectivoRecibido = recibidoTotal;
                vuelto = recibidoTotal - total;
            }
        }

        var dlgConfirm = new ConfirmarCobroDialog(
            nombreCliente:    _clienteActual!.NombreCliente,
            nCuota:           c.NCuota,
            comprobante:      c.Comprobante,
            capital:          c.Monto,
            punitorio:        c.Punitorio,
            reajuste:         reaj,
            entregaAnterior:  c.Entrega,
            total:            total,
            metodo:           metodo) { Owner = this };
        dlgConfirm.ShowDialog();
        if (!dlgConfirm.Confirmado) return;

        try
        {
            BtnCobrar.IsEnabled = false;

            var caja = await App.Services.GetRequiredService<ICajaRepository>()
                .ObtenerCajaAbiertaAsync(_session.LocalActual!.IdLocal);
            if (caja == null)
            {
                MostrarCajaCerrada();
                return;
            }

            var prm = new CobrarCuotaParams(
                IdCab:           c.IdCab,
                IdGeneradas:     c.IdGeneradas,
                NCuota:          c.NCuota,
                Comprobante:     c.Comprobante,
                MontoCuota:      c.Monto,
                Mora:            c.Mora,
                Punitorio:       c.Punitorio,
                Total:           totalBruto,
                IdUsuario:       _vendedorCobrador?.Id ?? _session.UsuarioActual!.IdUsuario,
                IdLocal:         (byte)_session.LocalActual.IdLocal,
                IdCajaFisica:    caja.IdCajaFisica,
                EntregaAnterior: c.Entrega,
                Inforcom:        _clienteActual.Inforcom,
                ElEstadoCab:     1,
                FormaPago:       metodo,
                Referencia:      ref_,
                Obs:             obs,
                Reajuste:        reaj,
                IdUsuarioSesion: _session.UsuarioActual!.IdUsuario);

            var ok = await _cuotas.CobrarCuotaAsync(prm);

            if (ok)
            {
                // Si el cargo de Inforconf se incluyó en este cobro, marcar la cuota para
                // que no se vuelva a aplicar mientras dure el mismo episodio de mora.
                if (TxtInforconf.Text != "0")
                    await _cuotas.MarcarInforconfAplicadoAsync(c.IdGeneradas);

                // La pantalla se refresca ANTES de mostrar el modal de éxito (no después de
                // cerrarlo) — el usuario reportó que quedaba desactualizada "detrás" del
                // modal (ej. YA ENTREGADO seguía en 0) mientras el modal seguía abierto, y
                // el pago ya está confirmado en este punto, así que no hay razón para esperar.
                // CargarCliente() lee TxtCi.Text, que queda VACÍO cuando se llegó al cliente
                // por "Listar clientes" (modo "Todos" — PanelBusquedaCI oculto) en vez de
                // tipeando el CI — en ese caso CargarCliente() retornaba sin hacer nada.
                // _clienteActual ya tiene el cliente cargado sin depender de TxtCi.
                await MostrarClienteYCuotas();
                Activate();
                UpdateLayout();

                var dlgExito = new CobroExitosoDialog(
                    nombreCliente: _clienteActual.NombreCliente,
                    nCuota:        c.NCuota,
                    total:         total,
                    metodo:        metodo,
                    totalACobrar:  efectivoRecibido > 0 ? total : 0,
                    efectivoRecibido: efectivoRecibido,
                    vuelto:        vuelto) { Owner = this };
                dlgExito.ShowDialog();

                if (dlgExito.Imprimir)
                    await ImprimirTicketCobroAsync(c, total, reaj);
            }
            else
            {
                MessageBox.Show("Error al procesar el cobro.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Cobro no autorizado", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error inesperado:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnCobrar.IsEnabled = _cuotaSeleccionada != null;
        }
    }

    private async Task CobrarParcialAsync(bool omitirConfirmacion = false)
    {
        var c = _cuotaSeleccionada!;
        var reaj = ObtenerReajusteTotal();
        var digitosMonto = new string(TxtMontoParcial.Text.Where(char.IsDigit).ToArray());
        decimal.TryParse(digitosMonto, out var montoPagado);
        var metodo = (CboMetodo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EFECTIVO";
        var obs    = TxtNota.Text.Trim();
        var ref_   = TxtReferencia.Text.Trim();
        // Saldo pendiente real (lo que el usuario ve en pantalla y lo que decide vuelto):
        // capital menos lo ya entregado, más c.Punitorio (persistido, calculado sobre el
        // capital ORIGINAL — ver CalcularPunitoriocAsync) más el reajuste. Igual criterio que
        // RecalcTotal: si Entrega ya superó Monto, ese excedente se descuenta del resto en vez
        // de perderse (ver comentario extenso en RecalcTotal).
        var capitalPendiente = Math.Max(0, c.Monto - c.Entrega);
        var excedenteEntregaParcial = Math.Max(0, c.Entrega - c.Monto);
        var saldoPendiente = capitalPendiente + Math.Max(0, c.Punitorio + reaj - excedenteEntregaParcial);
        // Total BRUTO de la cuota (Monto + Punitorio + Reajuste, SIN restar ninguna entrega) —
        // este es el valor que espera @TotalCuota en el SP, igual criterio que ya usa
        // sp_Guardar_Cobranza_Cs_2026 (pago completo) con @Total/@MontoHoy. El SP mismo resta
        // la entrega acumulada (@EntregaAnterior + @MontoPagado) de este bruto para obtener el
        // saldo que queda. Pasarle en cambio un total ya neto (como se hacía antes) y
        // @EntregaAnterior=0 fallaba en el SEGUNDO abono parcial sobre la misma cuota: el SP
        // pisaba GENERADAS.ENTREGA con solo el monto de hoy en vez de sumarlo al acumulado real,
        // perdiendo el abono anterior (bug real detectado vía AUDITORIA: tras dos abonos de
        // Gs. 10 sobre la misma cuota, ENTREGA quedaba en 10 en vez de 20).
        var totalCuotaBruto = c.Monto + c.Punitorio + reaj;

        if (montoPagado <= 0)
        {
            MessageBox.Show("Ingrese el monto que el cliente va a abonar.", "Monto inválido",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Si lo que entrega el cliente supera el saldo pendiente, esto ya no es un abono
        // parcial: es un pago completo con vuelto. Acreditamos solo el saldo real (nunca
        // mas que el total de la cuota) y el resto se devuelve como vuelto — no se infla
        // la deuda del cliente con dinero que en la practica vuelve a su bolsillo.
        var vuelto = 0m;
        var montoAAcreditar = montoPagado;
        if (montoPagado > saldoPendiente)
        {
            vuelto = montoPagado - saldoPendiente;
            montoAAcreditar = saldoPendiente;
        }

        // Cuando se llega acá desde el flujo automático (PagoParcialAutomaticoDialog ya
        // mostrado y confirmado en CobrarCompletoAsync), esta segunda confirmación se salta —
        // repetir la misma pregunta con otro modal es redundante para el cajero.
        if (!omitirConfirmacion)
        {
            var dlgConfirm = new ConfirmarCobroDialog(
                nombreCliente:    _clienteActual!.NombreCliente,
                nCuota:           c.NCuota,
                comprobante:      c.Comprobante,
                capital:          c.Monto,
                punitorio:        c.Punitorio,
                reajuste:         reaj,
                entregaAnterior:  c.Entrega,
                total:            montoAAcreditar,
                metodo:           metodo) { Owner = this };
            dlgConfirm.ShowDialog();
            if (!dlgConfirm.Confirmado) return;
        }

        try
        {
            BtnCobrar.IsEnabled = false;

            var caja = await App.Services.GetRequiredService<ICajaRepository>()
                .ObtenerCajaAbiertaAsync(_session.LocalActual!.IdLocal);
            if (caja == null)
            {
                MostrarCajaCerrada();
                return;
            }

            // Al SP siempre se le pasa lo que se acredita a la deuda (montoAAcreditar), nunca
            // el efectivo bruto entregado por el cliente — igual que en pago completo, donde
            // el vuelto jamás llega a la base de datos, solo se calcula y muestra en pantalla.
            // TotalCuota=totalCuotaBruto (Monto+Punitorio+Reajuste, sin restar entregas) y
            // EntregaAnterior=c.Entrega (el acumulado real ya entregado antes de hoy) — el SP
            // resta la entrega total (anterior + hoy) de este bruto para obtener el saldo que
            // queda, igual patrón que sp_Guardar_Cobranza_Cs_2026.
            var prm = new CobrarCuotaParcialParams(
                IdCab:           c.IdCab,
                IdGeneradas:     c.IdGeneradas,
                NCuota:          c.NCuota,
                Comprobante:     c.Comprobante,
                MontoCuota:      c.Monto,
                Mora:            c.Mora,
                Punitorio:       c.Punitorio,
                Reajuste:        reaj,
                TotalCuota:      totalCuotaBruto,
                MontoPagado:     montoAAcreditar,
                IdUsuario:       _vendedorCobrador?.Id ?? _session.UsuarioActual!.IdUsuario,
                IdLocal:         (byte)_session.LocalActual.IdLocal,
                IdCajaFisica:    caja.IdCajaFisica,
                EntregaAnterior: c.Entrega,
                Inforcom:        _clienteActual.Inforcom,
                FormaPago:       metodo,
                Referencia:      ref_,
                Obs:             obs,
                IdUsuarioSesion: _session.UsuarioActual!.IdUsuario);

            var (ok, pagoCompleto, msg) = await _cuotas.CobrarCuotaParcialAsync(prm);

            if (ok)
            {
                // Si el cargo de Inforconf se incluyó en este abono, marcar la cuota para
                // que no se vuelva a aplicar mientras dure el mismo episodio de mora.
                if (TxtInforconf.Text != "0")
                    await _cuotas.MarcarInforconfAplicadoAsync(c.IdGeneradas);

                // Saldo que queda pendiente en la cuota tras este abono (0 si el pago la cerró
                // — pagoCompleto ya lo indica el SP). Se muestra en el modal para que quede
                // claro que la cuota sigue activa, no solo cuánto se acreditó hoy.
                var saldoTrasAbono = pagoCompleto ? 0 : Math.Max(0, saldoPendiente - montoAAcreditar);

                // Refresco ANTES del modal de éxito — ver comentario equivalente en
                // CobrarCompletoAsync (CargarCliente() depende de TxtCi.Text, vacío cuando se
                // llegó por "Listar clientes"; el pago ya está confirmado en este punto).
                await MostrarClienteYCuotas();
                Activate();
                UpdateLayout();

                var dlgExito = new CobroExitosoDialog(
                    nombreCliente: _clienteActual.NombreCliente,
                    nCuota:        c.NCuota,
                    total:         montoAAcreditar,
                    metodo:        metodo,
                    esParcial:     !pagoCompleto,
                    saldoPendiente: saldoTrasAbono,
                    totalACobrar:  vuelto > 0 ? montoPagado - vuelto : 0,
                    efectivoRecibido: vuelto > 0 ? montoPagado : 0,
                    vuelto:        vuelto) { Owner = this };
                dlgExito.ShowDialog();

                if (dlgExito.Imprimir)
                    await ImprimirTicketCobroAsync(c, montoAAcreditar, reaj);
            }
            else
            {
                MessageBox.Show($"Error al procesar el abono:\n{msg}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Cobro no autorizado", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error inesperado:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnCobrar.IsEnabled = _cuotaSeleccionada != null;
        }
    }

    // ──────────────────────────────────────────────
    //  ACCIONES LATERALES
    // ──────────────────────────────────────────────
    private async void OnEliminarFactura(object s, RoutedEventArgs e)
    {
        if (_clienteActual == null || _idCabActual == null)
        {
            MessageBox.Show("Seleccioná primero un cliente.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlgElim = new EliminarCreditoDialog(_clienteActual.NombreCliente, _idCabActual.Value) { Owner = this };
        dlgElim.ShowDialog();
        if (!dlgElim.Confirmado) return;

        // Pedir autorización de administrador
        var auth = new AutorizacionAdminModal($"Eliminar crédito #{_idCabActual} — {_clienteActual.NombreCliente}") { Owner = this };
        if (auth.ShowDialog() != true) return;

        try
        {
            using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();

            // Verificar cuotas cobradas ANTES de intentar eliminar (ESTADO=0 = cobrada)
            var cobradas = (await conn.QueryAsync<CuotaCobradaItem>(
                @"SELECT NCUOTA AS NCuota,
                         ISNULL(COMPROBANTE, '') AS Comprobante,
                         ISNULL(TOTAL, 0) AS Monto,
                         ISNULL(FECHACOBRADO, GETDATE()) AS FechaPago
                  FROM GENERADAS
                  WHERE IDCAB = @IdCab AND ESTADO = 0
                  ORDER BY NCUOTA",
                new { IdCab = _idCabActual.Value })).ToList();

            if (cobradas.Count > 0)
            {
                var dlgErr = new ErrorEliminacionDialog(
                    _clienteActual.NombreCliente,
                    _idCabActual.Value,
                    cobradas) { Owner = this };
                dlgErr.ShowDialog();
                return;
            }

            var cab = await conn.QueryFirstOrDefaultAsync<(decimal Total, decimal Entregado, string NVenta, byte IdLocal)>(
                "SELECT TOTAL, ISNULL(ENTREGANORMAL,0) as Entregado, ISNULL(NVENTACHAR,'') as NVenta, ID_LOCAL " +
                "FROM CABECERA_SALES WHERE IDCAB = @Id",
                new { Id = _idCabActual.Value });

            var prm = new EliminarVentaParams(
                IdCab:      _idCabActual.Value,
                IdLocal:    cab.IdLocal,
                IdUsuario:  _session.UsuarioActual!.IdUsuario,
                TotalVenta: cab.Total,
                Entregado:  cab.Entregado,
                NVenta:     cab.NVenta);

            var ok = await _cuotas.EliminarVentaCreditoAsync(prm);

            if (ok)
            {
                MessageBox.Show("Crédito eliminado correctamente.", "Listo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                _clienteActual     = null;
                _idCabActual       = null;
                _cuotaSeleccionada = null;
                PanelCliente.Visibility = Visibility.Collapsed;
                GridCuotas.ItemsSource  = null;
                LimpiarInfoCredito();
                OcultarPanelCobro();
            }
            else
            {
                MessageBox.Show("Error al eliminar el crédito.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error inesperado:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnHistorial(object s, RoutedEventArgs e)
    {
        if (_clienteActual == null || _idCabActual == null)
        { MessageBox.Show("Seleccioná primero un cliente.", "Aviso"); return; }

        var cuotas = (await _cuotas.ObtenerHistorialAsync(_idCabActual.Value))
            .Select(c => new CuotaHistorialDetallada(c.NCuota, c.Monto, c.Vto, c.Estado, c.Mora, c.Obs, c.FechaPago, c.DiasVtoAPago));
        new HistorialCobrosModal(_clienteActual.NombreCliente, _idCabActual.Value, cuotas) { Owner = this }
            .ShowDialog();
    }

    private async void OnVerArticulos(object s, RoutedEventArgs e)
    {
        if (_idCabActual == null)
        { MessageBox.Show("Seleccioná primero un cliente.", "Aviso"); return; }

        var arts  = await _cuotas.ObtenerArticulosAsync(_idCabActual.Value);
        var items = arts.Select(a => new ArticuloVenta(a.Descripcion, a.Cantidad, a.PVenta));
        var modal = new ArticulosModal(_idCabActual.Value, items) { Owner = this };
        modal.ShowDialog();
    }

    // ──────────────────────────────────────────────
    //  TECLADO Y CIERRE
    // ──────────────────────────────────────────────
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)         Close();
        else if (e.Key == Key.F2)        TxtCi.Focus();
        else if (e.Key == Key.F3)        OnListarCuotas(this, new RoutedEventArgs());
        else if (e.Key == Key.F5 && BtnCobrar.IsEnabled) OnCobrar(this, new RoutedEventArgs());
    }

    // ──────────────────────────────────────────────
    //  TICKET DE COBRO
    // ──────────────────────────────────────────────
    private async Task ImprimirTicketCobroAsync(Cuota c, decimal totalPagado, decimal reajuste)
    {
        try
        {
            var emp     = await TicketPrinter.ObtenerDatosEmpresaAsync();
            var nTicket = await TicketPrinter.ObtenerNumeroTicketAsync(_session.LocalActual?.IdLocal ?? 1);
            var fmt     = await TicketPrinter.ObtenerFormatoComprobanteAsync();

            var nombreLocal = "";
            var telefonoLocal = "";
            try {
                var idLocal = _session.LocalActual?.IdLocal ?? 1;
                using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();
                var loc = await conn.QueryFirstOrDefaultAsync<(string Nombre, string Telefono)>(
                    "SELECT TOP 1 NOMBRE AS Nombre, TELEFONO AS Telefono FROM LOCALES WHERE ID_LOCAL=@id",
                    new { id = idLocal });
                telefonoLocal = loc.Telefono ?? "";
                nombreLocal = string.IsNullOrWhiteSpace(loc.Nombre)
                    ? $"LOCAL: {idLocal}"
                    : $"LOCAL: {idLocal}  —  {loc.Nombre}";
            } catch { }

            var esCancelacion = c.Estado == 0 && c.Entrega + totalPagado >= c.Monto;
            var concepto = esCancelacion ? "CANCELACION DE CUOTA" : "ENTREGA PARCIAL";
            var metodo   = (CboMetodo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EFECTIVO";
            var cajero   = _session.UsuarioActual?.NombreUsuario ?? "";

            // Si el cajero cargó cuánto recibió en efectivo y entregó más de lo que se cobró,
            // se muestra el desglose completo en el ticket (Total a Cobrar / Total Entregado /
            // Vuelto) — mismo criterio que TxtVuelto en pantalla, nunca llega a la base.
            decimal efectivoRecibido = 0, vuelto = 0;
            if (metodo == "EFECTIVO" && PanelEfectivoRecibido.Visibility == Visibility.Visible)
            {
                var digitosRecibido = new string(TxtEfectivoRecibido.Text.Where(char.IsDigit).ToArray());
                decimal.TryParse(digitosRecibido, out var recibido);
                if (recibido > totalPagado)
                {
                    efectivoRecibido = recibido;
                    vuelto = recibido - totalPagado;
                }
            }

            var datos = new DatosTicketCobro(
                NombreEmpresa:   emp.Nombre,
                NombreLocal:     nombreLocal,
                Fecha:           DateTime.Now,
                NumeroTicket:    nTicket,
                Comprobante:     c.Comprobante,
                Concepto:        concepto,
                // NCuota real (no NCuotaVisible) — consistente con el header del panel de
                // cobro y la grilla "Cuotas del crédito", que ya usan este mismo criterio
                // (ver corrección 2026-08-01). Antes el ticket restaba 1 para la numeración
                // de Proforma, y quedaba desfasado del número que el cajero ve en pantalla
                // (ej. pantalla "5/5", ticket "Cuota N° 4").
                NCuota:          c.NCuota,
                // Mismo criterio que TxtMontoCuota en el panel de cobro (capitalPendiente,
                // ver comentario en RefrescarCuota): capital PENDIENTE, no el monto original
                // de la cuota. Usar c.Monto acá hacía que el ticket mostrara "Capital: 300.000"
                // mientras la pantalla ya mostraba "Capital: 240.000" tras descontar la entrega
                // parcial — un comprobante impreso que no coincidía con lo que el cajero veía.
                EntregaCapital:  Math.Max(0, c.Monto - c.Entrega),
                DiasAtraso:      c.Mora,
                Punitorio:       c.Punitorio,
                Reajuste:        reajuste,
                EntregaAnterior: c.Entrega,
                TotalPagado:     totalPagado,
                Timbrado:        emp.Timbrado,
                VigenciaDesde:   emp.Desde,
                VigenciaHasta:   emp.Hasta,
                NombreCliente:   _clienteActual?.NombreCliente ?? "",
                Cajero:          cajero,
                MetodoPago:      metodo,
                EfectivoRecibido: efectivoRecibido,
                Vuelto:           vuelto,
                TelefonoLocal:    telefonoLocal);

            // Registro histórico para reimpresión posterior (ver módulo "Reimprimir
            // comprobantes") — no bloquea ni retrasa la impresión si falla.
            _ = TicketPrinter.RegistrarComprobanteAsync(
                tipo: "COBRO", numeroTicket: nTicket, idLocal: _session.LocalActual?.IdLocal ?? 1,
                datosTicket: datos,
                idUsuarioCajero: _session.UsuarioActual?.IdUsuario, nombreCajero: cajero,
                idCliente: _clienteActual?.IdCliente, nombreCliente: _clienteActual?.NombreCliente,
                idCab: c.IdCab, idGeneradas: c.IdGeneradas,
                nrecibo: c.Comprobante, montoTotal: totalPagado);

            // Mostrar vista previa según formato configurado
            var previa = new ComprobantePreviaWindow(datos, fmt) { Owner = this };
            previa.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al generar comprobante:\n{ex.Message}", "Impresión",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

internal class LocalItem
{
    public int    Id     { get; set; }
    public string Nombre { get; set; } = "";
}
