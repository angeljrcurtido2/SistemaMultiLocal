using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Services;
using CrediSoft.UI.Views.Maestros;
using CrediSoft.UI.Views.Ventas;
using CrediSoft.UI.Views.Cobros;
using CrediSoft.UI.Views.Caja;
using CrediSoft.UI.Views.Informes;
using CrediSoft.UI.Views.Transferencias;
using CrediSoft.UI.Views.Herramientas;
using CrediSoft.UI.Views.Compras;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Main;

public partial class MainWindow : Window
{
    private readonly ISessionService _session;

    public MainWindow()
    {
        InitializeComponent();
        _session = SessionService.Instance;
        ActualizarStatusBar();
        Loaded += (_, _) => ActualizarStatusBar();
    }

    private void ActualizarStatusBar()
    {
        if (_session.UsuarioActual != null)
        {
            TxtStatusUsuario.Text = $"👤 {_session.UsuarioActual.NombreUsuario} ({_session.UsuarioActual.CargoUsuario})";
            TxtStatusLocal.Text = $"🏪 {_session.LocalActual?.NombreLocal}";
        }
        TxtStatusFecha.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
    }

    private void SetModulo(string nombre) =>
        TxtStatusModulo.Text = nombre;

    // ── Apertura de módulos ──────────────────────────────────────────────

    private void AbrirVentana(Window win, string nombre)
    {
        SetModulo(nombre);
        win.Owner = this;
        win.ShowDialog();
    }

    // MAESTROS
    private void OnMenuArticulos(object s, RoutedEventArgs e)     => AbrirVentana(new ArticulosWindow(), "Artículos y/o Mercaderías");
    private void OnMenuClientes(object s, RoutedEventArgs e)      => AbrirVentana(new ClientesWindow(), "Altas, bajas y modificaciones de clientes");
    private void OnMenuBancos(object s, RoutedEventArgs e)        => AbrirVentana(new BancosWindow(), "Altas, bajas y modificaciones de Bancos");
    private void OnMenuCategorias(object s, RoutedEventArgs e)    => AbrirVentana(new CategoriasWindow(), "Altas, bajas y modificaciones de categorías");
    private void OnMenuSubcategorias(object s, RoutedEventArgs e)  => AbrirVentana(new SubcategoriasWindow(), "Altas, bajas y modificaciones de subcategorías");
    private void OnMenuMarcas(object s, RoutedEventArgs e)        => AbrirVentana(new MarcasWindow(), "Altas, bajas y modificaciones de marcas");
    private void OnMenuProveedores(object s, RoutedEventArgs e)   => AbrirVentana(new ProveedoresWindow(), "Altas, bajas y modificaciones de proveedores");
    private void OnMenuMedidas(object s, RoutedEventArgs e)       => AbrirVentana(new MedidasWindow(), "Unidades de medida");
    private void OnMenuProcedencias(object s, RoutedEventArgs e)  => AbrirVentana(new ProcedenciasWindow(), "Países / Procedencias");
    private void OnMenuSecciones(object s, RoutedEventArgs e)     => AbrirVentana(new SeccionesWindow(), "Secciones");
    private async void OnMenuFuncionarios(object s, RoutedEventArgs e)
    {
        if (!await ConfirmarAdministrador()) return;
        AbrirVentana(new FuncionariosWindow(), "Altas, bajas y modificaciones de funcionarios");
    }
    // Muestra modal que pide código + contraseña de administrador.
    // Devuelve true solo si las credenciales corresponden a un usuario ADMINISTRADOR.
    private async Task<bool> ConfirmarAdministrador()
    {
        var repo = App.Services.GetRequiredService<IUsuarioRepository>();

        var resultado = false;
        TextBox txtCodigo = null!;
        PasswordBox txtPass = null!;
        TextBlock lblError = null!;
        Button btnOk = null!;

        var dlg = new Window
        {
            Title = "Acceso restringido",
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.White,
        };

        // Encabezado naranja
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xE0, 0x70, 0x00)),
            Padding = new Thickness(16, 12, 16, 12),
            Child = new TextBlock
            {
                Text = "Ingrese contraseña de administrador",
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            }
        };

        // Cuerpo
        static TextBlock Lbl(string t) => new TextBlock
        {
            Text = t, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x40, 0x00)),
            Margin = new Thickness(0, 8, 0, 2),
        };

        txtCodigo = new TextBox { Padding = new Thickness(6, 4, 6, 4) };
        txtPass   = new PasswordBox { Padding = new Thickness(6, 4, 6, 4) };
        lblError  = new TextBlock
        {
            Foreground = Brushes.Red, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed,
        };

        var body = new StackPanel { Margin = new Thickness(16, 4, 16, 8) };
        body.Children.Add(Lbl("Código de usuario"));
        body.Children.Add(txtCodigo);
        body.Children.Add(Lbl("Contraseña"));
        body.Children.Add(txtPass);
        body.Children.Add(lblError);

        // Botones
        btnOk = new Button
        {
            Content = "Confirmar", Width = 100, Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(0xE0, 0x70, 0x00)),
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
        };
        var btnCancelar = new Button
        {
            Content = "Cancelar", Width = 80, Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75)),
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
        };
        btnCancelar.Click += (_, __) => dlg.Close();

        async Task Verificar()
        {
            lblError.Visibility = Visibility.Collapsed;
            btnOk.IsEnabled = false;
            btnOk.Content = "Verificando...";
            try
            {
                var usuario = await repo.BuscarPorCodigoAsync(txtCodigo.Text.Trim());
                if (usuario == null || usuario.ContrasenaUsuario != txtPass.Password)
                {
                    lblError.Text = "Código o contraseña incorrectos.";
                    lblError.Visibility = Visibility.Visible;
                    txtPass.Clear(); txtPass.Focus();
                    return;
                }
                if (!usuario.EsAdministrador)
                {
                    lblError.Text = "El usuario no tiene privilegio de Administrador.";
                    lblError.Visibility = Visibility.Visible;
                    txtPass.Clear(); txtPass.Focus();
                    return;
                }
                resultado = true;
                dlg.Close();
            }
            finally
            {
                btnOk.IsEnabled = true;
                btnOk.Content = "Confirmar";
            }
        }

        btnOk.Click += async (_, __) => await Verificar();
        txtPass.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Verificar(); };
        txtCodigo.KeyDown += (_, e) => { if (e.Key == Key.Enter) txtPass.Focus(); };

        var sep = new Border
        {
            Height = 1, Margin = new Thickness(0, 4, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
        };
        var botonesPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 10, 16, 16),
        };
        botonesPanel.Children.Add(btnOk);
        botonesPanel.Children.Add(btnCancelar);

        var root = new StackPanel();
        root.Children.Add(header);
        root.Children.Add(body);
        root.Children.Add(sep);
        root.Children.Add(botonesPanel);

        dlg.Content = root;
        dlg.Loaded += (_, __) => txtCodigo.Focus();
        dlg.ShowDialog();
        return resultado;
    }

    private void OnMenuLocales(object s, RoutedEventArgs e)
    {
        AbrirVentana(new LocalesWindow(), "Locales / Sucursales");
    }

    // VENTAS
    private void OnMenuVentaCredito(object s, RoutedEventArgs e)      => AbrirVentana(new VisorSolicitudesWindow(), "Ventas a crédito — Solicitudes");
    private void OnMenuVentaContado(object s, RoutedEventArgs e)      => AbrirVentana(new VentaContadoWindow(), "Venta al contado");
    private void OnMenuIngresarSolicitud(object s, RoutedEventArgs e) => AbrirVentana(new VentaCreditoWindow(), "Ingresar solicitud de crédito");
    private void OnMenuVisorSolicitudes(object s, RoutedEventArgs e)  => AbrirVentana(new VisorSolicitudesWindow(), "Visor de solicitudes");

    // COBROS
    private void OnMenuCobrarCuota(object s, RoutedEventArgs e)   => AbrirVentana(new CobrosWindow(), "Cobrar cuota");
    private void OnMenuCobrarMasivo(object s, RoutedEventArgs e)  => AbrirVentana(new CobrosWindow(), "Cobro masivo");

    // COMPRAS
    private void OnToolbarCompras(object s, RoutedEventArgs e) {
        if (s is Button btn && btn.ContextMenu != null) {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void OnToolbarVentas(object s, RoutedEventArgs e) {
        if (s is Button btn && btn.ContextMenu != null) {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }
    private void OnMenuNuevaCompra(object s, RoutedEventArgs e)    => AbrirVentana(new NuevaCompraWindow(), "Nueva Compra");
    private void OnMenuCompraRapida(object s, RoutedEventArgs e)   => AbrirVentana(new CompraRapidaWindow(), "Compra Rápida");
    private void OnMenuEditarCompras(object s, RoutedEventArgs e)  => AbrirVentana(new CrediSoft.UI.Views.Compras.EditarComprasWindow(), "Modificar datos de compras");

    // TRANSFERENCIAS
    private void OnMenuNuevaTransferencia(object s, RoutedEventArgs e)   => AbrirVentana(new NuevaTransferenciaWindow(), "Nueva Transferencia entre Locales");
    private void OnMenuAceptarTransferencia(object s, RoutedEventArgs e) => AbrirVentana(new AceptarTransferenciaWindow(), "Aceptar Transferencia");

    // CAJA
    private void OnMenuCajaApertura(object s, RoutedEventArgs e)  => AbrirVentana(new CajaAperturaWindow(), "Apertura de Caja");
    private void OnMenuCajaCierre(object s, RoutedEventArgs e)    => AbrirVentana(new CajaCierreWindow(), "Cierre de Caja");
    private void OnMenuCajaArqueo(object s, RoutedEventArgs e)    => AbrirVentana(new CajaArqueoWindow(), "Arqueo de Caja");
    private void OnMenuCajaGastos(object s, RoutedEventArgs e)    => AbrirVentana(new CajaGastosWindow(), "Ver Gastos de Caja");
    private void OnMenuCajaRegistrar(object s, RoutedEventArgs e) => AbrirVentana(new CajaRegistrarWindow(), "Registrar movimiento de caja");
    private void OnMenuCajaHistorial(object s, RoutedEventArgs e) => AbrirVentana(new CajaHistorialWindow(), "Historial de Caja");

    // INFORMES
    private void OnMenuAtrasos(object s, RoutedEventArgs e)          => AbrirVentana(new AtrasosWindow(), "Consultas sobre atrasos, moras, etc.");
    private void OnMenuHCobranzas(object s, RoutedEventArgs e)       => AbrirVentana(new HCobranzasWindow(), "Consultas e informes sobre cobranzas");
    private void OnMenuHCompras(object s, RoutedEventArgs e)         => AbrirVentana(new HComprasWindow(), "Historial de Compras");
    private void OnMenuHCreditos(object s, RoutedEventArgs e)        => AbrirVentana(new HCreditosWindow(), "Consultas e informes sobre créditos");
    private void OnMenuEnPromocion(object s, RoutedEventArgs e)      => AbrirVentana(new EnPromocionWindow(), "Artículos en Promoción");
    private void OnMenuHNotaCredito(object s, RoutedEventArgs e)     => AbrirVentana(new HNotaCreditoWindow(), "Cobros por nota de crédito");
    private void OnMenuMovArt(object s, RoutedEventArgs e)           => AbrirVentana(new MovArtWindow(), "Movimiento de artículos/productos");
    private void OnMenuPendientes(object s, RoutedEventArgs e)       => AbrirVentana(new PendientesWindow(), "Cobros Pendientes");
    private void OnMenuHTransferencias(object s, RoutedEventArgs e)  => AbrirVentana(new HTransferenciasWindow(), "Historial de Transferencias");
    private void OnMenuHVentas(object s, RoutedEventArgs e)          => AbrirVentana(new HVentasWindow(), "Consultas e informes sobre ventas");
    private void OnMenuVerArticulos(object s, RoutedEventArgs e)     => AbrirVentana(new VerArticulosWindow(), "Artículos y/o mercaderías");
    private void OnMenuVisorPromo(object s, RoutedEventArgs e)       => AbrirVentana(new VisorPromoWindow(), "Visor de Promociones");

    // HERRAMIENTAS
    private void OnMenuBloquearTransf(object s, RoutedEventArgs e)    => AbrirVentana(new BloquearTransfWindow(), "Bloquear transferencias");
    private void OnMenuEditarCuota(object s, RoutedEventArgs e)       => AbrirVentana(new EditarCuotaWindow(), "Editar cuota pagada");
    private void OnMenuEliminarVentaCont(object s, RoutedEventArgs e) => AbrirVentana(new EliminarVentaContadoWindow(), "Eliminar Venta al Contado");
    private void OnMenuFinalizarPromo(object s, RoutedEventArgs e)    => AbrirVentana(new FinalizarPromoWindow(), "Finalizar Promoción");
    private void OnMenuImpresoras(object s, RoutedEventArgs e)        => MostrarProximamente("Impresoras");
    private void OnMenuNotaCredito(object s, RoutedEventArgs e)       => AbrirVentana(new NotaCreditoWindow(), "Nota de Crédito");
    private void OnMenuPromocion(object s, RoutedEventArgs e)         => AbrirVentana(new PromocionWindow(), "Promoción");
    private void OnMenuPunitorio(object s, RoutedEventArgs e)         => AbrirVentana(new PunitorioWindow(), "Configuración de Punitorio");
    private void OnMenuGenerarPagos(object s, RoutedEventArgs e)      => AbrirVentana(new GenerarPagosWindow(), "Generar Pagos");
    private void OnMenuEditarPagos(object s, RoutedEventArgs e)       => AbrirVentana(new EditarPagosWindow(), "Editar Pagos");
    private void OnMenuEliminarPago(object s, RoutedEventArgs e)      => AbrirVentana(new EliminarPagoWindow(), "Eliminar Pago generado");
    private void OnMenuPagoRemuneraciones(object s, RoutedEventArgs e)=> AbrirVentana(new PagoRemuneracionesWindow(), "Pago de remuneraciones");
    private void OnMenuRetiroLibre(object s, RoutedEventArgs e)       => AbrirVentana(new RetiroLibreWindow(), "Retiro libre");

    private void OnMenuAcercaDe(object s, RoutedEventArgs e)
    {
        MessageBox.Show("CrediSoft v2.0\nCredimar S.A. Electrodomésticos\n\nSistema de Gestión Comercial",
                        "Acerca de...", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnMenuSalir(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("¿Desea salir del sistema?", "Salir",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            Application.Current.Shutdown();
    }

    private static void MostrarProximamente(string modulo) =>
        MessageBox.Show($"Módulo '{modulo}' en desarrollo.", "Próximamente",
            MessageBoxButton.OK, MessageBoxImage.Information);
}
