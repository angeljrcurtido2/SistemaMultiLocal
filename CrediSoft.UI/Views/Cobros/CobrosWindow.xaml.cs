using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Cobros;

public partial class CobrosWindow : Window
{
    private readonly IClienteRepository _clientes;
    private readonly ICuotaRepository _cuotas;
    private readonly ISessionService _session;
    private Cliente? _clienteActual;
    private Cuota? _cuotaSeleccionada;

    public CobrosWindow()
    {
        InitializeComponent();
        _clientes = App.Services.GetRequiredService<IClienteRepository>();
        _cuotas   = App.Services.GetRequiredService<ICuotaRepository>();
        _session  = SessionService.Instance;
    }

    private async void OnBuscarCliente(object s, RoutedEventArgs e) => await BuscarCliente();
    private async void OnCiKeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) await BuscarCliente(); }

    private async Task BuscarCliente()
    {
        var ci = TxtCi.Text.Trim();
        if (string.IsNullOrEmpty(ci)) return;

        _clienteActual = await _clientes.BuscarPorCiAsync(ci);
        if (_clienteActual == null)
        { MessageBox.Show("Cliente no encontrado.", "Aviso"); return; }

        TxtClienteNombre.Text = _clienteActual.NombreCliente;
        TxtTelefono.Text = _clienteActual.TelefonoCliente;
        TxtCiudad.Text = _clienteActual.CiudadCliente;
        TxtEstado.Text = _clienteActual.EstadoTexto;
        TxtCredMax.Text = _clienteActual.CredMax.ToString("N0") + " Gs.";

        var cuotas = await _cuotas.BuscarPendientesPorClienteAsync(_clienteActual.IdCliente);
        GridCuotas.ItemsSource = cuotas;
        LimpiarPanelCobro();
    }

    private void OnCuotaSeleccionada(object s, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GridCuotas.SelectedItem is not Cuota c) return;
        _cuotaSeleccionada = c;

        TxtMontoCuota.Text = c.Monto.ToString("N0");
        TxtPunitorio.Text  = c.DiasDeAtraso > 0 ? c.Punitorio.ToString("N0") : "0";
        TxtTotalCobrar.Text = (c.Monto + c.Punitorio).ToString("N0");
        BtnCobrar.IsEnabled = true;
    }

    private async void OnCobrar(object s, RoutedEventArgs e)
    {
        if (_cuotaSeleccionada == null || _clienteActual == null) return;

        var c = _cuotaSeleccionada;
        var total = c.Monto + c.Punitorio;
        var confirm = MessageBox.Show(
            $"¿Cobrar cuota N°{c.NCuota} por Gs. {total:N0}?",
            "Confirmar cobro", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            BtnCobrar.IsEnabled = false;

            // Verificar caja abierta
            var caja = await App.Services.GetRequiredService<ICajaRepository>()
                .ObtenerCajaAbiertaAsync(_session.LocalActual!.IdLocal);
            if (caja == null)
            {
                MessageBox.Show("No hay caja abierta en este local. Abra la caja antes de cobrar.",
                    "Caja cerrada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var prm = new CobrarCuotaParams(
                IdCab: c.IdCab,
                IdGeneradas: c.IdGeneradas,
                NCuota: c.NCuota,
                Comprobante: c.Comprobante,
                MontoCuota: c.Monto,
                Mora: c.Mora,
                Punitorio: c.Punitorio,
                Total: total,
                IdUsuario: _session.UsuarioActual!.IdUsuario,
                IdLocal: (byte)_session.LocalActual.IdLocal,
                IdCajaFisica: caja.IdCajaFisica,
                EntregaAnterior: c.Entrega,
                Inforcom: _clienteActual.Inforcom,
                ElEstadoCab: 1);

            var ok = await _cuotas.CobrarCuotaAsync(prm);

            if (ok)
            {
                MessageBox.Show($"Cuota cobrada correctamente.\nTotal cobrado: Gs. {total:N0}",
                    "Cobro exitoso", MessageBoxButton.OK, MessageBoxImage.Information);
                await BuscarCliente();
            }
            else
            {
                MessageBox.Show("Error al procesar el cobro.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnCobrar.IsEnabled = _cuotaSeleccionada != null;
        }
    }

    private void LimpiarPanelCobro()
    {
        TxtMontoCuota.Text = TxtPunitorio.Text = TxtTotalCobrar.Text = "";
        BtnCobrar.IsEnabled = false;
        _cuotaSeleccionada = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if ((e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) || e.Key == Key.Escape)
            Close();
        else if (e.Key == Key.F2)
            TxtCi.Focus();
    }

    private void OnCerrar(object s, RoutedEventArgs e) => Close();
}
