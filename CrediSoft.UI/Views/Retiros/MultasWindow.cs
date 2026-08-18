using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Shared;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Retiros;

// Carga de multas a funcionarios — a diferencia del Anticipo, esto NUNCA toca CAJA_DETALLE
// (no es un movimiento de efectivo, es un descuento administrativo puro). Vigencia mensual:
// la multa se imputa al mes calendario en que se carga (MultaRepository.CargarAsync usa
// GETDATE()) y el cálculo de sueldo del mes siguiente no vuelve a sumarla — no hay arrastre
// automático. Acceso restringido a administrador + código 67 (Usuario.PuedeVerTodosLosLocales,
// mismo criterio ya usado en Caja para este mismo usuario) — se valida al abrir la ventana,
// no solo ocultando el ítem de menú, porque el ítem de menú puede volverse visible para
// cualquiera si alguna vez se reordena el XAML.
public class MultasWindow : Window
{
    private readonly IMultaRepository  _multas;
    private readonly IUsuarioRepository _usuarios;
    private readonly ISessionService    _session;

    private ComboBox  _cboMes  = null!;
    private ComboBox  _cboAnio = null!;
    private ComboBox  _cboFiltroFuncionario = null!;
    private DataGrid  _grid    = null!;
    private List<Usuario> _todosFuncionarios = new();
    private TextBlock _kpiCantidad = null!;
    private TextBlock _kpiTotal    = null!;

    private int?      _idFuncionarioSeleccionado;
    private string    _nombreFuncionarioSeleccionado = "";
    private int       _idLocalFuncionarioSeleccionado;
    private Border     _btnFunc      = null!;
    private TextBlock  _tbFuncionario= null!;
    private TextBox    _txtMonto     = null!;
    private TextBox    _txtConcepto  = null!;
    private Button      _btnCargar   = null!;

    // Paleta consistente con el resto de módulos rediseñados esta sesión (Caja, Promociones).
    private static readonly SolidColorBrush BrPrimary   = new(Color.FromRgb(18, 78, 120));   // #124E78
    private static readonly SolidColorBrush BrPrimDark  = new(Color.FromRgb(12, 55, 88));
    private static readonly SolidColorBrush BrRojo      = new(Color.FromRgb(192, 40, 27));
    private static readonly SolidColorBrush BrRojoOsc   = new(Color.FromRgb(154, 30, 20));
    private static readonly SolidColorBrush BrRojoClaro = new(Color.FromRgb(255, 235, 238));
    private static readonly SolidColorBrush BrFondo     = new(Color.FromRgb(238, 242, 247));  // #EEF2F7
    private static readonly SolidColorBrush BrCard      = Brushes.White;
    private static readonly SolidColorBrush BrBorde     = new(Color.FromRgb(208, 218, 232));  // #D0DAE8
    private static readonly SolidColorBrush BrTexto     = new(Color.FromRgb(20, 31, 48));     // #141F30
    private static readonly SolidColorBrush BrLabel     = new(Color.FromRgb(90, 107, 124));   // #5A6B7C
    private static readonly SolidColorBrush BrPlaceholder = new(Color.FromRgb(144, 164, 174));
    private static readonly SolidColorBrush BrFilaAlt   = new(Color.FromRgb(247, 249, 251));

    public MultasWindow()
    {
        _multas   = App.Services.GetRequiredService<IMultaRepository>();
        _usuarios = App.Services.GetRequiredService<IUsuarioRepository>();
        _session  = SessionService.Instance;

        if (_session.UsuarioActual?.PuedeVerTodosLosLocales != true)
        {
            // No debería poder llegar a construirse esta ventana para otro usuario (el
            // ítem de menú ya está condicionado), pero se valida igual acá por si se abre
            // desde otro camino en el futuro.
            MessageBox.Show("No tenés permiso para acceder a esta pantalla.",
                "Acceso restringido", MessageBoxButton.OK, MessageBoxImage.Warning);
            Loaded += (_, _) => Close();
            return;
        }

        Title = "Multas a Funcionarios — ElectroMar";
        Width = 980;
        Height = 660;
        MinWidth = 880; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrFondo;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 12;
        ShowInTaskbar = false;

        // El contenido queda oculto hasta validar credenciales — antes se veía la pantalla
        // completa (grilla, botones, KPIs) DETRÁS del diálogo de credenciales mientras el
        // usuario todavía no se había autenticado; visualmente parecía que primero se abría
        // Multas y RECIÉN DESPUÉS pedía acceso, en vez de al revés. Pedido explícito
        // 2026-08-04.
        var contenido = BuildUI();
        contenido.Opacity = 0;
        contenido.IsHitTestVisible = false;
        Content = contenido;
        Loaded += async (_, _) =>
        {
            // Segunda barrera además de ocultar el ítem de menú (MainWindow.OnMenuMultas):
            // pedir credenciales de un usuario con PuedeVerTodosLosLocales al abrir, para que
            // no alcance con que la sesión quede abierta en la PC — pedido explícito
            // 2026-08-04. Mismo diálogo que ya usa Caja (CajaCredencialesDialog), pero acá se
            // valida el permiso específico de Multas en vez de dueño de caja.
            if (!await ValidarAccesoAsync())
            {
                Close();
                return;
            }

            contenido.Opacity = 1;
            contenido.IsHitTestVisible = true;

            _todosFuncionarios = (await _usuarios.ListarTodosAsync()).OrderBy(u => u.NombreUsuario).ToList();
            foreach (var u in _todosFuncionarios)
                _cboFiltroFuncionario.Items.Add(new ComboBoxItem { Content = u.NombreUsuario, Tag = u.IdUsuario });
            await CargarAsync();
        };
    }

    private UIElement BuildUI()
    {
        var root = new Grid { Margin = new Thickness(22, 18, 22, 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // KPIs
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // panel carga
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filtro período
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grilla

        // ── Header ──────────────────────────────────────────────────────────
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconoBadge = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(9),
            Background = BrRojoClaro, Margin = new Thickness(0, 0, 12, 0),
        };
        iconoBadge.Child = new TextBlock
        {
            Text = char.ConvertFromUtf32(0xE7BA), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15,
            Foreground = BrRojo, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(iconoBadge, 0);
        header.Children.Add(iconoBadge);

        var tituloSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        tituloSp.Children.Add(new TextBlock
        {
            Text = "Multas a Funcionarios", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = BrPrimary,
        });
        tituloSp.Children.Add(new TextBlock
        {
            Text = "Descuento administrativo del sueldo — no afecta caja",
            FontSize = 11, Foreground = BrLabel, Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(tituloSp, 1);
        header.Children.Add(tituloSp);

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // ── KPIs del período ──────────────────────────────────────────────────
        var kpiRow = new Grid();
        kpiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        kpiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        kpiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        kpiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        kpiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

        var kpiCant = KpiCard("MULTAS DEL PERÍODO", out _kpiCantidad, BrPrimary);
        Grid.SetColumn(kpiCant, 0);
        kpiRow.Children.Add(kpiCant);

        var kpiTotal = KpiCard("TOTAL A DESCONTAR", out _kpiTotal, BrRojo);
        Grid.SetColumn(kpiTotal, 2);
        kpiRow.Children.Add(kpiTotal);

        var kpiInfo = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(232, 245, 238)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
        };
        var kpiInfoSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        kpiInfoSp.Children.Add(new TextBlock
        {
            Text = "Vigencia mensual", FontSize = 9.5, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)),
        });
        kpiInfoSp.Children.Add(new TextBlock
        {
            Text = "Cada multa se descuenta solo del mes en que se carga — no se arrastra al siguiente.",
            FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(27, 94, 32)),
            Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
        kpiInfo.Child = kpiInfoSp;
        Grid.SetColumn(kpiInfo, 4);
        kpiRow.Children.Add(kpiInfo);

        Grid.SetRow(kpiRow, 2);
        root.Children.Add(kpiRow);

        // ── Panel de carga ──────────────────────────────────────────────────
        var card = new Border
        {
            Background = BrCard, CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18), BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = Colors.Black, Opacity = 0.06, BlurRadius = 10, ShadowDepth = 2 },
        };
        var cardBody = new StackPanel();

        cardBody.Children.Add(new TextBlock
        {
            Text = "CARGAR NUEVA MULTA", FontSize = 11.5, FontWeight = FontWeights.Bold,
            Foreground = BrLabel, Margin = new Thickness(0, 0, 0, 14),
        });

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Funcionario
        var spFunc = new StackPanel();
        spFunc.Children.Add(Lbl("FUNCIONARIO"));
        _btnFunc = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220)),
            BorderThickness = new Thickness(1.3), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(11, 0, 11, 0), Height = 34, Cursor = Cursors.Hand,
        };
        _tbFuncionario = new TextBlock
        {
            Text = "Click para seleccionar...", Foreground = BrPlaceholder, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _btnFunc.Child = _tbFuncionario;
        _btnFunc.MouseLeftButtonUp += async (_, _) => await SeleccionarFuncionarioAsync();
        spFunc.Children.Add(_btnFunc);
        Grid.SetColumn(spFunc, 0);
        row.Children.Add(spFunc);

        // Monto — con separador de miles en tiempo real mientras se escribe
        var spMonto = new StackPanel();
        spMonto.Children.Add(Lbl("MONTO (Gs.)"));
        _txtMonto = Txt();
        _txtMonto.TextAlignment = TextAlignment.Right;
        bool fmtMontoBusy = false;
        _txtMonto.TextChanged += (_, _) =>
        {
            if (fmtMontoBusy) return;
            fmtMontoBusy = true;
            var raw = new string(_txtMonto.Text.Where(char.IsDigit).ToArray());
            if (raw.Length > 0 && long.TryParse(raw, out var n))
            {
                var fmt = n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY"));
                _txtMonto.Text = fmt;
                _txtMonto.CaretIndex = fmt.Length;
            }
            fmtMontoBusy = false;
        };
        spMonto.Children.Add(_txtMonto);
        Grid.SetColumn(spMonto, 2);
        row.Children.Add(spMonto);

        // Concepto
        var spConcepto = new StackPanel();
        spConcepto.Children.Add(Lbl("MOTIVO"));
        _txtConcepto = Txt();
        spConcepto.Children.Add(_txtConcepto);
        Grid.SetColumn(spConcepto, 4);
        row.Children.Add(spConcepto);

        // Botón cargar — alineado a la misma altura que los campos (no al label superior)
        var spBtn = new StackPanel();
        spBtn.Children.Add(new TextBlock { Text = " ", FontSize = 10.5, Margin = new Thickness(0, 0, 0, 4) });
        _btnCargar = new Button
        {
            Content = "Cargar multa", Height = 34, Padding = new Thickness(18, 0, 18, 0),
            Background = BrRojo, Foreground = Brushes.White, FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
        };
        _btnCargar.Click += async (_, _) => await CargarMultaAsync();
        spBtn.Children.Add(_btnCargar);
        Grid.SetColumn(spBtn, 6);
        row.Children.Add(spBtn);

        cardBody.Children.Add(row);
        card.Child = cardBody;
        Grid.SetRow(card, 4);
        root.Children.Add(card);

        // ── Filtro de período ──────────────────────────────────────────────────
        var filtroRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        filtroRow.Children.Add(new TextBlock
        {
            Text = "HISTORIAL — Período:", VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0), Foreground = BrLabel, FontSize = 11.5, FontWeight = FontWeights.Bold,
        });
        _cboMes = new ComboBox { Width = 130, Margin = new Thickness(0, 0, 8, 0) };
        for (int m = 1; m <= 12; m++)
            _cboMes.Items.Add(new ComboBoxItem
            {
                Content = System.Globalization.CultureInfo.GetCultureInfo("es-PY").DateTimeFormat.GetMonthName(m),
                Tag = m
            });
        _cboMes.SelectedIndex = DateTime.Today.Month - 1;
        _cboMes.SelectionChanged += async (_, _) => await CargarAsync();
        filtroRow.Children.Add(_cboMes);

        _cboAnio = new ComboBox { Width = 90, Margin = new Thickness(0, 0, 16, 0) };
        var anioActual = DateTime.Today.Year;
        for (int a = anioActual - 1; a <= anioActual + 1; a++)
            _cboAnio.Items.Add(new ComboBoxItem { Content = a.ToString(), Tag = a });
        _cboAnio.SelectedIndex = 1;
        _cboAnio.SelectionChanged += async (_, _) => await CargarAsync();
        filtroRow.Children.Add(_cboAnio);

        filtroRow.Children.Add(new TextBlock
        {
            Text = "Funcionario:", VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0), Foreground = BrLabel, FontSize = 11.5, FontWeight = FontWeights.Bold,
        });
        _cboFiltroFuncionario = new ComboBox { Width = 220, Margin = new Thickness(0, 0, 16, 0) };
        _cboFiltroFuncionario.Items.Add(new ComboBoxItem { Content = "Todos", Tag = null, IsSelected = true });
        _cboFiltroFuncionario.SelectionChanged += async (_, _) => await CargarAsync();
        filtroRow.Children.Add(_cboFiltroFuncionario);

        var btnReporte = new Button
        {
            Content = "Generar reporte", Padding = new Thickness(14, 6, 14, 6), FontSize = 11.5,
            FontWeight = FontWeights.SemiBold, Background = BrPrimary, Foreground = Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
        };
        btnReporte.Click += (_, _) => AbrirReporte();
        filtroRow.Children.Add(btnReporte);

        Grid.SetRow(filtroRow, 6);
        root.Children.Add(filtroRow);

        // ── Grilla ──────────────────────────────────────────────────────────
        var gridCard = new Border
        {
            Background = BrCard, CornerRadius = new CornerRadius(10),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1), ClipToBounds = true,
        };

        _grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            RowHeaderWidth = 0,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            AlternatingRowBackground = BrFilaAlt, RowBackground = Brushes.White,
            FontSize = 12, RowHeight = 36,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalGridLinesBrush = Brushes.Transparent,
        };
        EstilizarHeader(_grid);

        _grid.Columns.Add(ColTexto("Fecha", "Fecha", 95, "dd/MM/yyyy"));
        _grid.Columns.Add(ColTextoStar("Funcionario", "NombreFuncionario", 1.2));
        _grid.Columns.Add(ColTextoStar("Local", "LocalNombre", 1.0));
        _grid.Columns.Add(ColTexto("Monto", "Monto", 110, "N0", right: true));
        _grid.Columns.Add(ColTextoStar("Motivo", "Concepto", 1.6));
        _grid.Columns.Add(ColTexto("Cargado por", "UsuarioCarga", 130));

        var colAnular = new DataGridTemplateColumn { Header = "", Width = 90 };
        var factory = new FrameworkElementFactory(typeof(Button));
        factory.SetValue(Button.ContentProperty, "Anular");
        factory.SetValue(Button.PaddingProperty, new Thickness(10, 5, 10, 5));
        factory.SetValue(Button.FontSizeProperty, 11.5);
        factory.SetValue(Button.FontWeightProperty, FontWeights.SemiBold);
        factory.SetValue(Button.BackgroundProperty, BrRojoClaro);
        factory.SetValue(Button.ForegroundProperty, BrRojoOsc);
        factory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        factory.SetValue(Button.CursorProperty, Cursors.Hand);
        factory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        factory.AddHandler(Button.ClickEvent, new RoutedEventHandler(async (s, _) =>
        {
            if (((FrameworkElement)s).DataContext is MultaFuncionarioRow fila)
                await AnularAsync(fila);
        }));
        colAnular.CellTemplate = new DataTemplate { VisualTree = factory };
        _grid.Columns.Add(colAnular);

        gridCard.Child = _grid;
        Grid.SetRow(gridCard, 8);
        root.Children.Add(gridCard);

        return root;
    }

    private static Border KpiCard(string titulo, out TextBlock valor, SolidColorBrush colorValor)
    {
        var card = new Border
        {
            Background = BrCard, CornerRadius = new CornerRadius(10),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 12, 16, 12),
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = titulo, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = BrLabel,
        });
        var tb = new TextBlock
        {
            FontSize = 20, FontWeight = FontWeights.Bold, Foreground = colorValor,
            Margin = new Thickness(0, 4, 0, 0),
        };
        sp.Children.Add(tb);
        card.Child = sp;
        valor = tb;
        return card;
    }

    private static void EstilizarHeader(DataGrid grid)
    {
        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(245, 247, 250))));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrLabel));
        headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 10.5));
        headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 9, 10, 9)));
        headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrBorde));
        grid.ColumnHeaderStyle = headerStyle;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 0, 10, 0)));
        cellStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        var trigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        trigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(227, 238, 247))));
        trigger.Setters.Add(new Setter(Control.ForegroundProperty, BrTexto));
        cellStyle.Triggers.Add(trigger);
        grid.CellStyle = cellStyle;
    }

    private static DataGridTextColumn ColTexto(string header, string prop, double width, string? format = null, bool right = false)
    {
        var col = new DataGridTextColumn
        {
            Header = header,
            Binding = new System.Windows.Data.Binding(prop) { StringFormat = format },
            Width = width,
        };
        var style = new Style(typeof(TextBlock));
        if (right) style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        col.ElementStyle = style;
        return col;
    }

    private static DataGridTextColumn ColTextoStar(string header, string prop, double starWeight) => new()
    {
        Header = header,
        Binding = new System.Windows.Data.Binding(prop),
        Width = new DataGridLength(starWeight, DataGridLengthUnitType.Star),
    };

    private static TextBlock Lbl(string texto) => new()
    {
        Text = texto, FontSize = 10.5, FontWeight = FontWeights.Bold,
        Foreground = BrLabel, Margin = new Thickness(1, 0, 0, 5),
    };

    private static TextBox Txt() => new()
    {
        Padding = new Thickness(10, 0, 10, 0), FontSize = 12, Height = 34,
        VerticalContentAlignment = VerticalAlignment.Center,
        BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 220)),
        BorderThickness = new Thickness(1.3),
    };

    private async Task SeleccionarFuncionarioAsync()
    {
        var usuarios = (await _usuarios.ListarTodosAsync()).ToList();
        var sel = SelectorModal.MostrarVendedores(this, usuarios);
        if (sel == null) return;
        _idFuncionarioSeleccionado = sel.IdUsuario;
        _nombreFuncionarioSeleccionado = sel.NombreUsuario;
        _idLocalFuncionarioSeleccionado = sel.LocalUsuario;
        _tbFuncionario.Text = sel.NombreUsuario;
        _tbFuncionario.Foreground = BrTexto;
    }

    private async Task CargarMultaAsync()
    {
        if (_idFuncionarioSeleccionado == null)
        {
            MessageBox.Show("Seleccioná el funcionario que recibe la multa.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var montoTxt = new string(_txtMonto.Text.Where(char.IsDigit).ToArray());
        if (!decimal.TryParse(montoTxt, out var monto) || monto <= 0)
        {
            MessageBox.Show("Ingresá un monto válido mayor a cero.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtConcepto.Text))
        {
            MessageBox.Show("Ingresá el motivo de la multa.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Local del FUNCIONARIO que recibe la multa (no el de quien la carga) — se guarda
        // solo para mostrarlo en el historial; ObtenerTotalMesAsync no filtra por local.
        var idLocal = _idLocalFuncionarioSeleccionado;
        var confirm = MessageBox.Show(
            $"¿Confirmar la siguiente multa?\n\n" +
            $"   Funcionario: {_nombreFuncionarioSeleccionado}\n" +
            $"   Monto:       Gs. {monto:N0}\n" +
            $"   Motivo:      {_txtConcepto.Text.Trim()}\n\n" +
            $"Esta multa se descontará del sueldo de {_nombreFuncionarioSeleccionado} " +
            $"correspondiente a {System.Globalization.CultureInfo.GetCultureInfo("es-PY").DateTimeFormat.GetMonthName(DateTime.Today.Month)} " +
            $"de {DateTime.Today.Year}. No afecta caja.",
            "Confirmar multa", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _multas.CargarAsync(_idFuncionarioSeleccionado.Value, (byte)idLocal, monto,
                _txtConcepto.Text.Trim(), _session.UsuarioActual!.IdUsuario);

            MessageBox.Show("Multa registrada correctamente.", "Listo",
                MessageBoxButton.OK, MessageBoxImage.Information);

            _idFuncionarioSeleccionado = null;
            _idLocalFuncionarioSeleccionado = 0;
            _tbFuncionario.Text = "Click para seleccionar...";
            _tbFuncionario.Foreground = BrPlaceholder;
            _txtMonto.Text = "";
            _txtConcepto.Text = "";

            // Vuelve al mes/año actual (el que se acaba de cargar) para que la multa nueva
            // sea visible de inmediato, aunque el usuario estuviera mirando otro período.
            _cboMes.SelectedIndex = DateTime.Today.Month - 1;
            _cboAnio.SelectedIndex = 1;
            await CargarAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al registrar la multa:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task AnularAsync(MultaFuncionarioRow fila)
    {
        var confirm = MessageBox.Show(
            $"¿Anular la multa de {fila.NombreFuncionario} por Gs. {fila.Monto:N0}?\n\n{fila.Concepto}",
            "Confirmar anulación", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;

        var ok = await _multas.AnularAsync(fila.Id);
        if (!ok)
        {
            MessageBox.Show("No se pudo anular la multa (puede que ya haya sido anulada).",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        if (_cboMes.SelectedItem is not ComboBoxItem miMes || _cboAnio.SelectedItem is not ComboBoxItem miAnio)
            return;

        var mes  = (byte)(int)miMes.Tag;
        var anio = (short)(int)miAnio.Tag;
        var idFuncionario = (_cboFiltroFuncionario?.SelectedItem as ComboBoxItem)?.Tag as int?;

        var filas = (await _multas.ListarAsync(mes, anio, idFuncionario: idFuncionario)).ToList();
        _grid.ItemsSource = filas;
        _kpiCantidad.Text = filas.Count.ToString();
        _kpiTotal.Text = $"Gs. {filas.Sum(f => f.Monto):N0}";
    }

    // Reporte imprimible del período/filtro actualmente visible en pantalla — mismo diseño
    // visual que el Arqueo de Caja (header Credimar, banda azul, agrupado por funcionario).
    private void AbrirReporte()
    {
        if (_grid.ItemsSource is not List<MultaFuncionarioRow> filas || filas.Count == 0)
        {
            MessageBox.Show("No hay multas en el período/filtro seleccionado para generar el reporte.",
                "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var mi = (ComboBoxItem)_cboMes.SelectedItem;
        var ai = (ComboBoxItem)_cboAnio.SelectedItem;
        var periodoTxt = $"{mi.Content} {ai.Content}";
        var filtroFuncTxt = (_cboFiltroFuncionario.SelectedItem as ComboBoxItem)?.Tag != null
            ? (_cboFiltroFuncionario.SelectedItem as ComboBoxItem)!.Content.ToString()!
            : "";

        var filasReporte = new List<FilaMultaReporte>();
        foreach (var grupo in filas.GroupBy(f => f.NombreFuncionario).OrderBy(g => g.Key))
        {
            var localesDelFuncionario = grupo.Select(f => f.LocalNombre).Distinct().ToList();
            var etiquetaLocal = localesDelFuncionario.Count == 1 ? localesDelFuncionario[0] : "";
            filasReporte.Add(new FilaMultaReporte { EsHeaderFuncionario = true, Funcionario = grupo.Key, LocalEtiqueta = etiquetaLocal });
            foreach (var f in grupo.OrderBy(f => f.Fecha))
                filasReporte.Add(new FilaMultaReporte
                {
                    FechaStr = f.Fecha.ToString("dd/MM/yyyy"), Monto = f.Monto,
                    Concepto = f.Concepto, UsuarioCarga = f.UsuarioCarga,
                });
        }

        var pagina = new MultasReportePagina
        {
            Filas = filasReporte,
            Periodo = periodoTxt,
            FiltroFuncionario = filtroFuncTxt,
            FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario = _session.UsuarioActual?.NombreUsuario ?? "",
            LogoPath = MultasReportePagina.ResolverLogoPath(),
        };
        new MultasReportePreviewWindow(pagina) { Owner = this }.ShowDialog();
    }

    // Pide código+contraseña (mismo diálogo que Caja) y verifica que ese usuario autenticado
    // — no necesariamente el logueado en la sesión — tenga PuedeVerTodosLosLocales. Permite
    // que un administrador distinto al que inició sesión igual pueda entrar tipeando sus
    // propias credenciales, sin exigir cerrar sesión.
    private async Task<bool> ValidarAccesoAsync()
    {
        var cred = new CrediSoft.UI.Views.Informes.CajaCredencialesDialog { Owner = Owner };
        if (cred.ShowDialog() != true) return false;

        using var conn = App.Services.GetRequiredService<CrediSoft.Data.IDbConnectionFactory>().Create();
        var cargoYCodigo = await conn.QueryFirstOrDefaultAsync<(string? Cargo, string? Codigo)>(
            "SELECT CARGO_USUARIO AS Cargo, CODIGO_USUARIO AS Codigo FROM USUARIOS WHERE ID_USUARIO = @id",
            new { id = cred.UsuarioId });
        var usuarioCred = new Usuario { CargoUsuario = cargoYCodigo.Cargo ?? "", CodigoUsuario = cargoYCodigo.Codigo ?? "" };

        if (!usuarioCred.PuedeVerTodosLosLocales)
        {
            MessageBox.Show("El usuario ingresado no tiene permiso para acceder a este módulo.",
                "Acceso restringido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }
}
