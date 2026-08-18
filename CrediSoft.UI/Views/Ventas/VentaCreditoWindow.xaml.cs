using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.UI.Views.Compras;
using CrediSoft.UI.Views.Informes;
using CrediSoft.UI.Views.Shared;
using Dapper;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Ventas;

// Artículo enriquecido con precio del local para el selector
internal class ArticuloConPrecio
{
    public int     Id         { get; set; }
    public string Ca { get; set; } = string.Empty;
    public string D { get; set; } = string.Empty;
    public string MarcaNombre { get; set; } = string.Empty;
    public decimal PventaLocal { get; set; }
    public decimal Pc { get; set; }
    public decimal Iva { get; set; }
    public decimal Stock { get; set; }
    public string LocalUbicacion { get; set; } = string.Empty;
    public int MaxCuota { get; set; }

    // Plan de pagos configurado en el simulador del modal "Seleccionar Artículo" (0 cuando el
    // artículo viene de otro flujo, como búsqueda por código, que no pasa por ese simulador).
    public decimal PctDescuentoPlan { get; set; }
    public decimal EntregaPlan      { get; set; }
    public decimal PctRecargoPlan   { get; set; }
    public int     CantCuotasPlan   { get; set; }
    public decimal ValorNetoPlan    { get; set; }
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
    // Bindeadas por las grillas de detalle (VentaContadoWindow/VentaCreditoWindow) en vez de
    // Pv/Subtotal directo — el StringFormat de un DataGridTextColumn no reformatea decimales
    // igual que ToString("N0"), así que se exponen ya formateadas con separador de miles.
    public string  PvStr          => Pv.ToString("N0").Replace(",", ".");
    public string  SubtotalStr    => Subtotal.ToString("N0").Replace(",", ".");
}

public partial class VentaCreditoWindow : Window
{
    private readonly IArticuloRepository _artRepo;
    private readonly IVentaRepository    _ventaRepo;
    private readonly IDbConnectionFactory _db;

    // % Recargo sugerido según la cantidad de cuotas — misma escala que VerArticulosWindow.
    // El sistema viejo (CrediSoft_local.exe) usaba 4,5% (verificado en vivo: Precio
    // 11.500.000, 3 cuotas, Entrega 3.833.333 → Valor mensual 2.900.556, Valor Final
    // 8.701.667, exacto con 4,5%), pero la dirección de Credimar pidió expresamente bajar la
    // tasa a 4,2% en el sistema nuevo — no es un error de cálculo, es una decisión de negocio.
    // No persiste en ninguna tabla ni columna de la base de datos, ni la actual ni la vieja:
    //   1-2 cuotas -> 0%, 3-10 cuotas -> 4,2%, 11+ cuotas -> 4%.
    private static decimal PctRecargoSugerido(int cuotas) => cuotas switch
    {
        <= 2  => 0m,
        <= 10 => 4.2m,
        _     => 4m,
    };
    // true cuando el cajero tipeó ENTREGA a mano para el artículo actual — a partir de ahí se
    // deja de autocompletarla al cambiar % Descuento/Cuotas (mismo criterio que
    // VerArticulosWindow/_entregaEditadaManualmente).
    private bool _entregaArticuloEditadaManualmente = false;
    // true mientras RecalcularLinea asigna TxtEntregaArticulo.Text por autocompletado — evita
    // que ese cambio de texto se confunda con una edición manual del cajero.
    private bool _recalculandoLinea = false;
    // Pedido explícito del cliente: el vendedor puede subir el % Recargo a mano en 3-10 y 11+
    // cuotas (para negociar/ganar más), pero NUNCA por debajo de 4,2% en ninguno de esos dos
    // rangos — ni siquiera en 11+ cuotas, que hoy sugiere 4% (por debajo del piso). El rango de
    // 1-2 cuotas NO se toca: sigue en 0% fijo, no editable, sin piso. Igual patrón que
    // _entregaArticuloEditadaManualmente: una vez que el cajero edita a mano, RecalcularLinea ya
    // no pisa el valor con el sugerido — solo lo saca del rango prohibido si lo hizo bajar de 4,2%.
    private bool _recargoEditadoManualmente = false;

    private PersonaItem? _clienteActual;
    private PersonaItem? _garanteActual;
    private int _idRef1, _idRef2;
    private byte _idLocalForm;   // local seleccionado en el form (botón Local)
    private ArticuloConPrecio? _articuloActual;
    private readonly List<LineaDetalle> _carrito = new();

    // Vendedor elegido en el selector (TxtVendedor solo guarda el NOMBRE, no alcanza para
    // grabar CAB_SOL_SALES.ID_USUARIO) — null hasta que el usuario abre el selector y elige
    // a alguien; si queda null al guardar, cae al usuario logueado (comportamiento anterior).
    private int? _idVendedorSeleccionado;

    public VentaCreditoWindow()
    {
        InitializeComponent();
        var svc = App.Services;
        _artRepo  = svc.GetRequiredService<IArticuloRepository>();
        _ventaRepo= svc.GetRequiredService<IVentaRepository>();
        _db       = svc.GetRequiredService<IDbConnectionFactory>();

        DtpSolicitud.SelectedDate = DateTime.Today;
        DtpFigurar.SelectedDate   = DateTime.Today;


        Loaded += async (_, _) => {
            if (FindName("TxtEstado") is System.Windows.Controls.TextBlock tb) tb.Text = "NUEVO";
            await GenerarNumeroSolicitudAsync();

            // Solo un ADMINISTRADOR (o el usuario con excepción puntual, ver
            // Usuario.PuedeVerTodosLosLocales) puede elegir el local de la solicitud. Un
            // vendedor normal siempre solicita desde SU local — se precarga automáticamente
            // y el botón para cambiarlo desaparece.
            var session = CrediSoft.Core.Services.SessionService.Instance;
            if (session.UsuarioActual?.PuedeVerTodosLosLocales != true && session.LocalActual != null)
            {
                _idLocalForm = (byte)session.LocalActual.IdLocal;
                if (FindName("TxtLocal")       is System.Windows.Controls.TextBox txLoc) txLoc.Text = session.LocalActual.IdLocal.ToString();
                if (FindName("TxtLocalNombre") is System.Windows.Controls.TextBox txNom) txNom.Text = session.LocalActual.NombreLocal;
                if (FindName("BtnSeleccionarLocal") is System.Windows.Controls.Button btnLoc) btnLoc.Visibility = Visibility.Collapsed;
            }

            // toggle Ingresos/Egresos
            if (FindName("BdrIEHeader")  is System.Windows.Controls.Border    ieHdr   &&
                FindName("PnlIE")        is System.Windows.Controls.Grid      iePanel &&
                FindName("TxtIEChevron") is System.Windows.Controls.TextBlock ieChev)
            {
                ieHdr.MouseLeftButtonUp += (_, _) =>
                {
                    bool visible = iePanel.Visibility == System.Windows.Visibility.Visible;
                    iePanel.Visibility = visible ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
                    ieChev.Text = visible ? "▼" : "▲";
                };
            }

            // navegación tabs
            InicializarNavegacion();
        };
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
        => await AbrirSelectorLocalAsync(obligatorio: false);

    private async Task AbrirSelectorLocalAsync(bool obligatorio)
    {
        try
        {
            var localRepo = App.Services.GetRequiredService<ILocalRepository>();
            var locales   = (await localRepo.ListarTodosAsync()).ToList();
            if (locales.Count == 0) { MessageBox.Show("No hay locales registrados.", "Aviso"); return; }

            Local? seleccionado = null;
            seleccionado = SelectorModal.MostrarLocales(this, locales, obligatorio);

            if (seleccionado != null)
            {
                _idLocalForm = (byte)seleccionado.IdLocal;
                if (FindName("TxtLocal")       is TextBox txLoc) txLoc.Text = seleccionado.IdLocal.ToString();
                if (FindName("TxtLocalNombre") is TextBox txNom) txNom.Text = seleccionado.NombreLocal;
            }
            else if (obligatorio)
            {
                this.Close();
            }
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error"); }
    }

    private async void OnBuscarVendedor(object sender, RoutedEventArgs e)
        => await AbrirSelectorVendedorAsync();

    private async Task AbrirSelectorVendedorAsync()
    {
        try
        {
            var usuarioRepo = App.Services.GetRequiredService<IUsuarioRepository>();
            var usuarios    = (await usuarioRepo.ListarTodosAsync()).ToList();
            if (usuarios.Count == 0) { MessageBox.Show("No hay usuarios registrados.", "Aviso"); return; }

            var seleccionado = SelectorModal.MostrarVendedores(this, usuarios);
            if (seleccionado != null)
            {
                TxtVendedor.Text = seleccionado.NombreUsuario;
                _idVendedorSeleccionado = seleccionado.IdUsuario;
            }
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error"); }
    }

    // ── CLIENTE ───────────────────────────────────────────────────────────────

    private void OnBuscarCliente(object sender, RoutedEventArgs e)
    {
        // Show() en vez de ShowDialog() — este buscador bloqueaba todo el sistema mientras
        // estaba abierto (bug real reportado: no se podía hacer click en otro módulo con
        // "Buscar Persona" abierto). Owner se mantiene (ver BuscadorPersonaWindow: ahora no
        // se puede minimizar, así que Owner ya no arrastra a todo el programa si alguien
        // intenta minimizarlo). La selección se procesa en Closed en vez de sincrónicamente.
        var win = new BuscadorPersonaWindow(_db) { Owner = this };
        win.Closed += async (_, _) =>
        {
            if (win.PersonaSeleccionada == null) return;
            _clienteActual = win.PersonaSeleccionada;
            // Cliente distinto → la excepción de funcionario público no se arrastra de la
            // consulta anterior. Se resetea acá (antes de RellenarCamposCliente/ActualizarCard)
            // para que no quede marcada por error para un cliente que nunca la tuvo.
            _esFuncionarioPublico = false;
            if (FindName("ChkFuncionarioPublico") is CheckBox chkReset) chkReset.IsChecked = false;
            RellenarCamposCliente(_clienteActual);
            await ActualizarCardEstadoGaranteAsync();
            await ActualizarPanelFotoCedulaAsync();
        };
        win.Show();
    }

    // Marca manual del cajero: "este cliente es funcionario público (policía, docente, etc.)".
    // Pedido explícito de Credimar — el legacy no tenía esta excepción, es una regla nueva: exime
    // de garante aunque el historial de créditos por sí solo lo exigiría (ver
    // EvaluarRequiereGaranteAsync). Solo tiene efecto mientras el checkbox está visible (o sea,
    // mientras el cliente ya requeriría garante por historial) — ver ActualizarCardEstadoGaranteAsync.
    private bool _esFuncionarioPublico = false;

    private async void OnFuncionarioPublicoChanged(object sender, RoutedEventArgs e)
    {
        _esFuncionarioPublico = (sender as CheckBox)?.IsChecked == true;
        await ActualizarCardEstadoGaranteAsync();
    }

    // Regla de negocio (igual que el legacy: "cliente nuevo o su calificación es menor a 3 -Bueno-
    // necesita garante"): garante obligatorio si el cliente es nuevo (nunca tuvo un crédito), o si
    // ya tuvo crédito(s) pero ninguno fue cancelado (pagado por completo) todavía — eso es lo que
    // se expone como "calificación menor a 3". Inforconf/CLIENTES.INFORCOM NO forma parte de esta
    // regla: es un dato informativo de la ficha del cliente, no un criterio de garante. Único
    // punto de verdad, reutilizado por el card informativo y por la validación de OnConfirmarVenta.
    //
    // La excepción de "funcionario público" (_esFuncionarioPublico) se aplica ACÁ, no solo en la
    // UI: si no se filtrara en este método, OnConfirmarVenta/ValidarTabActual (que llaman este
    // mismo método) seguirían bloqueando el guardado pese a que el card ya mostraba "no requerido".
    private async Task<(bool RequiereGarante, string Motivo)> EvaluarRequiereGaranteAsync()
    {
        if (_clienteActual == null) return (false, "");

        var clienteRepo = App.Services.GetRequiredService<IClienteRepository>();
        var (creditosPrevios, creditosCancelados) = await clienteRepo.ContarCreditosPreviosDetalladoAsync(_clienteActual.Id);

        bool requiere;
        string motivo;
        if (creditosPrevios == 0)
        {
            requiere = true;
            motivo = "el cliente es nuevo (no tiene créditos anteriores)";
        }
        else if (creditosCancelados == 0)
        {
            requiere = true;
            motivo = "su calificación como cliente es menor a 3 (Bueno)";
        }
        else
        {
            return (false, "su calificación como cliente es Buena");
        }

        if (requiere && _esFuncionarioPublico)
            return (false, "es funcionario público (policía, docente, etc.)");

        return (requiere, motivo);
    }

    private async Task ActualizarCardEstadoGaranteAsync()
    {
        var card   = FindName("CardEstadoGarante") as Border;
        var icono  = FindName("IconoEstadoGarante") as TextBlock;
        var texto  = FindName("TxtEstadoGarante") as TextBlock;
        var chkFuncPublico = FindName("ChkFuncionarioPublico") as CheckBox;
        if (card == null || icono == null || texto == null) return;

        if (_clienteActual == null)
        {
            card.Visibility = Visibility.Collapsed;
            return;
        }

        // Motivo "base" (sin la excepción) decide si el checkbox se muestra — el checkbox debe
        // seguir visible y marcable aunque el cajero ya lo haya tildado (si se ocultara al
        // eximir, no podría destildarlo para volver atrás).
        var clienteRepo = App.Services.GetRequiredService<IClienteRepository>();
        var (creditosPrevios, creditosCancelados) = await clienteRepo.ContarCreditosPreviosDetalladoAsync(_clienteActual.Id);
        var requiereGarantePorHistorial = creditosPrevios == 0 || creditosCancelados == 0;

        if (chkFuncPublico != null)
            chkFuncPublico.Visibility = requiereGarantePorHistorial ? Visibility.Visible : Visibility.Collapsed;

        var (requiereGarante, motivo) = await EvaluarRequiereGaranteAsync();
        card.Visibility = Visibility.Visible;
        PintarCardGarante(card, icono, texto, requiereGarante, motivo);

        // Card gemelo en la pestaña Garante — mismo estado, para que el cajero no tenga que
        // volver a la pestaña Cliente para saber si el garante es obligatorio u opcional acá.
        var cardTab  = FindName("CardEstadoGaranteTab")  as Border;
        var iconoTab = FindName("IconoEstadoGaranteTab") as TextBlock;
        var textoTab = FindName("TxtEstadoGaranteTab")   as TextBlock;
        if (cardTab != null && iconoTab != null && textoTab != null)
        {
            cardTab.Visibility = Visibility.Visible;
            PintarCardGarante(cardTab, iconoTab, textoTab, requiereGarante, motivo);
        }
    }

    private static void PintarCardGarante(Border card, TextBlock icono, TextBlock texto, bool requiereGarante, string motivo)
    {
        if (requiereGarante)
        {
            card.Background  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 245, 245));
            card.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 192, 192));
            icono.Text = "⚠";
            texto.Text = $"Este cliente requiere garante: {motivo}.";
            texto.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 40, 27));
        }
        else
        {
            card.Background  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 250, 244));
            card.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 220, 195));
            icono.Text = "✔";
            texto.Text = $"Garante no requerido: {motivo}.";
            texto.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 100, 58));
        }
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
        TxtClienteAntiguedad.Text   = c.Antiguedad;
        TxtClienteCredMax.Text      = c.CredMax > 0 ? c.CredMax.ToString("N0") : "";
        TxtClienteSaldo.Text        = c.SaldoActivo > 0 ? c.SaldoActivo.ToString("N0") : "0";
        TxtClienteConyuge.Text      = c.Conyuge;
        TxtClienteVencCI.Text       = c.VencCI;
    }

    private void OnVerHistorialCliente(object sender, RoutedEventArgs e)
    {
        if (_clienteActual == null) { MessageBox.Show("Primero seleccione un cliente.", "Aviso"); return; }
        CrediSoft.UI.Views.Shared.HistorialCrediticioModal.Mostrar(this, _db, _clienteActual.Id, _clienteActual.Nombre);
    }

    // Click en la miniatura del panel de foto de cédula — misma lógica que el botón "Ver
    // cédula", solo con otro punto de entrada (la vista previa en sí, más intuitivo que
    // obligar a ir hasta el botón de arriba).
    private void OnClickMiniaturaCedula(object sender, MouseButtonEventArgs e)
        => OnVerCedulaCliente(sender, e);

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

    // Actualiza el panel de miniatura/estado de foto de cédula al seleccionar cliente y
    // después de cargar una foto nueva — mismo criterio de búsqueda que OnVerCedulaCliente
    // (CI o IDCLIE, lo que exista primero).
    private async Task ActualizarPanelFotoCedulaAsync()
    {
        var imgMini   = FindName("ImgMiniaturaCedula") as System.Windows.Controls.Image;
        var txtEstado = FindName("TxtEstadoFotoCedula") as TextBlock;
        var panel     = FindName("PanelFotoCedula") as Border;
        var btnCargar = FindName("BtnCargarFotoCedula") as Button;
        if (imgMini == null || txtEstado == null || panel == null || btnCargar == null) return;

        imgMini.Source = null;

        if (_clienteActual == null)
        {
            txtEstado.Text = "Seleccione un cliente para ver el estado de su foto de cédula.";
            txtEstado.Foreground = (System.Windows.Media.Brush)FindResource("BrCS");
            btnCargar.Visibility = Visibility.Collapsed;
            return;
        }

        byte[]? datos = null;
        try
        {
            using var conn = _db.Create();
            datos = await conn.QueryFirstOrDefaultAsync<byte[]>(
                "SELECT TOP 1 DATOS FROM FOTOS WHERE CI = @ci OR IDCLIE = @id ORDER BY IDFOTO DESC",
                new { ci = _clienteActual.Ci, id = _clienteActual.Id });
        }
        catch { /* si falla la consulta, se trata igual que "sin foto" — no bloquea el formulario */ }

        if (datos == null || datos.Length == 0)
        {
            txtEstado.Text = "Este cliente no cuenta con foto de cédula cargada. Favor actualizar pulsando este botón para cargar la foto de cédula.";
            txtEstado.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 40, 27));
            btnCargar.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(datos);
            bmp.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 140;
            bmp.EndInit();
            bmp.Freeze();
            imgMini.Source = bmp;
        }
        catch { /* miniatura corrupta — igual se informa que SÍ hay una foto cargada */ }

        // Ya hay foto cargada — el botón de carga sobra, basta con la vista previa. Solo vuelve
        // a aparecer si el cliente realmente no tiene ninguna (rama de arriba).
        txtEstado.Text = "Este cliente ya cuenta con foto de cédula cargada.";
        txtEstado.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 100, 58));
        btnCargar.Visibility = Visibility.Collapsed;
    }

    private async void OnCargarFotoCedula(object sender, RoutedEventArgs e)
    {
        if (_clienteActual == null)
        {
            MessageBox.Show("Primero seleccione un cliente.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new OpenFileDialog {
            Title = "Seleccionar foto de cédula",
            Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp|Todos|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        byte[] datos;
        try
        {
            datos = await System.IO.File.ReadAllBytesAsync(dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo leer el archivo: " + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            using var conn = _db.Create();
            var ci = _clienteActual.Ci;
            var id = _clienteActual.Id;
            var existe = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM FOTOS WHERE CI = @ci OR IDCLIE = @id", new { ci, id });
            if (existe > 0)
                await conn.ExecuteAsync(
                    "UPDATE FOTOS SET DATOS = @datos WHERE CI = @ci OR IDCLIE = @id",
                    new { datos, ci, id });
            else
                await conn.ExecuteAsync(
                    "INSERT INTO FOTOS (IDCLIE, CI, DATOS) VALUES (@id, @ci, @datos)",
                    new { id, ci, datos });
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo guardar la foto: " + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        await ActualizarPanelFotoCedulaAsync();
        MessageBox.Show("Foto de cédula guardada correctamente.", "Éxito",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── GARANTE ───────────────────────────────────────────────────────────────

    private void OnBuscarGarante(object sender, RoutedEventArgs e)
    {
        var win = new BuscadorPersonaWindow(_db) { Owner = this };
        win.Closed += (_, _) =>
        {
            if (win.PersonaSeleccionada == null) return;
            _garanteActual = win.PersonaSeleccionada;
            RellenarCamposGarante(_garanteActual);
        };
        win.Show();
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
        // Mismo problema/mitigación que MainWindow.AbrirVentana: WPF a veces minimiza la
        // ventana Owner al cerrar esta hija (reportado: se minimizaba TODO el programa al
        // tocar "Cerrar" acá) — se restaura explícitamente en vez de confiar en el default.
        win.Closed += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();
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
        // La búsqueda es contra la tabla REFERENCIAS (independiente de CLIENTES) — un
        // catálogo reutilizable de personas de referencia. Antes no había forma de agregar una
        // nueva desde acá: si la persona no estaba cargada, había que tipear todo a mano en
        // los 4 campos fijos de la solicitud sin quedar guardada para la próxima vez.
        var btnNueva = new Button { Content = "➕ Nueva referencia", Height = 26, Padding = new Thickness(10,0,10,0), Margin = new Thickness(0,0,6,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200,80,0)),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
        var btnAceptar  = new Button { Content = "Aceptar",  Width = 80, Height = 26, Margin = new Thickness(0,0,6,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,140,60)),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
        var btnCancelar = new Button { Content = "Cerrar", Width = 80, Height = 26 };
        btnSp.Children.Add(btnNueva);
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
        btnNueva.Click      += async (_, _) =>
        {
            var nueva = await CrearReferenciaAsync(win);
            if (nueva == null) return;
            lista.Insert(0, nueva);
            listaFiltrada.Insert(0, nueva);
            dg.SelectedItem = nueva;
            dg.ScrollIntoView(nueva);
        };

        root.Children.Add(dg);
        win.Content = root;
        win.ShowDialog();
        return seleccionada;
    }

    // Formulario chico para dar de alta una persona nueva en el catálogo REFERENCIAS —
    // NOMBRE y TELEFONO son obligatorios en la tabla (CI y TRABAJO opcionales). Owner es la
    // ventana de "Lista de referencias" (no la solicitud de crédito), para que quede arriba
    // de esa lista y no se pierda detrás del formulario principal.
    private async Task<ReferenciaItem?> CrearReferenciaAsync(Window ownerLista)
    {
        var win = new Window
        {
            Title = "Nueva referencia", Width = 380, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = ownerLista, Background = System.Windows.Media.Brushes.White
        };
        // Misma mitigación que "Lista de referencias" — sin esto, cerrar este formulario
        // (3er nivel: MainWindow → VentaCreditoWindow → Lista → Nueva referencia) minimizaba
        // toda la cadena de ventanas hasta MainWindow.
        win.Closed += (_, _) =>
        {
            if (ownerLista.WindowState == WindowState.Minimized)
                ownerLista.WindowState = WindowState.Normal;
            ownerLista.Activate();
        };

        var root = new DockPanel();
        var hdr = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 80, 0)),
            Padding = new Thickness(14, 12, 14, 12)
        };
        hdr.Child = new TextBlock { Text = "NUEVA REFERENCIA", Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 13 };
        DockPanel.SetDock(hdr, Dock.Top);
        root.Children.Add(hdr);

        var form = new StackPanel { Margin = new Thickness(16) };
        TextBox AddCampo(string label, bool obligatorio)
        {
            form.Children.Add(new TextBlock { Text = label + (obligatorio ? " *" : ""), FontSize = 10,
                FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 8, 0, 2) });
            var tb = new TextBox { Padding = new Thickness(6), FontSize = 12 };
            form.Children.Add(tb);
            return tb;
        }
        var txtNombre  = AddCampo("NOMBRE Y APELLIDO", true);
        var txtCi      = AddCampo("C.I.", false);
        var txtTel     = AddCampo("TELÉFONO", true);
        var txtTrabajo = AddCampo("LUGAR DE TRABAJO", false);
        root.Children.Add(form);

        ReferenciaItem? creada = null;

        var pie = new Border { Padding = new Thickness(10), BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220)) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnGuardar = new Button { Content = "Guardar", Width = 90, Height = 28, Margin = new Thickness(0, 0, 6, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 140, 60)),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
        var btnCancelar = new Button { Content = "Cancelar", Width = 90, Height = 28 };
        pieSp.Children.Add(btnGuardar);
        pieSp.Children.Add(btnCancelar);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom);
        root.Children.Add(pie);

        btnCancelar.Click += (_, _) => win.Close();
        btnGuardar.Click += async (_, _) =>
        {
            var nombre = txtNombre.Text.Trim();
            var tel    = txtTel.Text.Trim();
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(tel))
            {
                MessageBox.Show("Nombre y teléfono son obligatorios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                using var conn = _db.Create();
                var nuevoId = await conn.QuerySingleAsync<int>(
                    "INSERT INTO REFERENCIAS (CI, NOMBRE, TELEFONO, TRABAJO, FECHA_REGISTRO) " +
                    "VALUES (@Ci, @Nombre, @Telefono, @Trabajo, GETDATE()); " +
                    "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { Ci = txtCi.Text.Trim(), Nombre = nombre, Telefono = tel, Trabajo = txtTrabajo.Text.Trim() });
                creada = new ReferenciaItem(nuevoId, txtCi.Text.Trim(), nombre, tel, txtTrabajo.Text.Trim());
                win.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        win.Content = root;
        win.Loaded += (_, _) => txtNombre.Focus();
        win.ShowDialog();
        return creada;
    }

    // ── ARTÍCULOS ─────────────────────────────────────────────────────────────

    private async void OnBuscarArticulo(object sender, RoutedEventArgs e)
    {
        // Si no hay local: preguntar y abrir selector antes de continuar
        if (_idLocalForm == 0)
        {
            var resp = MessageBox.Show(
                "Aún no se ha seleccionado un local.\n\n¿Desea seleccionar el local ahora?",
                "Local no asignado",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (resp != MessageBoxResult.Yes) return;

            await AbrirSelectorLocalAsync(obligatorio: false);
            if (_idLocalForm == 0) return;
        }

        // Siempre limpiar el artículo actual antes de abrir el modal
        LimpiarLineaArticulo();

        // Abrir VerArticulosWindow en modo selector, filtrado al local activo
        var visor = new VerArticulosWindow(_idLocalForm, owner: this);
        if (visor.ShowDialog() == true && visor.ArticuloSeleccionado is VisorRow row)
        {
            await SeleccionarArticuloAsync(new ArticuloConPrecio {
                Id          = row.IdArt,
                Ca          = row.Codigo,
                D           = row.Desc,
                PventaLocal = row.PVenta,
                Pc          = row.Pc,
                Iva         = 0,
                MaxCuota    = row.MaxCuota,
                // row.Stocks (array por local) siempre viene vacío desde el modo selector del
                // modal (VerArticulosWindow.PagToRow no lo completa, solo llena TotalStockOverride) —
                // usar row.Stocks[0] daba Stock=0 siempre, y como la validación de cantidad en
                // OnAgregarArticulo es "Stock > 0 && cantidad > Stock", nunca se disparaba: se
                // podía cargar cualquier cantidad sin bloquear por falta de stock (bug real
                // reportado, comparado contra el sistema viejo que sí valida esto).
                Stock       = row.TotalStock,
                PctDescuentoPlan = row.PctDescuentoPlan,
                EntregaPlan      = row.EntregaPlan,
                PctRecargoPlan   = row.PctRecargoPlan,
                CantCuotasPlan   = row.CantCuotasPlan,
                ValorNetoPlan    = row.ValorNetoPlan,
            });
        }
    }

    private async void OnCodigoArticuloKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await BuscarArticuloPorCodigoAsync((FindName("TxtCodigoArticulo") as TextBox)?.Text.Trim() ?? "");
    }

    private async Task BuscarArticuloPorCodigoAsync(string codigo)
    {
        if (string.IsNullOrEmpty(codigo)) return;
        var art = await _artRepo.BuscarPorCodigoAsync(codigo);
        if (art == null)
        {
            MessageBox.Show("No se encontró artículo con ese código.", "Buscar",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var precio = _idLocalForm > 0 ? await _artRepo.ObtenerPrecioLocalAsync(art.Id, _idLocalForm) : null;
        await SeleccionarArticuloAsync(new ArticuloConPrecio
        {
            Id          = art.Id, Ca = art.Ca, D = art.D,
            PventaLocal = precio?.Pventa ?? 0,
            Pc          = precio?.Pc     ?? 0,
            Iva         = art.Iva,
            MaxCuota    = art.Maxcuota,
            Stock       = precio?.S      ?? 0,
        });
    }

    private async Task SeleccionarArticuloAsync(ArticuloConPrecio art)
    {
        _articuloActual = art;
        HabilitarEdicionArticulo(true);
        if (FindName("TxtCodigoArticulo") is TextBox txCod) txCod.Text = art.Ca;
        TxtDescArticulo.Text = art.D;

        // PRECIO es siempre el precio de lista del artículo, nunca se pisa con el resultado
        // del plan de pagos — % Descuento y % Recargo tienen sus propios campos (fila 2) y son
        // los que ajustan el precio real de venta de la línea (ver RecalcularLinea/
        // OnAgregarArticulo). Bug real detectado: antes PRECIO mostraba el valor YA con
        // descuento+recargo aplicado, quedando idéntico a VALOR FINAL cuando no había entrega,
        // sin distinguir "precio de lista" de "resultado de la operación".
        TxtPrecioArticulo.Text = art.PventaLocal.ToString("N0").Replace(",", ".");

        // Si el artículo viene del simulador "PLAN DE PAGOS" del modal (CantCuotasPlan > 0 —
        // ahí siempre se fuerza a mínimo 1), se respeta lo que el usuario configuró ahí en vez
        // de resetear todo a entrega 0 / 1 cuota / sin descuento. Bug real reportado: cargar un
        // % de Descuento en el modal no se reflejaba al ingresar el artículo a la venta.
        var vieneDePlan = art.CantCuotasPlan > 0;
        // Si viene del modal (simulador "Plan de Pagos"), el % Recargo que el cajero ya haya
        // subido a mano ahí (art.PctRecargoPlan, mayor al sugerido de la escala) debe respetarse
        // acá también — bug real reportado: se pisaba con el valor fijo de la escala apenas se
        // abría la venta, perdiendo lo que el cajero acababa de configurar en el simulador. Se
        // marca como "editado manualmente" cuando difiere del sugerido para esas cuotas, así
        // RecalcularLinea (más abajo) no lo sobrescribe.
        _recargoEditadoManualmente = vieneDePlan && art.PctRecargoPlan > PctRecargoSugerido(art.CantCuotasPlan);
        // Si viene del modal, la Entrega ya quedó fijada ahí (autocompletada, nunca editable a
        // mano) — no se vuelve a recalcular acá. Si no (código directo), arranca en modo
        // autocompletado.
        _entregaArticuloEditadaManualmente = vieneDePlan;

        // Bug real detectado: las asignaciones de .Text de acá abajo disparan OnArticuloCalcular,
        // que solo respeta "_recalculandoLinea" como señal de "esto es autocompletado, no marques
        // el flag de edición manual" — pero _recalculandoLinea solo es true DENTRO de
        // RecalcularLinea(), no acá. Sin este guard, la asignación de TxtPctRecargoArticulo más
        // abajo pisaba el flag recién puesto en la línea anterior, dejando el autocompletado
        // desactivado desde la primera selección del artículo.
        _recalculandoLinea = true;
        try
        {
            TxtEntregaArticulo.Text       = (vieneDePlan ? art.EntregaPlan : 0).ToString("N0").Replace(",", ".");
            TxtPctDescuentoArticulo.Text  = (vieneDePlan ? art.PctDescuentoPlan : 0).ToString("N0");
            TxtCantidad.Text              = "1";

            // Ajustar el input al máximo de cuotas permitido por el artículo — ANTES de fijar
            // % Recargo, que necesita conocer la cantidad de cuotas EFECTIVA (ya ajustada al
            // máximo) para elegir la tasa de la escala correspondiente.
            if (FindName("CboCuotas") is TextBox txCuotas)
            {
                if (vieneDePlan) txCuotas.Text = art.CantCuotasPlan.ToString();
                AjustarCuotasAMaximo(txCuotas, art.MaxCuota);
            }
            int.TryParse((FindName("CboCuotas") as TextBox)?.Text, out var cuotasEfectivas);
            if (cuotasEfectivas <= 0) cuotasEfectivas = 1;
            TxtPctRecargoArticulo.Text = (vieneDePlan ? art.PctRecargoPlan : PctRecargoSugerido(cuotasEfectivas)).ToString("N1");
            ActualizarEdicionRecargo(cuotasEfectivas);
        }
        finally { _recalculandoLinea = false; }

        if (FindName("LblCuotas") is TextBlock lblC)
            lblC.Text = art.MaxCuota > 0 ? $"CUOTAS (máx. {art.MaxCuota})" : "CUOTAS";
        if (FindName("LblCantidad") is TextBlock lblCant)
            lblCant.Text = art.Stock > 0 ? $"CANT. (disp. {(int)art.Stock})" : "CANT.";

        RecalcularLinea();
        await Task.CompletedTask;
    }

    // Fija el valor del input al máximo permitido si lo supera
    private static void AjustarCuotasAMaximo(TextBox tx, int maxCuota)
    {
        if (maxCuota <= 0) return;
        if (!int.TryParse(tx.Text, out var v) || v <= 0 || v > maxCuota)
            tx.Text = maxCuota.ToString();
    }

    // Restaura el input a un valor por defecto cuando no hay artículo
    private static void RestaurarCuotas(TextBox tx)
    {
        tx.Text = "6";
    }

    private void OnArticuloCalcular(object sender, TextChangedEventArgs e)
    {
        // ENTREGA y % RECARGO marcan su propio flag de "edición manual" — cambios en Precio/%
        // Descuento (que comparten este mismo handler) no cuentan como que el cajero los tocó, y
        // no deben desactivar el autocompletado (ver RecalcularLinea).
        if (!_recalculandoLinea)
        {
            if (sender == FindName("TxtEntregaArticulo")) _entregaArticuloEditadaManualmente = true;
            if (sender == FindName("TxtPctRecargoArticulo") && ((TextBox)sender).IsEnabled) _recargoEditadoManualmente = true;
        }
        RecalcularLinea();
    }

    // El piso de 4,2% se corrige en el TEXTO del campo recién acá, al perder el foco — NO en cada
    // tecla (eso trababa poder borrar y reescribir un valor mayor, bug real reportado). Mientras
    // se edita, RecalcularLinea ya usa el piso para los totales en pantalla sin tocar el texto;
    // esto solo ajusta lo que queda escrito una vez que el cajero termina de tipear.
    private void OnPctRecargoLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tx || !tx.IsEnabled) return;
        if (!TryParsePorcentaje(tx.Text, out var pctRec) || pctRec < 4.2m)
        {
            tx.Text = 4.2m.ToString("N1");
            RecalcularLinea();
        }
    }

    private void OnCantidadChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tx) return;
        var soloDigitos = new string(tx.Text.Where(char.IsDigit).ToArray());
        if (soloDigitos != tx.Text || string.IsNullOrEmpty(soloDigitos) || soloDigitos == "0")
        {
            tx.Text = string.IsNullOrEmpty(soloDigitos) || soloDigitos == "0" ? "1" : soloDigitos;
            tx.CaretIndex = tx.Text.Length;
            return;
        }
        // No superar el stock disponible en el local
        if (_articuloActual?.Stock > 0
            && decimal.TryParse(soloDigitos, out var cant)
            && cant > _articuloActual.Stock)
        {
            tx.Text = ((int)_articuloActual.Stock).ToString();
            tx.CaretIndex = tx.Text.Length;
        }
    }

    private void OnCuotasChanged(object sender, TextChangedEventArgs e)
    {
        if (_articuloActual != null && _articuloActual.MaxCuota > 0
            && FindName("CboCuotas") is TextBox tx
            && int.TryParse(tx.Text, out var v) && v > _articuloActual.MaxCuota)
        {
            tx.Text = _articuloActual.MaxCuota.ToString();
            tx.CaretIndex = tx.Text.Length;
            return;
        }
        RecalcularLinea();
    }

    // Precio de venta con el % Descuento ya aplicado (SIN recargo — el recargo se calcula
    // sobre el saldo tras restar la entrega, no sobre el precio, ver RecalcularLinea). Este es
    // el valor que se guarda como LineaDetalle.Pv / precio real de la línea.
    private decimal CalcularPrecioConDescuento()
    {
        var precio = ParseMiles((FindName("TxtPrecioArticulo") as TextBox)?.Text ?? "0");
        decimal.TryParse((FindName("TxtPctDescuentoArticulo") as TextBox)?.Text, out var pctDesc);
        return precio - Math.Round(precio * pctDesc / 100, 0);
    }

    private void RecalcularLinea()
    {
        if (_recalculandoLinea) return;
        _recalculandoLinea = true;
        try
        {
            var txEntrega = FindName("TxtEntregaArticulo") as TextBox;
            var txCuotas  = FindName("CboCuotas")          as TextBox;
            var txCosto   = FindName("TxtCostoMensual")    as TextBox;
            var txFinal   = FindName("TxtValorFinal")      as TextBox;
            if (txEntrega == null || txCuotas == null || txCosto == null || txFinal == null) return;

            var precioConDescuento = CalcularPrecioConDescuento();
            int.TryParse(txCuotas.Text, out var cuotas);
            if (cuotas <= 0) cuotas = 6;

            // Entrega sugerida = precio con descuento / (Cant. Cuotas + 1) — reparte el precio en
            // partes iguales entre la entrega y cada cuota, NO solo precio/Cuotas. Confirmado por
            // el cliente con ejemplo real: Precio 60.000, 1 cuota → Entrega 30.000 y Cuota 30.000
            // (60.000/2), mitad y mitad — precio/Cuotas daría 60.000 (el 100%), dejando la cuota
            // en 0. Esta misma fórmula es también el umbral mínimo de Entrega para "sin recargo"
            // en 1-2 cuotas (ver más abajo) — es la misma cuenta.
            var entregaSugerida = Math.Round(precioConDescuento / (cuotas + 1), 0);
            if (!_entregaArticuloEditadaManualmente)
                txEntrega.Text = entregaSugerida.ToString("N0").Replace(",", ".");

            var entrega = ParseMiles(txEntrega.Text);

            ActualizarEdicionRecargo(cuotas);
            // En 1-2 cuotas el % Recargo sigue sin ser editable (0% fijo, o 4,2% automático si la
            // Entrega no alcanza el umbral mínimo — ver comentario original más abajo). Si el
            // cajero había editado el recargo a mano en 3+ cuotas y después baja a 1-2, esa
            // edición ya no aplica: se descarta el flag para volver al comportamiento fijo.
            if (cuotas <= 2) _recargoEditadoManualmente = false;

            // % Recargo es editable por el cajero SOLO en 3-10 y 11+ cuotas (pedido explícito:
            // el vendedor puede subir el recargo para ganar más), pero nunca por debajo de 4,2%
            // en esos dos rangos — ni siquiera en 11+ cuotas, que sugiere 4% (por debajo del
            // piso). En 1-2 cuotas el "sin recargo" no es automático: depende de que la Entrega
            // alcance el umbral mínimo (entregaSugerida, arriba). Si el cajero tipeó una Entrega
            // menor a mano, se aplica igual el 4,2% aunque sean 1-2 cuotas.
            var pctRecSugerido = PctRecargoSugerido(cuotas);
            if (cuotas <= 2 && pctRecSugerido == 0m && entrega < entregaSugerida) pctRecSugerido = 4.2m;
            var txPctRec = FindName("TxtPctRecargoArticulo") as TextBox;
            if (txPctRec != null && !(cuotas >= 3 && _recargoEditadoManualmente))
                txPctRec.Text = pctRecSugerido.ToString("N1");
            TryParsePorcentaje(txPctRec?.Text, out var pctRec);
            // El piso de 4,2% NO se fuerza acá sobre el TEXTO del campo — mientras el cajero
            // está tipeando (ej. borra "6" para escribir "8,5"), el texto pasa por estados
            // intermedios válidos (vacío, "8") que son momentáneamente menores a 4,2 sin que eso
            // signifique que el usuario quiso dejarlo así. Forzar el piso en cada tecla trababa
            // la edición (bug real reportado: no se podía borrar y reescribir el recargo). El
            // piso se aplica solo al perder el foco (OnPctRecargoLostFocus) y se revalida antes
            // de guardar (OnAgregarArticulo) — acá alcanza con no dejar que un valor
            // momentáneamente bajo infle el recargo mostrado por debajo del piso real.
            if (cuotas >= 3 && pctRec < 4.2m) pctRec = 4.2m;

            // saldo = precio con descuento menos la entrega — el capital que queda financiado.
            var saldo = Math.Max(0, precioConDescuento - entrega);
            // % Recargo es una tasa POR CUOTA (interés simple por período financiado), no un
            // cargo único — se multiplica por la cantidad de cuotas antes de aplicarse al saldo.
            // Escala: 1-2 cuotas 0%, 3-10 cuotas 4,2%, 11+ cuotas 4% (ver PctRecargoSugerido).
            var recargo    = Math.Round(saldo * pctRec / 100 * cuotas, 0);
            var totalConRecargo = saldo + recargo;
            var costoMens  = cuotas > 0 ? Math.Ceiling(totalConRecargo / cuotas) : 0;
            // VALOR FINAL = saldo financiado + recargo (mismo criterio que "Saldo"/"Valor Final"
            // del simulador del modal) — NO incluye la entrega, que ya se pagó aparte.
            var valorFinal = totalConRecargo;

            txCosto.Text = costoMens.ToString("N0").Replace(",", ".");
            txFinal.Text = valorFinal.ToString("N0").Replace(",", ".");
        }
        finally { _recalculandoLinea = false; }
    }

    private void OnAgregarArticulo(object sender, RoutedEventArgs e)
    {
        if (_articuloActual == null)
        {
            MessageBox.Show("Primero busque y seleccione un artículo.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var txEntrega = FindName("TxtEntregaArticulo") as TextBox;
        var txCantidad= FindName("TxtCantidad")        as TextBox;
        var txCosto   = FindName("TxtCostoMensual")    as TextBox;
        var txFinal   = FindName("TxtValorFinal")      as TextBox;

        // precio = precio de lista con % Descuento aplicado (SIN recargo), es lo que se guarda
        // en LineaDetalle.Pv y contra lo que se valida la entrega. El % Recargo no forma parte
        // del precio unitario — es un interés por cuota que solo se refleja en
        // CostoMensual/ValorFinal (ver RecalcularLinea).
        var precio    = CalcularPrecioConDescuento();
        var entrega   = ParseMiles(txEntrega?.Text ?? "0");
        int.TryParse((FindName("CboCuotas") as TextBox)?.Text ?? "0", out var cuotas);

        if (precio <= 0)
        {
            MessageBox.Show("El artículo no tiene precio asignado para este local.\nVerifique el local seleccionado.",
                "Precio inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (entrega >= precio)
        {
            MessageBox.Show($"La entrega (Gs. {entrega:N0}) no puede ser igual o mayor al precio (Gs. {precio:N0}).",
                "Entrega inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            txEntrega?.Focus();
            return;
        }
        if (cuotas <= 0)
        {
            MessageBox.Show("Seleccione la cantidad de cuotas.", "Cuotas requeridas",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!decimal.TryParse(txCantidad?.Text ?? "", out var cantidad) || cantidad <= 0)
        {
            MessageBox.Show("Ingrese una cantidad válida.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_articuloActual.Stock > 0 && cantidad > _articuloActual.Stock)
        {
            MessageBox.Show(
                $"Stock insuficiente en el local seleccionado.\n" +
                $"Disponible: {_articuloActual.Stock:N0} — Solicitado: {cantidad:N0}",
                "Stock insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
            txCantidad?.Focus();
            return;
        }

        // Revalidación final del piso de 4,2% en % Recargo — el LostFocus del campo ya lo
        // corrige al salir del TextBox, pero si el cajero hace click en "Ingresar" sin que el
        // campo llegue a perder el foco, esto asegura que el texto quede corregido (Costo
        // Mensual/Valor Final ya se calculan con el piso aplicado igual, ver RecalcularLinea;
        // esto es para que el % que se ve en pantalla no quede a medio escribir).
        if (FindName("TxtPctRecargoArticulo") is TextBox txRecFinal && txRecFinal.IsEnabled)
            OnPctRecargoLostFocus(txRecFinal, e);

        var costoMens = ParseMiles(txCosto?.Text ?? "0");
        var valFinal  = ParseMiles(txFinal?.Text  ?? "0");

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

    private void OnGridDetalleKeyDown(object sender, KeyEventArgs e)
    {
        if ((e.Key == Key.Delete || e.Key == Key.Back) && GridDetalle.SelectedItem is LineaDetalle linea)
        {
            _carrito.Remove(linea);
            RefrescarCarrito();
            e.Handled = true;
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
        _articuloActual = null;
        var txCod = FindName("TxtCodigoArticulo") as TextBox;
        if (txCod != null) txCod.Text = "";
        TxtDescArticulo.Text    = "";
        TxtPrecioArticulo.Text  = "";
        TxtEntregaArticulo.Text = "0";
        if (FindName("TxtPctDescuentoArticulo") is TextBox txDesc) txDesc.Text = "0";
        if (FindName("TxtPctRecargoArticulo")   is TextBox txRec)  txRec.Text  = "0";
        TxtCostoMensual.Text    = "";
        TxtValorFinal.Text      = "";
        TxtCantidad.Text        = "1";
        // Restaurar todas las opciones de cuotas
        if (FindName("CboCuotas") is TextBox txC) RestaurarCuotas(txC);
        if (FindName("LblCuotas")   is TextBlock lblC2)    lblC2.Text    = "CUOTAS";
        if (FindName("LblCantidad") is TextBlock lblCant2) lblCant2.Text = "CANT.";
        _entregaArticuloEditadaManualmente = false;
        _recargoEditadoManualmente = false;
        HabilitarEdicionArticulo(false);
        txCod?.Focus();
    }

    // Entrega/Cuotas/%Descuento/%Recargo solo se editan MIENTRAS se está cargando un artículo
    // (entre buscarlo y pulsar Ingresar) — igual que el sistema viejo, donde esa barra queda
    // deshabilitada apenas el artículo pasa a la grilla, dejando editable únicamente Cantidad.
    private void HabilitarEdicionArticulo(bool habilitar)
    {
        if (FindName("TxtEntregaArticulo")      is TextBox txEnt)  txEnt.IsEnabled  = habilitar;
        if (FindName("CboCuotas")               is TextBox txCuo)  txCuo.IsEnabled  = habilitar;
        if (FindName("TxtPctDescuentoArticulo")  is TextBox txDes) txDes.IsEnabled  = habilitar;
        // TxtPctRecargoArticulo se maneja aparte (ver ActualizarEdicionRecargo): solo es editable
        // en 3-10 y 11+ cuotas (pedido explícito del cliente, para que el vendedor pueda subir el
        // recargo y ganar más), nunca en 1-2 cuotas (sigue en 0% fijo, sin cambios).
        if (!habilitar && FindName("TxtPctRecargoArticulo") is TextBox txRecOff) txRecOff.IsEnabled = false;
    }

    // % Recargo es editable únicamente cuando hay 3+ cuotas (rangos 3-10 y 11+) — en 1-2 cuotas
    // sigue siendo 0% fijo, no editable, sin piso. Se llama cada vez que cambia la cantidad de
    // cuotas mientras se está cargando el artículo (HabilitarEdicionArticulo ya decide si la
    // barra completa está activa; esto decide específicamente el caso de Recargo dentro de ella).
    private void ActualizarEdicionRecargo(int cuotas)
    {
        if (FindName("TxtPctRecargoArticulo") is not TextBox txRec) return;
        var barraActiva = (FindName("TxtEntregaArticulo") as TextBox)?.IsEnabled ?? false;
        txRec.IsEnabled = barraActiva && cuotas >= 3;
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

        // Referencias Personales 1 y 2 obligatorias — ver ValidarReferenciasCompletas (mismo
        // punto de verdad que ValidarTabActual, por si el cajero llegó a "Mercaderías" antes de
        // que existiera esta validación en el tab de Referencias, o guardó sin pasar por ahí).
        if (!ValidarReferenciasCompletas(out var msgRefConfirmar))
        {
            MessageBox.Show(
                msgRefConfirmar + "\n\nComplete los datos en la pestaña Referencias antes de continuar.",
                "Referencias incompletas", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Garante obligatorio para clientes nuevos — ver EvaluarRequiereGaranteAsync para el
        // detalle completo de la regla (misma lógica que ya actualiza el card informativo de
        // la pestaña Cliente, acá solo se re-evalúa por si cambió el cliente seleccionado sin
        // que el card llegara a refrescarse antes de tocar "Siguiente").
        if (_garanteActual == null)
        {
            var (requiereGarante, motivo) = await EvaluarRequiereGaranteAsync();
            if (requiereGarante)
            {
                MessageBox.Show(
                    $"Este cliente requiere garante: {motivo}.\n\n" +
                    "Complete los datos del garante antes de continuar.",
                    "Garante requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var txVend = FindName("TxtVendedor") as TextBox;
        if (txVend == null || string.IsNullOrWhiteSpace(txVend.Text))
        {
            var resp = MessageBox.Show(
                "No ha seleccionado un vendedor.\n¿Desea seleccionar un vendedor ahora?",
                "Vendedor no asignado", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (resp == MessageBoxResult.Yes)
                await AbrirSelectorVendedorAsync();
            if (string.IsNullOrWhiteSpace(txVend?.Text)) return;
        }

        if (!MostrarModalConfirmacion()) return;

        var session = SessionService.Instance;
        if (session.UsuarioActual == null || session.LocalActual == null) return;

        if (_idLocalForm == 0)
        {
            MessageBox.Show("Debe seleccionar un Local antes de guardar.", "Local requerido",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnGuardar.IsEnabled = false;
        try
        {
            // El número real se calcula dentro de GuardarSolicitudAsync (a partir del
            // NUMERO más alto ya guardado en CAB_SOL_SALES, bajo TABLOCKX) — no acá antes
            // del INSERT, porque ObtenerNumeroSolicitudAsync/CONTADORES.SOL puede quedar
            // desincronizado del valor real (ej. tras restaurar un backup) y repetir un
            // número ya usado por otra venta. Este nSol solo queda para IdSolicitud, un
            // campo legado de DET_SOL_SALES que ya no se usa en el INSERT nuevo.
            var nSol = 0;

            var total      = ParseMiles(TxtTotal.Text);
            var totalEnt   = _carrito.Sum(x => x.EntregaLinea * x.Cantidad);
            // CANTCUOTAS de CABECERA se toma del carrito (lo que el cajero realmente confirmó
            // por artículo), NUNCA del TextBox CboCuotas — ese campo es solo el editor transitorio
            // de la línea en curso y LimpiarLineaArticulo() lo resetea a "6" apenas se agrega un
            // artículo al carrito. Leerlo acá (como se hacía antes) guardaba "6" en la cabecera
            // pese a que el cajero eligió, por ejemplo, 3 cuotas — bug real confirmado con datos:
            // CAB_SOL_SALES.CANTCUOTAS=6 pero DET_SOL_SALES.CANTCUOTAS=3 para la misma solicitud.
            var cuotas = _carrito.Count > 0 ? _carrito[^1].CuotasLinea : 6;
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

            // ESTADO_INFORCONF ya no se captura en esta pantalla (la validación de garante usa
            // solo historial de créditos, ver EvaluarRequiereGaranteAsync) — se guarda vacío;
            // la columna se mantiene en CAB_SOL_SALES por compatibilidad con el SP existente.
            var estadoInforconfAGuardar = "";

            // AGENTE=1 crea CAB_SOL_SALES (genera IDCABSOL y NUMERO reales, ver
            // GuardarSolicitudAsync), AGENTE>1 agrega solo DET_SOL_SALES reutilizando ambos.
            long idCabSolGenerado = 0;
            var numeroGenerado = "";
            for (int i = 0; i < _carrito.Count; i++)
            {
                var linea     = _carrito[i];
                var esPrimero = i == 0;

                var prm = new SolicitudParams(
                    Agente:         esPrimero ? 1 : 2,
                    IdCabSol:       esPrimero ? 0 : idCabSolGenerado,
                    Numero:         "",
                    IdLocal:        _idLocalForm,
                    IdUsuario:      _idVendedorSeleccionado ?? session.UsuarioActual.IdUsuario,
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
                    Subtotal:        linea.Subtotal,
                    EstadoInforconf: estadoInforconfAGuardar);

                var (idCabSolResultado, numeroResultado) = await _ventaRepo.GuardarSolicitudAsync(prm);
                if (esPrimero)
                {
                    idCabSolGenerado = idCabSolResultado;
                    numeroGenerado   = numeroResultado;
                }
            }

            MessageBox.Show(
                $"Solicitud guardada.\nNúmero: {numeroGenerado}\n\nPendiente de aprobación.",
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
        TxtClienteCondicion.Text = "";
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

    private void OnEliminarFila(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LineaDetalle linea)
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

    // ── Referencias de navegación (resueltas en Loaded) ──────────────────────
    private TabControl?   _mainTabs;
    private Button?       _btnAnterior;
    private Button?       _btnSiguiente;
    private TextBlock?    _txtPasoLabel;
    private System.Windows.Shapes.Ellipse?[] _navDots = Array.Empty<System.Windows.Shapes.Ellipse?>();

    private void InicializarNavegacion()
    {
        _mainTabs     = FindName("MainTabs")     as TabControl;
        _btnAnterior  = FindName("BtnAnterior")  as Button;
        _btnSiguiente = FindName("BtnSiguiente") as Button;
        _txtPasoLabel = FindName("TxtPasoLabel") as TextBlock;
        _navDots      = new[]
        {
            FindName("Dot0") as System.Windows.Shapes.Ellipse,
            FindName("Dot1") as System.Windows.Shapes.Ellipse,
            FindName("Dot2") as System.Windows.Shapes.Ellipse,
            FindName("Dot3") as System.Windows.Shapes.Ellipse,
        };
        if (_btnSiguiente != null)
            _btnSiguiente.Click += OnTabSiguiente;
        ActualizarNavegacion();
    }

    private void ActualizarNavegacion()
    {
        if (_mainTabs == null) return;
        int idx   = _mainTabs.SelectedIndex;
        int total = _mainTabs.Items.Count;

        if (_txtPasoLabel != null)
            _txtPasoLabel.Text = $"Paso {idx + 1} de {total}";

        for (int i = 0; i < _navDots.Length; i++)
        {
            if (_navDots[i] == null) continue;
            _navDots[i]!.Fill = i == idx
                ? System.Windows.Media.Brushes.White
                : new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromRgb(58, 90, 114));
        }

        if (_btnAnterior != null)
        {
            _btnAnterior.IsEnabled = idx > 0;
            _btnAnterior.Opacity   = idx > 0 ? 1.0 : 0.4;
        }

        bool esUltimo = idx == total - 1;
        if (_btnSiguiente != null)
        {
            _btnSiguiente.Content = esUltimo ? "💾 Guardar" : "Siguiente →";
            _btnSiguiente.Background = new System.Windows.Media.SolidColorBrush(
                esUltimo
                    ? System.Windows.Media.Color.FromRgb(30, 110, 66)
                    : System.Windows.Media.Color.FromRgb(31, 119, 180));
            _btnSiguiente.Click -= OnTabSiguiente;
            _btnSiguiente.Click -= OnConfirmarVenta;
            _btnSiguiente.Click += esUltimo ? OnConfirmarVenta : OnTabSiguiente;
        }
    }

    // ── Navegación entre tabs ─────────────────────────────────────────────────
    // Guard contra doble-click "fantasma": confirmado con diagnóstico (log con milisegundos) que
    // cuando ValidarTabActual muestra un MessageBox síncrono (ej. "C.I. vencida" Sí/No) DENTRO
    // del propio click en "Siguiente", el mouse-up del click que cerró ese diálogo (en "Sí")
    // llega 1ms después de que el método termina y "cae" sobre el mismo botón "Siguiente" que
    // queda debajo en la misma posición de pantalla — WPF lo reenvía como un SEGUNDO click real,
    // no una reentrada de código. Liberar el guard en el mismo tick (como hacía antes, en el
    // finally inmediato) no alcanza a absorberlo porque ese eco llega en el ciclo de eventos
    // siguiente, no durante la ejecución actual. Se libera con un despacho en Background — se
    // procesa después de que cualquier evento de mouse ya encolado por ese cierre de diálogo
    // termine de propagarse, así el eco queda bloqueado por el guard en vez de reprocesarse.
    private bool _validandoTabSiguiente = false;

    private async void OnTabSiguiente(object sender, RoutedEventArgs e)
    {
        if (_mainTabs == null || _validandoTabSiguiente) return;
        _validandoTabSiguiente = true;
        try
        {
            if (!await ValidarTabActual(_mainTabs.SelectedIndex)) return;
            if (_mainTabs.SelectedIndex < _mainTabs.Items.Count - 1)
                _mainTabs.SelectedIndex++;
        }
        finally
        {
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            _validandoTabSiguiente = false;
        }
    }

    // Valida el tab actual antes de avanzar — devuelve false si hay error
    private async Task<bool> ValidarTabActual(int tabIdx)
    {
        switch (tabIdx)
        {
            case 0: // Cliente
                if (_clienteActual == null)
                {
                    MessageBox.Show("Debe buscar y seleccionar un cliente antes de continuar.",
                        "Cliente requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                // Advertir CI vencida (no bloquea, solo avisa)
                var vencCITxt = (FindName("TxtClienteVencCI") as TextBox)?.Text ?? "";
                if (DateTime.TryParse(vencCITxt, out var vencCI) && vencCI < DateTime.Today)
                {
                    var r = MessageBox.Show(
                        $"La cédula del cliente venció el {vencCI:dd/MM/yyyy}.\n¿Desea continuar de todos modos?",
                        "C.I. vencida", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (r != MessageBoxResult.Yes) return false;
                }
                return true;

            case 1: // Garante — ver EvaluarRequiereGaranteAsync (misma regla que el card
                    // informativo y que OnConfirmarVenta): garante obligatorio si el cliente es
                    // nuevo, o si ninguno de sus créditos previos está culminado todavía.
                if (_garanteActual == null)
                {
                    var (requiereGarante, motivoGarante) = await EvaluarRequiereGaranteAsync();
                    if (requiereGarante)
                    {
                        MessageBox.Show(
                            $"Este cliente requiere garante: {motivoGarante}.\n\n" +
                            "Presione \"Buscar garante\" para cargarlo antes de continuar.",
                            "Garante requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
                return true;

            case 2: // Referencias
                if (!ValidarReferenciasCompletas(out var msgRef))
                {
                    MessageBox.Show(msgRef, "Referencias incompletas",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                return true;

            default:
                return true;
        }
    }

    // Referencias Personales 1 y 2 obligatorias (Nombre + Celular) para todos los clientes —
    // Lugar de trabajo queda opcional. Único punto de verdad, reutilizado por ValidarTabActual
    // (al presionar "Siguiente" desde la pestaña Referencias) y por OnConfirmarVenta (al
    // guardar), para que ambos caminos bloqueen igual si faltan datos.
    private bool ValidarReferenciasCompletas(out string mensaje)
    {
        if (string.IsNullOrWhiteSpace(TxtRef1Nom.Text) || string.IsNullOrWhiteSpace(TxtRef1Tel.Text) ||
            string.IsNullOrWhiteSpace(TxtRef2Nom.Text) || string.IsNullOrWhiteSpace(TxtRef2Tel.Text))
        {
            mensaje = "Debe completar Nombre y Celular de la Referencia Personal 1 y de la Referencia Personal 2.";
            return false;
        }
        mensaje = "";
        return true;
    }

    private void OnTabAnterior(object sender, RoutedEventArgs e)
    {
        if (_mainTabs != null && _mainTabs.SelectedIndex > 0)
            _mainTabs.SelectedIndex--;
    }

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActualizarNavegacion();
    }

    // ── Modal de confirmación ─────────────────────────────────────────────────

    private bool MostrarModalConfirmacion()
    {
        var session = SessionService.Instance;
        bool confirmado = false;

        // ── Helpers visuales ──────────────────────────────────────────────────
        System.Windows.Media.Brush BrN(byte r, byte g, byte b)
            => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));

        TextBlock Lbl(string t, bool bold = false) => new TextBlock
        {
            Text = t, FontSize = 11, Foreground = BrN(80, 80, 80),
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };
        TextBlock Val(string t, System.Windows.Media.Brush? fg = null) => new TextBlock
        {
            Text = t, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = fg ?? BrN(30, 30, 30),
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
        };
        Border Seccion(string titulo) => new Border
        {
            Background = BrN(230, 90, 0), CornerRadius = new CornerRadius(4, 4, 0, 0),
            Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 10, 0, 0),
            Child = new TextBlock { Text = titulo, Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold, FontSize = 12 }
        };
        Border Card(UIElement content) => new Border
        {
            Background = BrN(255, 252, 248), BorderBrush = BrN(220, 180, 140),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(0, 0, 4, 4),
            Padding = new Thickness(10, 8, 10, 8), Child = content
        };

        Grid Fila2(string lbl, string val, System.Windows.Media.Brush? fg = null)
        {
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l = Lbl(lbl + ":", true); Grid.SetColumn(l, 0); g.Children.Add(l);
            var v = Val(val, fg);         Grid.SetColumn(v, 1); g.Children.Add(v);
            return g;
        }

        // ── Ventana ───────────────────────────────────────────────────────────
        var win = new Window
        {
            Title = "Confirmar Solicitud de Crédito",
            Width = 700, Height = 620, MinWidth = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.CanResize,
            Background = BrN(245, 240, 235)
        };

        var root = new DockPanel();

        // ── Encabezado ────────────────────────────────────────────────────────
        var hdr = new Border
        {
            Background = BrN(200, 60, 0), Padding = new Thickness(16, 12, 16, 12)
        };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock
        {
            Text = "⚠  Confirmar Solicitud de Crédito",
            FontSize = 15, FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White
        });
        hdrSp.Children.Add(new TextBlock
        {
            Text = "Revise todos los datos antes de confirmar.",
            FontSize = 11, Foreground = BrN(255, 210, 180), Margin = new Thickness(0, 3, 0, 0)
        });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top);
        root.Children.Add(hdr);

        // ── Botones ───────────────────────────────────────────────────────────
        var footer = new Border
        {
            Background = BrN(235, 230, 225), Padding = new Thickness(12, 8, 12, 8),
            BorderBrush = BrN(200, 180, 160), BorderThickness = new Thickness(0, 1, 0, 0)
        };
        var btnBar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnConf = new Button
        {
            Content = "✔  Confirmar y Guardar", Width = 180, Height = 34, Margin = new Thickness(0, 0, 8, 0),
            Background = BrN(0, 130, 0), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 12, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnCanc = new Button
        {
            Content = "✘  Cancelar", Width = 110, Height = 34,
            Background = BrN(100, 100, 100), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 12, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        btnConf.Click += (_, _) => { confirmado = true;  win.Close(); };
        btnCanc.Click += (_, _) => { confirmado = false; win.Close(); };
        btnBar.Children.Add(btnConf);
        btnBar.Children.Add(btnCanc);
        footer.Child = btnBar;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        // ── Cuerpo scrollable ─────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(14, 4, 14, 8) };

        // Solicitud
        body.Children.Add(Seccion("📋  Solicitud"));
        var spSol = new StackPanel();
        spSol.Children.Add(Fila2("Nº Solicitud",  TxtNroSolicitud.Text));
        spSol.Children.Add(Fila2("Número",         TxtNumero.Text));
        spSol.Children.Add(Fila2("Local",          $"{TxtLocal.Text}  —  {TxtLocalNombre.Text}"));
        spSol.Children.Add(Fila2("Vendedor",        TxtVendedor.Text));
        spSol.Children.Add(Fila2("Fecha solicitud", DtpSolicitud.SelectedDate?.ToString("dd/MM/yyyy") ?? ""));
        spSol.Children.Add(Fila2("Fecha figurar",   DtpFigurar.SelectedDate?.ToString("dd/MM/yyyy") ?? ""));
        body.Children.Add(Card(spSol));

        // Cliente
        body.Children.Add(Seccion("👤  Cliente"));
        var spCli = new StackPanel();
        spCli.Children.Add(Fila2("Nombre",       _clienteActual!.Nombre, BrN(180, 60, 0)));
        spCli.Children.Add(Fila2("C.I.",          _clienteActual.Ci));
        spCli.Children.Add(Fila2("Teléfono",      _clienteActual.Telefono));
        spCli.Children.Add(Fila2("Ciudad",         _clienteActual.Ciudad));
        spCli.Children.Add(Fila2("Condición",      _clienteActual.Condicion));
        spCli.Children.Add(Fila2("Saldo actual",   _clienteActual.SaldoActivo.ToString("N0") + " Gs.", BrN(180, 0, 0)));
        spCli.Children.Add(Fila2("Crédito máx.",   _clienteActual.CredMax.ToString("N0") + " Gs."));
        body.Children.Add(Card(spCli));

        // Garante
        if (_garanteActual != null)
        {
            body.Children.Add(Seccion("🛡  Garante"));
            var spGar = new StackPanel();
            spGar.Children.Add(Fila2("Nombre",  _garanteActual.Nombre));
            spGar.Children.Add(Fila2("C.I.",     _garanteActual.Ci));
            spGar.Children.Add(Fila2("Teléfono", _garanteActual.Telefono));
            body.Children.Add(Card(spGar));
        }

        // Artículos
        body.Children.Add(Seccion("🛒  Artículos seleccionados"));
        var artGrid = new Grid();
        string[] hdrs  = { "Código", "Descripción", "Precio", "Cuotas", "Costo mens.", "Subtotal" };
        double[] wids  = { 65, 0, 80, 50, 90, 90 };  // 0 = Star
        for (int i = 0; i < hdrs.Length; i++)
            artGrid.ColumnDefinitions.Add(new ColumnDefinition
                { Width = wids[i] == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(wids[i]) });
        artGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Encabezado artículos
        var hdrRow = new Border { Background = BrN(80, 40, 10), Padding = new Thickness(0) };
        var hdrRowGrid = new Grid();
        foreach (var cd in artGrid.ColumnDefinitions)
            hdrRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = cd.Width });
        for (int i = 0; i < hdrs.Length; i++)
        {
            var tb = new TextBlock { Text = hdrs[i], Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold, FontSize = 10, Padding = new Thickness(6, 4, 4, 4) };
            Grid.SetColumn(tb, i); hdrRowGrid.Children.Add(tb);
        }
        hdrRow.Child = hdrRowGrid;

        var artSp = new StackPanel();
        artSp.Children.Add(hdrRow);

        decimal totalGs = 0;
        for (int ri = 0; ri < _carrito.Count; ri++)
        {
            var lin = _carrito[ri];
            totalGs += lin.Subtotal;
            var bg = ri % 2 == 0 ? BrN(255, 252, 248) : BrN(245, 238, 228);
            var rg = new Grid { Background = bg };
            foreach (var cd in artGrid.ColumnDefinitions)
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = cd.Width });

            string[] vals = { lin.ArticuloCodigo, lin.ArticuloNombre,
                              lin.Pv.ToString("N0"), lin.CuotasLinea.ToString(),
                              lin.CostoMensual.ToString("N0"), lin.Subtotal.ToString("N0") };
            for (int ci = 0; ci < vals.Length; ci++)
            {
                var tb = new TextBlock { Text = vals[ci], FontSize = 11, Padding = new Thickness(6, 5, 4, 5),
                    TextAlignment = ci >= 2 ? TextAlignment.Right : TextAlignment.Left,
                    TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(tb, ci); rg.Children.Add(tb);
            }
            artSp.Children.Add(new Border { Child = rg, BorderBrush = BrN(220, 200, 180), BorderThickness = new Thickness(0, 0, 0, 1) });
        }

        // Totalizador
        var totRow = new Border { Background = BrN(200, 60, 0), Padding = new Thickness(8, 6, 8, 6) };
        totRow.Child = new TextBlock
        {
            Text = $"TOTAL SOLICITUD:   {totalGs:N0} Gs.",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        artSp.Children.Add(totRow);
        body.Children.Add(Card(artSp));

        // Operador
        body.Children.Add(Seccion("🖥  Operación"));
        var spOp = new StackPanel();
        spOp.Children.Add(Fila2("Usuario",  session.UsuarioActual?.NombreUsuario ?? ""));
        spOp.Children.Add(Fila2("Local sesión", session.LocalActual?.NombreLocal ?? ""));
        body.Children.Add(Card(spOp));

        var scroll = new ScrollViewer
        {
            Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        root.Children.Add(scroll);

        win.Content = root;
        win.ShowDialog();
        return confirmado;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static decimal ParseMiles(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var digits = new string(s.Where(c => char.IsDigit(c)).ToArray());
        return decimal.TryParse(digits, out var v) ? v : 0;
    }

    // decimal.TryParse con NumberStyles.Number/Any (o sin especificar, que usa la cultura del
    // thread — es-PY, coma decimal) trata un punto como separador de MILES, no decimal: "4.8"
    // se parsea silenciosamente como 48, sin fallar — bug real reportado (el cajero tipeó "4.8"
    // con punto en el simulador y la venta terminó recibiendo un recargo completamente distinto
    // al que veía en pantalla). El cajero puede tipear con punto o coma indistintamente por
    // costumbre; esto acepta ambos de forma explícita para valores de PORCENTAJE (no montos:
    // ParseMiles ya cubre esos, donde no hay ambigüedad porque se descartan los no-dígitos).
    private static bool TryParsePorcentaje(string? s, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var normalizado = s.Trim().Replace(',', '.');
        return decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out valor);
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

    private TextBox    _txtBuscar    = null!;
    private ComboBox   _cboLocal     = null!;
    private CheckBox   _chkSoloStock = null!;
    private DataGrid   _grid         = null!;
    private TextBlock  _lblConteo    = null!;
    private Border     _panelDesglose = null!;
    private ItemsControl _listaDesglose = null!;
    private List<ArticuloBuscador> _todos = new();
    private int _idLocalSesion;

    private static readonly System.Windows.Media.SolidColorBrush _BrPrim  = new(System.Windows.Media.Color.FromRgb(14, 47, 68));    // #0E2F44
    private static readonly System.Windows.Media.SolidColorBrush _BrDark  = new(System.Windows.Media.Color.FromRgb(26, 79, 110));   // #1A4F6E
    private static readonly System.Windows.Media.SolidColorBrush _BrClaro = new(System.Windows.Media.Color.FromRgb(176, 212, 236)); // #B0D4EC
    private static readonly System.Windows.Media.SolidColorBrush _BrBorde = new(System.Windows.Media.Color.FromRgb(229,231,235));
    private static readonly System.Windows.Media.SolidColorBrush _BrAlt   = new(System.Windows.Media.Color.FromRgb(249,250,251));
    private static readonly System.Windows.Media.SolidColorBrush _BrLabel = new(System.Windows.Media.Color.FromRgb(107,114,128));

    public BuscadorArticuloWindow(int? idLocal = null)
    {
        _artRepo = App.Services.GetRequiredService<IArticuloRepository>();
        _idLocalSesion = idLocal ?? SessionService.Instance.LocalActual?.IdLocal ?? 0;
        Title  = "Buscar Artículo";
        Width  = 900; Height = 620;
        MinWidth = 700; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250,250,252));
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await CargarLocalesAsync();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // filtros
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // desglose (colapsable)
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // footer

        // ── Header azul corporativo ──────────────────────────────────────────
        var hdr = new Border { Background = _BrPrim, Padding = new Thickness(16,12,16,12) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "📦  Buscar Artículo",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0,0,0,8)
        });
        var searchBorder = new Border {
            Background = _BrDark, CornerRadius = new CornerRadius(6),
            BorderBrush  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 179, 211)), // #7FB3D3
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10,0,6,0)
        };
        var searchRow = new Grid();
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var searchIcon = new TextBlock {
            Text = "🔎", FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
            Foreground = _BrClaro, Margin = new Thickness(0,0,8,0)
        };
        Grid.SetColumn(searchIcon, 0); searchRow.Children.Add(searchIcon);

        // Contenedor con placeholder simulado (WPF no tiene placeholder nativo en TextBox) —
        // aclara que el buscador acepta tanto código como nombre/descripción, sin que el
        // usuario tenga que adivinarlo o descubrirlo por prueba y error (reportado: buscar
        // "P140" por código no encontraba el artículo real "P14O" y nadie entendía por qué).
        var searchInputArea = new Grid();
        _txtBuscar = new TextBox {
            Height = 34, MinWidth = 400, FontSize = 13,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.White,
            CaretBrush  = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var placeholder = new TextBlock {
            Text = "Buscar por código o por nombre/descripción...",
            FontSize = 13, FontStyle = FontStyles.Italic,
            Foreground = _BrClaro, Opacity = 0.75,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Margin = new Thickness(2,0,0,0)
        };
        void ActualizarPlaceholder() =>
            placeholder.Visibility = string.IsNullOrEmpty(_txtBuscar.Text) ? Visibility.Visible : Visibility.Collapsed;
        ActualizarPlaceholder();
        searchInputArea.Children.Add(placeholder);
        searchInputArea.Children.Add(_txtBuscar);

        var debounce = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        debounce.Tick += async (_, _) => { debounce.Stop(); await CargarArticulosAsync(); };
        _txtBuscar.TextChanged += (_, _) => { ActualizarPlaceholder(); debounce.Stop(); debounce.Start(); };
        _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Seleccionar(); };
        Grid.SetColumn(searchInputArea, 1); searchRow.Children.Add(searchInputArea);
        searchBorder.Child = searchRow;
        hdrSp.Children.Add(searchBorder);
        hdrSp.Children.Add(new TextBlock {
            Text = "Podés buscar por código de artículo o por nombre/descripción — con lo que sea más fácil de recordar.",
            FontSize = 10.5, Foreground = _BrClaro, Opacity = 0.85,
            Margin = new Thickness(2,6,0,0)
        });
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── Barra de filtros ─────────────────────────────────────────────────
        var filtros = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = _BrBorde, BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(14,8,14,8)
        };
        var filtrosRow = new StackPanel { Orientation = Orientation.Horizontal };
        filtrosRow.Children.Add(new TextBlock {
            Text = "Local:", FontWeight = FontWeights.SemiBold, Foreground = _BrLabel,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
        _cboLocal = new ComboBox {
            Width = 220, Padding = new Thickness(8,5,8,5), FontSize = 12.5,
            DisplayMemberPath = "Nombre", SelectedValuePath = "Id",
            VerticalAlignment = VerticalAlignment.Center
        };
        _cboLocal.SelectionChanged += async (_, _) => await CargarArticulosAsync();
        filtrosRow.Children.Add(_cboLocal);

        _chkSoloStock = new CheckBox {
            Content = "Solo con stock", FontSize = 12.5, Foreground = _BrLabel,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20,0,0,0),
            IsChecked = true
        };
        _chkSoloStock.Checked   += (_, _) => AplicarFiltroStockYOrden();
        _chkSoloStock.Unchecked += (_, _) => AplicarFiltroStockYOrden();
        filtrosRow.Children.Add(_chkSoloStock);
        filtros.Child = filtrosRow;
        Grid.SetRow(filtros, 1); root.Children.Add(filtros);

        // ── DataGrid ─────────────────────────────────────────────────────────
        var gridWrap = new Border {
            Margin = new Thickness(10,8,10,0),
            BorderBrush = _BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), ClipToBounds = true
        };
        gridWrap.Effect = new System.Windows.Media.Effects.DropShadowEffect {
            BlurRadius = 5, ShadowDepth = 1, Opacity = 0.07,
            Color = System.Windows.Media.Color.FromRgb(0,0,0)
        };
        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            RowHeight = 34, ColumnHeaderHeight = 32, FontSize = 12,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = _BrBorde,
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = _BrAlt,
            BorderThickness = new Thickness(0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            CanUserSortColumns = true // click en encabezado ordena — nativo de WPF, sin código extra
        };

        var hs = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, _BrPrim));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, System.Windows.Media.Brushes.White));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 11.5));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10,0,10,0)));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0,0,1,0)));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderBrushProperty, _BrDark));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.CursorProperty, Cursors.Hand));
        _grid.ColumnHeaderStyle = hs;

        var txs = new Style(typeof(TextBlock));
        txs.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10,0,10,0)));
        txs.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        var txsR = new Style(typeof(TextBlock));
        txsR.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10,0,10,0)));
        txsR.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        txsR.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));

        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Código", Binding = new System.Windows.Data.Binding("Ca"), Width = 100, ElementStyle = txs,
            SortMemberPath = "Ca" });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Descripción", Binding = new System.Windows.Data.Binding("D"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = txs,
            SortMemberPath = "D" });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Marca", Binding = new System.Windows.Data.Binding("MarcaNombre"),
            Width = 110, ElementStyle = txs, SortMemberPath = "MarcaNombre" });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Stock total", Binding = new System.Windows.Data.Binding("StockTotal") { StringFormat = "N0" },
            Width = 90, ElementStyle = txsR, SortMemberPath = "StockTotal" });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "P. Venta", Binding = new System.Windows.Data.Binding("Pventa") { StringFormat = "N0" },
            Width = 100, ElementStyle = txsR, SortMemberPath = "Pventa" });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "P. Contado", Binding = new System.Windows.Data.Binding("Contado") { StringFormat = "N0" },
            Width = 100, ElementStyle = txsR, SortMemberPath = "Contado" });
        _grid.MouseDoubleClick += (_, _) => Seleccionar();
        _grid.SelectionChanged += (_, _) => ActualizarDesglose();
        gridWrap.Child = _grid;
        Grid.SetRow(gridWrap, 2); root.Children.Add(gridWrap);

        // ── Panel de desglose por local (colapsado hasta que se seleccione algo) ──
        _panelDesglose = new Border {
            Margin = new Thickness(10,8,10,0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 244, 251)), // #EEF4FB
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(187, 222, 251)), // #BBDEFB
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12,8,12,8), MaxHeight = 130,
            Visibility = Visibility.Collapsed
        };
        var desgloseStack = new StackPanel();
        desgloseStack.Children.Add(new TextBlock {
            Text = "STOCK POR LOCAL", FontSize = 9.5, FontWeight = FontWeights.Bold,
            Foreground = _BrDark, Margin = new Thickness(0,0,0,6) });
        var scrollDesglose = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 95 };
        _listaDesglose = new ItemsControl();
        var wrapPanelFactory = new FrameworkElementFactory(typeof(WrapPanel));
        _listaDesglose.ItemsPanel = new ItemsPanelTemplate(wrapPanelFactory);
        scrollDesglose.Content = _listaDesglose;
        desgloseStack.Children.Add(scrollDesglose);
        _panelDesglose.Child = desgloseStack;
        Grid.SetRow(_panelDesglose, 3); root.Children.Add(_panelDesglose);

        // ── Footer ───────────────────────────────────────────────────────────
        var footer = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = _BrBorde, BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,6,0,0)
        };
        var dp = new DockPanel();
        _lblConteo = new TextBlock {
            FontSize = 11, Foreground = _BrLabel, VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_lblConteo, Dock.Left);
        dp.Children.Add(_lblConteo);

        var btnSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button MkB(string txt, string hex) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(16,0,16,0),
            Margin = new Thickness(6,0,0,0),
            Background = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            FontSize = 12, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnOk     = MkB("✔  Seleccionar", "#22C55E");
        var btnCerrar = MkB("✕  Cerrar",       "#6B7280");
        btnOk.Click     += (_, _) => Seleccionar();
        btnCerrar.Click += (_, _) => { DialogResult = false; Close(); };
        btnSp.Children.Add(btnOk);
        btnSp.Children.Add(btnCerrar);
        DockPanel.SetDock(btnSp, Dock.Right);
        dp.Children.Add(btnSp);
        footer.Child = dp;
        Grid.SetRow(footer, 4); root.Children.Add(footer);

        Content = root;
        Loaded += (_, _) => _txtBuscar.Focus();
    }

    private async Task CargarLocalesAsync()
    {
        try
        {
            var clienteRepo = App.Services.GetRequiredService<IClienteRepository>();
            var locales = (await clienteRepo.ObtenerLocalesAsync()).ToList();

            // Solo un ADMINISTRADOR (o el usuario con excepción puntual, ver
            // Usuario.PuedeVerTodosLosLocales) puede buscar artículos de "Todos los locales" o
            // de un local distinto al propio — antes cualquier vendedor podía ver stock y
            // precios de cualquier sucursal desde este selector. Un vendedor normal queda fijo
            // en SU local, con el combo deshabilitado.
            var puedeVerTodos = SessionService.Instance.UsuarioActual?.PuedeVerTodosLosLocales == true;
            if (puedeVerTodos)
            {
                var opciones = new List<LocalItemBuscador> { new() { Id = 0, Nombre = "Todos los locales" } };
                opciones.AddRange(locales.Select(l => new LocalItemBuscador { Id = l.Id, Nombre = l.Nombre }));
                _cboLocal.ItemsSource = opciones;
                // Preselecciona el local de la sesión actual, si existe en la lista
                _cboLocal.SelectedValue = opciones.Any(l => l.Id == _idLocalSesion) ? _idLocalSesion : 0;
            }
            else
            {
                var opciones = locales.Where(l => l.Id == _idLocalSesion)
                    .Select(l => new LocalItemBuscador { Id = l.Id, Nombre = l.Nombre })
                    .ToList();
                _cboLocal.ItemsSource = opciones;
                if (opciones.Count > 0) _cboLocal.SelectedValue = opciones[0].Id;
                _cboLocal.IsEnabled = false;
            }
        }
        catch { _cboLocal.SelectedIndex = 0; }
    }

    private async Task CargarArticulosAsync()
    {
        try
        {
            var termino = (_txtBuscar?.Text ?? "").Trim();
            int idLocalFiltro = _cboLocal.SelectedValue is int v ? v : 0;

            var articulos = await _artRepo.BuscarParaVentaContadoAsync(termino, idLocalFiltro > 0 ? idLocalFiltro : null);
            _todos = articulos.ToList();
            AplicarFiltroStockYOrden();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al cargar artículos: " + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AplicarFiltroStockYOrden()
    {
        int idLocalFiltro = _cboLocal?.SelectedValue is int v ? v : 0;
        IEnumerable<ArticuloBuscador> lista = _todos;

        if (idLocalFiltro > 0)
            lista = lista.Where(a => a.StockLocal > 0 || _chkSoloStock.IsChecked != true);
        if (_chkSoloStock.IsChecked == true)
            lista = lista.Where(a => (idLocalFiltro > 0 ? a.StockLocal : a.StockTotal) > 0);

        var result = lista.ToList();
        _grid.ItemsSource = result;
        _lblConteo.Text = result.Count == _todos.Count
            ? $"{result.Count} artículos"
            : $"{result.Count} de {_todos.Count} artículos";
        ActualizarDesglose();
    }

    private async void ActualizarDesglose()
    {
        if (_grid.SelectedItem is not ArticuloBuscador art)
        {
            _panelDesglose.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            // Un vendedor normal solo ve el stock de SU local — el desglose por local (útil
            // para saber si conviene pedir traslado) queda reservado a quien puede ver todos
            // los locales, igual que el combo de búsqueda de esta misma ventana.
            var puedeVerTodos = SessionService.Instance.UsuarioActual?.PuedeVerTodosLosLocales == true;
            var stockPorLocal = (await _artRepo.ObtenerStockTodosLocalesAsync(art.Id))
                .Where(p => p.S > 0 && (puedeVerTodos || p.IdLocal == _idLocalSesion))
                .OrderByDescending(p => p.S)
                .ToList();

            if (stockPorLocal.Count == 0)
            {
                _panelDesglose.Visibility = Visibility.Collapsed;
                return;
            }

            _listaDesglose.ItemsSource = stockPorLocal.Select(p => new
            {
                Texto = $"{p.LocalNombre}: {p.S:N0}"
            }).ToList();
            _listaDesglose.ItemTemplate = CrearPlantillaChip();
            _panelDesglose.Visibility = Visibility.Visible;
        }
        catch { _panelDesglose.Visibility = Visibility.Collapsed; }
    }

    private DataTemplate CrearPlantillaChip()
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.White);
        borderFactory.SetValue(Border.BorderBrushProperty, _BrClaro);
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(8,4,8,4));
        borderFactory.SetValue(Border.MarginProperty, new Thickness(0,0,6,6));

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Texto"));
        textFactory.SetValue(TextBlock.FontSizeProperty, 11.5);
        textFactory.SetValue(TextBlock.ForegroundProperty, _BrPrim);
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

        borderFactory.AppendChild(textFactory);
        return new DataTemplate { VisualTree = borderFactory };
    }

    private void Seleccionar()
    {
        if (_grid.SelectedItem is ArticuloBuscador art)
        {
            ArticuloSeleccionado = new ArticuloConPrecio
            {
                Id             = art.Id,
                Ca             = art.Ca,
                D              = art.D,
                MarcaNombre    = art.MarcaNombre,
                Stock          = art.StockLocal > 0 ? art.StockLocal : art.StockTotal,
                PventaLocal    = art.Pventa,
                Pc             = art.Contado,
                LocalUbicacion = ""
            };
            DialogResult = true;
        }
    }
}

internal class LocalItemBuscador
{
    public int    Id     { get; set; }
    public string Nombre { get; set; } = "";
}
