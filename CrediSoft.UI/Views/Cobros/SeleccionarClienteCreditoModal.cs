using CrediSoft.Core.Models;
using CrediSoft.Core.Interfaces;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Cobros;

public class SeleccionarClienteCreditoModal : Window
{
    private readonly IClienteRepository _clientes;
    private readonly ILocalRepository _localesRepo;
    private ComboBox _cboFiltro = null!;
    private TextBox _txtBuscar = null!;
    private TextBlock _txtConteo = null!;
    private TextBlock _txtTotal = null!;
    private TextBlock _txtAtraso = null!;
    private TextBlock _txtActivos = null!;
    private TextBlock _txtAlDia = null!;
    private Button _btnLocal = null!;
    private TextBlock _btnLocalEtiqueta = null!;
    private TextBlock _btnLocalValor = null!;
    private DataGrid _grid = null!;
    private Button _btnSeleccionar = null!;
    private CancellationTokenSource? _debounce;
    private List<ClienteCreditoResumen> _items = new();
    private int? _localFiltroId;
    private string _localFiltroNombre = "";

    public ClienteCreditoResumen? ClienteSeleccionado { get; private set; }

    public SeleccionarClienteCreditoModal()
    {
        _clientes = App.Services.GetRequiredService<IClienteRepository>();
        _localesRepo = App.Services.GetRequiredService<ILocalRepository>();

        Title = "Clientes con créditos";
        Width = 1220;
        Height = 720;
        MinWidth = 1040;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // search
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // summary
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        root.Children.Add(BuildHeader());
        Grid.SetRow(root.Children[^1], 0);
        root.Children.Add(BuildSearchBar());
        Grid.SetRow(root.Children[^1], 1);
        root.Children.Add(BuildSummaryBar());
        Grid.SetRow(root.Children[^1], 2);

        _grid = BuildGrid();
        Grid.SetRow(_grid, 3);
        root.Children.Add(_grid);

        root.Children.Add(BuildFooter());
        Grid.SetRow(root.Children[^1], 4);

        Content = root;
        Loaded += async (_, _) =>
        {
            _txtBuscar.Focus();
            await CargarAsync();
        };
    }

    private UIElement BuildHeader()
    {
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(14, 47, 68)),
            Padding = new Thickness(16, 12, 16, 12)
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "CLIENTES CON CRÉDITOS",
            Foreground = Brushes.White,
            FontSize = 16,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Elegí un cliente de la lista. Los que tienen atraso aparecen primero y quedan resaltados.",
            Foreground = new SolidColorBrush(Color.FromRgb(127, 179, 211)),
            FontSize = 11.5,
            Margin = new Thickness(0, 4, 0, 0)
        });
        header.Child = stack;
        return header;
    }

    private UIElement BuildSearchBar()
    {
        var searchBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(236, 239, 241)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 10, 14, 10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(new TextBlock
        {
            Text = "Buscar:",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(55, 71, 79)),
            Margin = new Thickness(0, 0, 8, 0)
        });

        var searchHost = new Grid
        {
            Width = 300,
            Height = 30
        };

        _txtBuscar = new TextBox
        {
            Padding = new Thickness(8, 5, 8, 5)
        };
        _txtBuscar.TextChanged += OnBuscarChanged;
        _txtBuscar.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                Seleccionar();
        };
        searchHost.Children.Add(_txtBuscar);

        var placeholder = new TextBlock
        {
            Text = "Buscar por nombre, C.I. o teléfono",
            Foreground = new SolidColorBrush(Color.FromRgb(144, 164, 174)),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        searchHost.Children.Add(placeholder);

        void UpdatePlaceholder()
        {
            placeholder.Visibility = string.IsNullOrWhiteSpace(_txtBuscar.Text) && !_txtBuscar.IsFocused
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        _txtBuscar.GotFocus += (_, _) => UpdatePlaceholder();
        _txtBuscar.LostFocus += (_, _) => UpdatePlaceholder();
        _txtBuscar.TextChanged += (_, _) => UpdatePlaceholder();
        UpdatePlaceholder();

        left.Children.Add(searchHost);

        _txtConteo = new TextBlock
        {
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold
        };
        left.Children.Add(_txtConteo);

        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        _btnLocalEtiqueta = new TextBlock
        {
            Text = "Local",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139))
        };
        _btnLocalValor = new TextBlock
        {
            Text = "Todos los locales",
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(33, 37, 41)),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _btnLocal = new Button
        {
            Height = 40,
            MinWidth = 260,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(8, 0, 8, 0),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220)),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            ToolTip = "Filtrar la lista por un local específico. Click para elegir."
        };
        var localStack = new StackPanel { Orientation = Orientation.Vertical };
        localStack.Children.Add(_btnLocalEtiqueta);
        localStack.Children.Add(_btnLocalValor);
        _btnLocal.Content = localStack;
        _btnLocal.HorizontalContentAlignment = HorizontalAlignment.Left;
        _btnLocal.Click += async (_, _) => await AbrirSelectorLocalAsync();
        Grid.SetColumn(_btnLocal, 1);
        grid.Children.Add(_btnLocal);

        var filtroStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 0, 8, 0) };
        filtroStack.Children.Add(new TextBlock
        {
            Text = "Filtro",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
            Margin = new Thickness(0, 0, 0, 2)
        });

        _cboFiltro = new ComboBox
        {
            Height = 30,
            Padding = new Thickness(8, 4, 8, 4)
        };
        _cboFiltro.Items.Add("Todos con historial");
        _cboFiltro.Items.Add("Solo activos");
        _cboFiltro.Items.Add("Solo con atrasos");
        _cboFiltro.SelectedIndex = 0;
        _cboFiltro.SelectionChanged += async (_, _) => await CargarAsync();
        filtroStack.Children.Add(_cboFiltro);
        Grid.SetColumn(filtroStack, 2);
        grid.Children.Add(filtroStack);

        var btnLimpiar = new Button
        {
            Content = "Limpiar",
            Padding = new Thickness(12, 5, 12, 5),
            Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnLimpiar.Click += async (_, _) =>
        {
            _txtBuscar.Text = "";
            _cboFiltro.SelectedIndex = 0;
            await CargarAsync();
            _txtBuscar.Focus();
        };
        Grid.SetColumn(btnLimpiar, 3);
        grid.Children.Add(btnLimpiar);

        searchBar.Child = grid;
        return searchBar;
    }

    private UIElement BuildSummaryBar()
    {
        var bar = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 12, 14, 12)
        };

        var grid = new UniformGrid { Columns = 4 };
        _txtTotal = AddSummaryCard(grid, "Clientes", "0", Color.FromRgb(21, 101, 192));
        _txtAtraso = AddSummaryCard(grid, "Con atraso", "0", Color.FromRgb(198, 40, 40));
        _txtActivos = AddSummaryCard(grid, "Con activos", "0", Color.FromRgb(46, 125, 50));
        _txtAlDia = AddSummaryCard(grid, "Al día", "0", Color.FromRgb(84, 110, 122));

        bar.Child = grid;
        return bar;
    }

    private static TextBlock AddSummaryCard(Panel parent, string title, string value, Color accent)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
            Background = Brushes.White,
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 10, 0)
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold
        });

        var valueText = new TextBlock
        {
            Text = value,
            Foreground = new SolidColorBrush(accent),
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };
        stack.Children.Add(valueText);
        card.Child = stack;
        parent.Children.Add(card);
        return valueText;
    }

    private DataGrid BuildGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            RowHeight = 36,
            ColumnHeaderHeight = 40,
            FontSize = 11.5,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(233, 236, 239)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            Margin = new Thickness(14),
            HeadersVisibility = DataGridHeadersVisibility.Column,
            CanUserResizeColumns = false
        };
        grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader))
        {
            Setters =
            {
                new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)),
                new Setter(Control.FontWeightProperty, FontWeights.SemiBold),
                new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center)
            }
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Cliente",
            Binding = new Binding(nameof(ClienteCreditoResumen.NombreCliente)),
            Width = new DataGridLength(1.7, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(new DataGridTextColumn { Header = "C.I.", Binding = new Binding(nameof(ClienteCreditoResumen.CiCliente)), Width = 95 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Créd. histórico", Binding = new Binding(nameof(ClienteCreditoResumen.CreditosTotales)), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Créd. activos", Binding = new Binding(nameof(ClienteCreditoResumen.CreditosActivos)), Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cuotas atrasadas", Binding = new Binding(nameof(ClienteCreditoResumen.CuotasAtrasadas)), Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Mora (días)", Binding = new Binding(nameof(ClienteCreditoResumen.DiasAtrasoMax)), Width = 85 });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Gs. atr.",
            Binding = new Binding(nameof(ClienteCreditoResumen.MontoAtraso)) { StringFormat = "Gs. {0:N0}" },
            Width = 105
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Próx. vto.",
            Binding = new Binding(nameof(ClienteCreditoResumen.ProximoVencimientoTexto)),
            Width = 95
        });
        grid.Columns.Add(CrearEstadoColumn());

        grid.LoadingRow += OnLoadingRow;
        grid.SelectionChanged += (_, _) => _btnSeleccionar.IsEnabled = grid.SelectedItem is ClienteCreditoResumen;
        grid.MouseDoubleClick += (_, _) => Seleccionar();
        return grid;
    }

    private static DataGridTemplateColumn CrearEstadoColumn()
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(8, 3, 8, 3));
        borderFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(ClienteCreditoResumen.EstadoTexto)));
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        textFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        borderFactory.AppendChild(textFactory);

        var template = new DataTemplate { VisualTree = borderFactory };
        template.Triggers.Add(new DataTrigger
        {
            Binding = new Binding(nameof(ClienteCreditoResumen.TieneAtraso)),
            Value = true,
            Setters =
            {
                new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 224, 178))),
                new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(239, 108, 0))),
                new Setter(Border.BorderThicknessProperty, new Thickness(1)),
                new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(127, 51, 0)))
            }
        });
        template.Triggers.Add(new DataTrigger
        {
            Binding = new Binding(nameof(ClienteCreditoResumen.TieneAtraso)),
            Value = false,
            Setters =
            {
                new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(227, 242, 253))),
                new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(33, 150, 243))),
                new Setter(Border.BorderThicknessProperty, new Thickness(1)),
                new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(13, 71, 161)))
            }
        });

        return new DataGridTemplateColumn
        {
            Header = "Estado",
            CellTemplate = template,
            Width = 100
        };
    }

    private UIElement BuildFooter()
    {
        var footer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(250, 251, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(238, 241, 243)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 8, 14, 8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = "Los clientes con atrasos aparecen primero. Doble clic o Enter para seleccionar.",
            Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156)),
            FontSize = 11
        });

        var btnCancelar = new Button
        {
            Content = "Cerrar",
            Padding = new Thickness(14, 5, 14, 5),
            Background = new SolidColorBrush(Color.FromRgb(84, 110, 122)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 8, 0)
        };
        btnCancelar.Click += (_, _) => Close();
        Grid.SetColumn(btnCancelar, 1);
        grid.Children.Add(btnCancelar);

        _btnSeleccionar = new Button
        {
            Content = "Seleccionar",
            Padding = new Thickness(14, 5, 14, 5),
            Background = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            IsEnabled = false
        };
        _btnSeleccionar.Click += (_, _) => Seleccionar();
        Grid.SetColumn(_btnSeleccionar, 2);
        grid.Children.Add(_btnSeleccionar);

        footer.Child = grid;
        return footer;
    }

    private async Task CargarAsync()
    {
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        try
        {
            await Task.Delay(200, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        var termino = _txtBuscar.Text.Trim();
        var soloAtrasos = _cboFiltro.SelectedIndex == 2;
        var soloActivos = _cboFiltro.SelectedIndex == 1;

        var filas = (await _clientes.BuscarClientesConCreditosAsync(termino, soloAtrasos, _localFiltroId)).ToList();
        if (soloActivos)
            filas = filas.Where(f => f.CreditosActivos > 0).ToList();

        _items = filas;
        _grid.ItemsSource = _items;
        _txtConteo.Text = $"{_items.Count:N0} cliente(s)";
        _txtTotal.Text = _items.Count.ToString("N0");
        _txtAtraso.Text = _items.Count(f => f.TieneAtraso).ToString("N0");
        _txtActivos.Text = _items.Count(f => f.CreditosActivos > 0).ToString("N0");
        _txtAlDia.Text = _items.Count(f => !f.TieneAtraso).ToString("N0");

        _btnSeleccionar.IsEnabled = _grid.SelectedItem is ClienteCreditoResumen;
    }

    private async Task AbrirSelectorLocalAsync()
    {
        var locales = (await _localesRepo.ListarTodosAsync()).ToList();
        var modal = new LocalBuscadorModal(locales)
        {
            Owner = this
        };

        if (modal.ShowDialog() == true && modal.LocalSeleccionado != null)
        {
            _localFiltroId = modal.LocalSeleccionado.IdLocal;
            _localFiltroNombre = $"{modal.LocalSeleccionado.Codigo} - {modal.LocalSeleccionado.NombreLocal}".Trim();
            _btnLocalValor.Text = _localFiltroNombre;
            _btnLocal.Background = new SolidColorBrush(Color.FromRgb(255, 247, 205));
            _btnLocal.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7));
        }
        else
        {
            _localFiltroId = null;
            _localFiltroNombre = "";
            _btnLocalValor.Text = "Todos los locales";
            _btnLocal.Background = Brushes.White;
            _btnLocal.BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220));
        }

        await CargarAsync();
    }

    private void OnBuscarChanged(object sender, TextChangedEventArgs e) => _ = CargarAsync();

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is not ClienteCreditoResumen fila)
            return;

        if (fila.TieneAtraso)
        {
            e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224));
            e.Row.Foreground = new SolidColorBrush(Color.FromRgb(127, 51, 0));
        }
        else
        {
            e.Row.Background = Brushes.White;
            e.Row.Foreground = Brushes.Black;
        }
    }

    private void Seleccionar()
    {
        if (_grid.SelectedItem is not ClienteCreditoResumen fila)
            return;

        ClienteSeleccionado = fila;
        DialogResult = true;
        Close();
    }

    private sealed class LocalBuscadorModal : Window
    {
        private readonly List<Local> _todos;
        private readonly TextBox _txtBuscar = null!;
        private readonly DataGrid _grid = null!;
        public Local? LocalSeleccionado { get; private set; }

        public LocalBuscadorModal(List<Local> locales)
        {
            _todos = locales;
            Title = "Seleccionar local";
            Width = 720;
            Height = 620;
            MinWidth = 640;
            MinHeight = 500;
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
                Text = "Buscar local por código o nombre",
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
            _txtBuscar = search;
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
                Binding = new Binding(nameof(Local.Codigo)),
                Width = 80
            });
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Local",
                Binding = new Binding(nameof(Local.NombreLocal)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "ID",
                Binding = new Binding(nameof(Local.IdLocal)),
                Width = 55
            });
            _grid.MouseDoubleClick += (_, _) => Confirmar();
            _grid.SelectionChanged += (_, _) => { };
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

            var btnTodos = new Button
            {
                Content = "Todos",
                Padding = new Thickness(14, 5, 14, 5),
                Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnTodos.Click += (_, _) =>
            {
                LocalSeleccionado = null;
                DialogResult = true;
                Close();
            };
            Grid.SetColumn(btnTodos, 1);
            footerGrid.Children.Add(btnTodos);

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
        }

        private void Filtrar(string texto)
        {
            var f = texto.Trim();
            var lista = string.IsNullOrWhiteSpace(f)
                ? _todos
                : _todos.Where(l =>
                    l.Codigo.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                    l.NombreLocal.Contains(f, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(l => l.Codigo)
                    .ThenBy(l => l.NombreLocal)
                    .ToList();

            _grid.ItemsSource = lista;
        }

        private void Confirmar()
        {
            if (_grid.SelectedItem is not Local local)
                return;

            LocalSeleccionado = local;
            DialogResult = true;
            Close();
        }
    }
}






