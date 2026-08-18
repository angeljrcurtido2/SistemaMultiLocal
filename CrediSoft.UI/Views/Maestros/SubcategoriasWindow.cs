using CrediSoft.Core.Models;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Maestros;

public class SubcategoriasWindow : Window
{
    private readonly IMaestrosSubcategoriaRepository _repo;
    private readonly IMaestrosCategoriaRepository _categorias;

    private TextBox _txtBuscar = null!;
    private TextBox _txtCodigo = null!;
    private TextBox _txtNombre = null!;
    private ComboBox _cboCategoria = null!;
    private DataGrid _grid = null!;
    private Button _btnEditar = null!;
    private Button _btnEliminar = null!;
    private Subcategoria? _seleccionada;

    // Alta rápida: se abre desde el "+ Nuevo" del selector de Subcategoría en Nuevo
    // Artículo — arranca en modo Nuevo, oculta buscador/listado/editar/eliminar, y al
    // guardar cierra la ventana devolviendo la subcategoría creada.
    private readonly bool _modoAltaRapida;
    public Subcategoria? SubcategoriaCreada { get; private set; }

    public SubcategoriasWindow(bool modoAltaRapida = false)
    {
        _repo = App.Services.GetRequiredService<IMaestrosSubcategoriaRepository>();
        _categorias = App.Services.GetRequiredService<IMaestrosCategoriaRepository>();
        _modoAltaRapida = modoAltaRapida;

        Title = modoAltaRapida ? "Nueva Subcategoría" : "Subcategorias";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        if (modoAltaRapida)
        {
            Width = 420;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
        }
        else
        {
            Width = 920;
            Height = 620;
        }

        BuildUI();
        Loaded += async (_, _) =>
        {
            await RefrescarAsync();
            if (_modoAltaRapida) OnNuevo(this, new RoutedEventArgs());
        };
    }

    private void BuildUI()
    {
        var root = new Grid { Margin = new Thickness(8, 8, 8, 8) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = _modoAltaRapida ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var search = new Border
        {
            Background = System.Windows.Media.Brushes.DarkOrange,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        searchRow.Children.Add(new TextBlock
        {
            Text = "Buscar:",
            Foreground = System.Windows.Media.Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        _txtBuscar = new TextBox { Width = 200, Padding = new Thickness(4, 3, 4, 3), Background = System.Windows.Media.Brushes.White };
        _txtBuscar.KeyDown += OnBuscarKeyDown;
        searchRow.Children.Add(_txtBuscar);
        var btnBuscar = new Button { Content = "Buscar", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(10, 4, 10, 4), Background = System.Windows.Media.Brushes.White };
        btnBuscar.Click += OnBuscar;
        searchRow.Children.Add(btnBuscar);
        search.Child = searchRow;
        Grid.SetRow(search, 0);
        search.Visibility = _modoAltaRapida ? Visibility.Collapsed : Visibility.Visible;
        root.Children.Add(search);

        var form = new Border
        {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = System.Windows.Media.Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 10, 10, 10),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var formGrid = new Grid();
        formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        left.Children.Add(Label("Código"));
        _txtCodigo = Input();
        left.Children.Add(_txtCodigo);
        left.Children.Add(Label("Nombre"));
        _txtNombre = Input();
        left.Children.Add(_txtNombre);

        var right = new StackPanel();
        right.Children.Add(Label("Categoría"));
        _cboCategoria = new ComboBox { Padding = new Thickness(4, 3, 4, 3), IsEnabled = false, DisplayMemberPath = "NombreCategoria", SelectedValuePath = "IdCategoria" };
        right.Children.Add(_cboCategoria);
        right.Children.Add(new TextBlock { Text = "F2=Buscar  Ctrl+N=Nuevo  Ctrl+S=Guardar", Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 8, 0, 0) });

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        formGrid.Children.Add(left);
        formGrid.Children.Add(right);
        form.Child = formGrid;
        Grid.SetRow(form, 1);
        root.Children.Add(form);

        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.FloralWhite
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Código", Binding = new System.Windows.Data.Binding("CodigoSubcategoria"), Width = 120 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Subcategoría", Binding = new System.Windows.Data.Binding("NombreSubcategoria"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Categoría", Binding = new System.Windows.Data.Binding("CategoriaNombre"), Width = 200 });
        _grid.SelectionChanged += OnGridSelectionChanged;
        Grid.SetRow(_grid, 2);
        _grid.Visibility = _modoAltaRapida ? Visibility.Collapsed : Visibility.Visible;
        root.Children.Add(_grid);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        var btnVista = Btn("Vista", "#757575"); btnVista.Click += (_, _) => HabilitarEdicion(false);
        var btnNuevo = Btn("Nuevo", "#FF8C00"); btnNuevo.Click += OnNuevo;
        _btnEditar = Btn("Editar", "#3F51B5"); _btnEditar.IsEnabled = false; _btnEditar.Click += (_, _) => HabilitarEdicion(true);
        var btnGuardar = Btn("Guardar", "#27AE60"); btnGuardar.Click += OnGuardarClick;
        _btnEliminar = Btn("Eliminar", "#E74C3C"); _btnEliminar.IsEnabled = false; _btnEliminar.Click += OnEliminar;
        var btnCerrar = Btn("Cerrar", "#757575"); btnCerrar.Click += (_, _) => Close();
        if (_modoAltaRapida)
        {
            buttons.Children.Add(btnGuardar);
            buttons.Children.Add(btnCerrar);
        }
        else
        {
            buttons.Children.Add(btnVista);
            buttons.Children.Add(btnNuevo);
            buttons.Children.Add(_btnEditar);
            buttons.Children.Add(btnGuardar);
            buttons.Children.Add(_btnEliminar);
            buttons.Children.Add(btnCerrar);
        }
        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);

        Content = root;
    }

    private static TextBlock Label(string text) =>
        new TextBlock { Text = text, Margin = new Thickness(0, 6, 0, 2), Foreground = System.Windows.Media.Brushes.DimGray, FontWeight = FontWeights.SemiBold };

    private static TextBox Input() =>
        new TextBox { Padding = new Thickness(4, 3, 4, 3), IsReadOnly = true };

    private Button Btn(string text, string hex) => new Button
    {
        Content = text,
        Width = 80,
        Height = 30,
        Margin = new Thickness(0, 0, 6, 0),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White
    };

    private async Task CargarCategoriasAsync()
    {
        var categorias = (await _categorias.ListarTodosAsync()).ToList();
        categorias.Insert(0, new Categoria { IdCategoria = 0, NombreCategoria = "— Sin categoría —" });
        _cboCategoria.ItemsSource = categorias;
        _cboCategoria.SelectedIndex = 0;
    }

    private async Task RefrescarAsync()
    {
        await CargarCategoriasAsync();
        _grid.ItemsSource = (await _repo.ListarTodosAsync()).ToList();
        LimpiarFormulario();
        HabilitarEdicion(false);
    }

    private void LimpiarFormulario()
    {
        _txtCodigo.Text = "";
        _txtNombre.Text = "";
        _cboCategoria.SelectedIndex = 0;
        _seleccionada = null;
        _btnEditar.IsEnabled = false;
        _btnEliminar.IsEnabled = false;
    }

    private void HabilitarEdicion(bool habilitar)
    {
        _txtCodigo.IsReadOnly = !habilitar;
        _txtNombre.IsReadOnly = !habilitar;
        _cboCategoria.IsEnabled = habilitar;
        if (!habilitar && _seleccionada == null)
            _btnEditar.IsEnabled = false;
    }

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_grid.SelectedItem is not Subcategoria sub) return;
        _seleccionada = sub;
        _txtCodigo.Text = sub.CodigoSubcategoria;
        _txtNombre.Text = sub.NombreSubcategoria;
        _cboCategoria.SelectedValue = sub.IdCategoria;
        _btnEditar.IsEnabled = true;
        _btnEliminar.IsEnabled = true;
    }

    private void OnNuevo(object sender, RoutedEventArgs e)
    {
        LimpiarFormulario();
        HabilitarEdicion(true);
        _txtCodigo.Focus();
    }

    private async void OnBuscar(object sender, RoutedEventArgs e) => await BuscarAsync();

    private async void OnBuscarKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await BuscarAsync();
    }

    private async Task BuscarAsync()
    {
        _grid.ItemsSource = (await _repo.ListarAsync(_txtBuscar.Text.Trim())).ToList();
    }

    private async void OnGuardarClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtCodigo.Text) || string.IsNullOrWhiteSpace(_txtNombre.Text))
        {
            MessageBox.Show("Complete codigo y nombre.", "Validacion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_cboCategoria.SelectedItem is not Categoria cat || cat.IdCategoria <= 0)
        {
            MessageBox.Show("Seleccione una categoria.", "Validacion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var cod = _txtCodigo.Text.Trim();
            var nom = _txtNombre.Text.Trim();

            if (_seleccionada == null)
                await _repo.InsertarAsync(cat.IdCategoria, cod, nom);
            else
                await _repo.ActualizarAsync(_seleccionada.IdSubcategoria, cod, nom);

            if (_modoAltaRapida)
            {
                var todas = (await _repo.ListarTodosAsync()).ToList();
                SubcategoriaCreada = todas.FirstOrDefault(x => x.CodigoSubcategoria == cod && x.NombreSubcategoria == nom)
                                   ?? todas.LastOrDefault(x => x.NombreSubcategoria == nom);
                DialogResult = true;
                return;
            }

            await RefrescarAsync();
            MessageBox.Show("Guardado correctamente.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnEliminar(object sender, RoutedEventArgs e)
    {
        if (_seleccionada == null) return;
        if (MessageBox.Show($"Eliminar subcategoria '{_seleccionada.NombreSubcategoria}'?", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            await _repo.EliminarAsync(_seleccionada.IdSubcategoria);
            await RefrescarAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F2)
            _txtBuscar.Focus();
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            OnNuevo(this, new RoutedEventArgs());
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            OnGuardarClick(this, new RoutedEventArgs());
    }
}
