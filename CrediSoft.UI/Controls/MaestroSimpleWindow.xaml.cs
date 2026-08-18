using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;

namespace CrediSoft.UI.Controls;

public abstract partial class MaestroSimpleWindow : Window
{
    protected bool ModoEdicion;

    // Alta rápida: se usa cuando esta ventana se abre desde un selector con lupa
    // (p.ej. el "+ Nuevo" de Sección/Categoría/Marca/etc. en Nuevo Artículo) — arranca
    // directo en modo "Nuevo", oculta buscador/listado/vista/editar/eliminar, y al
    // guardar cierra la ventana devolviendo el registro creado en vez de limpiar
    // el formulario para seguir cargando más registros.
    protected bool ModoAltaRapida { get; private set; }
    public MaestroItem? ItemCreado { get; private set; }

    protected MaestroSimpleWindow(bool modoAltaRapida = false)
    {
        InitializeComponent();
        ModoAltaRapida = modoAltaRapida;
        Loaded += (_, _) =>
        {
            if (FindName("TxtTituloHeader") is System.Windows.Controls.TextBlock tb)
                tb.Text = Title;
            if (ModoAltaRapida) AplicarModoAltaRapida();
        };
    }

    private void AplicarModoAltaRapida()
    {
        if (FindName("BuscarHeaderPanel") is UIElement buscarPanel) buscarPanel.Visibility = Visibility.Collapsed;
        if (FindName("PanelGridDatos") is UIElement gridPanel) gridPanel.Visibility = Visibility.Collapsed;
        if (FindName("BtnVistaPrevia") is UIElement btnVP) btnVP.Visibility = Visibility.Collapsed;
        if (FindName("BtnImprimir") is UIElement btnImp) btnImp.Visibility = Visibility.Collapsed;
        if (FindName("BtnImprimirTodo") is UIElement btnImpT) btnImpT.Visibility = Visibility.Collapsed;
        if (FindName("BtnNuevo") is UIElement btnN) btnN.Visibility = Visibility.Collapsed;
        if (FindName("BtnEditar") is UIElement btnE) btnE.Visibility = Visibility.Collapsed;
        if (FindName("BtnEliminar") is UIElement btnEl) btnEl.Visibility = Visibility.Collapsed;
        OnNuevo(this, new RoutedEventArgs());
    }

    // ── Helpers genéricos para ventanas de maestros simples sin repo propio ───

    private IMaestroGenericoRepository GenRepo =>
        App.Services.GetRequiredService<IMaestroGenericoRepository>();

    protected Task<IEnumerable<MaestroItem>> GenericList(
        string tabla, string colId, string colCod, string colNom, string? filtro)
        => GenRepo.ListarAsync(tabla, colId, colCod, colNom, filtro);

    protected Task GenericInsert(string tabla, string colCod, string colNom, string c, string n)
        => GenRepo.InsertarAsync(tabla, colCod, colNom, c, n);

    protected Task GenericUpdate(
        string tabla, string colId, string colCod, string colNom, int id, string c, string n)
        => GenRepo.ActualizarAsync(tabla, colId, colCod, colNom, id, c, n);

    protected Task GenericDelete(string tabla, string colId, int id)
        => GenRepo.EliminarAsync(tabla, colId, id);

    protected abstract Task<IEnumerable<MaestroItem>> CargarDatosAsync(string? filtro = null);
    protected abstract Task GuardarAsync(string codigo, string nombre);
    protected abstract Task ActualizarAsync(int id, string codigo, string nombre);
    protected abstract Task EliminarAsync(int id);

    protected int? ItemSeleccionadoId;
    private List<MaestroItem> _todosItems = new();

    protected async void OnBuscar(object s, RoutedEventArgs e) => await Refrescar();
    protected async void OnBuscarKeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) await Refrescar(); }

    protected void OnBuscarTextChanged(object s, System.Windows.Controls.TextChangedEventArgs e)
    {
        var txt = ((System.Windows.Controls.TextBox)FindName("TxtBuscar")).Text.Trim();
        var grid = (System.Windows.Controls.DataGrid)FindName("GridDatos");
        if (string.IsNullOrEmpty(txt))
            grid.ItemsSource = _todosItems;
        else
            grid.ItemsSource = _todosItems
                .Where(x => x.Nombre.Contains(txt, StringComparison.OrdinalIgnoreCase)
                         || x.Codigo.Contains(txt, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }

    protected async Task Refrescar()
    {
        var grid = (System.Windows.Controls.DataGrid)FindName("GridDatos");
        _todosItems = (await CargarDatosAsync()).ToList();
        var buscar = (System.Windows.Controls.TextBox)FindName("TxtBuscar");
        var txt = buscar.Text.Trim();
        grid.ItemsSource = string.IsNullOrEmpty(txt)
            ? _todosItems
            : _todosItems.Where(x => x.Nombre.Contains(txt, StringComparison.OrdinalIgnoreCase)
                                  || x.Codigo.Contains(txt, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    protected void OnGridSelectionChanged(object s, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var grid = (System.Windows.Controls.DataGrid)FindName("GridDatos");
        if (grid.SelectedItem is MaestroItem item)
        {
            ItemSeleccionadoId = item.Id;
            ((System.Windows.Controls.TextBox)FindName("TxtCodigo")).Text = item.Codigo;
            ((System.Windows.Controls.TextBox)FindName("TxtNombre")).Text = item.Nombre;
            ((System.Windows.Controls.Button)FindName("BtnEditar")).IsEnabled = true;
            ((System.Windows.Controls.Button)FindName("BtnEliminar")).IsEnabled = true;
        }
    }

    protected void HabilitarEdicion(bool habilitar)
    {
        ModoEdicion = habilitar;
        ((System.Windows.Controls.TextBox)FindName("TxtCodigo")).IsReadOnly = !habilitar;
        ((System.Windows.Controls.TextBox)FindName("TxtNombre")).IsReadOnly = !habilitar;
        if (FindName("BtnGuardarSimple") is System.Windows.Controls.Button btnG)
            btnG.IsEnabled = habilitar;
        if (FindName("BtnEditar") is System.Windows.Controls.Button btnE)
            btnE.IsEnabled = !habilitar && ItemSeleccionadoId.HasValue;
    }

    protected void OnNuevo(object s, RoutedEventArgs e)
    {
        ((System.Windows.Controls.TextBox)FindName("TxtCodigo")).Text = "";
        ((System.Windows.Controls.TextBox)FindName("TxtNombre")).Text = "";
        ItemSeleccionadoId = null;
        HabilitarEdicion(true);
        ((System.Windows.Controls.TextBox)FindName("TxtCodigo")).Focus();
    }

    protected void OnEditar(object s, RoutedEventArgs e) { HabilitarEdicion(true); }
    protected void OnVista(object s, RoutedEventArgs e) { HabilitarEdicion(false); }

    protected void OnVistaPrevia(object s, RoutedEventArgs e)  => AbrirMaestroPrevia(imprimir: false, todo: false);
    protected void OnImprimir(object s, RoutedEventArgs e)     => AbrirMaestroPrevia(imprimir: true,  todo: false);
    protected void OnImprimirTodo(object s, RoutedEventArgs e) => AbrirMaestroPrevia(imprimir: true,  todo: true);

    private void AbrirMaestroPrevia(bool imprimir, bool todo)
    {
        var grid    = (System.Windows.Controls.DataGrid)FindName("GridDatos");
        var buscar  = (System.Windows.Controls.TextBox)FindName("TxtBuscar");

        // todo=true usa lista completa; todo=false usa lo visible en el grid
        var fuente = todo
            ? _todosItems
            : (grid.ItemsSource as IEnumerable<MaestroItem>)?.ToList() ?? _todosItems;

        if (fuente.Count == 0) return;

        var filtroTxt = buscar.Text.Trim();
        var pagina = new CrediSoft.UI.Views.Maestros.MaestroSimplePagina
        {
            Titulo   = Title,
            Filas    = fuente.Select(x => new CrediSoft.UI.Views.Maestros.FilaMaestroSimple(x.Codigo, x.Nombre)).ToList(),
            Filtro   = string.IsNullOrEmpty(filtroTxt) ? "" : $"Búsqueda: {filtroTxt}",
            FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "",
            LogoPath = CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath(),
        };

        if (imprimir)
            CrediSoft.UI.Views.Maestros.MaestroSimpleImpresora.Imprimir(pagina, this);
        else
            new CrediSoft.UI.Views.Maestros.MaestroSimplePreviewWindow(pagina) { Owner = this }.ShowDialog();
    }

    protected void OnGuardarClick(object s, RoutedEventArgs e) => OnGuardar();

    protected async void OnGuardar()
    {
        try
        {
            var cod = ((System.Windows.Controls.TextBox)FindName("TxtCodigo")).Text.Trim();
            var nom = ((System.Windows.Controls.TextBox)FindName("TxtNombre")).Text.Trim();
            if (string.IsNullOrEmpty(cod) || string.IsNullOrEmpty(nom))
            { MessageBox.Show("Complete los campos obligatorios."); return; }

            var txtBuscar = (System.Windows.Controls.TextBox)FindName("TxtBuscar");
            var grid      = (System.Windows.Controls.DataGrid)FindName("GridDatos");

            if (ItemSeleccionadoId.HasValue)
            {
                await ActualizarAsync(ItemSeleccionadoId.Value, cod, nom);
                HabilitarEdicion(false);
                txtBuscar.Text = "";
                await Refrescar();
                MessageBox.Show("Actualizado correctamente.", "OK");
            }
            else
            {
                await GuardarAsync(cod, nom);

                if (ModoAltaRapida)
                {
                    var creados = (await CargarDatosAsync()).ToList();
                    ItemCreado = creados.FirstOrDefault(x => x.Codigo == cod && x.Nombre == nom)
                              ?? creados.LastOrDefault(x => x.Nombre == nom);
                    DialogResult = true;
                    return;
                }

                txtBuscar.Text = "";
                await Refrescar();
                ((System.Windows.Controls.TextBox)FindName("TxtCodigo")).Text = "";
                ((System.Windows.Controls.TextBox)FindName("TxtNombre")).Text = "";
                ItemSeleccionadoId = null;
                grid.SelectedItem = null;
                HabilitarEdicion(false);
                ((System.Windows.Controls.TextBox)FindName("TxtCodigo")).Focus();
                MessageBox.Show("Guardado correctamente.", "OK");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected virtual bool RequiereAdminParaEliminar => false;

    protected async void OnEliminar(object s, RoutedEventArgs e)
    {
        if (!ItemSeleccionadoId.HasValue) return;
        if (MessageBox.Show("¿Eliminar este registro?", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (RequiereAdminParaEliminar && !VerificarAdministrador()) return;
        try
        {
            await EliminarAsync(ItemSeleccionadoId.Value);
            await Refrescar();
            MessageBox.Show("Registro eliminado correctamente.", "Eliminado",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            // Buscar el mensaje más descriptivo en la cadena de excepciones
            var inner = ex.InnerException ?? ex;
            var rawMsg = inner.Message;

            string msg;
            if (rawMsg.Equals("Error", StringComparison.OrdinalIgnoreCase)
             || rawMsg.Contains("violation", StringComparison.OrdinalIgnoreCase)
             || rawMsg.Contains("foreign", StringComparison.OrdinalIgnoreCase)
             || rawMsg.Contains("referenc", StringComparison.OrdinalIgnoreCase)
             || rawMsg.Contains("integrity", StringComparison.OrdinalIgnoreCase))
            {
                msg = "No se puede eliminar este registro porque está siendo\nutilizado en otros datos del sistema (artículos, ventas, etc.).";
            }
            else if (string.IsNullOrWhiteSpace(rawMsg) || rawMsg.Length <= 5)
            {
                msg = "No se puede eliminar este registro. Es posible que esté\nvinculado a otros datos del sistema.";
            }
            else
            {
                msg = rawMsg;
            }

            MessageBox.Show(msg, "No se puede eliminar",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool VerificarAdministrador()
    {
        // Buscar todos los usuarios admin en background ANTES de abrir el diálogo
        var usuarios = Task.Run(async () => await App.Services
            .GetRequiredService<IUsuarioRepository>().ListarTodosAsync())
            .GetAwaiter().GetResult();

        var aprobado = false;

        System.Windows.Controls.TextBox     txtCodigo = null!;
        System.Windows.Controls.PasswordBox txtPass   = null!;
        System.Windows.Controls.TextBlock   lblError  = null!;
        System.Windows.Controls.Button      btnOk     = null!;

        var dlg = new Window {
            Title = "Acceso restringido", Width = 340,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, ResizeMode = ResizeMode.NoResize,
            Background = System.Windows.Media.Brushes.White
        };

        var header = new System.Windows.Controls.Border {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0x70, 0x00)),
            Padding = new Thickness(16, 12, 16, 12),
            Child = new System.Windows.Controls.TextBlock {
                Text = "Ingrese contraseña de administrador",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            }
        };

        txtCodigo = new System.Windows.Controls.TextBox { Padding = new Thickness(6, 4, 6, 4) };
        txtPass   = new System.Windows.Controls.PasswordBox { Padding = new Thickness(6, 4, 6, 4) };
        lblError  = new System.Windows.Controls.TextBlock {
            Foreground = System.Windows.Media.Brushes.Red, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed
        };

        static System.Windows.Controls.TextBlock Lbl(string t) => new() {
            Text = t, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0x40, 0x00)),
            Margin = new Thickness(0, 8, 0, 2)
        };
        var body = new System.Windows.Controls.StackPanel { Margin = new Thickness(16, 4, 16, 8) };
        body.Children.Add(Lbl("Código de usuario")); body.Children.Add(txtCodigo);
        body.Children.Add(Lbl("Contraseña"));        body.Children.Add(txtPass);
        body.Children.Add(lblError);

        btnOk = new System.Windows.Controls.Button {
            Content = "Confirmar", Width = 100, Height = 32, Margin = new Thickness(0, 0, 8, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0x70, 0x00)),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnCancelar = new System.Windows.Controls.Button {
            Content = "Cancelar", Width = 80, Height = 32,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x75, 0x75, 0x75)),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        btnCancelar.Click += (_, _) => dlg.Close();

        // Verificación 100% síncrona contra la lista ya cargada
        void Verificar() {
            lblError.Visibility = Visibility.Collapsed;
            var codigo = txtCodigo.Text.Trim();
            var pass   = txtPass.Password;
            var u = usuarios.FirstOrDefault(x =>
                x.CodigoUsuario.Equals(codigo, StringComparison.OrdinalIgnoreCase));
            if (u == null || u.ContrasenaUsuario != pass) {
                lblError.Text = "Código o contraseña incorrectos.";
                lblError.Visibility = Visibility.Visible;
                txtPass.Clear(); txtPass.Focus(); return;
            }
            if (!u.EsAdministrador) {
                lblError.Text = "El usuario no tiene privilegio de Administrador.";
                lblError.Visibility = Visibility.Visible;
                txtPass.Clear(); txtPass.Focus(); return;
            }
            aprobado = true;
            dlg.Close();
        }

        btnOk.Click       += (_, _) => Verificar();
        txtPass.KeyDown   += (_, e) => { if (e.Key == Key.Enter) Verificar(); };
        txtCodigo.KeyDown += (_, e) => { if (e.Key == Key.Enter) txtPass.Focus(); };

        var btnsSp = new System.Windows.Controls.StackPanel {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 10, 16, 16)
        };
        btnsSp.Children.Add(btnOk); btnsSp.Children.Add(btnCancelar);

        var root = new System.Windows.Controls.StackPanel();
        root.Children.Add(header); root.Children.Add(body);
        root.Children.Add(new System.Windows.Controls.Border {
            Height = 1, Margin = new Thickness(0, 4, 0, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD))
        });
        root.Children.Add(btnsSp);
        dlg.Content = root;
        dlg.Loaded += (_, _) => txtCodigo.Focus();
        dlg.ShowDialog();
        return aprobado;
    }

    protected void OnCerrar(object s, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F5)
            OnGuardar();
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            Close();
        else if (e.Key == Key.Escape || e.Key == Key.F4)
            HabilitarEdicion(false);
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            OnNuevo(this, new RoutedEventArgs());
        else if (e.Key == Key.F2)
            ((System.Windows.Controls.TextBox)FindName("TxtBuscar")).Focus();
        else if (e.Key == Key.F3)
            OnEditar(this, new RoutedEventArgs());
        else if (e.Key == Key.F6)
            OnEliminar(this, new RoutedEventArgs());
    }
}
