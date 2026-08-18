using CrediSoft.Core.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Shared;

public partial class SeleccionarLocalModal : Window
{
    public Local? LocalSeleccionado { get; private set; }

    private List<Local> _todos = new();
    private Local?      _preseleccionado;

    public SeleccionarLocalModal(IEnumerable<Local> locales, Local? preseleccionado = null)
    {
        InitializeComponent();
        _todos           = locales.ToList();
        _preseleccionado = preseleccionado;
        AplicarFiltro("");
        Loaded += (_, _) => TxtBuscar.Focus();
    }

    private void AplicarFiltro(string texto)
    {
        var filtro = texto.Trim().ToLower();
        var lista = string.IsNullOrEmpty(filtro)
            ? _todos
            : _todos.Where(l =>
                l.NombreLocal.ToLower().Contains(filtro) ||
                l.IdLocal.ToString().Contains(filtro)).ToList();

        ListLocales.ItemsSource = lista;

        // restaurar selección previa si aplica
        var sel = lista.FirstOrDefault(l => l.IdLocal == (_preseleccionado?.IdLocal ?? -1));
        if (sel != null)
        {
            ListLocales.SelectedItem = sel;
            ListLocales.ScrollIntoView(sel);
        }

        BtnSeleccionar.IsEnabled = ListLocales.SelectedItem != null;
    }

    private void OnBuscarChanged(object s, System.Windows.Controls.TextChangedEventArgs e)
        => AplicarFiltro(TxtBuscar.Text);

    private void OnSelectionChanged(object s, SelectionChangedEventArgs e)
        => BtnSeleccionar.IsEnabled = ListLocales.SelectedItem != null;

    private void OnSeleccionar(object s, RoutedEventArgs e) => Confirmar();

    private void OnDobleClick(object s, MouseButtonEventArgs e)
    { if (ListLocales.SelectedItem != null) Confirmar(); }

    private void OnKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (ListLocales.SelectedItem != null) { Confirmar(); return; }
            // desde el buscador, Enter mueve foco a la lista
            if (ListLocales.Items.Count > 0)
            {
                ListLocales.SelectedIndex = 0;
                ListLocales.Focus();
                (ListLocales.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            }
        }
        if (e.Key == Key.Escape) Close();
        // flechas desde el buscador mueven a la lista
        if (e.Key == Key.Down && s is TextBox && ListLocales.Items.Count > 0)
        {
            ListLocales.SelectedIndex = 0;
            ListLocales.Focus();
            (ListLocales.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
        }
    }

    private void Confirmar()
    {
        LocalSeleccionado = ListLocales.SelectedItem as Local;
        DialogResult = true;
        Close();
    }

    private void OnCancelar(object s, RoutedEventArgs e) => Close();
}
