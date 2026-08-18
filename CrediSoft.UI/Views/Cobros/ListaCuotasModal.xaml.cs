using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Cobros;

public partial class ListaCuotasModal : Window
{
    private readonly ICuotaRepository _cuotas;
    private List<Cuota> _todas = new();
    private CancellationTokenSource? _debounce;
    private Cuota? _cuotaEnFoco;

    public Cuota? CuotaSeleccionada { get; private set; }

    public ListaCuotasModal(IEnumerable<Cuota> cuotas)
    {
        InitializeComponent();
        _cuotas = App.Services.GetRequiredService<ICuotaRepository>();
        _todas  = cuotas.ToList();
        Loaded += (_, _) => { CargarLocales(); AplicarFiltro(); TxtBuscar.Focus(); };
    }

    // Pedido explícito: agregar filtro por local — antes las 738+ cuotas pendientes de
    // TODOS los locales se mostraban juntas sin forma de acotar a una sucursal puntual.
    // Arranca preseleccionado en el local del usuario logueado (lo más probable que quiera
    // ver primero); si quiere otro o "Todos", lo cambia libremente desde el combo.
    private void CargarLocales()
    {
        CboLocal.Items.Add(new ComboBoxItem { Content = "Todos los locales", Tag = null });
        foreach (var nombre in _todas.Select(c => c.LocalNombre).Distinct().OrderBy(n => n))
            CboLocal.Items.Add(new ComboBoxItem { Content = nombre, Tag = nombre });

        var localUsuario = SessionService.Instance.LocalActual?.NombreLocal?.Trim();
        var idx = -1;
        if (!string.IsNullOrEmpty(localUsuario))
        {
            for (int i = 1; i < CboLocal.Items.Count; i++)
            {
                var contenido = ((ComboBoxItem)CboLocal.Items[i]).Content?.ToString()?.Trim() ?? "";
                if (string.Equals(contenido, localUsuario, StringComparison.OrdinalIgnoreCase))
                { idx = i; break; }
            }
        }
        CboLocal.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void OnLocalCambiado(object s, SelectionChangedEventArgs e) => AplicarFiltro();

    private void AplicarFiltro()
    {
        var hoy = DateTime.Today;
        DateTime? hasta = null;

        if      (RbDiario.IsChecked  == true) hasta = hoy;
        else if (RbSemanal.IsChecked == true) hasta = hoy.AddDays(7);
        else if (RbMensual.IsChecked == true) hasta = hoy.AddMonths(1);

        var texto = TxtBuscar?.Text.Trim() ?? "";
        var localSel = (CboLocal?.SelectedItem as ComboBoxItem)?.Tag as string;

        var todasCopy = _todas;
        Task.Run(() =>
        {
            IEnumerable<Cuota> filtradas = hasta.HasValue
                ? todasCopy.Where(c => c.Vto.Date <= hasta.Value)
                : todasCopy;

            if (!string.IsNullOrEmpty(localSel))
                filtradas = filtradas.Where(c => c.LocalNombre == localSel);

            if (!string.IsNullOrEmpty(texto))
            {
                filtradas = filtradas.Where(c =>
                    c.ClienteNombre.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                    c.ClienteCi.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                    c.Comprobante.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                    c.LocalNombre.Contains(texto, StringComparison.OrdinalIgnoreCase));
            }

            var lista = filtradas.ToList();

            Dispatcher.Invoke(() =>
            {
                GridCuotas.ItemsSource = lista;
                TxtInfo.Text = $"{lista.Count} cuota(s) pendiente(s)";
            });
        });
    }

    private void OnFiltroVistaCambiado(object s, RoutedEventArgs e)
    {
        if (GridCuotas == null) return;
        AplicarFiltro();
    }

    private void OnBuscarChanged(object s, TextChangedEventArgs e)
    {
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;
        Task.Delay(200, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
                Dispatcher.Invoke(AplicarFiltro);
        }, token);
    }

    private void OnLimpiarBuscar(object s, RoutedEventArgs e)
    {
        TxtBuscar.Text = "";
        TxtBuscar.Focus();
    }

    private void OnCuotaSeleccionada(object s, SelectionChangedEventArgs e)
    {
        _cuotaEnFoco = GridCuotas.SelectedItem as Cuota;
        var habilitado = _cuotaEnFoco != null;
        BtnHistorial.IsEnabled = habilitado;
        BtnArticulos.IsEnabled = habilitado;
    }

    private void OnCuotaDobleClick(object s, MouseButtonEventArgs e)
    {
        if (GridCuotas.SelectedItem is not Cuota c) return;
        CuotaSeleccionada = c;
        DialogResult = true;
        Close();
    }

    private async void OnHistorial(object s, RoutedEventArgs e)
    {
        if (_cuotaEnFoco == null) return;

        var cuotas = (await _cuotas.ObtenerHistorialAsync(_cuotaEnFoco.IdCab))
            .Select(c => new CuotaHistorialDetallada(c.NCuota, c.Monto, c.Vto, c.Estado, c.Mora, c.Obs, c.FechaPago, c.DiasVtoAPago));
        new HistorialCobrosModal(_cuotaEnFoco.ClienteNombre, _cuotaEnFoco.IdCab, cuotas) { Owner = this }
            .ShowDialog();
    }

    private async void OnArticulos(object s, RoutedEventArgs e)
    {
        if (_cuotaEnFoco == null) return;
        var arts  = await _cuotas.ObtenerArticulosAsync(_cuotaEnFoco.IdCab);
        var items = arts.Select(a => new ArticuloVenta(a.Descripcion, a.Cantidad, a.PVenta));
        var modal = new ArticulosModal(_cuotaEnFoco.IdCab, items) { Owner = this };
        modal.ShowDialog();
    }

    private void OnCerrar(object s, RoutedEventArgs e) => Close();
}
