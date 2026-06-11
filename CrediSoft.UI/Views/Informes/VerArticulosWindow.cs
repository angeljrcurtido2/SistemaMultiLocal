using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Informes;

// ── Fila del visor con propiedades fijas + lista de stocks por índice ────────
public class VisorRow
{
    public string  Codigo   { get; set; } = string.Empty;
    public string  Desc     { get; set; } = string.Empty;
    public decimal Contado  { get; set; }
    public decimal PVenta   { get; set; }
    public int     MaxCuota { get; set; }
    // stocks[i] corresponde al local _locales[i]
    public decimal[] Stocks { get; set; } = Array.Empty<decimal>();

    public decimal TotalStock => Stocks.Length == 0 ? 0 : Stocks.Sum();
}

// ── Converter: extrae Stocks[índice] de la fila ──────────────────────────────
public class StockIndexConverter : IValueConverter
{
    public int Index { get; set; }
    private static readonly CultureInfo _cult = CultureInfo.GetCultureInfo("es-AR");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VisorRow row && Index < row.Stocks.Length)
            return row.Stocks[Index].ToString("N0", _cult);
        return "0";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// ════════════════════════════════════════════════════════════════════════════
// Ventana principal
// ════════════════════════════════════════════════════════════════════════════
public class VerArticulosWindow : Window
{
    private readonly IArticuloRepository _artRepo;
    private readonly ILocalRepository    _localRepo;

    private List<Local>              _locales = new();
    private ObservableCollection<VisorRow> _todos   = new();
    private List<VisorRow>           _todosList = new();

    private RadioButton _rbCodigo      = null!;
    private RadioButton _rbDescripcion = null!;
    private RadioButton _rbMarca       = null!;
    private TextBox     _txtBuscar     = null!;
    private DataGrid    _grid          = null!;
    private TextBlock   _lblTotal      = null!;

    // panel derecho
    private TextBox   _txtPanelCodigo  = null!;
    private TextBox   _txtPanelDesc    = null!;
    private TextBox   _txtContado      = null!;
    private TextBox   _txtPrecio       = null!;
    private TextBox   _txtPctDesc      = null!;
    private TextBox   _txtDescuento    = null!;
    private TextBox   _txtValor        = null!;
    private TextBox   _txtEntrega      = null!;
    private TextBox   _txtValorSaldo   = null!;
    private TextBox   _txtPctRec       = null!;
    private TextBox   _txtCantCuotas   = null!;
    private TextBox   _txtValorMensual = null!;
    private TextBox   _txtValorFinal   = null!;
    private TextBlock _lblMaxCuotas    = null!;

    private decimal _precioSel  = 0;
    private int     _maxcSel    = 0;

    private static readonly CultureInfo _cult    = CultureInfo.GetCultureInfo("es-AR");
    private static readonly SolidColorBrush BrVerde   = new(Color.FromRgb( 92, 184,  92));
    private static readonly SolidColorBrush BrRojo    = new(Color.FromRgb(217,  83,  79));
    private static readonly SolidColorBrush BrNaranja = new(Color.FromRgb(255, 165,   0));

    public VerArticulosWindow()
    {
        _artRepo   = App.Services.GetRequiredService<IArticuloRepository>();
        _localRepo = App.Services.GetRequiredService<ILocalRepository>();

        Title  = "Artículos / Mercaderías";
        Width  = 1320;
        Height = 740;
        MinWidth  = 900;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(20, 20, 20));

        Content = BuildLayout();
        Loaded += async (_, __) => await CargarAsync();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    // ══════════════════════════════════════════════════════════════════════
    // Layout
    // ══════════════════════════════════════════════════════════════════════
    private UIElement BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var barra = BuildBarra();
        Grid.SetRow(barra, 0);
        root.Children.Add(barra);

        var cuerpo = BuildCuerpo();
        Grid.SetRow(cuerpo, 1);
        root.Children.Add(cuerpo);

        var pie = BuildPie();
        Grid.SetRow(pie, 2);
        root.Children.Add(pie);

        return root;
    }

    // ── barra ─────────────────────────────────────────────────────────────
    private UIElement BuildBarra()
    {
        var panel = new DockPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(0, 150, 200)),
        };

        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        _rbCodigo      = MakeTab("CÓDIGO",      true);
        _rbDescripcion = MakeTab("DESCRIPCION", false);
        _rbMarca       = MakeTab("MARCA",       false);
        tabs.Children.Add(_rbCodigo);
        tabs.Children.Add(_rbDescripcion);
        tabs.Children.Add(_rbMarca);
        DockPanel.SetDock(tabs, Dock.Left);
        panel.Children.Add(tabs);

        _txtBuscar = new TextBox
        {
            Width   = 340,
            Margin  = new Thickness(12, 5, 4, 5),
            Padding = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
        };
        _txtBuscar.TextChanged += (_, __) => FiltrarGrid();
        DockPanel.SetDock(_txtBuscar, Dock.Left);
        panel.Children.Add(_txtBuscar);

        return panel;
    }

    private RadioButton MakeTab(string texto, bool isChecked)
    {
        var rb = new RadioButton
        {
            Content    = texto,
            IsChecked  = isChecked,
            GroupName  = "TabBusqueda",
            Margin     = new Thickness(2, 3, 2, 0),
            Padding    = new Thickness(14, 5, 14, 5),
            FontWeight = FontWeights.Bold,
            FontSize   = 12,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(0, 115, 160)),
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
        };
        rb.Checked += (_, __) => { _txtBuscar?.Clear(); FiltrarGrid(); };
        return rb;
    }

    // ── cuerpo ────────────────────────────────────────────────────────────
    private UIElement BuildCuerpo()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(255) });

        _grid = new DataGrid
        {
            AutoGenerateColumns      = false,
            IsReadOnly               = true,
            CanUserAddRows           = false,
            CanUserDeleteRows        = false,
            SelectionMode            = DataGridSelectionMode.Single,
            GridLinesVisibility      = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(50, 55, 70)),
            RowBackground            = new SolidColorBrush(Color.FromRgb(36, 40, 56)),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(36, 40, 56)),
            Background               = new SolidColorBrush(Color.FromRgb(25, 28, 40)),
            Foreground               = Brushes.White,
            BorderThickness          = new Thickness(0),
            FontSize                 = 13,
            ColumnHeaderHeight       = 32,
            RowHeight                = 36,
        };
        _grid.LoadingRow      += OnGridLoadingRow;
        _grid.SelectionChanged += OnSelectionChanged;

        // columnas fijas con binding a propiedades reales de VisorRow
        _grid.Columns.Add(MakeColProp("Código",      "Codigo",   130));
        _grid.Columns.Add(MakeColProp("Descripción", "Desc",     0, star: true, minW: 220));
        _grid.Columns.Add(MakeColNum ("Contado",      "Contado", 120));
        _grid.Columns.Add(MakeColNum ("P.Venta",      "PVenta",  120));
        _grid.Columns.Add(MakeColProp("Max",          "MaxCuota", 55));

        Grid.SetColumn(_grid, 0);
        grid.Children.Add(_grid);

        var panelDer = BuildPanelDerecho();
        Grid.SetColumn(panelDer, 1);
        grid.Children.Add(panelDer);

        return grid;
    }

    private static DataGridTextColumn MakeColProp(string header, string prop, double w, bool star = false, double minW = 0)
    {
        var c = new DataGridTextColumn
        {
            Header   = header,
            Binding  = new Binding(prop),
            Width    = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(w),
            MinWidth = minW,
        };
        c.HeaderStyle = MakeHeaderStyle();
        return c;
    }

    private static DataGridTextColumn MakeColNum(string header, string prop, double w)
    {
        var c = new DataGridTextColumn
        {
            Header  = header,
            Binding = new Binding(prop) { StringFormat = "N0", ConverterCulture = CultureInfo.GetCultureInfo("es-AR") },
            Width   = new DataGridLength(w),
        };
        c.HeaderStyle = MakeHeaderStyle();
        c.ElementStyle = new Style(typeof(TextBlock))
        {
            Setters =
            {
                new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right),
                new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0)),
            }
        };
        return c;
    }

    // columna de stock con converter que lee Stocks[index]
    private static DataGridTextColumn MakeColStock(string header, int stockIndex)
    {
        var conv = new StockIndexConverter { Index = stockIndex };
        var c = new DataGridTextColumn
        {
            Header  = header,
            Binding = new Binding(".") { Converter = conv },   // "." = el VisorRow completo
            Width   = new DataGridLength(100),
        };
        c.HeaderStyle = MakeHeaderStyle();
        c.ElementStyle = new Style(typeof(TextBlock))
        {
            Setters =
            {
                new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right),
                new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0)),
            }
        };
        return c;
    }

    private static Style MakeHeaderStyle()
    {
        var s = new Style(typeof(DataGridColumnHeader));
        s.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(30, 35, 48))));
        s.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(160, 170, 200))));
        s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(Control.FontSizeProperty,   11.0));
        s.Setters.Add(new Setter(Control.PaddingProperty,    new Thickness(6, 4, 6, 4)));
        s.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(50, 55, 75))));
        s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        return s;
    }

    // ── panel derecho moderno ─────────────────────────────────────────────
    private UIElement BuildPanelDerecho()
    {
        // paleta
        var bgPanel   = new SolidColorBrush(Color.FromRgb(30,  35,  48));   // fondo oscuro
        var bgInput   = new SolidColorBrush(Color.FromRgb(44,  50,  66));   // input readonly
        var bgEditable= new SolidColorBrush(Color.FromRgb(255, 255, 255));  // input editable
        var border    = new SolidColorBrush(Color.FromRgb(70,  80, 110));   // borde suave
        var lblColor  = new SolidColorBrush(Color.FromRgb(160, 170, 200));  // label gris-azul
        var txtColor  = Brushes.White;

        var sp = new StackPanel { Background = bgPanel };

        // ── encabezado: código ────────────────────────────────────────────
        sp.Children.Add(PanelLabel("Código", lblColor));
        _txtPanelCodigo = PanelField(bgInput, txtColor, border);
        sp.Children.Add(_txtPanelCodigo);

        // ── descripción (multilinea) ───────────────────────────────────────
        _txtPanelDesc = new TextBox
        {
            IsReadOnly      = true,
            Margin          = new Thickness(8, 0, 8, 6),
            Padding         = new Thickness(6, 4, 6, 4),
            Height          = 38,
            TextWrapping    = TextWrapping.Wrap,
            Background      = bgInput,
            Foreground      = txtColor,
            BorderBrush     = border,
            BorderThickness = new Thickness(1),
            FontSize        = 11,
        };
        sp.Children.Add(_txtPanelDesc);

        // ── contado ───────────────────────────────────────────────────────
        sp.Children.Add(PanelLabel("Contado", lblColor));
        _txtContado = PanelField(bgInput, txtColor, border);
        sp.Children.Add(_txtContado);

        // ── separador PLAN DE PAGO ────────────────────────────────────────
        sp.Children.Add(new Border
        {
            Background    = new SolidColorBrush(Color.FromRgb(60, 100, 200)),
            Margin        = new Thickness(0, 8, 0, 4),
            Padding       = new Thickness(0, 6, 0, 6),
            CornerRadius  = new CornerRadius(0),
            Child         = new TextBlock
            {
                Text       = "PLAN DE PAGO",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize   = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            }
        });

        // ── campos plan (label | input) ───────────────────────────────────
        sp.Children.Add(PRow("Precio",        out _txtPrecio,       ro: true,  bgRO: bgInput,    bgE: bgEditable, lbl: lblColor, fgRO: txtColor, brd: border));
        sp.Children.Add(PRow("% descuento",   out _txtPctDesc,      ro: false, bgRO: bgInput,    bgE: bgEditable, lbl: lblColor, fgRO: txtColor, brd: border, onChanged: () => RecalcPlan()));
        sp.Children.Add(PRow("Descuento",     out _txtDescuento,    ro: true,  bgRO: bgInput,    bgE: bgEditable, lbl: lblColor, fgRO: txtColor, brd: border));
        sp.Children.Add(PRow("Valor",         out _txtValor,        ro: true,  bgRO: bgInput,    bgE: bgEditable, lbl: lblColor, fgRO: txtColor, brd: border));
        sp.Children.Add(PRow("Entrega",       out _txtEntrega,      ro: false, bgRO: bgInput,    bgE: bgEditable, lbl: lblColor, fgRO: txtColor, brd: border, onChanged: () => RecalcPlan()));
        sp.Children.Add(PRow("Valor saldo",   out _txtValorSaldo,   ro: true,  bgRO: bgInput,    bgE: bgEditable, lbl: lblColor, fgRO: txtColor, brd: border));
        sp.Children.Add(PRow("% Recargo",     out _txtPctRec,       ro: false, bgRO: bgInput,    bgE: bgEditable, lbl: lblColor, fgRO: txtColor, brd: border, onChanged: () => RecalcPlan()));
        sp.Children.Add(PRow("Cant/Cuotas",   out _txtCantCuotas,   ro: false, bgRO: bgInput,    bgE: bgEditable, lbl: lblColor, fgRO: txtColor, brd: border, onChanged: () => RecalcPlan()));
        sp.Children.Add(PRow("Valor mensual", out _txtValorMensual, ro: true,  bgRO: bgInput,    bgE: bgEditable, lbl: lblColor, fgRO: txtColor, brd: border));

        // ── Valor Final — destacado ────────────────────────────────────────
        var bdrVF = new Border
        {
            Background      = new SolidColorBrush(Color.FromRgb(30, 35, 48)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(60, 100, 200)),
            BorderThickness = new Thickness(0, 2, 0, 0),
            Margin          = new Thickness(0, 6, 0, 0),
            Padding         = new Thickness(8, 6, 8, 6),
        };
        var gvf = new Grid();
        gvf.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gvf.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lblVF = new TextBlock
        {
            Text              = "Valor Final",
            FontWeight        = FontWeights.Bold,
            FontSize          = 13,
            Foreground        = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(lblVF, 0);
        gvf.Children.Add(lblVF);

        _txtValorFinal = new TextBox
        {
            IsReadOnly      = true,
            Padding         = new Thickness(8, 5, 8, 5),
            FontWeight      = FontWeights.Bold,
            FontSize        = 15,
            Foreground      = new SolidColorBrush(Color.FromRgb(255, 220, 50)),
            Background      = new SolidColorBrush(Color.FromRgb(40, 50, 80)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(60, 100, 200)),
            BorderThickness = new Thickness(2),
            TextAlignment   = TextAlignment.Right,
        };
        Grid.SetColumn(_txtValorFinal, 1);
        gvf.Children.Add(_txtValorFinal);

        bdrVF.Child = gvf;
        sp.Children.Add(bdrVF);

        // ── Máximo cuotas ─────────────────────────────────────────────────
        _lblMaxCuotas = new TextBlock
        {
            Text       = "Máximo  0  cuotas",
            FontWeight = FontWeights.Bold,
            FontSize   = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 200)),
            Margin     = new Thickness(8, 6, 8, 8),
            TextAlignment = TextAlignment.Center,
        };
        sp.Children.Add(_lblMaxCuotas);

        return new Border
        {
            Margin     = new Thickness(3, 0, 0, 0),
            Background = bgPanel,
            Child      = new ScrollViewer
            {
                Content = sp,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            },
        };
    }

    private static TextBlock PanelLabel(string txt, SolidColorBrush color) => new TextBlock
    {
        Text       = txt,
        Margin     = new Thickness(8, 6, 8, 2),
        FontSize   = 10,
        FontWeight = FontWeights.SemiBold,
        Foreground = color,
    };

    private static TextBox PanelField(SolidColorBrush bg, SolidColorBrush fg, SolidColorBrush brd) => new TextBox
    {
        IsReadOnly      = true,
        Margin          = new Thickness(8, 0, 8, 4),
        Padding         = new Thickness(6, 4, 6, 4),
        Background      = bg,
        Foreground      = fg,
        BorderBrush     = brd,
        BorderThickness = new Thickness(1),
        FontSize        = 12,
        FontWeight      = FontWeights.SemiBold,
    };

    // fila 2 columnas: label | input
    private static UIElement PRow(string label, out TextBox field,
        bool ro, SolidColorBrush bgRO, SolidColorBrush bgE,
        SolidColorBrush lbl, SolidColorBrush fgRO, SolidColorBrush brd,
        Action? onChanged = null)
    {
        var g = new Grid { Margin = new Thickness(8, 2, 8, 2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var tb = new TextBlock
        {
            Text              = label,
            FontSize          = 11,
            Foreground        = lbl,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 4, 0),
        };
        Grid.SetColumn(tb, 0);
        g.Children.Add(tb);

        field = new TextBox
        {
            IsReadOnly      = ro,
            Text            = ro ? string.Empty : "0",
            Padding         = new Thickness(6, 3, 6, 3),
            Background      = ro ? bgRO : bgE,
            Foreground      = ro ? fgRO : new SolidColorBrush(Color.FromRgb(20, 20, 20)),
            BorderBrush     = brd,
            BorderThickness = new Thickness(1),
            FontSize        = 12,
        };
        if (onChanged != null)
            field.TextChanged += (_, __) => onChanged();
        Grid.SetColumn(field, 1);
        g.Children.Add(field);

        return g;
    }

    private static TextBox MakePRO() => new TextBox { IsReadOnly = true };
    private static TextBox MakePE()  => new TextBox { Text = "0" };

    // ── pie ───────────────────────────────────────────────────────────────
    private UIElement BuildPie()
    {
        var panel = new DockPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
        };

        _lblTotal = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground        = Brushes.White,
            FontSize          = 12,
            Margin            = new Thickness(8, 6, 0, 6),
            Text              = "Cargando...",
        };
        DockPanel.SetDock(_lblTotal, Dock.Left);
        panel.Children.Add(_lblTotal);

        var btnCerrar = new Button
        {
            Content         = "Cerrar",
            Width           = 90,
            Padding         = new Thickness(0, 6, 0, 6),
            Background      = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor          = Cursors.Hand,
            Margin          = new Thickness(0, 4, 8, 4),
        };
        btnCerrar.Click += (_, __) => Close();
        DockPanel.SetDock(btnCerrar, Dock.Right);
        panel.Children.Add(btnCerrar);

        return panel;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Carga de datos
    // ══════════════════════════════════════════════════════════════════════
    private async Task CargarAsync()
    {
        _locales = (await _localRepo.ListarTodosAsync()).ToList();

        // agregar columnas dinámicas de stock — ANTES de asignar ItemsSource
        for (int i = 0; i < _locales.Count; i++)
        {
            var loc = _locales[i];
            _grid.Columns.Add(MakeColStock($"L{loc.IdLocal} {loc.NombreLocal}", i));
        }

        var (arts, prices) = await _artRepo.ObtenerVisorAsync();

        // agrupar: idArt → (idLocal → stock)
        var stocksMap = new Dictionary<int, Dictionary<int, decimal>>();
        var pricesMap = new Dictionary<int, Price>();

        foreach (var p in prices)
        {
            if (!stocksMap.ContainsKey(p.IdArt))
                stocksMap[p.IdArt] = new Dictionary<int, decimal>();
            stocksMap[p.IdArt][(int)p.IdLocal] = p.S;

            if (!pricesMap.ContainsKey(p.IdArt) && (p.Pventa > 0 || p.Contado > 0))
                pricesMap[p.IdArt] = p;
        }

        _todosList = arts.Select(art =>
        {
            var sm    = stocksMap.GetValueOrDefault(art.Id) ?? new();
            var price = pricesMap.GetValueOrDefault(art.Id);

            var stocks = new decimal[_locales.Count];
            for (int i = 0; i < _locales.Count; i++)
            {
                sm.TryGetValue(_locales[i].IdLocal, out var sv);
                stocks[i] = sv;
            }

            return new VisorRow
            {
                Codigo   = art.Ca,
                Desc     = art.D,
                Contado  = price?.Contado ?? 0,
                PVenta   = price?.Pventa  ?? 0,
                MaxCuota = art.Maxcuota,
                Stocks   = stocks,
            };
        }).ToList();

        _todos = new ObservableCollection<VisorRow>(_todosList);
        _grid.ItemsSource = _todos;
        _lblTotal.Text = $"Total: {_todos.Count} artículos";
    }

    // ══════════════════════════════════════════════════════════════════════
    // Filtrado
    // ══════════════════════════════════════════════════════════════════════
    private void FiltrarGrid()
    {
        if (_todosList.Count == 0) return;
        var term = _txtBuscar?.Text.Trim() ?? string.Empty;

        IEnumerable<VisorRow> result = _todosList;
        if (!string.IsNullOrEmpty(term))
        {
            if (_rbCodigo.IsChecked == true)
                result = _todosList.Where(r => r.Codigo.Contains(term, StringComparison.OrdinalIgnoreCase));
            else if (_rbDescripcion.IsChecked == true)
                result = _todosList.Where(r => r.Desc.Contains(term, StringComparison.OrdinalIgnoreCase));
            else
                result = _todosList.Where(r => r.Desc.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var lista = result.ToList();
        _grid.ItemsSource = lista;
        _lblTotal.Text = $"Total: {lista.Count} artículos";
    }

    // ══════════════════════════════════════════════════════════════════════
    // Color de filas
    // ══════════════════════════════════════════════════════════════════════
    private void OnGridLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is not VisorRow row) return;

        var total = row.TotalStock;
        if (total > 0)
            e.Row.Background = BrVerde;
        else if (total < 0)
            e.Row.Background = BrRojo;
        else
            e.Row.Background = BrNaranja;

        e.Row.Foreground = Brushes.Black;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Selección → panel derecho
    // ══════════════════════════════════════════════════════════════════════
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_grid.SelectedItem is not VisorRow row) { LimpiarPanel(); return; }

        _precioSel = row.PVenta;
        _maxcSel   = row.MaxCuota;

        _txtPanelCodigo.Text = row.Codigo;
        _txtPanelDesc.Text   = row.Desc;
        _txtContado.Text     = row.Contado.ToString("N0", _cult);
        _txtPrecio.Text      = row.PVenta.ToString("N0", _cult);
        _lblMaxCuotas.Text   = $"Máximo  {row.MaxCuota}  cuotas";

        _txtPctDesc.Text    = "0";
        _txtEntrega.Text    = "0";
        _txtPctRec.Text     = "0";
        _txtCantCuotas.Text = row.MaxCuota > 0 ? row.MaxCuota.ToString() : "1";

        RecalcPlan();
    }

    private void LimpiarPanel()
    {
        _txtPanelCodigo.Text  = string.Empty;
        _txtPanelDesc.Text    = string.Empty;
        _txtContado.Text      = string.Empty;
        _txtPrecio.Text       = string.Empty;
        _txtPctDesc.Text      = "0";
        _txtDescuento.Text    = string.Empty;
        _txtValor.Text        = string.Empty;
        _txtEntrega.Text      = "0";
        _txtValorSaldo.Text   = string.Empty;
        _txtPctRec.Text       = "0";
        _txtCantCuotas.Text   = "1";
        _txtValorMensual.Text = string.Empty;
        _txtValorFinal.Text   = string.Empty;
        _lblMaxCuotas.Text    = "Máximo  0  cuotas";
        _precioSel = 0; _maxcSel = 0;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Plan de pago
    // ══════════════════════════════════════════════════════════════════════
    private bool _calculando = false;
    private void RecalcPlan()
    {
        if (_calculando) return;
        _calculando = true;
        try
        {
            var precio = _precioSel;
            if (precio <= 0)
            {
                _txtDescuento.Text = _txtValor.Text = _txtValorSaldo.Text =
                    _txtValorMensual.Text = _txtValorFinal.Text = string.Empty;
                return;
            }

            decimal.TryParse(_txtPctDesc.Text, NumberStyles.Any, _cult, out var pctDesc);
            decimal.TryParse(_txtEntrega.Text, NumberStyles.Any, _cult, out var entrega);
            decimal.TryParse(_txtPctRec.Text,  NumberStyles.Any, _cult, out var pctRec);
            int.TryParse(_txtCantCuotas.Text, out var cuotas);
            if (cuotas <= 0) cuotas = 1;

            var descuento  = Math.Round(precio * pctDesc / 100, 0);
            var valor      = precio - descuento;
            var valorSaldo = valor - entrega;
            var recargo    = Math.Round(valorSaldo * pctRec / 100, 0);
            var mensual    = Math.Round((valorSaldo + recargo) / cuotas, 0);
            var valorFinal = entrega + mensual * cuotas;

            _txtDescuento.Text    = descuento.ToString("N0", _cult);
            _txtValor.Text        = valor.ToString("N0", _cult);
            _txtValorSaldo.Text   = valorSaldo.ToString("N0", _cult);
            _txtValorMensual.Text = mensual.ToString("N0", _cult);
            _txtValorFinal.Text   = valorFinal.ToString("N0", _cult);
        }
        finally { _calculando = false; }
    }
}
