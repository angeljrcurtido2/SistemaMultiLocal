using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Compras;
using CrediSoft.UI.Views.Maestros;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Ventas;

// ══════════════════════════════════════════════════════════════════════════════
//  VENTA AL CONTADO
// ══════════════════════════════════════════════════════════════════════════════

public class VentaContadoWindow : Window
{
    private readonly IClienteRepository _clienteRepo;
    private readonly IArticuloRepository _artRepo;
    private readonly IVentaRepository _ventaRepo;
    private readonly IDbConnectionFactory _db;

    private Cliente? _clienteActual;
    private readonly List<LineaDetalle> _carrito = new();
    private TextBox _txtLocalDisplay = null!;

    // Local "a nombre de" el cual figura esta venta contado — propio de esta ventana,
    // nunca se escribe en session.LocalActual (eso ensuciaría toda la sesión del admin:
    // dashboard, otras pantallas, etc. quedarían con el local equivocado después de
    // vender "para" otra sucursal). Arranca en el local de la sesión y solo un admin/
    // usuario con PuedeVerTodosLosLocales puede cambiarlo vía "Buscar local".
    private int    _idLocalVenta;
    private string _nombreLocalVenta = "";
    private string _telefonoLocalVenta = "";

    // controles
    private TextBox    _txtCodigo = null!, _txtNombreDesc = null!, _txtCantidad = null!;
    private TextBox    _txtPrecioContado = null!, _txtDescuento = null!;
    // Recargo/descuento en Gs. aplicado al precio del artículo antes de agregarlo al carrito.
    // Recargo lo puede usar cualquier vendedor (vender más caro nunca perjudica al negocio);
    // Descuento solo se renderiza si el usuario logueado es administrador — ver BuildUI.
    private TextBox    _txtRecargoArt = null!, _txtDescuentoArt = null!;
    // Se pregunta una sola vez por artículo seleccionado (no en cada foco al mismo campo) —
    // mismo patrón que _yaPreguntoReajuste en CobrosWindow. Se resetean en BuscarPorCodigoAsync/
    // AbrirSelectorArticuloVacio, donde se asigna un _artSeleccionado nuevo.
    private bool _yaPreguntoRecargo = false, _yaPreguntoDescuento = false;

    // Vendedor que realmente hizo esta venta, si es distinto de _session.UsuarioActual —
    // mismo patrón que _vendedorCobrador en CobrosWindow. No reemplaza la sesión: la caja
    // sigue siendo la del local/usuario logueado (ID_CAJERO), pero la comisión de venta
    // (CABECERA_SALES.ID_USUARIO / CAJA_DETALLE.ID_VENDEDOR) queda a nombre de este vendedor.
    private (int Id, string Nombre)? _vendedorVenta = null;
    private TextBlock _txtVendedorVenta = null!;
    private TextBox    _txtEfectivo = null!, _txtCambio = null!, _txtNroTarjeta = null!;
    private TextBlock  _lblIndicadorPago = null!;
    // campos legacy (se mantienen para que OnConfirmar compile sin cambios)
    private TextBox    _txtBuscarCliente = null!, _txtBuscarArticulo = null!, _txtTarjeta = null!;
    private TextBlock  _lblClienteNombre = null!, _lblClienteCI = null!, _lblTotal = null!;
    private DataGrid   _gridClientes = null!, _gridArticulos = null!, _gridDetalle = null!;
    private ComboBox   _cboMetodo = null!;
    private Button     _btnConfirmar = null!;

    // artículo seleccionado actualmente
    private ArticuloConPrecio? _artSeleccionado;

    // brushes
    private static readonly SolidColorBrush BrNaranja   = new(Color.FromRgb( 21,101,192));
    private static readonly SolidColorBrush BrNaranjaOsc= new(Color.FromRgb( 14, 47, 68));
    private static readonly SolidColorBrush BrFondo     = new(Color.FromRgb(238,244,251));
    private static readonly SolidColorBrush BrBlanco    = new(Colors.White);
    private static readonly SolidColorBrush BrGrisText  = new(Color.FromRgb(107,114,128));
    private static readonly SolidColorBrush BrVerde     = new(Color.FromRgb( 22,163, 74));
    private static readonly SolidColorBrush BrGris      = new(Color.FromRgb(107,114,128));
    private static readonly SolidColorBrush BrFooter    = new(Color.FromRgb( 50, 50, 50));
    private static readonly SolidColorBrush BrGridHdr   = new(Color.FromRgb( 21,101,192));
    private static readonly SolidColorBrush BrHdrDark   = new(Color.FromRgb(0x0E, 0x2F, 0x44));
    private static readonly SolidColorBrush BrHdrSub    = new(Color.FromRgb(0x7F, 0xB3, 0xD3));
    private static readonly SolidColorBrush BrCardBorde = new(Color.FromRgb(208, 218, 232));
    private static readonly SolidColorBrush BrCampoLbl  = new(Color.FromRgb( 90,107,124));

    public VentaContadoWindow()
    {
        var svc = App.Services;
        _clienteRepo = svc.GetRequiredService<IClienteRepository>();
        _artRepo     = svc.GetRequiredService<IArticuloRepository>();
        _ventaRepo   = svc.GetRequiredService<IVentaRepository>();
        _db          = svc.GetRequiredService<IDbConnectionFactory>();

        Title = "Ventas contado";
        Width = 960; Height = 690;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrBlanco;
        BuildUI();
        Loaded += async (_, _) => await PrecargarConsumidorFinalAsync();
    }

    // Venta a Contado nunca debería exigir identificar un cliente real — el legacy resuelve
    // esto con un cliente reservado (ID_CLIENTE=1, NOMBRE_CLIENTE='xxx', mismo registro en
    // LOCAL y producción) que los reportes ya reconocen y muestran como "CONSUMIDOR FINAL"
    // (ver AtrasosWindow.xaml.cs, esGenerico = clienteRaw.Trim().ToUpper() == "XXX"). Antes,
    // esta ventana bloqueaba agregar artículos hasta buscar/seleccionar un cliente cualquiera
    // — precargando "xxx" acá, el cajero puede facturar de inmediato y solo busca un cliente
    // real si necesita identificarlo (p.ej. para historial o descuentos).
    private async Task PrecargarConsumidorFinalAsync()
    {
        if (_clienteActual != null) return;
        var xxx = await _clienteRepo.BuscarPorIdAsync(1);
        if (xxx == null) return;
        _clienteActual = xxx;
        _txtBuscarCliente.Text = "Consumidor final (CI: xxx)";
    }

    // Mismo criterio que los reportes (ver AtrasosWindow.xaml.cs, esGenerico): el cliente
    // reservado "xxx" (ID_CLIENTE=1) se traduce a "Consumidor Final" en todo lo que ve el
    // cajero o el cliente final — modal de confirmación, mensaje de éxito y ticket impreso
    // nunca deberían mostrar el literal "xxx".
    private static string NombreClienteParaMostrar(string nombreCliente) =>
        nombreCliente.Trim().ToUpper() == "XXX" ? "Consumidor Final" : nombreCliente;

    private void BuildUI()
    {
        // ── helpers ───────────────────────────────────────────────────────────
        TextBox TB(string? def = null, double w = double.NaN) => new TextBox {
            Text = def ?? "", Height = 30, Padding = new Thickness(8,4,8,4),
            FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center,
            Background = BrBlanco,
            BorderBrush = BrCardBorde,
            BorderThickness = new Thickness(1),
            Width = double.IsNaN(w) ? double.NaN : w
        };
        Button CampoBtn(string text) => new Button {
            Content = text, Height = 30, Padding = new Thickness(12,0,12,0),
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Background = BrNaranja, Foreground = BrBlanco,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(4,0,0,0)
        };
        StackPanel Campo(string lbl, UIElement ctrl, double minW = 0) {
            var sp = new StackPanel { Margin = new Thickness(0,0,16,0), MinWidth = minW };
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = BrCampoLbl, Margin = new Thickness(1,0,0,4) });
            sp.Children.Add(ctrl);
            return sp;
        }

        var root = new Grid { Background = new SolidColorBrush(Color.FromRgb(245,245,245)) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // título
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // barra de campos
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // footer

        // ── Barra de título ──────────────────────────────────────────────────
        var titleBar = new Border { Background = BrHdrDark, Padding = new Thickness(20,14,20,14) };
        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new Border {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(20),
            Background = BrNaranja, Margin = new Thickness(0,0,14,0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "🛒", FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        });
        var titleTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleTexts.Children.Add(new TextBlock {
            Text = "VENTA AL CONTADO", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = BrBlanco });
        titleTexts.Children.Add(new TextBlock {
            Text = "Cargá los artículos y confirmá el cobro", FontSize = 11,
            Foreground = BrHdrSub, Margin = new Thickness(0,2,0,0) });
        titleRow.Children.Add(titleTexts);
        Grid.SetColumn(titleRow, 0); titleGrid.Children.Add(titleRow);

        // ── Badge "vendido por" + botón para cambiar de vendedor ──────────────
        // Permite que un vendedor SIN caja propia venda usando la caja física ya
        // abierta de este local — la comisión de venta queda a nombre de quien
        // realmente vendió (_vendedorVenta), no de quien está logueado/tiene la caja.
        var vendedorCard = new Border {
            Background = new SolidColorBrush(Color.FromRgb(21,101,192)), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12,6,12,6), VerticalAlignment = VerticalAlignment.Center
        };
        var vendedorSp = new StackPanel { Orientation = Orientation.Horizontal };
        var vendedorTextSp = new StackPanel { Margin = new Thickness(0,0,10,0) };
        vendedorTextSp.Children.Add(new TextBlock { Text = "VENDIDO POR", FontSize = 8.5,
            FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(187,222,251)) });
        _txtVendedorVenta = new TextBlock { Text = SessionService.Instance.UsuarioActual?.NombreUsuario ?? "",
            FontSize = 12, FontWeight = FontWeights.Bold, Foreground = BrBlanco };
        vendedorTextSp.Children.Add(_txtVendedorVenta);
        vendedorSp.Children.Add(vendedorTextSp);
        var btnCambiarVendedor = new Button {
            Content = "CAMBIAR", Height = 26, Padding = new Thickness(10,0,10,0),
            Background = BrBlanco, Foreground = BrNaranja, FontSize = 10, FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center
        };
        btnCambiarVendedor.Click += (_, _) =>
        {
            var explic = new ExplicarCambioVendedorDialog { Owner = this };
            explic.ShowDialog();
            if (!explic.QuiereContinuar) return;

            var dlg = new CrediSoft.UI.Views.Cobros.SeleccionarVendedorCobradorDialog { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _vendedorVenta = (dlg.VendedorId, dlg.VendedorNombre);
                _txtVendedorVenta.Text = dlg.VendedorNombre;
            }
        };
        vendedorSp.Children.Add(btnCambiarVendedor);
        vendedorCard.Child = vendedorSp;
        Grid.SetColumn(vendedorCard, 1); titleGrid.Children.Add(vendedorCard);

        titleBar.Child = titleGrid;
        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);

        // ── Barra de campos ──────────────────────────────────────────────────
        var camposBar = new Border {
            Background = BrBlanco,
            BorderBrush = BrCardBorde,
            BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(20,14,20,14)
        };
        var camposSp = new StackPanel();
        var fila1 = new WrapPanel { Orientation = Orientation.Horizontal };
        var fila2 = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,14,0,0) };

        // Local + botón búsqueda
        var session = SessionService.Instance;
        _idLocalVenta     = session.LocalActual?.IdLocal ?? 0;
        _nombreLocalVenta = session.LocalActual?.NombreLocal ?? "—";
        _telefonoLocalVenta = session.LocalActual?.TelefonoLocal ?? "";
        _txtLocalDisplay = TB(_nombreLocalVenta, 160);
        _txtLocalDisplay.IsReadOnly = true;
        _txtLocalDisplay.Background = new SolidColorBrush(Color.FromRgb(232,240,254));
        var btnBuscarLocal = CampoBtn("Buscar local");
        btnBuscarLocal.Click += async (_, _) => await AbrirBuscadorLocal();
        // Solo un ADMINISTRADOR (o el usuario con excepción puntual, ver
        // Usuario.PuedeVerTodosLosLocales) puede cambiar el local de la venta. Un vendedor
        // normal vende siempre desde SU local — el botón "Buscar local" desaparece y el
        // local queda fijo en el de la sesión (ya es el valor por defecto de _txtLocalDisplay).
        if (session.UsuarioActual?.PuedeVerTodosLosLocales != true)
            btnBuscarLocal.Visibility = Visibility.Collapsed;
        var localRow = new StackPanel { Orientation = Orientation.Horizontal };
        localRow.Children.Add(_txtLocalDisplay);
        localRow.Children.Add(btnBuscarLocal);
        fila1.Children.Add(Campo("Local", localRow));

        // Código + botón buscar artículo
        _txtCodigo = TB("", 110);
        _txtBuscarArticulo = _txtCodigo;
        _txtCodigo.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await BuscarPorCodigoAsync(); };
        var btnBuscarArt = CampoBtn("Buscar artículo");
        // El botón siempre abre el selector completo — es una acción explícita del
        // usuario para elegir/cambiar artículo, sin importar qué haya quedado tipeado
        // en el campo Código (antes, con texto no coincidente ahí, el botón llamaba a
        // BuscarPorCodigoAsync(), que si no encontraba nada solo escribía "— no
        // encontrado —" y no abría nada, dando la sensación de que el botón no hacía nada).
        btnBuscarArt.Click += async (_, _) => await AbrirSelectorArticuloVacio();
        var codRow = new StackPanel { Orientation = Orientation.Horizontal };
        codRow.Children.Add(_txtCodigo);
        codRow.Children.Add(btnBuscarArt);
        fila1.Children.Add(Campo("Código", codRow));

        // Nombre / descripción
        _txtNombreDesc = TB("", 280);
        _txtNombreDesc.IsReadOnly = true;
        _txtNombreDesc.Background = new SolidColorBrush(Color.FromRgb(232,240,254));
        fila1.Children.Add(Campo("Nombre o descripción", _txtNombreDesc));

        camposSp.Children.Add(fila1);

        // Cantidad
        _txtCantidad = TB("", 80);
        _txtCantidad.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
        fila2.Children.Add(Campo("Cantidad", _txtCantidad));

        // Precio contado-contado
        _txtPrecioContado = TB("", 130);
        _txtPrecioContado.IsReadOnly = true;
        _txtPrecioContado.Background = new SolidColorBrush(Color.FromRgb(232,240,254));
        fila2.Children.Add(Campo("Precio contado-contado", _txtPrecioContado));

        // Muestra en vivo, en "Precio contado-contado", el efecto del recargo/descuento sobre
        // el precio base del artículo seleccionado — así el vendedor ve el precio final antes
        // de confirmar, no recién al ver el subtotal del carrito.
        void ActualizarPrecioMostrado()
        {
            if (_artSeleccionado == null) return;
            var recargo   = ParseDec(_txtRecargoArt.Text);
            var descuento = SessionService.Instance.UsuarioActual?.EsAdministrador == true
                ? ParseDec(_txtDescuentoArt.Text) : 0m;
            var precio = Math.Max(0, _artSeleccionado.PventaLocal + recargo - descuento);
            _txtPrecioContado.Text = precio.ToString("N0").Replace(",", ".");
        }

        // ── Tarjeta de ajuste de precio (Recargo/Descuento) ─────────────────────
        // Separada visualmente del resto de los campos (borde + fondo propios, ícono, texto
        // explicativo) para que no se confunda con Cantidad/Precio/Cliente — mismo motivo que
        // llevó a rediseñar el campo Reajuste en Cobros: un vendedor puede tipear acá pensando
        // que es el precio final o el efectivo recibido. Además dispara un modal de
        // confirmación (ConfirmarAjustePrecioDialog) la primera vez que se entra a cada campo
        // por artículo, igual que ConfirmarReajusteDialog en Cobros.
        var ajusteCard = new Border {
            Background = new SolidColorBrush(Color.FromRgb(255,248,225)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(255,224,130)),
            BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,0,16,0)
        };
        var ajusteStack = new StackPanel();
        var ajusteHdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,6) };
        ajusteHdr.Children.Add(new TextBlock { Text = "💰", FontSize = 12, Margin = new Thickness(0,0,4,0) });
        ajusteHdr.Children.Add(new TextBlock { Text = "AJUSTAR PRECIO DEL ARTÍCULO (opcional)",
            FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(230,81,0)) });
        ajusteStack.Children.Add(ajusteHdr);

        var ajusteRow = new StackPanel { Orientation = Orientation.Horizontal };

        // Recargo (Gs.) — cualquier vendedor puede aumentar el precio del artículo.
        _txtRecargoArt = TB("0", 100);
        _txtRecargoArt.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
        _txtRecargoArt.TextChanged += (_, _) => ActualizarPrecioMostrado();
        _txtRecargoArt.GotFocus += (_, _) =>
        {
            if (_yaPreguntoRecargo) return;
            _yaPreguntoRecargo = true;
            var dlg = new ConfirmarAjustePrecioDialog(esDescuento: false) { Owner = this };
            dlg.ShowDialog();
            if (!dlg.QuiereAjustar) { _txtCantidad.Focus(); _txtCantidad.SelectAll(); }
        };
        ajusteRow.Children.Add(Campo("Recargo Gs.", _txtRecargoArt));

        // Descuento (Gs.) — solo un ADMINISTRADOR puede bajar el precio del artículo. Un
        // vendedor normal ni siquiera ve este campo (no solo deshabilitado — no se renderiza).
        if (SessionService.Instance.UsuarioActual?.EsAdministrador == true)
        {
            _txtDescuentoArt = TB("0", 100);
            _txtDescuentoArt.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
            _txtDescuentoArt.TextChanged += (_, _) => ActualizarPrecioMostrado();
            _txtDescuentoArt.GotFocus += (_, _) =>
            {
                if (_yaPreguntoDescuento) return;
                _yaPreguntoDescuento = true;
                var dlg = new ConfirmarAjustePrecioDialog(esDescuento: true) { Owner = this };
                dlg.ShowDialog();
                if (!dlg.QuiereAjustar) { _txtCantidad.Focus(); _txtCantidad.SelectAll(); }
            };
            ajusteRow.Children.Add(Campo("Descuento Gs.", _txtDescuentoArt));
        }
        else
        {
            _txtDescuentoArt = new TextBox { Text = "0", Visibility = Visibility.Collapsed };
        }

        ajusteStack.Children.Add(ajusteRow);
        ajusteCard.Child = ajusteStack;
        fila2.Children.Add(ajusteCard);

        // Cliente: campo readonly + botón naranja
        _lblClienteNombre = new TextBlock(); // legacy stub, no se usa visualmente
        _lblClienteCI     = new TextBlock();
        _txtBuscarCliente = TB("— sin cliente —", 210);
        _txtBuscarCliente.IsReadOnly = true;
        _txtBuscarCliente.Background = new SolidColorBrush(Color.FromRgb(232,240,254));
        var btnBuscarCliente = CampoBtn("Buscar cliente");
        btnBuscarCliente.Click += (_, _) => AbrirBuscadorCliente();
        var clienteRow = new StackPanel { Orientation = Orientation.Horizontal };
        clienteRow.Children.Add(_txtBuscarCliente);
        clienteRow.Children.Add(btnBuscarCliente);
        fila2.Children.Add(Campo("Cliente", clienteRow));

        // Botón Ingresar
        var btnIngresar = new Button {
            Content = "✔  Ingresar", Height = 30, Padding = new Thickness(18,0,18,0),
            Background = BrVerde, Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 12,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        btnIngresar.Click += async (_, _) => await AgregarArticuloAsync();
        fila2.Children.Add(btnIngresar);

        camposSp.Children.Add(fila2);
        camposBar.Child = camposSp;
        Grid.SetRow(camposBar, 1);
        root.Children.Add(camposBar);

        // ── FOOTER ──────────────────────────────────────────────────────────
        var footer = new Border {
            Background = new SolidColorBrush(Color.FromRgb(230,230,230)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200,200,200)),
            BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(14,10,14,10)
        };
        var fGrid = new Grid();
        fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var darkText  = new SolidColorBrush(Color.FromRgb(50,50,50));
        var labelText = new SolidColorBrush(Color.FromRgb(90,90,90));
        var boxBorder = new SolidColorBrush(Color.FromRgb(180,180,180));
        var boxBg     = new SolidColorBrush(Color.FromRgb(245,245,245));

        // Total
        _lblTotal = new TextBlock {
            Text = "Total: 0 Gs.", FontSize = 18, FontWeight = FontWeights.Bold,
            Foreground = BrNaranja, VerticalAlignment = VerticalAlignment.Center,  // BrNaranja ahora es azul
            Margin = new Thickness(0,0,20,0)
        };
        Grid.SetColumn(_lblTotal, 0); fGrid.Children.Add(_lblTotal);

        // Caja pago en efectivo — rediseñada "anti-tontos" con el mismo criterio que
        // Efectivo Recibido en Cobros: fondo verde bien llamativo (no gris apagado como el
        // resto de campos) para que el cajero identifique de un vistazo dónde escribir el
        // billete real, y un indicador de texto (FALTA/SOBRA/EXACTO) en vez de solo un número
        // de "Cambio" negativo sin contexto, que fácilmente se lee como un error.
        var caja = new Border {
            Background = new SolidColorBrush(Color.FromRgb(232,245,233)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(129,199,132)),
            BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14,10,14,10), Margin = new Thickness(0,0,14,0)
        };
        var cajaSp = new StackPanel();
        var cajaHdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,8) };
        cajaHdr.Children.Add(new TextBlock { Text = "💵 ", FontSize = 12 });
        cajaHdr.Children.Add(new TextBlock { Text = "EFECTIVO RECIBIDO DEL CLIENTE",
            FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(27,94,32)) });
        cajaSp.Children.Add(cajaHdr);

        _txtEfectivo = new TextBox { Width = 150, Height = 34, TextAlignment = TextAlignment.Right,
            Padding = new Thickness(8,4,8,4), FontSize = 15, FontWeight = FontWeights.Bold, Text = "0",
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(76,175,80)), BorderThickness = new Thickness(2),
            VerticalContentAlignment = VerticalAlignment.Center };
        _txtCambio = new TextBox { Width = 150, Height = 26, TextAlignment = TextAlignment.Right,
            Padding = new Thickness(6,2,6,2), FontSize = 12, Text = "0", IsReadOnly = true,
            Background = new SolidColorBrush(Color.FromRgb(235,235,235)),
            Foreground = darkText, VerticalContentAlignment = VerticalAlignment.Center };
        _txtTarjeta = new TextBox();

        _lblIndicadorPago = new TextBlock { FontSize = 11, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0,6,0,0), TextAlignment = TextAlignment.Center,
            Visibility = Visibility.Collapsed };
        cajaSp.Children.Add(_lblIndicadorPago);

        // solo dígitos, con separador de miles en tiempo real
        bool _efFormatting = false;
        _txtEfectivo.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };
        _txtEfectivo.TextChanged += (_, _) => {
            if (_efFormatting) return;
            _efFormatting = true;
            var digits = new string(_txtEfectivo.Text.Where(char.IsDigit).ToArray());
            if (decimal.TryParse(digits, out var v)) {
                var formatted = v == 0 ? "0" : v.ToString("N0").Replace(",",".");
                var caret = _txtEfectivo.CaretIndex;
                var oldLen = _txtEfectivo.Text.Length;
                _txtEfectivo.Text = formatted;
                // mantener cursor al final si se está escribiendo
                _txtEfectivo.CaretIndex = formatted.Length;
            }
            _efFormatting = false;
            ActualizarTotal();
        };

        TextBlock AddRow(StackPanel p, string lbl, UIElement ctrl) {
            var r = new Grid { Margin = new Thickness(0,3,0,0) };
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var tb = new TextBlock { Text = lbl, Foreground = darkText, FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tb, 0); r.Children.Add(tb);
            Grid.SetColumn(ctrl, 1); r.Children.Add(ctrl);
            p.Children.Add(r);
            return tb;
        }
        var lblEfectivoRow = AddRow(cajaSp, "Recibí Gs.", _txtEfectivo);
        AddRow(cajaSp, "Cambio",   _txtCambio);
        caja.Child = cajaSp;
        Grid.SetColumn(caja, 1); fGrid.Children.Add(caja);

        // Método + Número (Número solo visible cuando no es EFECTIVO)
        var metBorder = new Border {
            Background = boxBg, BorderBrush = boxBorder,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,0,14,0)
        };
        var metSp = new StackPanel();
        _cboMetodo = new ComboBox { Width = 150, Height = 26, Margin = new Thickness(0,0,0,6) };
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "EFECTIVO",        Tag = (byte)1, IsSelected = true });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta débito",  Tag = (byte)2 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta crédito", Tag = (byte)3 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "Transferencia",   Tag = (byte)4 });
        _cboMetodo.Items.Add(new ComboBoxItem { Content = "QR",              Tag = (byte)5 });
        _cboMetodo.SelectedIndex = 0;
        _txtNroTarjeta = new TextBox { Width = 150, Height = 26, Padding = new Thickness(6,2,6,2), FontSize = 11 };
        var lblNumero = new TextBlock { Text = "N° comprobante", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = labelText, Margin = new Thickness(0,6,0,4),
            Visibility = Visibility.Collapsed };
        _txtNroTarjeta.Visibility = Visibility.Collapsed;

        // mostrar/ocultar Número según método y ajustar labels descriptivos
        _cboMetodo.SelectionChanged += (_, _) => {
            var met = _cboMetodo.SelectedItem is ComboBoxItem ci ? (byte)(ci.Tag ?? (byte)1) : (byte)1;
            var esEfectivo = met == 1;
            lblNumero.Visibility      = esEfectivo ? Visibility.Collapsed : Visibility.Visible;
            _txtNroTarjeta.Visibility = esEfectivo ? Visibility.Collapsed : Visibility.Visible;
            lblEfectivoRow.Text = esEfectivo ? "Efectivo" : "Monto entregado";
            lblNumero.Text = met switch {
                2 => "N° voucher débito",
                3 => "N° voucher crédito",
                4 => "N° transferencia",
                5 => "N° operación QR",
                _ => "N° comprobante"
            };
            if (esEfectivo) _txtNroTarjeta.Text = "";
        };

        metSp.Children.Add(new TextBlock { Text = "Método", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = labelText, Margin = new Thickness(0,0,0,4) });
        metSp.Children.Add(_cboMetodo);
        metSp.Children.Add(lblNumero);
        metSp.Children.Add(_txtNroTarjeta);
        metBorder.Child = metSp;
        Grid.SetColumn(metBorder, 2); fGrid.Children.Add(metBorder);

        // Atajos
        var atajSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,0,0,0) };
        foreach (var t in new[] {"F1: Ayuda local","F2: Ayuda código","F5: Guardar",
                                  "F9: Cancelar","Ctrl+S: Cerrar","Ctrl+P: Imprimir"})
            atajSp.Children.Add(new TextBlock { Text = t, FontSize = 9, Foreground = labelText });
        Grid.SetColumn(atajSp, 3); fGrid.Children.Add(atajSp);

        // Botones
        _btnConfirmar = new Button {
            Content = "Guardar", Height = 46, Width = 90,
            Background = new SolidColorBrush(Color.FromRgb(22,163,74)), Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 13, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, Margin = new Thickness(0,0,8,0), IsEnabled = false
        };
        _btnConfirmar.Click += OnConfirmar;
        var btnCerrar = new Button {
            Content = "Cerrar", Height = 46, Width = 80,
            Background = new SolidColorBrush(Color.FromRgb(107,114,128)), Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 13, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnCerrar.Click += (_, _) => Close();
        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        btnsSp.Children.Add(_btnConfirmar); btnsSp.Children.Add(btnCerrar);
        Grid.SetColumn(btnsSp, 4); fGrid.Children.Add(btnsSp);

        footer.Child = fGrid;
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        // ── GRID DETALLE ─────────────────────────────────────────────────────
        _gridDetalle = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            Background = BrBlanco, RowBackground = BrBlanco,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(250,250,255)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(220,220,230)),
            CanUserResizeRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
            FontSize = 13, BorderThickness = new Thickness(0), RowHeight = 36,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto
        };
        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrNaranja));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrBlanco));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,9,10,9)));
        hdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(220,120,0))));
        hdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,0)));
        _gridDetalle.ColumnHeaderStyle = hdrStyle;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,0,10,0)));
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        _gridDetalle.CellStyle = cellStyle;

        Style rStyle() { var s = new Style(typeof(TextBlock));
            s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            s.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0,0,8,0))); return s; }
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("ArticuloCodigo"), Width = 100 });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("ArticuloNombre"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Cant.",       Binding = new System.Windows.Data.Binding("Cantidad"),        Width = 60,  ElementStyle = rStyle() });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "P. venta",    Binding = new System.Windows.Data.Binding("PvStr"),           Width = 110, ElementStyle = rStyle() });
        _gridDetalle.Columns.Add(new DataGridTextColumn { Header = "Subtotal",    Binding = new System.Windows.Data.Binding("SubtotalStr"),     Width = 120, ElementStyle = rStyle() });

        // Botón "Quitar" bien visible — antes era solo un "✕" de 30px sin fondo, casi
        // imperceptible en la grilla; ahora tiene tamaño, color y texto que se notan a simple
        // vista, para que quitar un artículo cargado por error sea obvio y fácil de encontrar.
        var btnQCol = new DataGridTemplateColumn { Header = "Quitar", Width = 90 };
        var cf = new FrameworkElementFactory(typeof(Button));
        var contentSp = new FrameworkElementFactory(typeof(StackPanel));
        contentSp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var iconTb = new FrameworkElementFactory(typeof(TextBlock));
        iconTb.SetValue(TextBlock.TextProperty, "🗑 ");
        iconTb.SetValue(TextBlock.FontSizeProperty, 11.0);
        var textTb = new FrameworkElementFactory(typeof(TextBlock));
        textTb.SetValue(TextBlock.TextProperty, "Quitar");
        textTb.SetValue(TextBlock.FontSizeProperty, 11.0);
        textTb.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        contentSp.AppendChild(iconTb);
        contentSp.AppendChild(textTb);
        cf.AppendChild(contentSp);
        cf.SetValue(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255,235,235)));
        cf.SetValue(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(198,40,40)));
        cf.SetValue(Button.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(239,154,154)));
        cf.SetValue(Button.BorderThicknessProperty, new Thickness(1));
        cf.SetValue(Button.CursorProperty, Cursors.Hand);
        cf.SetValue(Button.PaddingProperty, new Thickness(8,3,8,3));
        cf.SetValue(Button.MarginProperty, new Thickness(4,3,4,3));
        cf.SetValue(ToolTipService.ToolTipProperty, "Quitar este artículo de la venta");
        cf.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnQuitarArticulo));
        btnQCol.CellTemplate = new DataTemplate { VisualTree = cf };
        _gridDetalle.Columns.Add(btnQCol);

        _gridDetalle.KeyDown += (_, e) => {
            if ((e.Key == Key.Delete || e.Key == Key.Back) && _gridDetalle.SelectedItem is LineaDetalle ld)
                { _carrito.Remove(ld); RefrescarCarrito(); }
        };

        // campos legacy ocultos
        _gridClientes  = new DataGrid { Visibility = Visibility.Collapsed };
        _gridArticulos = new DataGrid { Visibility = Visibility.Collapsed };
        _txtDescuento  = new TextBox { Text = "0" };

        var gridBorder = new Border { Child = _gridDetalle, Background = BrBlanco };
        Grid.SetRow(gridBorder, 2);
        root.Children.Add(gridBorder);
        Content = root;
    }

    // ── Búsqueda por código (Enter en campo Código) ─────────────────────────
    private async Task BuscarPorCodigoAsync()
    {
        var term = _txtCodigo.Text.Trim();
        if (string.IsNullOrWhiteSpace(term)) return;
        var resultados = await _artRepo.BuscarAsync(term);
        var lista = resultados.ToList();
        if (lista.Count == 0) { _txtNombreDesc.Text = "— no encontrado —"; return; }

        ArticuloConPrecio Pick(Core.Models.Articulo a) {
            return new ArticuloConPrecio { Id = a.Id, Ca = a.Ca, D = a.D, MarcaNombre = a.MarcaNombre, PventaLocal = 0 };
        }

        if (lista.Count == 1) {
            var a = lista[0];
            var precio = _idLocalVenta > 0
                ? await _artRepo.ObtenerPrecioLocalAsync(a.Id, _idLocalVenta) : null;
            _artSeleccionado = new ArticuloConPrecio { Id = a.Id, Ca = a.Ca, D = a.D,
                MarcaNombre = a.MarcaNombre, PventaLocal = precio?.Pventa ?? 0 };
            _txtCodigo.Text        = a.Ca;
            _txtNombreDesc.Text    = a.D;
            _txtPrecioContado.Text = (_artSeleccionado.PventaLocal).ToString("N0").Replace(",",".");
            _txtRecargoArt.Text = "0"; _txtDescuentoArt.Text = "0";
            _yaPreguntoRecargo = false; _yaPreguntoDescuento = false;
            _txtCantidad.Focus(); _txtCantidad.SelectAll();
        } else {
            // múltiples resultados: abrir modal completo
            await AbrirSelectorArticulo(lista);
        }
    }

    private async Task AbrirSelectorArticuloVacio()
    {
        // Mismo selector visual que Venta a Crédito (header con búsqueda integrada,
        // filtro por local + "solo con stock", desglose de stock por local al
        // seleccionar) — sin el simulador de cuotas, que no aplica a venta contado.
        var win = new BuscadorArticuloWindow(_idLocalVenta) { Owner = this };
        if (win.ShowDialog() == true && win.ArticuloSeleccionado is ArticuloConPrecio a)
        {
            // Venta contado usa el precio contado-contado (Pc), no el precio de venta
            // normal (PventaLocal) que trae el selector compartido con Venta a Crédito.
            _artSeleccionado = new ArticuloConPrecio {
                Id = a.Id, Ca = a.Ca, D = a.D, MarcaNombre = a.MarcaNombre,
                PventaLocal = a.Pc, Stock = a.Stock,
            };
            _txtCodigo.Text        = a.Ca;
            _txtNombreDesc.Text    = a.D;
            _txtPrecioContado.Text = a.Pc.ToString("N0").Replace(",", ".");
            _txtRecargoArt.Text = "0"; _txtDescuentoArt.Text = "0";
            _yaPreguntoRecargo = false; _yaPreguntoDescuento = false;
            _txtCantidad.Focus(); _txtCantidad.SelectAll();
        }
        await Task.CompletedTask;
    }

    private async Task AbrirSelectorArticulo(List<Core.Models.Articulo> lista)
    {
        // Para resultados múltiples de búsqueda por código: abrir el modal con filtro ya aplicado
        await AbrirSelectorArticuloVacio();
    }

    private async Task AbrirBuscadorLocal()
    {
        // cargar locales desde BD
        List<(int Id, string Nombre, string Telefono)> locales;
        try {
            using var conn = _db.Create();
            var rows = await conn.QueryAsync<dynamic>("SELECT ID_LOCAL, NOMBRE, TELEFONO FROM LOCALES ORDER BY NOMBRE");
            locales = rows.Select(r => {
                var d = (IDictionary<string,object>)r;
                int id = d.TryGetValue("ID_LOCAL", out var v1) ? Convert.ToInt32(v1) : 0;
                string nom = d.TryGetValue("NOMBRE", out var v2) ? v2?.ToString() ?? "" : "";
                string tel = d.TryGetValue("TELEFONO", out var v3) ? v3?.ToString() ?? "" : "";
                return (id, nom, tel);
            }).ToList();
        } catch (Exception ex) {
            MessageBox.Show($"Error cargando locales: {ex.Message}");
            return;
        }

        int? idActual = _idLocalVenta > 0 ? _idLocalVenta : null;

        var BrClaro   = new SolidColorBrush(Color.FromRgb(176, 212, 236)); // #B0D4EC
        var BrTarjeta = new SolidColorBrush(Color.FromRgb(229, 231, 235)); // borde tarjeta
        var BrHoverBg = new SolidColorBrush(Color.FromRgb(238, 244, 251)); // #EEF4FB

        var dlg = new Window {
            Title = "Seleccionar local", Width = 440, Height = 560,
            MinWidth = 380, MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.CanResize, Background = BrFondo,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header + buscador
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // lista
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header con buscador ──────────────────────────────────────────────
        var hdr = new Border { Background = BrNaranjaOsc, Padding = new Thickness(16,14,16,14) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "🏬  Seleccionar local", FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = BrBlanco, Margin = new Thickness(0,0,0,10)
        });
        var searchBorder = new Border {
            Background = BrNaranja, CornerRadius = new CornerRadius(6),
            BorderBrush = BrClaro, BorderThickness = new Thickness(1),
            Padding = new Thickness(10,0,10,0)
        };
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        searchRow.Children.Add(new TextBlock {
            Text = "🔎", FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = BrClaro, Margin = new Thickness(0,0,8,0)
        });
        var txtBuscar = new TextBox {
            Height = 32, FontSize = 12.5,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = BrBlanco, CaretBrush = BrBlanco,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Width = 360
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
                : locales.Where(l => l.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filtrados.Count == 0) {
                lista.Children.Add(new TextBlock {
                    Text = "Sin resultados", FontStyle = FontStyles.Italic, Foreground = BrGrisText,
                    HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,20,0,0)
                });
                return;
            }

            foreach (var (id, nombre, telefono) in filtrados)
            {
                bool esActual = id == idActual;
                bool esSeleccionado = id == seleccionadoId;

                var card = new Border {
                    Background      = esSeleccionado ? BrHoverBg : BrBlanco,
                    BorderBrush     = esSeleccionado ? BrNaranja : BrTarjeta,
                    BorderThickness = new Thickness(esSeleccionado ? 2 : 1),
                    CornerRadius    = new CornerRadius(8),
                    Padding         = new Thickness(14,10,14,10),
                    Margin          = new Thickness(0,0,0,8),
                    Cursor          = Cursors.Hand,
                    Tag             = id
                };

                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icono = new Border {
                    Width = 34, Height = 34, CornerRadius = new CornerRadius(17),
                    Background = esSeleccionado ? BrNaranja : BrFondo,
                    Margin = new Thickness(0,0,12,0),
                    Child = new TextBlock {
                        Text = "🏢", FontSize = 15,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                Grid.SetColumn(icono, 0); row.Children.Add(icono);

                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textStack.Children.Add(new TextBlock {
                    Text = nombre, FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = BrNaranjaOsc
                });
                if (esActual)
                    textStack.Children.Add(new TextBlock {
                        Text = "Local actual de la sesión", FontSize = 10, Foreground = BrGrisText,
                        Margin = new Thickness(0,2,0,0)
                    });
                Grid.SetColumn(textStack, 1); row.Children.Add(textStack);

                if (esSeleccionado)
                {
                    var chip = new TextBlock {
                        Text = "✔", FontSize = 15, FontWeight = FontWeights.Bold,
                        Foreground = BrNaranja, VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(chip, 2); row.Children.Add(chip);
                }

                card.Child = row;
                card.MouseLeftButtonDown += (_, e) => {
                    seleccionadoId = id;
                    Renderizar(txtBuscar.Text);
                    if (e.ClickCount == 2)
                        ConfirmarSeleccion(id, nombre, telefono);
                };
                lista.Children.Add(card);
            }
        }

        void ConfirmarSeleccion(int idSel, string nomSel, string telSel = "")
        {
            // Solo afecta esta venta — nunca session.LocalActual (eso mantendría al admin
            // "atrapado" en el local ajeno para el resto de la sesión).
            _idLocalVenta     = idSel;
            _nombreLocalVenta = nomSel;
            _telefonoLocalVenta = telSel;
            _txtLocalDisplay.Text = nomSel;

            // El precio/stock del artículo cargado corresponde al local anterior — al
            // cambiar de local hay que limpiar la selección en curso para que el usuario
            // vuelva a buscar el artículo y traiga el precio correcto del nuevo local.
            _artSeleccionado = null;
            _txtCodigo.Text = "";
            _txtNombreDesc.Text = "";
            _txtCantidad.Text = "";
            _txtPrecioContado.Text = "";

            dlg.Close();
        }

        txtBuscar.TextChanged += (_, _) => Renderizar(txtBuscar.Text);
        Renderizar("");

        // ── Footer ───────────────────────────────────────────────────────────
        var ftr = new Border {
            Background = BrBlanco, Padding = new Thickness(14,10,14,10),
            BorderBrush = BrTarjeta, BorderThickness = new Thickness(0,1,0,0)
        };
        var btnSel = new Button { Content = "✔  Seleccionar", Height = 34, Padding = new Thickness(16,0,16,0),
            Background = BrNaranja, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, FontSize = 12.5, Cursor = Cursors.Hand };
        var btnCan = new Button { Content = "✕  Cancelar", Height = 34, Padding = new Thickness(16,0,16,0),
            Margin = new Thickness(8,0,0,0),
            Background = BrGris, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, FontSize = 12.5, Cursor = Cursors.Hand };
        btnCan.Click += (_, _) => dlg.Close();
        btnSel.Click += (_, _) => {
            if (seleccionadoId is not int idSel) return;
            var loc = locales.FirstOrDefault(l => l.Id == idSel);
            ConfirmarSeleccion(idSel, loc.Nombre ?? "", loc.Telefono ?? "");
        };
        var ftrSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        ftrSp.Children.Add(btnSel); ftrSp.Children.Add(btnCan);
        ftr.Child = ftrSp;
        Grid.SetRow(ftr, 2); root.Children.Add(ftr);

        dlg.Content = root;
        dlg.Loaded += (_, _) => txtBuscar.Focus();
        dlg.ShowDialog();
    }

    private void AbrirBuscadorCliente()
    {
        var BrVerde  = new SolidColorBrush(Color.FromRgb(40, 167, 69));
        var BrFondoCli = new SolidColorBrush(Color.FromRgb(250,250,252));

        var dlg = new Window {
            Title = "Buscar Cliente",
            Width = 980, Height = 540, MinWidth = 700, MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.CanResize, Background = BrFondoCli,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header azul (mismo estilo BuscadorPersonaWindow) ─────────────────
        var hdrBorder = new Border { Background = BrNaranja, Padding = new Thickness(16,12,16,12) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "👤  Buscar Cliente",
            Foreground = BrBlanco, FontSize = 14, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0,0,0,8)
        });

        // Caja de búsqueda integrada en el header
        var searchBorder = new Border {
            Background  = new SolidColorBrush(Color.FromRgb(11, 40, 60)),
            CornerRadius = new CornerRadius(6),
            BorderBrush  = new SolidColorBrush(Color.FromRgb(30,136,229)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10,0,6,0)
        };
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        searchRow.Children.Add(new TextBlock {
            Text = "🔎", FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(187,222,251)),
            Margin = new Thickness(0,0,8,0)
        });
        var txtF = new TextBox {
            Height = 34, MinWidth = 400, FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = Brushes.White, CaretBrush = Brushes.White,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        searchRow.Children.Add(txtF);
        searchRow.Children.Add(new TextBlock {
            Text = "Nombre, C.I., teléfono o empresa...",
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromArgb(120,187,222,251)),
            Margin = new Thickness(4,0,0,0), IsHitTestVisible = false
        });
        searchBorder.Child = searchRow;
        hdrSp.Children.Add(searchBorder);
        hdrBorder.Child = hdrSp;
        Grid.SetRow(hdrBorder, 0); root.Children.Add(hdrBorder);

        // ── Footer ───────────────────────────────────────────────────────────
        var ftrBorder = new Border {
            Background = BrBlanco, Padding = new Thickness(12,8,12,8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(220,220,220)),
            BorderThickness = new Thickness(0,1,0,0)
        };
        var lblConteo = new TextBlock {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(107,114,128)), FontSize = 11
        };
        var btnSel = new Button {
            Content = "✔ Seleccionar", Height = 32, Padding = new Thickness(16,0,16,0),
            Background = BrVerde, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, FontSize = 12
        };
        var btnCan = new Button {
            Content = "✖ Cerrar", Height = 32, Padding = new Thickness(14,0,14,0),
            Background = BrGris, Foreground = BrBlanco, BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, FontSize = 12,
            Margin = new Thickness(6,0,0,0)
        };
        btnCan.Click += (_, _) => dlg.Close();
        var ftrDp = new DockPanel();
        var ftrRight = new StackPanel { Orientation = Orientation.Horizontal };
        ftrRight.Children.Add(btnSel); ftrRight.Children.Add(btnCan);
        DockPanel.SetDock(ftrRight, Dock.Right);
        ftrDp.Children.Add(ftrRight); ftrDp.Children.Add(lblConteo);
        ftrBorder.Child = ftrDp;
        Grid.SetRow(ftrBorder, 2); root.Children.Add(ftrBorder);

        // ── DataGrid ─────────────────────────────────────────────────────────
        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrNaranja));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrBlanco));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,6,8,6)));
        hdrStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));

        var grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            Background = BrBlanco,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(249,250,251)),
            BorderThickness = new Thickness(0), ColumnHeaderStyle = hdrStyle,
            RowHeight = 40, FontSize = 12,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(229,231,235)),
            Margin = new Thickness(0)
        };

        grid.Columns.Add(new DataGridTextColumn {
            Header = "C.I.", Width = 100,
            Binding = new System.Windows.Data.Binding("CiCliente")
        });
        grid.Columns.Add(new DataGridTextColumn {
            Header = "Nombre", Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            Binding = new System.Windows.Data.Binding("NombreCliente")
        });
        grid.Columns.Add(new DataGridTextColumn {
            Header = "Teléfono", Width = 110,
            Binding = new System.Windows.Data.Binding("TelefonoCliente")
        });
        grid.Columns.Add(new DataGridTextColumn {
            Header = "Ciudad", Width = 110,
            Binding = new System.Windows.Data.Binding("CiudadCliente")
        });
        grid.Columns.Add(new DataGridTextColumn {
            Header = "Empresa", Width = 160,
            Binding = new System.Windows.Data.Binding("EmpresaLaboral")
        });

        Grid.SetRow(grid, 1); root.Children.Add(grid);
        dlg.Content = root;

        // Antes esto cargaba los primeros 200 clientes UNA vez al abrir y filtraba en
        // memoria sobre esos mismos 200 mientras se tipeaba — con 25.000+ clientes en la
        // base, cualquiera fuera de esos 200 (ordenados alfabéticamente) era imposible de
        // encontrar sin importar qué tan bien se escribiera el nombre. Bug real reportado
        // 2026-08-04: cliente real "Rosi Mariela Ojeda Toledo" no aparecía en ninguna
        // búsqueda. Ahora cada tecleo dispara una consulta real a la base (con debounce de
        // 300ms, mismo patrón que el buscador de artículos en VentaCreditoWindow).
        int _totalActual = 0;

        void ActualizarConteo(int shown) {
            lblConteo.Text = $"{shown} clientes";
        }

        async Task BuscarEnBaseAsync() {
            var termino = txtF.Text.Trim();
            var res = (await _clienteRepo.BuscarAsync(termino)).ToList();
            grid.ItemsSource = res;
            _totalActual = res.Count;
            ActualizarConteo(_totalActual);
        }

        var debounceCliente = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        debounceCliente.Tick += async (_, _) => { debounceCliente.Stop(); await BuscarEnBaseAsync(); };
        txtF.TextChanged += (_, _) => { debounceCliente.Stop(); debounceCliente.Start(); };
        txtF.KeyDown     += (_, e) => { if (e.Key == Key.Escape) dlg.Close(); };

        dlg.Loaded += async (_, _) => {
            txtF.Focus();
            lblConteo.Text = "Cargando...";
            await BuscarEnBaseAsync();
        };

        btnSel.Click += (_, _) => {
            if (grid.SelectedItem is not Cliente c) return;
            _clienteActual = c;
            _txtBuscarCliente.Text = $"{c.NombreCliente}  CI: {c.CiCliente}";
            ActualizarConfirmarBtn();
            dlg.Close();
        };
        grid.MouseDoubleClick += (_, _) => btnSel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        dlg.ShowDialog();
    }

    // ── Clientes (legacy stubs, lógica real en AbrirBuscadorCliente) ─────────

    private Task BuscarClienteAsync() => Task.CompletedTask;

    private void OnClienteSelected(object sender, SelectionChangedEventArgs e) { }

    // ── Artículos ───────────────────────────────────────────────────────────────

    private Task BuscarArticuloAsync() => BuscarPorCodigoAsync();

    private async Task AgregarArticuloAsync()
    {
        // Salvaguarda: en el uso normal esto nunca debería dispararse — la ventana precarga
        // "Consumidor Final" al abrir (ver PrecargarConsumidorFinalAsync), así que Venta a
        // Contado ya NO exige identificar un cliente real. Solo queda por si la precarga
        // falló (ej. sin conexión al abrir) y el cajero borró el cliente sin buscar otro.
        if (_clienteActual == null)
        {
            await PrecargarConsumidorFinalAsync();
            if (_clienteActual == null)
            {
                MessageBox.Show("No se pudo cargar el cliente de esta venta. Reintente o busque un cliente manualmente.",
                    "Cliente no disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (_artSeleccionado == null)
        {
            // intentar buscar por código si no hay artículo seleccionado
            await BuscarPorCodigoAsync();
            if (_artSeleccionado == null) {
                MessageBox.Show("Primero busque un artículo por código.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        var cantText = _txtCantidad.Text.Trim();
        if (!decimal.TryParse(string.IsNullOrWhiteSpace(cantText) ? "1" : cantText, out var cantidad) || cantidad <= 0)
            cantidad = 1;

        var artSelec = _artSeleccionado;
        var existente = _carrito.FirstOrDefault(x => x.IdArt == artSelec.Id);
        if (existente != null)
        {
            existente.Cantidad += cantidad;
        }
        else
        {
            var art = await _artRepo.BuscarPorCodigoAsync(artSelec.Ca);

            var recargo   = ParseDec(_txtRecargoArt.Text);
            // El campo de descuento no se renderiza para un vendedor normal (queda en "0"
            // fijo desde su construcción en BuildUI) — igual se relee acá por si el mismo
            // proceso cambia de usuario sin reiniciar la ventana.
            var descuento = SessionService.Instance.UsuarioActual?.EsAdministrador == true
                ? ParseDec(_txtDescuentoArt.Text) : 0m;
            var precioAjustado = Math.Max(0, artSelec.PventaLocal + recargo - descuento);

            _carrito.Add(new LineaDetalle
            {
                IdArt          = artSelec.Id,
                ArticuloCodigo = artSelec.Ca,
                ArticuloNombre = artSelec.D,
                Cantidad       = cantidad,
                Pv             = precioAjustado,
                Iva            = art?.Iva ?? 0
            });
        }
        // limpiar selección
        _artSeleccionado = null;
        _txtCodigo.Text = ""; _txtNombreDesc.Text = ""; _txtPrecioContado.Text = "";
        _txtRecargoArt.Text = "0"; _txtDescuentoArt.Text = "0";
        _yaPreguntoRecargo = false; _yaPreguntoDescuento = false;
        _txtCantidad.Text = ""; _txtCodigo.Focus();
        RefrescarCarrito();
    }

    private void OnQuitarArticulo(object sender, RoutedEventArgs e)
    {
        // Usa el DataContext del propio botón clickeado, no _gridDetalle.SelectedItem — antes,
        // si el usuario clickeaba "Quitar" en una fila sin seleccionarla primero, no pasaba
        // nada (o se borraba otra fila que sí estuviera seleccionada).
        if ((e.OriginalSource as FrameworkElement)?.DataContext is LineaDetalle l)
        {
            _carrito.Remove(l);
            RefrescarCarrito();
        }
    }

    private void RefrescarCarrito()
    {
        _gridDetalle.ItemsSource = null;
        _gridDetalle.ItemsSource = _carrito.ToList();
        ActualizarTotal();
        ActualizarConfirmarBtn();
    }

    private void ActualizarTotal()
    {
        var total = _carrito.Sum(x => x.Subtotal);
        if (_lblTotal != null) _lblTotal.Text = $"Total: {total:N0} Gs.";
        if (_txtCambio == null) return;

        decimal.TryParse(new string((_txtEfectivo?.Text ?? "0").Where(char.IsDigit).ToArray()), out var ent);
        var diff = ent - total;
        _txtCambio.Text = diff.ToString("N0").Replace(",", ".");

        if (_lblIndicadorPago == null) return;
        if (total <= 0) { _lblIndicadorPago.Visibility = Visibility.Collapsed; return; }
        _lblIndicadorPago.Visibility = Visibility.Visible;
        if (diff < 0) {
            _lblIndicadorPago.Text = $"⚠ FALTAN Gs. {Math.Abs(diff):N0}".Replace(",", ".");
            _lblIndicadorPago.Foreground = new SolidColorBrush(Color.FromRgb(198,40,40));
        } else if (diff > 0) {
            _lblIndicadorPago.Text = $"✔ VUELTO Gs. {diff:N0}".Replace(",", ".");
            _lblIndicadorPago.Foreground = new SolidColorBrush(Color.FromRgb(46,125,50));
        } else {
            _lblIndicadorPago.Text = "✔ MONTO EXACTO";
            _lblIndicadorPago.Foreground = new SolidColorBrush(Color.FromRgb(46,125,50));
        }
    }

    private void ActualizarConfirmarBtn()
        => _btnConfirmar.IsEnabled = _clienteActual != null && _carrito.Count > 0;

    // ── Modal de éxito + reset ──────────────────────────────────────────────────

    private void MostrarExitoYResetear(string clienteNombre, int nroVenta, decimal total)
    {
        var dlg = new Window {
            Title = "Venta registrada",
            Width = 400, Height = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            Background = BrBlanco
        };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // header verde
        var hdr = new Border { Background = new SolidColorBrush(Color.FromRgb(22,163,74)), Padding = new Thickness(16,12,16,12) };
        hdr.Child = new TextBlock { Text = "Venta registrada exitosamente", FontSize = 14,
            FontWeight = FontWeights.Bold, Foreground = BrBlanco };
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // detalle
        var body = new StackPanel { Margin = new Thickness(20,16,20,0), VerticalAlignment = VerticalAlignment.Center };
        void Linea(string lbl, string val) {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,4,0,0) };
            sp.Children.Add(new TextBlock { Text = lbl, FontWeight = FontWeights.Bold, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(80,80,80)), Width = 110 });
            sp.Children.Add(new TextBlock { Text = val, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(30,30,30)) });
            body.Children.Add(sp);
        }
        Linea("Cliente:",    clienteNombre);
        Linea("N° de venta:", nroVenta.ToString());
        Linea("Total:",      total.ToString("N0").Replace(",",".") + " Gs.");
        Grid.SetRow(body, 1); root.Children.Add(body);

        // botón aceptar
        var ftr = new Border { Padding = new Thickness(16,10,16,10),
            Background = new SolidColorBrush(Color.FromRgb(245,245,245)) };
        var btnOk = new Button { Content = "Aceptar", Height = 32, Width = 100,
            Background = new SolidColorBrush(Color.FromRgb(22,163,74)), Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right };
        btnOk.Click += (_, _) => dlg.Close();
        ftr.Child = btnOk;
        Grid.SetRow(ftr, 2); root.Children.Add(ftr);

        dlg.Content = root;
        dlg.ShowDialog();

        // al cerrar el modal de éxito → resetear el formulario
        ResetearFormulario();
    }

    private void ResetearFormulario()
    {
        _carrito.Clear();
        _artSeleccionado   = null;
        _clienteActual     = null;

        _txtCodigo.Text         = "";
        _txtNombreDesc.Text     = "";
        _txtPrecioContado.Text  = "";
        _txtCantidad.Text       = "";
        _txtBuscarCliente.Text  = "— sin cliente —";
        _txtEfectivo.Text       = "0";
        _txtCambio.Text         = "0";
        _txtNroTarjeta.Text     = "";
        _cboMetodo.SelectedIndex = 0;

        // Cada venta empieza de nuevo a nombre de quien está logueado — si se vendió "por
        // otro vendedor", eso NO debe arrastrarse silenciosamente a la próxima venta.
        _vendedorVenta = null;
        _txtVendedorVenta.Text = SessionService.Instance.UsuarioActual?.NombreUsuario ?? "";

        RefrescarCarrito();
        _txtCodigo.Focus();

        // La próxima venta también arranca con Consumidor Final precargado (ver
        // PrecargarConsumidorFinalAsync) — sin esto, tras la primera venta el cajero
        // volvía a quedar bloqueado hasta buscar un cliente para la siguiente.
        _ = PrecargarConsumidorFinalAsync();
    }

    // ── Modal de confirmación de venta ──────────────────────────────────────────

    private bool MostrarConfirmacionVenta(string clienteNombre, decimal total)
    {
        var dlg = new Window {
            Title = "Confirmar venta al contado",
            Width = 640, Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            Background = BrBlanco
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // info cliente
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // total
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // footer botones

        // ── header ──
        var hdr = new Border { Background = BrNaranja, Padding = new Thickness(16,12,16,12) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "¿Desea realizar la venta al contado de los siguientes productos?",
            FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BrBlanco,
            TextWrapping = TextWrapping.Wrap
        });
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── info cliente/vendedor/fecha ──
        // El vendedor mostrado acá es a quien le corresponde la comisión de esta venta
        // (_vendedorVenta si se usó "CAMBIAR", o quien está logueado) — no necesariamente
        // quien tiene la sesión abierta, dato que antes no aparecía en ningún lado de esta
        // pantalla de confirmación.
        var infoBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(238,244,251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(187,222,251)),
            BorderThickness = new Thickness(0,0,0,1),
            Padding = new Thickness(16,8,16,8)
        };
        var infoStack = new StackPanel();
        var infoTitulo = new SolidColorBrush(Color.FromRgb(14,47,68));
        var infoValor  = new SolidColorBrush(Color.FromRgb(50,50,50));

        var filaCliente = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,3) };
        filaCliente.Children.Add(new TextBlock { Text = "Cliente: ", FontWeight = FontWeights.Bold,
            FontSize = 12, Foreground = infoTitulo });
        filaCliente.Children.Add(new TextBlock { Text = clienteNombre, FontSize = 12, Foreground = infoValor });
        infoStack.Children.Add(filaCliente);

        var vendedorNombre = _vendedorVenta?.Nombre ?? SessionService.Instance.UsuarioActual?.NombreUsuario ?? "";
        var filaVendedorFecha = new StackPanel { Orientation = Orientation.Horizontal };
        filaVendedorFecha.Children.Add(new TextBlock { Text = "Vendedor: ", FontWeight = FontWeights.Bold,
            FontSize = 12, Foreground = infoTitulo });
        filaVendedorFecha.Children.Add(new TextBlock { Text = vendedorNombre, FontSize = 12, Foreground = infoValor,
            Margin = new Thickness(0,0,20,0) });
        filaVendedorFecha.Children.Add(new TextBlock { Text = "Fecha: ", FontWeight = FontWeights.Bold,
            FontSize = 12, Foreground = infoTitulo });
        filaVendedorFecha.Children.Add(new TextBlock { Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            FontSize = 12, Foreground = infoValor });
        infoStack.Children.Add(filaVendedorFecha);

        infoBorder.Child = infoStack;
        Grid.SetRow(infoBorder, 1); root.Children.Add(infoBorder);

        // ── grid artículos ──
        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrNaranja));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrBlanco));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,7,10,7)));

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10,0,10,0)));
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));

        Style rStyle() { var s = new Style(typeof(TextBlock));
            s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            s.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0,0,8,0))); return s; }

        var grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            RowHeight = 34, FontSize = 12,
            Background = BrBlanco, RowBackground = BrBlanco,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(232,240,254)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(220,220,220)),
            BorderThickness = new Thickness(0),
            ColumnHeaderStyle = hdrStyle, CellStyle = cellStyle,
            CanUserResizeRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
            Margin = new Thickness(0)
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("ArticuloCodigo"), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new System.Windows.Data.Binding("ArticuloNombre"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cant.",       Binding = new System.Windows.Data.Binding("Cantidad"),        Width = 55,  ElementStyle = rStyle() });
        grid.Columns.Add(new DataGridTextColumn { Header = "P. venta",    Binding = new System.Windows.Data.Binding("PvStr"),           Width = 100, ElementStyle = rStyle() });
        grid.Columns.Add(new DataGridTextColumn { Header = "Subtotal",    Binding = new System.Windows.Data.Binding("SubtotalStr"),     Width = 110, ElementStyle = rStyle() });
        grid.ItemsSource = _carrito.ToList();
        Grid.SetRow(grid, 2); root.Children.Add(grid);

        // ── total ──
        var totalBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(238,244,251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(187,222,251)),
            BorderThickness = new Thickness(0,1,0,1),
            Padding = new Thickness(16,10,16,10)
        };
        var totalTb = new TextBlock {
            Text = $"Total:  {total.ToString("N0").Replace(",",".")} Gs.",
            FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = BrNaranja, HorizontalAlignment = HorizontalAlignment.Right
        };
        totalBorder.Child = totalTb;
        Grid.SetRow(totalBorder, 3); root.Children.Add(totalBorder);

        // ── botones ──
        var ftrBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(245,245,245)),
            Padding = new Thickness(16,12,16,12)
        };
        bool resultado = false;
        var btnSi = new Button {
            Content = "Sí, confirmar venta", Height = 36, Padding = new Thickness(20,0,20,0),
            Background = new SolidColorBrush(Color.FromRgb(22,163,74)), Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnNo = new Button {
            Content = "Cancelar", Height = 36, Padding = new Thickness(20,0,20,0),
            Background = new SolidColorBrush(Color.FromRgb(107,114,128)), Foreground = BrBlanco,
            FontWeight = FontWeights.Bold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(10,0,0,0)
        };
        btnSi.Click += (_, _) => { resultado = true;  dlg.Close(); };
        btnNo.Click += (_, _) => { resultado = false; dlg.Close(); };
        var ftrSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        ftrSp.Children.Add(btnSi); ftrSp.Children.Add(btnNo);
        ftrBorder.Child = ftrSp;
        Grid.SetRow(ftrBorder, 4); root.Children.Add(ftrBorder);

        dlg.Content = root;
        dlg.ShowDialog();
        return resultado;
    }

    // ── Confirmar ───────────────────────────────────────────────────────────────

    private async void OnConfirmar(object sender, RoutedEventArgs e)
    {
        if (_clienteActual == null || _carrito.Count == 0) return;

        var session  = SessionService.Instance;
        if (session.UsuarioActual == null || _idLocalVenta <= 0) return;

        var subtotal = _carrito.Sum(x => x.Subtotal);
        var descPct  = ParseDec(_txtDescuento.Text);
        var descuento= subtotal * descPct / 100m;
        var total    = subtotal - descuento;
        var monto   = ParseDec(_txtEfectivo.Text);   // campo único para efectivo y no-efectivo
        var tarjeta = ParseDec(_txtTarjeta.Text);    // legacy, no usado visualmente
        byte metodo = 1;
        if (_cboMetodo.SelectedItem is ComboBoxItem cboItem && cboItem.Tag is byte m) metodo = m;

        // efectivo va en EntregaNormal; tarjeta/transferencia va en EntregaLogistica
        var efectivo = metodo == 1 ? monto : 0m;
        tarjeta      = metodo != 1 ? monto : 0m;

        // validar que el monto ingresado cubra el total
        if (monto < total)
        {
            MessageBox.Show(
                $"El monto ingresado ({monto.ToString("N0").Replace(",",".")} Gs.) es menor al total ({total.ToString("N0").Replace(",",".")} Gs.).",
                "Monto insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // La venta ingresa a la caja del local elegido (que puede ser distinto al de la
        // sesión si un admin/usuario con excepción vendió "a nombre de" otra sucursal) —
        // por lo tanto la caja que debe estar abierta es la de ESE local, no la de sesión.
        var caja = await App.Services.GetRequiredService<ICajaRepository>()
            .ObtenerCajaAbiertaAsync(_idLocalVenta);
        if (caja == null)
        {
            var dlgCaja = new CrediSoft.UI.Views.Cobros.CajaCerradaDialog(_nombreLocalVenta, "realizar la venta") { Owner = this };
            dlgCaja.ShowDialog();
            if (dlgCaja.IrAAbrirCaja)
                new CrediSoft.UI.Views.Caja.CajaAperturaWindow().Show();
            return;
        }

        if (!MostrarConfirmacionVenta(NombreClienteParaMostrar(_clienteActual.NombreCliente), total)) return;

        _btnConfirmar.IsEnabled = false;
        try
        {
            var nSol = await _ventaRepo.ObtenerNumeroSolicitudAsync();
            int idCabResult = 0;
            int idCab = 0;
            var formaPago = metodo == 1 ? "EFECTIVO" : metodo == 2 ? "TARJETA" : "TRANSFERENCIA";

            for (int i = 0; i < _carrito.Count; i++)
            {
                var det = _carrito[i];
                var agente = i + 1;
                var ultimo = _carrito.Count;
                var prm = new VentaContadoParams(
                    IdCab: idCab, NSol: nSol,
                    IdLocal: (byte)_idLocalVenta,
                    // Comisión de venta: el vendedor elegido en "CAMBIAR" si se usó, o quien
                    // está logueado por defecto — ver _vendedorVenta arriba.
                    IdUsuario: _vendedorVenta?.Id ?? session.UsuarioActual.IdUsuario,
                    IdCliente: _clienteActual.IdCliente,
                    IdGarante: 0, IdRef1: 0, IdRef2: 0,
                    NomRefCom1: "", TelRefCom1: "", TrabRefCom1: "",
                    NomRefCom2: "", TelRefCom2: "", TrabRefCom2: "",
                    // FORMA_DE_VENTA: 1=Contado, 2=Crédito — ver EliminarVentaContadoWindow/
                    // EliminarVentaCreditoWindow (HerramientasWindows.cs), que son la referencia
                    // de esta convención. Antes decía 2 acá, mezclando ventas contado con las
                    // ventas a crédito aprobadas por solicitud (que también usan 2).
                    FormaDeVenta: 1, MetodoDeVenta: metodo,
                    NTarjeta: _txtNroTarjeta.Text,
                    Parcial: subtotal, Descuento: descuento, Total: total,
                    EntregaNormal: efectivo, EntregaLogistica: tarjeta,
                    Cuotas: 1, MontoCuota: total,
                    Debe: 0, Haber: total, Cpha: 0,
                    Estado: 1, Tiva: _carrito.Sum(x => x.Iva * x.Cantidad * x.Pv / 100m),
                    IdDet: 0, IdCab2: 0, IdArt: det.IdArt,
                    Cantidad: det.Cantidad, Pc: 0, Pv: det.Pv, IvaArt: det.Iva, EsArt: 1,
                    IdMovArt: 0, Mov: 2, Mod: 2,
                    StIni: 0, PCant: 0, PcAct: 0,
                    IdCabCaja: 0, CountCaja: 0,
                    IdDetCaja: 0, Caja: 0,
                    Accion: 0, Concepto: 1, Monto: 0,
                    Metodo: metodo, Numero: _txtNroTarjeta.Text,
                    Para: 0, Obs: "", NVenta: 0,
                    // Caja real donde entra el efectivo: siempre la del usuario logueado
                    // (dueño de la sesión/caja abierta), nunca la del vendedor elegido arriba.
                    IdCajero: session.UsuarioActual.IdUsuario,
                    IdCajaFisica: (byte)caja.IdCajaFisica,
                    FormaPago: formaPago, MontoCaja: total, Referencia: _txtNroTarjeta.Text,
                    Agente: agente, Ultimo: ultimo);

                var (idCabDevuelto, nVenta) = await _ventaRepo.GuardarVentaContadoAsync(prm);
                idCab = idCabDevuelto;
                idCabResult = nVenta;
            }

            await OfrecerImprimirComprobanteAsync(idCabResult, total, formaPago);
            MostrarExitoYResetear(NombreClienteParaMostrar(_clienteActual.NombreCliente), idCabResult, total);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _btnConfirmar.IsEnabled = true;
        }
    }

    // extrae solo dígitos antes de parsear — compatible con formato "300.000" o "300,000"
    private static decimal ParseDec(string? s) {
        var digits = new string((s ?? "").Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var v) ? v : 0;
    }

    // ── Comprobante de venta (opcional) ──────────────────────────────────────────
    // Se ofrece imprimir ANTES de ResetearFormulario (que vacía _carrito) — reutiliza
    // TicketPrinter.DatosTicketVenta/ImprimirVentaAsync, ya construidos para Venta a
    // Crédito pero nunca conectados desde ningún módulo; EsContado:true ajusta los
    // textos del ticket ("VENTA AL CONTADO" en vez de "SOLICITUD APROBADA").
    private async Task OfrecerImprimirComprobanteAsync(int nVenta, decimal total, string formaPago)
    {
        try
        {
            var emp = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerDatosEmpresaAsync();
            var nTicket = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerNumeroTicketAsync(_idLocalVenta);
            var fmt = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerFormatoComprobanteAsync();

            var articulos = _carrito.Select(l => new CrediSoft.UI.Views.Shared.ArticuloTicket(
                l.ArticuloNombre, l.Cantidad, l.Pv, l.Subtotal)).ToList();

            var vendedorNombre = _vendedorVenta?.Nombre
                ?? SessionService.Instance.UsuarioActual?.NombreUsuario ?? "";

            var datos = new CrediSoft.UI.Views.Shared.DatosTicketVenta(
                NombreEmpresa: emp.Nombre,
                NombreLocal: $"LOCAL: {_idLocalVenta}  —  {_nombreLocalVenta}",
                Fecha: DateTime.Now,
                NumeroTicket: nTicket,
                NroSolicitud: nVenta.ToString(),
                Vendedor: vendedorNombre,
                NombreCliente: NombreClienteParaMostrar(_clienteActual?.NombreCliente ?? ""),
                TotalVenta: total,
                TotalEntrega: total,
                TotalConInteres: total,
                CantCuotas: 1,
                CostoCuota: total,
                Articulos: articulos,
                Timbrado: emp.Timbrado,
                VigenciaDesde: emp.Desde,
                VigenciaHasta: emp.Hasta,
                EsContado: true,
                TelefonoLocal: _telefonoLocalVenta);

            // Registro histórico para reimpresión posterior — no bloquea la impresión si falla.
            _ = CrediSoft.UI.Views.Shared.TicketPrinter.RegistrarComprobanteAsync(
                tipo: "VENTA_CONTADO", numeroTicket: nTicket, idLocal: _idLocalVenta,
                datosTicket: datos,
                idUsuarioCajero: SessionService.Instance.UsuarioActual?.IdUsuario, nombreCajero: vendedorNombre,
                idCliente: _clienteActual?.IdCliente, nombreCliente: _clienteActual?.NombreCliente,
                nroSolicitud: nVenta.ToString(), montoTotal: total);

            var previa = new ComprobanteVentaPreviaWindow(datos, fmt) { Owner = this };
            previa.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al generar comprobante:\n{ex.Message}", "Impresión",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static Button Btn(string text, string hex, int width = 110) => new Button
    {
        Content = text, Height = 30, Width = width, Margin = new Thickness(0, 0, 6, 0),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand
    };

    private static TextBlock Lbl(string t) => new TextBlock
        { Text = t, FontSize = 11, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 4, 4, 1) };
}

// ══════════════════════════════════════════════════════════════════════════════
//  VISOR DE SOLICITUDES
// ══════════════════════════════════════════════════════════════════════════════

public sealed class EstadoChipConverter : System.Windows.Data.IValueConverter
{
    public static readonly EstadoChipConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var s = (value as string ?? "").ToUpperInvariant().Trim();
        if (s.Contains("ACEPT") || s.Contains("APROBAD"))
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb( 22, 163,  74));
        if (s.Contains("VERIF"))
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb( 99,  91, 235));
        if (s.Contains("RECHAZ") || s.Contains("CANCEL"))
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220,  38,  38));
        if (s.Contains("NUEVO") || s.Contains("PEND"))
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb( 14, 116, 196));
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(217, 119, 6));
    }
    public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotSupportedException();
}

// Estado del segundo paso del flujo (Aprobar / Confirmar Entrega, ver ConfirmarEntregaAsync):
// distingue "aprobada, esperando confirmación" de "ya confirmada", y no aplica en absoluto
// para solicitudes en Verificar/Rechazado.
internal enum ConfirmacionEntregaEstado { NoAplica, Pendiente, Confirmada }

internal static class ConfirmacionEntregaHelper
{
    public static ConfirmacionEntregaEstado Calcular(SolicitudItem? s)
    {
        if (s == null || s.EstadoNum != 1) return ConfirmacionEntregaEstado.NoAplica;
        return s.VentaGenerada ? ConfirmacionEntregaEstado.Confirmada : ConfirmacionEntregaEstado.Pendiente;
    }
}

public sealed class ConfirmacionEntregaChipConverter : System.Windows.Data.IValueConverter
{
    public static readonly ConfirmacionEntregaChipConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => ConfirmacionEntregaHelper.Calcular(value as SolicitudItem) switch
        {
            ConfirmacionEntregaEstado.Confirmada => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb( 22, 163,  74)),
            ConfirmacionEntregaEstado.Pendiente  => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(217, 119,   6)),
            _                                     => System.Windows.Media.Brushes.Transparent,
        };
    public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotSupportedException();
}

public sealed class ConfirmacionEntregaTextoConverter : System.Windows.Data.IValueConverter
{
    public static readonly ConfirmacionEntregaTextoConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => ConfirmacionEntregaHelper.Calcular(value as SolicitudItem) switch
        {
            ConfirmacionEntregaEstado.Confirmada => "Confirmada",
            ConfirmacionEntregaEstado.Pendiente  => "Pendiente",
            _                                     => "—",
        };
    public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotSupportedException();
}

public sealed class ConfirmacionEntregaColorTextoConverter : System.Windows.Data.IValueConverter
{
    public static readonly ConfirmacionEntregaColorTextoConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => ConfirmacionEntregaHelper.Calcular(value as SolicitudItem) == ConfirmacionEntregaEstado.NoAplica
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 150, 150))
            : System.Windows.Media.Brushes.White;
    public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotSupportedException();
}

public class SolicitudItem
{
    public int      IdSolicitud    { get; set; }
    public int      IdLocal        { get; set; }
    public string   Numero         { get; set; } = string.Empty;
    public string   LocalNombre    { get; set; } = string.Empty;
    public string   ClienteNombre  { get; set; } = string.Empty;
    public string   VendedorNombre { get; set; } = string.Empty;
    public string   Estado         { get; set; } = string.Empty;
    public byte     EstadoNum      { get; set; }  // 0=Pendiente 1=Aprobado 2=Rechazado
    public DateTime FechaSolicitud { get; set; }
    // Fecha desde la que empiezan a correr las cuotas (CAB_SOL_SALES.FECHA_COBRO, elegida en
    // el campo "FECHA FIGURAR" al cargar la solicitud) — distinta de FechaSolicitud (cuándo se
    // cargó). Pedido explícito: mostrarla como columna aparte en el listado, ya que antes solo
    // se veía dentro del detalle de cada solicitud.
    public DateTime? FechaFigurar   { get; set; }
    public decimal  TotalVenta     { get; set; }
    public decimal  Entrega        { get; set; }
    public int      Cuotas         { get; set; }
    // Solo relevante cuando EstadoNum==1 (Aprobado): distingue "aprobada, esperando que se
    // confirme la entrega" de "aprobada y ya facturada" — antes de separar estos dos pasos,
    // Aprobar ya creaba la venta de inmediato, así que este campo no hacía falta.
    public bool     VentaGenerada  { get; set; }
}

public class VisorSolicitudesWindow : Window
{
    private readonly IDbConnectionFactory _db;

    private TextBox    _txtFiltro  = null!;
    private DataGrid   _grid       = null!;
    private TextBlock  _lblConteo  = null!;
    private DatePicker _dpDesde    = null!;
    private DatePicker _dpHasta    = null!;
    private ComboBox   _cboOrden   = null!;
    private ComboBox   _cboPorPag  = null!;
    private ComboBox   _cboLocal   = null!;
    private ComboBox   _cboConfirmacion = null!;
    private TextBlock  _lblPagInfo = null!;
    private Button     _btnAnterior = null!;
    private Button     _btnSiguiente = null!;
    private List<SolicitudItem> _todosItems = new();
    private List<SolicitudItem> _itemsFiltrados = new();
    private int _paginaActual = 1;
    private int _porPagina    = 10;

    // Paleta azul corporativa
    private static readonly System.Windows.Media.SolidColorBrush BrPrimary  = new(System.Windows.Media.Color.FromRgb( 21,  101, 192));
    private static readonly System.Windows.Media.SolidColorBrush BrPrimDark = new(System.Windows.Media.Color.FromRgb( 14,  47,  68));
    private static readonly System.Windows.Media.SolidColorBrush BrBlanco   = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.SolidColorBrush BrFondo    = new(System.Windows.Media.Color.FromRgb(238, 244, 251));
    private static readonly System.Windows.Media.SolidColorBrush BrBorde    = new(System.Windows.Media.Color.FromRgb(187, 222, 251));
    private static readonly System.Windows.Media.SolidColorBrush BrGris     = new(System.Windows.Media.Color.FromRgb(107, 114, 128));
    private static readonly System.Windows.Media.SolidColorBrush BrAlt      = new(System.Windows.Media.Color.FromRgb(232, 240, 254));


    public VisorSolicitudesWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Visor de Solicitudes de Crédito";
        // Ancho ajustado a las columnas más compactas (fuente/padding reducidos) — con ese
        // tamaño más chico, "Fecha Figurar"/"Monto Entrega"/"Confirmación de Entrega" ya
        // entran sin necesitar tanto ancho extra como con las columnas originales.
        Width = 1240; Height = 640;
        MinWidth = 980; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrFondo;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // barra búsqueda
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header azul ───────────────────────────────────────────────────────
        var hdr = new Border {
            Background = BrPrimDark, Padding = new Thickness(18, 12, 18, 12),
        };
        hdr.Effect = new System.Windows.Media.Effects.DropShadowEffect {
            BlurRadius = 6, ShadowDepth = 2, Opacity = 0.22,
            Color = System.Windows.Media.Color.FromRgb(0,0,0)
        };
        var hdrGrid = new Grid();
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hdrLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hdrLeft.Children.Add(new Border {
            Background = BrPrimary, CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock { Text = "📋", FontSize = 20 }
        });
        var hdrTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hdrTexts.Children.Add(new TextBlock {
            Text = "Ventas a Crédito — Solicitudes",
            Foreground = BrBlanco, FontSize = 16, FontWeight = FontWeights.Bold
        });
        hdrTexts.Children.Add(new TextBlock {
            Text = "Listado de solicitudes · Haga clic en \"+ Nueva solicitud\" para crear una nueva venta a crédito",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(187, 222, 251)),
            FontSize = 10.5
        });
        hdrLeft.Children.Add(hdrTexts);
        Grid.SetColumn(hdrLeft, 0);

        _lblConteo = new TextBlock {
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(187,222,251)),
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(_lblConteo, 1);

        hdrGrid.Children.Add(hdrLeft);
        hdrGrid.Children.Add(_lblConteo);
        hdr.Child = hdrGrid;
        Grid.SetRow(hdr, 0);
        root.Children.Add(hdr);

        // ── Barra de filtros (dos filas) ──────────────────────────────────────
        var searchBar = new Border {
            Background = BrBlanco, Padding = new Thickness(14, 8, 14, 8),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0, 0, 0, 1)
        };
        var barraVert = new StackPanel { Orientation = Orientation.Vertical };

        // Fila 1: buscador + botones
        var fila1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

        var searchBox = new Border {
            Background = BrFondo, BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(0, 0, 8, 0)
        };
        var searchInner = new StackPanel { Orientation = Orientation.Horizontal };
        searchInner.Children.Add(new TextBlock {
            Text = "🔍", FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0), Foreground = BrGris
        });
        _txtFiltro = new TextBox {
            Width = 240, Height = 32, FontSize = 13, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17,24,39))
        };
        _txtFiltro.TextChanged += (_, _) => AplicarFiltros();
        searchInner.Children.Add(_txtFiltro);
        searchBox.Child = searchInner;
        fila1.Children.Add(searchBox);

        var btnRefresh = MakeBtn("↺  Refrescar", BrPrimary);
        btnRefresh.Click += async (_, _) => {
            _txtFiltro.Text = ""; _dpDesde.SelectedDate = null; _dpHasta.SelectedDate = null;
            _cboOrden.SelectedIndex = 0;
            await Cargar();
        };
        fila1.Children.Add(btnRefresh);

        var btnNueva = MakeBtn("+ Nueva solicitud", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22,163,74)));
        btnNueva.Margin = new Thickness(8,0,0,0);
        btnNueva.Click += (_, _) => {
            // Show() en vez de ShowDialog() — mismo motivo que DetalleSolicitudWindow (ver
            // AbrirDetalle en esta misma clase): con ShowDialog() esta ventana bloqueaba
            // nativamente cualquier interacción con el resto del sistema (incluido abrir
            // otros módulos desde el menú principal) mientras estuviera abierta, sea que
            // estuviera minimizada o no — bug real reportado. Owner=this SÍ hace falta
            // igual (con Show() no bloquea nada): sin él, la ventana queda "huérfana" y
            // Windows la minimiza sola en cuanto otra ventana del mismo proceso toma el
            // foco. El refresco de la lista que antes se hacía al volver de ShowDialog()
            // ahora se hace en el evento Closed, que dispara igual sea modal o no.
            var w = new VentaCreditoWindow { Owner = this };
            w.Closed += async (_, _) =>
            {
                // Misma mitigación que MainWindow.AbrirVentana: WPF a veces minimiza el Owner
                // al cerrar una ventana hija — sin esto, cerrar "Ingresar solicitud" desde acá
                // (en vez del menú principal) minimizaba este visor entero.
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                Activate();
                await Cargar();
            };
            w.Show();
        };
        fila1.Children.Add(btnNueva);
        barraVert.Children.Add(fila1);

        // Fila 2: filtros de fecha + ordenamiento
        var fila2 = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        void AgregarLblFiltro(string txt) => fila2.Children.Add(new TextBlock {
            Text = txt, FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            Foreground = BrGris, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });

        AgregarLblFiltro("Desde:");
        _dpDesde = new DatePicker { Width = 115, Height = 28, FontSize = 11.5, Margin = new Thickness(0, 0, 10, 0) };
        _dpDesde.SelectedDateChanged += (_, _) => AplicarFiltros();
        fila2.Children.Add(_dpDesde);

        AgregarLblFiltro("Hasta:");
        _dpHasta = new DatePicker { Width = 115, Height = 28, FontSize = 11.5, Margin = new Thickness(0, 0, 14, 0) };
        _dpHasta.SelectedDateChanged += (_, _) => AplicarFiltros();
        fila2.Children.Add(_dpHasta);

        // Separador vertical
        fila2.Children.Add(new Border {
            Width = 1, Background = BrBorde, Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Stretch
        });

        AgregarLblFiltro("Ordenar:");
        _cboOrden = new ComboBox { Width = 175, Height = 28, FontSize = 11.5 };
        _cboOrden.Items.Add(new ComboBoxItem { Content = "Más recientes primero", Tag = "desc_fecha" });
        _cboOrden.Items.Add(new ComboBoxItem { Content = "Más antiguos primero",  Tag = "asc_fecha"  });
        _cboOrden.Items.Add(new ComboBoxItem { Content = "Mayor total primero",   Tag = "desc_total" });
        _cboOrden.Items.Add(new ComboBoxItem { Content = "Por estado",            Tag = "estado"     });
        _cboOrden.SelectedIndex = 0;
        _cboOrden.SelectionChanged += (_, _) => AplicarFiltros();
        fila2.Children.Add(_cboOrden);

        // Separador
        fila2.Children.Add(new Border {
            Width = 1, Background = BrBorde, Margin = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Stretch
        });
        AgregarLblFiltro("Items:");
        _cboPorPag = new ComboBox { Width = 78, Height = 28, FontSize = 11.5 };
        foreach (var op in new[] { "5", "10", "20", "Todos" })
            _cboPorPag.Items.Add(new ComboBoxItem { Content = op });
        _cboPorPag.SelectedIndex = 1; // default 10
        _cboPorPag.SelectionChanged += (_, _) => {
            var sel = (_cboPorPag.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "10";
            _porPagina = sel == "Todos" ? int.MaxValue : (int.TryParse(sel, out var n) ? n : 10);
            _paginaActual = 1;
            MostrarPagina();
        };
        fila2.Children.Add(_cboPorPag);

        // Separador + filtro de local
        fila2.Children.Add(new Border {
            Width = 1, Background = BrBorde, Margin = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Stretch
        });
        AgregarLblFiltro("Local:");
        _cboLocal = new ComboBox { Width = 160, Height = 28, FontSize = 11.5 };
        // Solo un ADMINISTRADOR (o el usuario con excepción puntual, ver
        // Usuario.PuedeVerTodosLosLocales) puede ver/filtrar por "Todos los locales" o elegir
        // uno distinto al propio. Un usuario normal solo ve solicitudes de SU local — el
        // combo queda fijo en ese local y deshabilitado.
        var puedeVerTodosSol = SessionService.Instance.UsuarioActual?.PuedeVerTodosLosLocales == true;
        if (puedeVerTodosSol)
        {
            _cboLocal.Items.Add(new ComboBoxItem { Content = "Todos los locales", Tag = (int?)null });
        }
        else
        {
            var localSesion = SessionService.Instance.LocalActual;
            _cboLocal.Items.Add(new ComboBoxItem { Content = localSesion?.NombreLocal ?? "Mi local", Tag = (int?)localSesion?.IdLocal });
            _cboLocal.IsEnabled = false;
        }
        _cboLocal.SelectedIndex = 0;
        _cboLocal.SelectionChanged += (_, _) => AplicarFiltros();
        fila2.Children.Add(_cboLocal);

        // Separador + filtro de confirmación de entrega — por defecto solo Pendientes, para
        // que el cajero vea directamente lo que falta procesar sin el ruido de las
        // solicitudes ya confirmadas (que se acumulan y no requieren ninguna acción más).
        fila2.Children.Add(new Border {
            Width = 1, Background = BrBorde, Margin = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Stretch
        });
        AgregarLblFiltro("Confirmación:");
        _cboConfirmacion = new ComboBox { Width = 130, Height = 28, FontSize = 11.5 };
        _cboConfirmacion.Items.Add(new ComboBoxItem { Content = "Pendientes",  Tag = ConfirmacionEntregaEstado.Pendiente });
        _cboConfirmacion.Items.Add(new ComboBoxItem { Content = "Confirmadas", Tag = ConfirmacionEntregaEstado.Confirmada });
        _cboConfirmacion.Items.Add(new ComboBoxItem { Content = "Todos",       Tag = (ConfirmacionEntregaEstado?)null });
        _cboConfirmacion.SelectedIndex = 0;
        _cboConfirmacion.SelectionChanged += (_, _) => AplicarFiltros();
        fila2.Children.Add(_cboConfirmacion);

        barraVert.Children.Add(fila2);
        searchBar.Child = barraVert;
        Grid.SetRow(searchBar, 1);
        root.Children.Add(searchBar);

        // ── DataGrid moderno ──────────────────────────────────────────────────
        var gridWrap = new Border {
            Margin = new Thickness(12, 10, 12, 0),
            Background = BrBlanco,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true
        };
        gridWrap.Effect = new System.Windows.Media.Effects.DropShadowEffect {
            BlurRadius = 8, ShadowDepth = 1, Opacity = 0.08,
            Color = System.Windows.Media.Color.FromRgb(0,0,0)
        };

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            RowHeight = 30, ColumnHeaderHeight = 30, FontSize = 10.5,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = BrBorde,
            Background = BrBlanco,
            AlternatingRowBackground = BrAlt,
            BorderThickness = new Thickness(0),
            RowBackground = BrBlanco,
            SelectionUnit = DataGridSelectionUnit.FullRow,
        };

        var colHdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, BrPrimary));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, BrBlanco));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 10.0));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(6, 0, 6, 0)));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
        colHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderBrushProperty, BrPrimDark));
        _grid.ColumnHeaderStyle = colHdrStyle;

        // RowStyle: sin bordes ni foco visual
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(DataGridRow.FocusVisualStyleProperty, null));
        rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
        _grid.RowStyle = rowStyle;

        // CellStyle: reemplaza el ControlTemplate para eliminar por completo el borde de foco/selección
        var cellTemplate = new ControlTemplate(typeof(DataGridCell));
        var cellBorderFactory = new FrameworkElementFactory(typeof(Border));
        cellBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0));
        cellBorderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        cellBorderFactory.AppendChild(contentFactory);
        cellTemplate.VisualTree = cellBorderFactory;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        cellStyle.Setters.Add(new Setter(DataGridCell.TemplateProperty, cellTemplate));
        cellStyle.Setters.Add(new Setter(DataGridCell.VerticalAlignmentProperty, VerticalAlignment.Stretch));
        _grid.CellStyle = cellStyle;

        var txtStyle = new Style(typeof(TextBlock));
        txtStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        txtStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6, 0, 6, 0)));

        _grid.Columns.Add(new DataGridTextColumn {
            Header = "N° Solicitud",
            Binding = new System.Windows.Data.Binding("Numero"),
            Width = 95, ElementStyle = txtStyle
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Local",
            Binding = new System.Windows.Data.Binding("LocalNombre"),
            Width = 100, ElementStyle = txtStyle
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Cliente",
            Binding = new System.Windows.Data.Binding("ClienteNombre"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            ElementStyle = txtStyle
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Vendedor",
            Binding = new System.Windows.Data.Binding("VendedorNombre"),
            Width = 120, ElementStyle = txtStyle
        });

        // Columna Estado — chip con color via binding+converter (no depende del árbol visual)
        var estadoTemplate = new DataTemplate();
        var chipFactory = new FrameworkElementFactory(typeof(Border));
        chipFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        chipFactory.SetValue(Border.PaddingProperty, new Thickness(7, 1, 7, 1));
        chipFactory.SetValue(Border.MarginProperty, new Thickness(4, 4, 4, 4));
        chipFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        // Background bound al campo Estado a través del converter — siempre correcto al hacer scroll
        chipFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Estado") {
            Converter = EstadoChipConverter.Instance
        });
        var chipText = new FrameworkElementFactory(typeof(TextBlock));
        chipText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Estado"));
        chipText.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        chipText.SetValue(TextBlock.FontSizeProperty, 9.0);
        chipText.SetValue(TextBlock.ForegroundProperty, BrBlanco);
        chipText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        chipFactory.AppendChild(chipText);
        estadoTemplate.VisualTree = chipFactory;
        _grid.Columns.Add(new DataGridTemplateColumn {
            Header = "Estado", CellTemplate = estadoTemplate, Width = 85
        });

        // Con hora — antes solo mostraba la fecha, y con varias solicitudes del mismo
        // cliente/día (caso frecuente) no se podía distinguir cuál era cuál de un vistazo.
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Fecha",
            Binding = new System.Windows.Data.Binding("FechaSolicitud") { StringFormat = "dd/MM/yyyy HH:mm" },
            Width = 115, ElementStyle = txtStyle
        });
        // Fecha desde la que empiezan a correr las cuotas (campo "FECHA FIGURAR" al cargar la
        // solicitud) — distinta de "Fecha" (cuándo se cargó). Antes solo se veía dentro del
        // detalle de cada solicitud; se pidió como columna aparte acá en el listado.
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Fecha Figurar",
            Binding = new System.Windows.Data.Binding("FechaFigurar") { StringFormat = "dd/MM/yyyy", TargetNullValue = "—" },
            Width = 90, ElementStyle = txtStyle
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Total (Gs.)",
            Binding = new System.Windows.Data.Binding("TotalVenta") { StringFormat = "N0" },
            Width = 90, ElementStyle = txtStyle
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Monto Entrega",
            Binding = new System.Windows.Data.Binding("Entrega") { StringFormat = "N0" },
            Width = 95, ElementStyle = txtStyle
        });

        // Solo tiene sentido cuando EstadoNum==1 (Aprobado) — distingue "aprobada, esperando
        // que alguien confirme la entrega" (venta y caja aún sin generar) de "ya confirmada"
        // (ver flujo de dos pasos en ConfirmarEntregaAsync). En Verificar/Rechazado no aplica.
        var confEntTemplate = new DataTemplate();
        var confEntChip = new FrameworkElementFactory(typeof(Border));
        confEntChip.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        confEntChip.SetValue(Border.PaddingProperty, new Thickness(7, 1, 7, 1));
        confEntChip.SetValue(Border.MarginProperty, new Thickness(4, 4, 4, 4));
        confEntChip.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        confEntChip.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(".") {
            Converter = ConfirmacionEntregaChipConverter.Instance
        });
        var confEntText = new FrameworkElementFactory(typeof(TextBlock));
        confEntText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(".") {
            Converter = ConfirmacionEntregaTextoConverter.Instance
        });
        confEntText.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        confEntText.SetValue(TextBlock.FontSizeProperty, 9.0);
        confEntText.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(".") {
            Converter = ConfirmacionEntregaColorTextoConverter.Instance
        });
        confEntText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        confEntChip.AppendChild(confEntText);
        confEntTemplate.VisualTree = confEntChip;
        _grid.Columns.Add(new DataGridTemplateColumn {
            Header = "Confirmación de Entrega", CellTemplate = confEntTemplate, Width = 125
        });

        // Columna acción
        var btnColTemplate = new DataTemplate();
        var btnFactory = new FrameworkElementFactory(typeof(Button));
        btnFactory.SetValue(Button.ContentProperty, "Ver detalle →");
        btnFactory.SetValue(Button.HeightProperty, 22.0);
        btnFactory.SetValue(Button.PaddingProperty, new Thickness(7, 0, 7, 0));
        btnFactory.SetValue(Button.MarginProperty, new Thickness(4, 4, 4, 4));
        btnFactory.SetValue(Button.BackgroundProperty, BrPrimary);
        btnFactory.SetValue(Button.ForegroundProperty, BrBlanco);
        btnFactory.SetValue(Button.CursorProperty, System.Windows.Input.Cursors.Hand);
        btnFactory.SetValue(Button.FontSizeProperty, 9.5);
        btnFactory.SetValue(Button.FontWeightProperty, FontWeights.SemiBold);
        btnFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        btnFactory.AddHandler(Button.ClickEvent, new System.Windows.RoutedEventHandler(OnVerDetalle));
        btnColTemplate.VisualTree = btnFactory;
        _grid.Columns.Add(new DataGridTemplateColumn { Header = "Acción", CellTemplate = btnColTemplate, Width = 90 });

        _grid.LoadingRow += OnLoadingRow;
        _grid.MouseDoubleClick += OnGridDoubleClick;
        gridWrap.Child = _grid;
        Grid.SetRow(gridWrap, 2);
        root.Children.Add(gridWrap);

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new Border {
            Background = BrBlanco, Padding = new Thickness(12, 8, 12, 8),
            BorderBrush = BrBorde, BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 8, 0, 0)
        };
        footer.Effect = new System.Windows.Media.Effects.DropShadowEffect {
            BlurRadius = 6, ShadowDepth = -2, Opacity = 0.07,
            Color = System.Windows.Media.Color.FromRgb(0,0,0)
        };
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // print buttons
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // pagination center
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // close

        // Botones de impresión (izquierda)
        var printPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var btnPrevia  = MakeBtn("Vista previa",  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 65, 81)));
        var btnImpr    = MakeBtn("Imprimir",       new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(27, 94, 32)));
        var btnImprTodo = MakeBtn("Imprimir todo", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 71,161)));
        btnPrevia.Click   += (_, _) => OnVistaPrevia();
        btnImpr.Click     += (_, _) => OnImprimir(soloFiltrados: true);
        btnImprTodo.Click += (_, _) => OnImprimir(soloFiltrados: false);
        printPanel.Children.Add(btnPrevia);
        printPanel.Children.Add(btnImpr);
        printPanel.Children.Add(btnImprTodo);
        Grid.SetColumn(printPanel, 0);

        // Paginación (centro)
        var pagPanel = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _btnAnterior  = MakeBtn("◀", BrGris); _btnAnterior.Width = 36; _btnAnterior.Margin = new Thickness(0,0,4,0);
        _lblPagInfo   = new TextBlock {
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55,65,81)),
            Margin = new Thickness(4,0,4,0), MinWidth = 120, TextAlignment = TextAlignment.Center
        };
        _btnSiguiente = MakeBtn("▶", BrGris); _btnSiguiente.Width = 36; _btnSiguiente.Margin = new Thickness(4,0,0,0);
        _btnAnterior.Click  += (_, _) => { _paginaActual--; MostrarPagina(); };
        _btnSiguiente.Click += (_, _) => { _paginaActual++; MostrarPagina(); };
        pagPanel.Children.Add(_btnAnterior);
        pagPanel.Children.Add(_lblPagInfo);
        pagPanel.Children.Add(_btnSiguiente);
        Grid.SetColumn(pagPanel, 1);

        var btnCerrar = MakeBtn("✕  Cerrar", BrGris);
        btnCerrar.Click += (_, _) => Close();
        Grid.SetColumn(btnCerrar, 2);

        footerGrid.Children.Add(printPanel);
        footerGrid.Children.Add(pagPanel);
        footerGrid.Children.Add(btnCerrar);
        footer.Child = footerGrid;
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;
    }

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(17, 24, 39));
    }

    private void AplicarFiltros()
    {
        var filtro = _txtFiltro?.Text.Trim() ?? "";
        var desde  = _dpDesde?.SelectedDate;
        var hasta  = _dpHasta?.SelectedDate;
        var idLocalFiltro = (_cboLocal?.SelectedItem as ComboBoxItem)?.Tag as int?;

        IEnumerable<SolicitudItem> lista = _todosItems;

        if (idLocalFiltro.HasValue)
            lista = lista.Where(x => x.IdLocal == idLocalFiltro.Value);

        if (!string.IsNullOrWhiteSpace(filtro))
            lista = lista.Where(x =>
                x.Numero.Contains(filtro, StringComparison.OrdinalIgnoreCase)         ||
                x.ClienteNombre.Contains(filtro, StringComparison.OrdinalIgnoreCase)  ||
                x.LocalNombre.Contains(filtro, StringComparison.OrdinalIgnoreCase)    ||
                x.VendedorNombre.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                x.Estado.Contains(filtro, StringComparison.OrdinalIgnoreCase));

        if (desde.HasValue)
            lista = lista.Where(x => x.FechaSolicitud.Date >= desde.Value.Date);
        if (hasta.HasValue)
            lista = lista.Where(x => x.FechaSolicitud.Date <= hasta.Value.Date);

        // Filtro por defecto: solo Pendientes de confirmar entrega — las Confirmadas ya no
        // requieren ninguna acción y se ocultan para no acumular ruido visual. Las
        // solicitudes que todavía no llegaron a "Aceptado" (NoAplica, sin chip de
        // Confirmación) siempre se muestran, sin importar este filtro — no tiene sentido
        // ocultar una solicitud recién creada por un filtro pensado solo para las ya
        // aprobadas.
        var filtroConfirmacion = (_cboConfirmacion?.SelectedItem as ComboBoxItem)?.Tag as ConfirmacionEntregaEstado?;
        if (filtroConfirmacion.HasValue)
            lista = lista.Where(x =>
            {
                var estadoConf = ConfirmacionEntregaHelper.Calcular(x);
                return estadoConf == ConfirmacionEntregaEstado.NoAplica || estadoConf == filtroConfirmacion.Value;
            });

        var orden = (_cboOrden?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "desc_fecha";
        lista = orden switch {
            "asc_fecha"  => lista.OrderBy(x => x.FechaSolicitud),
            "desc_total" => lista.OrderByDescending(x => x.TotalVenta),
            "estado"     => lista.OrderBy(x => x.Estado).ThenByDescending(x => x.FechaSolicitud),
            _            => lista.OrderByDescending(x => x.FechaSolicitud),
        };

        _itemsFiltrados = lista.ToList();
        _paginaActual   = 1;
        MostrarPagina();
    }

    private void MostrarPagina()
    {
        int total  = _itemsFiltrados.Count;
        int pgSize = _porPagina == int.MaxValue ? Math.Max(total, 1) : _porPagina;
        int totalPags = Math.Max(1, (total + pgSize - 1) / pgSize);
        _paginaActual = Math.Clamp(_paginaActual, 1, totalPags);

        var paginado = _itemsFiltrados
            .Skip((_paginaActual - 1) * pgSize)
            .Take(pgSize)
            .ToList();

        _grid.ItemsSource = paginado;

        _lblConteo.Text = total == _todosItems.Count
            ? $"{total} solicitudes"
            : $"{total} de {_todosItems.Count} solicitudes";

        if (_lblPagInfo != null)
            _lblPagInfo.Text = _porPagina == int.MaxValue
                ? $"Todos  ({total} registros)"
                : $"Página {_paginaActual} de {totalPags}  ({total} registros)";
        if (_btnAnterior  != null) _btnAnterior.IsEnabled  = _paginaActual > 1;
        if (_btnSiguiente != null) _btnSiguiente.IsEnabled = _paginaActual < totalPags;
    }

    private void OnVistaPrevia()
    {
        var pag = BuildSolicitudPagina(_itemsFiltrados);
        var w = new SolicitudPreviewWindow(pag) { Owner = this };
        w.ShowDialog();
    }

    private void OnImprimir(bool soloFiltrados)
    {
        var lista = soloFiltrados ? _itemsFiltrados : _todosItems;
        var pag = BuildSolicitudPagina(lista);
        SolicitudImpresora.Imprimir(pag, this);
    }

    private SolicitudPagina BuildSolicitudPagina(List<SolicitudItem> lista)
    {
        var filtro = _txtFiltro?.Text.Trim() ?? "";
        var desde  = _dpDesde?.SelectedDate;
        var hasta  = _dpHasta?.SelectedDate;
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro)) partes.Add($"Búsqueda: \"{filtro}\"");
        if (desde.HasValue) partes.Add($"Desde: {desde.Value:dd/MM/yyyy}");
        if (hasta.HasValue) partes.Add($"Hasta: {hasta.Value:dd/MM/yyyy}");
        return new SolicitudPagina {
            Filas    = lista,
            Filtro   = partes.Count > 0 ? string.Join("   |   ", partes) : "",
            FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "",
            LogoPath = ArticulosPagina.ResolverLogoPath(),
        };
    }

    private static Button MakeBtn(string text, System.Windows.Media.SolidColorBrush bg) => new Button {
        Content = text, Height = 32, Padding = new Thickness(14, 0, 14, 0),
        Margin = new Thickness(0, 0, 6, 0),
        Background = bg, Foreground = BrBlanco,
        BorderThickness = new Thickness(0),
        Cursor = System.Windows.Input.Cursors.Hand,
        FontSize = 12.5, FontWeight = FontWeights.SemiBold
    };

    private async void OnVerDetalle(object sender, System.Windows.RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not SolicitudItem item) return;
        await AbrirDetalle(item);
    }

    // Doble clic sobre cualquier fila de la grilla abre el mismo detalle que "Ver detalle →"
    // — pedido explícito, para no depender de apuntar exacto al botón de la columna Acción.
    private async void OnGridDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_grid.SelectedItem is not SolicitudItem item) return;
        await AbrirDetalle(item);
    }

    private async Task AbrirDetalle(SolicitudItem item)
    {
        var estadoAntes = item.EstadoNum;
        var ventaGeneradaAntes = item.VentaGenerada;
        if (item.EstadoNum == 1)
        {
            using var conn = _db.Create();
            item.VentaGenerada = await conn.ExecuteScalarAsync<bool>(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM CABECERA_SALES WHERE NSOLICITUD = @nsol) THEN 1 ELSE 0 END",
                new { nsol = item.Numero });
        }
        // Show() en vez de ShowDialog() — pedido explícito: este modal no debe bloquear poder
        // abrir otras ventanas del sistema mientras está abierto (con Show(), Owner NO
        // bloquea nada — eso solo lo hace ShowDialog()). Owner SÍ hace falta igual: sin él,
        // esta ventana queda "huérfana" y Windows la minimiza sola en cuanto otra ventana del
        // mismo proceso toma el foco (bug real detectado: se minimizaba espontáneamente al
        // abrir cualquier otro módulo desde el menú). El costo conocido de tener Owner: esta
        // ficha no puede pasar "por encima" de VisorSolicitudesWindow con un clic (Windows
        // mantiene toda ventana owned encima de su Owner) — aceptado a cambio de que no se
        // minimice sola. El refresco de la grilla que antes se hacía al volver de
        // ShowDialog() ahora se hace en el evento Closed, que dispara igual sea modal o no.
        var w = new DetalleSolicitudWindow(item, _db) { Owner = this };
        w.Closed += async (_, _) =>
        {
            // Sin este Activate() explícito, cerrar la ficha (con foco en ella) a veces deja
            // el proceso sin ninguna ventana activa y Windows minimiza TODO el programa (bug
            // real reportado desde el mismo flujo en el dashboard) — no hay garantía de que
            // WPF le devuelva el foco al Owner solo por cerrarse la ventana hija.
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            // Confirmar Entrega no cambia EstadoNum (sigue en 1/Aprobado antes y después) —
            // solo cambia VentaGenerada. Sin este segundo chequeo, la grilla no se refrescaba
            // sola al cerrar el detalle tras confirmar, y el chip seguía mostrando "Pendiente"
            // hasta que el usuario apretaba "Refrescar" a mano.
            if (item.EstadoNum != estadoAntes || item.VentaGenerada != ventaGeneradaAntes)
                await Cargar();
        };
        w.Show();
    }

    private async Task Cargar()
    {
        try
        {
            using var conn = _db.Create();
            IEnumerable<dynamic> rows;

            // Poblar el filtro de locales la primera vez — solo si el usuario puede ver
            // todos los locales (ver combo en BuildUI: para un usuario normal el combo ya
            // queda fijo con un único ítem, sin necesidad de agregar el resto).
            var puedeVerTodosCarga = SessionService.Instance.UsuarioActual?.PuedeVerTodosLosLocales == true;
            if (puedeVerTodosCarga && _cboLocal != null && _cboLocal.Items.Count == 1)
            {
                var locales = await conn.QueryAsync<(int Id, string Nombre)>(
                    "SELECT ID_LOCAL as Id, NOMBRE as Nombre FROM LOCALES ORDER BY NOMBRE");
                foreach (var loc in locales)
                    _cboLocal.Items.Add(new ComboBoxItem { Content = loc.Nombre, Tag = (int?)loc.Id });
            }

            // Reemplaza EXEC CARGAR_REV_SOL__VISOR_ADMIN_CS (SP legado) — traía "TOP 100"
            // ordenado ascendente por fecha, un límite que se volvió insuficiente: con más de
            // 100 solicitudes pendientes/aceptadas acumuladas sin resolver, las MÁS NUEVAS
            // (justo las que importa ver primero) quedaban totalmente fuera del listado, sin
            // ningún filtro de fecha visible que lo explicara — bug real reportado: "están
            // cargando solicitud y no está apareciendo en producción" (2 solicitudes de Tavai
            // cargadas el mismo día, posiciones 107 y 110 de 110 pendientes).
            //
            // Una sola consulta con los JOINs ya incluidos, en vez de traer los IDSOLICITUD
            // primero y después pedir el detalle de a UNO POR UNO dentro del foreach (patrón
            // N+1: con el TOP 100 ya quitado, esto pasó de a lo sumo 100 round-trips extra a
            // 110+ y creciendo sin techo — bug real reportado como lentitud tras el fix
            // anterior: "se está volviendo lento tarda mucho en cargar").
            rows = await conn.QueryAsync<dynamic>(
                "SELECT s.IDSOLICITUD, s.NUMERO, s.ID_LOCAL AS IdLocal, " +
                "CASE WHEN s.ESTADO = 0 THEN 'Verificar' WHEN s.ESTADO = 1 THEN 'Aceptado' ELSE 'Rechazado' END AS ESTADO, " +
                "s.FECHA_SOLICITUD AS FechaSolCruda, " +
                "cl.NOMBRE_CLIENTE as NomCli, u.NOMBRE_USUARIO as NomVend, l.NOMBRE as NomLocal, " +
                "s.TOTALSALE as Total, s.TOTALENTREGA as Entrega, s.CANTCUOTAS as Cuotas, " +
                "s.ESTADO as EstadoNum, s.FECHA_COBRO as FechaCobro, " +
                "CASE WHEN EXISTS (SELECT 1 FROM CABECERA_SALES cs WHERE cs.NSOLICITUD = s.NUMERO) THEN 1 ELSE 0 END as VentaGenerada " +
                "FROM CAB_SOL_SALES s " +
                "LEFT JOIN CLIENTES cl ON s.ID_CLIENTE = cl.ID_CLIENTE " +
                "LEFT JOIN USUARIOS u  ON s.ID_USUARIO = u.ID_USUARIO " +
                "LEFT JOIN LOCALES  l  ON s.ID_LOCAL   = l.ID_LOCAL " +
                "WHERE s.ESTADO < 2 ORDER BY s.FECHA_SOLICITUD ASC");

            var items = new List<SolicitudItem>();
            foreach (var r in rows)
            {
                int    idSol = (int)r.IDSOLICITUD;
                string nro   = ((string?)r.NUMERO) ?? idSol.ToString();
                string estado = ((string?)r.ESTADO) ?? "—";
                DateTime fecha = (DateTime?)r.FechaSolCruda ?? DateTime.MinValue;

                items.Add(new SolicitudItem {
                    IdSolicitud    = idSol,
                    IdLocal        = (int?)r.IdLocal ?? 0,
                    Numero         = nro,
                    LocalNombre    = (string?)r.NomLocal ?? "—",
                    ClienteNombre  = (string?)r.NomCli   ?? "—",
                    VendedorNombre = (string?)r.NomVend  ?? "—",
                    Estado         = estado,
                    FechaSolicitud = fecha,
                    FechaFigurar   = (DateTime?)r.FechaCobro,
                    TotalVenta     = (decimal?)r.Total   ?? 0,
                    Entrega        = (decimal?)r.Entrega ?? 0,
                    Cuotas         = (int?)r.Cuotas       ?? 0,
                    EstadoNum      = (byte?)r.EstadoNum   ?? (byte)0,
                    VentaGenerada  = (byte?)r.VentaGenerada == 1,
                });
            }

            _todosItems = items;
            AplicarFiltros();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar solicitudes: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  DETALLE DE SOLICITUD  (vista de aprobación al hacer clic en una fila)
// ══════════════════════════════════════════════════════════════════════════════

public class DetalleSolRow
{
    public string Codigo      { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Precio     { get; set; }
    public decimal Entrega    { get; set; }
    public int    Cuotas      { get; set; }
    public decimal CostoMens  { get; set; }
    public decimal ValorFinal { get; set; }
    public decimal Cantidad   { get; set; }
    public decimal TotalGral  { get; set; }
}

public class DetalleSolicitudWindow : Window
{
    private readonly SolicitudItem        _sol;
    private readonly IDbConnectionFactory _db;

    // Campos dinámicos — cliente
    private TextBlock _tbCliNombre = null!, _tbCliCi = null!, _tbCliDir = null!,
                      _tbCliCel = null!, _tbCliCiudad = null!, _tbCliEcv = null!,
                      _tbCliCredMax = null!, _tbCliSaldo = null!,
                      _tbCliCondicion = null!, _tbCliTipo = null!,
                      _tbCliVencCi = null!, _tbCliConyuge = null!;
    // Campos dinámicos — garante
    private TextBlock _tbGarNombre = null!, _tbGarCi = null!, _tbGarDir = null!,
                      _tbGarCel = null!, _tbGarCiudad = null!,
                      _tbGarEmpresa = null!, _tbGarTelLab = null!, _tbGarEcv = null!;
    // Referencias
    private TextBlock _tbRef1Nom = null!, _tbRef1Tel = null!, _tbRef1Trab = null!;
    private TextBlock _tbRef2Nom = null!, _tbRef2Tel = null!, _tbRef2Trab = null!;
    private TextBlock _tbRefCom1Nom = null!, _tbRefCom1Tel = null!;
    private TextBlock _tbRefCom2Nom = null!, _tbRefCom2Tel = null!;
    // Ingresos/Egresos
    private TextBlock _tbISalario = null!, _tbIHonorario = null!, _tbIConyuge = null!,
                      _tbIOtros = null!, _tbITotal = null!;
    private TextBlock _tbEGasto = null!, _tbECuota = null!, _tbEAlquiler = null!,
                      _tbEOtros = null!, _tbETotal = null!, _tbSaldo = null!;
    // Nota / logística
    private TextBox   _txtNota       = null!;
    // Campos DATOS
    private TextBlock _tbDatAprobado = null!, _tbDatTotal = null!, _tbDatEntrega = null!,
                      _tbDatCuotas  = null!, _tbDatMonto = null!, _tbDatPago    = null!;
    private DataGrid  _gridProductos = null!;
    // Estado interno cargado desde BD
    private string _fotoCedulaCliente = "";
    private int    _idClienteCargado  = 0;
    private int    _idGaranteCargado  = 0;
    private string _nomGaranteCargado = "";
    private byte   _permisoExcepcion  = 0;
    private Button _btnAutorizar      = null!;

    // Paleta idéntica a VentaCreditoWindow (#124E78 header, #1F77B4 acción, #EEF2F7 fondo)
    private static readonly System.Windows.Media.SolidColorBrush BrPrimary  = new(System.Windows.Media.Color.FromRgb( 18,  78, 120));  // #124E78
    private static readonly System.Windows.Media.SolidColorBrush BrPrimDark = new(System.Windows.Media.Color.FromRgb( 12,  55,  88));
    private static readonly System.Windows.Media.SolidColorBrush BrBlanco   = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.SolidColorBrush BrFondo    = new(System.Windows.Media.Color.FromRgb(238, 242, 247));  // #EEF2F7
    private static readonly System.Windows.Media.SolidColorBrush BrCard     = new(System.Windows.Media.Color.FromRgb(255, 255, 255));
    private static readonly System.Windows.Media.SolidColorBrush BrBorde    = new(System.Windows.Media.Color.FromRgb(208, 218, 232));  // #D0DAE8
    private static readonly System.Windows.Media.SolidColorBrush BrSecHead  = new(System.Windows.Media.Color.FromRgb( 18,  78, 120));  // #124E78
    private static readonly System.Windows.Media.SolidColorBrush BrSecBody  = new(System.Windows.Media.Color.FromRgb(255, 255, 255));
    private static readonly System.Windows.Media.SolidColorBrush BrGrisOsc  = new(System.Windows.Media.Color.FromRgb( 90, 107, 124));  // #5A6B7C
    private static readonly System.Windows.Media.SolidColorBrush BrVerde    = new(System.Windows.Media.Color.FromRgb( 30, 110,  66));
    private static readonly System.Windows.Media.SolidColorBrush BrRojo     = new(System.Windows.Media.Color.FromRgb(192,  40,  27));
    private static readonly System.Windows.Media.SolidColorBrush BrAzul     = new(System.Windows.Media.Color.FromRgb( 31, 119, 180));  // #1F77B4
    private static readonly System.Windows.Media.SolidColorBrush BrAmarillo = new(System.Windows.Media.Color.FromRgb(160, 100,   0));
    private static readonly System.Windows.Media.SolidColorBrush BrLabelTxt = new(System.Windows.Media.Color.FromRgb( 90, 107, 124));  // #5A6B7C
    private static readonly System.Windows.Media.SolidColorBrush BrValTxt   = new(System.Windows.Media.Color.FromRgb( 20,  31,  48));  // #141F30

    public DetalleSolicitudWindow(SolicitudItem sol, IDbConnectionFactory db)
    {
        _sol = sol;
        _db  = db;
        Title = $"Solicitud N° {sol.Numero}  —  {sol.ClienteNombre}";
        Width = 1020; Height = 660;
        MinWidth = 860; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 242, 247));
        // Pedido explícito del cliente: esta ventana se abre con Show() (no ShowDialog, ver
        // AbrirDetalle en VisorSolicitudesWindow) para no bloquear poder abrir otras ventanas
        // del sistema mientras está abierta — necesita conservar minimizar/maximizar.
        Tag = App.PermitirMinimizarTag;
        BuildUI();
        Loaded += async (_, _) => await CargarDetalleAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CONSTRUCCIÓN DE UI  —  layout idéntico a VentaCreditoWindow
    // ═══════════════════════════════════════════════════════════════════════
    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });  // Header
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Cuerpo
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });  // Footer

        // ── HEADER ── idéntico a VentaCreditoWindow ──────────────────────────
        var header = new Border { Background = BrPrimary };
        var hg = new Grid { Margin = new Thickness(20, 0, 20, 0) };
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // accent bar
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // título
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // badge Nº
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // badge estado

        // Accent bar vertical azul
        var accentBar = new Border {
            Width = 4, CornerRadius = new CornerRadius(2),
            Background = BrAzul, Margin = new Thickness(0, 12, 14, 12)
        };
        Grid.SetColumn(accentBar, 0); hg.Children.Add(accentBar);

        // Título + subtítulo
        var titleSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleSp.Children.Add(new TextBlock {
            Text = "VERIFICACIÓN DE SOLICITUD",
            FontSize = 17, FontWeight = FontWeights.Bold, Foreground = BrBlanco
        });
        titleSp.Children.Add(new TextBlock {
            Text = _sol.ClienteNombre,
            FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(180, 128, 170, 204)),
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(titleSp, 1); hg.Children.Add(titleSp);

        // Badge Nº solicitud (#0E2A40)
        var nroBadge = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(14, 42, 64)),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 0, 10, 0),
            Height = 40, VerticalAlignment = VerticalAlignment.Center
        };
        var nroSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nroSp.Children.Add(new TextBlock {
            Text = "SOLICITUD N°", FontSize = 8, FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(90, 138, 170)),
            TextAlignment = TextAlignment.Center
        });
        nroSp.Children.Add(new TextBlock {
            Text = _sol.Numero, FontSize = 15, FontWeight = FontWeights.Bold,
            Foreground = BrBlanco, TextAlignment = TextAlignment.Center, MinWidth = 64
        });
        nroBadge.Child = nroSp;
        Grid.SetColumn(nroBadge, 2); hg.Children.Add(nroBadge);

        // Badge estado — "APROBADO" no distinguía si la venta ya fue confirmada/facturada o
        // todavía está pendiente de ese segundo paso (ver ConfirmarEntregaAsync); ahora dice
        // "FACTURADO" cuando ya se generó la venta, para que se note de un vistazo sin tener
        // que leer el banner amarillo/verde de más abajo.
        var textoEstadoHeader = (_sol.EstadoNum == 1 && _sol.VentaGenerada) ? "FACTURADO" : _sol.Estado;
        var chipEstado = BuildChipEstadoHeader(textoEstadoHeader);
        chipEstado.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(chipEstado, 4); hg.Children.Add(chipEstado);

        header.Child = hg;
        Grid.SetRow(header, 0); root.Children.Add(header);

        // ── CUERPO ── misma estructura 210px izq + * derecha ─────────────────
        var body = new Grid { Margin = new Thickness(12, 10, 12, 6) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });  // Panel izq fijo
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // tabs

        // ─ Panel izquierdo ─
        var leftScroll = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var leftStack = new StackPanel();

        // Card info solicitud
        var cardSol = new Border {
            Background = BrCard, CornerRadius = new CornerRadius(10),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 8)
        };
        var cardSolSp = new StackPanel();
        cardSolSp.Children.Add(new TextBlock {
            Text = "SOLICITUD", FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = BrGrisOsc, Margin = new Thickness(0, 0, 0, 10)
        });

        // Vendedor
        cardSolSp.Children.Add(ChipLbl("VENDEDOR"));
        cardSolSp.Children.Add(ChipVal(_sol.VendedorNombre, Margin: new Thickness(0, 0, 0, 8)));

        // Fecha solicitud
        cardSolSp.Children.Add(ChipLbl("FECHA SOLICITUD"));
        cardSolSp.Children.Add(ChipVal(_sol.FechaSolicitud.ToString("dd/MM/yyyy"), Margin: new Thickness(0, 0, 0, 8)));

        // Local
        cardSolSp.Children.Add(ChipLbl("LOCAL"));
        cardSolSp.Children.Add(ChipVal(_sol.LocalNombre, Margin: new Thickness(0, 0, 0, 8)));

        // Resumen financiero en card izquierdo
        cardSolSp.Children.Add(new Border { Height = 1, Background = BrBorde, Margin = new Thickness(0, 4, 0, 8) });
        cardSolSp.Children.Add(new TextBlock {
            Text = "RESUMEN", FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = BrGrisOsc, Margin = new Thickness(0, 0, 0, 6)
        });
        cardSolSp.Children.Add(ChipLbl("TOTAL VENTA"));
        cardSolSp.Children.Add(ChipVal(_sol.TotalVenta.ToString("N0"), bold: true, color: BrPrimary, Margin: new Thickness(0, 0, 0, 6)));
        cardSolSp.Children.Add(ChipLbl("ENTREGA"));
        cardSolSp.Children.Add(ChipVal(_sol.Entrega.ToString("N0"), Margin: new Thickness(0, 0, 0, 6)));
        cardSolSp.Children.Add(ChipLbl("CUOTAS"));
        cardSolSp.Children.Add(ChipVal(_sol.Cuotas.ToString()));

        cardSol.Child = cardSolSp;
        leftStack.Children.Add(cardSol);

        // Botón Autorizar Venta (visible solo para admins, o cuando ya está autorizado)
        _btnAutorizar = new Button {
            Height = 36, Margin = new Thickness(0, 0, 0, 8),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, FontSize = 12, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ActualizarBtnAutorizar();
        _btnAutorizar.Click += async (_, _) => await AutorizarExcepcionAsync();
        leftStack.Children.Add(_btnAutorizar);

        leftScroll.Content = leftStack;
        Grid.SetColumn(leftScroll, 0); body.Children.Add(leftScroll);

        // ─ Área derecha: TabControl con misma paleta ─
        var tabs = new TabControl {
            Background = BrCard,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Padding = new Thickness(0)
        };

        // Estilo de tabs igual a VentaCreditoWindow
        var tabItemStyle = new Style(typeof(TabItem));
        tabItemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, Brushes.Transparent));
        tabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, BrGrisOsc));
        tabItemStyle.Setters.Add(new Setter(TabItem.BorderThicknessProperty, new Thickness(0)));
        tabItemStyle.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(18, 10, 18, 10)));
        tabItemStyle.Setters.Add(new Setter(TabItem.FontSizeProperty, 12.0));
        tabItemStyle.Setters.Add(new Setter(TabItem.FontWeightProperty, FontWeights.SemiBold));
        tabItemStyle.Setters.Add(new Setter(TabItem.CursorProperty, Cursors.Hand));
        var tabTemplate = new ControlTemplate(typeof(TabItem));
        var tabBorderFactory = new FrameworkElementFactory(typeof(Border));
        tabBorderFactory.Name = "TabBorder";
        tabBorderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        tabBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 3));
        tabBorderFactory.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        tabBorderFactory.SetValue(Border.PaddingProperty, new Thickness(18, 10, 18, 10));
        var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        cpFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        tabBorderFactory.AppendChild(cpFactory);
        tabTemplate.VisualTree = tabBorderFactory;
        var triggerSel = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        triggerSel.Setters.Add(new Setter(Border.BorderBrushProperty, BrAzul) { TargetName = "TabBorder" });
        triggerSel.Setters.Add(new Setter(Border.BackgroundProperty, BrCard) { TargetName = "TabBorder" });
        triggerSel.Setters.Add(new Setter(TabItem.ForegroundProperty, BrPrimary));
        tabTemplate.Triggers.Add(triggerSel);
        tabItemStyle.Setters.Add(new Setter(TabItem.TemplateProperty, tabTemplate));
        tabs.ItemContainerStyle = tabItemStyle;

        // ── Banner de estado (aprobado/rechazado) dentro del tab ─
        UIElement? bannerEstado = null;
        if (_sol.EstadoNum != 0)
        {
            bool ap = _sol.EstadoNum == 1;
            bool faltaEntrega = ap && !_sol.VentaGenerada;
            bannerEstado = new Border {
                Background = faltaEntrega
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 244, 215))
                    : ap
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231))
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 226, 226)),
                BorderBrush = faltaEntrega
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8))
                    : ap
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(134, 239, 172))
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 165, 165)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 0, 8),
                Child = new TextBlock {
                    Text = faltaEntrega
                        ? "⏳  Solicitud APROBADA — falta confirmar la entrega para generar la venta y el movimiento de caja."
                        : ap
                            ? "✔  Solicitud APROBADA — venta generada. Las cuotas ya se crearon" +
                              (_sol.FechaFigurar.HasValue ? $" a partir del {_sol.FechaFigurar:dd/MM/yyyy} (Fecha Figurar)." : ".") +
                              " Solo consulta."
                            : "✘  Solicitud RECHAZADA — solo consulta.",
                    FontWeight = FontWeights.SemiBold, FontSize = 12,
                    Foreground = faltaEntrega
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 70, 0))
                        : ap
                            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 101, 52))
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 27, 27))
                }
            };
        }

        // Tab: Cliente
        var tabCliente = new TabItem { Header = "👤 Cliente" };
        var cliScroll = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var cliSp = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        if (bannerEstado != null) cliSp.Children.Add(bannerEstado);
        cliSp.Children.Add(SecCard("Cliente", BuildClienteGrid()));
        cliScroll.Content = cliSp;
        tabCliente.Content = cliScroll;
        tabs.Items.Add(tabCliente);

        // Tab: Garante + Referencias
        var tabGar = new TabItem { Header = "🤝 Garante / Ref." };
        var garScroll = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var garSp = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        garSp.Children.Add(SecCard("Garante",     BuildGaranteGrid()));
        garSp.Children.Add(SecCard("Referencias", BuildReferenciasGrid()));
        garScroll.Content = garSp;
        tabGar.Content = garScroll;
        tabs.Items.Add(tabGar);

        // Tab: Ingresos / Egresos
        var tabIE = new TabItem { Header = "💰 Ing. / Egr." };
        var ieScroll = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var ieSp = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        ieSp.Children.Add(SecCard("Ingresos / Egresos", BuildIngresosEgresosGrid()));

        // Nota
        _txtNota = new TextBox {
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Height = 70, Padding = new Thickness(8),
            Background = BrCard, BorderBrush = BrBorde, BorderThickness = new Thickness(1), FontSize = 11
        };
        ieSp.Children.Add(SecCard("Nota / Observación", _txtNota));
        ieScroll.Content = ieSp;
        tabIE.Content = ieScroll;
        tabs.Items.Add(tabIE);

        // Tab: Mercaderías
        var tabMerc = new TabItem { Header = "📦 Mercaderías" };
        var mercScroll = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var mercSp = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        mercSp.Children.Add(SecCard("Mercaderías", BuildMercaderiasContent()));
        mercSp.Children.Add(BuildSeccionDatos());
        mercScroll.Content = mercSp;
        tabMerc.Content = mercScroll;
        tabs.Items.Add(tabMerc);

        Grid.SetColumn(tabs, 2); body.Children.Add(tabs);

        Grid.SetRow(body, 1); root.Children.Add(body);

        // ── FOOTER ── idéntico a VentaCreditoWindow ──────────────────────────
        var footer = new Border {
            Background = BrCard, BorderBrush = BrBorde,
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        var footerG = new Grid { Margin = new Thickness(16, 0, 16, 0) };
        footerG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // btns acción izq
        footerG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // btns derecha

        if (_sol.EstadoNum == 0)
        {
            var accSp = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btnVerif    = MakeBtn("🔍 Verif. Datos", BrAzul);
            var btnRechazar = MakeBtn("✕ Rechazar",      BrRojo);
            var btnAceptar  = MakeBtn("✔ Aceptar",       BrVerde);
            btnVerif.Click    += async (_, _) => await CambiarEstadoAsync(0);
            btnRechazar.Click += async (_, _) => await CambiarEstadoAsync(2);
            btnAceptar.Click  += async (_, _) => await CambiarEstadoAsync(1);
            accSp.Children.Add(btnVerif);
            accSp.Children.Add(btnRechazar);
            accSp.Children.Add(btnAceptar);
            Grid.SetColumn(accSp, 0); footerG.Children.Add(accSp);
        }
        else if (_sol.EstadoNum == 1 && !_sol.VentaGenerada)
        {
            // Solicitud Aprobada pero todavía sin venta generada — falta el paso de
            // confirmar la entrega (ver ConfirmarEntregaAsync). Sin este botón, una
            // solicitud aprobada bajo el nuevo flujo de dos pasos quedaría sin forma de
            // completarse desde esta pantalla.
            var accSp2 = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Más grande y destacado que los botones secundarios (MakeBtn genérico) — es la
            // única acción pendiente real en esta pantalla, se pedía que no se perdiera entre
            // "Vista previa"/"Imprimir"/"Cerrar" del lado derecho del footer.
            var btnConfirmarEntrega = new Button {
                Content = "💰  Confirmar Entrega", Height = 42, Padding = new Thickness(22, 0, 22, 0),
                Margin = new Thickness(0, 0, 6, 0),
                Background = BrVerde, Foreground = BrBlanco,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                FontSize = 13.5, FontWeight = FontWeights.Bold
            };
            // Caso real (Fatima/Buena Vista, 21/07): el botón seguía habilitado después de un
            // "Confirmar Entrega" exitoso (la ventana no se cerraba lo bastante rápido, o el
            // usuario alcanzaba a hacer otro clic mientras corría el primero) — se confirmó la
            // MISMA solicitud 3 veces, generando 3 celulares vendidos y 3 recibos físicos
            // distintos. Se deshabilita en el primer clic (vuelve a habilitarse solo si falla)
            // para que un segundo clic mientras la primera confirmación todavía está en vuelo
            // no dispare una segunda venta completa.
            btnConfirmarEntrega.Click += async (_, _) => {
                btnConfirmarEntrega.IsEnabled = false;
                try { await ConfirmarEntregaAsync(); }
                finally { btnConfirmarEntrega.IsEnabled = true; }
            };
            accSp2.Children.Add(btnConfirmarEntrega);
            Grid.SetColumn(accSp2, 0); footerG.Children.Add(accSp2);
        }

        var rightBtns = new StackPanel {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnPrevia   = MakeBtn("Vista previa", BrGrisOsc);
        var btnImprimir = MakeBtn("Imprimir",     BrVerde);
        var btnGuardar  = MakeBtn("Guardar",      BrPrimary);
        var btnCerrar   = MakeBtn("✕ Cerrar",     BrRojo);
        btnPrevia.Click   += async (_, _) => { var fd = await BuildFichaDataAsync(); if (fd != null) { var w = new SolicitudFichaPreviewWindow(fd) { Owner = this }; w.ShowDialog(); } };
        btnImprimir.Click += async (_, _) => { var fd = await BuildFichaDataAsync(); if (fd != null) SolicitudFichaImpresora.Imprimir(fd, this); };
        btnGuardar.Click  += async (_, _) => await GuardarAsync();
        btnCerrar.Click   += (_, _) => Close();
        rightBtns.Children.Add(btnPrevia);
        rightBtns.Children.Add(btnImprimir);
        rightBtns.Children.Add(btnGuardar);
        rightBtns.Children.Add(btnCerrar);
        Grid.SetColumn(rightBtns, 2); footerG.Children.Add(rightBtns);

        footer.Child = footerG;
        Grid.SetRow(footer, 2); root.Children.Add(footer);

        Content = root;
    }

    // ── Helpers de UI ────────────────────────────────────────────────────────

    private static TextBlock ChipLbl(string text) => new TextBlock {
        Text = text, FontSize = 9, FontWeight = FontWeights.SemiBold,
        Foreground = BrGrisOsc, Margin = new Thickness(0, 0, 0, 2)
    };

    private static TextBox ChipVal(string text, bool bold = false,
        System.Windows.Media.SolidColorBrush? color = null,
        Thickness? Margin = null) => new TextBox {
        Text = text, IsReadOnly = true, Height = 30,
        Padding = new Thickness(8, 5, 8, 5), FontSize = 11,
        Background = BrCard, BorderBrush = BrBorde, BorderThickness = new Thickness(1),
        Foreground = color ?? BrValTxt,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        Margin = Margin ?? new Thickness(0)
    };

    private static UIElement WithMargin(UIElement el, Thickness m)
    {
        if (el is FrameworkElement fe) fe.Margin = m;
        return el;
    }

    // Card de sección con header azul (para el panel derecho)
    private static Border SecCard(string titulo, UIElement contenido) => new Border {
        Background = BrCard, CornerRadius = new CornerRadius(8),
        BorderBrush = BrBorde, BorderThickness = new Thickness(1),
        Margin = new Thickness(0, 0, 0, 10),
        Child = new StackPanel {
            Children = {
                new Border {
                    Background = BrPrimary, CornerRadius = new CornerRadius(7, 7, 0, 0),
                    Padding = new Thickness(14, 8, 14, 8),
                    Child = new TextBlock {
                        Text = titulo.ToUpperInvariant(),
                        FontSize = 10, FontWeight = FontWeights.Bold,
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(170, 204, 232))
                    }
                },
                new Border {
                    Padding = new Thickness(14, 10, 14, 10),
                    Child = contenido
                }
            }
        }
    };

    private static Border BuildChipEstadoHeader(string estado)
    {
        var (bg, fg) = estado.ToUpperInvariant() switch {
            var s when s.Contains("FACTUR") => (System.Windows.Media.Color.FromRgb( 13,  71, 161), System.Windows.Media.Color.FromRgb(255, 255, 255)),
            var s when s.Contains("APROB")  => (System.Windows.Media.Color.FromRgb(209, 250, 229), System.Windows.Media.Color.FromRgb( 22, 101,  52)),
            var s when s.Contains("RECH")   => (System.Windows.Media.Color.FromRgb(254, 226, 226), System.Windows.Media.Color.FromRgb(153,  27,  27)),
            var s when s.Contains("VERIF")  => (System.Windows.Media.Color.FromRgb(219, 234, 254), System.Windows.Media.Color.FromRgb( 30,  64, 175)),
            _                               => (System.Windows.Media.Color.FromRgb(26,  58,  82),  System.Windows.Media.Color.FromRgb(255, 255, 255)),
        };
        return new Border {
            Background = new System.Windows.Media.SolidColorBrush(bg),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 0, 14, 0),
            Height = 34, VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(0),
            Child = new TextBlock {
                Text = estado.ToUpperInvariant(),
                FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(fg),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static Button MakeBtn(string text, System.Windows.Media.SolidColorBrush bg) => new Button {
        Content = text, Height = 32, Padding = new Thickness(18, 0, 18, 0),
        Margin = new Thickness(0, 0, 8, 0),
        Background = bg, Foreground = BrBlanco,
        FontWeight = FontWeights.SemiBold, FontSize = 13,
        BorderThickness = new Thickness(0),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    // ── Grids de datos ───────────────────────────────────────────────────────

    private Grid BuildClienteGrid()
    {
        var g = new Grid();
        // 3 pares label|valor por fila (6 columnas) — antes eran 5 pares (12 columnas) y cada
        // valor quedaba con tan poco espacio real que textos como "Estado Civil" o ciudades
        // largas se recortaban sin ningún indicador visual (ej. "Soltero" se veía como "So").
        for (int i = 0; i < 6; i++)
            g.ColumnDefinitions.Add(new ColumnDefinition {
                Width = i % 2 == 0 ? new GridLength(100) : new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _tbCliNombre   = Tb(""); _tbCliCi     = Tb(""); _tbCliDir      = Tb("");
        _tbCliCel      = Tb(""); _tbCliCiudad = Tb(""); _tbCliCondicion= Tb("");
        _tbCliCredMax  = Tb(""); _tbCliSaldo  = Tb(""); _tbCliTipo     = Tb("");
        _tbCliEcv      = Tb(""); _tbCliVencCi = Tb(""); _tbCliConyuge  = Tb("");
        var tbCliRuc   = Tb("");  // RUC separado — no reusar _tbCliCiudad

        // Fila 0: nombre Cliente ocupa cols 0-5 (span completo), en fila propia
        var btnCliente = BtnAccion("Cliente", "#1A3A52");
        btnCliente.IsEnabled = false;
        Grid.SetRow(btnCliente, 0); Grid.SetColumn(btnCliente, 0); g.Children.Add(btnCliente);
        Grid.SetRow(_tbCliNombre, 0); Grid.SetColumn(_tbCliNombre, 1);
        Grid.SetColumnSpan(_tbCliNombre, 5); g.Children.Add(_tbCliNombre);

        // Fila 1: C.I / RUC / Dirección
        AddCell(g, "C.I:",       _tbCliCi,  1, 0);
        AddCell(g, "RUC:",       tbCliRuc,  1, 2);
        AddCell(g, "Dirección:", _tbCliDir, 1, 4);

        // Fila 2: Celular / Ciudad / Estado Civil
        AddCell(g, "Celular:",      _tbCliCel,    2, 0);
        AddCell(g, "Ciudad:",       _tbCliCiudad, 2, 2);
        AddCell(g, "Estado Civil:", _tbCliEcv,    2, 4);

        // Fila 3: Condición / Tipo / Cónyuge
        AddCell(g, "Condición:", _tbCliCondicion, 3, 0);
        AddCell(g, "Tipo:",      _tbCliTipo,      3, 2);
        AddCell(g, "Cónyuge:",   _tbCliConyuge,   3, 4);

        // Fila 4: Crédito máx / Saldo actual / Venc. C.I.
        AddCell(g, "Crédito máx:",  _tbCliCredMax, 4, 0);
        AddCell(g, "Saldo actual:", _tbCliSaldo,   4, 2);
        AddCell(g, "Venc. C.I:",    _tbCliVencCi,  4, 4);

        // Fila 5: botones Historial y Ver cédula
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var btnHistorial = BtnAccion("Historial", "#6B7280");
        btnHistorial.Click += (_, _) => MostrarHistorial();
        Grid.SetRow(btnHistorial, 5); Grid.SetColumn(btnHistorial, 0);
        Grid.SetColumnSpan(btnHistorial, 2); g.Children.Add(btnHistorial);

        var btnVerCedula = BtnAccion("Ver cédula", "#3B82F6");
        btnVerCedula.Click += (_, _) => VerCedula();
        Grid.SetRow(btnVerCedula, 5); Grid.SetColumn(btnVerCedula, 2);
        Grid.SetColumnSpan(btnVerCedula, 2); g.Children.Add(btnVerCedula);

        return g;
    }

    private UIElement BuildGaranteGrid()
    {
        _tbGarNombre  = Tb(""); _tbGarCi    = Tb(""); _tbGarDir   = Tb("");
        _tbGarCel     = Tb(""); _tbGarCiudad= Tb(""); _tbGarEmpresa=Tb("");
        _tbGarTelLab  = Tb(""); _tbGarEcv   = Tb("");

        // Fila superior: Nombre (amplio) + CI + Teléfono
        var row0 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var gNom = ChipField("NOMBRE", _tbGarNombre);
        var gCi  = ChipField("C.I.", _tbGarCi);
        var gTel = ChipField("TELÉFONO", _tbGarCel);
        Grid.SetColumn(gNom, 0); row0.Children.Add(gNom);
        Grid.SetColumn(gCi,  2); row0.Children.Add(gCi);
        Grid.SetColumn(gTel, 4); row0.Children.Add(gTel);

        // Fila inferior: Dirección + Ciudad + Empresa + Tel. lab. + E.C.
        var row1 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var gDir = ChipField("DIRECCIÓN", _tbGarDir);
        var gCiu = ChipField("CIUDAD", _tbGarCiudad);
        var gEmp = ChipField("LUGAR DE TRABAJO", _tbGarEmpresa);
        var gTLb = ChipField("TEL. LABORAL", _tbGarTelLab);
        var gEcv = ChipField("ESTADO CIVIL", _tbGarEcv);
        Grid.SetColumn(gDir, 0); row1.Children.Add(gDir);
        Grid.SetColumn(gCiu, 2); row1.Children.Add(gCiu);
        Grid.SetColumn(gEmp, 4); row1.Children.Add(gEmp);
        Grid.SetColumn(gTLb, 6); row1.Children.Add(gTLb);
        Grid.SetColumn(gEcv, 8); row1.Children.Add(gEcv);

        var btnGar = new Button {
            Content = "🔍 Cambiar garante", Height = 28,
            Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 4, 0, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 58, 82)),
            Foreground = BrBlanco, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, FontSize = 11, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        btnGar.Click += (_, _) => MostrarInfoGarante();

        var sp = new StackPanel();
        sp.Children.Add(row0);
        sp.Children.Add(row1);
        sp.Children.Add(btnGar);
        return sp;
    }

    private UIElement BuildReferenciasGrid()
    {
        _tbRef1Nom = Tb(""); _tbRef1Tel = Tb(""); _tbRef1Trab = Tb("");
        _tbRef2Nom = Tb(""); _tbRef2Tel = Tb(""); _tbRef2Trab = Tb("");
        _tbRefCom1Nom = Tb(""); _tbRefCom1Tel = Tb("");
        _tbRefCom2Nom = Tb(""); _tbRefCom2Tel = Tb("");

        var sp = new StackPanel();

        // ─ Referencias Personales (2 columnas) ─
        var rowPers = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        rowPers.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowPers.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        rowPers.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var cardRef1 = BuildRefPersonalCard("REF. PERSONAL 1", _tbRef1Nom, _tbRef1Tel, _tbRef1Trab, () => MostrarInfoRef(1));
        var cardRef2 = BuildRefPersonalCard("REF. PERSONAL 2", _tbRef2Nom, _tbRef2Tel, _tbRef2Trab, () => MostrarInfoRef(2));
        Grid.SetColumn(cardRef1, 0); rowPers.Children.Add(cardRef1);
        Grid.SetColumn(cardRef2, 2); rowPers.Children.Add(cardRef2);
        sp.Children.Add(rowPers);

        // ─ Referencias Comerciales (2 columnas) ─
        var rowCom = new Grid();
        rowCom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowCom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        rowCom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var cardCom1 = BuildRefComercialCard("REF. COMERCIAL 1", _tbRefCom1Nom, _tbRefCom1Tel);
        var cardCom2 = BuildRefComercialCard("REF. COMERCIAL 2", _tbRefCom2Nom, _tbRefCom2Tel);
        Grid.SetColumn(cardCom1, 0); rowCom.Children.Add(cardCom1);
        Grid.SetColumn(cardCom2, 2); rowCom.Children.Add(cardCom2);
        sp.Children.Add(rowCom);
        return sp;
    }

    private static Border BuildRefPersonalCard(string titulo, TextBlock tbNom, TextBlock tbTel, TextBlock tbTrab, Action onClick)
    {
        var btnCambiar = new Button {
            Content = $"🔍 {titulo}", Height = 26,
            Padding = new Thickness(10, 0, 10, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 58, 82)),
            Foreground = BrBlanco, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, FontSize = 10, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8)
        };
        btnCambiar.Click += (_, _) => onClick();

        var fieldsGrid = new Grid();
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var fNom  = ChipField("NOMBRE", tbNom);
        var fTel  = ChipField("CELULAR", tbTel);
        var fTrab = ChipField("LUGAR DE TRABAJO", tbTrab);
        Grid.SetColumn(fNom,  0); fieldsGrid.Children.Add(fNom);
        Grid.SetColumn(fTel,  2); fieldsGrid.Children.Add(fTel);
        Grid.SetColumn(fTrab, 4); fieldsGrid.Children.Add(fTrab);

        var inner = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        inner.Children.Add(btnCambiar);
        inner.Children.Add(fieldsGrid);

        return new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 253)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Child = inner
        };
    }

    private static Border BuildRefComercialCard(string titulo, TextBlock tbNom, TextBlock tbTel)
    {
        var fieldsGrid = new Grid();
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var fNom2 = ChipField("NOMBRE / EMPRESA", tbNom);
        var fTel2 = ChipField("TELÉFONO", tbTel);
        Grid.SetColumn(fNom2, 0); fieldsGrid.Children.Add(fNom2);
        Grid.SetColumn(fTel2, 2); fieldsGrid.Children.Add(fTel2);

        var inner = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
        inner.Children.Add(new TextBlock {
            Text = titulo, FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = BrGrisOsc, Margin = new Thickness(0, 0, 0, 8)
        });
        inner.Children.Add(fieldsGrid);

        return new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 253)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Child = inner
        };
    }

    // ChipField: label encima + valor debajo (mismo estilo que panel izquierdo)
    private static StackPanel ChipField(string label, TextBlock tbVal)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock {
            Text = label, FontSize = 9, FontWeight = FontWeights.SemiBold,
            Foreground = BrGrisOsc, Margin = new Thickness(0, 0, 0, 2)
        });
        tbVal.FontSize   = 11;
        tbVal.Foreground = BrValTxt;
        tbVal.Padding    = new Thickness(8, 5, 8, 5);
        var border = new Border {
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Background = BrCard, Child = tbVal
        };
        sp.Children.Add(border);
        return sp;
    }

    // Botón con círculo rojo + texto (para encabezados de referencias)
    private static Button BtnCirculoRojo(string texto)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new Border
        {
            Width = 13, Height = 13, CornerRadius = new CornerRadius(7),
            Background = BrRojo, Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(new TextBlock
        {
            Text = texto, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = BrGrisOsc, VerticalAlignment = VerticalAlignment.Center
        });
        return new Button
        {
            Content = sp, Background = Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(2),
            Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Left
        };
    }

    private static TextBlock LbField(string texto) =>
        new TextBlock { Text = texto, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = BrGrisOsc, Margin = new Thickness(4, 2, 2, 2),
            VerticalAlignment = VerticalAlignment.Center };

    private void MostrarInfoRef(int numero)
    {
        var win = new BuscadorPersonaWindow(_db) { Owner = this };
        win.Closed += (_, _) =>
        {
            if (win.PersonaSeleccionada == null) return;
            var p = win.PersonaSeleccionada;
            if (numero == 1)
            {
                _tbRef1Nom.Text  = p.Nombre;
                _tbRef1Tel.Text  = p.Telefono;
                _tbRef1Trab.Text = p.Empresa;
            }
            else
            {
                _tbRef2Nom.Text  = p.Nombre;
                _tbRef2Tel.Text  = p.Telefono;
                _tbRef2Trab.Text = p.Empresa;
            }
        };
        win.Show();
    }

    private void MostrarInfoGarante()
    {
        var win = new BuscadorPersonaWindow(_db) { Owner = this };
        win.Closed += (_, _) =>
        {
            if (win.PersonaSeleccionada == null) return;
            var p = win.PersonaSeleccionada;
            _tbGarNombre.Text  = p.Nombre;
            _tbGarCi.Text      = p.Ci;
            _tbGarDir.Text     = p.Direccion;
            _tbGarCiudad.Text  = p.Ciudad;
            _tbGarCel.Text     = p.Telefono;
            _tbGarEmpresa.Text = p.Empresa;
            _tbGarTelLab.Text  = p.TelLaboral;
            _tbGarEcv.Text     = p.Ecv;
            _idGaranteCargado  = p.Id;
            _nomGaranteCargado = p.Nombre;
        };
        win.Show();
    }

    private UIElement BuildMercaderiasContent()
    {
        _gridProductos = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            ColumnHeaderHeight = 30, RowHeight = 30, FontSize = 10.5,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            BorderThickness = new Thickness(0),
            Background = BrCard,
            RowBackground = BrCard,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(249, 250, 251)),
            // Con scroll horizontal deshabilitado, WPF comprime TODAS las columnas (incluida la
            // Star de Descripción) para que quepan en el ancho visible de la ventana — con el
            // panel lateral de Solicitud ocupando espacio, las columnas fijas ya no entraban, y
            // "Descripción"/"Precio"/"Subtotal" quedaban truncadas o directamente fuera de vista.
            // Fuente más chica + columnas más angostas para que las 8 quepan sin scroll en el
            // ancho típico de esta ventana; scroll queda como respaldo si igual no entra.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled,
            MinWidth = 620,
        };

        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, BrSecHead));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, BrBlanco));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(4,2,4,2)));
        _gridProductos.ColumnHeaderStyle = hdrStyle;

        Style CellStyle(TextAlignment align = TextAlignment.Left) {
            var s = new Style(typeof(TextBlock));
            s.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, align));
            s.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0)));
            return s;
        }
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Código",      Binding = new System.Windows.Data.Binding("Codigo"),                   Width = 60,  ElementStyle = CellStyle() });
        var colDesc = new DataGridTextColumn {
            Header   = "Descripción",
            Binding  = new System.Windows.Data.Binding("Descripcion"),
            Width    = new DataGridLength(1, DataGridLengthUnitType.Star),
            MinWidth = 110,
        };
        var descStyle = new Style(typeof(TextBlock));
        descStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        descStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        descStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        descStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0)));
        colDesc.ElementStyle = descStyle;
        _gridProductos.Columns.Add(colDesc);
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Precio",      Binding = new System.Windows.Data.Binding("Precio")     { StringFormat = "N0" }, Width = 68,  ElementStyle = CellStyle(TextAlignment.Right) });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Entrega",     Binding = new System.Windows.Data.Binding("Entrega")    { StringFormat = "N0" }, Width = 68,  ElementStyle = CellStyle(TextAlignment.Right) });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Cuotas",      Binding = new System.Windows.Data.Binding("Cuotas"),                            Width = 42,  ElementStyle = CellStyle(TextAlignment.Center) });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Costo mens.", Binding = new System.Windows.Data.Binding("CostoMens")  { StringFormat = "N0" }, Width = 74,  ElementStyle = CellStyle(TextAlignment.Right) });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Total final", Binding = new System.Windows.Data.Binding("ValorFinal") { StringFormat = "N0" }, Width = 74,  ElementStyle = CellStyle(TextAlignment.Right) });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Cant.",        Binding = new System.Windows.Data.Binding("Cantidad")   { StringFormat = "N0" }, Width = 42,  ElementStyle = CellStyle(TextAlignment.Center) });
        _gridProductos.Columns.Add(new DataGridTextColumn { Header = "Subtotal",    Binding = new System.Windows.Data.Binding("TotalGral")  { StringFormat = "N0" }, Width = 74,  ElementStyle = CellStyle(TextAlignment.Right) });

        // Fila de Total debajo del grid
        var totalBorder = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 235, 252)),  // #D2EBFC
            BorderBrush = BrBorde, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(10, 6, 10, 6)
        };
        var totalSp = new StackPanel { Orientation = Orientation.Horizontal };

        TextBox TotalBox(string label, string valor) {
            totalSp.Children.Add(new TextBlock {
                Text = label, FontWeight = FontWeights.SemiBold, FontSize = 12,
                Foreground = BrLabelTxt, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            var tb = new TextBox {
                Text = valor, FontWeight = FontWeights.Bold, FontSize = 13, IsReadOnly = true,
                Background = BrCard, Padding = new Thickness(10, 4, 10, 4), MinWidth = 130,
                BorderBrush = BrBorde, BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 20, 0), Foreground = BrPrimary
            };
            totalSp.Children.Add(tb);
            return tb;
        }
        TotalBox("Total:", _sol.TotalVenta.ToString("N0"));
        TotalBox("Entrega:", _sol.Entrega.ToString("N0"));

        totalBorder.Child = totalSp;

        var sp = new StackPanel();
        sp.Children.Add(_gridProductos);
        sp.Children.Add(totalBorder);
        return sp;
    }


    private void ActualizarBtnAutorizar()
    {
        if (_btnAutorizar == null) return;
        if (_permisoExcepcion == 1)
        {
            // Ya autorizado — badge verde solo lectura
            _btnAutorizar.Content    = "✔ Autorizado por Admin";
            _btnAutorizar.Background = BrVerde;
            _btnAutorizar.Foreground = BrBlanco;
            _btnAutorizar.IsEnabled  = false;
            _btnAutorizar.Visibility = Visibility.Visible;
        }
        else if (_sol.EstadoNum == 0)
        {
            // Solicitud pendiente — botón activo (el modal filtrará si es admin)
            _btnAutorizar.Content    = "🔑 Autorizar Venta";
            _btnAutorizar.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(26, 58, 82));
            _btnAutorizar.Foreground = BrBlanco;
            _btnAutorizar.IsEnabled  = true;
            _btnAutorizar.Visibility = Visibility.Visible;
        }
        else
        {
            // Solicitud ya aprobada/rechazada y sin autorización — ocultar
            _btnAutorizar.Visibility = Visibility.Collapsed;
        }
    }

    private async Task AutorizarExcepcionAsync()
    {
        if (_permisoExcepcion == 1) return;

        // Cargar administradores + excepción puntual (usuario código "67", ver
        // Usuario.PuedeVerTodosLosLocales para el mismo criterio usado en otras pantallas).
        List<dynamic> admins = new();
        try
        {
            using var conn = _db.Create();
            var rows = await conn.QueryAsync<dynamic>(
                "SELECT ID_USUARIO, NOMBRE_USUARIO, CODIGO_USUARIO, CONTRASEÑA_USUARIO " +
                "FROM USUARIOS WHERE UPPER(CARGO_USUARIO) = 'ADMINISTRADOR' OR CODIGO_USUARIO = '67' " +
                "ORDER BY NOMBRE_USUARIO");
            admins = rows.ToList();
        }
        catch (Exception ex) { MessageBox.Show($"Error cargando administradores: {ex.Message}"); return; }

        if (admins.Count == 0)
        {
            MessageBox.Show("No hay administradores configurados en el sistema.", "Sin admins",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // ── Modal de credenciales ────────────────────────────────────────────
        bool confirmado = false;
        var BrH  = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 78, 120));
        var BrF2 = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 248, 252));
        var BrB2 = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(208, 218, 232));

        var dlg = new Window {
            Title = "Autorizar Venta", Width = 380, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            Background = System.Windows.Media.Brushes.White
        };

        var header = new Border {
            Background = BrH, Padding = new Thickness(20, 14, 20, 14),
            Child = new StackPanel {
                Children = {
                    new TextBlock { Text = "AUTORIZAR VENTA", FontSize = 14, FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White },
                    new TextBlock { Text = "Ingrese las credenciales de administrador",
                        FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(170, 204, 232)),
                        Margin = new Thickness(0, 3, 0, 0) }
                }
            }
        };

        TextBlock FL2(string t) => new TextBlock {
            Text = t, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrGrisOsc, Margin = new Thickness(0, 0, 0, 4)
        };
        TextBox FT2() => new TextBox {
            Height = 36, FontSize = 13, Padding = new Thickness(10, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = BrB2, BorderThickness = new Thickness(1), Background = BrF2
        };
        PasswordBox FP2() => new PasswordBox {
            Height = 36, FontSize = 13, Padding = new Thickness(10, 0, 10, 0),
            BorderBrush = BrB2, BorderThickness = new Thickness(1), Background = BrF2
        };

        var cboAdmin = new ComboBox {
            Height = 36, FontSize = 13, Margin = new Thickness(0, 0, 0, 14),
            BorderBrush = BrB2, BorderThickness = new Thickness(1)
        };
        foreach (var u in admins)
            cboAdmin.Items.Add(new ComboBoxItem { Content = (string)u.NOMBRE_USUARIO, Tag = u });
        cboAdmin.SelectedIndex = 0;

        var txtCodigo   = FT2(); txtCodigo.Margin   = new Thickness(0, 0, 0, 14);
        var txtPassword = FP2(); txtPassword.Margin = new Thickness(0, 0, 0, 6);

        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        body.Children.Add(FL2("Administrador")); body.Children.Add(cboAdmin);
        body.Children.Add(FL2("Código"));        body.Children.Add(txtCodigo);
        body.Children.Add(FL2("Contraseña"));    body.Children.Add(txtPassword);

        var btnAceptar = new Button {
            Content = "✔ Autorizar", Height = 36, Padding = new Thickness(20, 0, 20, 0),
            Background = BrVerde, Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold,
            FontSize = 13, Cursor = Cursors.Hand
        };
        var btnCancelar = new Button {
            Content = "Cancelar", Height = 36, Padding = new Thickness(20, 0, 20, 0),
            Background = BrGrisOsc, Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 13, Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var footerSp = new StackPanel {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right
        };
        footerSp.Children.Add(btnCancelar);
        footerSp.Children.Add(btnAceptar);

        var footer = new Border {
            Background = BrF2, BorderBrush = BrB2, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 12, 20, 12), Child = footerSp
        };

        var rootSp = new StackPanel();
        rootSp.Children.Add(header);
        rootSp.Children.Add(body);
        rootSp.Children.Add(footer);
        dlg.Content = rootSp;

        void confirmar()
        {
            if (cboAdmin.SelectedItem is not ComboBoxItem ci) { MessageBox.Show("Seleccione un administrador."); return; }
            dynamic u = ci.Tag;
            if (txtCodigo.Text.Trim()       != u.CODIGO_USUARIO.ToString())     { MessageBox.Show("Código incorrecto.",    "Error", MessageBoxButton.OK, MessageBoxImage.Warning); txtCodigo.Focus(); txtCodigo.SelectAll(); return; }
            if (txtPassword.Password.Trim() != u.CONTRASEÑA_USUARIO.ToString()) { MessageBox.Show("Contraseña incorrecta.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); txtPassword.Focus(); txtPassword.SelectAll(); return; }
            confirmado = true;
            dlg.DialogResult = true;
            dlg.Close();
        }

        btnAceptar.Click   += (_, _) => confirmar();
        btnCancelar.Click  += (_, _) => dlg.Close();
        txtPassword.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) confirmar(); };
        txtCodigo.KeyDown   += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) txtPassword.Focus(); };

        dlg.ShowDialog();
        if (!confirmado) return;

        // ── Aplicar autorización ─────────────────────────────────────────────
        try
        {
            using var conn = _db.Create();
            await conn.ExecuteAsync(
                "UPDATE CAB_SOL_SALES SET PERMISO_EXEPCION = 1 WHERE IDSOLICITUD = @id",
                new { id = _sol.IdSolicitud });
            _permisoExcepcion = 1;
            ActualizarBtnAutorizar();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al autorizar: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CambiarEstadoAsync(byte nuevoEstado)
    {
        // Verif. Datos: solo mostrar avisos
        if (nuevoEstado == 0)
        {
            var (avisos, _) = await ObtenerVerificacionAsync();
            MostrarModalVerificacion(avisos);
            return;
        }
        // Rechazar: confirmación simple
        if (nuevoEstado == 2)
        {
            if (MessageBox.Show("¿Confirmar RECHAZAR esta solicitud?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await AplicarCambioEstadoAsync(nuevoEstado);
            return;
        }
        // Aceptar: autorización admin → alertas + confirmación simple. Ya NO se pide acá el
        // método de pago/entrega ni se genera la venta — ver ConfirmarEntregaAsync más abajo.
        // Antes "Aprobar" y "registrar la venta con su movimiento de caja" eran la MISMA
        // operación: aprobar una solicitud con entrega inicial ya generaba el ingreso en
        // CAJA_DETALLE de inmediato, sin que nadie confirmara el monto/método de pago
        // realmente recibido — reportado por una vendedora: "la solicitud aprobada con
        // entrega de 2.350.000 aún no facturó pero ya le aparece en su caja". El sistema
        // legado (Delphi) separaba esto en dos pasos: aprobar la solicitud, y luego un
        // segundo paso manual ("doble clic en la solicitud aprobada" → pantalla con el
        // campo Entrega editable → Guardar) que recién ahí generaba TODO junto (cuotas +
        // movimiento de caja) — confirmado leyendo GUARDAR_ENTREGA_GENERADAS_CS /
        // AGREGAR_GENERADAS_CS del sistema viejo. Se replica ese mismo criterio acá: Aprobar
        // solo cambia CAB_SOL_SALES.ESTADO, y ConfirmarEntregaAsync (ver más abajo) es el
        // único punto que crea CABECERA_SALES/GENERADAS/CAJA_DETALLE, todo en una sola
        // llamada a GUARDAR_VENTA_CREDITO_CS — evita también el estado intermedio ambiguo
        // que ya nos había mordido antes (solicitud 5698, Alberto González).
        if (nuevoEstado == 1)
        {
            var permiso = await CrediSoft.UI.Views.Shared.PermisoUsuariosModal.MostrarAsync(this, _db);
            if (permiso == null) return;

            var (avisos, _) = await ObtenerVerificacionAsync();
            MostrarModalVerificacion(avisos);
            if (MessageBox.Show("¿Confirmar APROBAR esta solicitud?\n\nLa venta y el movimiento de caja se registrarán en un paso aparte, una vez que se confirme el monto de entrega realmente recibido.",
                "Confirmar aprobación", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await AplicarCambioEstadoAsync(nuevoEstado);
        }
    }

    private async Task AplicarCambioEstadoAsync(byte nuevoEstado)
    {
        try
        {
            using var conn = _db.Create();

            await conn.ExecuteAsync(
                "UPDATE CAB_SOL_SALES SET ESTADO=@Estado, FECHA_APROB=GETDATE() WHERE IDSOLICITUD=@Id",
                new { Estado = nuevoEstado, Id = _sol.IdSolicitud });

            _sol.EstadoNum = nuevoEstado;
            _sol.Estado = nuevoEstado switch { 1 => "Aprobado", 2 => "Rechazado", _ => "Verificar" };

            var msg = nuevoEstado == 1
                ? "Solicitud aprobada. Falta confirmar la entrega para generar la venta y el movimiento de caja."
                : "Estado actualizado correctamente.";
            MessageBox.Show(msg, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error: {ex.Message}\n\nLa solicitud NO quedó aprobada — revirtió a \"Verificar\". Puede reintentar la aprobación.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Segundo paso, separado de Aprobar: genera la venta real (CABECERA_SALES/GENERADAS) Y
    // el movimiento de caja de la entrega, todo junto en la misma llamada a
    // GUARDAR_VENTA_CREDITO_CS — solo debe estar disponible sobre solicitudes ya Aprobadas
    // que todavía no tengan una venta generada (ver _sol.VentaGenerada, cargado al abrir la
    // ventana desde EXISTS sobre CABECERA_SALES.NSOLICITUD).
    private async Task ConfirmarEntregaAsync()
    {
        // Revalida contra la base (no solo _sol.VentaGenerada en memoria, que puede estar
        // desactualizado si la ventana quedó abierta o se reabrió) antes de mostrar el modal —
        // misma query que ya usa el listado (VentaGenerada), ver comentario arriba.
        using (var connCheck = _db.Create())
        {
            var yaGenerada = await connCheck.ExecuteScalarAsync<bool>(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM CABECERA_SALES WHERE NSOLICITUD = @nsol) THEN 1 ELSE 0 END",
                new { nsol = _sol.Numero });
            if (yaGenerada)
            {
                MessageBox.Show("Esta solicitud ya tiene una venta generada — la entrega ya fue confirmada antes.",
                    "Ya confirmada", MessageBoxButton.OK, MessageBoxImage.Information);
                _sol.VentaGenerada = true;
                Close();
                return;
            }
        }

        var (avisos, datosCliente) = await ObtenerVerificacionAsync();
        var modalResult = MostrarModalAprobar(avisos, datosCliente, soloConfirmarEntrega: true);
        if (!modalResult.Confirmado) return;

        // GUARDAR_VENTA_CREDITO_CS (SP legado, compartido con el sistema viejo) escribe el
        // movimiento de caja en DET_CAJA, no en CAJA_DETALLE — la tabla que realmente lee el
        // sistema nuevo (Historial de Caja, Explorador de Caja) — así que la entrega quedaba
        // invisible ahí (confirmado: venta real generada, DET_CAJA con el registro, pero
        // CAJA_DETALLE vacío para esa venta). Se valida que haya caja abierta ANTES de llamar
        // al SP para poder avisar con claridad si falta, en vez de generar la venta y recién
        // ahí descubrir que no se puede reflejar el ingreso en ningún lado.
        var cajaRepo = App.Services.GetRequiredService<ICajaRepository>();
        var sesionActual = SessionService.Instance;
        CrediSoft.Core.Models.CajaMaster? caja;

        // Caso real que motivó esto: un administrador (o el código 67) logueado en el local A
        // confirma la entrega de una venta que pertenece al local B — antes la plata SIEMPRE
        // iba a la caja del local de LA SESIÓN (idLocalCaja = LocalActual), nunca al local real
        // de la venta ni al del vendedor elegido, descuadrando ambas cajas sin que nadie lo
        // notara hasta el arqueo. Solo para admin/67 se ofrece elegir explícitamente a qué caja
        // abierta va la entrega — un usuario normal solo puede operar su propia caja, así que
        // mantiene el comportamiento de siempre (la de su sesión).
        if (sesionActual.UsuarioActual?.PuedeVerTodosLosLocales == true)
        {
            // Pedido explícito: solo las cajas abiertas del LOCAL de esta venta — antes se
            // listaban las de todo el sistema, lo cual permitía elegir por error la caja de
            // una sucursal totalmente ajena a la venta.
            var cajasAbiertas = (await cajaRepo.ListarCajasAbiertasAsync())
                .Where(c => c.IdLocal == _sol.IdLocal).ToList();
            if (cajasAbiertas.Count == 0)
            {
                MessageBox.Show($"No hay ninguna caja abierta en \"{_sol.LocalNombre}\". Abra la caja de ese local antes de confirmar la entrega.",
                    "Sin cajas abiertas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            caja = MostrarSelectorCaja(cajasAbiertas, _sol.IdLocal, _sol.LocalNombre);
            if (caja == null) return; // canceló
        }
        else
        {
            var idLocalCaja = sesionActual.LocalActual?.IdLocal ?? _sol.IdLocal;
            caja = await cajaRepo.ObtenerCajaAbiertaAsync(idLocalCaja);
            if (caja == null)
            {
                MessageBox.Show($"No hay una caja abierta en \"{_sol.LocalNombre}\". Abra la caja antes de confirmar la entrega.",
                    "Caja cerrada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        int idCabResultante;
        try
        {
            using var conn = _db.Create();

            // GUARDAR_VENTA_CREDITO_CS rechaza la venta con "Error" genérico (sin detalle)
            // si @NRECIBO coincide con el de OTRO documento ya existente — el número de
            // recibo es el talonario físico, único por documento, no un monto. Validar acá
            // antes de llamar al SP para poder explicar la causa real en vez de un "Error"
            // sin contexto (confirmado: caso real donde el cajero puso el monto de la
            // entrega, 300000, que coincidía con otro NRECIBO histórico).
            if (!string.IsNullOrWhiteSpace(modalResult.NRecibo))
            {
                var yaExiste = await conn.ExecuteScalarAsync<bool>(
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM DOCUMENTOS WHERE NRECIBO = @nrecibo) THEN 1 ELSE 0 END",
                    new { nrecibo = modalResult.NRecibo });
                if (yaExiste)
                {
                    MessageBox.Show(
                        $"El N° de Recibo \"{modalResult.NRecibo}\" ya fue usado en otro documento.\n\n" +
                        "El N° de Recibo debe ser el número del talonario físico entregado al cliente " +
                        "(no el monto de la entrega) y no puede repetirse. Verifique el recibo físico e intente de nuevo.",
                        "N° de Recibo duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                idCabResultante = await RegistrarVentaAprobadaAsync(conn, modalResult.NRecibo, modalResult.NPagare, modalResult.Metodo, modalResult.NumPago, modalResult.MontoEntrega);
            }
            catch
            {
                // Mismo cuidado que antes (caso Alberto González): si esto falla a mitad de
                // camino, la solicitud debe quedar en un estado claro para reintentar, no
                // "Aprobada" con una venta a medias. GUARDAR_VENTA_CREDITO_CS ya corre en su
                // propia transacción (Begin Tran/Commit/Rollback), así que un fallo acá no
                // deja registros parciales en CABECERA_SALES/GENERADAS/CAJA_DETALLE — la
                // solicitud simplemente se queda en Aprobada, lista para reintentar
                // ConfirmarEntregaAsync de nuevo.
                throw;
            }

            // GUARDAR_VENTA_CREDITO_CS ya deja la fila de CAB_SOL_SALES en ESTADO=1 vía UPDATE
            // (fix del 17/07 — antes la borraba y este código la reinsertaba; con el SP
            // corregido, reinsertar acá chocaba contra la fila que ya existía, PK_CAB_SOL_SALES
            // duplicada — caso real: solicitud 8688, Fassardi/Buena Vista, 21/07). No hace
            // falta ningún snapshot ni reinserción.

            // GUARDAR_VENTA_CREDITO_CS a veces graba NSOLICITUD='0' en vez del número real de
            // la solicitud (bug confirmado varias veces esta sesión) — sin esto, VentaGenerada
            // (que busca por NSOLICITUD) nunca encuentra la venta y el chip queda en
            // "Pendiente" para siempre aunque la venta y la caja ya estén generadas.
            if (idCabResultante > 0)
                await conn.ExecuteAsync(
                    "UPDATE CABECERA_SALES SET NSOLICITUD = @nsol WHERE IDCAB = @idcab",
                    new { nsol = _sol.Numero, idcab = idCabResultante });

            // Movimiento real en CAJA_DETALLE — el propio sistema nuevo, no DET_CAJA (legado).
            await InsertarMovimientoCajaAsync(conn, caja, idCabResultante, modalResult.MontoEntrega, modalResult.Metodo);

            MessageBox.Show("Entrega confirmada. Venta y movimiento de caja registrados correctamente.",
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al confirmar la entrega: {ex.Message}\n\nLa solicitud sigue Aprobada — puede reintentar.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Comprobante impreso — antes el flujo de Confirmar Entrega no imprimía nada (el
        // ticket con "SOLICITUD APROBADA"/"ENTREGA APROBADA" solo existía en el sistema
        // viejo). Se reutiliza TicketPrinter/ComprobanteVentaPreviaWindow ya construidos
        // para Venta Contado, leyendo los datos reales recién persistidos (DETALLES_SALES)
        // en vez de un carrito en memoria — evita el bug de artículo duplicado visto en el
        // ticket del sistema viejo, que no viene de los datos sino de cómo arma la impresión.
        if (idCabResultante > 0)
            await ImprimirComprobanteEntregaAsync(idCabResultante);

        // Sin esto, AbrirDetalle() (VisorSolicitudesWindow) comparaba item.VentaGenerada
        // antes/después de este ShowDialog() y no detectaba ningún cambio en memoria —
        // el listado quedaba con el chip "Pendiente" hasta que el usuario apretaba
        // "Refrescar" a mano, pese a que la venta ya se había generado correctamente.
        _sol.VentaGenerada = true;

        Close();
    }

    // GUARDAR_VENTA_CREDITO_CS (SP legado) escribe el movimiento de la entrega en DET_CAJA,
    // no en CAJA_DETALLE — la tabla que realmente usa el sistema nuevo (Historial de Caja,
    // Explorador de Caja, Arqueo). Se inserta acá directo, mismo patrón que el resto de la
    // app (ver sp_Guardar_Cobranza_Cs_2026 / Anular() en AdicionalesWindows.cs), para que la
    // entrega cuente en los reportes de caja del sistema nuevo.
    private async Task InsertarMovimientoCajaAsync(System.Data.IDbConnection conn, CrediSoft.Core.Models.CajaMaster caja, int idCab, decimal monto, byte metodo)
    {
        var formaPago = metodo switch { 2 => "TARJETA", 3 => "TARJETA", 4 => "TRANSFERENCIA", 5 => "QR", _ => "EFECTIVO" };
        var idCajero = SessionService.Instance.UsuarioActual!.IdUsuario;

        // ID_VENDEDOR = a quién se le atribuye la venta/comisión (CABECERA_SALES.ID_USUARIO,
        // ya grabado correctamente por RegistrarVentaAprobadaAsync con el vendedor elegido en
        // el selector de la solicitud) — NO necesariamente quien tiene la caja física abierta
        // (ID_CAJERO). Bug real reportado: Chisel (dueña de la caja) confirma la entrega de una
        // venta cuyo vendedor real es Mabel Escobar, y la comisión quedaba atribuida a Chisel
        // porque acá se usaba SIEMPRE el usuario de sesión para ambas columnas. Mismo criterio
        // ya corregido antes en la query de Arqueo de Caja (ID_VENDEDOR con fallback a
        // ID_CAJERO cuando no hay vendedor registrado).
        var idVendedorReal = await conn.ExecuteScalarAsync<int?>(
            "SELECT ID_USUARIO FROM CABECERA_SALES WHERE IDCAB = @idCab", new { idCab }) ?? idCajero;

        await conn.ExecuteAsync(
            "INSERT INTO CAJA_DETALLE (ID_MASTER, ID_VENTA, ID_LOCAL, FECHA_HORA, TIPO, SUBTIPO, FORMA_PAGO, MONTO, " +
            "ID_CAJERO, ID_ENTIDAD, CONCEPTO, REFERENCIA, ESTADO_REG, ID_VENDEDOR) " +
            "VALUES (@IdMaster, @IdVenta, @IdLocal, GETDATE(), 'I', 'VENTA', @FormaPago, @Monto, " +
            "@IdCajero, NULL, @Concepto, NULL, 'V', @IdVendedor)",
            new {
                IdMaster  = caja.IdMaster,
                IdVenta   = idCab,
                IdLocal   = caja.IdLocal,
                FormaPago = formaPago,
                Monto     = monto,
                IdCajero  = idCajero,
                Concepto  = $"VENTA CREDITO SEGUN COMPROBANTE: {idCab:D9} | ENTREGA: {monto:0}",
                IdVendedor = idVendedorReal,
            });
    }

    private async Task ImprimirComprobanteEntregaAsync(int idCab)
    {
        try
        {
            using var conn = _db.Create();
            var cab = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT CB.NSOLICITUD, CB.TOTAL, CB.ENTREGANORMAL, CB.CUOTAS, CB.MONTO_CUOTA, " +
                "  CL.NOMBRE_CLIENTE, U.NOMBRE_USUARIO AS NomVend " +
                "FROM CABECERA_SALES CB " +
                "LEFT JOIN CLIENTES CL ON CL.ID_CLIENTE = CB.ID_CLIENTE " +
                "LEFT JOIN USUARIOS U  ON U.ID_USUARIO  = CB.ID_USUARIO " +
                "WHERE CB.IDCAB = @id", new { id = idCab });
            if (cab == null) return;

            var detalles = (await conn.QueryAsync<dynamic>(
                "SELECT D.CANTIDAD, D.PV, A.D AS Descripcion " +
                "FROM DETALLES_SALES D LEFT JOIN ARTICULOS A ON A.ID = D.IDART " +
                "WHERE D.IDCAB = @id", new { id = idCab })).ToList();

            decimal total       = (decimal?)cab.TOTAL         ?? 0;
            decimal entrega     = (decimal?)cab.ENTREGANORMAL ?? 0;

            // DETALLES_SALES no guarda una entrega por artículo — ENTREGANORMAL es un monto
            // único a nivel de toda la venta. La columna "Entrega" del ticket reparte ese
            // total proporcionalmente al peso de cada línea sobre el total vendido (mismo
            // criterio que ya usa ArticuloTicket en otros flujos) — antes se pasaba
            // Cantidad*Precio ahí por error, mostrando el subtotal de cada línea en vez de
            // su porción de entrega (con 1 solo artículo daba, por coincidencia, el total de
            // la venta en vez de lo realmente entregado).
            var articulos = detalles.Select(d => {
                decimal subtotal = (decimal)d.CANTIDAD * (decimal)d.PV;
                decimal entregaLinea = total > 0 ? Math.Round(entrega * subtotal / total) : 0;
                return new CrediSoft.UI.Views.Shared.ArticuloTicket(
                    (string?)d.Descripcion ?? "", (decimal)d.CANTIDAD, (decimal)d.PV, entregaLinea);
            }).ToList();

            var emp = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerDatosEmpresaAsync();
            var idLocalTicket = SessionService.Instance.LocalActual?.IdLocal ?? _sol.IdLocal;
            var nTicket = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerNumeroTicketAsync(idLocalTicket);
            var fmt = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerFormatoComprobanteAsync();
            var telefonoLocalTicket = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT TOP 1 TELEFONO FROM LOCALES WHERE ID_LOCAL=@id", new { id = idLocalTicket }) ?? "";
            int     cuotas      = (int?)cab.CUOTAS            ?? 0;
            decimal montoCuota  = (decimal?)cab.MONTO_CUOTA   ?? 0;

            var datos = new CrediSoft.UI.Views.Shared.DatosTicketVenta(
                NombreEmpresa: emp.Nombre,
                NombreLocal: $"LOCAL: {idLocalTicket}  —  {_sol.LocalNombre}",
                Fecha: DateTime.Now,
                NumeroTicket: nTicket,
                // GUARDAR_VENTA_CREDITO_CS a veces graba NSOLICITUD='0' en vez del número real
                // (mismo patrón huérfano visto en varias ventas de esta sesión) — se prioriza
                // _sol.Numero, que es el número real de la solicitud que ya tenemos en mano,
                // sobre lo que haya quedado persistido en CABECERA_SALES.
                NroSolicitud: _sol.Numero,
                Vendedor: (string?)cab.NomVend ?? _sol.VendedorNombre,
                NombreCliente: (string?)cab.NOMBRE_CLIENTE ?? _sol.ClienteNombre,
                TotalVenta: total,
                TotalEntrega: entrega,
                TotalConInteres: total,
                CantCuotas: cuotas,
                CostoCuota: montoCuota,
                Articulos: articulos,
                Timbrado: emp.Timbrado,
                VigenciaDesde: emp.Desde,
                VigenciaHasta: emp.Hasta,
                EsContado: false,
                TelefonoLocal: telefonoLocalTicket);

            // Registro histórico para reimpresión posterior — no bloquea la impresión si falla.
            _ = CrediSoft.UI.Views.Shared.TicketPrinter.RegistrarComprobanteAsync(
                tipo: "VENTA_CREDITO", numeroTicket: nTicket, idLocal: idLocalTicket,
                datosTicket: datos,
                idUsuarioCajero: SessionService.Instance.UsuarioActual?.IdUsuario,
                nombreCajero: (string?)cab.NomVend ?? _sol.VendedorNombre,
                nombreCliente: (string?)cab.NOMBRE_CLIENTE ?? _sol.ClienteNombre,
                idCab: idCab, nroSolicitud: _sol.Numero, montoTotal: total);

            var previa = new ComprobanteVentaPreviaWindow(datos, fmt) { Owner = this };
            previa.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"La venta se registró correctamente, pero hubo un error al generar el comprobante:\n{ex.Message}",
                "Impresión", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task<int> RegistrarVentaAprobadaAsync(System.Data.IDbConnection conn, string nRecibo = "", string nPagare = "", byte metodo = 1, string numPago = "", decimal? montoEntregaConfirmado = null)
    {
        var session = CrediSoft.Core.Services.SessionService.Instance;
        if (session.UsuarioActual == null || session.LocalActual == null)
            throw new InvalidOperationException("No hay sesión activa.");

        // Cargar datos completos de la solicitud desde CAB_SOL_SALES
        var cab = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT s.ID_CLIENTE, s.ID_GARANTE, s.ID_LOCAL, s.ID_USUARIO," +
            "  s.NUMERO, s.CANTCUOTAS, s.TOTAL_MONTO_CUOTA, s.TOTALSALE, s.TOTALENTREGA," +
            "  s.FECHA_COBRO," +
            "  s.ID_REFERENCIA1, s.ID_REFERENCIA2," +
            "  s.NOM_REFERENCIA1, s.TEL_REFERENCIA1, s.TRAB_REFERENCIA1," +
            "  s.NOM_REFERENCIA2, s.TEL_REFERENCIA2, s.TRAB_REFERENCIA2," +
            "  s.NOM_REFERENCIACOMERCIAL1, s.TEL_REFERENCIACOMERCIAL1, s.TRAB_REFERENCIACOMERCIAL1," +
            "  s.NOM_REFERENCIACOMERCIAL2, s.TEL_REFERENCIACOMERCIAL2, s.TRAB_REFERENCIACOMERCIAL2," +
            "  s.I_SALARIO, s.I_HONORARIO, s.I_CONYUGE, s.I_OTROS, s.I_TOTAL," +
            "  s.E_GASTO, s.E_CUOTA, s.E_ALQUILER, s.E_OTROS, s.E_TOTAL" +
            " FROM CAB_SOL_SALES s WHERE s.IDSOLICITUD = @id",
            new { id = _sol.IdSolicitud });

        if (cab == null) throw new Exception("No se encontraron datos de la solicitud.");

        // Cargar detalles de DET_SOL_SALES para registrar cada artículo
        var detalles = (await conn.QueryAsync<dynamic>(
            "SELECT d.IDART, d.CA, d.D, d.PRECIO, d.ENTREGA, d.CANTCUOTAS," +
            "  d.COSTOMENSUAL, d.VALORFINAL, d.CANTIDAD, d.SUBTOTAL," +
            "  ISNULL(a.IVA, 0) as Iva, ISNULL(p.PC, 0) as Pc" +
            " FROM DET_SOL_SALES d" +
            " LEFT JOIN ARTICULOS a ON d.IDART = a.ID" +
            " LEFT JOIN PRICES p   ON d.IDART = p.IDART AND p.IDLOCAL = @loc" +
            " WHERE d.IDSOLICITUD = @id ORDER BY d.ID_DET_SOL",
            new { id = _sol.IdSolicitud, loc = (int?)cab.ID_LOCAL ?? session.LocalActual.IdLocal }))
            .ToList();

        if (detalles.Count == 0) throw new Exception("La solicitud no tiene artículos.");

        var ventaRepo = App.Services.GetRequiredService<IVentaRepository>();

        int    idCliente    = (int)cab.ID_CLIENTE;
        int    idGarante    = (int?)cab.ID_GARANTE ?? 0;
        if (cab.ID_LOCAL == null || (int)cab.ID_LOCAL == 0)
            throw new Exception("La solicitud no tiene local asignado. No se puede aprobar.");
        byte   idLocal      = (byte)cab.ID_LOCAL;
        int    idUsuario    = (int?)cab.ID_USUARIO ?? session.UsuarioActual.IdUsuario;
        string nSol         = ((string?)cab.NUMERO ?? "").Trim();
        byte   cuotas       = (byte?)cab.CANTCUOTAS ?? 1;
        decimal montoCuota  = (decimal?)cab.TOTAL_MONTO_CUOTA ?? 0;
        decimal totalSale   = (decimal?)cab.TOTALSALE ?? 0;
        // El monto de entrega confirmado por el cajero en el modal (lo realmente recibido)
        // tiene prioridad sobre el TOTALENTREGA estimado al cargar la solicitud — pueden
        // diferir, y este es el valor que efectivamente se acredita a GENERADAS/CAJA_DETALLE.
        decimal totalEnt    = montoEntregaConfirmado ?? (decimal?)cab.TOTALENTREGA ?? 0;
        decimal entNorm     = totalEnt;
        decimal entLog      = 0;
        DateTime fechaCobro = (DateTime?)cab.FECHA_COBRO ?? DateTime.Today.AddMonths(1);

        for (int i = 0; i < detalles.Count; i++)
        {
            var d = detalles[i];
            bool esPrimero = i == 0;

            decimal pc  = (decimal?)d.Pc  ?? 0;
            decimal iva = (decimal?)d.Iva ?? 0;

            var prm = new VentaCreditoParams(
                IdCab: 0, NSol: 0,
                IdLocal: idLocal, IdUsuario: idUsuario,
                IdCliente: idCliente, IdGarante: idGarante,
                IdRef1: (int?)cab.ID_REFERENCIA1 ?? 0,
                IdRef2: (int?)cab.ID_REFERENCIA2 ?? 0,
                NomRef1:  (string?)cab.NOM_REFERENCIA1  ?? "", TelRef1: (string?)cab.TEL_REFERENCIA1  ?? "", TrabRef1: (string?)cab.TRAB_REFERENCIA1  ?? "",
                NomRef2:  (string?)cab.NOM_REFERENCIA2  ?? "", TelRef2: (string?)cab.TEL_REFERENCIA2  ?? "", TrabRef2: (string?)cab.TRAB_REFERENCIA2  ?? "",
                NomRefCom1: (string?)cab.NOM_REFERENCIACOMERCIAL1 ?? "", TelRefCom1: (string?)cab.TEL_REFERENCIACOMERCIAL1 ?? "", TrabRefCom1: (string?)cab.TRAB_REFERENCIACOMERCIAL1 ?? "",
                NomRefCom2: (string?)cab.NOM_REFERENCIACOMERCIAL2 ?? "", TelRefCom2: (string?)cab.TEL_REFERENCIACOMERCIAL2 ?? "", TrabRefCom2: (string?)cab.TRAB_REFERENCIACOMERCIAL2 ?? "",
                // FORMA_DE_VENTA=2 (Crédito) es correcto acá — ver comentario en
                // VentaContadoWindow.OnConfirmar sobre esta convención.
                FormaDeVenta: 2, MetodoDeVenta: 1, NTarjeta: "",
                Parcial: totalSale, Descuento: 0, Total: totalSale,
                EntregaNormal: entNorm, EntregaLogistica: entLog,
                Cuotas: cuotas, MontoCuota: montoCuota,
                Debe: totalSale - totalEnt, Haber: totalEnt,
                Cpha: 0, Estado: 1,
                Tiva: iva * (decimal)d.CANTIDAD * (decimal)d.PRECIO / 100m,
                IdDet: 0, IdArt: (int)d.IDART,
                Cantidad: (decimal)d.CANTIDAD,
                Pc: pc, Pv: (decimal)d.PRECIO, IvaArt: iva, EsArt: 1,
                IdPrices: 0, IdMovArt: 0, Mov: 2, Mod: 2,
                StIni: 0, PCant: 0,
                // GENERADAS siempre necesita 1 fila más que "cuotas": NCUOTA=1 es la fila de
                // la Entrega (exista monto o sea $0), NCUOTA=2..(cuotas+1) son las cuotas
                // reales pactadas — mismo patrón que todo el historial real del sistema viejo
                // (ver comentario en GenerarCuotasAsync).
                IdSolicitud: _sol.IdSolicitud, TCuotas: cuotas + 1,
                IdDetCaja: 0, IdCabCaja: 0, Caja: 0,
                CountCaja: esPrimero ? 1 : 0,
                Accion: esPrimero ? (byte)1 : (byte)0,
                Concepto: 1,
                Monto: esPrimero ? entNorm : 0,
                Metodo: metodo, Numero: numPago, Para: 0, Obs: "",
                IdDoc: 0, NRecibo: nRecibo, NPagare: nPagare,
                FechaInicioExterna: fechaCobro, NVenta: 0,
                Agente: esPrimero ? "SI" : "NO");

            var idCabResultante = await ventaRepo.GuardarVentaCreditoAsync(prm);
            // Solo interesa el IDCAB de la primera iteración (esPrimero) — con más de un
            // artículo, cada llamada subsiguiente al SP inserta un DETALLES_SALES adicional
            // sobre la MISMA venta, no crea una venta nueva.
            if (esPrimero) return idCabResultante;
        }
        return 0;
    }

    private async Task<(string avisos, string datosCliente)> ObtenerVerificacionAsync()
    {
        try
        {
            using var conn = _db.Create();
            var datos = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT s.ID_CLIENTE, s.ID_GARANTE, s.TOTALSALE," +
                "  cl.TIPO, cl.NOMBRE_CLIENTE, cl.CI_CLIENTE," +
                "  cl.TELEFONO_CLIENTE, cl.CIUDAD_CLIENTE, cl.CRED_MAX" +
                " FROM CAB_SOL_SALES s" +
                " LEFT JOIN CLIENTES cl ON s.ID_CLIENTE = cl.ID_CLIENTE" +
                " WHERE s.IDSOLICITUD = @id", new { id = _sol.IdSolicitud });

            if (datos == null) return ("No se pudo cargar datos.", "");

            int     idCliente = (int)datos.ID_CLIENTE;
            int     idGarante = _idGaranteCargado > 0 ? _idGaranteCargado : ((int?)datos.ID_GARANTE ?? 0);
            byte    tipo      = (byte?)datos.TIPO      ?? 0;
            decimal totalSol  = (decimal?)datos.TOTALSALE ?? 0;
            decimal credMax   = (decimal?)datos.CRED_MAX  ?? 0;
            string  nomCli    = (string?)datos.NOMBRE_CLIENTE  ?? "";
            string  ciCli     = (string?)datos.CI_CLIENTE      ?? "";
            string  telCli    = (string?)datos.TELEFONO_CLIENTE ?? "";
            string  ciudadCli = (string?)datos.CIUDAD_CLIENTE   ?? "";
            string  tipoTxt   = tipo switch { 1=>"Bueno", 2=>"Regular", 3=>"Malo", _=>"—" };
            string  garTxt    = idGarante > 0
                ? (_nomGaranteCargado.Length > 0 ? _nomGaranteCargado : "Asignado")
                : "Sin garante";

            var sb = new System.Text.StringBuilder();

            int totalCreditos = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM CABECERA_SALES WHERE ID_CLIENTE = @id AND FORMA_DE_VENTA = 2",
                new { id = idCliente });

            if (totalCreditos == 0)
                sb.AppendLine("• Cliente nuevo: sin ventas a crédito previas.");

            if ((totalCreditos == 0 || tipo >= 2) && idGarante == 0)
                sb.AppendLine($"• Tipo de cliente: {tipoTxt}. No tiene garante asignado.");

            decimal deudaActiva = await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(DEBE - HABER), 0) FROM CABECERA_SALES" +
                " WHERE ID_CLIENTE = @id AND ESTADO = 1 AND DEBE > HABER",
                new { id = idCliente });

            if (deudaActiva > 0)
                sb.AppendLine($"• Deuda activa: {deudaActiva:N0} Gs.");

            if (credMax > 0 && totalSol > credMax)
                sb.AppendLine($"• Monto ({totalSol:N0} Gs.) supera crédito máximo ({credMax:N0} Gs.).");

            string datosCliente =
                $"Nombre:        {nomCli}\n" +
                $"C.I.:          {ciCli}\n" +
                $"Teléfono:      {telCli}\n" +
                $"Ciudad:        {ciudadCli}\n" +
                $"Tipo:          {tipoTxt}\n" +
                $"Garante:       {garTxt}\n" +
                $"Total sol.:    {totalSol:N0} Gs.\n" +
                $"Crédito máx.:  {(credMax > 0 ? credMax.ToString("N0") + " Gs." : "Sin límite")}";

            return (sb.ToString().TrimEnd(), datosCliente);
        }
        catch (Exception ex)
        {
            return ($"Error en verificación: {ex.Message}", "");
        }
    }

    private void MostrarModalVerificacion(string avisos)
    {
        bool sinAvisos = string.IsNullOrEmpty(avisos);

        var win = new Window {
            Title = "Verificación de Datos", SizeToContent = SizeToContent.Height,
            Width = 480, MinHeight = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(238, 242, 247))
        };

        // Header
        var header = new Border {
            Background = BrPrimary,
            Child = new Grid { Margin = new Thickness(20, 0, 20, 0),
                Children = {
                    new Border {
                        Width = 4, CornerRadius = new CornerRadius(2),
                        Background = BrAzul, Margin = new Thickness(0, 12, 0, 12),
                        HorizontalAlignment = HorizontalAlignment.Left
                    }
                }
            }
        };
        var headerGrid = new Grid { Margin = new Thickness(20, 0, 20, 0) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var accentBar = new Border {
            Width = 4, CornerRadius = new CornerRadius(2),
            Background = BrAzul, Margin = new Thickness(0, 14, 12, 14),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var titleSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 12, 0, 12) };
        titleSp.Children.Add(new TextBlock {
            Text = "VERIFICACIÓN DE DATOS",
            FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BrBlanco
        });
        titleSp.Children.Add(new TextBlock {
            Text = sinAvisos ? "Sin observaciones pendientes" : "Se encontraron observaciones",
            FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(170, 204, 232)),
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(accentBar, 0); headerGrid.Children.Add(accentBar);
        Grid.SetColumn(titleSp,   1); headerGrid.Children.Add(titleSp);
        header.Child = headerGrid;

        // Body
        var body = new Border {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(208, 218, 232)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Margin = new Thickness(16, 14, 16, 0), Padding = new Thickness(16, 14, 16, 14)
        };

        if (sinAvisos)
        {
            var okSp = new StackPanel();
            okSp.Children.Add(new Border {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.FromRgb(209, 250, 229)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock {
                    Text = "✔", FontSize = 20, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
            okSp.Children.Add(new TextBlock {
                Text = "Verificación completada. No se encontraron observaciones.",
                TextWrapping = TextWrapping.Wrap, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52))
            });
            okSp.Children.Add(new TextBlock {
                Text = "Puede proceder a Aprobar o Rechazar la solicitud.",
                TextWrapping = TextWrapping.Wrap, FontSize = 11,
                Foreground = BrGrisOsc, Margin = new Thickness(0, 4, 0, 0)
            });
            body.Child = okSp;
        }
        else
        {
            var avisoSp = new StackPanel();

            // Encabezado de alerta
            var alertHeader = new Border {
                Background = new SolidColorBrush(Color.FromRgb(255, 244, 215)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(234, 179, 8)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 8), Margin = new Thickness(0, 0, 0, 10),
                Child = new StackPanel { Orientation = Orientation.Horizontal,
                    Children = {
                        new TextBlock {
                            Text = "⚠", FontSize = 16,
                            Foreground = new SolidColorBrush(Color.FromRgb(161, 98, 7)),
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 8, 0)
                        },
                        new TextBlock {
                            Text = "OBSERVACIONES ENCONTRADAS", FontSize = 12,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(120, 70, 0)),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            avisoSp.Children.Add(alertHeader);

            // Líneas de avisos como items
            foreach (var linea in avisos.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var txt = linea.TrimStart('•', ' ', '-');
                if (string.IsNullOrWhiteSpace(txt)) continue;
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                row.Children.Add(new Border {
                    Width = 6, Height = 6, CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromRgb(161, 98, 7)),
                    VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 5, 8, 0)
                });
                row.Children.Add(new TextBlock {
                    Text = txt, TextWrapping = TextWrapping.Wrap, FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(41, 37, 36)),
                    MaxWidth = 380
                });
                avisoSp.Children.Add(row);
            }

            // Pie
            avisoSp.Children.Add(new Border {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(208, 218, 232)),
                Margin = new Thickness(0, 10, 0, 10)
            });
            avisoSp.Children.Add(new TextBlock {
                Text = "Revise los datos antes de Aprobar o Rechazar la solicitud.",
                TextWrapping = TextWrapping.Wrap, FontSize = 11, FontStyle = FontStyles.Italic,
                Foreground = BrGrisOsc
            });
            body.Child = avisoSp;
        }

        // Footer
        var btnOk = new Button {
            Content = "Entendido", Height = 36, Padding = new Thickness(24, 0, 24, 0),
            Background = BrPrimary, Foreground = BrBlanco,
            BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold,
            FontSize = 13, Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        btnOk.Click += (_, _) => win.Close();

        var footer = new Border {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(208, 218, 232)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 12, 0, 0),
            Child = btnOk
        };

        var rootSp = new StackPanel();
        rootSp.Children.Add(header);
        rootSp.Children.Add(body);
        rootSp.Children.Add(footer);
        win.Content = rootSp;
        win.ShowDialog();
    }

    // Selector de caja destino — solo se muestra a admin/67 cuando confirman una entrega
    // (ver ConfirmarEntregaAsync). Devuelve null si el usuario canceló.
    private CrediSoft.Core.Models.CajaMaster? MostrarSelectorCaja(
        List<CrediSoft.Core.Models.CajaMaster> cajas, int idLocalVenta, string localVentaNombre)
    {
        CrediSoft.Core.Models.CajaMaster? seleccionada = null;

        var win = new Window
        {
            Title = "Seleccionar caja destino", Width = 560, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = Brushes.White,
            FontFamily = new FontFamily("Segoe UI")
        };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
            Padding = new Thickness(20, 16, 20, 16)
        };
        var headerSp = new StackPanel();
        headerSp.Children.Add(new TextBlock { Text = "¿A qué caja va la entrega?",
            FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
        headerSp.Children.Add(new TextBlock {
            Text = $"La venta pertenece a \"{localVentaNombre}\" — elegí en qué caja abierta se registra el dinero.",
            FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(187, 222, 251)),
            Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
        header.Child = headerSp;

        var body = new StackPanel { Margin = new Thickness(16) };
        foreach (var c in cajas.OrderByDescending(c => c.IdLocal == idLocalVenta))
        {
            bool esLocalVenta = c.IdLocal == idLocalVenta;
            var card = new Border
            {
                Background = esLocalVenta ? new SolidColorBrush(Color.FromRgb(232, 245, 233)) : new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                BorderBrush = esLocalVenta ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(esLocalVenta ? 2 : 1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            var cardSp = new StackPanel();
            var tituloRow = new StackPanel { Orientation = Orientation.Horizontal };
            tituloRow.Children.Add(new TextBlock { Text = c.LocalNombre, FontWeight = FontWeights.Bold, FontSize = 13 });
            if (esLocalVenta)
                tituloRow.Children.Add(new TextBlock { Text = "  ✓ local de esta venta", FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)), FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center });
            cardSp.Children.Add(tituloRow);
            cardSp.Children.Add(new TextBlock { Text = $"Cajero: {c.NombreCajero}  ·  Abierta: {c.FechaApertura:dd/MM HH:mm}",
                FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(97, 97, 97)), Margin = new Thickness(0, 2, 0, 0) });
            card.Child = cardSp;
            card.MouseLeftButtonUp += (_, _) => { seleccionada = c; win.Close(); };
            card.MouseEnter += (_, _) => card.Background = esLocalVenta
                ? new SolidColorBrush(Color.FromRgb(200, 230, 201)) : new SolidColorBrush(Color.FromRgb(238, 238, 238));
            card.MouseLeave += (_, _) => card.Background = esLocalVenta
                ? new SolidColorBrush(Color.FromRgb(232, 245, 233)) : new SolidColorBrush(Color.FromRgb(248, 249, 250));
            body.Children.Add(card);
        }

        var btnCancelar = new Button { Content = "✕  Cancelar", Height = 34, Padding = new Thickness(16, 0, 16, 0),
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 0, 16, 16),
            Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCancelar.Click += (_, _) => win.Close();

        var root = new StackPanel();
        root.Children.Add(header);
        root.Children.Add(body);
        root.Children.Add(btnCancelar);
        win.Content = root;
        win.ShowDialog();
        return seleccionada;
    }

    private record AprobarModalResult(bool Confirmado, string NRecibo, string NPagare, byte Metodo, string NumPago, decimal MontoEntrega);

    private AprobarModalResult MostrarModalAprobar(string avisos, string datosCliente, bool soloConfirmarEntrega = false)
    {
        bool  confirmado = false;
        bool  hayAvisos  = !string.IsNullOrEmpty(avisos);

        var win = new Window {
            Title = soloConfirmarEntrega ? "Confirmar Entrega" : "Aprobar Solicitud",
            // +70px sobre los valores originales: el checkbox de auto-asignar N° de Recibo
            // (agregado 2026-08-10) suma dos líneas de texto explicativo que no entraban en la
            // altura fija anterior, obligando a scrollear para ver los botones Confirmar/
            // Cancelar al pie — pedido explícito de que entre todo sin scroll.
            Width = 520, Height = hayAvisos ? 630 : 550,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.NoResize, Background = Brushes.White
        };

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new StackPanel { Margin = new Thickness(14, 12, 14, 12) };
        scroll.Content = root;

        // Alertas (si hay)
        if (hayAvisos)
        {
            var alertBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(255, 243, 205)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 140, 0)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 8)
            };
            var alertSp = new StackPanel();
            alertSp.Children.Add(new TextBlock {
                Text = "⚠  OBSERVACIONES:", FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 80, 0)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            alertSp.Children.Add(new TextBlock {
                Text = avisos, TextWrapping = TextWrapping.Wrap, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 50, 0))
            });
            alertBorder.Child = alertSp;
            root.Children.Add(alertBorder);
        }

        // Pregunta de confirmación
        root.Children.Add(new TextBlock {
            Text = soloConfirmarEntrega
                ? "Confirme el monto y método de pago de la entrega recibida — esto genera la venta y el movimiento de caja."
                : "¿Está seguro de querer APROBAR esta solicitud?",
            FontWeight = FontWeights.Bold, FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 100, 30)),
            Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap
        });

        // Datos del cliente
        var datosBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(235, 245, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(150, 180, 220)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 8)
        };
        datosBorder.Child = new TextBlock {
            Text = datosCliente, FontSize = 12,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(datosBorder);

        // Campos de registro del pago
        var pagoGroup = new Border {
            BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 8),
            Background = new SolidColorBrush(Color.FromRgb(248, 252, 248))
        };
        var pagoSp = new StackPanel();
        pagoSp.Children.Add(new TextBlock {
            Text = "DATOS DE REGISTRO", FontWeight = FontWeights.Bold, FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(40, 80, 40)),
            Margin = new Thickness(0, 0, 0, 6)
        });

        TextBlock Lbl(string t) => new TextBlock {
            Text = t, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            Margin = new Thickness(0, 0, 0, 3)
        };
        TextBox Txt() => new TextBox {
            Height = 26, Padding = new Thickness(6, 2, 6, 2), FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Monto de entrega — de solo lectura, tomado tal cual del monto ya aprobado en la
        // solicitud. Pedido explícito: dejarlo editable acá se presta a que un cajero baje
        // el monto registrado en el sistema y se quede con la diferencia en efectivo — el
        // control de ese monto ya pasó por Verificación/Aprobación, no debe poder tocarse
        // en este último paso. Solo se muestra en el paso de Confirmar Entrega.
        TextBlock? lblMontoEntrega = null;
        if (soloConfirmarEntrega)
        {
            pagoSp.Children.Add(Lbl("Monto de Entrega a confirmar (Gs.)"));
            lblMontoEntrega = new TextBlock {
                Text = _sol.Entrega.ToString("N0") + " Gs.",
                FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 100, 30)),
                Background = new SolidColorBrush(Color.FromRgb(235, 245, 235)),
                Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 0, 8)
            };
            pagoSp.Children.Add(lblMontoEntrega);
        }

        // Recibo y Pagaré lado a lado — antes apilados verticalmente dejaban mucho espacio
        // horizontal sin usar en un modal de este ancho.
        var reciboPagareGrid = new Grid();
        reciboPagareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        reciboPagareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        reciboPagareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var reciboSp = new StackPanel();
        reciboSp.Children.Add(Lbl("N° de Recibo *"));
        var txtRecibo = Txt();
        // No es un monto: es el número del talonario físico de recibos, único por
        // documento — si se repite el mismo número que otro ya emitido (ej. usar el
        // monto de la entrega por error), el SP legado rechaza la venta.
        txtRecibo.ToolTip = "Número del talonario de recibo físico (no el monto de la entrega)";
        reciboSp.Children.Add(txtRecibo);
        Grid.SetColumn(reciboSp, 0); reciboPagareGrid.Children.Add(reciboSp);

        // Auto-asignar N° de Recibo: algunos locales están dejando de usar talonario físico
        // y solo entregan el comprobante que imprime el sistema. IMPORTANTE: NRECIBO es única
        // GLOBAL en toda la tabla DOCUMENTOS (no por local) y es una secuencia totalmente
        // aparte del número de ticket/comprobante (sp_ObtenerNumeroTicket_cs) — reusar ese
        // contador como NRECIBO causó colisiones reales con recibos ya emitidos (bug
        // reproducido: auto-asignó "16", que ya existía). Por eso acá se calcula el próximo
        // número mirando el máximo NRECIBO numérico ya usado en DOCUMENTOS, con reintento si
        // otro cajero guarda al mismo tiempo y pisa el número (mismo patrón de choque que ya
        // usa VentaRepository antes del INSERT).
        async Task<string> ObtenerSiguienteNReciboAsync()
        {
            using var conn = _db.Create();
            for (var intento = 0; intento < 5; intento++)
            {
                // TRY_CAST no existe en SQL Server 2008 (la versión real de producción, ver
                // error reproducido: "'TRY_CAST' no es un nombre de función integrada
                // reconocida") — se reemplaza por el patrón clásico compatible desde SQL 2000:
                // NOT LIKE '%[^0-9]%' filtra a solo dígitos (evita que ISNUMERIC acepte cosas
                // como "1e5" o "-5" como numéricas, que CAST(...AS INT) rompería igual).
                // LEN(NRECIBO) <= 9 es el segundo fix real: algunos NRECIBO históricos son
                // códigos/folios largos de puros dígitos (ej. "0030010003366", 13 dígitos) que
                // pasan el filtro de "solo números" pero exceden el rango de INT (máx.
                // ~2.147 millones, 10 dígitos) — el CAST reventaba igual con "la conversión...
                // ha desbordado una columna int" (error reproducido en producción). Con 9
                // dígitos como tope, cualquier valor que pase el filtro cabe siempre en INT sin
                // desbordar, y esos folios largos simplemente se ignoran del cálculo del máximo
                // (no son candidatos válidos a "próximo N° de recibo" de todos modos).
                var max = await conn.ExecuteScalarAsync<int?>(
                    "SELECT MAX(CAST(NRECIBO AS INT)) FROM DOCUMENTOS " +
                    "WHERE NRECIBO IS NOT NULL AND NRECIBO NOT LIKE '%[^0-9]%' " +
                    "AND LEN(NRECIBO) > 0 AND LEN(NRECIBO) <= 9");
                var candidato = (max ?? 0) + 1 + intento;
                var yaExiste = await conn.ExecuteScalarAsync<bool>(
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM DOCUMENTOS WHERE NRECIBO = @r) THEN 1 ELSE 0 END",
                    new { r = candidato.ToString() });
                if (!yaExiste) return candidato.ToString();
            }
            return "";
        }

        var chkAutoRecibo = new CheckBox {
            Content = new TextBlock {
                Text = "¿Deseas que el sistema auto-asigne el N° de Recibo? Utilizá esto si no " +
                       "usarás recibo físico y usarás el comprobante que se imprime desde el sistema.",
                TextWrapping = TextWrapping.Wrap, FontSize = 10.5
            },
            Margin = new Thickness(0, 2, 0, 8)
        };
        reciboSp.Children.Add(chkAutoRecibo);

        var reciboOriginal = "";
        chkAutoRecibo.Checked += async (_, _) => {
            reciboOriginal = txtRecibo.Text;
            txtRecibo.IsEnabled = false;
            txtRecibo.Text = "(auto)";
            var numero = await ObtenerSiguienteNReciboAsync();
            // Si el usuario destildó el checkbox mientras se esperaba la consulta, no pisar
            // lo que haya vuelto a escribir manualmente mientras tanto.
            if (chkAutoRecibo.IsChecked != true) return;
            txtRecibo.Text = !string.IsNullOrEmpty(numero) ? numero : reciboOriginal;
        };
        chkAutoRecibo.Unchecked += (_, _) => {
            txtRecibo.IsEnabled = true;
            txtRecibo.Text = reciboOriginal;
            txtRecibo.Focus();
        };

        var pagareSp = new StackPanel();
        pagareSp.Children.Add(Lbl("N° de Pagaré"));
        var txtPagare = Txt();
        pagareSp.Children.Add(txtPagare);
        Grid.SetColumn(pagareSp, 2); reciboPagareGrid.Children.Add(pagareSp);

        pagoSp.Children.Add(reciboPagareGrid);

        pagoSp.Children.Add(Lbl("Método de pago/entrega"));
        var cboMetodo = new ComboBox { Height = 26, Margin = new Thickness(0, 0, 0, 8) };
        cboMetodo.Items.Add(new ComboBoxItem { Content = "EFECTIVO",        Tag = (byte)1, IsSelected = true });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta débito",  Tag = (byte)2 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "Tarjeta crédito", Tag = (byte)3 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "Transferencia",   Tag = (byte)4 });
        cboMetodo.Items.Add(new ComboBoxItem { Content = "QR",              Tag = (byte)5 });
        cboMetodo.SelectedIndex = 0;
        pagoSp.Children.Add(cboMetodo);

        var lblNumPago = Lbl("N° (tarjeta / comprobante)");
        var txtNumPago = Txt();
        txtNumPago.Margin = new Thickness(0, 0, 0, 0);
        pagoSp.Children.Add(lblNumPago);
        pagoSp.Children.Add(txtNumPago);

        cboMetodo.SelectionChanged += (_, _) => {
            bool esEfectivo = cboMetodo.SelectedItem is ComboBoxItem ci && (byte)(ci.Tag ?? (byte)1) == 1;
            lblNumPago.Visibility = esEfectivo ? Visibility.Collapsed : Visibility.Visible;
            txtNumPago.Visibility = esEfectivo ? Visibility.Collapsed : Visibility.Visible;
        };
        lblNumPago.Visibility = Visibility.Collapsed;
        txtNumPago.Visibility = Visibility.Collapsed;

        pagoGroup.Child = pagoSp;
        root.Children.Add(pagoGroup);

        // Botones
        var barBtns = new StackPanel {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnSi = new Button {
            Content = soloConfirmarEntrega ? "Confirmar" : "Sí, Aprobar",
            Width = 110, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(30, 140, 60)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        var btnNo = new Button {
            Content = "Cancelar", Width = 90, Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(180, 40, 40)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand
        };
        // N° de Recibo obligatorio: GUARDAR_VENTA_CREDITO_CS rechaza la venta si @NRECIBO
        // coincide con el de OTRO documento ya existente — con el campo vacío, cualquier
        // venta nueva sin recibo choca contra el primer documento histórico que también
        // quedó vacío, y el SP devuelve "Error" genérico sin más detalle (confirmado
        // reproduciendo el SP paso a paso: "ya existe un documento con NRECIBO vacío").
        btnSi.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(txtRecibo.Text))
            {
                MessageBox.Show("El N° de Recibo es obligatorio para continuar.",
                    "Falta un dato", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtRecibo.Focus();
                return;
            }
            // La auto-asignación consulta el correlativo de forma asíncrona (chkAutoRecibo.
            // Checked) — si el cajero confirma antes de que esa consulta termine, el campo
            // todavía muestra el placeholder "(auto)" en vez de un número real.
            if (chkAutoRecibo.IsChecked == true && txtRecibo.Text == "(auto)")
            {
                MessageBox.Show("Esperando a que el sistema asigne el N° de Recibo, intentá de nuevo en un instante.",
                    "Un momento", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            confirmado = true; win.Close();
        };
        btnNo.Click += (_, _) => { confirmado = false; win.Close(); };
        barBtns.Children.Add(btnSi);
        barBtns.Children.Add(btnNo);
        root.Children.Add(barBtns);

        win.Content = scroll;
        win.ShowDialog();

        byte metodoVal = cboMetodo.SelectedItem is ComboBoxItem selItem
            ? (byte)(selItem.Tag ?? (byte)1)
            : (byte)1;

        return new AprobarModalResult(
            confirmado,
            txtRecibo.Text.Trim(),
            txtPagare.Text.Trim(),
            metodoVal,
            txtNumPago.Text.Trim(),
            _sol.Entrega);
    }

    private UIElement BuildSeccionDatos()
    {
        _tbDatAprobado = Tb(_sol.FechaSolicitud.ToString("dd/MM/yyyy"));
        _tbDatTotal    = Tb(_sol.TotalVenta.ToString("N0"));
        _tbDatEntrega  = Tb(_sol.Entrega.ToString("N0"));
        _tbDatCuotas   = Tb(_sol.Cuotas.ToString());
        _tbDatMonto    = Tb(_sol.Cuotas > 0
            ? ((_sol.TotalVenta - _sol.Entrega) / _sol.Cuotas).ToString("N0") : "0");
        _tbDatPago     = Tb("—");

        // Barra de resumen financiero (azul oscuro)
        var bar = new Border {
            Background = BrPrimDark,
            Padding = new Thickness(12, 6, 12, 6)
        };
        var barSp = new StackPanel { Orientation = Orientation.Horizontal };
        void AddStat(string lbl, string val) {
            var col = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
            col.Children.Add(new TextBlock {
                Text = lbl, FontSize = 10, Foreground = BrBlanco, Opacity = 0.8
            });
            col.Children.Add(new TextBlock {
                Text = val, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BrBlanco
            });
            barSp.Children.Add(col);
        }
        AddStat("Total venta",   _sol.TotalVenta.ToString("N0") + " Gs.");
        AddStat("Entrega",       _sol.Entrega.ToString("N0")    + " Gs.");
        AddStat("Cuotas",        _sol.Cuotas.ToString());
        AddStat("Costo cuota",   _sol.Cuotas > 0
            ? ((_sol.TotalVenta - _sol.Entrega) / _sol.Cuotas).ToString("N0") + " Gs." : "—");
        AddStat("Fecha sol.",    _sol.FechaSolicitud.ToString("dd/MM/yyyy"));
        bar.Child = barSp;

        var outer = new StackPanel();
        outer.Children.Add(SecCard("Resumen financiero", bar));
        return outer;
    }


    private Grid BuildIngresosEgresosGrid()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // etiqueta
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // INGRESOS val
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // etiqueta egresos
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // EGRESOS val
        for (int i = 0; i < 7; i++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Encabezados
        var hIng = new TextBlock { Text = "INGRESOS", FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, Foreground = BrBlanco,
            Background = BrVerde, Padding = new Thickness(4, 3, 4, 3), FontSize = 11 };
        Grid.SetColumn(hIng, 0); Grid.SetColumnSpan(hIng, 2); Grid.SetRow(hIng, 0);
        g.Children.Add(hIng);

        var hEgr = new TextBlock { Text = "EGRESOS", FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, Foreground = BrBlanco,
            Background = BrRojo, Padding = new Thickness(4, 3, 4, 3), FontSize = 11 };
        Grid.SetColumn(hEgr, 2); Grid.SetColumnSpan(hEgr, 2); Grid.SetRow(hEgr, 0);
        g.Children.Add(hEgr);

        _tbISalario   = Tb("0"); _tbIHonorario = Tb("0"); _tbIConyuge = Tb("0");
        _tbIOtros     = Tb("0"); _tbITotal     = Tb("0");
        _tbEGasto     = Tb("0"); _tbECuota     = Tb("0"); _tbEAlquiler= Tb("0");
        _tbEOtros     = Tb("0"); _tbETotal     = Tb("0"); _tbSaldo    = Tb("0");

        AddIERow(g, 1, "Salario",        _tbISalario,   "Gastos familiares", _tbEGasto);
        AddIERow(g, 2, "Honorario Prof.",_tbIHonorario, "Cuotas",            _tbECuota);
        AddIERow(g, 3, "Salario Cónyuge",_tbIConyuge,   "Alquiler",          _tbEAlquiler);
        AddIERow(g, 4, "Otros",          _tbIOtros,     "Otros",             _tbEOtros);

        var totalBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 244, 246));
        // TOTAL row
        var lbTI = new TextBlock { Text = "TOTAL", FontWeight = FontWeights.Bold,
            FontSize = 11, Padding = new Thickness(4, 3, 4, 3), Background = totalBg };
        Grid.SetColumn(lbTI, 0); Grid.SetRow(lbTI, 5); g.Children.Add(lbTI);
        _tbITotal.FontWeight = FontWeights.Bold; _tbITotal.Background = totalBg;
        _tbITotal.Foreground = BrVerde;
        Grid.SetColumn(_tbITotal, 1); Grid.SetRow(_tbITotal, 5); g.Children.Add(_tbITotal);

        var lbTE = new TextBlock { Text = "TOTAL", FontWeight = FontWeights.Bold,
            FontSize = 11, Padding = new Thickness(4, 3, 4, 3), Background = totalBg };
        Grid.SetColumn(lbTE, 2); Grid.SetRow(lbTE, 5); g.Children.Add(lbTE);
        _tbETotal.FontWeight = FontWeights.Bold; _tbETotal.Background = totalBg;
        _tbETotal.Foreground = BrRojo;
        Grid.SetColumn(_tbETotal, 3); Grid.SetRow(_tbETotal, 5); g.Children.Add(_tbETotal);

        // SALDO
        var saldoBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 247, 237));
        var lbS = new TextBlock { Text = "SALDO:", FontWeight = FontWeights.Bold, FontSize = 11,
            Padding = new Thickness(4, 3, 4, 3), Background = saldoBg, Foreground = BrPrimDark };
        Grid.SetColumn(lbS, 0); Grid.SetColumnSpan(lbS, 2); Grid.SetRow(lbS, 6); g.Children.Add(lbS);
        _tbSaldo.FontWeight = FontWeights.Bold; _tbSaldo.Background = saldoBg;
        _tbSaldo.Foreground = BrPrimDark;
        Grid.SetColumn(_tbSaldo, 2); Grid.SetColumnSpan(_tbSaldo, 2); Grid.SetRow(_tbSaldo, 6);
        g.Children.Add(_tbSaldo);

        return g;
    }

    private static void AddIERow(Grid g, int row, string lblI, TextBlock valI, string lblE, TextBlock valE)
    {
        var li = new TextBlock { Text = lblI, Padding = new Thickness(4,2,4,2) };
        Grid.SetColumn(li, 0); Grid.SetRow(li, row); g.Children.Add(li);
        Grid.SetColumn(valI, 1); Grid.SetRow(valI, row); g.Children.Add(valI);
        var le = new TextBlock { Text = lblE, Padding = new Thickness(4,2,4,2) };
        Grid.SetColumn(le, 2); Grid.SetRow(le, row); g.Children.Add(le);
        Grid.SetColumn(valE, 3); Grid.SetRow(valE, row); g.Children.Add(valE);
    }

    // ── Carga de datos ────────────────────────────────────────────────────────
    private async Task CargarDetalleAsync()
    {
        try
        {
            using var conn = _db.Create();

            var cab = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT " +
                "  s.ID_CLIENTE as IdCli, s.ID_GARANTE as IdGar," +
                "  cl.NOMBRE_CLIENTE as NomCli, cl.CI_CLIENTE as Ci, cl.RUC_CLIENTE as Ruc," +
                "  cl.DIRECCION_CLIENTE as Dir, cl.TELEFONO_CLIENTE as Cel, cl.CIUDAD_CLIENTE as Ciudad," +
                "  cl.ECV as Ecv, cl.CRED_MAX as CredMax, cl.CONDICION as Condicion," +
                "  cl.TIPO as Tipo, cl.VENC_CEDULA as VencCi, cl.CONYUGE as Conyuge," +
                "  g.NOMBRE_CLIENTE as NomGar, g.CI_CLIENTE as CiGar," +
                "  g.DIRECCION_CLIENTE as DirGar, g.TELEFONO_CLIENTE as CelGar," +
                "  g.CIUDAD_CLIENTE as CiudadGar, g.EMPRESA_LABORAL as EmpGar," +
                "  g.TELEFONO_LABORAL as TelLabGar, g.ECV as EcvGar," +
                "  s.NOTA as Nota, s.PERMISO_EXEPCION as PermisoExc," +
                "  s.NOM_REFERENCIA1 as Ref1Nom, s.TEL_REFERENCIA1 as Ref1Tel, s.TRAB_REFERENCIA1 as Ref1Trab," +
                "  s.NOM_REFERENCIA2 as Ref2Nom, s.TEL_REFERENCIA2 as Ref2Tel, s.TRAB_REFERENCIA2 as Ref2Trab," +
                "  s.NOM_REFERENCIACOMERCIAL1 as RefC1Nom, s.TEL_REFERENCIACOMERCIAL1 as RefC1Tel," +
                "  s.NOM_REFERENCIACOMERCIAL2 as RefC2Nom, s.TEL_REFERENCIACOMERCIAL2 as RefC2Tel," +
                "  s.I_SALARIO as ISal, s.I_HONORARIO as IHon, s.I_CONYUGE as ICon," +
                "  s.I_OTROS as IOtros, s.I_TOTAL as ITotal," +
                "  s.E_GASTO as EGas, s.E_CUOTA as ECuo, s.E_ALQUILER as EAlq," +
                "  s.E_OTROS as EOtros, s.E_TOTAL as ETotal" +
                " FROM CAB_SOL_SALES s" +
                " LEFT JOIN CLIENTES cl ON s.ID_CLIENTE = cl.ID_CLIENTE" +
                " LEFT JOIN CLIENTES g  ON s.ID_GARANTE  = g.ID_CLIENTE" +
                " WHERE s.IDSOLICITUD = @id", new { id = _sol.IdSolicitud });

            if (cab != null)
            {
                static string S(object? v) => v?.ToString()?.Trim() is { Length: > 0 } s ? s : "—";
                static string N(object? v) => v is decimal d ? d.ToString("N0") : "0";
                static void Set(TextBlock tb, string v) { tb.Text = v; tb.ToolTip = v == "—" ? null : v; }

                // Cliente
                Set(_tbCliNombre,    S(cab.NomCli));
                Set(_tbCliCi,        S(cab.Ci));
                Set(_tbCliDir,       S(cab.Dir));
                Set(_tbCliCel,       S(cab.Cel));
                Set(_tbCliCiudad,    S(cab.Ciudad));
                Set(_tbCliEcv,       ((byte?)cab.Ecv) switch { 1=>"Soltero",2=>"Casado",3=>"Divorciado",4=>"Viudo",_=>"—" });
                Set(_tbCliCredMax,   N(cab.CredMax));
                Set(_tbCliCondicion, ((byte?)cab.Condicion) switch { 1=>"Activo",2=>"Inactivo",_=>"—" });
                Set(_tbCliTipo,      ((byte?)cab.Tipo) switch { 1=>"Bueno",2=>"Regular",3=>"Malo",_=>"—" });
                Set(_tbCliVencCi,    cab.VencCi is DateTime vd ? vd.ToString("dd/MM/yyyy") : "—");
                Set(_tbCliConyuge,   S(cab.Conyuge));
                Set(_tbCliSaldo,     "—");

                // Garante
                _tbGarNombre.Text   = S(cab.NomGar);
                _tbGarCi.Text       = S(cab.CiGar);
                _tbGarDir.Text      = S(cab.DirGar);
                _tbGarCel.Text      = S(cab.CelGar);
                _tbGarCiudad.Text   = S(cab.CiudadGar);
                _tbGarEmpresa.Text  = S(cab.EmpGar);
                _tbGarTelLab.Text   = S(cab.TelLabGar);
                _tbGarEcv.Text      = ((byte?)cab.EcvGar) switch { 1=>"Soltero",2=>"Casado",3=>"Divorciado",4=>"Viudo",_=>"—" };

                // Referencias
                _tbRef1Nom.Text    = S(cab.Ref1Nom);  _tbRef1Tel.Text  = S(cab.Ref1Tel);  _tbRef1Trab.Text = S(cab.Ref1Trab);
                _tbRef2Nom.Text    = S(cab.Ref2Nom);  _tbRef2Tel.Text  = S(cab.Ref2Tel);  _tbRef2Trab.Text = S(cab.Ref2Trab);
                _tbRefCom1Nom.Text = S(cab.RefC1Nom); _tbRefCom1Tel.Text = S(cab.RefC1Tel);
                _tbRefCom2Nom.Text = S(cab.RefC2Nom); _tbRefCom2Tel.Text = S(cab.RefC2Tel);

                // Ingresos / Egresos
                _tbISalario.Text   = N(cab.ISal);   _tbIHonorario.Text = N(cab.IHon);
                _tbIConyuge.Text   = N(cab.ICon);   _tbIOtros.Text     = N(cab.IOtros);
                _tbITotal.Text     = N(cab.ITotal);
                _tbEGasto.Text     = N(cab.EGas);   _tbECuota.Text     = N(cab.ECuo);
                _tbEAlquiler.Text  = N(cab.EAlq);   _tbEOtros.Text     = N(cab.EOtros);
                _tbETotal.Text     = N(cab.ETotal);
                decimal saldo = ((decimal?)cab.ITotal ?? 0) - ((decimal?)cab.ETotal ?? 0);
                _tbSaldo.Text = saldo.ToString("N0");

                // Nota
                _txtNota.Text = (string?)cab.Nota ?? "";

                // Guardar estado para botones Historial / Ver cédula / Garante
                _idClienteCargado  = (int?)cab.IdCli  ?? 0;
                _fotoCedulaCliente = S(cab.Ci) == "—" ? "" : S(cab.Ci);
                _idGaranteCargado  = (int?)cab.IdGar  ?? 0;
                _nomGaranteCargado = S(cab.NomGar);

                // Autorización por excepción
                _permisoExcepcion  = (byte?)cab.PermisoExc ?? 0;
                ActualizarBtnAutorizar();
            }

            // Productos
            var detalles = await conn.QueryAsync<dynamic>(
                "SELECT a.CA as Codigo, a.D as Descripcion," +
                "  d.PRECIO as Precio, d.ENTREGA as Entrega, d.CANTCUOTAS as Cuotas," +
                "  d.COSTOMENSUAL as CostoMens, d.VALORFINAL as ValorFinal," +
                "  d.CANTIDAD as Cantidad, d.SUBTOTAL as TotalGral" +
                " FROM DET_SOL_SALES d" +
                " LEFT JOIN ARTICULOS a ON d.IDART = a.ID" +
                " WHERE d.IDSOLICITUD = @id ORDER BY a.D", new { id = _sol.IdSolicitud });

            _gridProductos.ItemsSource = detalles.Select(d => new DetalleSolRow {
                Codigo      = (string?)d.Codigo      ?? "",
                Descripcion = (string?)d.Descripcion ?? "",
                Precio      = (decimal?)d.Precio     ?? 0,
                Entrega     = (decimal?)d.Entrega    ?? 0,
                Cuotas      = (int?)d.Cuotas         ?? 0,
                CostoMens   = (decimal?)d.CostoMens  ?? 0,
                ValorFinal  = (decimal?)d.ValorFinal ?? 0,
                Cantidad    = (decimal?)d.Cantidad   ?? 0,
                TotalGral   = (decimal?)d.TotalGral  ?? 0,
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar detalle: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<SolicitudFichaData?> BuildFichaDataAsync()
    {
        try
        {
            using var conn = _db.Create();

            var cab = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT " +
                "  s.ID_CLIENTE as IdCli, s.ID_GARANTE as IdGar," +
                "  cl.NOMBRE_CLIENTE as NomCli, cl.CI_CLIENTE as Ci, cl.RUC_CLIENTE as Ruc," +
                "  cl.DIRECCION_CLIENTE as Dir, cl.TELEFONO_CLIENTE as Cel, cl.CIUDAD_CLIENTE as Ciudad," +
                "  cl.ECV as Ecv, cl.CRED_MAX as CredMax, cl.CONDICION as Condicion," +
                "  cl.TIPO as Tipo, cl.VENC_CEDULA as VencCi, cl.CONYUGE as Conyuge," +
                "  g.NOMBRE_CLIENTE as NomGar, g.CI_CLIENTE as CiGar," +
                "  g.DIRECCION_CLIENTE as DirGar, g.TELEFONO_CLIENTE as CelGar," +
                "  g.EMPRESA_LABORAL as EmpGar, g.TELEFONO_LABORAL as TelLabGar, g.ECV as EcvGar," +
                "  s.NOTA as Nota," +
                "  s.NOM_REFERENCIA1 as Ref1Nom, s.TEL_REFERENCIA1 as Ref1Tel, s.TRAB_REFERENCIA1 as Ref1Trab," +
                "  s.NOM_REFERENCIA2 as Ref2Nom, s.TEL_REFERENCIA2 as Ref2Tel, s.TRAB_REFERENCIA2 as Ref2Trab," +
                "  s.NOM_REFERENCIACOMERCIAL1 as RefC1Nom, s.TEL_REFERENCIACOMERCIAL1 as RefC1Tel," +
                "  s.NOM_REFERENCIACOMERCIAL2 as RefC2Nom, s.TEL_REFERENCIACOMERCIAL2 as RefC2Tel," +
                "  s.I_SALARIO as ISal, s.I_HONORARIO as IHon, s.I_CONYUGE as ICon," +
                "  s.I_OTROS as IOtros, s.I_TOTAL as ITotal," +
                "  s.E_GASTO as EGas, s.E_CUOTA as ECuo, s.E_ALQUILER as EAlq," +
                "  s.E_OTROS as EOtros, s.E_TOTAL as ETotal," +
                "  s.CANTCUOTAS as Cuotas, s.TOTALSALE as TotalVenta, s.TOTALENTREGA as TotalEntrega," +
                "  s.TOTAL_MONTO_CUOTA as CostoCuota," +
                "  u.NOMBRE_USUARIO as NomVend, l.NOMBRE as NomLocal, s.FECHA_SOLICITUD as FechaSol" +
                " FROM CAB_SOL_SALES s" +
                " LEFT JOIN CLIENTES cl ON s.ID_CLIENTE = cl.ID_CLIENTE" +
                " LEFT JOIN CLIENTES g  ON s.ID_GARANTE  = g.ID_CLIENTE" +
                " LEFT JOIN USUARIOS u  ON s.ID_USUARIO  = u.ID_USUARIO" +
                " LEFT JOIN LOCALES  l  ON s.ID_LOCAL    = l.ID_LOCAL" +
                " WHERE s.IDSOLICITUD = @id", new { id = _sol.IdSolicitud });

            if (cab == null) return null;

            static string S(object? v) => v?.ToString()?.Trim() is { Length: > 0 } s ? s : "—";
            static string N(object? v) => v is decimal d ? d.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY")) : "0";

            var detalles = (await conn.QueryAsync<dynamic>(
                "SELECT a.CA as Codigo, a.D as Descripcion," +
                "  d.PRECIO as Precio, d.ENTREGA as Entrega, d.CANTCUOTAS as Cuotas," +
                "  d.COSTOMENSUAL as CostoMens, d.VALORFINAL as ValorFinal," +
                "  d.CANTIDAD as Cantidad, d.SUBTOTAL as TotalGral" +
                " FROM DET_SOL_SALES d" +
                " LEFT JOIN ARTICULOS a ON d.IDART = a.ID" +
                " WHERE d.IDSOLICITUD = @id ORDER BY a.D", new { id = _sol.IdSolicitud }))
                .Select(d => new DetalleSolRow {
                    Codigo      = (string?)d.Codigo      ?? "",
                    Descripcion = (string?)d.Descripcion ?? "",
                    Precio      = (decimal?)d.Precio     ?? 0,
                    Entrega     = (decimal?)d.Entrega    ?? 0,
                    Cuotas      = (int?)d.Cuotas         ?? 0,
                    CostoMens   = (decimal?)d.CostoMens  ?? 0,
                    ValorFinal  = (decimal?)d.ValorFinal ?? 0,
                    Cantidad    = (decimal?)d.Cantidad   ?? 0,
                    TotalGral   = (decimal?)d.TotalGral  ?? 0,
                }).ToList();

            // Foto cédula
            System.Drawing.Image? fotoDoc = null;
            try
            {
                var ci = S(cab.Ci);
                var idCli = (int?)cab.IdCli ?? 0;
                if (ci != "—" || idCli > 0)
                {
                    var datos = await conn.QueryFirstOrDefaultAsync<byte[]>(
                        "SELECT TOP 1 DATOS FROM FOTOS WHERE CI = @ci OR IDCLIE = @id ORDER BY IDFOTO DESC",
                        new { ci, id = idCli });
                    if (datos != null && datos.Length > 0)
                        fotoDoc = System.Drawing.Image.FromStream(new System.IO.MemoryStream(datos));
                }
            }
            catch { }

            decimal iTotal = (decimal?)cab.ITotal ?? 0;
            decimal eTotal = (decimal?)cab.ETotal ?? 0;
            decimal saldo  = iTotal - eTotal;

            // Fecha solicitud
            string fechaSol = "—";
            if (cab.FechaSol is DateTime fsDt)
                fechaSol = fsDt.ToString("dd/MM/yyyy");
            else if (cab.FechaSol is string fsStr && !string.IsNullOrEmpty(fsStr))
                fechaSol = fsStr;

            return new SolicitudFichaData {
                Numero     = _sol.Numero,
                Estado     = _sol.Estado,
                Fecha      = fechaSol,
                Local      = S(cab.NomLocal),
                Vendedor   = S(cab.NomVend),
                Nota       = (string?)cab.Nota ?? "",
                CliNombre  = S(cab.NomCli),   CliCi      = S(cab.Ci),
                CliRuc     = S(cab.Ruc),       CliDir     = S(cab.Dir),
                CliCel     = S(cab.Cel),       CliCiudad  = S(cab.Ciudad),
                CliEcv     = ((byte?)cab.Ecv) switch { 1=>"Soltero",2=>"Casado",3=>"Divorciado",4=>"Viudo",_=>"—" },
                CliCredMax = N(cab.CredMax),   CliSaldo   = "—",
                CliCondicion=((byte?)cab.Condicion) switch { 1=>"Activo",2=>"Inactivo",_=>"—" },
                CliTipo    = ((byte?)cab.Tipo) switch { 1=>"Bueno",2=>"Regular",3=>"Malo",_=>"—" },
                CliVencCi  = cab.VencCi is DateTime vd ? vd.ToString("dd/MM/yyyy") : "—",
                CliConyuge = S(cab.Conyuge),
                GarNombre  = S(cab.NomGar),    GarCi      = S(cab.CiGar),
                GarDir     = S(cab.DirGar),    GarCel     = S(cab.CelGar),
                GarEmpresa = S(cab.EmpGar),    GarTelLab  = S(cab.TelLabGar),
                GarEcv     = ((byte?)cab.EcvGar) switch { 1=>"Soltero",2=>"Casado",3=>"Divorciado",4=>"Viudo",_=>"—" },
                Ref1Nom    = S(cab.Ref1Nom),   Ref1Tel    = S(cab.Ref1Tel),   Ref1Trab   = S(cab.Ref1Trab),
                Ref2Nom    = S(cab.Ref2Nom),   Ref2Tel    = S(cab.Ref2Tel),   Ref2Trab   = S(cab.Ref2Trab),
                RefC1Nom   = S(cab.RefC1Nom),  RefC1Tel   = S(cab.RefC1Tel),
                RefC2Nom   = S(cab.RefC2Nom),  RefC2Tel   = S(cab.RefC2Tel),
                ISal       = N(cab.ISal),       IHon       = N(cab.IHon),
                ICon       = N(cab.ICon),       IOtros     = N(cab.IOtros),    ITotal = N(cab.ITotal),
                EGas       = N(cab.EGas),       ECuo       = N(cab.ECuo),
                EAlq       = N(cab.EAlq),       EOtros     = N(cab.EOtros),    ETotal = N(cab.ETotal),
                Saldo      = saldo.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY")),
                Productos  = detalles,
                TotalVenta = (decimal?)cab.TotalVenta  ?? 0,
                TotalEntrega=(decimal?)cab.TotalEntrega?? 0,
                Cuotas     = (int?)cab.Cuotas          ?? 0,
                CostoCuota = (decimal?)cab.CostoCuota  ?? 0,
                FotoDoc    = fotoDoc,
                LogoPath   = ArticulosPagina.ResolverLogoPath(),
                Usuario    = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "",
                FechaImp   = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al preparar la ficha:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    private async Task GuardarAsync()
    {
        try
        {
            using var conn = _db.Create();

            // Guardar nota + garante si fue asignado desde el buscador
            if (_idGaranteCargado > 0)
            {
                await conn.ExecuteAsync(
                    "UPDATE CAB_SOL_SALES SET NOTA=@Nota, ID_GARANTE=@Gar WHERE IDSOLICITUD=@Id",
                    new { Nota = _txtNota.Text, Gar = _idGaranteCargado, Id = _sol.IdSolicitud });
            }
            else
            {
                await conn.ExecuteAsync(
                    "UPDATE CAB_SOL_SALES SET NOTA=@Nota WHERE IDSOLICITUD=@Id",
                    new { Nota = _txtNota.Text, Id = _sol.IdSolicitud });
            }

            MessageBox.Show("Guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Helpers de UI ────────────────────────────────────────────────────────

    private static void AddCell(Grid g, string label, TextBlock val, int row, int col, int colSpanVal = 1)
    {
        var lbl = new TextBlock {
            Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabelTxt,
            Padding = new Thickness(2, 3, 6, 3), VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(lbl, row); Grid.SetColumn(lbl, col); g.Children.Add(lbl);

        val.Padding = new Thickness(3, 3, 3, 3);
        val.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(val, row); Grid.SetColumn(val, col + 1);
        if (colSpanVal > 1) Grid.SetColumnSpan(val, colSpanVal * 2 - 1);
        g.Children.Add(val);
    }

    private static TextBlock Tb(string text) => new TextBlock {
        Text = text, FontSize = 12, Padding = new Thickness(3, 3, 3, 3),
        Foreground = BrValTxt, VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = string.IsNullOrEmpty(text) ? null : text
    };

    private static Button Btn(string text, string hex) => new Button {
        Content = text, Height = 32, Padding = new Thickness(18, 0, 18, 0),
        Margin = new Thickness(0, 0, 8, 0),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = BrBlanco, Cursor = System.Windows.Input.Cursors.Hand, FontSize = 13,
        FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0)
    };

    private static Button BtnAccion(string text, string hex) => new Button {
        Content = text, Height = 26, Padding = new Thickness(10, 0, 10, 0),
        Margin = new Thickness(0, 2, 6, 2),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = BrBlanco, Cursor = System.Windows.Input.Cursors.Hand, FontSize = 11,
        FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0)
    };

    private void AbrirFichaCliente()
    {
        if (_idClienteCargado == 0) return;
        MessageBox.Show($"ID Cliente: {_idClienteCargado}\n(Ficha de cliente — pendiente de implementar)",
            "Cliente", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MostrarHistorial()
    {
        if (_idClienteCargado == 0) return;
        // Mismo modal completo (créditos reales + saldo + garante + detalle de cuotas) que usa
        // Venta a Crédito — antes esta pantalla armaba su propio historial pobre, sobre
        // CAB_SOL_SALES (solicitudes, no ventas reales), sin cuotas ni saldo ni garante.
        CrediSoft.UI.Views.Shared.HistorialCrediticioModal.Mostrar(this, _db, _idClienteCargado, _tbCliNombre.Text);
    }

    private async void VerCedula()
    {
        if (string.IsNullOrEmpty(_fotoCedulaCliente) && _idClienteCargado == 0) {
            MessageBox.Show("No se encontró número de C.I. para buscar la foto.",
                "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        byte[]? datos = null;
        try
        {
            using var conn = _db.Create();
            datos = await conn.QueryFirstOrDefaultAsync<byte[]>(
                "SELECT TOP 1 DATOS FROM FOTOS WHERE CI = @ci OR IDCLIE = @id ORDER BY IDFOTO DESC",
                new { ci = _fotoCedulaCliente, id = _idClienteCargado });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al consultar la foto: {ex.Message}",
                "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (datos == null || datos.Length == 0) {
            MessageBox.Show($"No se encontró foto de cédula para el cliente (CI: {_fotoCedulaCliente}).",
                "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BitmapImage bmp;
        try
        {
            bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(datos);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 500; // reduce desde la decodificación, sin pérdida visible
            bmp.EndInit();
            bmp.Freeze();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo cargar la imagen: {ex.Message}",
                "Ver cédula", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        double escala    = 1.0;
        double rotacion  = 0.0;
        var tg = new TransformGroup();
        var st = new ScaleTransform(1, 1);
        var rt = new RotateTransform(0);
        tg.Children.Add(st);
        tg.Children.Add(rt);

        var img = new System.Windows.Controls.Image {
            Source = bmp,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            LayoutTransform = tg,
            Margin = new Thickness(8)
        };

        var scroll = new ScrollViewer {
            Content = img,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };

        var win = new Window {
            Title = $"Cédula  —  CI: {_fotoCedulaCliente}",
            Width = 580, Height = 500,
            MinWidth = 420, MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.CanResize,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        };

        void AplicarTransform()
        {
            st.ScaleX = escala; st.ScaleY = escala;
            rt.Angle  = rotacion;
            img.Stretch = escala <= 1.0 ? Stretch.Uniform : Stretch.None;
        }

        // Helper: botón moderno oscuro con ícono+texto apilados
        Button BtnViewer(string icono, string label, Color bg)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock {
                Text = icono, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.White
            });
            sp.Children.Add(new TextBlock {
                Text = label, FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 210))
            });
            return new Button {
                Content = sp, Height = 52, Width = 64, Margin = new Thickness(3, 3, 3, 3),
                Background = new SolidColorBrush(bg),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 100)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(0)
            };
        }

        var btnDis   = BtnViewer("🔍−", "Alejar",    Color.FromRgb(55,  55,  70));
        var btnAum   = BtnViewer("🔍+", "Acercar",   Color.FromRgb(25,  90, 160));
        var btnReset = BtnViewer("⊡",   "Normal",    Color.FromRgb(60,  60,  60));
        var btnRotL  = BtnViewer("↺",   "Rotar ←",  Color.FromRgb(70,  50, 100));
        var btnRotR  = BtnViewer("↻",   "Rotar →",  Color.FromRgb(70,  50, 100));
        var btnCer   = BtnViewer("✕",   "Cerrar",    Color.FromRgb(160, 30,  30));

        btnDis.Click   += (_, _) => { escala   = Math.Max(escala - 0.25, 0.25);    AplicarTransform(); };
        btnAum.Click   += (_, _) => { escala   = Math.Min(escala + 0.25, 5.0);     AplicarTransform(); };
        btnReset.Click += (_, _) => { escala   = 1.0; rotacion = 0.0;              AplicarTransform(); };
        btnRotL.Click  += (_, _) => { rotacion = (rotacion - 90 + 360) % 360;      AplicarTransform(); };
        btnRotR.Click  += (_, _) => { rotacion = (rotacion + 90) % 360;            AplicarTransform(); };
        btnCer.Click   += (_, _) => win.Close();

        // Separador vertical decorativo
        var sep = new Border {
            Width = 1, Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
            Margin = new Thickness(6, 8, 6, 8)
        };

        var bar = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(22, 22, 28))
        };
        bar.Children.Add(btnDis);
        bar.Children.Add(btnReset);
        bar.Children.Add(btnAum);
        bar.Children.Add(sep);
        bar.Children.Add(btnRotL);
        bar.Children.Add(btnRotR);
        bar.Children.Add(new Border { Width = 1, Background = new SolidColorBrush(Color.FromRgb(70,70,70)), Margin = new Thickness(6,8,6,8) });
        bar.Children.Add(btnCer);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
        Grid.SetRow(scroll, 0);
        Grid.SetRow(bar, 1);
        root.Children.Add(scroll);
        root.Children.Add(bar);

        win.Content = root;
        win.ShowDialog();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BUSCADOR DE PERSONA  (lista de referentes / garante)
// ══════════════════════════════════════════════════════════════════════════════

public class PersonaItem
{
    public int    Fila       { get; set; }
    public int    Id         { get; set; }
    public string Ci         { get; set; } = "";
    public string Nombre     { get; set; } = "";
    public string Telefono   { get; set; } = "";
    public string Direccion  { get; set; } = "";
    public string Ciudad     { get; set; } = "";
    public string Empresa    { get; set; } = "";
    public string TelLaboral { get; set; } = "";
    public string Ecv        { get; set; } = "";
    // campos extendidos del cliente
    public string Sexo       { get; set; } = "";
    public string Inforconf  { get; set; } = "";
    public string Tipo       { get; set; } = "";
    public string Condicion  { get; set; } = "";
    public string EstadoTexto{ get; set; } = "";
    public string Conyuge    { get; set; } = "";
    public string VencCI     { get; set; } = "";
    public decimal CredMax   { get; set; }
    public decimal SaldoActivo { get; set; }
    public string Antiguedad { get; set; } = "";
    public string Ruc        { get; set; } = "";
}

// DTO tipado para mapeo Dapper — evita problemas de cast con dynamic
file class ClienteRow
{
    public long     Fila       { get; set; }
    public int      Id         { get; set; }
    public string?  Ci         { get; set; }
    public string?  Ruc        { get; set; }
    public string?  Nombre     { get; set; }
    public string?  Telefono   { get; set; }
    public string?  Dir        { get; set; }
    public string?  Ciudad     { get; set; }
    public string?  Empresa    { get; set; }
    public string?  TelLab     { get; set; }
    public int      EcvNum     { get; set; }
    public int      SexoNum    { get; set; }
    public int      Inforcom   { get; set; }
    public int      TipoNum    { get; set; }
    public int      CondNum    { get; set; }
    public int      EstNum     { get; set; }
    public decimal  CredMax    { get; set; }
    public string?  Antiguedad { get; set; }
    public string?  Conyuge    { get; set; }
    public DateTime? VencCI    { get; set; }
    public decimal  SaldoActivo{ get; set; }
}

public class BuscadorPersonaWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtBuscar = null!;
    private DataGrid  _grid      = null!;
    // paginación BD — ya no cargamos toda la lista en memoria
    private int _paginaBusc    = 1;
    private int _porPagina     = 20;
    private int _totalPersonas = 0;
    private bool _cargandoBusc = false;
    private System.Threading.CancellationTokenSource? _busc_cts;
    private Button    _btnBuscAnt  = null!;
    private Button    _btnBuscSig  = null!;
    private TextBlock _lblPagBusc  = null!;

    public PersonaItem? PersonaSeleccionada { get; private set; }

    private TextBlock _lblConteo = null!;

    private static readonly SolidColorBrush _BrPrim  = new(Color.FromRgb( 21,101,192));
    private static readonly SolidColorBrush _BrDark  = new(Color.FromRgb( 14, 47, 68));
    private static readonly SolidColorBrush _BrFondo = new(Color.FromRgb(238,244,251));
    private static readonly SolidColorBrush _BrCard  = Brushes.White;
    private static readonly SolidColorBrush _BrBorde = new(Color.FromRgb(187,222,251));
    private static readonly SolidColorBrush _BrLabel = new(Color.FromRgb(107,114,128));
    private static readonly SolidColorBrush _BrAlt   = new(Color.FromRgb(232,240,254));

    public BuscadorPersonaWindow(IDbConnectionFactory db)
    {
        _db = db;
        Title = "Buscar Persona / Referencia";
        Width = 860; Height = 580; MinWidth = 640; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = _BrFondo;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await CargarAsync();

        // Esta ventana necesita Owner (para no quedar "huérfana" y minimizarse sola al
        // abrir otro módulo — mismo bug ya visto en DetalleSolicitudWindow), pero con
        // Owner, Windows minimiza TODO el proceso en cadena si el usuario minimiza esta
        // ventana puntual — bug real reportado ("minimizo el buscador y se minimiza todo
        // el programa"). En vez de sacar el Owner (lo que reintroduce el minimizado
        // espontáneo), directamente no se permite minimizar esta ventana: si el estado
        // pasa a Minimized por cualquier vía (botón, Win+D, doble-click en la barra de
        // tareas), se revierte a Normal en el acto.
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; };
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header azul ─────────────────────────────────────────────────────
        var hdr = new Border { Background = _BrPrim, Padding = new Thickness(16,12,16,12) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "👤  Buscar Persona",
            Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0,0,0,8)
        });

        // Caja de búsqueda integrada en el header
        var searchBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(11, 40, 60)),
            CornerRadius = new CornerRadius(6),
            BorderBrush  = new SolidColorBrush(Color.FromRgb(30,  136, 229)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10,0,6,0)
        };
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        searchRow.Children.Add(new TextBlock {
            Text = "🔎", FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(187,222,251)),
            Margin = new Thickness(0,0,8,0)
        });
        _txtBuscar = new TextBox {
            Height = 34, MinWidth = 360, FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = Brushes.White, CaretBrush = Brushes.White,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _txtBuscar.TextChanged += (_, _) => Filtrar();
        _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Aceptar(); };
        searchRow.Children.Add(_txtBuscar);

        // Placeholder simulado con hint
        searchRow.Children.Add(new TextBlock {
            Text = "Nombre, C.I. o ciudad...",
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromArgb(120,187,222,251)),
            Margin = new Thickness(4,0,0,0), IsHitTestVisible = false
        });

        searchBorder.Child = searchRow;
        hdrSp.Children.Add(searchBorder);
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── DataGrid ────────────────────────────────────────────────────────
        var gridWrap = new Border {
            Margin = new Thickness(10,8,10,0),
            BorderBrush = _BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), ClipToBounds = true
        };
        gridWrap.Effect = new System.Windows.Media.Effects.DropShadowEffect {
            BlurRadius = 5, ShadowDepth = 1, Opacity = 0.07,
            Color = Color.FromRgb(0,0,0)
        };

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            RowHeight = 34, ColumnHeaderHeight = 32, FontSize = 12,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = _BrBorde,
            RowBackground = _BrCard,
            AlternatingRowBackground = _BrAlt,
            BorderThickness = new Thickness(0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            SelectionUnit = DataGridSelectionUnit.FullRow
        };

        // Header azul
        var hs = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, _BrPrim));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, Brushes.White));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 11.5));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10,0,10,0)));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0,0,1,0)));
        hs.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderBrushProperty, _BrDark));
        _grid.ColumnHeaderStyle = hs;

        // Celdas sin borde foco
        var ct = new ControlTemplate(typeof(DataGridCell));
        var bf = new FrameworkElementFactory(typeof(Border));
        bf.SetValue(Border.BorderThicknessProperty, new Thickness(0));
        bf.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        var cpf = new FrameworkElementFactory(typeof(ContentPresenter));
        cpf.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        bf.AppendChild(cpf); ct.VisualTree = bf;
        var cs = new Style(typeof(DataGridCell));
        cs.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cs.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        cs.Setters.Add(new Setter(DataGridCell.TemplateProperty, ct));
        _grid.CellStyle = cs;

        var txs = new Style(typeof(TextBlock));
        txs.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10,0,10,0)));
        txs.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));

        _grid.Columns.Add(new DataGridTextColumn { Header = "C.I.",   Binding = new System.Windows.Data.Binding("Ci"),       Width = 110, ElementStyle = txs });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Nombre", Binding = new System.Windows.Data.Binding("Nombre"),   Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = txs });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Teléfono", Binding = new System.Windows.Data.Binding("Telefono"), Width = 110, ElementStyle = txs });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Ciudad",   Binding = new System.Windows.Data.Binding("Ciudad"),   Width = 120, ElementStyle = txs });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Empresa",  Binding = new System.Windows.Data.Binding("Empresa"),  Width = 140, ElementStyle = txs });
        _grid.MouseDoubleClick += (_, _) => Aceptar();
        gridWrap.Child = _grid;
        Grid.SetRow(gridWrap, 1); root.Children.Add(gridWrap);

        // ── Footer ──────────────────────────────────────────────────────────
        var footer = new Border {
            Background = _BrCard, BorderBrush = _BrBorde,
            BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(12,8,12,8),
            Margin = new Thickness(0,6,0,0)
        };
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // conteo
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // paginacion centro
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // botones

        _lblConteo = new TextBlock {
            FontSize = 11, Foreground = _BrLabel,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_lblConteo, 0);
        footerGrid.Children.Add(_lblConteo);

        // Paginación central: selector de ítems + flechas
        var pagSp = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0,0,0,0)
        };

        // Selector de ítems por página
        var lblPorPag = new TextBlock {
            Text = "Mostrar:", FontSize = 11, Foreground = _BrLabel,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0)
        };
        pagSp.Children.Add(lblPorPag);

        var tamanos = new[] { (lbl:"10",val:10),(lbl:"20",val:20),(lbl:"50",val:50),(lbl:"Todos",val:int.MaxValue) };
        var chipBrushes = new Dictionary<int, Border>();
        foreach (var (lbl, val) in tamanos)
        {
            bool active = val == _porPagina;
            var chip = new Border {
                CornerRadius = new CornerRadius(4),
                BorderBrush = _BrBorde, BorderThickness = new Thickness(1),
                Background = active ? _BrPrim : _BrCard,
                Padding = new Thickness(10,4,10,4), Margin = new Thickness(2,0,2,0),
                Cursor = Cursors.Hand,
                Tag = val
            };
            chip.Child = new TextBlock {
                Text = lbl, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = active ? Brushes.White : _BrLabel,
                VerticalAlignment = VerticalAlignment.Center
            };
            chipBrushes[val] = chip;
            chip.MouseLeftButtonUp += async (_, _) => {
                _porPagina = (int)chip.Tag;
                _paginaBusc = 1;
                foreach (var kv in chipBrushes) {
                    bool on = kv.Key == _porPagina;
                    kv.Value.Background = on ? _BrPrim : _BrCard;
                    ((TextBlock)kv.Value.Child).Foreground = on ? Brushes.White : _BrLabel;
                }
                await CargarPaginaBuscAsync();
            };
            pagSp.Children.Add(chip);
        }

        // Separador
        pagSp.Children.Add(new Border { Width = 1, Background = _BrBorde, Margin = new Thickness(10,3,10,3), VerticalAlignment = VerticalAlignment.Stretch });

        // Botón anterior
        _btnBuscAnt = new Button {
            Width = 32, Height = 28,
            Background = new SolidColorBrush(Color.FromRgb(107,114,128)),
            Foreground = Brushes.White, BorderThickness = new Thickness(0),
            Margin = new Thickness(0,0,4,0), Cursor = Cursors.Hand, FontSize = 14,
            ToolTip = "Página anterior",
            Content = new TextBlock { Text = "←", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
        };
        _lblPagBusc = new TextBlock {
            FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            Foreground = _BrLabel, Margin = new Thickness(4,0,4,0),
            MinWidth = 90, TextAlignment = TextAlignment.Center
        };
        // Botón siguiente
        _btnBuscSig = new Button {
            Width = 32, Height = 28,
            Background = new SolidColorBrush(Color.FromRgb(107,114,128)),
            Foreground = Brushes.White, BorderThickness = new Thickness(0),
            Margin = new Thickness(4,0,0,0), Cursor = Cursors.Hand, FontSize = 14,
            ToolTip = "Página siguiente",
            Content = new TextBlock { Text = "→", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
        };
        _btnBuscAnt.Click += async (_, _) => { _paginaBusc--; await CargarPaginaBuscAsync(); };
        _btnBuscSig.Click += async (_, _) => { _paginaBusc++; await CargarPaginaBuscAsync(); };
        pagSp.Children.Add(_btnBuscAnt);
        pagSp.Children.Add(_lblPagBusc);
        pagSp.Children.Add(_btnBuscSig);
        Grid.SetColumn(pagSp, 1);
        footerGrid.Children.Add(pagSp);

        var btnsSp = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        Button MkB(string txt, string hex) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(16,0,16,0),
            Margin = new Thickness(6,0,0,0),
            Background = (SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            FontSize = 12, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };

        var btnNuevo   = MkB("＋ Nuevo",        "#1565C0");
        var btnAceptar = MkB("✔  Seleccionar",   "#22C55E");
        var btnCerrar  = MkB("✕  Cerrar",        "#6B7280");

        btnNuevo.Click   += async (_, _) => { _grid.SelectedItem = null; await AbrirFormNuevo(); _grid.SelectedItem = null; };
        btnAceptar.Click += (_, _) => Aceptar();
        // DialogResult ya no se usa (ver Aceptar()) — esta ventana se abre con Show(), no
        // ShowDialog(). PersonaSeleccionada queda null, que es lo que ya chequean los llamadores.
        btnCerrar.Click  += (_, _) => Close();

        btnsSp.Children.Add(btnNuevo);
        btnsSp.Children.Add(btnAceptar);
        btnsSp.Children.Add(btnCerrar);
        Grid.SetColumn(btnsSp, 2);
        footerGrid.Children.Add(btnsSp);

        footer.Child = footerGrid;
        Grid.SetRow(footer, 2); root.Children.Add(footer);

        Content = root;
        Loaded += (_, _) => _txtBuscar.Focus();
    }

    private static byte ToB(object? v)
    {
        if (v == null || v is DBNull) return 0;
        try { return Convert.ToByte(v); } catch { return 0; }
    }

    private async Task CargarAsync()
    {
        _paginaBusc = 1;
        await CargarPaginaBuscAsync();
    }

    private void Filtrar()
    {
        _paginaBusc = 1;
        _busc_cts?.Cancel();
        _busc_cts = new System.Threading.CancellationTokenSource();
        var token = _busc_cts.Token;
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested)
                    await CargarPaginaBuscAsync();
            }
            catch (TaskCanceledException) { }
        });
    }

    private async Task CargarPaginaBuscAsync()
    {
        if (_cargandoBusc) return;
        _cargandoBusc = true;
        try
        {
            var q = (_txtBuscar?.Text ?? "").Trim();
            bool todosModo = _porPagina == int.MaxValue;
            int porPag = todosModo ? 5000 : _porPagina;

            using var conn = _db.Create();

            var whereClause = string.IsNullOrEmpty(q)
                ? ""
                : "AND (cl.NOMBRE_CLIENTE LIKE @Q OR cl.CI_CLIENTE LIKE @Q " +
                  "OR cl.TELEFONO_CLIENTE LIKE @Q OR cl.CIUDAD_CLIENTE LIKE @Q " +
                  "OR cl.EMPRESA_LABORAL LIKE @Q) ";

            // COUNT sin subquery — solo filtra CLIENTES
            var sqlCount = $"SELECT COUNT(*) FROM CLIENTES cl WHERE 1=1 {whereClause}";

            // SaldoActivo via LEFT JOIN derivado (una sola ejecución para todos los clientes de la página)
            // en lugar de subquery correlacionado que se ejecuta 1 vez por fila
            int _offsetBusc = (_paginaBusc - 1) * porPag;
            var sqlData =
                "SELECT Fila, Id, Ci, Ruc, Nombre, Telefono, Dir, Ciudad, Empresa, TelLab," +
                " EcvNum, SexoNum, Inforcom, TipoNum, CondNum, EstNum, CredMax, Antiguedad," +
                " Conyuge, VencCI, SaldoActivo" +
                " FROM (" +
                "SELECT ROW_NUMBER() OVER (ORDER BY cl.NOMBRE_CLIENTE) as Fila," +
                " cl.ID_CLIENTE as Id, cl.CI_CLIENTE as Ci, cl.RUC_CLIENTE as Ruc," +
                " cl.NOMBRE_CLIENTE as Nombre, cl.TELEFONO_CLIENTE as Telefono," +
                " cl.DIRECCION_CLIENTE as Dir, cl.CIUDAD_CLIENTE as Ciudad," +
                " cl.EMPRESA_LABORAL as Empresa, cl.TELEFONO_LABORAL as TelLab," +
                " cl.ECV as EcvNum, cl.SEXO as SexoNum, cl.INFORCOM as Inforcom," +
                " cl.TIPO as TipoNum, cl.CONDICION as CondNum, cl.ESTADO as EstNum," +
                " ISNULL(cl.CRED_MAX,0) as CredMax, cl.ANTIGUEDAD as Antiguedad," +
                " ISNULL(cl.CONYUGE,'') as Conyuge, cl.VENC_CEDULA as VencCI," +
                " ISNULL(cs_agg.SaldoActivo, 0) as SaldoActivo" +
                " FROM CLIENTES cl" +
                " LEFT JOIN (" +
                "   SELECT ID_CLIENTE, SUM(DEBE - HABER) AS SaldoActivo" +
                "   FROM CABECERA_SALES WHERE ESTADO = 1 AND FORMA_DE_VENTA = 2" +
                "   GROUP BY ID_CLIENTE" +
                " ) cs_agg ON cs_agg.ID_CLIENTE = cl.ID_CLIENTE" +
                $" WHERE 1=1 {whereClause}" +
                $") __p WHERE Fila BETWEEN {_offsetBusc + 1} AND {_offsetBusc + porPag}";

            // Un solo round-trip: COUNT + datos en el mismo batch
            var prm = new { Q = $"%{q}%" };
            using var multi = await conn.QueryMultipleAsync(
                sqlCount + "; " + sqlData, prm, commandTimeout: 30);
            _totalPersonas = await multi.ReadFirstAsync<int>();
            var rows = await multi.ReadAsync<ClienteRow>();

            var lista = rows.Select(r => new PersonaItem {
                Fila        = (int)r.Fila,
                Id          = r.Id,
                Ci          = r.Ci         ?? "",
                Ruc         = r.Ruc        ?? "",
                Nombre      = r.Nombre     ?? "",
                Telefono    = r.Telefono   ?? "",
                Direccion   = r.Dir        ?? "",
                Ciudad      = r.Ciudad     ?? "",
                Empresa     = r.Empresa    ?? "",
                TelLaboral  = r.TelLab     ?? "",
                Antiguedad  = r.Antiguedad ?? "",
                Conyuge     = r.Conyuge    ?? "",
                VencCI      = r.VencCI.HasValue ? r.VencCI.Value.ToString("dd/MM/yyyy") : "",
                CredMax     = r.CredMax,
                SaldoActivo = r.SaldoActivo,
                Ecv = r.EcvNum switch {
                    0=>"Soltero",1=>"Casado",2=>"Divorciado",3=>"Separado",4=>"Viudo",_=>""
                },
                Sexo = r.SexoNum switch { 0=>"Femenino",1=>"Masculino",_=>"" },
                Inforconf = r.Inforcom switch { 0=>"Libre",1=>"Registrado",_=>"" },
                Tipo = r.TipoNum switch {
                    0=>"Malo",1=>"Regular",2=>"Bueno",3=>"Muy Bueno",4=>"Excelente",_=>""
                },
                Condicion = r.CondNum switch {
                    0=>"Activo",1=>"Bloqueado",2=>"Moroso",3=>"Inactivo",_=>""
                },
                EstadoTexto = r.EstNum switch { 0=>"Normal",1=>"Moroso",_=>"" },
            }).ToList();

            _grid.ItemsSource = lista;
            MostrarPaginaBusc();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al cargar lista:\n" + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _cargandoBusc = false; }
    }

    private void MostrarPaginaBusc()
    {
        bool todosModo = _porPagina == int.MaxValue;
        int porPag    = todosModo ? 5000 : _porPagina;
        int totalPags = Math.Max(1, (int)Math.Ceiling(_totalPersonas / (double)porPag));
        _paginaBusc   = Math.Clamp(_paginaBusc, 1, totalPags);

        if (_lblConteo != null)
            _lblConteo.Text = string.IsNullOrEmpty(_txtBuscar?.Text?.Trim())
                ? $"{_totalPersonas:N0} personas"
                : $"{_totalPersonas:N0} personas";

        if (_lblPagBusc != null)
            _lblPagBusc.Text = todosModo ? $"Pág. 1 de 1  (todos)" : $"Pág. {_paginaBusc} de {totalPags}";
        if (_btnBuscAnt != null) _btnBuscAnt.IsEnabled = !todosModo && _paginaBusc > 1;
        if (_btnBuscSig != null) _btnBuscSig.IsEnabled = !todosModo && _paginaBusc < totalPags;
    }

    private void Aceptar()
    {
        if (_grid.SelectedItem is PersonaItem p)
        {
            // DialogResult ya no se usa acá — esta ventana pasó a abrirse con Show() en vez de
            // ShowDialog() (ver AbrirDetalle en VentasWindows.cs / OnBuscarCliente en
            // VentaCreditoWindow.xaml.cs), y DialogResult solo puede asignarse en ventanas
            // mostradas como diálogo modal — asignarlo acá lanzaba InvalidOperationException al
            // presionar "Seleccionar". El llamador ya lee PersonaSeleccionada desde el evento
            // Closed, así que alcanza con cerrar la ventana.
            PersonaSeleccionada = p;
            Close();
        }
        else
        {
            MessageBox.Show("Seleccione una persona de la lista.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // "+ Nuevo" abre el módulo COMPLETO de Clientes (todos los campos reales: dirección,
    // ciudad, garante, crédito máximo, etc.) en vez del mini-formulario anterior (solo CI,
    // nombre, celular y trabajo) — ese formulario acotado creaba clientes con datos
    // incompletos que después había que ir a completar aparte en Clientes. Al cerrar
    // ClientesWindow, si se dio de alta un cliente nuevo (CiUltimoClienteGuardado), se
    // refresca la lista y se autoselecciona ese CI en el buscador para no obligar al cajero
    // a buscarlo de nuevo a mano.
    private async Task AbrirFormNuevo()
    {
        var win = new CrediSoft.UI.Views.Maestros.ClientesWindow { Owner = this };
        win.ShowDialog();

        var ciNuevo = win.CiUltimoClienteGuardado;
        if (string.IsNullOrWhiteSpace(ciNuevo)) return;

        _txtBuscar.Text = ciNuevo;
        await CargarPaginaBuscAsync();

        var lista = _grid.ItemsSource as IEnumerable<PersonaItem>;
        var match = lista?.FirstOrDefault(p => p.Ci == ciNuevo);
        if (match != null) _grid.SelectedItem = match;
    }
}
