using CrediSoft.Data.Repositories;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Shared;

// VM ligero para el DataTemplate
sealed class FuncionarioVM
{
    public FuncionarioInfo Info { get; }
    public string Nombre     => Info.Nombre;
    public string Ci         => Info.Ci;
    public string Cargo      => Info.NombreLocal; // se usa en el badge de local
    public string NombreLocal => Info.NombreLocal;
    public string Inicial    => Info.Nombre.Length > 0 ? Info.Nombre[0].ToString().ToUpper() : "?";
    public string SalarioFmt => Info.Salario > 0 ? $"Gs. {Info.Salario:N0}" : "—";

    public FuncionarioVM(FuncionarioInfo info) => Info = info;

    public bool Coincide(string filtro)
    {
        if (string.IsNullOrEmpty(filtro)) return true;
        var f = filtro.ToLower();
        return Info.Nombre.ToLower().Contains(f)
            || Info.Ci.ToLower().Contains(f)
            || Info.NombreLocal.ToLower().Contains(f);
    }
}

public partial class SeleccionarFuncionarioModal : Window
{
    public FuncionarioInfo? FuncionarioSeleccionado { get; private set; }

    private readonly List<FuncionarioVM> _todos = new();

    public SeleccionarFuncionarioModal(IEnumerable<FuncionarioInfo> funcionarios,
                                       FuncionarioInfo? preseleccionado = null)
    {
        InitializeComponent();

        _todos = funcionarios.Select(f => new FuncionarioVM(f)).ToList();
        AplicarFiltro("");

        // Pre-seleccionar si se pasó uno
        if (preseleccionado != null)
        {
            var vm = _todos.FirstOrDefault(f => f.Info.IdUsuario == preseleccionado.IdUsuario);
            if (vm != null)
            {
                ListFuncionarios.SelectedItem = vm;
                ListFuncionarios.ScrollIntoView(vm);
            }
        }

        Loaded += (_, _) => TxtBuscar.Focus();
    }

    private void AplicarFiltro(string texto)
    {
        var filtrados = _todos.Where(f => f.Coincide(texto)).ToList();
        ListFuncionarios.ItemsSource = filtrados;
        TxtContador.Text = $"{filtrados.Count} de {_todos.Count}";
        BtnSeleccionar.IsEnabled = ListFuncionarios.SelectedItem != null
                                   && filtrados.Contains(ListFuncionarios.SelectedItem as FuncionarioVM ?? new FuncionarioVM(new FuncionarioInfo(0,"","",0,"",0,0,0)));
        ActualizarInfoSeleccion();
    }

    private void OnBuscarChanged(object s, TextChangedEventArgs e)
        => AplicarFiltro(TxtBuscar.Text.Trim());

    private void OnSelectionChanged(object s, SelectionChangedEventArgs e)
    {
        BtnSeleccionar.IsEnabled = ListFuncionarios.SelectedItem != null;
        ActualizarInfoSeleccion();
    }

    private void ActualizarInfoSeleccion()
    {
        if (ListFuncionarios.SelectedItem is FuncionarioVM vm)
            TxtInfoSeleccion.Text = $"Seleccionado: {vm.Nombre}  ·  {vm.NombreLocal}  ·  Gs. {vm.Info.Salario:N0}";
        else
            TxtInfoSeleccion.Text = "";
    }

    private void OnDobleClick(object s, MouseButtonEventArgs e)
    {
        if (ListFuncionarios.SelectedItem != null) Confirmar();
    }

    private void OnKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (ListFuncionarios.SelectedItem != null) Confirmar();
            else if (ListFuncionarios.Items.Count == 1)
            {
                ListFuncionarios.SelectedIndex = 0;
                Confirmar();
            }
        }
        if (e.Key == Key.Escape) Close();

        // Flechas desde el TextBox mueven la lista
        if (s == TxtBuscar && (e.Key == Key.Down || e.Key == Key.Up))
        {
            var idx = ListFuncionarios.SelectedIndex;
            if (e.Key == Key.Down) idx = Math.Min(idx + 1, ListFuncionarios.Items.Count - 1);
            else                   idx = Math.Max(idx - 1, 0);
            ListFuncionarios.SelectedIndex = idx;
            ListFuncionarios.ScrollIntoView(ListFuncionarios.SelectedItem);
            e.Handled = true;
        }
    }

    private void OnSeleccionar(object s, RoutedEventArgs e) => Confirmar();
    private void OnCancelar(object s, RoutedEventArgs e)    => Close();

    private void Confirmar()
    {
        if (ListFuncionarios.SelectedItem is FuncionarioVM vm)
        {
            FuncionarioSeleccionado = vm.Info;
            DialogResult = true;
            Close();
        }
    }
}
