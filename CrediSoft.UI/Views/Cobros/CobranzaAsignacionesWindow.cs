using CrediSoft.Core.Models;
using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Cobros;

public class CobranzaAsignacionesWindow : Window
{
    private readonly IClienteRepository _clientes;
    private readonly ICuotaRepository _cuotas;
    private readonly IUsuarioRepository _usuarios;
    private readonly ISessionService _session;

    private TextBox _txtCi = null!;
    private ComboBox _cboCobrador = null!;
    private TextBlock _txtCliente = null!;
    private Border _txtClienteBadge = null!;
    private TextBlock _txtClienteEstado = null!;
    private TextBlock _txtResumen = null!;
    private TextBlock _txtResumenDetalle = null!;
    private DataGrid _gridCuotas = null!;
    private DataGrid _gridAsignaciones = null!;
    private DataGrid _gridPendientes = null!;
    private Border _panelPendientes = null!;
    private Button _btnAsignarCredito = null!;
    private Button _btnInsertarAsignaciones = null!;
    private Button _btnConfirmarAsignaciones = null!;
    private Button _btnQuitarPendiente = null!;
    private Button _btnQuitarAsignacion = null!;
    private TextBlock _txtResumenPendientes = null!;
    private ComboBox _cboCobradorAccion = null!;
    private Button _btnCobrador = null!;
    private Button _btnCobradorAccion = null!;
    private bool _sincronizandoCobrador;
    private TextBlock _txtAyudaAcciones = null!;
    private Border _bordeAyudaAcciones = null!;

    // Borrador en memoria: nada de esto está guardado en la base hasta "Confirmar
    // asignaciones de cuotas". "Insertar" solo arma esta lista.
    private sealed record PendienteFila(
        int IdGeneradas, string NCuotaTexto, string ClienteNombre, decimal Monto,
        int IdCobrador, string CobradorNombre, string? CobradorAnterior);
    private readonly ObservableCollection<PendienteFila> _pendientes = new();
    private StackPanel _panelSelectorCredito = null!;
    private ComboBox _cboCreditoFiltro = null!;
    private int? _idCabFiltro;
    private WrapPanel _panelFiltroEstado = null!;
    // Por defecto se filtra a "Vencida": el caso de uso real de esta pantalla es asignar
    // cobrador a lo que está atrasado, no revisar el historial completo del cliente.
    private string? _estadoFiltro = "Vencida";

    // Mismo patrón que CobrosWindow.PoblarSelectorCredito: un cliente puede tener varios
    // créditos, y este selector evita ver todas sus cuotas mezcladas en una sola tabla.
    private record CreditoItem(int IdCab, string Texto);

    private readonly ObservableCollection<CuotaFila> _cuotasCliente = new();
    private readonly ObservableCollection<AsignacionCobranza> _asignaciones = new();
    private ICollectionView _vistaCuotas = null!;
    private List<Usuario> _usuariosCobradores = new();
    private Cliente? _clienteActual;
    private ClienteCreditoResumen? _resumenClienteActual;
    private decimal _valorInforconf;

    public CobranzaAsignacionesWindow()
    {
        _clientes = App.Services.GetRequiredService<IClienteRepository>();
        _cuotas   = App.Services.GetRequiredService<ICuotaRepository>();
        _usuarios = App.Services.GetRequiredService<IUsuarioRepository>();
        _session  = SessionService.Instance;

        // Defensa en profundidad: MainWindow ya oculta el ítem de menú para quien no tiene
        // permiso (Usuario.PuedeAsignarCobradores), pero esta ventana también puede abrirse
        // desde otros lugares — el bloqueo real tiene que estar acá, no solo en el menú.
        if (_session.UsuarioActual?.PuedeAsignarCobradores != true)
        {
            Loaded += (_, _) =>
            {
                MessageBox.Show("No tenés permiso para asignar cobradores.", "Acceso restringido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
            };
        }

        Title = "Asignaciones de cobranza";
        Width = 1180;
        Height = 720;
        MinWidth = 980;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 11.5;

        BuildUi();
        Loaded += async (_, _) => await CargarInicialAsync();
    }

    private void BuildUi()
    {
        var root = new DockPanel();

        var hdr = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(14, 47, 68)),
            Padding = new Thickness(14, 8, 14, 8)
        };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock
        {
            Text = "ASIGNACIONES DE COBRANZA",
            Foreground = Brushes.White,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold
        });
        hdrSp.Children.Add(new TextBlock
        {
            Text = "1) Buscá un cliente  →  2) elegí una cuota o crédito y un cobrador  →  3) asigná.",
            Foreground = new SolidColorBrush(Color.FromRgb(127, 179, 211)),
            FontSize = 10,
            Margin = new Thickness(0, 3, 0, 0)
        });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top);
        root.Children.Add(hdr);

        var filtros = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(236, 239, 241)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 8, 12, 8)
        };
        var filtrosGrid = new Grid();
        filtrosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filtrosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        filtrosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        filtrosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        var clienteSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        clienteSp.Children.Add(new TextBlock
        {
            Text = "C.I. cliente:",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(55, 71, 79)),
            Margin = new Thickness(0, 0, 8, 0)
        });
        _txtCi = new TextBox
        {
            Width = 150,
            Padding = new Thickness(6, 4, 6, 4),
            ToolTip = "Escribí la cédula del cliente y presioná Enter."
        };
        _txtCi.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await BuscarClienteAsync(); };
        clienteSp.Children.Add(_txtCi);

        var btnModalClientes = new Button
        {
            Content = "Clientes con créditos",
            ToolTip = "Buscar y elegir un cliente de una lista, en vez de escribir la C.I.",
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(Color.FromRgb(13, 110, 95)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnModalClientes.Click += async (_, _) => await AbrirModalClientesAsync();
        clienteSp.Children.Add(btnModalClientes);

        var btnBuscar = new Button
        {
            Content = "Buscar",
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnBuscar.Click += async (_, _) => await BuscarClienteAsync();
        clienteSp.Children.Add(btnBuscar);

        var btnLimpiar = new Button
        {
            Content = "Limpiar",
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnLimpiar.Click += async (_, _) =>
        {
            _txtCi.Text = "";
            _clienteActual = null;
            _resumenClienteActual = null;
            ActualizarClienteSeleccionado(null, null);
            await RefrescarCuotasClienteAsync();
            await RefrescarAsignacionesAsync();
        };
        clienteSp.Children.Add(btnLimpiar);

        Grid.SetColumn(clienteSp, 0);
        filtrosGrid.Children.Add(clienteSp);

        var cobradorStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 0, 8, 0) };
        cobradorStack.Children.Add(new TextBlock
        {
            Text = "Cobrador (para asignar y filtrar)",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
            Margin = new Thickness(0, 0, 0, 2)
        });
        _cboCobrador = new ComboBox
        {
            DisplayMemberPath = "NombreUsuario",
            SelectedValuePath = "IdUsuario",
            Visibility = Visibility.Collapsed // fuente de datos interna; la UI real es _btnCobrador
        };
        _cboCobrador.SelectionChanged += async (_, _) =>
        {
            _btnCobrador.Content = _cboCobrador.SelectedItem is Usuario u ? u.NombreUsuario : "Elegir cobrador...";
            if (!_sincronizandoCobrador)
            {
                _sincronizandoCobrador = true;
                _cboCobradorAccion.SelectedItem = _cboCobrador.SelectedItem;
                _sincronizandoCobrador = false;
            }
            ActualizarEstadoBotones();
            await RefrescarAsignacionesAsync();
        };
        cobradorStack.Children.Add(_cboCobrador);

        _btnCobrador = new Button
        {
            Content = "Elegir cobrador...",
            Padding = new Thickness(8, 4, 8, 4),
            Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(33, 37, 41)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220)),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            ToolTip = "Buscar y elegir el cobrador al que le vas a asignar la cuota o crédito. También filtra la lista de asignaciones de la derecha."
        };
        _btnCobrador.Click += (_, _) => AbrirBuscadorCobrador(_cboCobrador);
        cobradorStack.Children.Add(_btnCobrador);
        Grid.SetColumn(cobradorStack, 1);
        filtrosGrid.Children.Add(cobradorStack);

        var btnTodos = new Button
        {
            Content = "Ver todos",
            ToolTip = "Quitar el filtro por cobrador y ver las asignaciones de todos.",
            Padding = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(Color.FromRgb(27, 94, 32)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 14, 8, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        btnTodos.Click += async (_, _) =>
        {
            // "Ver todos" tiene que mostrar TODAS las asignaciones del sistema en la lista de
            // la derecha. RefrescarAsignacionesAsync también filtra por _clienteActual (el
            // cliente buscado a la izquierda) cuando hay uno cargado — sin ignorar ese filtro
            // acá, el botón parecía no hacer nada mientras hubiera un cliente seleccionado.
            // No tocamos _clienteActual: el cliente sigue cargado a la izquierda, solo se
            // amplía qué se ve en "Asignaciones activas".
            _cboCobrador.SelectedIndex = -1;
            await RefrescarAsignacionesAsync(ignorarFiltroCliente: true);
        };
        Grid.SetColumn(btnTodos, 2);
        filtrosGrid.Children.Add(btnTodos);

        var btnCerrar = new Button
        {
            Content = "Cerrar",
            Padding = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(Color.FromRgb(84, 110, 122)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 14, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        btnCerrar.Click += (_, _) => Close();
        Grid.SetColumn(btnCerrar, 3);
        filtrosGrid.Children.Add(btnCerrar);

        filtros.Child = filtrosGrid;
        DockPanel.SetDock(filtros, Dock.Top);
        root.Children.Add(filtros);

        var content = new Grid { Margin = new Thickness(10) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7, GridUnitType.Star), MinWidth = 560 });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star), MinWidth = 280 });

        var clienteCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var clienteStack = new StackPanel();
        var clienteHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        clienteHeader.Children.Add(new TextBlock
        {
            Text = "Cliente seleccionado",
            Foreground = new SolidColorBrush(Color.FromRgb(55, 71, 79)),
            FontWeight = FontWeights.Bold,
            FontSize = 11.5
        });

        _txtClienteBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(236, 239, 241)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(9, 2, 9, 2),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _txtClienteEstado = new TextBlock
        {
            Text = "Sin cliente",
            Foreground = new SolidColorBrush(Color.FromRgb(84, 110, 122)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 10
        };
        _txtClienteBadge.Child = _txtClienteEstado;
        DockPanel.SetDock(_txtClienteBadge, Dock.Right);
        clienteHeader.Children.Add(_txtClienteBadge);
        clienteStack.Children.Add(clienteHeader);

        _txtCliente = new TextBlock
        {
            Text = "Escribí una C.I. y presioná Enter, o abrí el selector para elegir un cliente con créditos.",
            Foreground = new SolidColorBrush(Color.FromRgb(55, 71, 79)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 0)
        };
        clienteStack.Children.Add(_txtCliente);
        clienteCard.Child = clienteStack;

        _gridCuotas = CrearGridCuotas();
        var panelCuotas = new Grid();
        panelCuotas.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panelCuotas.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panelCuotas.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panelCuotas.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panelCuotas.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panelCuotas.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panelCuotas.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Grid.SetRow(clienteCard, 0);
        panelCuotas.Children.Add(clienteCard);

        var barraAcciones = new WrapPanel { Margin = new Thickness(0, 8, 0, 8), VerticalAlignment = VerticalAlignment.Center };
        _btnAsignarCredito = MkActionButton("Asignar todo el crédito", OnAsignarCredito, "#1565C0");
        _btnAsignarCredito.ToolTip = "Asigna al cobrador TODAS las cuotas del crédito que está mostrando la tabla ahora mismo (salvo las que ya tengan una asignación puntual). No requiere marcar cuotas.";
        _btnAsignarCredito.IsEnabled = false;
        barraAcciones.Children.Add(_btnAsignarCredito);

        _btnInsertarAsignaciones = MkActionButton("Insertar cuotas a cobrador", OnInsertarAsignaciones, "#2E7D32");
        _btnInsertarAsignaciones.ToolTip = "Agrega las cuotas tildadas + el cobrador elegido a la lista de \"Pendientes de confirmar\" de abajo. Todavía NO guarda nada en la base.";
        _btnInsertarAsignaciones.IsEnabled = false;
        barraAcciones.Children.Add(_btnInsertarAsignaciones);

        var cobradorAccionStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
        cobradorAccionStack.Children.Add(new TextBlock
        {
            Text = "COBRADOR A ASIGNAR:",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
            Margin = new Thickness(0, 0, 0, 2)
        });
        _cboCobradorAccion = new ComboBox
        {
            DisplayMemberPath = "NombreUsuario",
            SelectedValuePath = "IdUsuario",
            Visibility = Visibility.Collapsed // fuente de datos interna; la UI real es _btnCobradorAccion
        };
        _cboCobradorAccion.SelectionChanged += (_, _) =>
        {
            _btnCobradorAccion.Content = _cboCobradorAccion.SelectedItem is Usuario u ? u.NombreUsuario : "Elegir cobrador...";
            if (_sincronizandoCobrador) return;
            _sincronizandoCobrador = true;
            _cboCobrador.SelectedItem = _cboCobradorAccion.SelectedItem;
            _sincronizandoCobrador = false;
        };
        cobradorAccionStack.Children.Add(_cboCobradorAccion);

        _btnCobradorAccion = new Button
        {
            Content = "Elegir cobrador...",
            Width = 190,
            Padding = new Thickness(8, 4, 8, 4),
            Background = Brushes.White,
            Foreground = new SolidColorBrush(Color.FromRgb(33, 37, 41)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220)),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            ToolTip = "Buscar y elegir el cobrador al que le vas a asignar las cuotas tildadas. Es el mismo selector que el de arriba."
        };
        _btnCobradorAccion.Click += (_, _) => AbrirBuscadorCobrador(_cboCobradorAccion);
        cobradorAccionStack.Children.Add(_btnCobradorAccion);
        barraAcciones.Children.Add(cobradorAccionStack);
        Grid.SetRow(barraAcciones, 1);
        panelCuotas.Children.Add(barraAcciones);

        _bordeAyudaAcciones = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 6, 9, 6),
            Margin = new Thickness(0, 0, 0, 8)
        };
        _txtAyudaAcciones = new TextBlock
        {
            Text = "Tildá una o más cuotas con el check de la izquierda de la tabla y elegí un cobrador arriba para habilitar \"Insertar cuotas a cobrador\".",
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        _bordeAyudaAcciones.Child = _txtAyudaAcciones;
        Grid.SetRow(_bordeAyudaAcciones, 2);
        panelCuotas.Children.Add(_bordeAyudaAcciones);
        ActualizarColorAyuda(new SolidColorBrush(Color.FromRgb(96, 125, 139)), new SolidColorBrush(Color.FromRgb(240, 244, 247)));

        _panelSelectorCredito = new StackPanel { Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
        _panelSelectorCredito.Children.Add(new TextBlock
        {
            Text = "CRÉDITO:",
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(84, 110, 122)),
            Margin = new Thickness(0, 0, 0, 3)
        });
        _cboCreditoFiltro = new ComboBox
        {
            Padding = new Thickness(6, 3, 6, 3),
            DisplayMemberPath = "Texto",
            ToolTip = "Este cliente tiene más de un crédito. Elegí cuál mostrar para no mezclar cuotas de créditos distintos."
        };
        _cboCreditoFiltro.SelectionChanged += (_, _) =>
        {
            if (_cboCreditoFiltro.SelectedItem is CreditoItem sel)
                _idCabFiltro = sel.IdCab;
            AplicarFiltros();
            ActualizarEstadoBotones();
        };
        _panelSelectorCredito.Children.Add(_cboCreditoFiltro);
        Grid.SetRow(_panelSelectorCredito, 3);
        panelCuotas.Children.Add(_panelSelectorCredito);

        var filtroEstadoStack = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        filtroEstadoStack.Children.Add(new TextBlock
        {
            Text = "ESTADO DE LAS CUOTAS:",
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(84, 110, 122)),
            Margin = new Thickness(0, 0, 0, 3)
        });
        _panelFiltroEstado = new WrapPanel();
        filtroEstadoStack.Children.Add(_panelFiltroEstado);
        Grid.SetRow(filtroEstadoStack, 4);
        panelCuotas.Children.Add(filtroEstadoStack);
        ConstruirChipsEstado();

        var cuotasTitle = new TextBlock
        {
            Text = "Cuotas del cliente",
            Foreground = new SolidColorBrush(Color.FromRgb(55, 71, 79)),
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            Margin = new Thickness(0, 2, 0, 5)
        };
        Grid.SetRow(cuotasTitle, 5);
        panelCuotas.Children.Add(cuotasTitle);

        _gridCuotas.Margin = new Thickness(0, 0, 0, 0);
        Grid.SetRow(_gridCuotas, 6);
        panelCuotas.Children.Add(_gridCuotas);
        Grid.SetColumn(panelCuotas, 0);
        content.Children.Add(panelCuotas);

        var separador = new Border
        {
            Width = 1,
            Background = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(separador, 1);
        content.Children.Add(separador);

        var ladoDerecho = new Grid();
        ladoDerecho.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ladoDerecho.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ladoDerecho.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ladoDerecho.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ladoDerecho.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _txtResumen = new TextBlock
        {
            Text = "Asignaciones activas: 0",
            Foreground = new SolidColorBrush(Color.FromRgb(55, 71, 79)),
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            Margin = new Thickness(0, 0, 0, 2)
        };
        Grid.SetRow(_txtResumen, 0);
        ladoDerecho.Children.Add(_txtResumen);

        _txtResumenDetalle = new TextBlock
        {
            Text = "Mostrando todos los cobradores.",
            Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(_txtResumenDetalle, 1);
        ladoDerecho.Children.Add(_txtResumenDetalle);

        var barraDerecha = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        _btnQuitarAsignacion = MkActionButton("Quitar asignación", OnQuitarAsignacion, "#C62828");
        _btnQuitarAsignacion.ToolTip = "Elimina la asignación seleccionada en la tabla de abajo. La cuota o crédito queda sin cobrador asignado.";
        _btnQuitarAsignacion.IsEnabled = false;
        barraDerecha.Children.Add(_btnQuitarAsignacion);
        Grid.SetRow(barraDerecha, 2);
        ladoDerecho.Children.Add(barraDerecha);

        _gridAsignaciones = CrearGridAsignaciones();
        _gridAsignaciones.Margin = new Thickness(0, 0, 0, 0);
        Grid.SetRow(_gridAsignaciones, 3);
        ladoDerecho.Children.Add(_gridAsignaciones);

        var panelPendientes = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 246)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed
        };
        var pendientesStack = new StackPanel();
        var pendientesHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        _txtResumenPendientes = new TextBlock
        {
            Text = "Pendientes de confirmar: 0",
            FontWeight = FontWeights.SemiBold,
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
            VerticalAlignment = VerticalAlignment.Center
        };
        pendientesHeader.Children.Add(_txtResumenPendientes);
        _btnConfirmarAsignaciones = new Button
        {
            Content = "Confirmar asignaciones",
            Padding = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = false,
            ToolTip = "Recién acá se escriben en la base todas las asignaciones de la lista de abajo."
        };
        _btnConfirmarAsignaciones.Click += OnConfirmarAsignaciones;
        DockPanel.SetDock(_btnConfirmarAsignaciones, Dock.Right);
        pendientesHeader.Children.Add(_btnConfirmarAsignaciones);
        pendientesStack.Children.Add(pendientesHeader);

        _gridPendientes = CrearGridPendientes();
        pendientesStack.Children.Add(_gridPendientes);

        _btnQuitarPendiente = new Button
        {
            Content = "Quitar de la lista",
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 6, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false,
            ToolTip = "Saca la fila seleccionada de la lista de pendientes (todavía no está guardada en la base, así que no hace falta confirmar nada para descartarla)."
        };
        _btnQuitarPendiente.Click += (_, _) =>
        {
            if (_gridPendientes.SelectedItem is PendienteFila fila)
                _pendientes.Remove(fila);
            ActualizarResumenPendientes();
        };
        pendientesStack.Children.Add(_btnQuitarPendiente);

        panelPendientes.Child = pendientesStack;
        Grid.SetRow(panelPendientes, 4);
        ladoDerecho.Children.Add(panelPendientes);
        _panelPendientes = panelPendientes;

        Grid.SetColumn(ladoDerecho, 2);
        content.Children.Add(ladoDerecho);

        DockPanel.SetDock(content, Dock.Top);
        root.Children.Add(content);

        var footer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(250, 251, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(238, 241, 243)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 6, 12, 6)
        };
        footer.Child = new TextBlock
        {
            Text = "Tip: si asignás una cuota puntual, esa asignación manda por sobre la asignación general del crédito. " +
                   "* el monto incluye el cargo de Inforconf (mora de 90+ días, se cobra una sola vez por episodio).",
            Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156)),
            FontSize = 9.5,
            TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
    }

    private static Button MkActionButton(string texto, RoutedEventHandler click, string colorHex)
    {
        var btn = new Button
        {
            Content = texto,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 6, 0),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)!),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btn.Click += click;
        return btn;
    }
    private void ActualizarClienteSeleccionado(Cliente? cliente, ClienteCreditoResumen? resumen)
    {
        if (cliente == null)
        {
            _txtCliente.Text = "Escribí una C.I. y presioná Enter, o abrí el selector para elegir un cliente con créditos.";
            _txtCliente.Foreground = new SolidColorBrush(Color.FromRgb(55, 71, 79));
            _txtClienteEstado.Text = "Sin cliente";
            _txtClienteEstado.Foreground = new SolidColorBrush(Color.FromRgb(84, 110, 122));
            _txtClienteBadge.Background = new SolidColorBrush(Color.FromRgb(236, 239, 241));
            _txtClienteBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220));
            return;
        }

        if (resumen == null)
        {
            _txtCliente.Text = $"{cliente.NombreCliente} | CI {cliente.CiCliente} | Sin créditos activos";
            _txtCliente.Foreground = new SolidColorBrush(Color.FromRgb(55, 71, 79));
            _txtClienteEstado.Text = "Sin créditos";
            _txtClienteEstado.Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156));
            _txtClienteBadge.Background = new SolidColorBrush(Color.FromRgb(236, 239, 241));
            _txtClienteBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220));
            return;
        }

        _txtCliente.Text = $"{cliente.NombreCliente} | CI {cliente.CiCliente} | {resumen.CreditosTotales:N0} crédito(s) en historial | {resumen.CuotasAtrasadas:N0} cuota(s) vencida(s) | Gs. {resumen.MontoAtraso:N0} en atraso";
        if (resumen.TieneAtraso)
        {
            _txtCliente.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
            _txtClienteEstado.Text = "Con atraso";
            _txtClienteEstado.Foreground = new SolidColorBrush(Color.FromRgb(127, 51, 0));
            _txtClienteBadge.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224));
            _txtClienteBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 183, 77));
        }
        else
        {
            _txtCliente.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
            _txtClienteEstado.Text = "Al día";
            _txtClienteEstado.Foreground = new SolidColorBrush(Color.FromRgb(27, 94, 32));
            _txtClienteBadge.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
            _txtClienteBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(165, 214, 167));
        }
    }

    private void ActualizarResumenCobrador(bool ignorarFiltroCliente = false)
    {
        var partes = new List<string>();
        partes.Add(_cboCobrador.SelectedItem is Usuario u ? $"cobrador: {u.NombreUsuario}" : "todos los cobradores");
        if (!ignorarFiltroCliente && _clienteActual != null)
            partes.Add($"cliente: {_clienteActual.NombreCliente}");

        _txtResumenDetalle.Text = "Mostrando " + string.Join(" | ", partes) + ".";
    }

    private void ActualizarEstadoBotones()
    {
        var cobradorSeleccionado = _cboCobrador.SelectedItem as Usuario;
        var marcadas = _cuotasCliente.Where(f => f.EstaMarcada).ToList();
        var hayMarcadas = marcadas.Count > 0;
        var puedeGuardar = hayMarcadas && cobradorSeleccionado != null;

        // "Asignar todo el crédito" actúa sobre el crédito que la tabla está mostrando ahora
        // (el filtro de arriba) — solo tiene sentido si hay exactamente un crédito a la vista.
        // (Ese botón sigue escribiendo directo en la base: aplica a TODO el crédito, no pasa
        // por el borrador de "Pendientes de confirmar", que es solo para cuotas puntuales.)
        _btnAsignarCredito.IsEnabled = _idCabFiltro != null && cobradorSeleccionado != null;
        _btnQuitarAsignacion.IsEnabled = _gridAsignaciones.SelectedItem is AsignacionCobranza;
        _btnInsertarAsignaciones.IsEnabled = puedeGuardar;
        _btnInsertarAsignaciones.Content = hayMarcadas ? $"Insertar cuotas a cobrador ({marcadas.Count})" : "Insertar cuotas a cobrador";

        var heredanDelCredito = marcadas.Where(f => f.YaAsignada && !f.EsAsignacionPuntual).ToList();
        // Cuota con asignación PUNTUAL propia a un cobrador distinto del elegido ahora — acá
        // "Insertar" va a REEMPLAZAR esa asignación anterior, no es una cuota nueva.
        var reasignanOtroCobrador = puedeGuardar
            ? marcadas.Where(f => f.YaAsignada && f.EsAsignacionPuntual && f.CobradorAsignado != cobradorSeleccionado!.NombreUsuario).ToList()
            : new List<CuotaFila>();

        if (puedeGuardar && reasignanOtroCobrador.Count > 0)
        {
            var detalle = reasignanOtroCobrador.Count == 1
                ? $"la {reasignanOtroCobrador[0].NCuotaTexto} ya está asignada a {reasignanOtroCobrador[0].CobradorAsignado}"
                : $"{reasignanOtroCobrador.Count} de las {marcadas.Count} cuota(s) tildada(s) ya están asignadas a otro cobrador";
            _txtAyudaAcciones.Text = $"⚠ Atención: {detalle}. Si insertás igual, se la vas a REASIGNAR a {cobradorSeleccionado!.NombreUsuario} (la asignación anterior se reemplaza al confirmar).";
            ActualizarColorAyuda(new SolidColorBrush(Color.FromRgb(140, 20, 20)), new SolidColorBrush(Color.FromRgb(253, 224, 224)));
        }
        else if (puedeGuardar && heredanDelCredito.Count > 0)
        {
            _txtAyudaAcciones.Text = $"⚠ Atención: {heredanDelCredito.Count} de las {marcadas.Count} cuota(s) tildada(s) ya están cubiertas por la asignación de su crédito completo. " +
                                      $"Al insertarlas, esas cuotas puntuales van a tener prioridad y quedan con un cobrador distinto al resto de su crédito.";
            ActualizarColorAyuda(new SolidColorBrush(Color.FromRgb(140, 20, 20)), new SolidColorBrush(Color.FromRgb(253, 224, 224)));
        }
        else if (puedeGuardar)
        {
            _txtAyudaAcciones.Text = $"✓ Listo: {marcadas.Count} cuota(s) → {cobradorSeleccionado!.NombreUsuario}. Hacé clic en \"Insertar cuotas a cobrador\" para agregarlas a la lista de pendientes (todavía no se guarda en la base).";
            ActualizarColorAyuda(new SolidColorBrush(Color.FromRgb(27, 94, 32)), new SolidColorBrush(Color.FromRgb(220, 245, 222)));
        }
        else if (hayMarcadas && cobradorSeleccionado == null)
        {
            _txtAyudaAcciones.Text = $"→ Tildaste {marcadas.Count} cuota(s). Ahora elegí un cobrador arriba para habilitar \"Insertar cuotas a cobrador\".";
            ActualizarColorAyuda(new SolidColorBrush(Color.FromRgb(140, 75, 0)), new SolidColorBrush(Color.FromRgb(255, 231, 189)));
        }
        else if (!hayMarcadas && cobradorSeleccionado != null)
        {
            _txtAyudaAcciones.Text = $"→ Cobrador: {cobradorSeleccionado.NombreUsuario}. Ahora tildá una o más cuotas de la tabla de abajo.";
            ActualizarColorAyuda(new SolidColorBrush(Color.FromRgb(140, 75, 0)), new SolidColorBrush(Color.FromRgb(255, 231, 189)));
        }
        else
        {
            _txtAyudaAcciones.Text = "Tildá una o más cuotas con el check de la izquierda de la tabla y elegí un cobrador arriba para habilitar \"Insertar cuotas a cobrador\".";
            ActualizarColorAyuda(new SolidColorBrush(Color.FromRgb(96, 125, 139)), new SolidColorBrush(Color.FromRgb(240, 244, 247)));
        }
    }

    private void ActualizarColorAyuda(SolidColorBrush colorTexto, SolidColorBrush colorFondo)
    {
        _txtAyudaAcciones.Foreground = colorTexto;
        _bordeAyudaAcciones.Background = colorFondo;
    }

    private DataGrid CrearGridCuotas()
    {
        _vistaCuotas = CollectionViewSource.GetDefaultView(_cuotasCliente);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = false,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            RowHeight = 24,
            ColumnHeaderHeight = 26,
            FontSize = 9.5,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(233, 236, 239)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            ItemsSource = _vistaCuotas
        };

        grid.CellStyle = new Style(typeof(DataGridCell))
        {
            Setters =
            {
                new Setter(Control.PaddingProperty, new Thickness(6, 0, 6, 0)),
                new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            }
        };

        grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader))
        {
            Setters =
            {
                new Setter(Control.PaddingProperty, new Thickness(6, 3, 6, 3)),
                new Setter(Control.FontWeightProperty, FontWeights.SemiBold),
                new Setter(Control.FontSizeProperty, 9.0),
                new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center)
            }
        };

        grid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "Marcar",
            Binding = new System.Windows.Data.Binding(nameof(CuotaFila.EstaMarcada)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = 55,
            IsReadOnly = false,
            ElementStyle = new Style(typeof(CheckBox))
            {
                Setters =
                {
                    new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center),
                    new Setter(FrameworkElement.WidthProperty, 17.0),
                    new Setter(FrameworkElement.HeightProperty, 17.0)
                }
            }
        });
        grid.Columns.Add(new DataGridTextColumn { Header = "Créd.", Binding = new System.Windows.Data.Binding(nameof(CuotaFila.IdCab)), Width = 48, IsReadOnly = true });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cuota", Binding = new System.Windows.Data.Binding(nameof(CuotaFila.NCuotaTexto)), Width = 95, IsReadOnly = true });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Monto",
            Binding = new System.Windows.Data.Binding(nameof(CuotaFila.MontoTexto)),
            Width = 100,
            IsReadOnly = true,
            ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right) } }
        });
        grid.Columns.Add(new DataGridTextColumn { Header = "Venc.", Binding = new System.Windows.Data.Binding(nameof(CuotaFila.VtoTexto)), Width = 75, IsReadOnly = true });
        grid.Columns.Add(new DataGridTextColumn { Header = "Mora", Binding = new System.Windows.Data.Binding(nameof(CuotaFila.DiasAtrasoTexto)), Width = 50, IsReadOnly = true });
        grid.Columns.Add(new DataGridTextColumn { Header = "Estado", Binding = new System.Windows.Data.Binding(nameof(CuotaFila.EstadoTexto)), Width = 75, IsReadOnly = true });
        grid.Columns.Add(new DataGridTextColumn { Header = "Asignado a", Binding = new System.Windows.Data.Binding(nameof(CuotaFila.AsignadoA)), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 150, IsReadOnly = true });
        grid.LoadingRow += (_, e) =>
        {
            if (e.Row.Item is CuotaFila fila)
            {
                if (fila.EsVencida)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 236, 209));
                    e.Row.Foreground = new SolidColorBrush(Color.FromRgb(127, 51, 0));
                }
                else if (fila.YaAsignada && fila.EsAsignacionPuntual)
                {
                    // Cuota con asignación propia que se sale de lo que dice el crédito: se
                    // marca distinto (celeste) para que la excepción salte a la vista.
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(227, 242, 253));
                    e.Row.Foreground = Brushes.Black;
                }
                else if (fila.YaAsignada)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                    e.Row.Foreground = Brushes.Black;
                }
                else
                {
                    e.Row.Background = Brushes.White;
                    e.Row.Foreground = Brushes.Black;
                }
            }
        };
        grid.SelectionChanged += (_, _) => ActualizarEstadoBotones();

        // El checkbox de "Marcar" es chico y no siempre se nota que hay que apretarlo justo
        // ahí — con esto, un clic en cualquier parte de la fila (menos si ya tocó el propio
        // checkbox, que ya se togglea solo) también prende/apaga la marca de esa cuota.
        grid.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject dep && FindAncestorOrSelf<CheckBox>(dep) != null)
                return;

            if (FindAncestorOrSelf<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is CuotaFila fila)
                fila.EstaMarcada = !fila.EstaMarcada;
        };

        return grid;
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node != null)
        {
            if (node is T match) return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    private DataGrid CrearGridPendientes()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            RowHeight = 23,
            ColumnHeaderHeight = 24,
            FontSize = 9.5,
            MaxHeight = 125,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(233, 236, 239)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            Background = Brushes.White,
            ItemsSource = _pendientes
        };

        grid.CellStyle = new Style(typeof(DataGridCell))
        {
            Setters =
            {
                new Setter(Control.PaddingProperty, new Thickness(6, 0, 6, 0)),
                new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            }
        };

        grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader))
        {
            Setters =
            {
                new Setter(Control.PaddingProperty, new Thickness(6, 3, 6, 3)),
                new Setter(Control.FontWeightProperty, FontWeights.SemiBold),
                new Setter(Control.FontSizeProperty, 9.0),
                new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left)
            }
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Cuota", Binding = new System.Windows.Data.Binding(nameof(PendienteFila.NCuotaTexto)), Width = 65 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cliente", Binding = new System.Windows.Data.Binding(nameof(PendienteFila.ClienteNombre)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cobrador", Binding = new System.Windows.Data.Binding(nameof(PendienteFila.CobradorNombre)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.SelectionChanged += (_, _) => _btnQuitarPendiente.IsEnabled = grid.SelectedItem is PendienteFila;

        return grid;
    }

    private DataGrid CrearGridAsignaciones()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            RowHeight = 22,
            ColumnHeaderHeight = 24,
            FontSize = 9,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(233, 236, 239)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            ItemsSource = _asignaciones
        };

        grid.CellStyle = new Style(typeof(DataGridCell))
        {
            Setters =
            {
                new Setter(Control.PaddingProperty, new Thickness(5, 0, 5, 0)),
                new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            }
        };

        grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader))
        {
            Setters =
            {
                new Setter(Control.PaddingProperty, new Thickness(5, 3, 5, 3)),
                new Setter(Control.FontWeightProperty, FontWeights.SemiBold),
                new Setter(Control.FontSizeProperty, 8.5),
                new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center)
            }
        };

        // Panel angosto (columna derecha de la ventana): se prioriza que "Cliente" y
        // "Cobrador" se lean completos. Un mismo cliente puede tener varias cuotas asignadas
        // (filas duplicadas en Cliente/Cobrador), por eso "Ref." (Cuota N / Crédito #N) queda
        // visible en su propia columna — es lo único que distingue una fila de otra. "Nivel"
        // pasa a una sola letra (C/Cr), "Fecha" solo queda como tooltip de la fila.
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Niv.",
            Binding = new System.Windows.Data.Binding(nameof(AsignacionCobranza.Nivel)) { Converter = new PrimeraLetraConverter() },
            Width = 28
        });
        grid.Columns.Add(new DataGridTextColumn { Header = "Ref.", Binding = new System.Windows.Data.Binding(nameof(AsignacionCobranza.Referencia)), Width = 58 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cliente", Binding = new System.Windows.Data.Binding(nameof(AsignacionCobranza.ClienteNombre)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star), MinWidth = 70 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cobrador", Binding = new System.Windows.Data.Binding(nameof(AsignacionCobranza.CobradorNombre)), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 60 });
        grid.LoadingRow += (_, e) =>
        {
            if (e.Row.Item is AsignacionCobranza a)
                e.Row.ToolTip = $"{a.Referencia}\n{a.ClienteNombre}\nCobrador: {a.CobradorNombre}\nAsignado: {a.FechaAsignacion:dd/MM/yyyy HH:mm}";
        };
        grid.SelectionChanged += (_, _) => ActualizarEstadoBotones();

        return grid;
    }
    private async Task CargarInicialAsync()
    {
        _usuariosCobradores = (await _usuarios.ListarTodosAsync())
            .Where(u => u.PuedeCobrar || u.EsAdministrador)
            .OrderBy(u => u.NombreUsuario)
            .ToList();

        _cboCobrador.ItemsSource = _usuariosCobradores;
        _cboCobrador.SelectedIndex = -1;
        _cboCobradorAccion.ItemsSource = _usuariosCobradores;
        _cboCobradorAccion.SelectedIndex = -1;
        _valorInforconf = await _cuotas.ObtenerValorInforconfAsync();

        await RefrescarCuotasClienteAsync();
        await RefrescarAsignacionesAsync();
    }

    private async Task BuscarClienteAsync()
    {
        var ci = _txtCi.Text.Trim();
        if (string.IsNullOrWhiteSpace(ci))
        {
            _clienteActual = null;
            _resumenClienteActual = null;
            ActualizarClienteSeleccionado(null, null);
            return;
        }

        _clienteActual = await _clientes.BuscarPorCiAsync(ci);
        _resumenClienteActual = null;
        if (_clienteActual == null)
        {
            MessageBox.Show("Cliente no encontrado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _resumenClienteActual = (await _clientes.BuscarClientesConCreditosAsync(
            idCliente: _clienteActual.IdCliente))
            .FirstOrDefault();

        ActualizarClienteSeleccionado(_clienteActual, _resumenClienteActual);
        await RefrescarCuotasClienteAsync();
    }

    private async Task AbrirModalClientesAsync()
    {
        var modal = new SeleccionarClienteCreditoModal { Owner = this };
        if (modal.ShowDialog() != true || modal.ClienteSeleccionado == null)
            return;

        var sel = modal.ClienteSeleccionado;
        _clienteActual = new Cliente
        {
            IdCliente = sel.IdCliente,
            CiCliente = sel.CiCliente,
            NombreCliente = sel.NombreCliente
        };
        _resumenClienteActual = sel;
        _txtCi.Text = sel.CiCliente;

        ActualizarClienteSeleccionado(_clienteActual, _resumenClienteActual);
        await RefrescarCuotasClienteAsync();
    }

    private async Task RefrescarCuotasClienteAsync(bool resetearFiltroCredito = true)
    {
        // Antes de reconstruir la lista, guardar qué cuotas estaban tildadas para
        // reaplicar la marca después — sin esto, cualquier refresco (ej. cambiar el
        // cobrador, que también filtra "Asignaciones activas") perdía en silencio los
        // checkboxes que el usuario ya había marcado.
        var marcadasPrevias = _cuotasCliente.Where(f => f.EstaMarcada).Select(f => f.IdGeneradas).ToHashSet();

        _cuotasCliente.Clear();
        if (resetearFiltroCredito)
        {
            _idCabFiltro = null;

            // resetearFiltroCredito=true significa "cambió el cliente activo" (búsqueda,
            // modal, limpiar, carga inicial) — el borrador de pendientes es por cliente, no
            // tiene sentido arrastrarlo a otro cliente distinto.
            if (_pendientes.Count > 0)
            {
                MessageBox.Show(
                    $"Se descartaron {_pendientes.Count} asignación(es) pendientes de confirmar del cliente anterior (no se habían guardado en la base).",
                    "Aviso",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                _pendientes.Clear();
                ActualizarResumenPendientes();
            }
        }

        if (_clienteActual == null)
        {
            PoblarSelectorCredito();
            return;
        }

        var cuotas = (await _cuotas.BuscarTodasPorClienteAsync(_clienteActual.IdCliente)).ToList();
        var asignaciones = (await _cuotas.ListarAsignacionesCobranzaAsync()).ToList();

        // El cargo de Inforconf solo puede recaer sobre la cuota atrasada MÁS ANTIGUA del
        // cliente (mismo criterio que CorrespondeCargoInforconfAsync/CobrosWindow) — se
        // resuelve acá una sola vez en vez de una llamada por cuota.
        var masAntiguaAtrasada = cuotas
            .Where(c => c.EstaVencida)
            .OrderBy(c => c.Vto)
            .FirstOrDefault();
        var idGeneradasConInforconf = masAntiguaAtrasada != null
            && await _cuotas.CorrespondeCargoInforconfAsync(_clienteActual.IdCliente, masAntiguaAtrasada.IdGeneradas)
            ? masAntiguaAtrasada.IdGeneradas
            : (int?)null;

        foreach (var c in cuotas.OrderBy(c => c.IdCab).ThenBy(c => c.NCuota))
        {
            var asignado = ObtenerAsignacion(c.IdCab, c.IdGeneradas, asignaciones);
            var incluyeInforconf = c.IdGeneradas == idGeneradasConInforconf;
            var fila = new CuotaFila(c, asignado, incluyeInforconf ? _valorInforconf : 0m)
                { EstaMarcada = marcadasPrevias.Contains(c.IdGeneradas) };
            fila.PropertyChanged += (_, _) => ActualizarEstadoBotones();
            _cuotasCliente.Add(fila);
        }

        PoblarSelectorCredito(cuotas);
        AplicarFiltros();
        ActualizarEstadoBotones();
    }

    private void PoblarSelectorCredito(List<Cuota>? cuotas = null)
    {
        if (cuotas == null || cuotas.Count == 0)
        {
            _panelSelectorCredito.Visibility = Visibility.Collapsed;
            _cboCreditoFiltro.ItemsSource = null;
            return;
        }

        var creditos = cuotas
            .GroupBy(c => c.IdCab)
            .Select(g => new CreditoItem(
                g.Key,
                $"Crédito {g.Key}  —  {g.First().NSolicitud}  —  Debe Gs. {(g.First().CabTotal - g.First().CabHaber):N0}"))
            .OrderByDescending(c => c.IdCab)
            .ToList();

        _panelSelectorCredito.Visibility = creditos.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        _cboCreditoFiltro.ItemsSource = creditos;

        if (creditos.Count <= 1)
        {
            // Un solo crédito: no hace falta elegir, pero igual queda "seleccionado" para
            // que "Asignar todo el crédito" funcione sin depender de un combo oculto.
            _idCabFiltro = creditos.Count == 1 ? creditos[0].IdCab : null;
            return;
        }

        var target = _idCabFiltro.HasValue
            ? creditos.FirstOrDefault(c => c.IdCab == _idCabFiltro.Value) ?? creditos[0]
            : creditos[0];
        _idCabFiltro = target.IdCab;
        _cboCreditoFiltro.SelectedItem = target;
    }

    private void AplicarFiltros()
    {
        _vistaCuotas.Filter = o =>
        {
            if (o is not CuotaFila f) return false;
            if (_idCabFiltro != null && f.IdCab != _idCabFiltro.Value) return false;
            if (_estadoFiltro != null && f.EstadoTexto != _estadoFiltro) return false;
            return true;
        };
    }

    private void ConstruirChipsEstado()
    {
        _panelFiltroEstado.Children.Clear();
        _panelFiltroEstado.Children.Add(MkChipEstado("Vencidas", "Vencida"));
        _panelFiltroEstado.Children.Add(MkChipEstado("Pendientes", "Pendiente"));
        _panelFiltroEstado.Children.Add(MkChipEstado("Cobradas", "Cobrada"));
        _panelFiltroEstado.Children.Add(MkChipEstado("Todas", null));
    }

    private Button MkChipEstado(string texto, string? valor)
    {
        var activo = _estadoFiltro == valor;
        var chip = new Button
        {
            Content = texto,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 5, 0),
            FontSize = 10.5,
            Background = activo ? new SolidColorBrush(Color.FromRgb(21, 101, 192)) : Brushes.White,
            Foreground = activo ? Brushes.White : new SolidColorBrush(Color.FromRgb(55, 71, 79)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220)),
            BorderThickness = new Thickness(1),
            FontWeight = activo ? FontWeights.SemiBold : FontWeights.Normal,
            Cursor = Cursors.Hand
        };
        chip.Click += (_, _) =>
        {
            _estadoFiltro = valor;
            ConstruirChipsEstado();
            AplicarFiltros();
        };
        return chip;
    }

    // recargarCuotas=true solo cuando las asignaciones cambiaron DE VERDAD en la base
    // (guardar/quitar) — la tabla de cuotas de la izquierda necesita releerse para reflejar
    // la nueva columna "Asignado a". Filtrar por cobrador (combo de arriba) no cambia nada
    // en la base, así que no debe releer cuotas: eso recreaba todas las filas y, aunque ya
    // se preservan los checkboxes marcados, seguía siendo un refresco visible e innecesario
    // en la tabla cada vez que el usuario solo quería elegir con quién va a asignar.
    private async Task RefrescarAsignacionesAsync(bool recargarCuotas = false, bool ignorarFiltroCliente = false)
    {
        _asignaciones.Clear();

        int? idCobrador = null;
        if (_cboCobrador.SelectedItem is Usuario u)
            idCobrador = u.IdUsuario;

        var filas = (await _cuotas.ListarAsignacionesCobranzaAsync(idCobrador)).ToList();
        if (!ignorarFiltroCliente && _clienteActual != null)
            filas = filas.Where(a => a.IdCliente == _clienteActual.IdCliente).ToList();

        foreach (var a in filas)
            _asignaciones.Add(a);

        _gridAsignaciones.ItemsSource = _asignaciones;
        _txtResumen.Text = $"Asignaciones activas: {_asignaciones.Count}";
        ActualizarResumenCobrador(ignorarFiltroCliente);
        ActualizarEstadoBotones();

        if (recargarCuotas && _clienteActual != null)
            await RefrescarCuotasClienteAsync(resetearFiltroCredito: false);
    }

    private static AsignacionCobranza? ObtenerAsignacion(int idCab, int idGeneradas, List<AsignacionCobranza> asignaciones)
    {
        return asignaciones
            .Where(a => (a.IdGeneradas.HasValue && a.IdGeneradas.Value == idGeneradas) ||
                        (!a.IdGeneradas.HasValue && a.IdCab.HasValue && a.IdCab.Value == idCab))
            .OrderByDescending(a => a.IdGeneradas.HasValue)
            .ThenByDescending(a => a.FechaAsignacion)
            .FirstOrDefault();
    }

    private async void OnAsignarCredito(object? sender, RoutedEventArgs e)
    {
        if (_idCabFiltro is not int idCab)
        {
            MessageBox.Show("Elegí un crédito específico arriba (CRÉDITO) para poder asignarlo completo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_cboCobrador.SelectedItem is not Usuario cobrador)
        {
            MessageBox.Show("Seleccioná un cobrador.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var asignacionCreditoActual = (await _cuotas.ListarAsignacionesCobranzaAsync())
            .FirstOrDefault(a => !a.IdGeneradas.HasValue && a.IdCab == idCab);
        if (asignacionCreditoActual != null && asignacionCreditoActual.CobradorNombre != cobrador.NombreUsuario)
        {
            var confirmar = MessageBox.Show(
                $"El crédito #{idCab} ya está asignado a {asignacionCreditoActual.CobradorNombre}.\n\n" +
                $"¿Reasignarlo a {cobrador.NombreUsuario}? La asignación anterior se reemplaza.",
                "Reasignar crédito",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmar != MessageBoxResult.Yes)
                return;
        }

        await _cuotas.AsignarCobranzaCreditoAsync(idCab, cobrador.IdUsuario, _session.UsuarioActual!.IdUsuario);
        await RefrescarAsignacionesAsync(recargarCuotas: true);
        MostrarConfirmacion($"Se asignó el crédito #{idCab} completo a {cobrador.NombreUsuario}.");
    }

    // Solo arma el borrador en memoria (_pendientes). No toca la base de datos — eso
    // ocurre recién en OnConfirmarAsignaciones.
    private void OnInsertarAsignaciones(object? sender, RoutedEventArgs e)
    {
        var marcadas = _cuotasCliente.Where(f => f.EstaMarcada).ToList();
        if (marcadas.Count == 0)
        {
            MessageBox.Show("Tildá al menos una cuota de la tabla.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_cboCobrador.SelectedItem is not Usuario cobrador)
        {
            MessageBox.Show("Seleccioná un cobrador.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Cuotas que ya tienen asignación PUNTUAL a otro cobrador — insertar acá las va a
        // reemplazar (reasignación), no es una asignación nueva. Se avisa explícitamente
        // antes de agregarlas al borrador, con opción de cancelar.
        var yaAsignadasAOtro = marcadas
            .Where(f => f.YaAsignada && f.EsAsignacionPuntual && f.CobradorAsignado != cobrador.NombreUsuario)
            .ToList();
        if (yaAsignadasAOtro.Count > 0)
        {
            var detalle = string.Join("\n", yaAsignadasAOtro.Select(f => $"• {f.NCuotaTexto} → hoy: {f.CobradorAsignado}"));
            var confirmar = MessageBox.Show(
                $"{yaAsignadasAOtro.Count} de las cuotas tildadas ya están asignadas a otro cobrador:\n\n{detalle}\n\n" +
                $"¿Reasignarlas a {cobrador.NombreUsuario}? La asignación anterior se reemplaza al confirmar.",
                "Reasignar cuota",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmar != MessageBoxResult.Yes)
                return;
        }

        foreach (var fila in marcadas)
        {
            // Si la misma cuota ya estaba en el borrador (ej. el usuario cambió de cobrador
            // y la volvió a insertar), la línea vieja se reemplaza en vez de duplicarse.
            var existente = _pendientes.FirstOrDefault(p => p.IdGeneradas == fila.IdGeneradas);
            if (existente != null)
                _pendientes.Remove(existente);

            var cobradorAnterior = fila.YaAsignada && fila.CobradorAsignado != cobrador.NombreUsuario
                ? fila.CobradorAsignado
                : null;
            _pendientes.Add(new PendienteFila(
                fila.IdGeneradas, fila.NCuotaTexto, fila.ClienteNombre, fila.MontoTotal,
                cobrador.IdUsuario, cobrador.NombreUsuario, cobradorAnterior));
            fila.EstaMarcada = false;
        }

        ActualizarResumenPendientes();
        ActualizarEstadoBotones();
    }

    private async void OnConfirmarAsignaciones(object? sender, RoutedEventArgs e)
    {
        if (_pendientes.Count == 0)
        {
            MessageBox.Show("No hay asignaciones pendientes de confirmar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var modal = new ConfirmarAsignacionesModal(_pendientes.ToList()) { Owner = this };
        if (modal.ShowDialog() != true)
            return;

        var cantidad = _pendientes.Count;
        foreach (var p in _pendientes)
            await _cuotas.AsignarCobranzaCuotaAsync(p.IdGeneradas, p.IdCobrador, _session.UsuarioActual!.IdUsuario);

        _pendientes.Clear();
        ActualizarResumenPendientes();
        await RefrescarAsignacionesAsync(recargarCuotas: true);
        MostrarConfirmacion($"Se confirmaron {cantidad} asignación(es).");
    }

    private void ActualizarResumenPendientes()
    {
        _txtResumenPendientes.Text = $"Pendientes de confirmar: {_pendientes.Count}";
        _btnConfirmarAsignaciones.IsEnabled = _pendientes.Count > 0;
        _btnQuitarPendiente.IsEnabled = _gridPendientes.SelectedItem is PendienteFila;
        _panelPendientes.Visibility = _pendientes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnQuitarAsignacion(object? sender, RoutedEventArgs e)
    {
        if (_gridAsignaciones.SelectedItem is not AsignacionCobranza fila)
        {
            MessageBox.Show("Seleccioná una asignación de la lista.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirmar = MessageBox.Show(
            $"¿Quitar la asignación de {fila.Referencia} a {fila.CobradorNombre}?",
            "Confirmar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmar != MessageBoxResult.Yes)
            return;

        await _cuotas.QuitarAsignacionCobranzaAsync(fila.IdCab, fila.IdGeneradas);
        await RefrescarAsignacionesAsync(recargarCuotas: true);
    }

    private static void MostrarConfirmacion(string mensaje) =>
        MessageBox.Show(mensaje, "Asignación registrada", MessageBoxButton.OK, MessageBoxImage.Information);

    private void AbrirBuscadorCobrador(ComboBox destino)
    {
        var modal = new CobradorBuscadorModal(_usuariosCobradores) { Owner = this };
        if (modal.ShowDialog() == true)
            destino.SelectedItem = modal.CobradorSeleccionado;
    }

    // Mismo patrón que SeleccionarClienteCreditoModal.LocalBuscadorModal — modal de búsqueda
    // por código o nombre, en vez de un ComboBox largo difícil de recorrer con muchos cobradores.
    private sealed class CobradorBuscadorModal : Window
    {
        private readonly List<Usuario> _todos;
        private readonly DataGrid _grid;
        public Usuario? CobradorSeleccionado { get; private set; }

        public CobradorBuscadorModal(List<Usuario> cobradores)
        {
            _todos = cobradores;
            Title = "Seleccionar cobrador";
            Width = 640;
            Height = 560;
            MinWidth = 520;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontFamily = new FontFamily("Segoe UI");

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 47, 68)),
                Padding = new Thickness(16, 12, 16, 12)
            };
            header.Child = new TextBlock
            {
                Text = "Buscar cobrador por código o nombre",
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.Bold
            };
            root.Children.Add(header);
            Grid.SetRow(header, 0);

            var body = new Grid { Margin = new Thickness(14) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var search = new TextBox
            {
                Height = 32,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 10)
            };
            search.TextChanged += (_, _) => Filtrar(search.Text);
            body.Children.Add(search);
            Grid.SetRow(search, 0);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                RowHeight = 34,
                ColumnHeaderHeight = 38,
                FontSize = 11.5,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(233, 236, 239)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                CanUserResizeColumns = false
            };
            _grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader))
            {
                Setters =
                {
                    new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)),
                    new Setter(Control.FontWeightProperty, FontWeights.SemiBold),
                    new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                    new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center)
                }
            };
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Cód.",
                Binding = new Binding(nameof(Usuario.CodigoUsuario)),
                Width = 80
            });
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Cobrador",
                Binding = new Binding(nameof(Usuario.NombreUsuario)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Cargo",
                Binding = new Binding(nameof(Usuario.CargoUsuario)),
                Width = 130
            });
            _grid.MouseDoubleClick += (_, _) => Confirmar();
            body.Children.Add(_grid);
            Grid.SetRow(_grid, 1);

            root.Children.Add(body);
            Grid.SetRow(body, 1);

            var footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(250, 251, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(238, 241, 243)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 8, 14, 8)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            footerGrid.Children.Add(new TextBlock
            {
                Text = "Buscá por código o por nombre. Doble clic o Enter para elegir.",
                Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156)),
                FontSize = 11
            });

            var btnCancelar = new Button
            {
                Content = "Cancelar",
                Padding = new Thickness(14, 5, 14, 5),
                Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCancelar.Click += (_, _) => Close();
            Grid.SetColumn(btnCancelar, 1);
            footerGrid.Children.Add(btnCancelar);

            var btnSeleccionar = new Button
            {
                Content = "Seleccionar",
                Padding = new Thickness(14, 5, 14, 5),
                Background = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnSeleccionar.Click += (_, _) => Confirmar();
            Grid.SetColumn(btnSeleccionar, 2);
            footerGrid.Children.Add(btnSeleccionar);

            footer.Child = footerGrid;
            root.Children.Add(footer);
            Grid.SetRow(footer, 2);

            Content = root;
            Loaded += (_, _) =>
            {
                Filtrar("");
                search.Focus();
            };
            search.KeyDown += (_, e) => { if (e.Key == Key.Enter) Confirmar(); };
        }

        private void Filtrar(string texto)
        {
            var f = texto.Trim();
            var lista = string.IsNullOrWhiteSpace(f)
                ? _todos
                : _todos.Where(u =>
                    u.CodigoUsuario.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                    u.NombreUsuario.Contains(f, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            _grid.ItemsSource = lista.OrderBy(u => u.NombreUsuario).ToList();
        }

        private void Confirmar()
        {
            if (_grid.SelectedItem is not Usuario u)
                return;

            CobradorSeleccionado = u;
            DialogResult = true;
            Close();
        }
    }

    // Modal dedicado de confirmación — reemplaza el MessageBox genérico para dar el detalle
    // real de qué se va a escribir en la base: cada cuota, su monto, el cobrador nuevo y,
    // si corresponde, a quién se le está sacando la cuota (reasignación).
    private sealed class ConfirmarAsignacionesModal : Window
    {
        private sealed record FilaModal(string NCuotaTexto, string ClienteNombre, string MontoTexto, string CobradorTexto, bool EsReasignacion);

        public ConfirmarAsignacionesModal(List<PendienteFila> pendientes)
        {
            Title = "Confirmar asignaciones";
            Width = 640;
            Height = 560;
            MinWidth = 520;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontFamily = new FontFamily("Segoe UI");

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var reasignaciones = pendientes.Count(p => p.CobradorAnterior != null);

            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 47, 68)),
                Padding = new Thickness(16, 12, 16, 12)
            };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "CONFIRMAR ASIGNACIONES",
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.Bold
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Esto escribe las siguientes asignaciones en la base de datos.",
                Foreground = new SolidColorBrush(Color.FromRgb(127, 179, 211)),
                FontSize = 11.5,
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Child = headerStack;
            root.Children.Add(header);
            Grid.SetRow(header, 0);

            var filas = pendientes.Select(p => new FilaModal(
                p.NCuotaTexto,
                p.ClienteNombre,
                $"Gs. {p.Monto:N0}",
                p.CobradorAnterior != null ? $"{p.CobradorAnterior} → {p.CobradorNombre}" : p.CobradorNombre,
                p.CobradorAnterior != null)).ToList();

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                RowHeight = 32,
                ColumnHeaderHeight = 34,
                FontSize = 11.5,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(233, 236, 239)),
                BorderThickness = new Thickness(1, 0, 1, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                Margin = new Thickness(14, 14, 14, 8),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                CanUserResizeColumns = false,
                ItemsSource = filas
            };
            grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader))
            {
                Setters =
                {
                    new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)),
                    new Setter(Control.FontWeightProperty, FontWeights.SemiBold),
                    new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left)
                }
            };
            grid.Columns.Add(new DataGridTextColumn { Header = "Cuota", Binding = new Binding(nameof(FilaModal.NCuotaTexto)), Width = 90 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Cliente", Binding = new Binding(nameof(FilaModal.ClienteNombre)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Monto",
                Binding = new Binding(nameof(FilaModal.MontoTexto)),
                Width = 105,
                ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right) } }
            });
            grid.Columns.Add(new DataGridTextColumn { Header = "Cobrador", Binding = new Binding(nameof(FilaModal.CobradorTexto)), Width = 190 });
            grid.LoadingRow += (_, e) =>
            {
                if (e.Row.Item is FilaModal f && f.EsReasignacion)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(253, 236, 224));
                    e.Row.Foreground = new SolidColorBrush(Color.FromRgb(140, 75, 0));
                }
            };
            root.Children.Add(grid);
            Grid.SetRow(grid, 1);

            var totalMonto = pendientes.Sum(p => p.Monto);
            var resumen = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 244, 247)),
                Margin = new Thickness(14, 0, 14, 10),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8)
            };
            var resumenTexto = $"{pendientes.Count} cuota(s) — Gs. {totalMonto:N0} en total";
            if (reasignaciones > 0)
                resumenTexto += $" — {reasignaciones} reasignación(es) desde otro cobrador";
            resumen.Child = new TextBlock
            {
                Text = resumenTexto,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(38, 66, 94)),
                TextWrapping = TextWrapping.Wrap
            };
            root.Children.Add(resumen);
            Grid.SetRow(resumen, 2);

            var footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(250, 251, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(238, 241, 243)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 8, 14, 8)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            footerGrid.Children.Add(new TextBlock
            {
                Text = reasignaciones > 0 ? "Las filas naranjas le sacan la cuota a otro cobrador." : "",
                Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });

            var btnCancelar = new Button
            {
                Content = "Cancelar",
                Padding = new Thickness(14, 5, 14, 5),
                Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCancelar.Click += (_, _) => { DialogResult = false; Close(); };
            Grid.SetColumn(btnCancelar, 1);
            footerGrid.Children.Add(btnCancelar);

            var btnConfirmar = new Button
            {
                Content = "Confirmar y guardar",
                Padding = new Thickness(14, 5, 14, 5),
                Background = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnConfirmar.Click += (_, _) => { DialogResult = true; Close(); };
            Grid.SetColumn(btnConfirmar, 2);
            footerGrid.Children.Add(btnConfirmar);

            footer.Child = footerGrid;
            root.Children.Add(footer);
            Grid.SetRow(footer, 3);

            Content = root;
        }
    }

    private sealed class CuotaFila : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _estaMarcada;
        public bool EstaMarcada
        {
            get => _estaMarcada;
            set
            {
                if (_estaMarcada == value) return;
                _estaMarcada = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EstaMarcada)));
            }
        }

        public int IdCab { get; }
        public int IdGeneradas { get; }
        public string ClienteNombre { get; }
        public string ClienteCi { get; }
        public string NCuotaTexto { get; }
        public decimal MontoTotal { get; }
        public string MontoTexto { get; }
        public string VtoTexto { get; }
        public string DiasAtrasoTexto { get; }
        public string EstadoTexto { get; }
        public string AsignadoA { get; }
        public bool EsVencida { get; }
        public bool YaAsignada { get; }
        // true = esta cuota puntual tiene su propia asignación (nivel "Cuota");
        // false + YaAsignada = hereda la asignación del crédito completo (nivel "Crédito").
        public bool EsAsignacionPuntual { get; }
        public string? CobradorAsignado { get; }
        // true = este monto incluye el cargo de Inforconf (90+ días de mora, se cobra una
        // sola vez por episodio, siempre en la cuota atrasada más antigua del cliente — mismo
        // criterio que el panel de detalle de "Cobrar Cuota", ver CorrespondeCargoInforconfAsync).
        public bool IncluyeInforconf { get; }

        public CuotaFila(Cuota c, AsignacionCobranza? asignacion, decimal montoInforconf = 0m)
        {
            IdCab = c.IdCab;
            IdGeneradas = c.IdGeneradas;
            ClienteNombre = c.ClienteNombre;
            ClienteCi = c.ClienteCi;
            NCuotaTexto = c.NCuotaTexto;
            MontoTotal = c.TotalCuota + montoInforconf;
            IncluyeInforconf = montoInforconf > 0m;
            MontoTexto = IncluyeInforconf ? $"Gs. {MontoTotal:N0} *" : $"Gs. {MontoTotal:N0}";
            VtoTexto = c.Vto.ToString("dd/MM/yyyy");
            DiasAtrasoTexto = c.DiasDeAtraso > 0 ? $"{c.DiasDeAtraso} d" : "—";
            EstadoTexto = c.EstadoTextoCorto;
            EsVencida = c.EstaVencida;
            YaAsignada = asignacion != null;
            EsAsignacionPuntual = asignacion?.Nivel == "Cuota";
            CobradorAsignado = asignacion?.CobradorNombre;
            AsignadoA = asignacion == null
                ? "Sin asignar"
                : EsAsignacionPuntual
                    ? $"{asignacion.CobradorNombre} (esta cuota)"
                    : $"{asignacion.CobradorNombre} (todo el crédito)";
        }
    }

    private void InitializeComponent()
    {
        // Ventana construida por código.
    }
}

// "Cuota" → "C", "Crédito" → "Cr" — columna "Niv." de CrearGridAsignaciones, para no
// gastar ancho de columna mostrando la palabra completa en un panel angosto.
public sealed class PrimeraLetraConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value switch
        {
            "Cuota" => "C",
            "Crédito" => "Cr",
            string s when s.Length > 0 => s[..1],
            _ => ""
        };

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}






