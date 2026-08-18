using CrediSoft.Core.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Retiros;

public class LocalSelectorDialog : Window
{
    public Local? SelectedLocal { get; private set; }

    private readonly DataGrid _grid;
    private List<Local> _todos;
    private List<Local> _filtrados;
    private TextBox     _txtBuscar = null!;

    public LocalSelectorDialog(List<Local> locales)
    {
        _todos     = locales;
        _filtrados = locales;

        Title  = "Seleccionar local";
        Width  = 500; Height = 380;
        ResizeMode           = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize   = 13;

        var dp = new DockPanel();

        // Cabecera
        var header = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1565C0")),
            Padding = new Thickness(12, 8, 12, 8)
        };
        var ht = new TextBlock
        {
            Text = "SELECCIONAR LOCAL",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 14
        };
        header.Child = ht;
        DockPanel.SetDock(header, Dock.Top);
        dp.Children.Add(header);

        // Buscador
        _txtBuscar = new TextBox { Padding = new Thickness(6, 5, 6, 5), Margin = new Thickness(8) };
        _txtBuscar.TextChanged += OnBuscar;
        DockPanel.SetDock(_txtBuscar, Dock.Top);
        dp.Children.Add(_txtBuscar);

        // Pie
        var pie = new Border
        {
            BorderBrush = System.Windows.Media.Brushes.LightGray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8)
        };
        var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnSel = new Button
        {
            Content = "Seleccionar", Padding = new Thickness(14, 6, 14, 6),
            Cursor = Cursors.Hand,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1565C0")),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 8, 0)
        };
        btnSel.Click += (_, _) => Confirmar();
        var btnCan = new Button
        {
            Content = "Cancelar", Padding = new Thickness(12, 6, 12, 6), Cursor = Cursors.Hand
        };
        btnCan.Click += (_, _) => Close();
        sp.Children.Add(btnSel);
        sp.Children.Add(btnCan);
        pie.Child = sp;
        DockPanel.SetDock(pie, Dock.Bottom);
        dp.Children.Add(pie);

        // Grid
        _grid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            CanUserResizeRows = false,
            CanUserAddRows = false,
            Margin = new Thickness(8, 0, 8, 0)
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "ID",     Binding = new System.Windows.Data.Binding("IdLocal"),   Width = 50 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Nombre", Binding = new System.Windows.Data.Binding("NombreLocal"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.MouseDoubleClick += (_, _) => Confirmar();
        _grid.PreviewKeyDown   += (_, e) => { if (e.Key == Key.Enter) { Confirmar(); e.Handled = true; } };
        _grid.ItemsSource = _filtrados;
        dp.Children.Add(_grid);

        Content = dp;
        Loaded += (_, _) =>
        {
            _txtBuscar.Focus();
            if (_grid.Items.Count > 0) _grid.SelectedIndex = 0;
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void OnBuscar(object s, TextChangedEventArgs e)
    {
        var t = _txtBuscar.Text.Trim().ToUpperInvariant();
        _filtrados = string.IsNullOrEmpty(t)
            ? _todos
            : _todos.Where(l =>
                l.NombreLocal.ToUpperInvariant().Contains(t) ||
                l.IdLocal.ToString().Contains(t)).ToList();
        _grid.ItemsSource = _filtrados;
        if (_grid.Items.Count > 0) _grid.SelectedIndex = 0;
    }

    private void Confirmar()
    {
        if (_grid.SelectedItem is Local sel)
        {
            SelectedLocal = sel;
            DialogResult  = true;
            Close();
        }
    }
}
