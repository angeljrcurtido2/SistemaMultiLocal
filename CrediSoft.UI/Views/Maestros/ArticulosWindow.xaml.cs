using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
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
    private TextBox _txtBuscar = null!;
    private DataGrid _grid = null!;
    private List<Articulo> _todos = new();
    public Articulo? Seleccionado { get; private set; }

    public BuscarArticuloDialog(IArticuloRepository repo, string titulo)
    {
        _repo = repo;
        Title = titulo;
        Width = 600; Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        Content = Build();
        KeyDown += (_, ke) => { if (ke.Key == Key.Escape) DialogResult = false; };
        Loaded += async (_, __) => await CargarTodosAsync();
    }

    private UIElement Build()
    {
        var root = UiH.MkGrid(rows: new[] { "Auto", "*", "Auto" });
        root.Margin = new Thickness(16);

        // Barra de búsqueda
        var searchPanel = new Border
        {
            Background = new SolidColorBrush(UiH.FondoPanel),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10),
        };
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        wrap.Children.Add(new TextBlock { Text = "Buscar:", Foreground = new SolidColorBrush(UiH.TextoLabel), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        _txtBuscar = UiH.Input(400);
        _txtBuscar.TextChanged += (_, __) => FiltrarEnMemoria();
        wrap.Children.Add(_txtBuscar);
        searchPanel.Child = wrap;
        UiH.SetRow(searchPanel, 0); root.Children.Add(searchPanel);

        // Grid
        _grid = UiH.ModernGrid();
        _grid.Columns.Add(UiH.Col("Código",      "Ca",          90));
        _grid.Columns.Add(UiH.Col("Descripción", "D",           0, star: true));
        _grid.Columns.Add(UiH.Col("Marca",       "MarcaNombre", 110));
        _grid.Columns.Add(UiH.Col("Estado",      "EstadoTexto", 75));
        _grid.MouseDoubleClick += (_, __) => Aceptar();
        var gridBorder = new Border { Child = _grid, CornerRadius = new CornerRadius(6), ClipToBounds = true, Margin = new Thickness(0, 0, 0, 10) };
        UiH.SetRow(gridBorder, 1); root.Children.Add(gridBorder);

        // Botones
        var bot = new Grid();
        bot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hint = UiH.Hint("Escribir filtra la lista   Doble-clic o Aceptar: Seleccionar   Esc: Cancelar");
        System.Windows.Controls.Grid.SetColumn(hint, 0); bot.Children.Add(hint);
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var btnOk = UiH.Btn("✓  Aceptar", UiH.Verde);   btnOk.Click += (_, __) => Aceptar();
        var btnCx = UiH.Btn("✕  Cancelar", UiH.Gris);   btnCx.Click += (_, __) => { DialogResult = false; };
        btns.Children.Add(btnOk); btns.Children.Add(btnCx);
        System.Windows.Controls.Grid.SetColumn(btns, 1); bot.Children.Add(btns);
        UiH.SetRow(bot, 2); root.Children.Add(bot);

        return root;
    }

    private async Task CargarTodosAsync()
    {
        _todos = (await _repo.BuscarTodosAsync()).ToList();
        _grid.ItemsSource = _todos;
        _txtBuscar.Focus();
    }

    private void FiltrarEnMemoria()
    {
        var term = _txtBuscar.Text.Trim().ToLowerInvariant();
        _grid.ItemsSource = string.IsNullOrEmpty(term)
            ? _todos
            : _todos.Where(a => a.Ca.ToLowerInvariant().Contains(term) || a.D.ToLowerInvariant().Contains(term)).ToList();
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
    private readonly string _displayProp;
    private readonly List<object> _todos;
    private DataGrid _grid = null!;
    private TextBox _txtFiltro = null!;
    public object? Seleccionado { get; private set; }

    public LupaDialog(string titulo, IEnumerable<object> items, string displayProp)
    {
        _displayProp = displayProp;
        _todos = items.ToList();
        Title = titulo;
        Width = 440; Height = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        Content = Build();
        KeyDown += (_, ke) => { if (ke.Key == Key.Escape) DialogResult = false; };
    }

    private UIElement Build()
    {
        var root = UiH.MkGrid(rows: new[] { "Auto", "*", "Auto" });
        root.Margin = new Thickness(16);

        // Filtro
        var filtroPanel = new Border
        {
            Background = new SolidColorBrush(UiH.FondoPanel),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10),
        };
        var filtroRow = new StackPanel { Orientation = Orientation.Horizontal };
        filtroRow.Children.Add(new TextBlock { Text = "Filtrar:", Foreground = new SolidColorBrush(UiH.TextoLabel), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), FontWeight = FontWeights.SemiBold });
        _txtFiltro = UiH.Input();
        _txtFiltro.TextChanged += (_, __) => Filtrar();
        filtroRow.Children.Add(_txtFiltro);
        filtroPanel.Child = filtroRow;
        UiH.SetRow(filtroPanel, 0); root.Children.Add(filtroPanel);

        // Grid
        _grid = UiH.ModernGrid();
        _grid.Columns.Add(UiH.Col("Nombre / Descripción", _displayProp, 0, star: true));
        _grid.MouseDoubleClick += (_, __) => Aceptar();
        _grid.ItemsSource = _todos;
        var gridBorder = new Border { Child = _grid, CornerRadius = new CornerRadius(6), ClipToBounds = true, Margin = new Thickness(0, 0, 0, 10) };
        UiH.SetRow(gridBorder, 1); root.Children.Add(gridBorder);

        // Botones
        var bot = UiH.BtnBar(
            UiH.Btn("✓  Seleccionar", UiH.Verde),
            UiH.Btn("✕  Cancelar",    UiH.Gris)
        );
        ((Button)bot.Children[0]).Click += (_, __) => Aceptar();
        ((Button)bot.Children[1]).Click += (_, __) => { DialogResult = false; };
        UiH.SetRow(bot, 2); root.Children.Add(bot);

        return root;
    }

    private void Filtrar()
    {
        var term = _txtFiltro.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(term)) { _grid.ItemsSource = _todos; return; }
        var prop = _todos.FirstOrDefault()?.GetType().GetProperty(_displayProp);
        if (prop == null) return;
        _grid.ItemsSource = _todos.Where(o => (prop.GetValue(o)?.ToString() ?? "").ToLowerInvariant().Contains(term)).ToList();
    }

    private void Aceptar()
    {
        if (_grid.SelectedItem != null) { Seleccionado = _grid.SelectedItem; DialogResult = true; }
    }
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
    private ComboBox _cboGravada = null!, _cboContado = null!;
    private RadioButton _rdoImper = null!, _rdoPer = null!;
    private DatePicker _dpVto = null!;

    private Seccion? _sec; private Categoria? _cat; private Subcategoria? _sub;
    private Marca? _marca; private Proveedor? _prov; private Medida? _med; private Pais? _pais;

    private TextBox _cSec = null!, _dSec = null!, _cCat = null!, _dCat = null!,
                    _cSub = null!, _dSub = null!, _cMarca = null!, _dMarca = null!,
                    _cProv = null!, _dProv = null!, _cMed = null!, _dMed = null!,
                    _cPais = null!, _dPais = null!;

    public NuevoEditarArticuloDialog(IArticuloRepository repo, ArtCaches caches, Articulo? original)
    {
        _repo = repo; _c = caches; _orig = original;
        Title = original == null ? "Nuevo Artículo / Mercadería" : "Editar Artículo / Mercadería";
        Width = 760; Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        ResizeMode = ResizeMode.NoResize;
        KeyDown += OnKey;
        Content = Build();
        if (original != null) Load(original);
    }

    private UIElement Build()
    {
        var root = new StackPanel { Margin = new Thickness(16) };

        // ── Sección: Identificación ──────────────────────────────────────────
        var secId = UiH.SectionPanel("Identificación", out var panelId);
        var r1 = new WrapPanel();
        r1.Children.Add(UiH.FieldGroup("Código *",        out _txtCodigo,    120));
        r1.Children.Add(UiH.FieldGroup("Serial / Modelo", out _txtSerial,    160));
        r1.Children.Add(UiH.ComboGroup("Gravada",  out _cboGravada, new[] { ("SI","1"),("NO","0") }, 70));
        r1.Children.Add(UiH.FieldGroup("IVA (%)",         out _txtIva,        60, "0"));
        r1.Children.Add(UiH.FieldGroup("Máx. cuotas",     out _txtMaxCuota,   60, "0"));
        r1.Children.Add(UiH.FieldGroup("Stock Mínimo",    out _txtStockMin,   80, "0"));
        r1.Children.Add(UiH.ComboGroup("Solo contado", out _cboContado, new[] { ("NO","0"),("SI","1") }, 80, 0));
        panelId.Children.Add(r1);
        var r1b = new WrapPanel();
        r1b.Children.Add(UiH.FieldGroup("Nombre / Descripción *", out _txtNombre, 460));
        r1b.Children.Add(UiH.FieldGroup("Presentación",            out _txtPres,  200, marginRight: 0));
        panelId.Children.Add(r1b);
        root.Children.Add(secId);

        // ── Sección: Clasificación (3 columnas) ──────────────────────────────
        var secClas = UiH.SectionPanel("Clasificación", out var panelClas);
        var grid3 = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        grid3.Children.Add(UiH.LupaGroupWithCode("Sección",         out _cSec,   out _dSec,   () => Pick(_c.Secs.Cast<object>(),  "NombreSeccion",      v => { _sec   = (Seccion)v;      _cSec.Text   = _sec.IdSeccion.ToString();      _dSec.Text   = _sec.NombreSeccion; }),      50, 130, 8));
        grid3.Children.Add(UiH.LupaGroupWithCode("Proveedor",        out _cProv,  out _dProv,  () => Pick(_c.Provs.Cast<object>(), "NombreProveedor",    v => { _prov  = (Proveedor)v;    _cProv.Text  = _prov.IdProveedor.ToString();   _dProv.Text  = _prov.NombreProveedor; }),   50, 130, 8));
        grid3.Children.Add(UiH.LupaGroupWithCode("Subcategoría",     out _cSub,   out _dSub,   () => Pick(_c.Subs.Cast<object>(),  "NombreSubcategoria", v => { _sub   = (Subcategoria)v; _cSub.Text   = _sub.IdSubcategoria.ToString(); _dSub.Text   = _sub.NombreSubcategoria; }), 50, 130, 0));
        grid3.Children.Add(UiH.LupaGroupWithCode("Categoría",        out _cCat,   out _dCat,   () => Pick(_c.Cats.Cast<object>(),  "NombreCategoria",    v => { _cat   = (Categoria)v;    _cCat.Text   = _cat.IdCategoria.ToString();    _dCat.Text   = _cat.NombreCategoria; }),    50, 130, 8));
        grid3.Children.Add(UiH.LupaGroupWithCode("País",             out _cPais,  out _dPais,  () => Pick(_c.Paises.Cast<object>(),"NombrePais",         v => { _pais  = (Pais)v;         _cPais.Text  = _pais.IdPais.ToString();        _dPais.Text  = _pais.NombrePais; }),        50, 130, 8));
        grid3.Children.Add(UiH.LupaGroupWithCode("Marca",            out _cMarca, out _dMarca, () => Pick(_c.Marcas.Cast<object>(),"NombreMarca",        v => { _marca = (Marca)v;        _cMarca.Text = _marca.IdMarca.ToString();      _dMarca.Text = _marca.NombreMarca; }),      50, 130, 0));
        grid3.Children.Add(UiH.LupaGroupWithCode("Unidad de Medida", out _cMed,   out _dMed,   () => Pick(_c.Meds.Cast<object>(),  "NombreMedida",       v => { _med   = (Medida)v;       _cMed.Text   = _med.IdMedida.ToString();       _dMed.Text   = _med.NombreMedida; }),       50, 130, 8));
        panelClas.Children.Add(grid3);
        root.Children.Add(secClas);

        // ── Sección: Vencimiento ─────────────────────────────────────────────
        var secVto = UiH.SectionPanel("Vencimiento", out var panelVto);
        var vtoRow = new StackPanel { Orientation = Orientation.Horizontal };
        _rdoImper = new RadioButton
        {
            Content = "Imperecedero", IsChecked = true,
            Foreground = new SolidColorBrush(UiH.TextoLabel), FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0)
        };
        _rdoPer = new RadioButton
        {
            Content = "Perecedero (tiene fecha de vencimiento)",
            Foreground = new SolidColorBrush(UiH.TextoLabel), FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0)
        };
        _dpVto = new DatePicker
        {
            IsEnabled = false, Width = 140, Height = 28,
            Background = Brushes.White, VerticalAlignment = VerticalAlignment.Center,
        };
        _dpVto.Loaded += (_, __) => ForzarFondoDatePicker(_dpVto);
        _dpVto.IsEnabledChanged += (_, __) => ForzarFondoDatePicker(_dpVto);
        _rdoPer.Checked   += (_, __) => _dpVto.IsEnabled = true;
        _rdoImper.Checked += (_, __) => { _dpVto.IsEnabled = false; _dpVto.SelectedDate = null; };
        vtoRow.Children.Add(_rdoImper);
        vtoRow.Children.Add(_rdoPer);
        vtoRow.Children.Add(new TextBlock { Text = "Fecha:", Foreground = new SolidColorBrush(UiH.TextoLabel), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        vtoRow.Children.Add(_dpVto);
        panelVto.Children.Add(vtoRow);
        root.Children.Add(secVto);

        // ── Pie: hint + botones ──────────────────────────────────────────────
        var pie = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pie.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hint = UiH.Hint("F5 / Ctrl+S: Guardar   Esc: Cerrar");
        System.Windows.Controls.Grid.SetColumn(hint, 0); pie.Children.Add(hint);
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var bG = UiH.Btn("💾  Guardar", UiH.Verde);  bG.Click += async (_, __) => await SaveAsync();
        var bC = UiH.Btn("✕  Cerrar",   UiH.Gris);   bC.Click += (_, __) => Close();
        btns.Children.Add(bG); btns.Children.Add(bC);
        System.Windows.Controls.Grid.SetColumn(btns, 1); pie.Children.Add(btns);
        root.Children.Add(pie);

        return root;
    }

    private void Pick(IEnumerable<object> items, string prop, Action<object> onSel)
    {
        var dlg = new LupaDialog(prop, items, prop) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Seleccionado != null) onSel(dlg.Seleccionado);
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

    private async Task SaveAsync()
    {
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
        try
        {
            if (_orig == null)
            {
                await _repo.GuardarAsync(art, session.LocalActual?.IdLocal ?? 1, session.UsuarioActual?.IdUsuario ?? 1);
                MessageBox.Show("Artículo guardado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                art.Id = _orig.Id;
                await _repo.ActualizarAsync(art);
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

    public EditarPreciosDialog(IArticuloRepository repo, List<Local> locales)
    {
        _repo = repo; _locales = locales;
        Title = "Modificar Precios";
        Width = 720; Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(UiH.FondoDlg);
        ResizeMode = ResizeMode.NoResize;
        Content = Build();
        // Pre-cargar el local de la sesión activa
        var localSesion = SessionService.Instance.LocalActual;
        if (localSesion != null)
        {
            _idLocal = localSesion.IdLocal;
            _dLocalNombre.Text = localSesion.NombreLocal;
        }
    }

    private UIElement Build()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new StackPanel { Margin = new Thickness(16) };
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

        // Botones
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

        return scroll;
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
        var dlg = new BuscarArticuloDialog(_repo, "Seleccionar artículo") { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Seleccionado != null)
        {
            _idArt = dlg.Seleccionado.Id;
            _dCodArt.Text  = dlg.Seleccionado.Ca;
            _dDescArt.Text = dlg.Seleccionado.D;
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
        // Asignar como entero puro — el NumericInput formatea con separador de miles automáticamente
        _txtPcosto.Text  = ((long)(p?.Pc      ?? 0)).ToString(inv);
        _txtPventa.Text  = ((long)(p?.Pventa  ?? 0)).ToString(inv);
        _txtPpromo.Text  = ((long)(p?.Ppromo  ?? 0)).ToString(inv);
        _txtContado.Text = ((long)(p?.Contado ?? 0)).ToString(inv);
        _txtPctDesc.Text = "0";
        _txtValDesc.Text = "0";
        _lblFcompra.Text = p?.Fcompra.HasValue == true ? p.Fcompra.Value.ToString("dd/MM/yyyy") : "—";
        _lblFventa.Text  = p?.Fventa.HasValue  == true ? p.Fventa.Value.ToString("dd/MM/yyyy")  : "—";
        _lblFmp.Text     = p?.Fmp.HasValue     == true ? p.Fmp.Value.ToString("dd/MM/yyyy")     : "—";
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
            foreach (var idL in destinos)
                await _repo.ActualizarPreciosAsync(_idArt, idL, pc, pv, cto, pp);
            MessageBox.Show("Precios guardados.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
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
        var dlg = new BuscarArticuloDialog(_repo, "Seleccionar artículo") { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Seleccionado != null)
        {
            _idArt = dlg.Seleccionado.Id;
            _dCodArt.Text  = dlg.Seleccionado.Ca;
            _dDescArt.Text = dlg.Seleccionado.D;
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

internal class InhabRow
{
    public string Ca { get; set; } = "";
    public string D { get; set; } = "";
    public string LocalNombre { get; set; } = "";
    public int IdLocal { get; set; }
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

        // Selección
        var secSel = UiH.SectionPanel("Selección de artículo y local", out var panelSel);
        var selRow = new WrapPanel();
        selRow.Children.Add(UiH.LupaGroup("Local de búsqueda", out _dLocalNombre, () => PickLocal(), 180));
        selRow.Children.Add(UiH.LupaGroup("Código artículo",   out _txtCodigo,    async () => await PickArtAsync(), 120));
        selRow.Children.Add(UiH.LupaGroup("Descripción",       out _dDescArt,     async () => await PickArtAsync(), 260, 0));
        _dDescArt.IsReadOnly = true;
        panelSel.Children.Add(selRow);
        var btnFila = new StackPanel { Orientation = Orientation.Horizontal };
        var btnIngresar = UiH.Btn("Buscar", UiH.Azul, 80);
        btnIngresar.Click += async (_, __) => await CargarGridAsync();
        var btnLimpiarLocal = UiH.Btn("✕ Quitar filtro local", UiH.Gris, 140);
        btnLimpiarLocal.Click += async (_, __) => { _idLocal = 0; _dLocalNombre.Text = ""; FiltrarGrid(); };
        btnFila.Children.Add(btnIngresar);
        btnFila.Children.Add(btnLimpiarLocal);
        panelSel.Children.Add(btnFila);
        UiH.SetRow(secSel, 0); root.Children.Add(secSel);

        // Hint
        var hint = new TextBlock
        {
            Text = "Seleccione artículo y luego marque los locales donde desea " + (_inhab ? "inhabilitar." : "habilitar."),
            Foreground = new SolidColorBrush(UiH.TextoSub), FontSize = 10,
            Margin = new Thickness(0, 0, 0, 8)
        };
        UiH.SetRow(hint, 1); root.Children.Add(hint);

        // Grid con selección resaltada
        _grid = UiH.ModernGrid();
        _grid.SelectionMode = DataGridSelectionMode.Extended;
        _grid.Background = Brushes.White;
        _grid.RowBackground = Brushes.White;
        _grid.AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 249, 250));
        _grid.Columns.Add(UiH.Col("Código",      "Ca",          160));
        _grid.Columns.Add(UiH.Col("Descripción", "D",           0, star: true));
        _grid.Columns.Add(UiH.Col("Local",        "LocalNombre", 160));

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

        // Checkboxes locales
        var chkBorder = new Border
        {
            Background = new SolidColorBrush(UiH.FondoPanel),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10)
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
        var hintPie = UiH.Hint("Marque los locales y pulse Guardar.");
        System.Windows.Controls.Grid.SetColumn(hintPie, 0); pie.Children.Add(hintPie);
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var color = _inhab ? UiH.Rojo : UiH.Verde;
        var label = _inhab ? "🚫  Inhabilitar" : "✓  Habilitar";
        var bG = UiH.Btn(label, color); bG.Click += async (_, __) => await SaveAsync();
        var bC = UiH.Btn("✕  Cerrar", UiH.Gris); bC.Click += (_, __) => Close();
        btns.Children.Add(bG); btns.Children.Add(bC);
        System.Windows.Controls.Grid.SetColumn(btns, 1); pie.Children.Add(btns);
        UiH.SetRow(pie, 4); root.Children.Add(pie);

        return root;
    }

    private void PickLocal()
    {
        var dlg = new LupaDialog("Seleccionar local", _locales.Cast<object>(), "NombreLocal") { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Seleccionado is Local l)
        {
            _idLocal = l.IdLocal;
            _dLocalNombre.Text = l.NombreLocal;
            FiltrarGrid();
        }
    }

    private async Task PickArtAsync()
    {
        var dlg = new BuscarArticuloDialog(_repo, "Seleccionar artículo") { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Seleccionado != null)
        {
            _txtCodigo.Text = dlg.Seleccionado.Ca;
            _dDescArt.Text  = dlg.Seleccionado.D;
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
            .Select(p => (object)new InhabRow { Ca = art.Ca, D = art.D, LocalNombre = p.LocalNombre, IdLocal = p.IdLocal })
            .ToList();
        FiltrarGrid();
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
        var selLocales = _chkLocales.Where(c => c.IsChecked == true).Select(c => (int)c.Tag!).ToList();
        if (!selLocales.Any()) { MessageBox.Show("Marque al menos un local.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var codigo = _txtCodigo.Text.Trim();
        if (string.IsNullOrWhiteSpace(codigo)) { MessageBox.Show("Ingrese el código del artículo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var art = await _repo.BuscarPorCodigoAsync(codigo);
        if (art == null) { MessageBox.Show("Artículo no encontrado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        // Paso 1: pedir contraseña
        var dlgPass = new ConfirmarPasswordDialog { Owner = this };
        if (dlgPass.ShowDialog() != true) return;

        // Paso 2: confirmación con detalle de lo que se va a hacer
        var nombresLocales = _chkLocales
            .Where(c => c.IsChecked == true)
            .Select(c => c.Content?.ToString() ?? "")
            .ToList();
        var accionVerbo = _inhab ? "inhabilitar" : "habilitar";
        var dlgConf = new ConfirmarAccionDialog(accionVerbo, art.Ca, art.D, nombresLocales) { Owner = this };
        if (dlgConf.ShowDialog() != true) return;

        try
        {
            foreach (var idL in selLocales)
                await _repo.InhabilitarEnLocalAsync(art.Id, idL, _inhab);
            var accion = _inhab ? "inhabilitado" : "habilitado";
            MessageBox.Show($"Artículo {accion} en los locales seleccionados.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
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
