using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Informes;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Maestros;

// ════════════════════════════════════════════════════════════════════════════
// Ventana principal — botonera lateral
// ════════════════════════════════════════════════════════════════════════════
public partial class ArticulosWindow : Window
{
    private readonly IArticuloRepository _artRepo;
    private readonly IMaestrosSeccionRepository _secRepo;
    private readonly IMaestrosCategoriaRepository _catRepo;
    private readonly IMaestrosSubcategoriaRepository _subRepo;
    private readonly IMaestrosMarcaRepository _marcaRepo;
    private readonly IMaestrosProveedorRepository _provRepo;
    private readonly IMaestrosMedidaRepository _medRepo;
    private readonly IMaestrosPaisRepository _paisRepo;
    private readonly ILocalRepository _localRepo;
    private ArtCaches? _caches;

    public ArticulosWindow()
    {
        InitializeComponent();
        var svc = App.Services;
        _artRepo   = svc.GetRequiredService<IArticuloRepository>();
        _secRepo   = svc.GetRequiredService<IMaestrosSeccionRepository>();
        _catRepo   = svc.GetRequiredService<IMaestrosCategoriaRepository>();
        _subRepo   = svc.GetRequiredService<IMaestrosSubcategoriaRepository>();
        _marcaRepo = svc.GetRequiredService<IMaestrosMarcaRepository>();
        _provRepo  = svc.GetRequiredService<IMaestrosProveedorRepository>();
        _medRepo   = svc.GetRequiredService<IMaestrosMedidaRepository>();
        _paisRepo  = svc.GetRequiredService<IMaestrosPaisRepository>();
        _localRepo = svc.GetRequiredService<ILocalRepository>();
    }

    private async Task EnsureCachesAsync()
    {
        if (_caches != null) return;
        _caches = new ArtCaches
        {
            Secs    = (await _secRepo.ListarTodosAsync()).ToList(),
            Cats    = (await _catRepo.ListarTodosAsync()).ToList(),
            Subs    = (await _subRepo.ListarTodosAsync()).ToList(),
            Marcas  = (await _marcaRepo.ListarTodosAsync()).ToList(),
            Provs   = (await _provRepo.ListarTodosAsync()).ToList(),
            Meds    = (await _medRepo.ListarTodosAsync()).ToList(),
            Paises  = (await _paisRepo.ListarTodosAsync()).ToList(),
            Locales = (await _localRepo.ListarTodosAsync()).ToList(),
        };
    }

    private async void OnNuevo(object s, RoutedEventArgs e)
    {
        await EnsureCachesAsync();
        new NuevoEditarArticuloDialog(_artRepo, _caches!, null) { Owner = this }.ShowDialog();
    }

    private async void OnEditarGeneral(object s, RoutedEventArgs e)
    {
        await EnsureCachesAsync();
        var artPick = await PickArticuloAsync("Seleccionar artículo para editar");
        if (artPick == null) return;
        var art = await _artRepo.BuscarPorIdAsync(artPick.Id) ?? artPick;
        new NuevoEditarArticuloDialog(_artRepo, _caches!, art) { Owner = this }.ShowDialog();
    }

    private async void OnEditarPrecios(object s, RoutedEventArgs e)
    {
        await EnsureCachesAsync();
        new EditarPreciosDialog(_artRepo, _caches!.Locales) { Owner = this }.ShowDialog();
    }

    private async void OnEditarStock(object s, RoutedEventArgs e)
    {
        await EnsureCachesAsync();
        new EditarStockDialog(_artRepo, _caches!.Locales) { Owner = this }.ShowDialog();
    }

    private async void OnEditarSeccCat(object s, RoutedEventArgs e)
    {
        await EnsureCachesAsync();
        new EditarSeccCatDialog(_artRepo, _caches!) { Owner = this }.ShowDialog();
    }

    private async void OnInhabilitar(object s, RoutedEventArgs e)
    {
        await EnsureCachesAsync();
        new InhabilitarHabilitarDialog(_artRepo, _caches!.Locales, inhabilitar: true) { Owner = this }.ShowDialog();
    }

    private async void OnHabilitar(object s, RoutedEventArgs e)
    {
        await EnsureCachesAsync();
        new InhabilitarHabilitarDialog(_artRepo, _caches!.Locales, inhabilitar: false) { Owner = this }.ShowDialog();
    }

    private void OnCerrar(object s, RoutedEventArgs e) => Close();

    private void OnImprimirVistaPrevia(object s, RoutedEventArgs e)
    {
        new BuscarArticuloDialog(_artRepo, "Imprimir / Vista previa de artículos",
            soloVisualizacion: true) { Owner = this }.ShowDialog();
    }

    private async Task<Articulo?> PickArticuloAsync(string titulo)
    {
        var dlg = new BuscarArticuloDialog(_artRepo, titulo) { Owner = this };
        return dlg.ShowDialog() == true ? dlg.Seleccionado : null;
    }

    private void OnWindowKeyDown(object s, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                OnNuevo(s, new RoutedEventArgs()); e.Handled = true; break;
            case Key.F2: OnEditarGeneral(s, new RoutedEventArgs()); e.Handled = true; break;
            case Key.F3: OnEditarPrecios(s, new RoutedEventArgs()); e.Handled = true; break;
            case Key.F4: OnEditarStock(s, new RoutedEventArgs()); e.Handled = true; break;
            case Key.F5: OnEditarSeccCat(s, new RoutedEventArgs()); e.Handled = true; break;
            case Key.F6: OnImprimirVistaPrevia(s, new RoutedEventArgs()); e.Handled = true; break;
        }
    }
}

// ── Caché de listas ──────────────────────────────────────────────────────────
public class ArtCaches
{
    public List<Seccion> Secs { get; set; } = new();
    public List<Categoria> Cats { get; set; } = new();
    public List<Subcategoria> Subs { get; set; } = new();
    public List<Marca> Marcas { get; set; } = new();
    public List<Proveedor> Provs { get; set; } = new();
    public List<Medida> Meds { get; set; } = new();
    public List<Pais> Paises { get; set; } = new();
    public List<Local> Locales { get; set; } = new();
}

// ════════════════════════════════════════════════════════════════════════════
// UI Helper — estilos modernos centralizados
// ════════════════════════════════════════════════════════════════════════════
public static class UiH
{
    // Paleta
    public static readonly Color Naranja    = Color.FromRgb(230, 126, 34);
    public static readonly Color NaranjaOsc = Color.FromRgb(190, 90, 10);
    public static readonly Color Verde      = Color.FromRgb(39, 174, 96);
    public static readonly Color Gris       = Color.FromRgb(85, 85, 85);
    public static readonly Color Azul       = Color.FromRgb(41, 128, 185);
    public static readonly Color Morado     = Color.FromRgb(142, 68, 173);
    public static readonly Color Rojo       = Color.FromRgb(192, 57, 43);
    public static readonly Color FondoDlg   = Color.FromRgb(44, 62, 80);
    public static readonly Color FondoPanel = Color.FromRgb(52, 73, 94);
    public static readonly Color FondoInput = Colors.White;
    public static readonly Color BorderInput = Color.FromRgb(189, 195, 199);
    public static readonly Color TextoLabel  = Color.FromRgb(236, 240, 241);
    public static readonly Color TextoSub    = Color.FromRgb(149, 165, 166);

    // ── TextBox moderno ──────────────────────────────────────────────────────
    public static TextBox Input(double width = double.NaN, string def = "", bool readOnly = false)
    {
        var tb = new TextBox
        {
            Text       = def,
            IsReadOnly = readOnly,
            Padding    = new Thickness(8, 5, 8, 5),
            Background = new SolidColorBrush(readOnly ? Color.FromRgb(236, 240, 241) : FondoInput),
            Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(BorderInput),
            BorderThickness = new Thickness(1),
            FontSize   = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (!double.IsNaN(width)) tb.Width = width;
        tb.Height = 28;
        return tb;
    }

    // ── ComboBox moderno ─────────────────────────────────────────────────────
    public static ComboBox Combo(double width, (string text, string tag)[] items)
    {
        var cbo = new ComboBox
        {
            Width = width, Height = 28,
            Padding = new Thickness(6, 2, 6, 2),
            Background = new SolidColorBrush(FondoInput),
            BorderBrush = new SolidColorBrush(BorderInput),
            BorderThickness = new Thickness(1),
            FontSize = 12,
        };
        foreach (var (t, tg) in items)
            cbo.Items.Add(new ComboBoxItem { Content = t, Tag = tg });
        cbo.SelectedIndex = 0;
        return cbo;
    }

    // ── Label de campo ───────────────────────────────────────────────────────
    public static TextBlock Label(string text, bool small = false) => new TextBlock
    {
        Text       = text,
        Foreground = new SolidColorBrush(TextoLabel),
        FontSize   = small ? 10 : 11,
        FontWeight = FontWeights.SemiBold,
        Margin     = new Thickness(0, 0, 0, 3),
    };

    // ── Campo con etiqueta ───────────────────────────────────────────────────
    public static StackPanel FieldGroup(string lbl, out TextBox txt, double width = double.NaN,
        string def = "", bool readOnly = false, double marginRight = 10)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, marginRight, 8) };
        sp.Children.Add(Label(lbl));
        txt = Input(width, def, readOnly);
        sp.Children.Add(txt);
        return sp;
    }

    // ── TextBox numérico con separador de miles y decimales en tiempo real ───
    // Formato: puntos para miles, coma para decimales  →  1.500.000,50
    private static readonly System.Globalization.CultureInfo _cult =
        System.Globalization.CultureInfo.GetCultureInfo("es-AR");

    public static TextBox NumericInput(double width = double.NaN, decimal def = 0)
    {
        var tb = new TextBox
        {
            Text = def == 0 ? "0" : def.ToString("N2", _cult).TrimEnd('0').TrimEnd(','),
            Padding = new Thickness(8, 5, 8, 5),
            Background = new SolidColorBrush(FondoInput),
            Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(BorderInput),
            BorderThickness = new Thickness(1),
            FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
        };
        if (!double.IsNaN(width)) tb.Width = width;
        tb.Height = 28;

        bool _upd = false;

        // Solo dígitos, una coma y backspace/delete
        tb.PreviewTextInput += (_, e) =>
        {
            var c = e.Text;
            if (c == "," || c == ".")
            {
                // permitir solo una coma decimal
                e.Handled = tb.Text.Contains(',');
                if (!e.Handled)
                {
                    // insertar coma en la posición del cursor sin disparar el formato
                    _upd = true;
                    var pos = tb.CaretIndex;
                    // quitar puntos de miles para insertar la coma limpia
                    var sin = tb.Text.Replace(".", "");
                    var posReal = tb.Text.Take(pos).Count(x => x != '.');
                    sin = sin.Insert(Math.Min(posReal, sin.Length), ",");
                    tb.Text = FormatNumeric(sin);
                    // buscar nueva posición: justo después de la coma
                    tb.CaretIndex = tb.Text.IndexOf(',') + 1;
                    _upd = false;
                }
                e.Handled = true; // siempre consumir — ya lo insertamos arriba
                return;
            }
            e.Handled = !char.IsDigit(c[0]);
        };

        DataObject.AddPastingHandler(tb, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var p = (string)e.DataObject.GetData(DataFormats.Text);
                if (!p.All(c => char.IsDigit(c) || c == ',' || c == '.'))
                    e.CancelCommand();
            }
            else e.CancelCommand();
        });

        tb.TextChanged += (_, __) =>
        {
            if (_upd) return;
            _upd = true;

            var caretPos = tb.CaretIndex;
            var digitsBeforeCaret = tb.Text.Take(caretPos).Count(char.IsDigit);

            var formatted = FormatNumeric(tb.Text);
            tb.Text = formatted;

            // Restaurar cursor contando dígitos
            var newCaret = 0;
            var digits = 0;
            foreach (var c in formatted)
            {
                if (digits == digitsBeforeCaret) break;
                if (char.IsDigit(c)) digits++;
                newCaret++;
            }
            tb.CaretIndex = Math.Min(newCaret, formatted.Length);

            _upd = false;
        };

        tb.LostFocus += (_, __) =>
        {
            if (string.IsNullOrWhiteSpace(tb.Text)) tb.Text = "0";
        };

        return tb;
    }

    // Formatea una cadena numérica con punto de miles y coma decimal
    private static string FormatNumeric(string raw)
    {
        // Separar parte entera y decimal
        raw = raw.Replace(".", ""); // quitar puntos de miles anteriores
        var parts = raw.Split(',');
        var intPart = parts[0].TrimStart('0');
        if (intPart == "") intPart = "0";
        var decPart = parts.Length > 1 ? parts[1] : null;

        // Formato miles en parte entera
        if (long.TryParse(intPart, out var n))
            intPart = n.ToString("#,0", _cult); // usa punto como sep miles en es-AR

        return decPart != null ? $"{intPart},{decPart}" : intPart;
    }

    // ── Campo numérico con etiqueta (separador de miles) ─────────────────────
    public static StackPanel NumericFieldGroup(string lbl, out TextBox txt,
        double width = double.NaN, decimal def = 0, double marginRight = 10)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, marginRight, 8) };
        sp.Children.Add(Label(lbl));
        txt = NumericInput(width, def);
        sp.Children.Add(txt);
        return sp;
    }

    // ── Leer valor decimal desde NumericInput (punto=miles, coma=decimal) ────
    public static decimal ReadDecimal(TextBox tb)
    {
        var raw = tb.Text.Trim();
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Number, _cult, out var v) ? v : 0;
    }

    // ── Combo con etiqueta ───────────────────────────────────────────────────
    public static StackPanel ComboGroup(string lbl, out ComboBox cbo,
        (string text, string tag)[] items, double width, double marginRight = 10)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, marginRight, 8) };
        sp.Children.Add(Label(lbl));
        cbo = Combo(width, items);
        sp.Children.Add(cbo);
        return sp;
    }

    // ── Campo lupa (botón + display readonly) ────────────────────────────────
    public static StackPanel LupaGroup(string lbl, out TextBox display, Action onPick,
        double displayWidth = 160, double marginRight = 10)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, marginRight, 8) };
        sp.Children.Add(Label(lbl));

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var btn = LupaBtn();
        var cap = onPick;
        btn.Click += (_, __) => cap();
        System.Windows.Controls.Grid.SetColumn(btn, 0);
        row.Children.Add(btn);

        display = new TextBox
        {
            IsReadOnly = true, Height = 28,
            Padding    = new Thickness(8, 5, 8, 5),
            Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
            Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(BorderInput),
            BorderThickness = new Thickness(1, 1, 1, 1),
            FontSize   = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (!double.IsNaN(displayWidth)) display.Width = displayWidth;
        System.Windows.Controls.Grid.SetColumn(display, 1);
        row.Children.Add(display);

        sp.Children.Add(row);
        return sp;
    }

    // ── Campo lupa con código separado (botón + [código] + [nombre]) ─────────
    public static StackPanel LupaGroupWithCode(string lbl, out TextBox codigo, out TextBox nombre,
        Action onPick, double codigoWidth = 60, double nombreWidth = 140, double marginRight = 10)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, marginRight, 8) };
        sp.Children.Add(Label(lbl));

        var row = new StackPanel { Orientation = Orientation.Horizontal };

        var btn = LupaBtn();
        btn.Click += (_, __) => onPick();
        row.Children.Add(btn);

        codigo = new TextBox
        {
            IsReadOnly = true, Height = 28, Width = codigoWidth,
            Padding = new Thickness(6, 5, 6, 5),
            Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
            Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(BorderInput),
            BorderThickness = new Thickness(1),
            FontSize = 11,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0),
        };
        row.Children.Add(codigo);

        nombre = new TextBox
        {
            IsReadOnly = true, Height = 28, Width = nombreWidth,
            Padding = new Thickness(8, 5, 8, 5),
            Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
            Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(BorderInput),
            BorderThickness = new Thickness(1),
            FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(nombre);

        sp.Children.Add(row);
        return sp;
    }

    // ── Botón lupa ───────────────────────────────────────────────────────────
    public static Button LupaBtn() => new Button
    {
        Content = "⌕", Width = 30, Height = 28,
        Background = new SolidColorBrush(Azul),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        FontSize = 14, FontWeight = FontWeights.Bold,
        Padding = new Thickness(0),
        Margin = new Thickness(0, 0, 4, 0),
        VerticalAlignment = VerticalAlignment.Center,
        ToolTip = "Buscar",
    };

    // ── Botón de acción ──────────────────────────────────────────────────────
    public static Button Btn(string text, Color bg, double minWidth = 90) => new Button
    {
        Content = text,
        MinWidth = minWidth, Height = 32,
        Padding = new Thickness(14, 0, 14, 0),
        Background = new SolidColorBrush(bg),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        FontSize = 12, FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(6, 0, 0, 0),
        Cursor = System.Windows.Input.Cursors.Hand,
    };

    // ── Panel sección con título ─────────────────────────────────────────────
    public static Border SectionPanel(string titulo, out StackPanel content, bool dark = true)
    {
        var bg = dark ? FondoPanel : Color.FromRgb(62, 86, 108);
        var outer = new Border
        {
            Background = new SolidColorBrush(bg),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 10),
        };
        var inner = new StackPanel();
        if (!string.IsNullOrEmpty(titulo))
        {
            inner.Children.Add(new TextBlock
            {
                Text = titulo.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                FontSize = 10, FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8),
            });
        }
        content = inner;
        outer.Child = inner;
        return outer;
    }

    // ── DataGrid moderno ─────────────────────────────────────────────────────
    public static DataGrid ModernGrid()
    {
        var dg = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Background = Brushes.White,
            RowBackground = Brushes.White,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(BorderInput),
            FontSize = 12,
            CanUserReorderColumns = false,
            CanUserResizeRows = false,
            SelectionUnit = DataGridSelectionUnit.FullRow,
        };
        // Header style
        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Color.FromRgb(52, 73, 94))));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, new SolidColorBrush(Color.FromRgb(236, 240, 241))));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 6, 8, 6)));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(80, 100, 120))));
        dg.ColumnHeaderStyle = headerStyle;

        var darkText = new SolidColorBrush(Color.FromRgb(20, 20, 20));
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty, Brushes.White));
        rowStyle.Setters.Add(new Setter(DataGridRow.ForegroundProperty, darkText));
        rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
        // Hover: amarillo claro
        var hoverTrigger = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 236, 179))));
        hoverTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, darkText));
        rowStyle.Triggers.Add(hoverTrigger);
        // Seleccionado: naranja con texto negro negrita
        var selTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        selTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 165, 0))));
        selTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, darkText));
        selTrigger.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.Bold));
        rowStyle.Triggers.Add(selTrigger);
        dg.RowStyle = rowStyle;

        // CellStyle: Transparent en base y en triggers para nunca tapar el color de la fila
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        var cellSelTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        cellSelTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        cellSelTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty, darkText));
        cellSelTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
        cellStyle.Triggers.Add(cellSelTrigger);
        var cellHoverTrigger = new Trigger { Property = DataGridCell.IsMouseOverProperty, Value = true };
        cellHoverTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        cellStyle.Triggers.Add(cellHoverTrigger);
        dg.CellStyle = cellStyle;

        return dg;
    }

    public static DataGridTextColumn Col(string header, string path, double width, bool star = false)
    {
        var col = new DataGridTextColumn
        {
            Header = header,
            Binding = new System.Windows.Data.Binding(path),
        };
        col.Width = star ? new DataGridLength(1, DataGridLengthUnitType.Star)
                         : width > 0 ? new DataGridLength(width) : DataGridLength.Auto;
        return col;
    }

    // ── Grid helper ─────────────────────────────────────────────────────────
    public static Grid MkGrid(string[] rows)
    {
        var g = new Grid();
        foreach (var r in rows)
        {
            if (r == "*")             g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            else if (r == "Auto")     g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            else if (double.TryParse(r, out var d)) g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(d) });
            else g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        return g;
    }

    public static void SetRow(UIElement el, int row) => System.Windows.Controls.Grid.SetRow(el, row);

    // ── Separador ────────────────────────────────────────────────────────────
    public static Border Separator() => new Border
    {
        Height = 1,
        Background = new SolidColorBrush(Color.FromRgb(80, 100, 120)),
        Margin = new Thickness(0, 4, 0, 10),
    };

    // ── Barra de botones inferior ────────────────────────────────────────────
    public static StackPanel BtnBar(params Button[] btns)
    {
        var sp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        foreach (var b in btns) sp.Children.Add(b);
        return sp;
    }

    // ── Hint de atajos ───────────────────────────────────────────────────────
    public static TextBlock Hint(string text) => new TextBlock
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
        FontSize = 10,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0),
    };
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: Búsqueda de artículo
// ════════════════════════════════════════════════════════════════════════════
public class BuscarArticuloDialog : Window
{
    private readonly IArticuloRepository _repo;
    private TextBox        _txtBuscar      = null!;
    private ComboBox       _cboEstado      = null!;
    private TextBlock      _lblConteo      = null!;
    private TextBlock      _lblMarcaTag    = null!;
    private Button         _btnMarca       = null!;
    private DataGrid       _grid           = null!;
    private TextBlock      _lblPag         = null!;
    private ComboBox       _cboPorPagina   = null!;
    private List<Articulo> _todos          = new();
    private List<Articulo> _filtrados      = new();
    private List<string>   _marcas         = new();
    private Dictionary<int, (decimal pventa, decimal contado, decimal stock)> _priceMap = new();
    private string         _marcaActiva    = "";
    private int            _pagActual      = 1;
    private int            _porPagina      = 50;
    public  Articulo?      Seleccionado       { get; private set; }
    private readonly bool  _soloVisualizacion;

    public BuscarArticuloDialog(IArticuloRepository repo, string titulo, bool soloVisualizacion = false)
    {
        _repo               = repo;
        _soloVisualizacion  = soloVisualizacion;
        Title  = titulo;
        Width  = 860; Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        Content = Build();
        KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Escape) { DialogResult = false; return; }
            if (ke.Key == Key.Enter)  { Aceptar(); ke.Handled = true; }
        };
        Loaded += async (_, __) => await CargarTodosAsync();
    }

    private UIElement Build()
    {
        // root: fila 0=filtros, fila 1=grid, fila 2=paginador, fila 3=pie
        var root = UiH.MkGrid(rows: new[] { "Auto", "*", "Auto", "Auto" });
        root.Margin = new Thickness(14);

        // ── Fila 0: Buscar + Filtrar marcas + Estado ──────────────────────────
        var panelTop = new Border
        {
            Background   = new SolidColorBrush(UiH.FondoPanel),
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(10, 7, 10, 7),
            Margin       = new Thickness(0, 0, 0, 6),
        };
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        TextBlock Lbl(string t, double ml = 0) => new TextBlock
        {
            Text = t, Foreground = new SolidColorBrush(UiH.TextoLabel),
            VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold,
            FontSize = 11, Margin = new Thickness(ml, 0, 6, 0),
        };

        var lblB = Lbl("Buscar:"); lblB.Margin = new Thickness(0, 0, 6, 0);
        System.Windows.Controls.Grid.SetColumn(lblB, 0); topGrid.Children.Add(lblB);

        _txtBuscar = UiH.Input(); _txtBuscar.Height = 28;
        _txtBuscar.TextChanged += (_, __) => { _pagActual = 1; FiltrarEnMemoria(); };
        System.Windows.Controls.Grid.SetColumn(_txtBuscar, 1); topGrid.Children.Add(_txtBuscar);

        // Botón Filtrar marcas — abre modal
        _btnMarca = new Button
        {
            Height          = 28,
            Padding         = new Thickness(10, 0, 10, 0),
            FontSize        = 11,
            FontWeight      = FontWeights.SemiBold,
            Foreground      = Brushes.White,
            Background      = new SolidColorBrush(Color.FromRgb(62, 86, 108)),
            BorderThickness = new Thickness(0),
            Cursor          = System.Windows.Input.Cursors.Hand,
            Margin          = new Thickness(8, 0, 0, 0),
        };
        _lblMarcaTag = new TextBlock { Text = "▼  Filtrar marcas", VerticalAlignment = VerticalAlignment.Center };
        _btnMarca.Content = _lblMarcaTag;
        _btnMarca.Click   += OnBtnMarcaClick;
        System.Windows.Controls.Grid.SetColumn(_btnMarca, 2); topGrid.Children.Add(_btnMarca);

        var lblE = Lbl("Estado:", 12);
        System.Windows.Controls.Grid.SetColumn(lblE, 3); topGrid.Children.Add(lblE);
        _cboEstado = new ComboBox { Height = 28, FontSize = 11, Style = (Style)Application.Current.Resources["DarkComboBox"] };
        foreach (var (txt, tag) in new[] { ("Todos", ""), ("Activo", "Activo"), ("Inactivo", "Inactivo") })
            _cboEstado.Items.Add(new ComboBoxItem { Content = txt, Tag = tag });
        _cboEstado.SelectedIndex = 1;
        _cboEstado.SelectionChanged += (_, __) => { _pagActual = 1; FiltrarEnMemoria(); };
        System.Windows.Controls.Grid.SetColumn(_cboEstado, 4); topGrid.Children.Add(_cboEstado);

        panelTop.Child = topGrid;
        UiH.SetRow(panelTop, 0); root.Children.Add(panelTop);

        // ── Fila 1: Grid ──────────────────────────────────────────────────────
        _grid = UiH.ModernGrid();
        _grid.Columns.Add(UiH.Col("Código",      "Ca",          95));
        _grid.Columns.Add(UiH.Col("Descripción", "D",           0, star: true));
        _grid.Columns.Add(UiH.Col("Marca",       "MarcaNombre", 140));
        _grid.Columns.Add(UiH.Col("Estado",      "EstadoTexto", 70));
        _grid.MouseDoubleClick += (_, __) => Aceptar();
        var gridBorder = new Border
        {
            Child = _grid, CornerRadius = new CornerRadius(6),
            ClipToBounds = true, Margin = new Thickness(0, 0, 0, 4)
        };
        UiH.SetRow(gridBorder, 1); root.Children.Add(gridBorder);

        // ── Fila 2: Paginador ─────────────────────────────────────────────────
        var paginador = new Border
        {
            Background   = new SolidColorBrush(UiH.FondoPanel),
            CornerRadius = new CornerRadius(4),
            Padding      = new Thickness(10, 5, 10, 5),
            Margin       = new Thickness(0, 0, 0, 6),
        };
        var pagRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        Button PagBtn(string lbl) => new Button
        {
            Content         = lbl,
            Height          = 26, MinWidth = 28,
            Padding         = new Thickness(6, 0, 6, 0),
            FontSize        = 11, FontWeight = FontWeights.Bold,
            Foreground      = Brushes.White,
            Background      = new SolidColorBrush(Color.FromRgb(55, 80, 100)),
            BorderThickness = new Thickness(0),
            Cursor          = System.Windows.Input.Cursors.Hand,
            Margin          = new Thickness(2, 0, 2, 0),
        };

        var btnPrev = PagBtn("◀");
        var btnNext = PagBtn("▶");
        _lblPag = new TextBlock
        {
            Text = "", Foreground = new SolidColorBrush(UiH.TextoLabel),
            VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
            Margin = new Thickness(8, 0, 8, 0), MinWidth = 110,
            TextAlignment = TextAlignment.Center,
        };
        btnPrev.Click += (_, __) => { if (_pagActual > 1) { _pagActual--; AplicarPagina(); } };
        btnNext.Click += (_, __) =>
        {
            if (_pagActual < TotalPaginas()) { _pagActual++; AplicarPagina(); }
        };

        _lblConteo = new TextBlock
        {
            Text = "", Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 100)),
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 14, 0),
        };

        // Selector ítems por página
        var lblPP = new TextBlock
        {
            Text = "Mostrar:", Foreground = new SolidColorBrush(UiH.TextoSub),
            FontSize = 10, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 4, 0),
        };
        _cboPorPagina = new ComboBox { Height = 24, FontSize = 11, Width = 60, Style = (Style)Application.Current.Resources["DarkComboBox"] };
        foreach (var n in new[] { 5, 10, 20, 30, 50, 100 })
            _cboPorPagina.Items.Add(new ComboBoxItem { Content = n.ToString(), Tag = n });
        _cboPorPagina.SelectedIndex = 4; // 50 por defecto
        _cboPorPagina.SelectionChanged += (_, __) =>
        {
            if (_cboPorPagina.SelectedItem is ComboBoxItem ci && ci.Tag is int n)
            {
                _porPagina = n; _pagActual = 1; AplicarPagina();
            }
        };

        pagRow.Children.Add(btnPrev);
        pagRow.Children.Add(_lblPag);
        pagRow.Children.Add(btnNext);
        pagRow.Children.Add(_lblConteo);
        pagRow.Children.Add(lblPP);
        pagRow.Children.Add(_cboPorPagina);

        paginador.Child = pagRow;
        UiH.SetRow(paginador, 2); root.Children.Add(paginador);

        // ── Fila 3: Pie ───────────────────────────────────────────────────────
        var bot = new Grid();
        bot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hint = UiH.Hint("Escribir filtra   Doble-clic o Enter: Seleccionar   Esc: Cancelar");
        hint.VerticalAlignment = VerticalAlignment.Center;
        System.Windows.Controls.Grid.SetColumn(hint, 0); bot.Children.Add(hint);

        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var btnVP  = UiH.Btn("Vista previa",  Color.FromRgb(21, 101, 192));
        var btnIm  = UiH.Btn("Imprimir",      Color.FromRgb(27,  94,  32));
        var btnImT = UiH.Btn("Imprimir todo", Color.FromRgb(100, 60,  160));
        btnVP.Click  += (_, __) => AbrirVistaPrevia(imprimir: false);
        btnIm.Click  += (_, __) => AbrirVistaPrevia(imprimir: true);
        btnImT.Click += (_, __) => ImprimirTodo();
        btns.Children.Add(btnVP); btns.Children.Add(btnIm); btns.Children.Add(btnImT);

        if (!_soloVisualizacion)
        {
            var btnOk = UiH.Btn("✓  Aceptar",  UiH.Verde);
            var btnCx = UiH.Btn("✕  Cancelar", UiH.Gris);
            btnOk.Click += (_, __) => Aceptar();
            btnCx.Click += (_, __) => { DialogResult = false; };
            btns.Children.Add(btnOk); btns.Children.Add(btnCx);
        }
        else
        {
            var btnCx = UiH.Btn("✕  Cerrar", UiH.Gris);
            btnCx.Click += (_, __) => Close();
            btns.Children.Add(btnCx);
        }
        System.Windows.Controls.Grid.SetColumn(btns, 1); bot.Children.Add(btns);
        UiH.SetRow(bot, 3); root.Children.Add(bot);

        return root;
    }

    // ── Modal de marcas ───────────────────────────────────────────────────────
    private void OnBtnMarcaClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_marcaActiva))
        {
            SeleccionarMarca(""); return;
        }
        var dlg = new SeleccionarMarcaDialog(_marcas, _marcaActiva) { Owner = this };
        if (dlg.ShowDialog() == true)
            SeleccionarMarca(dlg.MarcaSeleccionada);
    }

    private void SeleccionarMarca(string valor)
    {
        _marcaActiva = valor;
        _pagActual   = 1;
        _lblMarcaTag.Text = string.IsNullOrEmpty(valor)
            ? "▼  Filtrar marcas"
            : $"✕  {valor}";
        _btnMarca.Background = new SolidColorBrush(string.IsNullOrEmpty(valor)
            ? Color.FromRgb(62, 86, 108)
            : Color.FromRgb(230, 126, 34));
        FiltrarEnMemoria();
    }

    // ── Paginación ────────────────────────────────────────────────────────────
    private int TotalPaginas() =>
        _filtrados.Count == 0 ? 1 : (_filtrados.Count + _porPagina - 1) / _porPagina;

    private void AplicarPagina()
    {
        int total = TotalPaginas();
        _pagActual = Math.Max(1, Math.Min(_pagActual, total));
        _grid.ItemsSource = _filtrados
            .Skip((_pagActual - 1) * _porPagina)
            .Take(_porPagina)
            .ToList();
        _lblPag.Text    = $"Página {_pagActual} / {total}";
        _lblConteo.Text = $"{_filtrados.Count:N0} artículos";
    }

    // ── Vista previa / Imprimir ──────────────────────────────────────────────
    private void AbrirVistaPrevia(bool imprimir)
    {
        var filtroDesc = new List<string>();
        if (!string.IsNullOrWhiteSpace(_txtBuscar.Text)) filtroDesc.Add($"Texto: {_txtBuscar.Text.Trim()}");
        if (!string.IsNullOrEmpty(_marcaActiva))         filtroDesc.Add($"Marca: {_marcaActiva}");
        var estadoTag = (_cboEstado.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        if (!string.IsNullOrEmpty(estadoTag))            filtroDesc.Add($"Estado: {estadoTag}");

        // Usar la página visible actual (respeta el filtro Y la paginación)
        var fuenteDatos = (_grid.ItemsSource as IEnumerable<Articulo>)?.ToList() ?? new List<Articulo>();

        var pagina = new ArticulosPagina
        {
            Filas = fuenteDatos.Select(a => new FilaArticuloImp(
                Codigo:       a.Ca,
                Descripcion:  a.D,
                Gravada:      a.Gra == 1 ? "Sí" : "No",
                Iva:          a.Iva > 0 ? $"{a.Iva:0}%" : "—",
                MaxCuota:     a.Maxcuota > 0 ? a.Maxcuota.ToString() : "—",
                StockMinimo:  a.Smin > 0 ? a.Smin.ToString("N0") : "—",
                SoloContado:  a.Slc == 1 ? "Sí" : "No",
                Seccion:      a.SeccionNombre,
                Proveedor:    a.ProveedorNombre,
                Subcategoria: a.SubcategoriaNombre,
                Categoria:    a.CategoriaNombre,
                Pais:         a.PaisNombre,
                Marca:        a.MarcaNombre,
                UnidadMedida: a.MedidaNombre,
                Estado:       a.EstadoTexto
            )).ToList(),
            FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "",
            Filtro   = filtroDesc.Count > 0 ? string.Join("  |  ", filtroDesc) : "",
            LogoPath = ArticulosPagina.ResolverLogoPath(),
        };

        if (imprimir)
            ArticulosImpresora.Imprimir(pagina, this);
        else
            new ArticulosPreviewWindow(pagina) { Owner = this }.ShowDialog();
    }

    private void ImprimirTodo()
    {
        if (_filtrados.Count == 0) return;

        var filtroDesc = new List<string>();
        if (!string.IsNullOrWhiteSpace(_txtBuscar.Text)) filtroDesc.Add($"Texto: {_txtBuscar.Text.Trim()}");
        if (!string.IsNullOrEmpty(_marcaActiva))         filtroDesc.Add($"Marca: {_marcaActiva}");
        var estadoTag = (_cboEstado.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        if (!string.IsNullOrEmpty(estadoTag))            filtroDesc.Add($"Estado: {estadoTag}");

        var pagina = new ArticulosPagina
        {
            Filas = _filtrados.Select(a => new FilaArticuloImp(
                Codigo:       a.Ca,
                Descripcion:  a.D,
                Gravada:      a.Gra == 1 ? "Sí" : "No",
                Iva:          a.Iva > 0 ? $"{a.Iva:0}%" : "—",
                MaxCuota:     a.Maxcuota > 0 ? a.Maxcuota.ToString() : "—",
                StockMinimo:  a.Smin > 0 ? a.Smin.ToString("N0") : "—",
                SoloContado:  a.Slc == 1 ? "Sí" : "No",
                Seccion:      a.SeccionNombre,
                Proveedor:    a.ProveedorNombre,
                Subcategoria: a.SubcategoriaNombre,
                Categoria:    a.CategoriaNombre,
                Pais:         a.PaisNombre,
                Marca:        a.MarcaNombre,
                UnidadMedida: a.MedidaNombre,
                Estado:       a.EstadoTexto
            )).ToList(),
            FechaImp = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "",
            Filtro   = filtroDesc.Count > 0 ? string.Join("  |  ", filtroDesc) : "",
            LogoPath = ArticulosPagina.ResolverLogoPath(),
        };

        ArticulosImpresora.Imprimir(pagina, this);
    }

    private async Task CargarTodosAsync()
    {
        var (arts, prices) = await _repo.ObtenerVisorAsync();
        _todos    = arts.ToList();
        _priceMap = prices
            .GroupBy(p => p.IdArt)
            .ToDictionary(g => g.Key, g => (pventa: g.Max(p => p.Pventa), contado: g.Max(p => p.Contado), stock: g.Sum(p => p.S)));

        _marcas = _todos
            .Select(a => a.MarcaNombre)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct().OrderBy(m => m).ToList();

        FiltrarEnMemoria();
        _txtBuscar.Focus();
    }

    private void FiltrarEnMemoria()
    {
        var term   = _txtBuscar.Text.Trim().ToLowerInvariant();
        var estado = (_cboEstado.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

        var resultado = _todos.AsEnumerable();

        if (!string.IsNullOrEmpty(term))
            resultado = resultado.Where(a =>
                a.Ca.ToLowerInvariant().Contains(term) ||
                a.D.ToLowerInvariant().Contains(term));

        if (!string.IsNullOrEmpty(_marcaActiva))
            resultado = resultado.Where(a => a.MarcaNombre == _marcaActiva);

        if (!string.IsNullOrEmpty(estado))
            resultado = resultado.Where(a => a.EstadoTexto == estado);

        _filtrados = resultado.ToList();
        AplicarPagina();
    }

    private void Aceptar()
    {
        if (_grid.SelectedItem is Articulo a) { Seleccionado = a; DialogResult = true; }
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: Lupa genérica
// ════════════════════════════════════════════════════════════════════════════
public class LupaDialog : Window
{
    static readonly SolidColorBrush LBrPrimary = new(Color.FromRgb(18,  78, 120));
    static readonly SolidColorBrush LBrAzul    = new(Color.FromRgb(31, 119, 180));
    static readonly SolidColorBrush LBrFondo   = new(Color.FromRgb(238, 242, 247));
    static readonly SolidColorBrush LBrCard    = Brushes.White;
    static readonly SolidColorBrush LBrBorde   = new(Color.FromRgb(208, 218, 232));
    static readonly SolidColorBrush LBrLabel   = new(Color.FromRgb(60,  80, 100));
    static readonly SolidColorBrush LBrSub     = new(Color.FromRgb(100, 120, 140));
    static readonly SolidColorBrush LBrVerde   = new(Color.FromRgb(30,  110, 66));
    static readonly SolidColorBrush LBrGris    = new(Color.FromRgb(100, 116, 132));

    private readonly string _displayProp;
    private readonly string _titulo;
    private readonly List<object> _todos;
    private readonly Func<Window, Task<object?>>? _onNuevo;
    private DataGrid _grid = null!;
    private TextBox _txtFiltro = null!;
    private TextBlock _lblConteo = null!;
    public object? Seleccionado { get; private set; }

    public LupaDialog(string titulo, IEnumerable<object> items, string displayProp, Func<Window, Task<object?>>? onNuevo = null)
    {
        _displayProp = displayProp;
        _titulo = titulo;
        _todos = items.ToList();
        _onNuevo = onNuevo;
        Title = titulo;
        Width = 460; Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = LBrFondo;
        ResizeMode = ResizeMode.NoResize;
        Content = Build();
        KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Escape) { DialogResult = false; }
            if (ke.Key == Key.Enter)  { Aceptar(); ke.Handled = true; }
            if (ke.Key == Key.F3 || (ke.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control))
                _txtFiltro.Focus();
        };
        Loaded += (_, __) => _txtFiltro.Focus();
    }

    private UIElement Build()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // buscar
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // footer

        // ── Header ───────────────────────────────────────────────────────────
        var hdrGrid = new Grid { Margin = new Thickness(16, 0, 16, 0) };
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var accent = new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = LBrAzul, Margin = new Thickness(0, 14, 10, 14) };
        var hdrSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 14, 0, 14) };
        hdrSp.Children.Add(new TextBlock { Text = _titulo.ToUpperInvariant(), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
        hdrSp.Children.Add(new TextBlock { Text = "Seleccioná un elemento de la lista", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(170, 204, 232)), Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(accent, 0); hdrGrid.Children.Add(accent);
        Grid.SetColumn(hdrSp, 1);  hdrGrid.Children.Add(hdrSp);
        var hdrBorder = new Border { Background = LBrPrimary, Child = hdrGrid };
        Grid.SetRow(hdrBorder, 0); root.Children.Add(hdrBorder);

        // ── Buscar ────────────────────────────────────────────────────────────
        var buscarCard = new Border {
            Background = LBrCard, BorderBrush = LBrBorde, BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 16, 10)
        };
        var buscarRow = new Grid();
        buscarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        buscarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buscarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var lupa = new TextBlock { Text = "⌕", FontSize = 15, Foreground = LBrSub, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        _txtFiltro = new TextBox {
            Height = 32, FontSize = 12,
            Padding = new Thickness(4, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.White, BorderBrush = LBrBorde, BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.FromRgb(20, 31, 48))
        };
        _txtFiltro.TextChanged += (_, __) => Filtrar();
        _lblConteo = new TextBlock {
            FontSize = 10, Foreground = LBrSub,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
        };
        ActualizarConteo(_todos.Count);
        Grid.SetColumn(lupa,      0); buscarRow.Children.Add(lupa);
        Grid.SetColumn(_txtFiltro,1); buscarRow.Children.Add(_txtFiltro);
        Grid.SetColumn(_lblConteo,2); buscarRow.Children.Add(_lblConteo);
        buscarCard.Child = buscarRow;
        Grid.SetRow(buscarCard, 1); root.Children.Add(buscarCard);

        // ── Grid ──────────────────────────────────────────────────────────────
        _grid = BuildGrid();
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Nombre / Descripción",
            Binding = new System.Windows.Data.Binding(_displayProp),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        _grid.ItemsSource = _todos;
        _grid.MouseDoubleClick += (_, __) => Aceptar();
        var gridWrap = new Border { Child = _grid, Margin = new Thickness(0) };
        Grid.SetRow(gridWrap, 2); root.Children.Add(gridWrap);

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new Border {
            Background = LBrCard, BorderBrush = LBrBorde, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 10, 16, 10)
        };
        var footerRow = new Grid();
        footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hint = new TextBlock { Text = "Enter: Seleccionar   Esc: Cancelar   Ctrl+F: Buscar", FontSize = 10, Foreground = LBrSub, VerticalAlignment = VerticalAlignment.Center };
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        if (_onNuevo != null)
        {
            var bNuevo = LBtn("+  Nuevo", LBrAzul);
            bNuevo.Click += async (_, __) =>
            {
                var creado = await _onNuevo(this);
                if (creado == null) return;
                Seleccionado = creado;
                DialogResult = true;
            };
            btns.Children.Add(bNuevo);
        }
        var bSel = LBtn("✓  Seleccionar", LBrVerde);
        var bCan = LBtn("✕  Cancelar",    LBrGris);
        bSel.Click += (_, __) => Aceptar();
        bCan.Click += (_, __) => { DialogResult = false; };
        btns.Children.Add(bSel); btns.Children.Add(bCan);
        Grid.SetColumn(hint, 0); footerRow.Children.Add(hint);
        Grid.SetColumn(btns, 1); footerRow.Children.Add(btns);
        footer.Child = footerRow;
        Grid.SetRow(footer, 3); root.Children.Add(footer);

        return root;
    }

    private DataGrid BuildGrid()
    {
        var dg = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Background = Brushes.White,
            RowBackground = Brushes.White,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(246, 249, 252)),
            BorderThickness = new Thickness(0),
            FontSize = 12, CanUserReorderColumns = false, CanUserResizeRows = false,
            SelectionUnit = DataGridSelectionUnit.FullRow,
        };
        var hdrStyle = new Style(typeof(DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, LBrPrimary));
        hdrStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));
        hdrStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty,  FontWeights.SemiBold));
        hdrStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty,     new Thickness(12, 8, 12, 8)));
        hdrStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0)));
        dg.ColumnHeaderStyle = hdrStyle;

        var darkText = new SolidColorBrush(Color.FromRgb(20, 31, 48));
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(DataGridRow.ForegroundProperty, darkText));
        rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
        var hover = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(219, 234, 254))));
        rowStyle.Triggers.Add(hover);
        var sel = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        sel.Setters.Add(new Setter(DataGridRow.BackgroundProperty, LBrPrimary));
        sel.Setters.Add(new Setter(DataGridRow.ForegroundProperty, Brushes.White));
        sel.Setters.Add(new Setter(DataGridRow.FontWeightProperty,  FontWeights.SemiBold));
        rowStyle.Triggers.Add(sel);
        dg.RowStyle = rowStyle;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty,   new Thickness(0)));
        cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty,  null));
        cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty,        Brushes.Transparent));
        cellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty,           new Thickness(12, 6, 12, 6)));
        var cSel = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        cSel.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        cSel.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
        cellStyle.Triggers.Add(cSel);
        dg.CellStyle = cellStyle;
        return dg;
    }

    private void ActualizarConteo(int n) => _lblConteo.Text = $"{n} registro{(n == 1 ? "" : "s")}";

    private void Filtrar()
    {
        var term = _txtFiltro.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(term)) { _grid.ItemsSource = _todos; ActualizarConteo(_todos.Count); return; }
        var prop = _todos.FirstOrDefault()?.GetType().GetProperty(_displayProp);
        if (prop == null) return;
        var filtrados = _todos.Where(o => (prop.GetValue(o)?.ToString() ?? "").ToLowerInvariant().Contains(term)).ToList();
        _grid.ItemsSource = filtrados;
        ActualizarConteo(filtrados.Count);
    }

    private void Aceptar()
    {
        if (_grid.SelectedItem != null) { Seleccionado = _grid.SelectedItem; DialogResult = true; }
    }

    private static Button LBtn(string text, SolidColorBrush bg) => new Button {
        Content = text, Height = 32,
        Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(0, 0, 8, 0),
        Background = bg, Foreground = Brushes.White,
        BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold,
        FontSize = 12, Cursor = Cursors.Hand
    };
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: NUEVO / EDITAR GENERAL
// ════════════════════════════════════════════════════════════════════════════
public class NuevoEditarArticuloDialog : Window
{
    private readonly IArticuloRepository _repo;
    private readonly ArtCaches _c;
    private readonly Articulo? _orig;

    private TextBox _txtCodigo = null!, _txtSerial = null!, _txtNombre = null!, _txtPres = null!;
    private TextBox _txtStockMin = null!, _txtIva = null!, _txtMaxCuota = null!;
    private TextBox _txtPventa = null!, _txtContado = null!, _txtPcosto = null!;
    private TextBox _txtPctContado = null!, _txtPctPventa = null!;
    private bool    _recalcPct = false;
    private ComboBox _cboGravada = null!, _cboContado = null!;
    private RadioButton _rdoImper = null!, _rdoPer = null!;
    private DatePicker _dpVto = null!;

    private Seccion? _sec; private Categoria? _cat; private Subcategoria? _sub;
    private Marca? _marca; private Proveedor? _prov; private Medida? _med; private Pais? _pais;

    private TextBox _cSec = null!, _dSec = null!, _cCat = null!, _dCat = null!,
                    _cSub = null!, _dSub = null!, _cMarca = null!, _dMarca = null!,
                    _cProv = null!, _dProv = null!, _cMed = null!, _dMed = null!,
                    _cPais = null!, _dPais = null!;

    // ── Paleta moderna ───────────────────────────────────────────────────────
    static readonly SolidColorBrush BrPrimary = new(Color.FromRgb(18,  78, 120));  // #124E78
    static readonly SolidColorBrush BrAzul    = new(Color.FromRgb(31, 119, 180));  // #1F77B4
    static readonly SolidColorBrush BrFondo   = new(Color.FromRgb(238, 242, 247)); // #EEF2F7
    static readonly SolidColorBrush BrCard    = Brushes.White;
    static readonly SolidColorBrush BrBorde   = new(Color.FromRgb(208, 218, 232)); // #D0DAE8
    static readonly SolidColorBrush BrLabel   = new(Color.FromRgb(60,  80, 100));
    static readonly SolidColorBrush BrSub     = new(Color.FromRgb(100, 120, 140));
    static readonly SolidColorBrush BrVerde   = new(Color.FromRgb(30,  110,  66));
    static readonly SolidColorBrush BrGrisBtn = new(Color.FromRgb(100, 116, 132));

    public NuevoEditarArticuloDialog(IArticuloRepository repo, ArtCaches caches, Articulo? original)
    {
        _repo = repo; _c = caches; _orig = original;
        Title = original == null ? "Nuevo Artículo / Mercadería" : "Editar Artículo / Mercadería";
        Width = 800; Height = 660; MaxHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrFondo;
        ResizeMode = ResizeMode.NoResize;
        KeyDown += OnKey;
        Content = Build();
        if (original != null) Load(original);
    }

    private UIElement Build()
    {
        // Grid de 3 filas: header(Auto) | scroll(*) | footer(Auto)
        // Así el footer siempre es visible independientemente del contenido
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Header ───────────────────────────────────────────────────────────
        var hdr = BuildHeader();
        Grid.SetRow(hdr, 0);
        root.Children.Add(hdr);

        // ── Cuerpo con scroll ─────────────────────────────────────────────────
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 4) };

        body.Children.Add(BuildSeccion("IDENTIFICACIÓN", BuildIdGrid()));
        body.Children.Add(BuildSeccion("PRECIOS", BuildPreciosGrid()));
        body.Children.Add(BuildSeccion("CLASIFICACIÓN", BuildClasGrid()));
        body.Children.Add(BuildSeccion("VENCIMIENTO", BuildVtoRow()));

        scroll.Content = body;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        // ── Footer (siempre visible en la fila 2) ────────────────────────────
        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private Border BuildHeader()
    {
        var g = new Grid { Margin = new Thickness(20, 0, 20, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var accent = new Border {
            Width = 4, CornerRadius = new CornerRadius(2),
            Background = BrAzul, Margin = new Thickness(0, 16, 12, 16)
        };
        var titleSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 16, 0, 16) };
        titleSp.Children.Add(new TextBlock {
            Text = _orig == null ? "NUEVO ARTÍCULO / MERCADERÍA" : "EDITAR ARTÍCULO / MERCADERÍA",
            FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White
        });
        titleSp.Children.Add(new TextBlock {
            Text = _orig == null ? "Complete los datos del nuevo artículo" : $"Editando: {_orig.Ca} — {_orig.D}",
            FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(170, 204, 232)),
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(accent, 0); g.Children.Add(accent);
        Grid.SetColumn(titleSp, 1); g.Children.Add(titleSp);
        return new Border { Background = BrPrimary, Child = g };
    }

    private Border BuildSeccion(string titulo, UIElement contenido)
    {
        var card = new Border {
            Background = BrCard, CornerRadius = new CornerRadius(6),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(16, 12, 16, 14)
        };
        var sp = new StackPanel();

        // Título de sección con línea accent
        var hdr = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var bar = new Border {
            Width = 3, CornerRadius = new CornerRadius(2),
            Background = BrAzul, Margin = new Thickness(0, 1, 8, 1)
        };
        var lbl = new TextBlock {
            Text = titulo, FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = BrPrimary, VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(bar, 0); hdr.Children.Add(bar);
        Grid.SetColumn(lbl, 1); hdr.Children.Add(lbl);

        sp.Children.Add(hdr);
        sp.Children.Add(contenido);
        card.Child = sp;
        return card;
    }

    private UIElement BuildIdGrid()
    {
        var sp = new StackPanel();

        // Fila 1: Código | Serial | Gravada | IVA | Máx.Cuotas | Stock Mín | Solo contado
        var r1 = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        r1.Children.Add(ModField("Código *",        out _txtCodigo,    120));
        r1.Children.Add(ModField("Serial / Modelo", out _txtSerial,    160));
        r1.Children.Add(ModCombo("Gravada",    out _cboGravada, new[] { ("SI","1"),("NO","0") }, 70));
        r1.Children.Add(ModField("IVA (%)",         out _txtIva,        60, "0"));
        r1.Children.Add(ModField("Máx. cuotas",     out _txtMaxCuota,   60, "0"));
        r1.Children.Add(ModField("Stock Mínimo",    out _txtStockMin,   80, "0"));
        r1.Children.Add(ModCombo("Solo contado", out _cboContado, new[] { ("NO","0"),("SI","1") }, 80));
        sp.Children.Add(r1);

        // Fila 2: Nombre | Presentación
        var r2 = new WrapPanel();
        r2.Children.Add(ModField("Nombre / Descripción *", out _txtNombre, 480));
        r2.Children.Add(ModField("Presentación",            out _txtPres,  200, marginRight: 0));
        sp.Children.Add(r2);

        return sp;
    }

    private UIElement BuildPreciosGrid()
    {
        var sp = new StackPanel();

        // Fila 1: los 3 precios
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var fPcosto  = ModNumField("Precio Costo",   out _txtPcosto);
        var fContado = ModNumField("Precio Contado", out _txtContado);
        var fPventa  = ModNumField("Precio Venta",   out _txtPventa);

        Grid.SetColumn(fPcosto,  0); g.Children.Add(fPcosto);
        Grid.SetColumn(fContado, 1); g.Children.Add(fContado);
        Grid.SetColumn(fPventa,  2); g.Children.Add(fPventa);
        sp.Children.Add(g);

        // Fila 2: inputs de % ganancia sobre costo → calculan precio automáticamente
        var pctRow = new Grid();
        pctRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pctRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pctRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Columna 0 vacía (no hay % para Precio Costo)
        var spacer = new Border();
        Grid.SetColumn(spacer, 0); pctRow.Children.Add(spacer);

        // Helper para crear input de porcentaje
        StackPanel MkPct(string lbl, out TextBox pctTxt, TextBox precioTxt)
        {
            var psp = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            var hint = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
            hint.Children.Add(new TextBlock {
                Text = "% ganancia sobre costo →", FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 132)),
                VerticalAlignment = VerticalAlignment.Center
            });
            psp.Children.Add(hint);

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            pctTxt = new TextBox {
                Text = "0", Height = 28, TextAlignment = TextAlignment.Right,
                Padding = new Thickness(6, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 11, Background = new SolidColorBrush(Color.FromRgb(240, 248, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(144, 190, 240)), BorderThickness = new Thickness(1),
                Foreground = new SolidColorBrush(Color.FromRgb(20, 31, 48)),
                ToolTip = $"Ingresá el % de ganancia sobre el Precio Costo para calcular el {lbl}"
            };
            pctTxt.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.');

            var sufijo = new Border {
                Background = new SolidColorBrush(Color.FromRgb(210, 230, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(144, 190, 240)),
                BorderThickness = new Thickness(0, 1, 1, 1),
                Padding = new Thickness(6, 0, 6, 0)
            };
            sufijo.Child = new TextBlock {
                Text = "%", FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(pctTxt, 0); row.Children.Add(pctTxt);
            Grid.SetColumn(sufijo, 1); row.Children.Add(sufijo);
            psp.Children.Add(row);

            // Al cambiar %, recalcular precio
            var localPct = pctTxt;
            var localPrecio = precioTxt;
            localPct.TextChanged += (_, __) => {
                if (_recalcPct) return;
                if (!decimal.TryParse(_txtPcosto.Text.Replace(".", ""), out var costo) || costo == 0) return;
                if (!decimal.TryParse(localPct.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct)) return;
                _recalcPct = true;
                var resultado = Math.Round(costo * (1 + pct / 100m), 0);
                localPrecio.Text = resultado.ToString("N0", _pyCI);
                _recalcPct = false;
            };

            // Al cambiar precio manualmente, actualizar % inverso
            localPrecio.TextChanged += (_, __) => {
                if (_recalcPct) return;
                if (!decimal.TryParse(_txtPcosto.Text.Replace(".", ""), out var costo) || costo == 0) return;
                if (!decimal.TryParse(localPrecio.Text.Replace(".", ""), out var precio)) return;
                _recalcPct = true;
                var pctCalc = Math.Round((precio - costo) / costo * 100m, 1);
                localPct.Text = pctCalc.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                _recalcPct = false;
            };

            return psp;
        }

        var fPctContado = MkPct("Precio Contado", out _txtPctContado, _txtContado);
        var fPctPventa  = MkPct("Precio Venta",   out _txtPctPventa,  _txtPventa);

        Grid.SetColumn(fPctContado, 1); pctRow.Children.Add(fPctContado);
        Grid.SetColumn(fPctPventa,  2); pctRow.Children.Add(fPctPventa);
        sp.Children.Add(pctRow);

        return sp;
    }

    private UIElement BuildClasGrid()
    {
        var g = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        g.Children.Add(ModLupa("Sección",         out _cSec,   out _dSec,   () => Pick(_c.Secs.Cast<object>(),   "NombreSeccion",      v => { _sec   = (Seccion)v;      _cSec.Text   = _sec.IdSeccion.ToString();      _dSec.Text   = _sec.NombreSeccion; }, AbrirNuevaSeccionRapidaAsync)));
        g.Children.Add(ModLupa("Proveedor",        out _cProv,  out _dProv,  () => Pick(_c.Provs.Cast<object>(),  "NombreProveedor",    v => { _prov  = (Proveedor)v;    _cProv.Text  = _prov.IdProveedor.ToString();   _dProv.Text  = _prov.NombreProveedor; }, AbrirNuevoProveedorRapidoAsync)));
        g.Children.Add(ModLupa("Subcategoría",     out _cSub,   out _dSub,   () => Pick(_c.Subs.Cast<object>(),   "NombreSubcategoria", v => { _sub   = (Subcategoria)v; _cSub.Text   = _sub.IdSubcategoria.ToString(); _dSub.Text   = _sub.NombreSubcategoria; }, AbrirNuevaSubcategoriaRapidaAsync)));
        g.Children.Add(ModLupa("Categoría",        out _cCat,   out _dCat,   () => Pick(_c.Cats.Cast<object>(),   "NombreCategoria",    v => { _cat   = (Categoria)v;    _cCat.Text   = _cat.IdCategoria.ToString();    _dCat.Text   = _cat.NombreCategoria; }, AbrirNuevaCategoriaRapidaAsync)));
        g.Children.Add(ModLupa("País",             out _cPais,  out _dPais,  () => Pick(_c.Paises.Cast<object>(), "NombrePais",         v => { _pais  = (Pais)v;         _cPais.Text  = _pais.IdPais.ToString();        _dPais.Text  = _pais.NombrePais; }, AbrirNuevoPaisRapidoAsync)));
        g.Children.Add(ModLupa("Marca",            out _cMarca, out _dMarca, () => Pick(_c.Marcas.Cast<object>(), "NombreMarca",        v => { _marca = (Marca)v;        _cMarca.Text = _marca.IdMarca.ToString();      _dMarca.Text = _marca.NombreMarca; }, AbrirNuevaMarcaRapidaAsync)));
        g.Children.Add(ModLupa("Unidad de Medida", out _cMed,   out _dMed,   () => Pick(_c.Meds.Cast<object>(),   "NombreMedida",       v => { _med   = (Medida)v;       _cMed.Text   = _med.IdMedida.ToString();       _dMed.Text   = _med.NombreMedida; }, AbrirNuevaMedidaRapidaAsync)));
        return g;
    }

    private UIElement BuildVtoRow()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        _rdoImper = new RadioButton {
            Content = "Imperecedero", IsChecked = true,
            Foreground = BrLabel, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0)
        };
        _rdoPer = new RadioButton {
            Content = "Perecedero (tiene fecha de vencimiento)",
            Foreground = BrLabel, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0)
        };
        _dpVto = new DatePicker {
            IsEnabled = false, Width = 150, Height = 32,
            Background = Brushes.White, VerticalAlignment = VerticalAlignment.Center,
        };
        _dpVto.Loaded           += (_, __) => ForzarFondoDatePicker(_dpVto);
        _dpVto.IsEnabledChanged += (_, __) => ForzarFondoDatePicker(_dpVto);
        _rdoPer.Checked         += (_, __) => _dpVto.IsEnabled = true;
        _rdoImper.Checked       += (_, __) => { _dpVto.IsEnabled = false; _dpVto.SelectedDate = null; };
        sp.Children.Add(_rdoImper);
        sp.Children.Add(_rdoPer);
        sp.Children.Add(new TextBlock { Text = "Fecha:", Foreground = BrLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        sp.Children.Add(_dpVto);
        return sp;
    }

    private Border BuildFooter()
    {
        var g = new Grid { Margin = new Thickness(20, 0, 20, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hint = new TextBlock {
            Text = "F5 / Ctrl+S: Guardar   Esc: Cerrar",
            FontSize = 10, Foreground = BrSub,
            VerticalAlignment = VerticalAlignment.Center
        };

        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var bVP = FBtn("Vista previa", Color.FromRgb(21, 101, 192));
        var bIm = FBtn("Imprimir",     Color.FromRgb(27,  94,  32));
        var bG  = FBtn("💾  Guardar",  Color.FromRgb(18,  78, 120));
        var bC  = FBtn("✕  Cerrar",   Color.FromRgb(100, 116, 132));
        bVP.Click += (_, __) => AbrirFichaPrevia(imprimir: false);
        bIm.Click += (_, __) => AbrirFichaPrevia(imprimir: true);
        bG.Click  += async (_, __) => await SaveAsync();
        bC.Click  += (_, __) => Close();
        btns.Children.Add(bVP); btns.Children.Add(bIm);
        btns.Children.Add(bG);  btns.Children.Add(bC);

        Grid.SetColumn(hint, 0); g.Children.Add(hint);
        Grid.SetColumn(btns, 1); g.Children.Add(btns);

        return new Border {
            Background = BrCard, BorderBrush = BrBorde,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 12, 0, 12),
            Child = g
        };
    }

    // ── Helpers de UI modernos ────────────────────────────────────────────────
    private StackPanel ModField(string lbl, out TextBox txt, double width = double.NaN,
        string def = "", bool readOnly = false, double marginRight = 12)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, marginRight, 10) };
        sp.Children.Add(new TextBlock {
            Text = lbl, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, Margin = new Thickness(0, 0, 0, 3)
        });
        txt = new TextBox {
            Width = width, Height = 32, Text = def, IsReadOnly = readOnly,
            Padding = new Thickness(8, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 12, Background = Brushes.White,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.FromRgb(20, 31, 48))
        };
        sp.Children.Add(txt);
        return sp;
    }

    private static readonly System.Globalization.CultureInfo _pyCI =
        new System.Globalization.CultureInfo("es-PY");   // punto = sep miles, sin decimales

    private StackPanel ModNumField(string lbl, out TextBox txt, double marginRight = 12)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, marginRight, 10) };
        sp.Children.Add(new TextBlock {
            Text = lbl, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, Margin = new Thickness(0, 0, 0, 3)
        });

        txt = new TextBox {
            Text = "0",
            Height = 32, TextAlignment = TextAlignment.Right,
            Padding = new Thickness(8, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 12, Background = Brushes.White,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.FromRgb(20, 31, 48))
        };

        var t = txt;
        bool _updating = false;

        // Solo dígitos — bloquear cualquier otra tecla en PreviewTextInput
        t.PreviewTextInput += (_, e) =>
            e.Handled = !e.Text.All(char.IsDigit);

        // Bloquear pegar texto no numérico
        DataObject.AddPastingHandler(t, (_, e) => {
            if (e.DataObject.GetDataPresent(DataFormats.Text)) {
                var p = (string)e.DataObject.GetData(DataFormats.Text);
                if (!p.All(char.IsDigit)) e.CancelCommand();
            } else e.CancelCommand();
        });

        // Separador de miles en tiempo real al escribir
        t.TextChanged += (_, __) => {
            if (_updating) return;
            _updating = true;

            // Guardar posición relativa del cursor (en dígitos, no en chars totales)
            var caretPos = t.CaretIndex;
            var digitsBeforeCaret = t.Text.Take(caretPos).Count(char.IsDigit);

            // Limpiar puntos y reformatear
            var soloDigitos = new string(t.Text.Where(char.IsDigit).ToArray()).TrimStart('0');
            if (soloDigitos == "") soloDigitos = "0";

            var formatted = long.TryParse(soloDigitos, out var n)
                ? n.ToString("N0", _pyCI)   // "1.500.000"
                : soloDigitos;

            t.Text = formatted;

            // Restaurar cursor contando dígitos
            var newCaret = 0;
            var digits = 0;
            foreach (var c in formatted) {
                if (digits == digitsBeforeCaret) break;
                if (char.IsDigit(c)) digits++;
                newCaret++;
            }
            t.CaretIndex = Math.Min(newCaret, formatted.Length);

            _updating = false;
        };

        // Al ganar foco: si es "0" limpiar para facilitar escritura
        t.GotFocus += (_, __) => { if (t.Text == "0") { t.Text = ""; } };

        // Al perder foco: si quedó vacío volver a "0"
        t.LostFocus += (_, __) => { if (string.IsNullOrWhiteSpace(t.Text)) t.Text = "0"; };

        sp.Children.Add(txt);
        return sp;
    }

    // Lee el valor decimal de un ModNumField (elimina puntos de miles)
    private static decimal ReadPrecio(TextBox t) {
        var raw = new string(t.Text.Where(char.IsDigit).ToArray());
        return long.TryParse(raw, out var v) ? v : 0;
    }

    private StackPanel ModCombo(string lbl, out ComboBox cbo,
        (string Label, string Tag)[] items, double width = double.NaN, double marginRight = 12)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, marginRight, 10) };
        sp.Children.Add(new TextBlock {
            Text = lbl, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, Margin = new Thickness(0, 0, 0, 3)
        });
        cbo = new ComboBox {
            Width = width, Height = 32, FontSize = 12,
            Background = Brushes.White, BorderBrush = BrBorde, BorderThickness = new Thickness(1)
        };
        foreach (var (label, tag) in items)
            cbo.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        cbo.SelectedIndex = 0;
        sp.Children.Add(cbo);
        return sp;
    }

    private StackPanel ModLupa(string lbl, out TextBox cod, out TextBox desc, Action onPick)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        sp.Children.Add(new TextBlock {
            Text = lbl, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = BrLabel, Margin = new Thickness(0, 0, 0, 3)
        });
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var btn = new Button {
            Content = "⌕", Height = 32, Width = 36,
            Background = BrPrimary, Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 14,
            Margin = new Thickness(0, 0, 4, 0), Cursor = Cursors.Hand
        };
        btn.Click += (_, __) => onPick();

        cod = new TextBox {
            Height = 32, IsReadOnly = true,
            Padding = new Thickness(6, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 11, Background = new SolidColorBrush(Color.FromRgb(245, 248, 252)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Foreground = BrSub, Margin = new Thickness(0, 0, 4, 0)
        };
        desc = new TextBox {
            Height = 32, IsReadOnly = true,
            Padding = new Thickness(6, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 11, Background = new SolidColorBrush(Color.FromRgb(245, 248, 252)),
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.FromRgb(20, 31, 48))
        };

        Grid.SetColumn(btn,  0); row.Children.Add(btn);
        Grid.SetColumn(cod,  1); row.Children.Add(cod);
        Grid.SetColumn(desc, 2); row.Children.Add(desc);
        sp.Children.Add(row);
        return sp;
    }

    private static Button FBtn(string text, Color bg) => new Button {
        Content = text, Height = 34,
        Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(0, 0, 8, 0),
        Background = new SolidColorBrush(bg), Foreground = Brushes.White,
        BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold,
        FontSize = 12, Cursor = Cursors.Hand
    };

    private void Pick(IEnumerable<object> items, string prop, Action<object> onSel, Func<Window, Task<object?>>? onNuevo = null)
    {
        var dlg = new LupaDialog(prop, items, prop, onNuevo) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Seleccionado != null) onSel(dlg.Seleccionado);
    }

    private async Task<object?> AbrirNuevoProveedorRapidoAsync(Window owner)
    {
        var dlg = new CrediSoft.UI.Views.Maestros.ProveedoresWindow(modoAltaRapida: true) { Owner = owner };
        if (dlg.ShowDialog() != true || dlg.ProveedorCreado == null) return null;

        var provRepo = App.Services.GetRequiredService<IMaestrosProveedorRepository>();
        _c.Provs = (await provRepo.ListarTodosAsync()).ToList();
        return (object?)_c.Provs.FirstOrDefault(p => p.IdProveedor == dlg.ProveedorCreado.IdProveedor);
    }

    private async Task<object?> AbrirNuevaSeccionRapidaAsync(Window owner)
    {
        var dlg = new CrediSoft.UI.Views.Maestros.SeccionesWindow(modoAltaRapida: true) { Owner = owner };
        if (dlg.ShowDialog() != true || dlg.ItemCreado == null) return null;

        var secRepo = App.Services.GetRequiredService<IMaestrosSeccionRepository>();
        _c.Secs = (await secRepo.ListarTodosAsync()).ToList();
        return (object?)_c.Secs.FirstOrDefault(x => x.IdSeccion == dlg.ItemCreado.Id);
    }

    private async Task<object?> AbrirNuevaCategoriaRapidaAsync(Window owner)
    {
        var dlg = new CrediSoft.UI.Views.Maestros.CategoriasWindow(modoAltaRapida: true) { Owner = owner };
        if (dlg.ShowDialog() != true || dlg.ItemCreado == null) return null;

        var catRepo = App.Services.GetRequiredService<IMaestrosCategoriaRepository>();
        _c.Cats = (await catRepo.ListarTodosAsync()).ToList();
        return (object?)_c.Cats.FirstOrDefault(x => x.IdCategoria == dlg.ItemCreado.Id);
    }

    private async Task<object?> AbrirNuevaSubcategoriaRapidaAsync(Window owner)
    {
        var dlg = new CrediSoft.UI.Views.Maestros.SubcategoriasWindow(modoAltaRapida: true) { Owner = owner };
        if (dlg.ShowDialog() != true || dlg.SubcategoriaCreada == null) return null;

        var subRepo = App.Services.GetRequiredService<IMaestrosSubcategoriaRepository>();
        _c.Subs = (await subRepo.ListarTodosAsync()).ToList();
        return (object?)_c.Subs.FirstOrDefault(x => x.IdSubcategoria == dlg.SubcategoriaCreada.IdSubcategoria);
    }

    private async Task<object?> AbrirNuevaMarcaRapidaAsync(Window owner)
    {
        var dlg = new CrediSoft.UI.Views.Maestros.MarcasWindow(modoAltaRapida: true) { Owner = owner };
        if (dlg.ShowDialog() != true || dlg.ItemCreado == null) return null;

        var marcaRepo = App.Services.GetRequiredService<IMaestrosMarcaRepository>();
        _c.Marcas = (await marcaRepo.ListarTodosAsync()).ToList();
        return (object?)_c.Marcas.FirstOrDefault(x => x.IdMarca == dlg.ItemCreado.Id);
    }

    private async Task<object?> AbrirNuevaMedidaRapidaAsync(Window owner)
    {
        var dlg = new CrediSoft.UI.Views.Maestros.MedidasWindow(modoAltaRapida: true) { Owner = owner };
        if (dlg.ShowDialog() != true || dlg.ItemCreado == null) return null;

        var medRepo = App.Services.GetRequiredService<IMaestrosMedidaRepository>();
        _c.Meds = (await medRepo.ListarTodosAsync()).ToList();
        return (object?)_c.Meds.FirstOrDefault(x => x.IdMedida == dlg.ItemCreado.Id);
    }

    private async Task<object?> AbrirNuevoPaisRapidoAsync(Window owner)
    {
        var dlg = new CrediSoft.UI.Views.Maestros.ProcedenciasWindow(modoAltaRapida: true) { Owner = owner };
        if (dlg.ShowDialog() != true || dlg.ItemCreado == null) return null;

        var paisRepo = App.Services.GetRequiredService<IMaestrosPaisRepository>();
        _c.Paises = (await paisRepo.ListarTodosAsync()).ToList();
        return (object?)_c.Paises.FirstOrDefault(x => x.IdPais == dlg.ItemCreado.Id);
    }

    private static void ForzarFondoDatePicker(DatePicker dp)
    {
        var tb = dp.Template?.FindName("PART_TextBox", dp) as DatePickerTextBox;
        if (tb == null) return;
        tb.Background = Brushes.White;
        tb.Foreground = Brushes.Black;
        tb.IsReadOnly = true;
    }

    private void Load(Articulo art)
    {
        _txtCodigo.Text   = art.Ca;
        _txtSerial.Text   = art.Serial;
        _txtNombre.Text   = art.D;
        _txtPres.Text     = art.Pres;
        _txtStockMin.Text = art.Smin.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        _txtIva.Text      = art.Iva.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        _txtMaxCuota.Text = art.Maxcuota.ToString();
        SetTag(_cboGravada, art.Gra == 1 ? "1" : "0");
        SetTag(_cboContado, art.Slc == 1 ? "1" : "0");
        _txtPventa.Text  = ((long)art.Pventa).ToString("N0", _pyCI);
        _txtContado.Text = ((long)art.Contado).ToString("N0", _pyCI);
        _txtPcosto.Text  = ((long)art.Pc).ToString("N0", _pyCI);
        if (art.Vto.HasValue) { _rdoPer.IsChecked = true; _dpVto.SelectedDate = art.Vto; }
        else _rdoImper.IsChecked = true;

        _sec   = _c.Secs.FirstOrDefault(x => x.IdSeccion == art.Ids);
        _cat   = _c.Cats.FirstOrDefault(x => x.IdCategoria == art.Idc);
        _sub   = _c.Subs.FirstOrDefault(x => x.IdSubcategoria == art.Idsbc);
        _marca = _c.Marcas.FirstOrDefault(x => x.IdMarca == art.Idm);
        _prov  = _c.Provs.FirstOrDefault(x => x.IdProveedor == art.Idpr);
        _med   = _c.Meds.FirstOrDefault(x => x.IdMedida == art.Idmed);
        _pais  = art.Idpais.HasValue ? _c.Paises.FirstOrDefault(x => x.IdPais == art.Idpais.Value) : null;

        _cSec.Text   = _sec   != null ? _sec.IdSeccion.ToString()       : ""; _dSec.Text   = _sec?.NombreSeccion       ?? "";
        _cCat.Text   = _cat   != null ? _cat.IdCategoria.ToString()     : ""; _dCat.Text   = _cat?.NombreCategoria     ?? "";
        _cSub.Text   = _sub   != null ? _sub.IdSubcategoria.ToString()  : ""; _dSub.Text   = _sub?.NombreSubcategoria  ?? "";
        _cMarca.Text = _marca != null ? _marca.IdMarca.ToString()       : ""; _dMarca.Text = _marca?.NombreMarca       ?? "";
        _cProv.Text  = _prov  != null ? _prov.IdProveedor.ToString()    : ""; _dProv.Text  = _prov?.NombreProveedor    ?? "";
        _cMed.Text   = _med   != null ? _med.IdMedida.ToString()        : ""; _dMed.Text   = _med?.NombreMedida        ?? "";
        _cPais.Text  = _pais  != null ? _pais.IdPais.ToString()         : ""; _dPais.Text  = _pais?.NombrePais         ?? "";
    }

    private static void SetTag(ComboBox cbo, string tag)
    {
        foreach (ComboBoxItem item in cbo.Items)
            if (item.Tag?.ToString() == tag) { cbo.SelectedItem = item; return; }
        cbo.SelectedIndex = 0;
    }

    private void AbrirFichaPrevia(bool imprimir)
    {
        var gravada    = (_cboGravada.SelectedItem  as ComboBoxItem)?.Content?.ToString() ?? "";
        var soloContado = (_cboContado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        string vto = _rdoPer.IsChecked == true && _dpVto.SelectedDate.HasValue
            ? _dpVto.SelectedDate.Value.ToString("dd/MM/yyyy")
            : "Imperecedero";

        var ficha = new ArticuloFicha(
            Codigo:        _txtCodigo.Text.Trim(),
            Descripcion:   _txtNombre.Text.Trim(),
            Serial:        _txtSerial.Text.Trim(),
            Presentacion:  _txtPres.Text.Trim(),
            Gravada:       gravada,
            Iva:           _txtIva.Text.Trim(),
            MaxCuota:      _txtMaxCuota.Text.Trim(),
            StockMinimo:   _txtStockMin.Text.Trim(),
            SoloContado:   soloContado,
            Seccion:       _dSec.Text.Trim(),
            Proveedor:     _dProv.Text.Trim(),
            Subcategoria:  _dSub.Text.Trim(),
            Categoria:     _dCat.Text.Trim(),
            Pais:          _dPais.Text.Trim(),
            Marca:         _dMarca.Text.Trim(),
            UnidadMedida:  _dMed.Text.Trim(),
            Vencimiento:   vto,
            FechaImp:      DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario:       CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "",
            LogoPath:      ArticulosPagina.ResolverLogoPath(),
            Impresora:     ""
        );

        if (imprimir)
            ArticuloFichaPreviewWindow.ImprimirFicha(ficha, this);
        else
            new ArticuloFichaPreviewWindow(ficha) { Owner = this }.ShowDialog();
    }

    private async Task SaveAsync()
    {
        // ── Validaciones bloqueantes ─────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(_txtCodigo.Text))
        { MessageBox.Show("El código es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); _txtCodigo.Focus(); return; }
        if (string.IsNullOrWhiteSpace(_txtNombre.Text))
        { MessageBox.Show("El nombre es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); _txtNombre.Focus(); return; }

        var graTag = ((ComboBoxItem?)_cboGravada.SelectedItem)?.Tag?.ToString() ?? "0";
        var slcTag = ((ComboBoxItem?)_cboContado.SelectedItem)?.Tag?.ToString() ?? "0";
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        decimal.TryParse(_txtStockMin.Text, System.Globalization.NumberStyles.Number, inv, out var smin);
        decimal.TryParse(_txtIva.Text, System.Globalization.NumberStyles.Number, inv, out var iva);
        byte.TryParse(_txtMaxCuota.Text, out var maxc);
        var pventa  = ReadPrecio(_txtPventa);
        var contado = ReadPrecio(_txtContado);
        var pcosto  = ReadPrecio(_txtPcosto);

        // ── Validación bloqueante: crédito sin cuotas definidas ─────────────
        bool permiteCredito = slcTag != "1";   // Solo contado = NO → puede ir a crédito
        if (permiteCredito && maxc == 0)
        {
            new ValidacionBloqueadaDialog(
                campo:   "Máx. Cuotas",
                mensaje: "El artículo está configurado para venderse a crédito / cuota " +
                         "(Solo Contado = NO), pero el campo Máx. Cuotas está en 0.\n\n" +
                         "Definí un número de cuotas mayor a 0 antes de guardar."
            ) { Owner = this }.ShowDialog();
            _txtMaxCuota.Focus();
            return;
        }

        // ── Validaciones de advertencia (datos incompletos pero no bloqueantes) ─
        var advertencias = new List<(string Campo, string Detalle)>();
        if (pventa == 0)
            advertencias.Add(("Precio Venta", "El precio de venta está en cero."));
        if (pcosto == 0)
            advertencias.Add(("Precio Costo", "El precio de costo está en cero."));
        if (_sec == null)
            advertencias.Add(("Sección", "No se asignó sección al artículo."));

        if (advertencias.Count > 0)
        {
            var confirmar = new AdvertenciaDatosDialog(advertencias) { Owner = this };
            if (confirmar.ShowDialog() != true) return;
        }

        var art = new Articulo
        {
            Ca = _txtCodigo.Text.Trim(), Serial = _txtSerial.Text.Trim(),
            D  = _txtNombre.Text.Trim(), Pres   = _txtPres.Text.Trim(),
            Smin = smin, Iva = iva, Maxcuota = maxc,
            Ids = _sec?.IdSeccion, Idc = _cat?.IdCategoria,
            Idsbc = _sub?.IdSubcategoria, Idm = _marca?.IdMarca,
            Idpr = _prov?.IdProveedor, Idmed = (byte?)_med?.IdMedida,
            Idpais = (byte?)_pais?.IdPais,
            Gra = (byte)(graTag == "1" ? 1 : 0),
            Slc = (byte)(slcTag == "1" ? 1 : 0),
            Es = 1,
            Vto = _rdoPer.IsChecked == true ? _dpVto.SelectedDate : null,
        };

        var session = SessionService.Instance;
        var idLocal = session.LocalActual?.IdLocal ?? 1;
        try
        {
            var idUsuarioSesion = session.UsuarioActual?.IdUsuario ?? 1;
            var nomMaquina = Dns.GetHostName();
            if (_orig == null)
            {
                var idArt = await _repo.GuardarAsync(art, idLocal, idUsuarioSesion);
                if (pventa > 0 || contado > 0 || pcosto > 0)
                    await _repo.ActualizarPreciosAsync(idArt, idLocal, pcosto, pventa, contado, 0, idUsuarioSesion, nomMaquina);
                MessageBox.Show("Artículo guardado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                art.Id = _orig.Id;
                await _repo.ActualizarAsync(art);
                if (pventa > 0 || contado > 0 || pcosto > 0)
                    await _repo.ActualizarPreciosAsync(art.Id, idLocal, pcosto, pventa, contado, 0, idUsuarioSesion, nomMaquina);
                MessageBox.Show("Artículo actualizado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnKey(object s, KeyEventArgs e)
    {
        if (e.Key == Key.F5 || (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control))
        { _ = SaveAsync(); e.Handled = true; }
        else if (e.Key == Key.Escape) Close();
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: ERROR BLOQUEANTE — no se puede guardar sin corregir
// ════════════════════════════════════════════════════════════════════════════
public class ValidacionBloqueadaDialog : Window
{
    static readonly SolidColorBrush BrHeader = new(Color.FromRgb(127,  29,  29)); // red-900
    static readonly SolidColorBrush BrRojo   = new(Color.FromRgb(220,  38,  38)); // red-600
    static readonly SolidColorBrush BrRojoL  = new(Color.FromRgb(254, 226, 226)); // red-100
    static readonly SolidColorBrush BrBorde  = new(Color.FromRgb(252, 165, 165)); // red-300
    static readonly SolidColorBrush BrTexto  = new(Color.FromRgb( 69,  10,  10)); // red-950
    static readonly SolidColorBrush BrGris   = new(Color.FromRgb(100, 116, 132));

    public ValidacionBloqueadaDialog(string campo, string mensaje)
    {
        Title  = "No se puede guardar";
        Width  = 460;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(255, 241, 242));
        ResizeMode = ResizeMode.NoResize;
        Content = Build(campo, mensaje);
    }

    private UIElement Build(string campo, string mensaje)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Header ───────────────────────────────────────────────────────────
        var hdr = new Border { Background = BrHeader, Padding = new Thickness(20, 16, 20, 16) };
        var hdrRow = new StackPanel { Orientation = Orientation.Horizontal };
        hdrRow.Children.Add(new TextBlock {
            Text = "✕", FontSize = 22, Foreground = new SolidColorBrush(Color.FromRgb(252, 165, 165)),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0)
        });
        var hdrTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hdrTxt.Children.Add(new TextBlock {
            Text = "No se puede guardar", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.White
        });
        hdrTxt.Children.Add(new TextBlock {
            Text = "Corregí el campo indicado antes de continuar.",
            FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(252, 165, 165)),
            Margin = new Thickness(0, 3, 0, 0)
        });
        hdrRow.Children.Add(hdrTxt);
        hdr.Child = hdrRow;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── Cuerpo ────────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };

        var card = new Border {
            Background = BrRojoL, BorderBrush = BrBorde,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12, 14, 12)
        };
        var cardRow = new StackPanel { Orientation = Orientation.Horizontal };
        cardRow.Children.Add(new Border {
            Width = 4, CornerRadius = new CornerRadius(2),
            Background = BrRojo, Margin = new Thickness(0, 0, 12, 0)
        });
        var cardTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        cardTxt.Children.Add(new TextBlock {
            Text = campo, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = BrHeader
        });
        cardTxt.Children.Add(new TextBlock {
            Text = mensaje, FontSize = 11, Foreground = BrTexto,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), LineHeight = 18
        });
        cardRow.Children.Add(cardTxt);
        card.Child = cardRow;
        body.Children.Add(card);

        Grid.SetRow(body, 1); root.Children.Add(body);

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new Border {
            Background = BrRojoL, BorderBrush = BrBorde,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 12, 20, 12)
        };
        var footerRow = new Grid();
        footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hint = new TextBlock {
            Text = "Enter o Esc: Volver a editar",
            FontSize = 10, Foreground = BrGris, VerticalAlignment = VerticalAlignment.Center
        };
        var bVolver = new Button {
            Content = "✏  Corregir ahora",
            Height = 34, Padding = new Thickness(18, 0, 18, 0),
            Background = BrRojo, Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, FontSize = 12,
            Cursor = Cursors.Hand
        };
        bVolver.Click += (_, __) => Close();

        Grid.SetColumn(hint,   0); footerRow.Children.Add(hint);
        Grid.SetColumn(bVolver,1); footerRow.Children.Add(bVolver);
        footer.Child = footerRow;
        Grid.SetRow(footer, 2); root.Children.Add(footer);

        KeyDown += (_, ke) => {
            if (ke.Key == Key.Enter || ke.Key == Key.Escape) { Close(); ke.Handled = true; }
        };

        return root;
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: ADVERTENCIA — datos incompletos antes de guardar
// ════════════════════════════════════════════════════════════════════════════
public class AdvertenciaDatosDialog : Window
{
    static readonly SolidColorBrush BrFondo   = new(Color.FromRgb(255, 251, 235)); // amarillo muy suave
    static readonly SolidColorBrush BrHeader  = new(Color.FromRgb(120,  53,  15)); // marrón oscuro (amber-900)
    static readonly SolidColorBrush BrAmbar   = new(Color.FromRgb(217, 119,   6)); // amber-600
    static readonly SolidColorBrush BrAmbarL  = new(Color.FromRgb(253, 230, 138)); // amber-200
    static readonly SolidColorBrush BrTexto   = new(Color.FromRgb( 92,  45,   0));
    static readonly SolidColorBrush BrBorde   = new(Color.FromRgb(252, 211,  77)); // amber-300
    static readonly SolidColorBrush BrVerde   = new(Color.FromRgb( 22, 101,  52));
    static readonly SolidColorBrush BrGris    = new(Color.FromRgb(100, 116, 132));

    public AdvertenciaDatosDialog(List<(string Campo, string Detalle)> advertencias)
    {
        Title  = "Datos incompletos";
        Width  = 500;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrFondo;
        ResizeMode = ResizeMode.NoResize;
        Content = Build(advertencias);
    }

    private UIElement Build(List<(string Campo, string Detalle)> items)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // cuerpo
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

        // ── Header ───────────────────────────────────────────────────────────
        var hdr = new Border {
            Background = BrHeader,
            Padding    = new Thickness(20, 16, 20, 16),
        };
        var hdrSp = new StackPanel { Orientation = Orientation.Horizontal };
        hdrSp.Children.Add(new TextBlock {
            Text = "⚠", FontSize = 22, Foreground = BrAmbarL,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });
        var hdrTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hdrTxt.Children.Add(new TextBlock {
            Text = "¿Guardar con datos incompletos?",
            FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        });
        hdrTxt.Children.Add(new TextBlock {
            Text = "Se detectaron campos que podrían afectar el funcionamiento del artículo.",
            FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(252, 211, 77)),
            Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap
        });
        hdrSp.Children.Add(hdrTxt);
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // ── Cuerpo — lista de advertencias ───────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 4) };

        body.Children.Add(new TextBlock {
            Text = $"Se encontraron {items.Count} dato{(items.Count > 1 ? "s" : "")} sin completar:",
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrTexto, Margin = new Thickness(0, 0, 0, 12)
        });

        foreach (var (campo, detalle) in items)
        {
            var card = new Border {
                Background      = BrAmbarL,
                BorderBrush     = BrBorde,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(12, 10, 12, 10),
                Margin          = new Thickness(0, 0, 0, 8),
            };
            var cardSp = new StackPanel { Orientation = Orientation.Horizontal };
            cardSp.Children.Add(new Border {
                Width = 4, CornerRadius = new CornerRadius(2),
                Background = BrAmbar, Margin = new Thickness(0, 0, 10, 0)
            });
            var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txt.Children.Add(new TextBlock {
                Text = campo, FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = BrHeader
            });
            txt.Children.Add(new TextBlock {
                Text = detalle, FontSize = 10, Foreground = BrTexto,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0)
            });
            cardSp.Children.Add(txt);
            card.Child = cardSp;
            body.Children.Add(card);
        }

        body.Children.Add(new TextBlock {
            Text = "Podés guardar igual o volver a corregir los datos.",
            FontSize = 10, Foreground = BrGris,
            Margin = new Thickness(0, 4, 0, 0), FontStyle = FontStyles.Italic
        });

        Grid.SetRow(body, 1); root.Children.Add(body);

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new Border {
            Background      = new SolidColorBrush(Color.FromRgb(254, 243, 199)),
            BorderBrush     = BrBorde,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding         = new Thickness(20, 12, 20, 12),
        };
        var footerRow = new Grid();
        footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hint = new TextBlock {
            Text = "Enter: Guardar igual   Esc: Volver a editar",
            FontSize = 10, Foreground = BrGris,
            VerticalAlignment = VerticalAlignment.Center
        };

        var btns = new StackPanel { Orientation = Orientation.Horizontal };

        var bGuardar = new Button {
            Content = "💾  Guardar igual",
            Height = 34, Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Background = BrVerde, Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, FontSize = 12,
            Cursor = Cursors.Hand
        };
        var bVolver = new Button {
            Content = "✏  Volver a editar",
            Height = 34, Padding = new Thickness(16, 0, 16, 0),
            Background = BrGris, Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold, FontSize = 12,
            Cursor = Cursors.Hand
        };
        bGuardar.Click += (_, __) => { DialogResult = true; };
        bVolver.Click  += (_, __) => { DialogResult = false; };
        btns.Children.Add(bGuardar);
        btns.Children.Add(bVolver);

        Grid.SetColumn(hint, 0); footerRow.Children.Add(hint);
        Grid.SetColumn(btns, 1); footerRow.Children.Add(btns);
        footer.Child = footerRow;

        Grid.SetRow(footer, 2); root.Children.Add(footer);

        KeyDown += (_, ke) => {
            if (ke.Key == Key.Enter)  { DialogResult = true;  ke.Handled = true; }
            if (ke.Key == Key.Escape) { DialogResult = false; ke.Handled = true; }
        };

        return root;
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: EDITAR PRECIOS
// ════════════════════════════════════════════════════════════════════════════
public class EditarPreciosDialog : Window
{
    private readonly IArticuloRepository _repo;
    private readonly List<Local> _locales;
    private int _idArt = 0, _idLocal = 0;
    private TextBox _dCodArt = null!, _dDescArt = null!;
    private TextBox _dLocalNombre = null!;
    private TextBox _txtPcosto = null!, _txtPpromo = null!, _txtPventa = null!;
    private TextBox _txtPctDesc = null!, _txtValDesc = null!, _txtContado = null!;
    private TextBlock _lblFcompra = null!, _lblFventa = null!, _lblFmp = null!;
    private List<CheckBox> _chkLocales = new();
    private CheckBox _chkTodos = null!;

    // Snapshot de los 4 precios tal como quedaron después del último Load/Save — usado por
    // HayCambiosSinGuardar() para decidir si hace falta confirmar antes de cerrar. Se actualiza
    // en LoadPreciosAsync (nuevo artículo/local cargado) y justo antes de cerrar tras un
    // guardado exitoso, nunca en cada tecla — así no confunde "estoy escribiendo" con "hay
    // cambios reales respecto de lo último guardado/cargado".
    private (decimal Pc, decimal Pv, decimal Co, decimal Pp)? _snapshotPrecios;
    private bool _preciosGuardados;

    public EditarPreciosDialog(IArticuloRepository repo, List<Local> locales)
    {
        _repo = repo; _locales = locales;
        Title = "Modificar Precios";
        Width = 720; Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        ResizeMode = ResizeMode.NoResize;
        Content = Build();
        Closing += OnClosing;
        // Pre-cargar el local de la sesión activa
        var localSesion = SessionService.Instance.LocalActual;
        if (localSesion != null)
        {
            _idLocal = localSesion.IdLocal;
            _dLocalNombre.Text = localSesion.NombreLocal;
        }
    }

    // Pedido explícito: "si es que se modificó algo y se pulsa en cerrar se debe preguntar si
    // deseas salir sin completar el guardado" — antes Cerrar/la X de la ventana descartaban
    // cualquier precio tipeado sin avisar, fácil de hacer sin querer.
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_preciosGuardados || !HayCambiosSinGuardar()) return;
        var resp = MessageBox.Show(
            "Modificaste precios que todavía no se guardaron.\n\n¿Deseás salir sin guardar los cambios?",
            "Cambios sin guardar", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (resp != MessageBoxResult.Yes) e.Cancel = true;
    }

    private bool HayCambiosSinGuardar()
    {
        if (_snapshotPrecios is not { } s) return false;
        var pc  = UiH.ReadDecimal(_txtPcosto);
        var pv  = UiH.ReadDecimal(_txtPventa);
        var pp  = UiH.ReadDecimal(_txtPpromo);
        var cto = UiH.ReadDecimal(_txtContado);
        return pc != s.Pc || pv != s.Pv || pp != s.Pp || cto != s.Co;
    }

    private UIElement Build()
    {
        // Pie de botones FUERA del ScrollViewer (DockPanel.Bottom) — antes vivía adentro del
        // mismo scroll que el resto del contenido, así que con el footer más alto (3 botones
        // en vez de 2) directamente quedaba tapado abajo, obligando a scrollear para ver
        // "Guardar"/"Cerrar" — pedido explícito: que los botones entren siempre visibles, sin
        // scroll. Ahora solo el contenido de arriba (Selección/Precios/Contado/Locales)
        // scrollea; el pie queda fijo en la parte inferior de la ventana.
        var outer = new DockPanel { Margin = new Thickness(16) };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), EstiloScrollBarGris());
        var root = new StackPanel();
        scroll.Content = root;

        // Artículo + Local
        var secSel = UiH.SectionPanel("Selección", out var panelSel);
        var selRow = new WrapPanel();
        selRow.Children.Add(UiH.LupaGroup("Artículo (código)", out _dCodArt, async () => await PickArtAsync(), 80));
        _dCodArt.Width = 80;
        selRow.Children.Add(UiH.LupaGroup("Descripción", out _dDescArt, async () => await PickArtAsync(), 240));
        _dDescArt.IsReadOnly = true;
        selRow.Children.Add(UiH.LupaGroup("Local", out _dLocalNombre, () => PickLocal(), double.NaN, 0));
        panelSel.Children.Add(selRow);
        root.Children.Add(secSel);

        // Precios
        var secPrc = UiH.SectionPanel("Precios", out var panelPrc);
        var prcRow = new WrapPanel();
        prcRow.Children.Add(UiH.NumericFieldGroup("Precio costo",    out _txtPcosto, 140));
        prcRow.Children.Add(UiH.NumericFieldGroup("Precio promoción", out _txtPpromo, 140));
        prcRow.Children.Add(UiH.NumericFieldGroup("Precio venta",    out _txtPventa,  140, marginRight: 0));
        panelPrc.Children.Add(prcRow);

        // Fechas
        var fechaGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 8, 0, 0) };
        fechaGrid.Children.Add(MkFechaField("Última compra",    out _lblFcompra));
        fechaGrid.Children.Add(MkFechaField("Última venta",     out _lblFventa));
        fechaGrid.Children.Add(MkFechaField("Mod. de P. Venta", out _lblFmp));
        panelPrc.Children.Add(fechaGrid);

        // Contado destacado
        var ctBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(243, 156, 18)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 4, 0, 0),
        };
        // Título
        var ctTitle = new TextBlock { Text = "CONTADO — CONTADO", FontWeight = FontWeights.Bold, FontSize = 13, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) };
        // Tres campos en fila
        var ctRow = new StackPanel { Orientation = Orientation.Horizontal };

        // % desc
        var spPct = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        spPct.Children.Add(new TextBlock { Text = "% desc.", Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
        _txtPctDesc = UiH.Input(70, "0");
        _txtPctDesc.TextChanged += (_, __) => RecalcContado();
        spPct.Children.Add(_txtPctDesc);
        ctRow.Children.Add(spPct);

        // Valor del desc.
        var spVal = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        spVal.Children.Add(new TextBlock { Text = "Valor del desc.", Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
        _txtValDesc = UiH.Input(100, "0");
        _txtValDesc.IsReadOnly = true;
        _txtValDesc.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241));
        spVal.Children.Add(_txtValDesc);
        ctRow.Children.Add(spVal);

        // Precio contado-contado
        var spCto = new StackPanel();
        spCto.Children.Add(new TextBlock { Text = "Precio contado-contado", Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
        _txtContado = UiH.NumericInput(130);
        _txtContado.TextChanged += (_, __) => RecalcDescFromContado();
        spCto.Children.Add(_txtContado);
        ctRow.Children.Add(spCto);

        var ctStack = new StackPanel();
        ctStack.Children.Add(ctTitle);
        ctStack.Children.Add(ctRow);
        ctBorder.Child = ctStack;
        panelPrc.Children.Add(ctBorder);
        root.Children.Add(secPrc);

        // GUARDAR EN LOCALES
        var secLoc = UiH.SectionPanel("Guardar en locales", out var panelLoc);
        var locWrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        _chkTodos = new CheckBox
        {
            Content = "TODOS", FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(UiH.TextoLabel),
            Margin = new Thickness(0, 0, 16, 4)
        };
        _chkTodos.Checked   += (_, __) => _chkLocales.ForEach(c => c.IsChecked = true);
        _chkTodos.Unchecked += (_, __) => _chkLocales.ForEach(c => c.IsChecked = false);
        locWrap.Children.Add(_chkTodos);
        foreach (var local in _locales)
        {
            var chk = new CheckBox
            {
                Content = local.NombreLocal, Tag = local.IdLocal,
                Foreground = new SolidColorBrush(UiH.TextoLabel),
                Margin = new Thickness(0, 0, 12, 4)
            };
            _chkLocales.Add(chk);
            locWrap.Children.Add(chk);
        }
        panelLoc.Children.Add(locWrap);
        root.Children.Add(secLoc);

        // Botones — Grid separado, se agrega a "outer" (fuera del scroll) más abajo, no a "root"
        var pie = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hint = UiH.Hint("F5: Guardar   Esc: Cerrar");
        System.Windows.Controls.Grid.SetColumn(hint, 0); pie.Children.Add(hint);
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        // Botón compacto propio (no UiH.Btn, compartido por toda la app con padding/fuente más
        // grandes) — con los 3 botones del footer más largos que antes ("Historial de precios"
        // sumado a Guardar/Cerrar), el ancho fijo del diálogo (720, NoResize) no alcanzaba y
        // aparecía scroll horizontal — pedido explícito: que entren todos sin scroll.
        Button BtnChico(string texto, Color bg, System.Windows.RoutedEventHandler onClick)
        {
            var b = new Button
            {
                Content = texto, Height = 28, Padding = new Thickness(10, 0, 10, 0),
                Background = new SolidColorBrush(bg), Foreground = Brushes.White,
                BorderThickness = new Thickness(0), FontSize = 10.5, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(6, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand,
            };
            b.Click += onClick;
            return b;
        }
        var bH = BtnChico("🕘  Historial", Color.FromRgb(0x2C, 0x5F, 0x8A), (_, __) => MostrarHistorial());
        var bG = BtnChico("💾  Guardar",   UiH.Verde, async (_, __) => await SaveAsync());
        var bC = BtnChico("✕  Cerrar",    UiH.Gris,  (_, __) => Close());
        btns.Children.Add(bH); btns.Children.Add(bG); btns.Children.Add(bC);
        System.Windows.Controls.Grid.SetColumn(btns, 1); pie.Children.Add(btns);

        DockPanel.SetDock(pie, Dock.Bottom); outer.Children.Add(pie);
        outer.Children.Add(scroll);
        return outer;
    }

    // Scrollbar gris/gris claro — pedido explícito: el ScrollBar nativo de Windows (azul oscuro
    // sobre gris muy oscuro, tema por defecto) desentonaba fuerte contra el resto de la UI de
    // este diálogo. Definido en XAML embebido (vía XamlReader) en vez de anidar
    // FrameworkElementFactory a mano — un ScrollBar tiene Track+Thumb+2 RepeatButton, mucho más
    // legible como XAML que como árbol de factories.
    private static Style EstiloScrollBarGris()
    {
        const string xaml = @"
<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
       TargetType=""ScrollBar"">
  <Setter Property=""Width"" Value=""10""/>
  <Setter Property=""Background"" Value=""Transparent""/>
  <Setter Property=""Template"">
    <Setter.Value>
      <ControlTemplate TargetType=""ScrollBar"">
        <Grid Background=""#F0F2F4"">
          <Track Name=""PART_Track"" IsDirectionReversed=""True"">
            <Track.Thumb>
              <Thumb>
                <Thumb.Template>
                  <ControlTemplate TargetType=""Thumb"">
                    <Border Background=""#B7BFC7"" CornerRadius=""5"" Margin=""2,0,2,0""/>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
            <Track.DecreaseRepeatButton>
              <RepeatButton Command=""ScrollBar.PageUpCommand"" Opacity=""0"" />
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
              <RepeatButton Command=""ScrollBar.PageDownCommand"" Opacity=""0"" />
            </Track.IncreaseRepeatButton>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
        return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
    }

    private void MostrarHistorial()
    {
        if (_idArt == 0) { MessageBox.Show("Seleccione un artículo primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        // Siempre TODOS los locales (idLocal=0), no solo el tildado en el combo de arriba —
        // un guardado puede afectar varios locales a la vez ("Guardar en locales"), y filtrar
        // acá por el local activo de la sesión ocultaba el resto, mostrando solo 1 local aunque
        // el cambio real hubiera pegado en 13 (bug real reportado: "actualicé en todos los
        // locales, acá no muestra eso").
        var dlg = new HistorialPreciosDialog(_repo, _locales, _idArt, idLocal: 0, _dDescArt.Text) { Owner = this };
        dlg.ShowDialog();
    }

    private bool _recalcLock = false;

    private void RecalcContado()
    {
        if (_recalcLock) return;
        _recalcLock = true;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (decimal.TryParse(_txtPventa.Text.Replace(".", "").Replace(",", "."), System.Globalization.NumberStyles.Any, inv, out var pv)
            && decimal.TryParse(_txtPctDesc.Text, System.Globalization.NumberStyles.Any, inv, out var pct) && pct >= 0)
        {
            var desc = Math.Round(pv * pct / 100, 0);
            var contado = pv - desc;
            _txtValDesc.Text = ((long)desc).ToString(inv);
            _txtContado.Text = ((long)contado).ToString(inv);
        }
        _recalcLock = false;
    }

    private void RecalcDescFromContado()
    {
        if (_recalcLock) return;
        _recalcLock = true;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var pvRaw = _txtPventa.Text.Replace(".", "").Replace(",", ".");
        if (decimal.TryParse(pvRaw, System.Globalization.NumberStyles.Any, inv, out var pv) && pv > 0
            && decimal.TryParse(_txtContado.Text.Replace(".", "").Replace(",", "."), System.Globalization.NumberStyles.Any, inv, out var cto))
        {
            var desc = pv - cto;
            var pct  = Math.Round(desc * 100 / pv, 2);
            _txtValDesc.Text  = ((long)desc).ToString(inv);
            _txtPctDesc.Text  = pct.ToString("0.##", inv);
        }
        _recalcLock = false;
    }

    private static StackPanel MkFechaField(string label, out TextBlock valor)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        sp.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(UiH.TextoSub),
            FontSize = 10, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 2),
        });
        valor = new TextBlock
        {
            Text = "—",
            Foreground = new SolidColorBrush(UiH.TextoLabel),
            FontSize = 11,
            Background = new SolidColorBrush(Color.FromRgb(62, 86, 108)),
            Padding = new Thickness(8, 4, 8, 4),
        };
        sp.Children.Add(valor);
        return sp;
    }

    private async Task PickArtAsync()
    {
        var dlg = new VerArticulosWindow(this);
        if (dlg.ShowDialog() == true && dlg.ArticuloSeleccionado != null)
        {
            _idArt = dlg.ArticuloSeleccionado.IdArt;
            _dCodArt.Text  = dlg.ArticuloSeleccionado.Codigo;
            _dDescArt.Text = dlg.ArticuloSeleccionado.Desc;
            // Si no hay local elegido, usar el primero disponible
            if (_idLocal == 0 && _locales.Count > 0)
            {
                _idLocal = _locales[0].IdLocal;
                _dLocalNombre.Text = _locales[0].NombreLocal;
            }
            await LoadPreciosAsync();
        }
    }

    private void PickLocal()
    {
        var dlg = new LupaDialog("Seleccionar local", _locales.Cast<object>(), "NombreLocal") { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Seleccionado is Local l)
        {
            _idLocal = l.IdLocal;
            _dLocalNombre.Text = l.NombreLocal;
            if (_idArt > 0) _ = LoadPreciosAsync();
        }
    }

    private async Task LoadPreciosAsync()
    {
        var p = await _repo.ObtenerPrecioLocalAsync(_idArt, _idLocal);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        // _recalcLock=true mientras se asignan estos valores: sin esto, poner _txtPctDesc.Text
        // = "0" dos líneas más abajo disparaba su propio TextChanged -> RecalcContado(), que
        // pisaba el _txtContado.Text recién cargado (el valor REAL de la base) con "P.Venta -
        // 0% = P.Venta" — bug real reportado: el usuario decía haber tocado solo Precio Costo,
        // pero el Contado terminaba guardándose igual al Precio Venta porque este recálculo se
        // disparaba solo, sin que el usuario tocara nada, cada vez que se abría/recargaba un
        // artículo. Contado debe quedar EXACTAMENTE como viene de la base hasta que el usuario
        // edite %desc o el propio campo a mano.
        _recalcLock = true;
        // Asignar como entero puro — el NumericInput formatea con separador de miles automáticamente
        _txtPcosto.Text  = ((long)(p?.Pc      ?? 0)).ToString(inv);
        _txtPventa.Text  = ((long)(p?.Pventa  ?? 0)).ToString(inv);
        _txtPpromo.Text  = ((long)(p?.Ppromo  ?? 0)).ToString(inv);
        _txtContado.Text = ((long)(p?.Contado ?? 0)).ToString(inv);
        _txtPctDesc.Text = "0";
        _txtValDesc.Text = "0";
        _recalcLock = false;
        _lblFcompra.Text = p?.Fcompra.HasValue == true ? p.Fcompra.Value.ToString("dd/MM/yyyy") : "—";
        _lblFventa.Text  = p?.Fventa.HasValue  == true ? p.Fventa.Value.ToString("dd/MM/yyyy")  : "—";
        _lblFmp.Text     = p?.Fmp.HasValue     == true ? p.Fmp.Value.ToString("dd/MM/yyyy")     : "—";
        _snapshotPrecios = (p?.Pc ?? 0, p?.Pventa ?? 0, p?.Contado ?? 0, p?.Ppromo ?? 0);
    }

    private async Task SaveAsync()
    {
        if (_idArt == 0) { MessageBox.Show("Seleccione un artículo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var pc  = UiH.ReadDecimal(_txtPcosto);
        var pv  = UiH.ReadDecimal(_txtPventa);
        var pp  = UiH.ReadDecimal(_txtPpromo);
        var cto = UiH.ReadDecimal(_txtContado);

        var destinos = _chkLocales.Where(c => c.IsChecked == true).Select(c => (int)c.Tag!).ToList();
        if (!destinos.Any() && _idLocal > 0) destinos.Add(_idLocal);
        if (!destinos.Any()) { MessageBox.Show("Seleccione al menos un local.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        try
        {
            var idUsuarioSesion = SessionService.Instance.UsuarioActual?.IdUsuario ?? 1;
            var nomMaquina = Dns.GetHostName();
            // ActualizarPreciosAsync devuelve false si el artículo NUNCA tuvo una fila de
            // PRICES para ese local (caso distinto de "dado de baja", que ahora se reactiva
            // solo) — sin este chequeo, ese caso también se reportaba como "Precios guardados"
            // sin haber guardado nada, el mismo síntoma que motivó todo este historial.
            var fallidos = new List<string>();
            foreach (var idL in destinos)
            {
                var ok = await _repo.ActualizarPreciosAsync(_idArt, idL, pc, pv, cto, pp, idUsuarioSesion, nomMaquina);
                if (!ok) fallidos.Add(_locales.FirstOrDefault(l => l.IdLocal == idL)?.NombreLocal ?? $"Local {idL}");
            }

            if (fallidos.Count > 0)
            {
                MessageBox.Show(
                    $"No se pudo guardar el precio en: {string.Join(", ", fallidos)}.\n\n" +
                    "Ese artículo no tiene stock/precio configurado en ese local (nunca se cargó ahí). " +
                    "Cargalo primero desde Compras o Stock antes de modificar su precio.",
                    "Guardado parcial", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            if (fallidos.Count < destinos.Count)
            {
                MessageBox.Show("Precios guardados.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                _preciosGuardados = true;
                Close();
            }
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: HISTORIAL DE PRECIOS — pedido explícito del usuario tras reportar que "tiene la
// percepción de que no se está actualizando los precios desde acá": ActualizarPreciosAsync
// antes hacía un UPDATE ciego sin dejar ningún rastro (ni quién ni cuándo), así que no había
// forma de comprobar que un guardado realmente pegó salvo confiar a ciegas en el valor
// mostrado. Ahora cada guardado también inserta en AUDITORIA (antes→después por campo), y este
// diálogo lee esos registros para el artículo actual.
// ════════════════════════════════════════════════════════════════════════════
public class HistorialPreciosDialog : Window
{
    private readonly IArticuloRepository _repo;
    private readonly List<Local> _locales;
    private readonly int _idArt;
    private readonly int _idLocal;
    private StackPanel _lista = null!;
    private TextBlock _lblVacio = null!;

    // Colores pensados para texto SOBRE FONDO CLARO (BrFondo/BrBorde de la tarjeta) — la
    // tarjeta usa un fondo blanco/gris muy claro por dentro, distinto del fondo oscuro de la
    // ventana (UiH.FondoDlg), así que NO puede reutilizar UiH.TextoLabel (texto casi blanco,
    // pensado para fondo oscuro): quedaba invisible por bajo contraste — bug real reportado
    // ("el color blanco se camufla y no se distingue").
    private static readonly SolidColorBrush BrVerde     = new(Color.FromRgb(0x0E, 0x7A, 0x3E));
    private static readonly SolidColorBrush BrRojo      = new(Color.FromRgb(0xC0, 0x28, 0x1B));
    private static readonly SolidColorBrush BrGris      = new(Color.FromRgb(0x6B, 0x74, 0x7E));   // texto secundario, oscuro
    private static readonly SolidColorBrush BrTextoCard = new(Color.FromRgb(0x1F, 0x29, 0x37));   // texto principal, casi negro
    private static readonly SolidColorBrush BrFondo     = new(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush BrBorde     = new(Color.FromRgb(0xD8, 0xDE, 0xE4));
    private static readonly SolidColorBrush BrEncabezadoBg = new(Color.FromRgb(0xEE, 0xF2, 0xF6));

    public HistorialPreciosDialog(IArticuloRepository repo, List<Local> locales, int idArt, int idLocal, string descArt)
    {
        _repo = repo; _locales = locales; _idArt = idArt; _idLocal = idLocal;
        Title = "Historial de Modificación de Precios";
        Width = 560; Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        Content = Build(descArt);
        Loaded += async (_, __) => await CargarAsync();
    }

    private UIElement Build(string descArt)
    {
        var root = new DockPanel { Margin = new Thickness(16) };

        var titulo = new TextBlock
        {
            Text = $"Últimos cambios de precio — {descArt}",
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            Foreground = new SolidColorBrush(UiH.TextoLabel),
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap,
        };
        DockPanel.SetDock(titulo, Dock.Top); root.Children.Add(titulo);

        var bC = UiH.Btn("✕  Cerrar", UiH.Gris); bC.Click += (_, __) => Close();
        var piePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        piePanel.Children.Add(bC);
        DockPanel.SetDock(piePanel, Dock.Bottom); root.Children.Add(piePanel);

        _lblVacio = new TextBlock
        {
            Text = "Sin cambios registrados todavía para este artículo.\nLos cambios se registran a partir de ahora — no hay historial retroactivo.",
            Foreground = BrGris, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        _lista = new StackPanel();
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _lista };

        var wrap = new Grid();
        wrap.Children.Add(scroll);
        wrap.Children.Add(_lblVacio);
        root.Children.Add(wrap);

        return root;
    }

    private async Task CargarAsync()
    {
        var historial = (await _repo.ObtenerHistorialPreciosAsync(_idArt, _idLocal)).ToList();
        _lista.Children.Clear();
        _lblVacio.Visibility = historial.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Un guardado con varios locales tildados en "Guardar en locales" genera un registro de
        // AUDITORIA por local, todos con el mismo usuario/valores casi al mismo instante —
        // mostrarlos como tarjetas separadas hacía parecer que el cambio fue exclusivo de UN
        // local, cuando en realidad pegó en varios a la vez (pedido explícito: "falta mostrar a
        // qué locales se actualizó porque así puede ser que se malentienda"). Se agrupan acá por
        // usuario + antes/después + mismo segundo (margen chico por los inserts casi
        // simultáneos, no exactamente iguales en milisegundos) para juntarlos en una sola
        // tarjeta con TODOS los locales afectados.
        var grupos = new List<(DateTime Fecha, string Usuario, string ValorAntes, string ValorDespues, List<int> Locales)>();
        foreach (var h in historial.OrderByDescending(x => x.Fecha))
        {
            var idLocalRegistro = h.IdRegistro.Contains('-') && int.TryParse(h.IdRegistro.Split('-')[1], out var idl) ? idl : 0;
            var grupo = grupos.FirstOrDefault(g =>
                g.Usuario == h.Usuario && g.ValorAntes == h.ValorAntes && g.ValorDespues == h.ValorDespues &&
                Math.Abs((g.Fecha - h.Fecha).TotalSeconds) <= 2);
            if (grupo.Locales != null) grupo.Locales.Add(idLocalRegistro);
            else grupos.Add((h.Fecha, h.Usuario, h.ValorAntes, h.ValorDespues, new List<int> { idLocalRegistro }));
        }

        foreach (var g in grupos)
            _lista.Children.Add(TarjetaCambio(g.Fecha, g.Usuario, g.ValorAntes, g.ValorDespues, g.Locales));
    }

    // Una tarjeta por cambio: fecha/usuario/locales arriba, y abajo SOLO los campos que
    // realmente variaron, cada uno como "valor anterior → valor nuevo" resaltado — pedido
    // explícito del usuario ("no es nada intuitivo, falta mejorar la UI") tras ver la tabla
    // plana anterior con un bloque de texto "C:... V:... Co:... P:..." difícil de leer.
    private Border TarjetaCambio(DateTime fecha, string usuario, string valorAntes, string valorDespues, List<int> idsLocales)
    {
        var h = new HistorialPrecioRow { Fecha = fecha, Usuario = usuario, ValorAntes = valorAntes, ValorDespues = valorDespues };
        var nombresLocales = idsLocales
            .Select(id => _locales.FirstOrDefault(l => l.IdLocal == id)?.NombreLocal ?? $"Local {id}")
            .ToList();

        var card = new Border
        {
            Background = BrFondo, BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 8),
            ClipToBounds = true,
        };
        var sp = new StackPanel();
        card.Child = sp;

        // Encabezado con fondo propio (distinto del blanco de la tarjeta) para separarlo
        // visualmente de la tabla de campos — fecha/hora + usuario arriba, y TODOS los locales
        // afectados por este mismo guardado abajo, como badges en wrap (no solo uno): un
        // guardado con "Guardar en locales" tildando varios (o "TODOS") aplica el mismo cambio
        // a la vez en cada uno, y mostrar un solo local sugería que el cambio fue exclusivo de
        // ahí — pedido explícito: "falta mostrar a qué locales se actualizó... puede ser que se
        // malentienda". Todo en tonos oscuros: el fondo acá es claro, no el oscuro de la
        // ventana, así que el texto casi-blanco de UiH.TextoLabel quedaba invisible.
        var headStack = new StackPanel { Background = BrEncabezadoBg, Margin = new Thickness(0) };

        var headTxt = new TextBlock
        {
            FontSize = 11, Foreground = BrTextoCard, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(12, 8, 12, 4),
        };
        headTxt.Inlines.Add(new System.Windows.Documents.Run(h.Fecha.ToString("dd/MM/yyyy HH:mm")));
        headTxt.Inlines.Add(new System.Windows.Documents.Run($"   ·   {h.Usuario}") { FontWeight = FontWeights.Normal, Foreground = BrGris });
        headStack.Children.Add(headTxt);

        var localesRow = new WrapPanel { Margin = new Thickness(12, 0, 12, 8) };
        localesRow.Children.Add(new TextBlock
        {
            Text = nombresLocales.Count > 1 ? "Locales: " : "Local: ",
            FontSize = 10, Foreground = BrGris, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 4),
        });
        foreach (var nombreLocal in nombresLocales)
        {
            var localBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xD8, 0xE7, 0xF4)), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 5, 4),
            };
            localBadge.Child = new TextBlock { Text = nombreLocal, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0x4A, 0x73)), FontWeight = FontWeights.Bold };
            localesRow.Children.Add(localBadge);
        }
        headStack.Children.Add(localesRow);
        sp.Children.Add(headStack);

        // Tabla de campos modificados: encabezado propio ("Campo | Antes | Después") para que
        // se entienda de un vistazo qué representa cada columna, en vez de solo mostrar los
        // valores sueltos — pedido explícito: "una tabla de los que se modificó... pero que se
        // entienda bien". Solo se listan los campos que efectivamente cambiaron.
        var campos = new (string Nombre, decimal Antes, decimal Despues)[]
        {
            ("Precio costo",     h.CostoAntes,   h.CostoDespues),
            ("Precio venta",     h.VentaAntes,   h.VentaDespues),
            ("Precio contado",   h.ContadoAntes, h.ContadoDespues),
            ("Precio promoción", h.PromoAntes,   h.PromoDespues),
        }.Where(c => c.Antes != c.Despues).ToList();

        if (campos.Count == 0)
        {
            sp.Children.Add(new TextBlock
            {
                Text = "Guardado sin cambios en los precios.", FontSize = 11, Foreground = BrGris,
                Margin = new Thickness(12, 8, 12, 10), FontStyle = FontStyles.Italic,
            });
            return card;
        }

        var tabla = new Grid { Margin = new Thickness(12, 8, 12, 10) };
        tabla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        tabla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tabla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tabla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        TextBlock ColHead(string texto) => new()
        {
            Text = texto, FontSize = 9.5, Foreground = BrGris, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4),
        };
        tabla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var hCampo = ColHead("CAMPO");   Grid.SetRow(hCampo, 0); Grid.SetColumn(hCampo, 0); tabla.Children.Add(hCampo);
        var hAntes = ColHead("ANTES");   Grid.SetRow(hAntes, 0); Grid.SetColumn(hAntes, 1); tabla.Children.Add(hAntes);
        var hDespues = ColHead("DESPUÉS"); Grid.SetRow(hDespues, 0); Grid.SetColumn(hDespues, 3); tabla.Children.Add(hDespues);

        int fila = 1;
        foreach (var (nombre, antes, despues) in campos)
        {
            tabla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var subio = despues > antes;

            var lbl = new TextBlock
            {
                Text = nombre, FontSize = 12, Foreground = BrTextoCard,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 8, 3),
            };
            Grid.SetRow(lbl, fila); Grid.SetColumn(lbl, 0); tabla.Children.Add(lbl);

            var antesTxt = new TextBlock
            {
                Text = $"{antes:N0}", FontSize = 12, Foreground = BrGris,
                TextDecorations = TextDecorations.Strikethrough, VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(antesTxt, fila); Grid.SetColumn(antesTxt, 1); tabla.Children.Add(antesTxt);

            var flecha = new TextBlock
            {
                Text = "→", FontSize = 12, Foreground = BrGris, Margin = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(flecha, fila); Grid.SetColumn(flecha, 2); tabla.Children.Add(flecha);

            var despuesTxt = new TextBlock
            {
                Text = $"{despues:N0}", FontSize = 12.5, Foreground = subio ? BrVerde : BrRojo,
                FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(despuesTxt, fila); Grid.SetColumn(despuesTxt, 3); tabla.Children.Add(despuesTxt);
            fila++;
        }
        sp.Children.Add(tabla);

        return card;
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: EDITAR STOCK
// ════════════════════════════════════════════════════════════════════════════
public class EditarStockDialog : Window
{
    private readonly IArticuloRepository _repo;
    private readonly List<Local> _locales;
    private int _idArt = 0, _idLocal = 0;
    private TextBox _dCodArt = null!, _dDescArt = null!, _dLocalNombre = null!;
    private TextBox _txtStock = null!;

    public EditarStockDialog(IArticuloRepository repo, List<Local> locales)
    {
        _repo = repo; _locales = locales;
        Title = "Modificar Stock";
        Width = 480; Height = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        ResizeMode = ResizeMode.NoResize;
        Content = Build();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F5 || (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control))
            { _ = SaveAsync(); e.Handled = true; }
            else if (e.Key == Key.Escape) Close();
        };
    }

    private UIElement Build()
    {
        var root = new StackPanel { Margin = new Thickness(16) };

        var secSel = UiH.SectionPanel("Selección", out var panelSel);
        var r1 = new WrapPanel();
        r1.Children.Add(UiH.LupaGroup("Local",         out _dLocalNombre, () => PickLocal(),           180));
        r1.Children.Add(UiH.LupaGroup("Código artículo", out _dCodArt,    async () => await PickArtAsync(), 80));
        panelSel.Children.Add(r1);
        _dDescArt = UiH.Input(double.NaN, readOnly: true);
        _dDescArt.Margin = new Thickness(0, 0, 0, 4);
        var descSp = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
        descSp.Children.Add(UiH.Label("Descripción"));
        descSp.Children.Add(_dDescArt);
        panelSel.Children.Add(descSp);
        root.Children.Add(secSel);

        var secStock = UiH.SectionPanel("Stock / Existencia", out var panelStock);
        panelStock.Children.Add(UiH.FieldGroup("Cantidad actual", out _txtStock, 160, "0"));
        root.Children.Add(secStock);

        var pie = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hint = UiH.Hint("F5: Guardar   Esc: Cerrar");
        System.Windows.Controls.Grid.SetColumn(hint, 0); pie.Children.Add(hint);
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var bG = UiH.Btn("💾  Guardar", UiH.Verde); bG.Click += async (_, __) => await SaveAsync();
        var bC = UiH.Btn("✕  Cerrar",   UiH.Gris);  bC.Click += (_, __) => Close();
        btns.Children.Add(bG); btns.Children.Add(bC);
        System.Windows.Controls.Grid.SetColumn(btns, 1); pie.Children.Add(btns);
        root.Children.Add(pie);

        return root;
    }

    private void PickLocal()
    {
        var dlg = new LupaDialog("Seleccionar local", _locales.Cast<object>(), "NombreLocal") { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Seleccionado is Local l)
        {
            _idLocal = l.IdLocal;
            _dLocalNombre.Text = l.NombreLocal;
        }
    }

    private async Task PickArtAsync()
    {
        var dlg = new VerArticulosWindow(this);
        if (dlg.ShowDialog() == true && dlg.ArticuloSeleccionado != null)
        {
            _idArt = dlg.ArticuloSeleccionado.IdArt;
            _dCodArt.Text  = dlg.ArticuloSeleccionado.Codigo;
            _dDescArt.Text = dlg.ArticuloSeleccionado.Desc;
            if (_idLocal > 0)
            {
                var p = await _repo.ObtenerPrecioLocalAsync(_idArt, _idLocal);
                _txtStock.Text = (p?.S ?? 0).ToString("N0");
            }
        }
    }

    private async Task SaveAsync()
    {
        if (_idArt == 0)    { MessageBox.Show("Seleccione un artículo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (_idLocal == 0)  { MessageBox.Show("Seleccione un local.",    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (!decimal.TryParse(_txtStock.Text, out var stock))
        { MessageBox.Show("Cantidad inválida.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        try
        {
            await _repo.ActualizarStockAsync(_idArt, _idLocal, stock, "F", SessionService.Instance.UsuarioActual?.IdUsuario ?? 1);
            MessageBox.Show("Stock actualizado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: EDITAR SECC-CAT
// ════════════════════════════════════════════════════════════════════════════
public class EditarSeccCatDialog : Window
{
    private readonly IArticuloRepository _repo;
    private readonly ArtCaches _c;
    private DataGrid _grid = null!;
    private TextBox _txtFiltro = null!;
    private List<Articulo> _todos = new();

    public EditarSeccCatDialog(IArticuloRepository repo, ArtCaches caches)
    {
        _repo = repo; _c = caches;
        Title = "Editar Sección / Categoría";
        Width = 960; Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        Content = Build();
        Loaded += async (_, __) => await RefreshAsync();
    }

    private UIElement Build()
    {
        var root = UiH.MkGrid(rows: new[] { "Auto", "Auto", "*", "Auto" });
        root.Margin = new Thickness(16);

        // Info
        var info = new TextBlock
        {
            Text = "Doble clic sobre una fila para editar su Sección, Marca, Categoría y Subcategoría.",
            Foreground = new SolidColorBrush(UiH.TextoSub),
            FontSize = 11, Margin = new Thickness(0, 0, 0, 10)
        };
        UiH.SetRow(info, 0); root.Children.Add(info);

        // Barra de búsqueda
        var searchPanel = new Border
        {
            Background = new SolidColorBrush(UiH.FondoPanel),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 10),
        };
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        searchRow.Children.Add(new TextBlock
        {
            Text = "Buscar por código, descripción o marca:",
            Foreground = new SolidColorBrush(UiH.TextoLabel),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11, Margin = new Thickness(0, 0, 10, 0)
        });
        _txtFiltro = UiH.Input(320);
        _txtFiltro.TextChanged += (_, __) => FiltrarEnMemoria();
        searchRow.Children.Add(_txtFiltro);
        searchPanel.Child = searchRow;
        UiH.SetRow(searchPanel, 1); root.Children.Add(searchPanel);

        // Grid
        _grid = UiH.ModernGrid();
        _grid.Columns.Add(UiH.Col("Código",       "Ca",                 90));
        _grid.Columns.Add(UiH.Col("Descripción",  "D",                  0, star: true));
        _grid.Columns.Add(UiH.Col("Marca",        "MarcaNombre",        120));
        _grid.Columns.Add(UiH.Col("Sección",      "SeccionNombre",      120));
        _grid.Columns.Add(UiH.Col("Categoría",    "CategoriaNombre",    120));
        _grid.Columns.Add(UiH.Col("Subcategoría", "SubcategoriaNombre", 120));
        _grid.MouseDoubleClick += async (_, __) => await OnItemAsync();
        var gridBorder = new Border { Child = _grid, CornerRadius = new CornerRadius(6), ClipToBounds = true, Margin = new Thickness(0, 0, 0, 10) };
        UiH.SetRow(gridBorder, 2); root.Children.Add(gridBorder);

        var bot = UiH.BtnBar(UiH.Btn("✕  Cerrar", UiH.Gris));
        ((Button)bot.Children[0]).Click += (_, __) => Close();
        UiH.SetRow(bot, 3); root.Children.Add(bot);

        return root;
    }

    private void FiltrarEnMemoria()
    {
        var term = _txtFiltro.Text.Trim().ToLowerInvariant();
        _grid.ItemsSource = string.IsNullOrEmpty(term)
            ? _todos
            : _todos.Where(a =>
                a.Ca.ToLowerInvariant().Contains(term) ||
                a.D.ToLowerInvariant().Contains(term) ||
                a.MarcaNombre.ToLowerInvariant().Contains(term)).ToList();
    }

    private async Task RefreshAsync()
    {
        _todos = (await _repo.BuscarTodosAsync()).ToList();
        FiltrarEnMemoria();
    }

    private async Task OnItemAsync()
    {
        if (_grid.SelectedItem is not Articulo art) return;
        var dlg = new SeccCatItemDialog(_repo, art, _c) { Owner = this };
        if (dlg.ShowDialog() == true) await RefreshAsync();
    }
}

public class SeccCatItemDialog : Window
{
    private readonly IArticuloRepository _repo;
    private readonly Articulo _art;
    private readonly ArtCaches _c;
    private Seccion? _sec; private Categoria? _cat;
    private Subcategoria? _sub; private Marca? _marca;
    private TextBox _cSec = null!, _dSec = null!, _cCat = null!, _dCat = null!,
                    _cSub = null!, _dSub = null!, _cMarca = null!, _dMarca = null!;

    public SeccCatItemDialog(IArticuloRepository repo, Articulo art, ArtCaches c)
    {
        _repo = repo; _art = art; _c = c;
        Title = $"Clasificación — {art.Ca}";
        Width = 820; Height = 500;
        MinWidth = 820; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        ResizeMode = ResizeMode.CanResize;
        Content = Build();
    }

    private UIElement Build()
    {
        var root = new StackPanel { Margin = new Thickness(16) };

        // Info artículo: Código + Descripción
        var secInfo = UiH.SectionPanel("", out var panelInfo, dark: false);
        var infoGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        infoGrid.Children.Add(UiH.FieldGroup("Código",      out var txtCod,  double.NaN, _art.Ca,  readOnly: true));
        infoGrid.Children.Add(UiH.FieldGroup("Descripción", out var txtDesc, double.NaN, _art.D,   readOnly: true, marginRight: 0));
        panelInfo.Children.Add(infoGrid);
        root.Children.Add(secInfo);

        // Clasificación con código + nombre + botón +++
        var secFields = UiH.SectionPanel("Clasificación", out var panelF);
        var g3 = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        g3.Children.Add(MkClasifField("Sección",      out _cSec,   out _dSec,
            () => Pick(_c.Secs.Cast<object>(),   "NombreSeccion",      v => { _sec   = (Seccion)v;      _cSec.Text   = _sec.IdSeccion.ToString();      _dSec.Text   = _sec.NombreSeccion; }),
            () => NuevoMaestro("Sección",     async (c,n) => { await App.Services.GetRequiredService<IMaestrosSeccionRepository>().InsertarAsync(c,n);  var r = (await App.Services.GetRequiredService<IMaestrosSeccionRepository>().ListarTodosAsync()).ToList(); _c.Secs.Clear(); _c.Secs.AddRange(r); })));
        g3.Children.Add(MkClasifField("Marca",        out _cMarca, out _dMarca,
            () => Pick(_c.Marcas.Cast<object>(), "NombreMarca",        v => { _marca = (Marca)v;        _cMarca.Text = _marca.IdMarca.ToString();      _dMarca.Text = _marca.NombreMarca; }),
            () => NuevoMaestro("Marca",       async (c,n) => { await App.Services.GetRequiredService<IMaestrosMarcaRepository>().InsertarAsync(c,n);    var r = (await App.Services.GetRequiredService<IMaestrosMarcaRepository>().ListarTodosAsync()).ToList(); _c.Marcas.Clear(); _c.Marcas.AddRange(r); })));
        g3.Children.Add(MkClasifField("Categoría",    out _cCat,   out _dCat,
            () => Pick(_c.Cats.Cast<object>(),   "NombreCategoria",    v => { _cat   = (Categoria)v;    _cCat.Text   = _cat.IdCategoria.ToString();    _dCat.Text   = _cat.NombreCategoria; }),
            () => NuevoMaestro("Categoría",   async (c,n) => { await App.Services.GetRequiredService<IMaestrosCategoriaRepository>().InsertarAsync(c,n); var r = (await App.Services.GetRequiredService<IMaestrosCategoriaRepository>().ListarTodosAsync()).ToList(); _c.Cats.Clear(); _c.Cats.AddRange(r); })));
        g3.Children.Add(MkClasifField("Subcategoría", out _cSub,   out _dSub,
            () => Pick(_c.Subs.Cast<object>(),   "NombreSubcategoria", v => { _sub   = (Subcategoria)v; _cSub.Text   = _sub.IdSubcategoria.ToString(); _dSub.Text   = _sub.NombreSubcategoria; }),
            () => NuevoMaestro("Subcategoría",async (c,n) => { await App.Services.GetRequiredService<IMaestrosSubcategoriaRepository>().InsertarAsync(_cat?.IdCategoria ?? 1,c,n); var r = (await App.Services.GetRequiredService<IMaestrosSubcategoriaRepository>().ListarTodosAsync()).ToList(); _c.Subs.Clear(); _c.Subs.AddRange(r); })));
        panelF.Children.Add(g3);
        root.Children.Add(secFields);

        // Cargar valores actuales
        _sec   = _c.Secs.FirstOrDefault(x => x.IdSeccion == _art.Ids);
        _cat   = _c.Cats.FirstOrDefault(x => x.IdCategoria == _art.Idc);
        _sub   = _c.Subs.FirstOrDefault(x => x.IdSubcategoria == _art.Idsbc);
        _marca = _c.Marcas.FirstOrDefault(x => x.IdMarca == _art.Idm);
        _cSec.Text   = _sec   != null ? _sec.IdSeccion.ToString()       : ""; _dSec.Text   = _sec?.NombreSeccion       ?? "";
        _cCat.Text   = _cat   != null ? _cat.IdCategoria.ToString()     : ""; _dCat.Text   = _cat?.NombreCategoria     ?? "";
        _cSub.Text   = _sub   != null ? _sub.IdSubcategoria.ToString()  : ""; _dSub.Text   = _sub?.NombreSubcategoria  ?? "";
        _cMarca.Text = _marca != null ? _marca.IdMarca.ToString()       : ""; _dMarca.Text = _marca?.NombreMarca       ?? "";

        var pie = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var bG = UiH.Btn("💾  Guardar", UiH.Verde); bG.Click += async (_, __) => await SaveAsync();
        var bC = UiH.Btn("✕  Cerrar",   UiH.Gris);  bC.Click += (_, __) => { DialogResult = false; Close(); };
        btns.Children.Add(bG); btns.Children.Add(bC);
        System.Windows.Controls.Grid.SetColumn(btns, 1); pie.Children.Add(btns);
        root.Children.Add(pie);

        return root;
    }

    // Campo con lupa + botón +++ + dos inputs (código | nombre)
    private StackPanel MkClasifField(string label, out TextBox codigo, out TextBox nombre,
        Action onPick, Action onNuevo)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 8, 8) };
        sp.Children.Add(UiH.Label(label));
        var row = new StackPanel { Orientation = Orientation.Horizontal };

        // Botón lupa
        var btnLupa = UiH.LupaBtn();
        btnLupa.Click += (_, __) => onPick();
        row.Children.Add(btnLupa);

        // Código (readonly, angosto)
        codigo = new TextBox
        {
            IsReadOnly = true, Width = 48, Height = 28,
            Padding = new Thickness(6, 5, 6, 5),
            Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
            Foreground = Brushes.Black, BorderBrush = new SolidColorBrush(UiH.BorderInput),
            BorderThickness = new Thickness(1), FontSize = 11,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0),
        };
        row.Children.Add(codigo);

        // Nombre (readonly)
        nombre = new TextBox
        {
            IsReadOnly = true, Width = 150, Height = 28,
            Padding = new Thickness(8, 5, 8, 5),
            Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
            Foreground = Brushes.Black, BorderBrush = new SolidColorBrush(UiH.BorderInput),
            BorderThickness = new Thickness(1), FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        row.Children.Add(nombre);

        // Botón "Nueva Sección / Marca / etc."
        var btnNuevo = new Button
        {
            Content = $"+ Nueva {label}", Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Background = new SolidColorBrush(UiH.Verde),
            Foreground = Brushes.White, BorderThickness = new Thickness(0),
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
        };
        btnNuevo.Click += (_, __) => onNuevo();
        row.Children.Add(btnNuevo);

        sp.Children.Add(row);
        return sp;
    }

    private void NuevoMaestro(string tipo, Func<string, string, Task> guardar)
    {
        var dlg = new NuevoMaestroDialog(tipo) { Owner = this };
        if (dlg.ShowDialog() == true)
            _ = guardar(dlg.Codigo, dlg.Nombre);
    }

    private void Pick(IEnumerable<object> items, string prop, Action<object> onSel)
    {
        var dlg = new LupaDialog(prop, items, prop) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Seleccionado != null) onSel(dlg.Seleccionado);
    }

    private async Task SaveAsync()
    {
        var copy = new Articulo
        {
            Id = _art.Id, Ca = _art.Ca, Serial = _art.Serial, D = _art.D, Pres = _art.Pres,
            Smin = _art.Smin, Iva = _art.Iva, Maxcuota = _art.Maxcuota, Gra = _art.Gra,
            Slc = _art.Slc, Es = _art.Es, Vto = _art.Vto, Vu = _art.Vu,
            Ids = _sec?.IdSeccion, Idc = _cat?.IdCategoria,
            Idsbc = _sub?.IdSubcategoria, Idm = _marca?.IdMarca,
            Idpr = _art.Idpr, Idmed = _art.Idmed, Idpais = _art.Idpais
        };
        try
        {
            await _repo.ActualizarAsync(copy);
            MessageBox.Show("Clasificación actualizada.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true; Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: NUEVO MAESTRO (Sección / Marca / Categoría / Subcategoría)
// ════════════════════════════════════════════════════════════════════════════
public class NuevoMaestroDialog : Window
{
    public string Codigo { get; private set; } = "";
    public string Nombre { get; private set; } = "";
    private TextBox _txtCodigo = null!, _txtNombre = null!;

    public NuevoMaestroDialog(string tipo)
    {
        Title = $"Nueva {tipo}";
        Width = 360; Height = 210;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        ResizeMode = ResizeMode.NoResize;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; } };
        Content = Build(tipo);
    }

    private UIElement Build(string tipo)
    {
        var root = new StackPanel { Margin = new Thickness(16) };

        var sec = UiH.SectionPanel($"Nueva {tipo}", out var panel);
        panel.Children.Add(UiH.FieldGroup("Código", out _txtCodigo, double.NaN));
        panel.Children.Add(UiH.FieldGroup("Nombre", out _txtNombre, double.NaN, marginRight: 0));
        root.Children.Add(sec);

        var pie = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var bG = UiH.Btn("💾  Guardar", UiH.Verde);
        bG.Click += (_, __) =>
        {
            if (string.IsNullOrWhiteSpace(_txtNombre.Text))
            { MessageBox.Show("El nombre es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            Codigo = _txtCodigo.Text.Trim();
            Nombre = _txtNombre.Text.Trim();
            DialogResult = true;
        };
        var bC = UiH.Btn("✕  Cancelar", UiH.Gris); bC.Click += (_, __) => { DialogResult = false; };
        btns.Children.Add(bG); btns.Children.Add(bC);
        System.Windows.Controls.Grid.SetColumn(btns, 1); pie.Children.Add(btns);
        root.Children.Add(pie);

        return root;
    }
}

internal class InhabRow : System.ComponentModel.INotifyPropertyChanged
{
    private bool _seleccionado;
    public int IdArt { get; set; }
    public string Ca { get; set; } = "";
    public string D { get; set; } = "";
    public string LocalNombre { get; set; } = "";
    public int IdLocal { get; set; }
    public decimal Stock { get; set; }
    // Checkbox por fila — antes la única forma de elegir varios locales de un mismo artículo
    // era Ctrl+click en la fila del DataGrid, poco descubrible una vez que las filas quedan
    // anidadas dentro de un grupo expandido (reportado real: "agregar checkbox o algo para
    // que puedan seleccionar varios locales").
    public bool IsSeleccionado
    {
        get => _seleccionado;
        set { _seleccionado = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSeleccionado))); }
    }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: INHABILITAR / HABILITAR por local
// ════════════════════════════════════════════════════════════════════════════
public class InhabilitarHabilitarDialog : Window
{
    private readonly IArticuloRepository _repo;
    private readonly List<Local> _locales;
    private readonly bool _inhab;
    private int _idLocal = 0;
    private TextBox _dLocalNombre = null!, _txtCodigo = null!, _dDescArt = null!;
    private List<object> _todosPrecios = new();
    private DataGrid _grid = null!;
    private List<CheckBox> _chkLocales = new();

    public InhabilitarHabilitarDialog(IArticuloRepository repo, List<Local> locales, bool inhabilitar)
    {
        _repo = repo; _locales = locales; _inhab = inhabilitar;
        Title = inhabilitar ? "Inhabilitar Artículo por Local" : "Habilitar Artículo por Local";
        Width = 920; Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        Content = Build();
    }

    private UIElement Build()
    {
        var root = UiH.MkGrid(rows: new[] { "Auto", "Auto", "*", "Auto", "Auto" });
        root.Margin = new Thickness(16);

        // Selección — en modo Inhabilitar: buscar UN artículo puntual por código (flujo
        // original). En modo Habilitar: el "Código artículo" pasa a ser un filtro de texto
        // libre (código o descripción) sobre la lista completa ya cargada, ya que acá el
        // usuario parte de "no sé qué está inhabilitado" en vez de saber qué buscar.
        var secSel = UiH.SectionPanel(_inhab ? "Selección de artículo y local" : "Filtrar lista de inhabilitados", out var panelSel);
        var selRow = new WrapPanel();
        selRow.Children.Add(UiH.LupaGroup("Local de búsqueda", out _dLocalNombre, () => PickLocal(), 180));
        if (_inhab)
        {
            selRow.Children.Add(UiH.LupaGroup("Código artículo", out _txtCodigo, async () => await PickArtAsync(), 120));
            selRow.Children.Add(UiH.LupaGroup("Descripción",     out _dDescArt,  async () => await PickArtAsync(), 260, 0));
            _dDescArt.IsReadOnly = true;
        }
        else
        {
            _txtCodigo = new TextBox { Width = 220, Padding = new Thickness(6, 4, 6, 4) };
            var filtroGroup = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            filtroGroup.Children.Add(new TextBlock { Text = "Buscar (código o descripción)", FontSize = 10,
                Foreground = new SolidColorBrush(UiH.TextoSub), Margin = new Thickness(0, 0, 0, 3) });
            filtroGroup.Children.Add(_txtCodigo);
            selRow.Children.Add(filtroGroup);
        }
        panelSel.Children.Add(selRow);
        var btnFila = new StackPanel { Orientation = Orientation.Horizontal };
        var btnIngresar = UiH.Btn("Buscar", UiH.Azul, 80);
        btnIngresar.Click += async (_, __) => { if (_inhab) await CargarGridAsync(); else await CargarInhabilitadosAsync(); };
        var btnLimpiarLocal = UiH.Btn("✕ Quitar filtro local", UiH.Gris, 140);
        btnLimpiarLocal.Click += async (_, __) => {
            _idLocal = 0; _dLocalNombre.Text = "";
            if (_inhab) FiltrarGrid(); else await CargarInhabilitadosAsync();
        };
        btnFila.Children.Add(btnIngresar);
        btnFila.Children.Add(btnLimpiarLocal);
        panelSel.Children.Add(btnFila);
        UiH.SetRow(secSel, 0); root.Children.Add(secSel);

        // Hint
        var hint = new TextBlock
        {
            Text = _inhab
                ? "Seleccione artículo y luego marque los locales donde desea inhabilitar."
                : "Lista de artículos actualmente inhabilitados por local — los que tienen stock cargado aparecen primero. Expanda un artículo, tilde los locales (o use \"marcar todos\") y pulse Habilitar seleccionados.",
            Foreground = new SolidColorBrush(UiH.TextoSub), FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        UiH.SetRow(hint, 1); root.Children.Add(hint);

        // Grid con selección resaltada
        _grid = UiH.ModernGrid();
        _grid.SelectionMode = DataGridSelectionMode.Extended;
        _grid.Background = Brushes.White;
        _grid.RowBackground = Brushes.White;
        _grid.AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 249, 250));
        if (_inhab)
        {
            _grid.Columns.Add(UiH.Col("Código",      "Ca",          160));
            _grid.Columns.Add(UiH.Col("Descripción", "D",           0, star: true));
            _grid.Columns.Add(UiH.Col("Local",        "LocalNombre", 160));
        }
        else
        {
            // En modo Habilitar la columna Código/Descripción queda en el header del grupo
            // (ver GroupStyle más abajo) — acá solo el detalle por local, que es lo que
            // aparece al expandir. Sin esto, un mismo artículo inhabilitado en 14 locales
            // repetía su código/descripción 14 veces seguidas en la lista, puro ruido visual
            // (reportado real: "veo muchos artículos iguales").
            _grid.SelectionMode = DataGridSelectionMode.Single; // la selección real ahora es el checkbox, no la fila
            _grid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "", Binding = new System.Windows.Data.Binding(nameof(InhabRow.IsSeleccionado)) { Mode = System.Windows.Data.BindingMode.TwoWay },
                Width = 36, ElementStyle = BuildCheckboxCellStyle()
            });
            _grid.Columns.Add(UiH.Col("Local", "LocalNombre", 220));
            _grid.Columns.Add(UiH.Col("Stock", "Stock", 100));

            // Doble click en cualquier parte de la fila alterna el checkbox — antes solo
            // clickear el cuadrito chico lo tildaba, poco cómodo para tildar varias filas
            // rápido (pedido explícito: "doble click ya se active el checkbox").
            _grid.MouseDoubleClick += (_, __) =>
            {
                if (_grid.SelectedItem is InhabRow fila) fila.IsSeleccionado = !fila.IsSeleccionado;
            };
        }

        // Hover: amarillo claro — Selección: naranja fuerte con texto negro
        var selBg    = new SolidColorBrush(Color.FromRgb(255, 165,   0)); // naranja
        var hoverBg  = new SolidColorBrush(Color.FromRgb(255, 236, 179)); // amarillo claro
        var darkText = new SolidColorBrush(Color.FromRgb( 20,  20,  20)); // negro casi puro

        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty, Brushes.White));
        rowStyle.Setters.Add(new Setter(DataGridRow.ForegroundProperty, darkText));
        rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
        var hoverTrigger = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, hoverBg));
        hoverTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, darkText));
        rowStyle.Triggers.Add(hoverTrigger);
        var selTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        selTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, selBg));
        selTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, darkText));
        selTrigger.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.Bold));
        rowStyle.Triggers.Add(selTrigger);
        _grid.RowStyle = rowStyle;

        // CellStyle: siempre Transparent para no tapar el color de la fila
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        var cellSel = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        cellSel.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        cellSel.Setters.Add(new Setter(DataGridCell.ForegroundProperty, darkText));
        cellSel.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
        cellStyle.Triggers.Add(cellSel);
        var cellHover = new Trigger { Property = DataGridCell.IsMouseOverProperty, Value = true };
        cellHover.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        cellStyle.Triggers.Add(cellHover);
        _grid.CellStyle = cellStyle;

        var gridBorder = new Border { Child = _grid, CornerRadius = new CornerRadius(6), ClipToBounds = true, Margin = new Thickness(0, 0, 0, 10) };
        UiH.SetRow(gridBorder, 2); root.Children.Add(gridBorder);

        // Checkboxes locales — solo tienen sentido en modo Inhabilitar (un artículo puntual,
        // elegir en qué locales sacarlo). En modo Habilitar la grilla lista muchos artículos
        // distintos, cada uno YA con su propio local (columna "Local" de la fila); replicar el
        // mismo local elegido a todas las filas seleccionadas no tendría sentido ahí.
        var chkBorder = new Border
        {
            Background = new SolidColorBrush(UiH.FondoPanel),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10),
            Visibility = _inhab ? Visibility.Visible : Visibility.Collapsed
        };
        var chkSp = new StackPanel();
        chkSp.Children.Add(new TextBlock { Text = "APLICAR EN LOCALES:", Foreground = new SolidColorBrush(UiH.TextoSub), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
        var chkWrap = new WrapPanel();
        foreach (var local in _locales)
        {
            var chk = new CheckBox
            {
                Content = local.NombreLocal, Tag = local.IdLocal,
                Foreground = new SolidColorBrush(UiH.TextoLabel),
                Margin = new Thickness(0, 0, 14, 4)
            };
            _chkLocales.Add(chk);
            chkWrap.Children.Add(chk);
        }
        chkSp.Children.Add(chkWrap);
        chkBorder.Child = chkSp;
        UiH.SetRow(chkBorder, 3); root.Children.Add(chkBorder);

        // Botones
        var pie = new Grid();
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hintPie = UiH.Hint(_inhab
            ? "Marque los locales y pulse Guardar."
            : "Tilde los checkboxes de los locales a habilitar y pulse Habilitar seleccionados.");
        System.Windows.Controls.Grid.SetColumn(hintPie, 0); pie.Children.Add(hintPie);
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var color = _inhab ? UiH.Rojo : UiH.Verde;
        var label = _inhab ? "🚫  Inhabilitar" : "✓  Habilitar seleccionados";
        var bG = UiH.Btn(label, color); bG.Click += async (_, __) => await SaveAsync();
        var bC = UiH.Btn("✕  Cerrar", UiH.Gris); bC.Click += (_, __) => Close();
        btns.Children.Add(bG); btns.Children.Add(bC);
        System.Windows.Controls.Grid.SetColumn(btns, 1); pie.Children.Add(btns);
        UiH.SetRow(pie, 4); root.Children.Add(pie);

        if (!_inhab)
        {
            _grid.GroupStyle.Add(BuildGroupStyleArticulo());
            Loaded += async (_, __) => await CargarInhabilitadosAsync();
        }

        return root;
    }

    private static Style BuildCheckboxCellStyle()
    {
        var style = new Style(typeof(CheckBox));
        style.Setters.Add(new Setter(HorizontalAlignmentProperty, HorizontalAlignment.Center));
        return style;
    }

    // Grupo colapsable real — un artículo inhabilitado en 14 locales antes repetía su código/
    // descripción 14 veces seguidas en la lista (ruido puro, reportado real). Con solo
    // GroupStyle.HeaderTemplate + Expander, el Expander.IsExpanded NO controla nada: WPF sigue
    // dibujando las filas del grupo por fuera, vía su propio ItemsPresenter — el triángulo se
    // movía pero nunca ocultaba ni mostraba nada (bug real reportado: "pulso y no comprime").
    // El patrón correcto es GroupStyle.ContainerStyle retemplateando el GroupItem COMPLETO,
    // con el Expander conteniendo el ItemsPresenter real como su Content — ahí sí
    // IsExpanded oculta/muestra las filas de verdad.
    private GroupStyle BuildGroupStyleArticulo()
    {
        var xaml = @"
        <Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
               xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
               TargetType='{x:Type GroupItem}'>
          <Setter Property='Template'>
            <Setter.Value>
              <ControlTemplate TargetType='{x:Type GroupItem}'>
                <Expander IsExpanded='False' Margin='4,2,0,2'>
                  <Expander.Header>
                    <StackPanel Orientation='Horizontal'>
                      <TextBlock Text='{Binding Items[0].Ca}' FontWeight='Bold' Width='150'/>
                      <TextBlock Text='{Binding Items[0].D}' Width='300' TextTrimming='CharacterEllipsis'/>
                      <TextBlock Foreground='#6B7280' FontStyle='Italic' FontSize='11'>
                        <TextBlock.Text>
                          <Binding Path='ItemCount' StringFormat='{}{0} local(es) inhabilitado(s) — click para ver/seleccionar'/>
                        </TextBlock.Text>
                      </TextBlock>
                      <TextBlock Name='PART_MarcarTodos' Text='  ✓ marcar todos'
                                 Foreground='#1A4F6E' FontSize='11' FontWeight='Bold' Cursor='Hand'
                                 Margin='12,0,0,0'/>
                    </StackPanel>
                  </Expander.Header>
                  <ItemsPresenter Margin='24,0,0,0'/>
                </Expander>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Style>";
        var containerStyle = (Style)System.Windows.Markup.XamlReader.Parse(xaml);
        return new GroupStyle { ContainerStyle = containerStyle };
    }

    // "marcar todos" del header de grupo no puede resolverse con un Command en puro XAML acá
    // (el DataContext del header es el CollectionViewGroup, sin acceso directo a este code-
    // behind) — se engancha el Click a mano después de que cada GroupItem se genera, vía el
    // evento ItemContainerGenerator del DataGrid.
    private void EnlazarMarcarTodosDeGrupos()
    {
        void Enganchar(DependencyObject visual)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
            {
                var child = VisualTreeHelper.GetChild(visual, i);
                if (child is TextBlock tb && tb.Name == "PART_MarcarTodos" &&
                    tb.Tag as string != "enganchado")
                {
                    tb.Tag = "enganchado";
                    tb.MouseLeftButtonUp += (_, __) =>
                    {
                        if (tb.DataContext is System.Windows.Data.CollectionViewGroup grupo)
                            foreach (var item in grupo.Items.OfType<InhabRow>())
                                item.IsSeleccionado = true;
                    };
                }
                Enganchar(child);
            }
        }
        Enganchar(_grid);
    }

    private async void PickLocal()
    {
        var dlg = new LupaDialog("Seleccionar local", _locales.Cast<object>(), "NombreLocal") { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Seleccionado is Local l)
        {
            _idLocal = l.IdLocal;
            _dLocalNombre.Text = l.NombreLocal;
            if (_inhab) FiltrarGrid(); else await CargarInhabilitadosAsync();
        }
    }

    private async Task PickArtAsync()
    {
        var dlg = new VerArticulosWindow(this);
        if (dlg.ShowDialog() == true && dlg.ArticuloSeleccionado != null)
        {
            _txtCodigo.Text = dlg.ArticuloSeleccionado.Codigo;
            _dDescArt.Text  = dlg.ArticuloSeleccionado.Desc;
            await CargarGridAsync();
        }
    }

    private async Task CargarGridAsync()
    {
        var codigo = _txtCodigo.Text.Trim();
        if (string.IsNullOrWhiteSpace(codigo)) return;
        var art = await _repo.BuscarPorCodigoAsync(codigo);
        if (art == null) return;
        var precios = await _repo.ObtenerStockTodosLocalesAsync(art.Id);
        _todosPrecios = precios
            .Select(p => (object)new InhabRow { IdArt = art.Id, Ca = art.Ca, D = art.D, LocalNombre = p.LocalNombre, IdLocal = p.IdLocal })
            .ToList();
        FiltrarGrid();
    }

    // Lista TODOS los artículos/local inhabilitados de una — antes, el modo Habilitar exigía
    // saber de antemano el código exacto del artículo a reactivar; sin forma de "ver qué está
    // inhabilitado", el usuario no tenía cómo enterarse de que algo necesitaba corregirse.
    // Pedido explícito: mostrar la lista completa al abrir, con filtro opcional encima.
    private async Task CargarInhabilitadosAsync()
    {
        var filtro = string.IsNullOrWhiteSpace(_txtCodigo.Text) ? null : _txtCodigo.Text.Trim();
        try
        {
            var filas = await _repo.ObtenerInhabilitadosAsync(filtro, _idLocal);
            _todosPrecios = filas
                .Select(r => (object)new InhabRow { IdArt = r.IdArt, Ca = r.Ca, D = r.D, LocalNombre = r.LocalNombre, IdLocal = r.IdLocal, Stock = r.Stock })
                .ToList();

            // Agrupado por artículo (Ca) — cada grupo se ve como un Expander colapsado (ver
            // BuildGroupStyleArticulo), evitando repetir código/descripción por cada local.
            var vista = System.Windows.Data.CollectionViewSource.GetDefaultView(_todosPrecios);
            vista.GroupDescriptions.Clear();
            vista.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(InhabRow.Ca)));
            _grid.ItemsSource = vista;

            // El "✓ marcar todos" de cada header de grupo recién existe en el árbol visual
            // después de que el layout termine de generar los GroupItem — engancharlo antes
            // (ej. justo después de asignar ItemsSource) no encuentra nada todavía.
            Dispatcher.InvokeAsync(EnlazarMarcarTodosDeGrupos, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo cargar la lista de inhabilitados: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FiltrarGrid()
    {
        if (_idLocal == 0)
            _grid.ItemsSource = _todosPrecios;
        else
            _grid.ItemsSource = _todosPrecios
                .Cast<InhabRow>()
                .Where(r => r.IdLocal == _idLocal)
                .ToList<object>();
    }

    private async Task SaveAsync()
    {
        if (_inhab) await SaveInhabilitarAsync();
        else        await SaveHabilitarSeleccionadosAsync();
    }

    private async Task SaveInhabilitarAsync()
    {
        var selLocales = _chkLocales.Where(c => c.IsChecked == true).Select(c => (int)c.Tag!).ToList();
        if (!selLocales.Any()) { MessageBox.Show("Marque al menos un local.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var codigo = _txtCodigo.Text.Trim();
        if (string.IsNullOrWhiteSpace(codigo)) { MessageBox.Show("Ingrese el código del artículo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var art = await _repo.BuscarPorCodigoAsync(codigo);
        if (art == null) { MessageBox.Show("Artículo no encontrado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        var dlgPass = new ConfirmarPasswordDialog { Owner = this };
        if (dlgPass.ShowDialog() != true) return;

        var nombresLocales = _chkLocales
            .Where(c => c.IsChecked == true)
            .Select(c => c.Content?.ToString() ?? "")
            .ToList();
        var dlgConf = new ConfirmarAccionDialog("inhabilitar", art.Ca, art.D, nombresLocales) { Owner = this };
        if (dlgConf.ShowDialog() != true) return;

        try
        {
            var idUsuario = SessionService.Instance.UsuarioActual?.IdUsuario ?? 0;
            foreach (var idL in selLocales)
                await _repo.InhabilitarEnLocalAsync(art.Id, idL, true, idUsuario, Environment.MachineName);
            MessageBox.Show("Artículo inhabilitado en los locales seleccionados.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    // Habilita cada fila SELECCIONADA de la grilla — a diferencia de Inhabilitar, acá cada
    // fila ya trae su propio artículo Y local (son combinaciones distintas entre sí, no "un
    // artículo en varios locales"), así que no hay un único IdArt/lista de locales común para
    // pedir contraseña/confirmar una sola vez con un solo nombre — se resume la cantidad de
    // pares distintos en la confirmación en vez de listarlos todos si son muchos.
    private async Task SaveHabilitarSeleccionadosAsync()
    {
        // Selección real ahora es el checkbox por fila (IsSeleccionado), no SelectedItems del
        // DataGrid — con las filas agrupadas y colapsables, Ctrl+click dejó de ser práctico
        // para elegir varias a la vez (pedido explícito: "agregar checkbox... para que puedan
        // seleccionar varios locales").
        var seleccion = _todosPrecios.Cast<InhabRow>().Where(r => r.IsSeleccionado).ToList();
        if (seleccion.Count == 0)
        {
            MessageBox.Show("Marque al menos un checkbox de la lista.",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlgPass = new ConfirmarPasswordDialog { Owner = this };
        if (dlgPass.ShowDialog() != true) return;

        var resumenLocales = seleccion.Count <= 6
            ? seleccion.Select(r => $"{r.Ca} → {r.LocalNombre}").ToList()
            : new List<string> { $"{seleccion.Count} artículos/local distintos" };
        var dlgConf = new ConfirmarAccionDialog("habilitar", $"{seleccion.Count} ítem(s)", "artículos seleccionados en la grilla", resumenLocales) { Owner = this };
        if (dlgConf.ShowDialog() != true) return;

        try
        {
            var idUsuario = SessionService.Instance.UsuarioActual?.IdUsuario ?? 0;
            foreach (var fila in seleccion)
                await _repo.InhabilitarEnLocalAsync(fila.IdArt, fila.IdLocal, false, idUsuario, Environment.MachineName);
            MessageBox.Show($"{seleccion.Count} artículo(s) habilitado(s).", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            await CargarInhabilitadosAsync(); // refresca la lista — las filas ya habilitadas desaparecen
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: CONFIRMAR CONTRASEÑA
// ════════════════════════════════════════════════════════════════════════════
public class ConfirmarPasswordDialog : Window
{
    private PasswordBox _pwdBox = null!;

    public ConfirmarPasswordDialog()
    {
        Title = "Confirmar identidad";
        Width = 360; Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        ResizeMode = ResizeMode.NoResize;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };
        Content = Build();
    }

    private UIElement Build()
    {
        var root = new StackPanel { Margin = new Thickness(20) };

        var sec = UiH.SectionPanel("Verificación de usuario", out var panel);
        panel.Children.Add(new TextBlock
        {
            Text = "Ingrese su contraseña para continuar:",
            Foreground = new SolidColorBrush(UiH.TextoSub),
            FontSize = 11, Margin = new Thickness(0, 0, 0, 8)
        });
        _pwdBox = new PasswordBox
        {
            Height = 30, Padding = new Thickness(8, 5, 8, 5),
            Background = Brushes.White, Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(UiH.BorderInput),
            BorderThickness = new Thickness(1), FontSize = 13,
        };
        _pwdBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) Confirmar(); };
        panel.Children.Add(_pwdBox);
        root.Children.Add(sec);

        var btns = UiH.BtnBar(
            UiH.Btn("✓  Confirmar", UiH.Verde),
            UiH.Btn("✕  Cancelar",  UiH.Gris));
        ((Button)btns.Children[0]).Click += (_, __) => Confirmar();
        ((Button)btns.Children[1]).Click += (_, __) => { DialogResult = false; };
        root.Children.Add(btns);

        Loaded += (_, __) => _pwdBox.Focus();
        return root;
    }

    private void Confirmar()
    {
        var usuario = SessionService.Instance.UsuarioActual;
        if (usuario == null) { DialogResult = false; return; }
        // Verificar contra la contraseña almacenada en sesión
        if (_pwdBox.Password == usuario.ContrasenaUsuario)
            DialogResult = true;
        else
            MessageBox.Show("Contraseña incorrecta.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: CONFIRMAR ACCIÓN (inhabilitar / habilitar)
// ════════════════════════════════════════════════════════════════════════════
public class ConfirmarAccionDialog : Window
{
    public ConfirmarAccionDialog(string accion, string codigo, string descripcion, List<string> locales)
    {
        Title = $"Confirmar — {char.ToUpper(accion[0]) + accion[1..]}";
        Width = 480; Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        ResizeMode = ResizeMode.NoResize;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };
        Content = Build(accion, codigo, descripcion, locales);
    }

    private UIElement Build(string accion, string codigo, string descripcion, List<string> locales)
    {
        var root = new StackPanel { Margin = new Thickness(20) };

        // Pregunta principal
        var colorAccion = accion == "inhabilitar" ? UiH.Rojo : UiH.Verde;
        root.Children.Add(new TextBlock
        {
            Text = $"¿Estás seguro de {accion} el siguiente artículo?",
            Foreground = new SolidColorBrush(colorAccion),
            FontSize = 13, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 12),
        });

        // Panel con datos del artículo
        var artPanel = new Border
        {
            Background = new SolidColorBrush(UiH.FondoPanel),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 12),
        };
        var artSp = new StackPanel();
        artSp.Children.Add(new TextBlock { Text = $"Código:      {codigo}", Foreground = new SolidColorBrush(UiH.TextoLabel), FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
        artSp.Children.Add(new TextBlock { Text = $"Descripción: {descripcion}", Foreground = new SolidColorBrush(UiH.TextoLabel), FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
        artSp.Children.Add(new TextBlock
        {
            Text = $"Sucursales:  {string.Join(", ", locales)}",
            Foreground = new SolidColorBrush(UiH.Naranja),
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        artPanel.Child = artSp;
        root.Children.Add(artPanel);

        // Botones
        var btns = UiH.BtnBar(
            UiH.Btn($"✓  Sí, {accion}", colorAccion),
            UiH.Btn("✕  Cancelar", UiH.Gris));
        ((Button)btns.Children[0]).Click += (_, __) => { DialogResult = true; };
        ((Button)btns.Children[1]).Click += (_, __) => { DialogResult = false; };
        root.Children.Add(btns);

        return root;
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Dialog: Seleccionar marca (modal tipo lupa)
// ════════════════════════════════════════════════════════════════════════════
public class SeleccionarMarcaDialog : Window
{
    private readonly List<string> _marcas;
    private TextBox   _txtFiltro = null!;
    private DataGrid  _grid      = null!;
    public  string    MarcaSeleccionada { get; private set; } = "";

    public SeleccionarMarcaDialog(List<string> marcas, string marcaActiva)
    {
        _marcas               = marcas;
        Title                 = "Seleccionar marca";
        Width                 = 420;
        Height                = 480;
        MinWidth              = 320;
        MinHeight             = 300;
        ResizeMode            = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = new SolidColorBrush(UiH.FondoDlg);
        Content               = Build(marcaActiva);
        KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Escape) { DialogResult = false; }
            if (ke.Key == Key.Enter)  { Aceptar(); ke.Handled = true; }
        };
    }

    private UIElement Build(string marcaActiva)
    {
        var root = UiH.MkGrid(new[] { "Auto", "*", "Auto" });
        root.Margin = new Thickness(14);

        // ── Filtro ────────────────────────────────────────────────────────────
        var filtroBorder = new Border
        {
            Background   = new SolidColorBrush(UiH.FondoPanel),
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(10, 7, 10, 7),
            Margin       = new Thickness(0, 0, 0, 8),
        };
        var filtroRow = new Grid();
        filtroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filtroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lblBuscar = new TextBlock
        {
            Text              = "Buscar:",
            Foreground        = new SolidColorBrush(UiH.TextoLabel),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight        = FontWeights.SemiBold,
            FontSize          = 11,
            Margin            = new Thickness(0, 0, 8, 0),
        };
        System.Windows.Controls.Grid.SetColumn(lblBuscar, 0); filtroRow.Children.Add(lblBuscar);
        _txtFiltro = UiH.Input();
        _txtFiltro.Height = 28;
        _txtFiltro.TextChanged += (_, __) => Filtrar(_txtFiltro.Text.Trim());
        System.Windows.Controls.Grid.SetColumn(_txtFiltro, 1); filtroRow.Children.Add(_txtFiltro);
        filtroBorder.Child = filtroRow;
        UiH.SetRow(filtroBorder, 0); root.Children.Add(filtroBorder);

        // ── Grid (tema oscuro para encajar con el fondo del modal) ──────────────
        _grid = UiH.ModernGrid();
        _grid.Background              = new SolidColorBrush(Color.FromRgb(30, 42, 52));
        _grid.RowBackground           = new SolidColorBrush(Color.FromRgb(30, 42, 52));
        _grid.AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(36, 50, 62));
        _grid.BorderThickness         = new Thickness(0);

        var whiteText = Brushes.White;
        var rowSt = new Style(typeof(DataGridRow));
        rowSt.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(30, 42, 52))));
        rowSt.Setters.Add(new Setter(DataGridRow.ForegroundProperty, whiteText));
        rowSt.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
        var hov = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
        hov.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(62, 86, 108))));
        hov.Setters.Add(new Setter(DataGridRow.ForegroundProperty, whiteText));
        rowSt.Triggers.Add(hov);
        var sel = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        sel.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(230, 126, 34))));
        sel.Setters.Add(new Setter(DataGridRow.ForegroundProperty, whiteText));
        sel.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.Bold));
        rowSt.Triggers.Add(sel);
        _grid.RowStyle = rowSt;

        var cellSt = new Style(typeof(DataGridCell));
        cellSt.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
        cellSt.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
        cellSt.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        cellSt.Setters.Add(new Setter(DataGridCell.ForegroundProperty, whiteText));
        var csel = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        csel.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        csel.Setters.Add(new Setter(DataGridCell.ForegroundProperty, whiteText));
        csel.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
        cellSt.Triggers.Add(csel);
        _grid.CellStyle = cellSt;

        _grid.Columns.Add(UiH.Col("Marca", "Nombre", 0, star: true));
        _grid.MouseDoubleClick += (_, __) => Aceptar();
        var gridBorder = new Border
        {
            Child        = _grid,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Margin       = new Thickness(0, 0, 0, 8),
        };
        UiH.SetRow(gridBorder, 1); root.Children.Add(gridBorder);

        // ── Pie ───────────────────────────────────────────────────────────────
        var bot = new Grid();
        bot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hint = UiH.Hint("Doble-clic o Enter: Seleccionar   Esc: Cancelar");
        hint.VerticalAlignment = VerticalAlignment.Center;
        System.Windows.Controls.Grid.SetColumn(hint, 0); bot.Children.Add(hint);

        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var btnTodas = UiH.Btn("Todas las marcas", Color.FromRgb(62, 86, 108));
        var btnOk    = UiH.Btn("✓  Aceptar",       UiH.Verde);
        var btnCx    = UiH.Btn("✕  Cancelar",      UiH.Gris);
        btnTodas.Click += (_, __) => { MarcaSeleccionada = ""; DialogResult = true; };
        btnOk.Click    += (_, __) => Aceptar();
        btnCx.Click    += (_, __) => { DialogResult = false; };
        btns.Children.Add(btnTodas);
        btns.Children.Add(btnOk);
        btns.Children.Add(btnCx);
        System.Windows.Controls.Grid.SetColumn(btns, 1); bot.Children.Add(btns);
        UiH.SetRow(bot, 2); root.Children.Add(bot);

        Filtrar("");

        if (!string.IsNullOrEmpty(marcaActiva))
        {
            Loaded += (_, __) =>
            {
                var item = (_grid.ItemsSource as IEnumerable<MarcaItem>)?
                    .FirstOrDefault(m => m.Nombre == marcaActiva);
                if (item != null)
                {
                    _grid.SelectedItem = item;
                    _grid.ScrollIntoView(item);
                }
                _txtFiltro.Focus();
            };
        }
        else
        {
            Loaded += (_, __) => _txtFiltro.Focus();
        }

        return root;
    }

    private void Filtrar(string texto)
    {
        var lista = string.IsNullOrEmpty(texto)
            ? _marcas
            : _marcas.Where(m => m.ToLowerInvariant().Contains(texto.ToLowerInvariant())).ToList();
        _grid.ItemsSource = lista.Select(m => new MarcaItem(m)).ToList();
    }

    private void Aceptar()
    {
        if (_grid.SelectedItem is MarcaItem m)
        {
            MarcaSeleccionada = m.Nombre;
            DialogResult      = true;
        }
    }
}

// Record interno para el DataGrid de marcas
internal record MarcaItem(string Nombre);
