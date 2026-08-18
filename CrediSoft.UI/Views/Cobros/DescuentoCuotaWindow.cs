using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Maestros;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Cobros;

// ══════════════════════════════════════════════════════════════════════════════
//  DESCUENTO POR NOTA DE CRÉDITO — aplicado de antemano a una cuota puntual
//  Pedido explícito: un administrador (o el usuario código 67) puede crear un
//  descuento para una cuota específica ANTES de que se cobre. Queda guardado en
//  DESCUENTOS_CUOTA y CobrosWindow lo detecta automáticamente al cargar esa cuota
//  — lo ve y lo aplica cualquier cajero de cualquier local, no solo quien lo creó.
//  Restringido en el punto de entrada del menú (ver MainWindow) — esta ventana en
//  sí no vuelve a chequear el permiso, confía en que solo se llega acá si ya se
//  tiene acceso, mismo criterio que el resto de las pantallas administrativas.
// ══════════════════════════════════════════════════════════════════════════════
public class DescuentoCuotaWindow : Window
{
    private readonly IClienteRepository _clientes;
    private readonly ICuotaRepository   _cuotas;
    private readonly ISessionService    _session;

    private static readonly SolidColorBrush BrPrimary   = new(Color.FromRgb(14, 47, 68));
    private static readonly SolidColorBrush BrPrimDark  = new(Color.FromRgb(14, 47, 68));
    private static readonly SolidColorBrush BrAzulClaro = new(Color.FromRgb(127, 179, 211));
    private static readonly SolidColorBrush BrVerde     = new(Color.FromRgb(22, 163, 74));
    private static readonly SolidColorBrush BrVerdeBg   = new(Color.FromRgb(240, 253, 244));
    private static readonly SolidColorBrush BrVerdeBd   = new(Color.FromRgb(187, 247, 208));
    private static readonly SolidColorBrush BrGris      = new(Color.FromRgb(107, 114, 128));
    private static readonly SolidColorBrush BrAmbar     = new(Color.FromRgb(180, 83, 9));
    private static readonly SolidColorBrush BrAmbarBg   = new(Color.FromRgb(255, 251, 235));
    private static readonly SolidColorBrush BrAmbarBd   = new(Color.FromRgb(253, 224, 171));
    private static readonly SolidColorBrush BrLabel     = new(Color.FromRgb(107, 114, 128));
    private static readonly SolidColorBrush BrBorde     = new(Color.FromRgb(226, 232, 240));
    private static readonly SolidColorBrush BrFondo     = new(Color.FromRgb(245, 247, 250));
    private static readonly SolidColorBrush BrBlanco    = Brushes.White;
    private static readonly SolidColorBrush BrRojo      = new(Color.FromRgb(220, 38, 38));
    private static readonly SolidColorBrush BrTextoOsc  = new(Color.FromRgb(31, 41, 55));

    private Border    _cardCliente   = null!;
    private TextBlock _lblClienteNombre = null!;
    private TextBlock _lblClienteSub    = null!;
    private Border     _panelCuotas  = null!;
    private DataGrid  _gridCuotas    = null!;
    private TextBlock _lblCuotaSel   = null!;
    private Border    _panelDescuento = null!;
    private TextBox   _txtMonto      = null!;
    private TextBox   _txtMotivo     = null!;
    private TextBox   _txtNroNc      = null!;
    private Button    _btnGuardar    = null!;

    private bool _formateandoMonto = false;

    private Cliente? _clienteActual;
    private Cuota?   _cuotaSeleccionada;
    private List<Cuota> _cuotasPendientes = new();

    public DescuentoCuotaWindow()
    {
        _clientes = App.Services.GetRequiredService<IClienteRepository>();
        _cuotas   = App.Services.GetRequiredService<ICuotaRepository>();
        _session  = SessionService.Instance;

        Title = "Descuento por Nota de Crédito"; Width = 880; Height = 560;
        MinWidth = 760; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrFondo;
        Content = Build();
    }

    private UIElement Build()
    {
        var root = new DockPanel();

        // ── Header ────────────────────────────────────────────────────────
        var hdr = new Border { Background = BrPrimary, Padding = new Thickness(16, 10, 16, 10) };
        var hdrRow = new StackPanel { Orientation = Orientation.Horizontal };
        hdrRow.Children.Add(new TextBlock { Text = "💳", FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = "DESCUENTO POR NOTA DE CRÉDITO", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = BrBlanco });
        hdrSp.Children.Add(new TextBlock
        {
            Text = "Se guarda en la cuota y se aplica solo al cobrarla, en cualquier local.",
            FontSize = 9.5, Foreground = BrAzulClaro, Margin = new Thickness(0, 1, 0, 0), TextWrapping = TextWrapping.Wrap
        });
        hdrRow.Children.Add(hdrSp);
        hdr.Child = hdrRow;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var body = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };

        // ── PASO 1 · Cliente ──────────────────────────────────────────────
        body.Children.Add(PasoTitulo("1", "CLIENTE"));

        _cardCliente = new Border
        {
            Background = BrBlanco, BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 5, 0, 0), Cursor = Cursors.Hand
        };
        var clienteRow = new DockPanel();
        var clienteInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _lblClienteNombre = new TextBlock { Text = "Ningún cliente seleccionado", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = BrLabel };
        _lblClienteSub = new TextBlock { Text = "Toque para buscar por C.I., nombre o apellido", FontSize = 9.5, Foreground = BrLabel, Margin = new Thickness(0, 1, 0, 0) };
        clienteInfo.Children.Add(_lblClienteNombre);
        clienteInfo.Children.Add(_lblClienteSub);
        DockPanel.SetDock(clienteInfo, Dock.Left);
        clienteRow.Children.Add(clienteInfo);

        var btnBuscarCliente = new Button
        {
            Content = "🔍  Buscar", Height = 28, Padding = new Thickness(12, 0, 12, 0), FontSize = 11,
            Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)), Foreground = BrBlanco,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center
        };
        btnBuscarCliente.Click += (_, __) => AbrirBuscadorCliente();
        clienteRow.Children.Add(btnBuscarCliente);
        _cardCliente.Child = clienteRow;
        _cardCliente.MouseLeftButtonUp += (_, __) => AbrirBuscadorCliente();
        body.Children.Add(_cardCliente);

        // ── PASO 2 (grilla de cuotas) + PASO 3 (datos del descuento) en dos columnas —
        // antes iban apilados uno debajo del otro y la tarjeta amarilla ocupaba todo el
        // ancho para 3 campos angostos, dejando mucho espacio horizontal sin usar (pedido
        // explícito: "utilizar mejor el ancho y llevar la parte amarilla a otra columna").
        var dosColumnas = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        dosColumnas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dosColumnas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        dosColumnas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });

        // ── PASO 2 · Cuota ────────────────────────────────────────────────
        _panelCuotas = new Border { Visibility = Visibility.Collapsed };
        var panelCuotasSp = new StackPanel();
        panelCuotasSp.Children.Add(PasoTitulo("2", "CUOTA — seleccione a cuál aplicar el descuento"));

        _gridCuotas = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, Height = 220, Margin = new Thickness(0, 5, 0, 0),
            SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
            FontSize = 11, RowHeight = 30, ColumnHeaderHeight = 34,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowBackground = BrBlanco, AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(249, 250, 251)),
            Foreground = Brushes.Black, BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CanUserResizeRows = false, ColumnHeaderStyle = BuildHeaderStyle()
        };
        // Anchos ajustados al texto real del header (antes se cortaban: "Total cuota (Gs.)"
        // y "Días atraso" no entraban en el ancho fijo asignado, reportado real) — ahora los
        // headers también envuelven texto (BuildHeaderStyle) como red de seguridad extra.
        _gridCuotas.Columns.Add(new DataGridTextColumn { Header = "Cuota", Binding = new System.Windows.Data.Binding(nameof(Cuota.NCuotaTexto)), Width = 70 });
        _gridCuotas.Columns.Add(new DataGridTextColumn { Header = "Vence", Binding = new System.Windows.Data.Binding(nameof(Cuota.Vto)) { StringFormat = "dd/MM/yyyy" }, Width = 85 });
        _gridCuotas.Columns.Add(new DataGridTextColumn { Header = "Estado", Binding = new System.Windows.Data.Binding(nameof(Cuota.EstadoTextoCorto)), Width = 80 });
        _gridCuotas.Columns.Add(new DataGridTextColumn { Header = "Atraso", Binding = new System.Windows.Data.Binding(nameof(Cuota.DiasDeAtraso)), Width = 60 });
        _gridCuotas.Columns.Add(new DataGridTextColumn { Header = "Total cuota (Gs.)", Binding = new System.Windows.Data.Binding(nameof(Cuota.TotalCuota)) { StringFormat = "N0" }, Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 110 });
        _gridCuotas.SelectionChanged += (_, __) => { _cuotaSeleccionada = _gridCuotas.SelectedItem as Cuota; ActualizarPanelDescuento(); };
        panelCuotasSp.Children.Add(_gridCuotas);
        _panelCuotas.Child = panelCuotasSp;
        Grid.SetColumn(_panelCuotas, 0); dosColumnas.Children.Add(_panelCuotas);

        // ── PASO 3 · Descuento (colapsado hasta elegir cuota) ────────────
        _panelDescuento = new Border
        {
            Background = BrAmbarBg, BorderBrush = BrAmbarBd, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 10, 12, 10),
            Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Top
        };
        var panelSp = new StackPanel();

        var pasoTitulo3 = PasoTitulo("3", "DATOS DEL DESCUENTO");
        panelSp.Children.Add(pasoTitulo3);

        _lblCuotaSel = new TextBlock { FontSize = 10, Foreground = BrAmbar, Margin = new Thickness(0, 4, 0, 8), TextWrapping = TextWrapping.Wrap };
        panelSp.Children.Add(_lblCuotaSel);

        TextBox MkInput() => new TextBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 5, 8, 5), FontSize = 12,
            Background = BrBlanco, BorderBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6)), BorderThickness = new Thickness(1)
        };
        TextBlock MkLbl(string t) => new TextBlock { Text = t, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = BrAmbar, Margin = new Thickness(0, 0, 0, 3) };

        var colMonto = new StackPanel();
        colMonto.Children.Add(MkLbl("MONTO DEL DESCUENTO (Gs.)"));
        _txtMonto = MkInput();
        _txtMonto.FontWeight = FontWeights.Bold; _txtMonto.FontSize = 14; _txtMonto.TextAlignment = TextAlignment.Right;
        _txtMonto.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        // Separador de miles en vivo (mismo patrón que TxtMontoParcial en CobrosWindow) —
        // sin esto un monto de 30.000 se veía como "30000", difícil de leer rápido al cargar
        // un descuento (pedido explícito: "falta agregar separador de miles").
        _txtMonto.TextChanged += (_, __) =>
        {
            if (_formateandoMonto) return;
            _formateandoMonto = true;
            var digitos = new string(_txtMonto.Text.Where(char.IsDigit).ToArray());
            decimal.TryParse(digitos, out var monto);
            _txtMonto.Text = monto == 0 ? "" : monto.ToString("N0").Replace(",", ".");
            _txtMonto.CaretIndex = _txtMonto.Text.Length;
            _formateandoMonto = false;
        };
        colMonto.Children.Add(_txtMonto);
        panelSp.Children.Add(colMonto);

        var fila2 = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        fila2.Children.Add(MkLbl("N° NOTA DE CRÉDITO (opcional)"));
        _txtNroNc = MkInput();
        fila2.Children.Add(_txtNroNc);
        panelSp.Children.Add(fila2);

        var fila3 = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        fila3.Children.Add(MkLbl("MOTIVO"));
        _txtMotivo = new TextBox { Padding = new Thickness(8, 5, 8, 5), FontSize = 11.5, Background = BrBlanco,
            BorderBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6)), BorderThickness = new Thickness(1),
            TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 90 };
        fila3.Children.Add(_txtMotivo);
        panelSp.Children.Add(fila3);

        _panelDescuento.Child = panelSp;
        Grid.SetColumn(_panelDescuento, 2); dosColumnas.Children.Add(_panelDescuento);

        body.Children.Add(dosColumnas);

        scroll.Content = body;
        DockPanel.SetDock(scroll, Dock.Top); root.Children.Add(scroll);

        // ── Footer ────────────────────────────────────────────────────────
        var footer = new Border { Background = BrBlanco, BorderBrush = BrBorde, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 8, 16, 8) };
        var footSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnCancelar = new Button { Content = "Cerrar", Height = 30, Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(0, 0, 6, 0), FontSize = 11,
            Background = BrGris, Foreground = BrBlanco, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCancelar.Click += (_, __) => Close();
        _btnGuardar = new Button { Content = "✓  Guardar descuento", Height = 30, Padding = new Thickness(16, 0, 16, 0), FontSize = 11,
            Background = BrVerde, Foreground = BrBlanco, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, IsEnabled = false };
        _btnGuardar.Click += async (_, __) => await GuardarAsync();
        footSp.Children.Add(_btnGuardar); footSp.Children.Add(btnCancelar);
        footer.Child = footSp;
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);

        return root;
    }

    // Headers del DataGrid por defecto no envuelven texto ni crecen con el contenido — con
    // columnas angostas el texto del header se corta a la mitad (reportado real: "Total
    // cuota (Gs.)" y "Días atraso" ilegibles). TextWrapping.Wrap + altura de header más alta
    // (ColumnHeaderHeight=42) resuelve el corte incluso si el ancho de columna queda ajustado.
    private static Style BuildHeaderStyle()
    {
        var style = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(14, 47, 68))));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 9.5));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 3, 6, 3)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        var template = new ControlTemplate(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetValue(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var content = new FrameworkElementFactory(typeof(TextBlock));
        content.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Content") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        content.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        content.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        content.SetValue(TextBlock.FontSizeProperty, 9.5);
        content.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        content.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private UIElement PasoTitulo(string numero, string texto)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var badge = new Border
        {
            Width = 17, Height = 17, CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.FromRgb(14, 47, 68)),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock { Text = numero, Foreground = BrBlanco, FontSize = 9.5, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(badge);
        row.Children.Add(new TextBlock { Text = texto, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = BrTextoOsc,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
        return row;
    }

    // Reutiliza el buscador estándar de clientes (ya usado en Ventas/Cobros/Maestros) — con
    // grid paginado, filtros y búsqueda por CI, Nombre o Teléfono en un solo campo. Antes acá
    // había un TextBox aislado que solo buscaba por CI exacto: sin nombre/apellido no había
    // forma de encontrar al cliente si no se sabía su CI de memoria (pedido explícito: "poder
    // buscar ya sea por ci o por nombre o apellido").
    private void AbrirBuscadorCliente()
    {
        var modal = new BuscadorClienteModal(_clientes, soloConCuotas: true) { Owner = this };
        if (modal.ShowDialog() == true && modal.ClienteSeleccionado != null)
            _ = CargarClienteAsync(modal.ClienteSeleccionado);
    }

    private async Task CargarClienteAsync(Cliente cliente)
    {
        _clienteActual = cliente;
        _cuotaSeleccionada = null;
        _panelDescuento.Visibility = Visibility.Collapsed;
        _btnGuardar.IsEnabled = false;

        _lblClienteNombre.Text = cliente.NombreCliente;
        _lblClienteNombre.Foreground = BrPrimDark;
        _lblClienteSub.Text = $"C.I.: {cliente.CiCliente}   —   Toque para cambiar de cliente";

        var todas = (await _cuotas.BuscarTodasPorCiAsync(cliente.CiCliente)).ToList();
        // Solo cuotas pendientes tiene sentido descontar — una ya cobrada no puede "recibir"
        // un descuento retroactivo con este flujo (para eso existe la NC como ajuste posterior,
        // ver ventana "Cobros por Nota de Crédito").
        _cuotasPendientes = todas.Where(c => c.EstaPendiente && c.NCuota > 1).OrderBy(c => c.Vto).ToList();
        _gridCuotas.ItemsSource = _cuotasPendientes;
        _panelCuotas.Visibility = Visibility.Visible;

        if (_cuotasPendientes.Count == 0)
            MessageBox.Show("Este cliente no tiene cuotas pendientes (fuera de la entrega inicial).", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ActualizarPanelDescuento()
    {
        if (_cuotaSeleccionada == null) { _panelDescuento.Visibility = Visibility.Collapsed; _btnGuardar.IsEnabled = false; return; }

        var yaExiste = await _cuotas.ObtenerDescuentoPendienteAsync(_cuotaSeleccionada.IdGeneradas);
        if (yaExiste != null)
        {
            MessageBox.Show(
                $"Esta cuota ya tiene un descuento pendiente sin cobrar:\n\n" +
                $"Monto: {yaExiste.Monto:N0} Gs.\nMotivo: {yaExiste.Motivo}\nCreado: {yaExiste.FechaCreacion:dd/MM/yyyy HH:mm}\n\n" +
                "No se puede cargar un segundo descuento mientras el anterior siga pendiente.",
                "Descuento ya existente", MessageBoxButton.OK, MessageBoxImage.Warning);
            _panelDescuento.Visibility = Visibility.Collapsed;
            _btnGuardar.IsEnabled = false;
            _gridCuotas.SelectedItem = null;
            _cuotaSeleccionada = null;
            return;
        }

        _lblCuotaSel.Text = $"Cuota {_cuotaSeleccionada.NCuotaTexto}  ·  vence {_cuotaSeleccionada.Vto:dd/MM/yyyy}  ·  total actual {_cuotaSeleccionada.TotalCuota:N0} Gs.";
        _panelDescuento.Visibility = Visibility.Visible;
        _btnGuardar.IsEnabled = true;
        _txtMonto.Focus();
    }

    private async Task GuardarAsync()
    {
        if (_cuotaSeleccionada == null) return;
        var digitosMonto = new string(_txtMonto.Text.Where(char.IsDigit).ToArray());
        if (!decimal.TryParse(digitosMonto, out var monto) || monto <= 0)
        {
            MessageBox.Show("Ingrese un monto de descuento válido.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (monto >= _cuotaSeleccionada.TotalCuota)
        {
            MessageBox.Show("El descuento no puede ser mayor o igual al total de la cuota.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var idUsuario = _session.UsuarioActual?.IdUsuario ?? 0;
        _btnGuardar.IsEnabled = false;
        try
        {
            var ok = await _cuotas.CrearDescuentoCuotaAsync(
                _cuotaSeleccionada.IdGeneradas, monto,
                string.IsNullOrWhiteSpace(_txtMotivo.Text) ? null : _txtMotivo.Text.Trim(),
                string.IsNullOrWhiteSpace(_txtNroNc.Text) ? null : _txtNroNc.Text.Trim(),
                idUsuario);

            if (ok)
            {
                MessageBox.Show(
                    $"Descuento de {monto:N0} Gs. guardado para la cuota {_cuotaSeleccionada.NCuotaTexto} de {_clienteActual!.NombreCliente}.\n\n" +
                    "Se aplicará automáticamente cuando esa cuota se cobre, en cualquier local.",
                    "Descuento guardado", MessageBoxButton.OK, MessageBoxImage.Information);
                _txtMonto.Text = ""; _txtMotivo.Text = ""; _txtNroNc.Text = "";
                _panelDescuento.Visibility = Visibility.Collapsed;
                _gridCuotas.SelectedItem = null;
                _cuotaSeleccionada = null;
                if (_clienteActual != null) await CargarClienteAsync(_clienteActual);
            }
            else
            {
                MessageBox.Show("No se pudo guardar el descuento.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _btnGuardar.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _btnGuardar.IsEnabled = true;
        }
    }
}
