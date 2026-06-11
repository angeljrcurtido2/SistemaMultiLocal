using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Maestros;

// ═══════════════════════════════════════════════════════════════════
//  FUNCIONARIOS (Usuarios del sistema — CRUD completo)
// ═══════════════════════════════════════════════════════════════════
public class FuncionariosWindow : Window
{
    private readonly IUsuarioRepository _repo;
    private readonly ILocalRepository   _locales;
    private readonly ISessionService    _session;

    private static readonly System.Globalization.CultureInfo _cult =
        System.Globalization.CultureInfo.GetCultureInfo("es-AR");

    private DataGrid _grid = null!;
    private TextBox  _txtBuscar = null!;
    private List<Usuario> _todosUsuarios = new();
    private TextBox  _txtCi = null!, _txtCodigo = null!, _txtNombre = null!,
                     _txtDireccion = null!, _txtTelefono = null!,
                     _txtSalario = null!,
                     _txtComision = null!, _txtComCob = null!;
    private PasswordBox _txtPass = null!, _txtConfPass = null!;
    private ComboBox  _cboCargo = null!, _cboLocal = null!, _cboZona = null!;
    private CheckBox  _chkDesc = null!, _chkStock = null!, _chkPrecio = null!,
                      _chkElim = null!, _chkCuotas = null!, _chkCompras = null!, _chkFact = null!;
    private Button    _btnGuardar = null!, _btnEliminar = null!, _btnNuevo = null!;
    private TextBlock _lblFechaIngreso = null!;
    private Usuario?  _seleccionado;
    private List<Local> _listaLocales = new();

    public FuncionariosWindow()
    {
        _repo    = App.Services.GetRequiredService<IUsuarioRepository>();
        _locales = App.Services.GetRequiredService<ILocalRepository>();
        _session = SessionService.Instance;
        Title  = "Funcionarios del Sistema";
        Width  = 980; Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xFF, 0xA5, 0x00));
        BuildUI();
        Loaded += async (_, _) => { await CargarLocales(); await Refrescar(); };
    }

    // ── helpers visuales ──────────────────────────────────────────────
    private static System.Windows.Media.Color Hex(string h) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h)!;

    private static Border SectionHeader(string titulo) => new Border
    {
        Background  = new System.Windows.Media.SolidColorBrush(Hex("#CC6600")),
        Padding     = new Thickness(8, 3, 8, 3),
        Margin      = new Thickness(0, 8, 0, 4),
        Child       = new TextBlock
        {
            Text       = titulo,
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize   = 11,
        }
    };

    private static TextBox MakeTxt() => new TextBox
    {
        Padding    = new Thickness(6, 4, 6, 4),
        Background = System.Windows.Media.Brushes.White,
        BorderBrush = new System.Windows.Media.SolidColorBrush(Hex("#CC6600")),
        BorderThickness = new Thickness(1),
        Margin     = new Thickness(0, 0, 0, 2),
    };

    // Crea un PasswordBox con botón ojo que alterna visibilidad
    private static (PasswordBox pass, UIElement container) MakePassField()
    {
        var pass = new PasswordBox
        {
            Padding = new Thickness(6, 4, 6, 4),
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        var txtVisible = new TextBox
        {
            Padding = new Thickness(6, 4, 6, 4),
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        // Ojo abierto: emoji Unicode, ojo cerrado: ojo + línea diagonal superpuesta
        var ojoAbierto = "👁";

        // Contenido del botón: Grid con el emoji y encima una línea diagonal (solo visible al ocultar)
        var ojoLabel = new TextBlock
        {
            Text = ojoAbierto,
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Cursor = Cursors.Hand,
            ToolTip = "Mostrar/ocultar contraseña",
        };
        var slash = new System.Windows.Shapes.Line
        {
            X1 = 2, Y1 = 14, X2 = 14, Y2 = 2,
            Stroke = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60)),
            StrokeThickness = 2,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        var ojoGrid = new Grid { Width = 20, Height = 18 };
        ojoGrid.Children.Add(ojoLabel);
        ojoGrid.Children.Add(slash);

        var btnOjo = new Button
        {
            Width = 32,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = ojoGrid,
            Focusable = false,
        };

        // Template sin chrome de Windows para que sea realmente transparente
        var tpl = new System.Windows.Controls.ControlTemplate(typeof(Button));
        var border2 = new System.Windows.FrameworkElementFactory(typeof(Border));
        border2.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        border2.SetValue(Border.PaddingProperty, new Thickness(4, 0, 4, 0));
        var cp = new System.Windows.FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border2.AppendChild(cp);
        tpl.VisualTree = border2;
        btnOjo.Template = tpl;

        bool visible = false;
        btnOjo.Click += (_, __) =>
        {
            visible = !visible;
            if (visible)
            {
                txtVisible.Text = pass.Password;
                pass.Visibility = Visibility.Collapsed;
                txtVisible.Visibility = Visibility.Visible;
                txtVisible.Focus();
                txtVisible.CaretIndex = txtVisible.Text.Length;
                slash.Visibility = Visibility.Visible;
            }
            else
            {
                pass.Password = txtVisible.Text;
                txtVisible.Visibility = Visibility.Collapsed;
                pass.Visibility = Visibility.Visible;
                pass.Focus();
                slash.Visibility = Visibility.Collapsed;
            }
        };
        txtVisible.TextChanged += (_, __) => { if (visible) pass.Password = txtVisible.Text; };

        var border = new Border
        {
            BorderBrush = new System.Windows.Media.SolidColorBrush(Hex("#CC6600")),
            BorderThickness = new Thickness(1),
            Background = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0, 0, 0, 2),
        };
        var inner = new Grid();
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(pass,       0); Grid.SetColumn(txtVisible, 0); Grid.SetColumn(btnOjo, 1);
        inner.Children.Add(pass);
        inner.Children.Add(txtVisible);
        inner.Children.Add(btnOjo);
        border.Child = inner;

        return (pass, border);
    }

    private static TextBlock FieldLabel(string text) => new TextBlock
    {
        Text       = text,
        FontSize   = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = System.Windows.Media.Brushes.White,
        Margin     = new Thickness(0, 5, 0, 1),
    };

    private static StackPanel WithPct(TextBox tb)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(tb);
        sp.Children.Add(new TextBlock
        {
            Text = " %", VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White,
        });
        return sp;
    }

    private Button Btn(string text, string hex, int width = 80) => new Button
    {
        Content = text, Width = width, Height = 32, Margin = new Thickness(0, 0, 6, 0),
        Background = new System.Windows.Media.SolidColorBrush(Hex(hex)),
        Foreground = System.Windows.Media.Brushes.White,
        FontWeight = FontWeights.SemiBold,
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
    };

    // ── BuildUI ───────────────────────────────────────────────────────
    private void BuildUI()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(450) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ════════════ PANEL IZQUIERDO (formulario) ════════════
        var form = new StackPanel { Margin = new Thickness(12, 8, 12, 8) };

        void Add(string lbl, UIElement ctrl) { form.Children.Add(FieldLabel(lbl)); form.Children.Add(ctrl); }

        // Fecha de ingreso (solo lectura)
        var fechaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
        fechaRow.Children.Add(new TextBlock
        {
            Text = "Fecha de ingreso:", FontWeight = FontWeights.SemiBold,
            Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        _lblFechaIngreso = new TextBlock
        {
            Text = DateTime.Today.ToString("d/M/yyyy"),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        fechaRow.Children.Add(_lblFechaIngreso);
        form.Children.Add(fechaRow);

        // ── DATOS PERSONALES ─────────────────────────────────
        form.Children.Add(SectionHeader("DATOS PERSONALES"));

        var datosGrid = new Grid();
        datosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        datosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _txtCi     = MakeTxt();
        _txtCodigo = MakeTxt();

        var colCi = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
        colCi.Children.Add(FieldLabel("C.I.")); colCi.Children.Add(_txtCi);
        var colCod = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
        colCod.Children.Add(FieldLabel("Código")); colCod.Children.Add(_txtCodigo);

        Grid.SetColumn(colCi, 0); Grid.SetColumn(colCod, 1);
        datosGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        datosGrid.Children.Add(colCi); datosGrid.Children.Add(colCod);
        form.Children.Add(datosGrid);

        _txtNombre    = MakeTxt();  Add("Nombre", _txtNombre);
        _txtDireccion = MakeTxt(); Add("Dirección", _txtDireccion);
        _txtTelefono  = MakeTxt();  Add("Teléfono", _txtTelefono);

        // Salario en fila con separador de miles
        _txtSalario = MakeTxt();
        _txtSalario.TextAlignment = TextAlignment.Right;
        AjustarNumerico(_txtSalario, soloEntero: true);
        Add("Salario", _txtSalario);

        // Comisiones en fila doble
        var comRow = new Grid();
        comRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        comRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        comRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _txtComision = MakeTxt(); _txtComision.Width = 80; _txtComision.TextAlignment = TextAlignment.Right;
        _txtComCob   = MakeTxt(); _txtComCob.Width   = 80; _txtComCob.TextAlignment   = TextAlignment.Right;
        AjustarNumerico(_txtComision, soloEntero: true);
        AjustarNumerico(_txtComCob,   soloEntero: true);

        var colCom = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
        colCom.Children.Add(FieldLabel("Comisión venta")); colCom.Children.Add(WithPct(_txtComision));
        var colCob = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
        colCob.Children.Add(FieldLabel("Comisión cobranza")); colCob.Children.Add(WithPct(_txtComCob));
        Grid.SetColumn(colCom, 0); Grid.SetColumn(colCob, 1);
        comRow.Children.Add(colCom); comRow.Children.Add(colCob);
        form.Children.Add(comRow);

        // Cargo / Local
        var cargoRow = new Grid();
        cargoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cargoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cargoRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _cboCargo = new ComboBox { Padding = new Thickness(4, 3, 4, 3) };
        foreach (var c in new[] { "Vendedor", "ADMINISTRADOR" })
            _cboCargo.Items.Add(c);
        _cboCargo.SelectedIndex = 0;

        _cboLocal = new ComboBox { Padding = new Thickness(4, 3, 4, 3), DisplayMemberPath = "NombreLocal", SelectedValuePath = "IdLocal" };

        var colCargo = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
        colCargo.Children.Add(FieldLabel("Cargo")); colCargo.Children.Add(_cboCargo);
        var colLocal = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
        colLocal.Children.Add(FieldLabel("Local")); colLocal.Children.Add(_cboLocal);
        Grid.SetColumn(colCargo, 0); Grid.SetColumn(colLocal, 1);
        cargoRow.Children.Add(colCargo); cargoRow.Children.Add(colLocal);
        form.Children.Add(cargoRow);

        _cboZona = new ComboBox { Padding = new Thickness(4, 3, 4, 3), DisplayMemberPath = "NombreLocal", SelectedValuePath = "IdLocal" };
        form.Children.Add(FieldLabel("Zona (local de cobranza)"));
        form.Children.Add(_cboZona);

        // ── CONTRASEÑA ───────────────────────────────────────
        form.Children.Add(SectionHeader("CONTRASEÑA"));
        UIElement passContainer, confContainer;
        (_txtPass,     passContainer) = MakePassField();
        (_txtConfPass, confContainer) = MakePassField();
        form.Children.Add(FieldLabel("Contraseña"));           form.Children.Add(passContainer);
        form.Children.Add(FieldLabel("Confirmar contraseña")); form.Children.Add(confContainer);

        // ── PRIVILEGIOS ──────────────────────────────────────
        form.Children.Add(SectionHeader("PRIVILEGIOS"));

        CheckBox Chk(string label)
        {
            var cb = new CheckBox
            {
                Content = label, Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 3, 0, 3),
            };
            return cb;
        }

        _chkDesc   = Chk("Hacer descuento en ventas");
        _chkStock  = Chk("Modificar stock");
        _chkPrecio = Chk("Modificar precios");
        _chkElim   = Chk("Eliminar registro");
        _chkCuotas = Chk("Cobrar cuotas");
        _chkCompras= Chk("Realizar compras");
        _chkFact   = Chk("Eliminar factura (venta)");

        var permPanel = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
        foreach (var ch in new[] { _chkDesc, _chkStock, _chkPrecio, _chkElim, _chkCuotas, _chkCompras, _chkFact })
            permPanel.Children.Add(ch);
        form.Children.Add(permPanel);

        // ── BOTONES ──────────────────────────────────────────
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        _btnNuevo    = Btn("Nuevo",   "#FF8C00"); _btnNuevo.Click    += OnNuevo;
        _btnGuardar  = Btn("Guardar", "#27AE60"); _btnGuardar.Click  += OnGuardar;
        _btnEliminar = Btn("Eliminar","#E74C3C"); _btnEliminar.Click += OnEliminar; _btnEliminar.IsEnabled = false;
        var btnCerrar = Btn("Cerrar", "#757575");  btnCerrar.Click   += (_, _) => Close();
        foreach (var b in new[] { _btnNuevo, _btnGuardar, _btnEliminar, btnCerrar })
            btnPanel.Children.Add(b);
        form.Children.Add(btnPanel);

        var scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(scroll, 0);
        root.Children.Add(scroll);

        // ════════════ PANEL DERECHO (listado) ════════════
        var rightPanel = new DockPanel { Margin = new Thickness(8), Background = System.Windows.Media.Brushes.White };

        // ── Buscador ──
        var buscarBorder = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(Hex("#E07000")),
            Padding = new Thickness(8, 6, 8, 6),
        };
        var buscarPanel = new StackPanel { Orientation = Orientation.Horizontal };
        buscarPanel.Children.Add(new TextBlock
        {
            Text = "Buscar funcionario:",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        _txtBuscar = new TextBox
        {
            Width = 180, Padding = new Thickness(6, 3, 6, 3),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        var btnBuscar = new Button
        {
            Content = "🔍 Buscar", Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(6, 0, 0, 0),
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontWeight = FontWeights.SemiBold,
        };
        buscarPanel.Children.Add(_txtBuscar);
        buscarPanel.Children.Add(btnBuscar);
        buscarBorder.Child = buscarPanel;
        DockPanel.SetDock(buscarBorder, Dock.Top);
        rightPanel.Children.Add(buscarBorder);

        // Enter en el buscador o click en botón filtran el grid
        _txtBuscar.TextChanged += (_, __) => FiltrarGrid();
        _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) FiltrarGrid(); };
        btnBuscar.Click        += (_, __) => FiltrarGrid();

        // ── Header del grid ──
        var hdr = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(Hex("#CC6600")),
            Padding = new Thickness(10, 5, 10, 5),
        };
        hdr.Child = new TextBlock
        {
            Text = "Lista de Funcionarios",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold,
        };
        DockPanel.SetDock(hdr, Dock.Top);
        rightPanel.Children.Add(hdr);

        _grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.FloralWhite,
            Margin = new Thickness(0, 4, 0, 0),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Código", Binding = new System.Windows.Data.Binding("CodigoUsuario"), Width = 70 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Nombre", Binding = new System.Windows.Data.Binding("NombreUsuario"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Cargo",  Binding = new System.Windows.Data.Binding("CargoUsuario"),  Width = 100 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Local",  Binding = new System.Windows.Data.Binding("LocalNombre"),   Width = 90 });
        _grid.SelectionChanged += OnSeleccion;
        rightPanel.Children.Add(_grid);

        Grid.SetColumn(rightPanel, 1);
        root.Children.Add(rightPanel);
        Content = root;
    }

    // Formato de miles en tiempo real para TextBox numérico (sin decimales)
    private static void AjustarNumerico(TextBox tb, bool soloEntero = true)
    {
        bool upd = false;
        tb.PreviewTextInput += (_, e) => { e.Handled = !char.IsDigit(e.Text[0]); };
        DataObject.AddPastingHandler(tb, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            { if (!((string)e.DataObject.GetData(DataFormats.Text)).All(char.IsDigit)) e.CancelCommand(); }
            else e.CancelCommand();
        });
        tb.TextChanged += (_, __) =>
        {
            if (upd) return; upd = true;
            var caret = tb.CaretIndex;
            var digsBefore = tb.Text.Take(caret).Count(char.IsDigit);
            var raw = tb.Text.Replace(".", "").TrimStart('0');
            if (raw == "") raw = "0";
            var fmt = long.TryParse(raw, out var n) ? n.ToString("#,0", _cult) : raw;
            tb.Text = fmt;
            var nc = 0; var digs = 0;
            foreach (var c in fmt) { if (digs == digsBefore) break; if (char.IsDigit(c)) digs++; nc++; }
            tb.CaretIndex = Math.Min(nc, fmt.Length);
            upd = false;
        };
        tb.LostFocus += (_, __) => { if (string.IsNullOrWhiteSpace(tb.Text)) tb.Text = "0"; };
    }

    // ── Datos ─────────────────────────────────────────────────────────
    private async Task CargarLocales()
    {
        _listaLocales = (await _locales.ListarTodosAsync()).ToList();
        _cboLocal.ItemsSource = _listaLocales;
        if (_listaLocales.Any())
            _cboLocal.SelectedValue = _session.LocalActual?.IdLocal ?? _listaLocales[0].IdLocal;

        // Zona: mismo listado pero con opción "Sin zona" (IdLocal = 0) al inicio
        var zonaItems = new List<Local> { new Local { IdLocal = 0, NombreLocal = "(Sin zona)" } };
        zonaItems.AddRange(_listaLocales);
        _cboZona.ItemsSource = zonaItems;
        _cboZona.SelectedValue = 0;
    }

    private async Task Refrescar()
    {
        _todosUsuarios = (await _repo.ListarTodosAsync()).ToList();
        foreach (var u in _todosUsuarios)
            u.LocalNombre = _listaLocales.FirstOrDefault(l => l.IdLocal == u.LocalUsuario)?.NombreLocal ?? u.LocalUsuario.ToString();
        FiltrarGrid();
    }

    private void FiltrarGrid()
    {
        var term = _txtBuscar?.Text.Trim() ?? string.Empty;
        _grid.ItemsSource = string.IsNullOrEmpty(term)
            ? _todosUsuarios
            : _todosUsuarios.Where(u =>
                u.NombreUsuario.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.CodigoUsuario.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnSeleccion(object s, SelectionChangedEventArgs e)
    {
        if (_grid.SelectedItem is not Usuario u) return;
        _seleccionado = u;
        _txtCi.Text        = u.CiUsuario;
        _txtCodigo.Text    = u.CodigoUsuario;
        _txtPass.Password     = u.ContrasenaUsuario;
        _txtConfPass.Password = u.ContrasenaUsuario;
        _txtNombre.Text    = u.NombreUsuario;
        _txtDireccion.Text = u.DireccionUsuario;
        _txtTelefono.Text  = u.TelefonoUsuario;
        _cboZona.SelectedValue = int.TryParse(u.ZonaUsuario, out var z) ? z : 0;
        _txtSalario.Text   = u.SalarioUsuario == 0 ? "0" : u.SalarioUsuario.ToString("#,0", _cult);
        _txtComision.Text  = u.ComisionUsuario;
        _txtComCob.Text    = u.ComisionCobranza;
        _cboCargo.Text     = u.CargoUsuario;
        _cboLocal.SelectedValue  = u.LocalUsuario;
        _chkDesc.IsChecked   = u.PermisoDescuento == "SI";
        _chkStock.IsChecked  = u.PermisoStock == "SI";
        _chkPrecio.IsChecked = u.PermisoPrecio == "SI";
        _chkElim.IsChecked   = u.PermisoEliminar == "SI";
        _chkCuotas.IsChecked = u.PermisoCobrarCuotas == "SI";
        _chkCompras.IsChecked= u.PermisoCompras == "SI";
        _chkFact.IsChecked   = u.PermisoEliminarFactura == "SI";
        _btnEliminar.IsEnabled = true;
    }

    private void OnNuevo(object s, RoutedEventArgs e)
    {
        _seleccionado = null;
        _grid.SelectedItem = null;
        foreach (var t in new[] { _txtCi, _txtCodigo, _txtNombre, _txtDireccion, _txtTelefono })
            t.Text = "";
        _txtPass.Password = "";
        _txtConfPass.Password = "";
        _txtSalario.Text  = "0";
        _txtComision.Text = "0";
        _txtComCob.Text   = "0";
        _cboCargo.SelectedIndex = 0;
        if (_listaLocales.Any()) _cboLocal.SelectedValue = _session.LocalActual?.IdLocal ?? _listaLocales[0].IdLocal;
        _cboZona.SelectedValue = 0;
        foreach (var ch in new[] { _chkDesc, _chkStock, _chkPrecio, _chkElim, _chkCuotas, _chkCompras, _chkFact })
            ch.IsChecked = false;
        _lblFechaIngreso.Text = DateTime.Today.ToString("d/M/yyyy");
        _btnEliminar.IsEnabled = false;
        _txtCi.Focus();
    }

    private async void OnGuardar(object s, RoutedEventArgs e)
    {
        // Campos obligatorios
        if (string.IsNullOrWhiteSpace(_txtCi.Text))
        {
            MessageBox.Show("El CI es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            _txtCi.Focus(); return;
        }
        if (string.IsNullOrWhiteSpace(_txtCodigo.Text))
        {
            MessageBox.Show("El código de acceso es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            _txtCodigo.Focus(); return;
        }
        if (string.IsNullOrWhiteSpace(_txtNombre.Text))
        {
            MessageBox.Show("El nombre es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            _txtNombre.Focus(); return;
        }
        if (_cboLocal.SelectedValue == null)
        {
            MessageBox.Show("Debe seleccionar un local.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            _cboLocal.Focus(); return;
        }
        if (string.IsNullOrWhiteSpace(_txtPass.Password))
        {
            MessageBox.Show("La contraseña es obligatoria.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_txtPass.Password != _txtConfPass.Password)
        {
            MessageBox.Show("Las contraseñas no coinciden.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _txtConfPass.Focus();
            return;
        }

        // Rango de comisiones 0-100
        if (int.TryParse(_txtComision.Text.Replace(".", ""), out var comV) && comV > 100)
        {
            MessageBox.Show("La comisión de venta no puede superar 100%.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            _txtComision.Focus(); return;
        }
        if (int.TryParse(_txtComCob.Text.Replace(".", ""), out var comC) && comC > 100)
        {
            MessageBox.Show("La comisión de cobranza no puede superar 100%.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            _txtComCob.Focus(); return;
        }

        string Si(bool? v) => v == true ? "SI" : "NO";
        decimal.TryParse(_txtSalario.Text, System.Globalization.NumberStyles.Number, _cult, out var sal);

        var u = new Usuario
        {
            IdUsuario              = _seleccionado?.IdUsuario ?? 0,
            CiUsuario              = _txtCi.Text.Trim(),
            CodigoUsuario          = _txtCodigo.Text.Trim(),
            ContrasenaUsuario      = _txtPass.Password.Trim(),
            NombreUsuario          = _txtNombre.Text.Trim(),
            DireccionUsuario       = _txtDireccion.Text.Trim(),
            TelefonoUsuario        = _txtTelefono.Text.Trim(),
            ZonaUsuario            = ((_cboZona.SelectedValue as int?) ?? 0).ToString(),
            SalarioUsuario         = sal,
            ComisionUsuario        = _txtComision.Text.Trim(),
            ComisionCobranza       = _txtComCob.Text.Trim(),
            CargoUsuario           = _cboCargo.Text,
            LocalUsuario           = (int?)_cboLocal.SelectedValue ?? _session.LocalActual!.IdLocal,
            PermisoDescuento       = Si(_chkDesc.IsChecked),
            PermisoStock           = Si(_chkStock.IsChecked),
            PermisoPrecio          = Si(_chkPrecio.IsChecked),
            PermisoEliminar        = Si(_chkElim.IsChecked),
            PermisoCobrarCuotas    = Si(_chkCuotas.IsChecked),
            PermisoCompras         = Si(_chkCompras.IsChecked),
            PermisoEliminarFactura = Si(_chkFact.IsChecked),
        };

        try
        {
            _btnGuardar.IsEnabled = false;
            if (_seleccionado == null)
                await _repo.GuardarAsync(u);
            else
                await _repo.ActualizarAsync(u);
            await Refrescar();
            MessageBox.Show("Funcionario guardado correctamente.", "OK",
                MessageBoxButton.OK, MessageBoxImage.Information);
            OnNuevo(s, e);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error al guardar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _btnGuardar.IsEnabled = true; }
    }

    private async void OnEliminar(object s, RoutedEventArgs e)
    {
        if (_seleccionado == null) return;
        if (_seleccionado.IdUsuario == _session.UsuarioActual?.IdUsuario)
        { MessageBox.Show("No puede eliminarse a sí mismo.", "Error"); return; }
        if (MessageBox.Show($"¿Eliminar funcionario '{_seleccionado.NombreUsuario}'?", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await _repo.EliminarAsync(_seleccionado.IdUsuario);
        await Refrescar();
        OnNuevo(s, e);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  PROVEEDORES (CRUD completo)
// ═══════════════════════════════════════════════════════════════════
public class ProveedoresWindow : Window
{
    private readonly IMaestrosProveedorRepository _repo;

    private static readonly System.Windows.Media.SolidColorBrush BrPrimary  = new(System.Windows.Media.Color.FromRgb(255, 140,   0));
    private static readonly System.Windows.Media.SolidColorBrush BrPrimDark = new(System.Windows.Media.Color.FromRgb(224, 112,   0));
    private static readonly System.Windows.Media.SolidColorBrush BrFondo    = new(System.Windows.Media.Color.FromRgb(240, 242, 245));
    private static readonly System.Windows.Media.SolidColorBrush BrCard     = System.Windows.Media.Brushes.White;
    private static readonly System.Windows.Media.SolidColorBrush BrBorde    = new(System.Windows.Media.Color.FromRgb(229, 231, 235));
    private static readonly System.Windows.Media.SolidColorBrush BrLabel    = new(System.Windows.Media.Color.FromRgb(107, 114, 128));
    private static readonly System.Windows.Media.SolidColorBrush BrVerde    = new(System.Windows.Media.Color.FromRgb( 22, 163,  74));
    private static readonly System.Windows.Media.SolidColorBrush BrRojo     = new(System.Windows.Media.Color.FromRgb(220,  38,  38));
    private static readonly System.Windows.Media.SolidColorBrush BrGris     = new(System.Windows.Media.Color.FromRgb(107, 114, 128));
    private static readonly System.Windows.Media.SolidColorBrush BrBlanco   = System.Windows.Media.Brushes.White;

    private TextBox _txtCodigo = null!, _txtNombre = null!, _txtRuc = null!,
                    _txtDireccion = null!, _txtTelefono = null!, _txtCiudad = null!,
                    _txtEmail = null!, _txtWeb = null!, _txtContacto = null!,
                    _txtCargo = null!, _txtCelular = null!, _txtCorreo = null!;
    private Button _btnGuardar = null!, _btnEliminar = null!, _btnEditar = null!;
    private Proveedor? _seleccionado;
    private bool _modoEdicion = false;

    public ProveedoresWindow()
    {
        _repo = App.Services.GetRequiredService<IMaestrosProveedorRepository>();
        Title = "Proveedores"; Width = 680; Height = 620;
        MinWidth = 620; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = BrFondo;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // contenido
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header ──────────────────────────────────────────────────────
        var header = new Border { Background = BrPrimary, Padding = new Thickness(14, 10, 14, 10) };
        var headerRoot = new Grid();
        headerRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // icono
        headerRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // titulo+buscar
        headerRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // atajos

        // Icono
        var iconBorder = new Border {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(20),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 255, 255)),
            Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center
        };
        iconBorder.Child = new TextBlock { Text = "🏭", FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(iconBorder, 0);
        headerRoot.Children.Add(iconBorder);

        // Centro: título + buscador + botón Ver Listado
        var centerSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        titleRow.Children.Add(new TextBlock { Text = "Proveedores", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = BrBlanco, VerticalAlignment = VerticalAlignment.Center });
        centerSp.Children.Add(titleRow);

        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        var txtCodBuscar = new TextBox {
            Width = 130, Height = 28, FontSize = 12,
            Padding = new Thickness(6, 3, 6, 3),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = BrBlanco,
        };
        txtCodBuscar.KeyDown += async (_, e) => {
            if (e.Key != Key.Enter) return;
            var cod = txtCodBuscar.Text.Trim();
            if (string.IsNullOrEmpty(cod)) return;
            var todos = (await _repo.ListarTodosAsync()).ToList();
            var found = todos.FirstOrDefault(p => p.CodigoProveedor.Equals(cod, StringComparison.OrdinalIgnoreCase)
                                                || p.NombreProveedor.Contains(cod, StringComparison.OrdinalIgnoreCase));
            if (found != null) CargarProveedorEnForm(found);
            else MessageBox.Show("No se encontró ningún proveedor.", "Buscar");
        };
        var btnBuscarCod = new Button {
            Content = "Buscar", Height = 28, Padding = new Thickness(10, 0, 10, 0),
            Background = BrPrimDark, Foreground = BrBlanco,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, Margin = new Thickness(4, 0, 8, 0)
        };
        btnBuscarCod.Click += async (_, _) => {
            var cod = txtCodBuscar.Text.Trim();
            if (string.IsNullOrEmpty(cod)) return;
            var todos = (await _repo.ListarTodosAsync()).ToList();
            var found = todos.FirstOrDefault(p => p.CodigoProveedor.Equals(cod, StringComparison.OrdinalIgnoreCase)
                                                || p.NombreProveedor.Contains(cod, StringComparison.OrdinalIgnoreCase));
            if (found != null) CargarProveedorEnForm(found);
            else MessageBox.Show("No se encontró ningún proveedor.", "Buscar");
        };
        var btnVerListado = new Button {
            Height = 28, Padding = new Thickness(10, 0, 10, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 255, 255)),
            Foreground = BrBlanco, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(1), BorderBrush = BrBlanco,
            Cursor = Cursors.Hand
        };
        var btnVerListadoSp = new StackPanel { Orientation = Orientation.Horizontal };
        btnVerListadoSp.Children.Add(new System.Windows.Shapes.Path {
            Data = System.Windows.Media.Geometry.Parse("M9.5,3 C6.46,3 4,5.46 4,8.5 C4,11.54 6.46,14 9.5,14 C10.75,14 11.9,13.57 12.83,12.86 L17.5,17.5 L18.5,16.5 L13.86,11.83 C14.57,10.9 15,9.75 15,8.5 C15,5.46 12.54,3 9.5,3 Z M9.5,4.5 C11.71,4.5 13.5,6.29 13.5,8.5 C13.5,10.71 11.71,12.5 9.5,12.5 C7.29,12.5 5.5,10.71 5.5,8.5 C5.5,6.29 7.29,4.5 9.5,4.5 Z"),
            Fill = BrBlanco, Stretch = System.Windows.Media.Stretch.Uniform, Width = 13, Height = 13,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,5,0)
        });
        btnVerListadoSp.Children.Add(new TextBlock { Text = "Ver Listado de Proveedores", VerticalAlignment = VerticalAlignment.Center });
        btnVerListado.Content = btnVerListadoSp;
        btnVerListado.Click += OnVerListado;
        searchRow.Children.Add(txtCodBuscar);
        searchRow.Children.Add(btnBuscarCod);
        searchRow.Children.Add(btnVerListado);
        centerSp.Children.Add(searchRow);
        centerSp.Children.Add(new TextBlock {
            Text = "Código o nombre · Enter para buscar · F2=Ver Listado",
            FontSize = 9.5, Foreground = BrBlanco, Opacity = 0.75, Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(centerSp, 1);
        headerRoot.Children.Add(centerSp);

        // Atajos derecha
        var atajosTb = new TextBlock {
            Text = "F1=Ayuda  F2=Buscar\nCtrl+N=Nuevo  Ctrl+S=Cerrar",
            FontSize = 9, Foreground = BrBlanco, Opacity = 0.75,
            VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right
        };
        Grid.SetColumn(atajosTb, 2);
        headerRoot.Children.Add(atajosTb);

        header.Child = headerRoot;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // ── Contenido ────────────────────────────────────────────────────
        var formScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12, 10, 12, 10) };
        var form = new StackPanel();
        formScroll.Content = form;
        Grid.SetRow(formScroll, 1);
        root.Children.Add(formScroll);

        TextBox Txt(int max = 100) => new TextBox {
            Padding = new Thickness(8, 6, 8, 6), MaxLength = max, FontSize = 12,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Background = BrBlanco, IsReadOnly = true
        };

        // Sección empresa
        var card1 = new Border { Background = BrCard, BorderBrush = BrBorde, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 12, 14, 14), Margin = new Thickness(0, 0, 0, 8) };
        var formEmpresa = new StackPanel();
        _txtCodigo    = Txt(20); _txtNombre = Txt(100); _txtRuc = Txt(20);
        _txtDireccion = Txt(150); _txtTelefono = Txt(30); _txtCiudad = Txt(60);
        _txtEmail = Txt(100); _txtWeb = Txt(100);

        formEmpresa.Children.Add(new Border { Background = BrPrimary, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 0, 10), Child = new TextBlock { Text = "DATOS DE LA EMPRESA", Foreground = BrBlanco, FontWeight = FontWeights.Bold, FontSize = 11 } });
        var gCodRuc = new Grid { Margin = new Thickness(0, 4, 0, 10) };
        gCodRuc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gCodRuc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        gCodRuc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var spCod = new StackPanel(); spCod.Children.Add(new TextBlock { Text = "Código", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrLabel, Margin = new Thickness(0,0,0,3) }); spCod.Children.Add(_txtCodigo);
        var spRuc = new StackPanel(); spRuc.Children.Add(new TextBlock { Text = "RUC", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrLabel, Margin = new Thickness(0,0,0,3) }); spRuc.Children.Add(_txtRuc);
        Grid.SetColumn(spCod, 0); Grid.SetColumn(spRuc, 2);
        gCodRuc.Children.Add(spCod); gCodRuc.Children.Add(spRuc);
        formEmpresa.Children.Add(gCodRuc);

        void FE(string lbl, TextBox tb, bool last = false) {
            formEmpresa.Children.Add(new TextBlock { Text = lbl, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrLabel, Margin = new Thickness(0,0,0,3) });
            tb.Margin = new Thickness(0, 0, 0, last ? 0 : 8); formEmpresa.Children.Add(tb);
        }
        void FERow(string l1, TextBox t1, string l2, TextBox t2) {
            var g = new Grid { Margin = new Thickness(0,0,0,8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var s1 = new StackPanel(); s1.Children.Add(new TextBlock { Text = l1, FontSize=11, FontWeight=FontWeights.SemiBold, Foreground=BrLabel, Margin=new Thickness(0,0,0,3) }); s1.Children.Add(t1);
            var s2 = new StackPanel(); s2.Children.Add(new TextBlock { Text = l2, FontSize=11, FontWeight=FontWeights.SemiBold, Foreground=BrLabel, Margin=new Thickness(0,0,0,3) }); s2.Children.Add(t2);
            Grid.SetColumn(s1, 0); Grid.SetColumn(s2, 2); g.Children.Add(s1); g.Children.Add(s2); formEmpresa.Children.Add(g);
        }
        FE("Nombre / Razón Social *", _txtNombre);
        FE("Dirección", _txtDireccion);
        FERow("Teléfono", _txtTelefono, "Ciudad", _txtCiudad);
        FERow("E-mail", _txtEmail, "Web", _txtWeb);
        card1.Child = formEmpresa;
        form.Children.Add(card1);

        // Sección contacto
        var card2 = new Border { Background = BrCard, BorderBrush = BrBorde, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 12, 14, 14) };
        var formContacto = new StackPanel();
        _txtContacto = Txt(80); _txtCargo = Txt(60); _txtCelular = Txt(20); _txtCorreo = Txt(100);

        formContacto.Children.Add(new Border { Background = BrPrimary, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 0, 10), Child = new TextBlock { Text = "DATOS DEL CONTACTO", Foreground = BrBlanco, FontWeight = FontWeights.Bold, FontSize = 11 } });
        void FCRow(string l1, TextBox t1, string l2, TextBox t2, bool last = false) {
            var g = new Grid { Margin = new Thickness(0,0,0, last ? 0 : 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var s1 = new StackPanel(); s1.Children.Add(new TextBlock { Text = l1, FontSize=11, FontWeight=FontWeights.SemiBold, Foreground=BrLabel, Margin=new Thickness(0,0,0,3) }); s1.Children.Add(t1);
            var s2 = new StackPanel(); s2.Children.Add(new TextBlock { Text = l2, FontSize=11, FontWeight=FontWeights.SemiBold, Foreground=BrLabel, Margin=new Thickness(0,0,0,3) }); s2.Children.Add(t2);
            Grid.SetColumn(s1, 0); Grid.SetColumn(s2, 2); g.Children.Add(s1); g.Children.Add(s2); formContacto.Children.Add(g);
        }
        FCRow("Nombre", _txtContacto, "Cargo", _txtCargo);
        FCRow("Celular", _txtCelular, "Correo", _txtCorreo, last: true);
        card2.Child = formContacto;
        form.Children.Add(card2);

        // ── Footer ───────────────────────────────────────────────────────
        var footer = new Border { Background = BrCard, BorderBrush = BrBorde, BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(12, 8, 12, 8) };
        var footerSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnVista  = MakeBtn("Vista",   BrGris);    btnVista.Click   += (_, _) => HabilitarEdicion(false);
        var btnNuevo  = MakeBtn("Nuevo",   BrPrimary); btnNuevo.Click   += OnNuevo;
        _btnEditar    = MakeBtn("Editar",  BrPrimary); _btnEditar.Click += (_, _) => HabilitarEdicion(true); _btnEditar.IsEnabled = false;
        _btnGuardar   = MakeBtn("Guardar", BrVerde);   _btnGuardar.Click += OnGuardar; _btnGuardar.IsEnabled = false;
        _btnEliminar  = MakeBtn("Eliminar",BrRojo);    _btnEliminar.Click += OnEliminar; _btnEliminar.IsEnabled = false;
        var btnCerrar = MakeBtn("Cerrar",  BrGris);    btnCerrar.Click += (_, _) => Close();
        foreach (var b in new[] { btnVista, btnNuevo, _btnEditar, _btnGuardar, _btnEliminar, btnCerrar })
            footerSp.Children.Add(b);
        footer.Child = footerSp;
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
    }

    private static Button MakeBtn(string text, System.Windows.Media.SolidColorBrush bg) => new Button {
        Content = text, Height = 34, Padding = new Thickness(18, 0, 18, 0), Margin = new Thickness(0, 0, 8, 0),
        Background = bg, Foreground = System.Windows.Media.Brushes.White,
        FontWeight = FontWeights.SemiBold, FontSize = 13, BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand
    };

    private void HabilitarEdicion(bool habilitar)
    {
        _modoEdicion = habilitar;
        foreach (var t in new[] { _txtCodigo, _txtNombre, _txtRuc, _txtDireccion, _txtTelefono,
                                   _txtCiudad, _txtEmail, _txtWeb, _txtContacto, _txtCargo, _txtCelular, _txtCorreo })
            t.IsReadOnly = !habilitar;
        _btnGuardar.IsEnabled  = habilitar;
        _btnEditar.IsEnabled   = !habilitar && _seleccionado != null;
        _btnEliminar.IsEnabled = !habilitar && _seleccionado != null;
    }

    private void CargarProveedorEnForm(Proveedor p)
    {
        _seleccionado      = p;
        _txtCodigo.Text    = p.CodigoProveedor;
        _txtNombre.Text    = p.NombreProveedor;
        _txtRuc.Text       = p.RucProveedor;
        _txtDireccion.Text = p.DireccionProveedor;
        _txtTelefono.Text  = p.TelefonoProveedor;
        _txtCiudad.Text    = p.CiudadProveedor;
        _txtEmail.Text     = p.EmailProveedor;
        _txtWeb.Text       = p.WebProveedor;
        _txtContacto.Text  = p.ContactoNombre;
        _txtCargo.Text     = p.ContactoCargo;
        _txtCelular.Text   = p.ContactoCelular;
        _txtCorreo.Text    = p.ContactoCorreo ?? "";
        HabilitarEdicion(false);
        _btnEditar.IsEnabled   = true;
        _btnEliminar.IsEnabled = true;
    }

    private void OnVerListado(object s, RoutedEventArgs e)
    {
        var modal = new BuscadorProveedorModal(_repo) { Owner = this };
        if (modal.ShowDialog() != true || modal.ProveedorSeleccionado == null) return;
        CargarProveedorEnForm(modal.ProveedorSeleccionado);
    }

    private void OnNuevo(object s, RoutedEventArgs e)
    {
        _seleccionado = null;
        foreach (var t in new[] { _txtCodigo, _txtNombre, _txtRuc, _txtDireccion, _txtTelefono,
                                   _txtCiudad, _txtEmail, _txtWeb, _txtContacto, _txtCargo, _txtCelular, _txtCorreo })
            t.Text = "";
        HabilitarEdicion(true);
        _txtCodigo.Focus();
    }

    private async void OnGuardar(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtNombre.Text))
        { MessageBox.Show("El nombre es obligatorio.", "Error"); return; }
        try
        {
            _btnGuardar.IsEnabled = false;
            var prov = new Proveedor
            {
                IdProveedor        = _seleccionado?.IdProveedor ?? 0,
                CodigoProveedor    = _txtCodigo.Text.Trim(),
                NombreProveedor    = _txtNombre.Text.Trim(),
                RucProveedor       = _txtRuc.Text.Trim(),
                DireccionProveedor = _txtDireccion.Text.Trim(),
                TelefonoProveedor  = _txtTelefono.Text.Trim(),
                CiudadProveedor    = _txtCiudad.Text.Trim(),
                EmailProveedor     = _txtEmail.Text.Trim(),
                WebProveedor       = _txtWeb.Text.Trim(),
                ContactoNombre     = _txtContacto.Text.Trim(),
                ContactoCargo      = _txtCargo.Text.Trim(),
                ContactoCelular    = _txtCelular.Text.Trim(),
                ContactoCorreo     = _txtCorreo.Text.Trim(),
            };
            if (_seleccionado == null)
                await _repo.InsertarCompletoAsync(prov);
            else
                await _repo.ActualizarCompletoAsync(prov);
            MessageBox.Show("Proveedor guardado.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            OnNuevo(s, e);
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { _btnGuardar.IsEnabled = _modoEdicion; }
    }

    private async void OnEliminar(object s, RoutedEventArgs e)
    {
        if (_seleccionado == null) return;
        if (MessageBox.Show($"¿Eliminar proveedor '{_seleccionado.NombreProveedor}'?", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await _repo.EliminarAsync(_seleccionado.IdProveedor);
        OnNuevo(s, e);
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == System.Windows.Input.Key.F5)
            OnGuardar(this, new RoutedEventArgs());
        else if (e.Key == System.Windows.Input.Key.S && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            Close();
        else if (e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.F4)
            HabilitarEdicion(false);
        else if (e.Key == System.Windows.Input.Key.N && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            OnNuevo(this, new RoutedEventArgs());
        else if (e.Key == System.Windows.Input.Key.F2)
            OnVerListado(this, new RoutedEventArgs());
        else if (e.Key == System.Windows.Input.Key.F3)
            HabilitarEdicion(true);
        else if (e.Key == System.Windows.Input.Key.F6)
            OnEliminar(this, new RoutedEventArgs());
    }
}

public class BuscadorProveedorModal : Window
{
    private readonly IMaestrosProveedorRepository _repo;
    private TextBox  _txtBuscar = null!;
    private DataGrid _grid      = null!;
    private List<Proveedor> _todos = new();

    public Proveedor? ProveedorSeleccionado { get; private set; }

    public BuscadorProveedorModal(IMaestrosProveedorRepository repo)
    {
        _repo  = repo;
        Title  = "Listado de Proveedores";
        Width  = 720; Height = 500;
        MinWidth = 560; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 242, 245));
        BuildUI();
        Loaded += async (_, _) => await CargarAsync();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header con buscador
        var headerBg = new Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0)),
            Padding = new Thickness(12, 10, 12, 10)
        };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "Código o Nombre:",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0)
        });
        _txtBuscar = new TextBox {
            Width = 280, Height = 30, FontSize = 13,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = System.Windows.Media.Brushes.White,
        };
        _txtBuscar.TextChanged += (_, _) => Filtrar();
        _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Seleccionar(); };
        headerSp.Children.Add(_txtBuscar);
        headerBg.Child = headerSp;
        Grid.SetRow(headerBg, 0); root.Children.Add(headerBg);

        // DataGrid
        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)),
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251)),
            FontSize = 12, Margin = new Thickness(8, 6, 8, 0),
            ColumnHeaderStyle = BuildHeaderStyle()
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Código",            Binding = new System.Windows.Data.Binding("CodigoProveedor"),  Width = new DataGridLength(90) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Nombre / Razón Social", Binding = new System.Windows.Data.Binding("NombreProveedor"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "RUC",               Binding = new System.Windows.Data.Binding("RucProveedor"),      Width = new DataGridLength(110) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Teléfono",          Binding = new System.Windows.Data.Binding("TelefonoProveedor"), Width = new DataGridLength(100) });
        _grid.MouseDoubleClick += (_, _) => Seleccionar();
        Grid.SetRow(_grid, 1); root.Children.Add(_grid);

        // Botones
        var barBtns = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)),
            BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(8)
        };
        var barGrid = new Grid();
        barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Button MkBtn(string txt, string hex) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(6, 0, 0, 0), FontWeight = FontWeights.SemiBold,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnSelec  = MkBtn("Seleccionar", "#22C55E");
        var btnCerrar = MkBtn("Cerrar",       "#6B7280");
        btnSelec.Click  += (_, _) => Seleccionar();
        btnCerrar.Click += (_, _) => Close();

        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btnsSp.Children.Add(btnSelec);
        btnsSp.Children.Add(btnCerrar);
        Grid.SetColumn(btnsSp, 1);
        barGrid.Children.Add(btnsSp);
        barBtns.Child = barGrid;
        Grid.SetRow(barBtns, 2); root.Children.Add(barBtns);

        Content = root;
        Loaded += (_, _) => _txtBuscar.Focus();
    }

    private static Style BuildHeaderStyle()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0))));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10, 0, 10, 0)));
        s.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0)));
        return s;
    }

    private async Task CargarAsync()
    {
        _todos = (await _repo.ListarTodosAsync()).ToList();
        _grid.ItemsSource = _todos;
    }

    private void Filtrar()
    {
        var txt = _txtBuscar.Text.Trim();
        _grid.ItemsSource = string.IsNullOrEmpty(txt)
            ? _todos
            : _todos.Where(p => p.NombreProveedor.Contains(txt, StringComparison.OrdinalIgnoreCase)
                             || p.CodigoProveedor.Contains(txt, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void Seleccionar()
    {
        if (_grid.SelectedItem is not Proveedor p) return;
        ProveedorSeleccionado = p;
        DialogResult = true;
        Close();
    }
}

// ═══════════════════════════════════════════════════════════════════
//  LOCALES / SUCURSALES (CRUD — solo Administrador)
// ═══════════════════════════════════════════════════════════════════
public class LocalesWindow : Window
{
    private readonly ILocalRepository _repo;

    private DataGrid _grid = null!;
    private TextBox _txtCodigo = null!, _txtNombre = null!, _txtDireccion = null!,
                    _txtCiudad = null!, _txtTelefono = null!;
    private Button _btnGuardar = null!, _btnEliminar = null!;
    private Local? _seleccionado;

    public LocalesWindow()
    {
        _repo = App.Services.GetRequiredService<ILocalRepository>();
        Title = "Locales / Sucursales"; Width = 800; Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await Refrescar();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var form = new StackPanel { Margin = new Thickness(12) };
        void Add(string lbl, UIElement ctrl) {
            form.Children.Add(new TextBlock { Text = lbl, FontSize = 11, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 6, 0, 1) });
            form.Children.Add(ctrl);
        }
        TextBox Txt(int max = 100) => new TextBox { Padding = new Thickness(4, 3, 4, 3), MaxLength = max };

        _txtCodigo   = Txt(5);   Add("Código / ID", _txtCodigo);
        _txtNombre   = Txt(100); Add("Nombre *",    _txtNombre);
        _txtDireccion= Txt(100); Add("Dirección",   _txtDireccion);
        _txtCiudad   = Txt(100); Add("Ciudad",      _txtCiudad);
        _txtTelefono = Txt(50);  Add("Teléfono",    _txtTelefono);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var btnNuevo  = Btn("Nuevo",   "#FF8C00"); btnNuevo.Click   += OnNuevo;
        _btnGuardar   = Btn("Guardar", "#27AE60"); _btnGuardar.Click += OnGuardar;
        _btnEliminar  = Btn("Eliminar","#E74C3C"); _btnEliminar.Click += OnEliminar; _btnEliminar.IsEnabled = false;
        var btnCerrar = Btn("Cerrar",  "#757575"); btnCerrar.Click  += (_, _) => Close();
        foreach (var b in new[] { btnNuevo, _btnGuardar, _btnEliminar, btnCerrar })
            btnPanel.Children.Add(b);
        form.Children.Add(btnPanel);

        Grid.SetColumn(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 0);
        var scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(scroll, 0);
        root.Children.Add(scroll);

        var rightPanel = new DockPanel { Margin = new Thickness(8) };
        var hdr = new Border { Background = System.Windows.Media.Brushes.DarkOrange, Padding = new Thickness(8, 4, 8, 4) };
        hdr.Child = new TextBlock { Text = "Lista de Locales", Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(hdr, Dock.Top); rightPanel.Children.Add(hdr);

        _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.FloralWhite, Margin = new Thickness(0, 4, 0, 0) };
        _grid.Columns.Add(new DataGridTextColumn { Header = "ID",        Binding = new System.Windows.Data.Binding("IdLocal"),     Width = 40 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Nombre",    Binding = new System.Windows.Data.Binding("NombreLocal"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Ciudad",    Binding = new System.Windows.Data.Binding("CiudadLocal"), Width = 110 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Teléfono",  Binding = new System.Windows.Data.Binding("TelefonoLocal"), Width = 100 });
        _grid.SelectionChanged += OnSeleccion;
        rightPanel.Children.Add(_grid);
        Grid.SetColumn(rightPanel, 1); root.Children.Add(rightPanel);
        Content = root;
    }

    private Button Btn(string text, string hex) => new Button {
        Content = text, Width = 72, Height = 30, Margin = new Thickness(0, 0, 6, 0),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White
    };

    private async Task Refrescar() { _grid.ItemsSource = (await _repo.ListarTodosAsync()).ToList(); }

    private void OnSeleccion(object s, SelectionChangedEventArgs e)
    {
        if (_grid.SelectedItem is not Local l) return;
        _seleccionado = l;
        _txtCodigo.Text    = l.IdLocal.ToString();
        _txtNombre.Text    = l.NombreLocal;
        _txtDireccion.Text = l.DireccionLocal;
        _txtCiudad.Text    = l.CiudadLocal;
        _txtTelefono.Text  = l.TelefonoLocal;
        _btnEliminar.IsEnabled = true;
    }

    private void OnNuevo(object s, RoutedEventArgs e)
    {
        _seleccionado = null; _grid.SelectedItem = null;
        _txtCodigo.Text = _txtNombre.Text = _txtDireccion.Text = _txtCiudad.Text = _txtTelefono.Text = "";
        _btnEliminar.IsEnabled = false; _txtNombre.Focus();
    }

    private async void OnGuardar(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtNombre.Text))
        { MessageBox.Show("El nombre es obligatorio.", "Error"); return; }
        try
        {
            _btnGuardar.IsEnabled = false;
            var l = new Local {
                IdLocal       = _seleccionado?.IdLocal ?? 0,
                NombreLocal   = _txtNombre.Text.Trim(),
                DireccionLocal= _txtDireccion.Text.Trim(),
                CiudadLocal   = _txtCiudad.Text.Trim(),
                TelefonoLocal = _txtTelefono.Text.Trim(),
            };
            if (_seleccionado == null) await _repo.GuardarAsync(l);
            else await _repo.ActualizarAsync(l);
            await Refrescar();
            MessageBox.Show("Local guardado.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            OnNuevo(s, e);
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { _btnGuardar.IsEnabled = true; }
    }

    private async void OnEliminar(object s, RoutedEventArgs e)
    {
        if (_seleccionado == null) return;
        if (MessageBox.Show($"¿Eliminar local '{_seleccionado.NombreLocal}'?", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await _repo.EliminarAsync(_seleccionado.IdLocal);
        await Refrescar(); OnNuevo(s, e);
    }
}
