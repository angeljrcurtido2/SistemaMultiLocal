using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Shared;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Pagos;

public partial class EliminarPagoWindow : Window
{
    private readonly IPagoRepository    _pagos;
    private FuncionarioInfo?            _funcionario;
    private HistorialPagoItem?          _registro;
    private List<FuncionarioInfo>       _todosFuncionarios = new();

    private static readonly NumberFormatInfo _fmtGs = new()
    {
        NumberGroupSeparator   = ".",
        NumberDecimalSeparator = ",",
        NumberDecimalDigits    = 0
    };

    public EliminarPagoWindow()
    {
        InitializeComponent();
        _pagos = App.Services.GetRequiredService<IPagoRepository>();

        var hoy = DateTime.Today;
        DpDesde.SelectedDate = new DateTime(hoy.Year, hoy.Month, 1);
        DpHasta.SelectedDate = hoy;

        LimpiarDetalle();
        Loaded += async (_, _) =>
            _todosFuncionarios = (await _pagos.ListarFuncionariosAsync()).ToList();
    }

    // ── BÚSQUEDA ──────────────────────────────────
    private void OnCiKeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) OnBuscar(s, new RoutedEventArgs()); }

    private async void OnBuscar(object s, RoutedEventArgs e)
    {
        var ci = TxtCi.Text.Trim();
        if (string.IsNullOrEmpty(ci)) return;

        _funcionario = await _pagos.BuscarFuncionarioPorCiAsync(ci);
        if (_funcionario == null)
        {
            MessageBox.Show("Funcionario no encontrado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await CargarHistorial();
    }

    private async void OnListar(object s, RoutedEventArgs e)
    {
        if (_todosFuncionarios.Count == 0)
            _todosFuncionarios = (await _pagos.ListarFuncionariosAsync()).ToList();

        var modal = new SeleccionarFuncionarioModal(_todosFuncionarios, _funcionario) { Owner = this };
        if (modal.ShowDialog() != true || modal.FuncionarioSeleccionado == null) return;

        _funcionario = modal.FuncionarioSeleccionado;
        TxtCi.Text   = _funcionario.Ci;
        await CargarHistorial();
    }

    private async void OnFiltroChanged(object s, SelectionChangedEventArgs e)
    {
        if (_funcionario != null) await CargarHistorial();
    }

    private async Task CargarHistorial()
    {
        if (_funcionario == null || DpDesde.SelectedDate == null || DpHasta.SelectedDate == null) return;

        var items = (await _pagos.ObtenerHistorialPagosAsync(
            _funcionario.IdUsuario,
            DpDesde.SelectedDate.Value,
            DpHasta.SelectedDate.Value)).ToList();

        GridHistorial.ItemsSource = items;
        TxtContadorHistorial.Text = $"{items.Count} registro(s)";
        LimpiarDetalle();
    }

    // ── SELECCIÓN EN GRILLA ────────────────────────
    private void OnHistorialSeleccionado(object s, SelectionChangedEventArgs e)
    {
        if (GridHistorial.SelectedItem is not HistorialPagoItem item) return;
        MostrarDetalle(item);
    }

    private void MostrarDetalle(HistorialPagoItem item)
    {
        _registro = item;

        TxtSalario.Text         = item.Salario.ToString("N", _fmtGs);
        TxtPorcVenta.Text       = item.PorcVenta.ToString("0.##");
        TxtTotalVenta.Text      = item.Venta.ToString("N", _fmtGs);
        TxtPorcCobranza.Text    = item.PorcCobranza.ToString("0.##");
        TxtTotalCobranza.Text   = item.Cobranza.ToString("N", _fmtGs);
        TxtPlus.Text            = item.Plus.ToString("N", _fmtGs);
        TxtHorasExtras.Text     = item.HorasExtras.ToString("N", _fmtGs);
        TxtBonificacion.Text    = item.Bonificacion.ToString("N", _fmtGs);
        TxtOtrasComisiones.Text = item.OtrasComisiones.ToString("N", _fmtGs);
        TxtTotalIngresos.Text   = item.TotalIngresos.ToString("N", _fmtGs);

        TxtAusencias.Text       = item.Ausencias.ToString("N", _fmtGs);
        TxtAdelantos.Text       = item.Adelantos.ToString("N", _fmtGs);
        TxtIps.Text             = item.Ips.ToString("N", _fmtGs);
        TxtCuotas.Text          = item.Cuotas.ToString("N", _fmtGs);
        TxtMultas.Text          = item.Multas.ToString("N", _fmtGs);
        TxtOtros.Text           = item.Otros.ToString("N", _fmtGs);
        TxtEquis.Text           = item.Equis.ToString("N", _fmtGs);
        TxtTotalEgresos.Text    = item.TotalEgresos.ToString("N", _fmtGs);

        TxtNeto.Text            = item.Neto.ToString("N", _fmtGs);
        TxtNotaAsignacion.Text  = item.NotaAsignacion;
        TxtNotaEgreso.Text      = item.NotaEgreso;

        BtnEliminar.IsEnabled   = true;
    }

    // ── ELIMINAR ──────────────────────────────────
    private async void OnEliminar(object s, RoutedEventArgs e)
    {
        if (_registro == null || _funcionario == null) return;

        var auth = new AutorizacionAdminModal(
            $"Eliminar pago #{_registro.IdHpf} — {_funcionario.Nombre}  ·  {_registro.FechaFmt}") { Owner = this };
        if (auth.ShowDialog() != true) return;

        var confirmar = MessageBox.Show(
            $"¿Confirma la eliminación permanente del pago #{_registro.IdHpf} de {_funcionario.Nombre} ({_registro.FechaFmt})?\n\nEsta acción no se puede deshacer.",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmar != MessageBoxResult.Yes) return;

        var ok = await _pagos.EliminarPagoDefinitivoAsync(_registro.IdHpf);
        if (ok)
        {
            MessageBox.Show($"Pago #{_registro.IdHpf} eliminado correctamente.",
                "Eliminado", MessageBoxButton.OK, MessageBoxImage.Information);
            await CargarHistorial();
        }
        else
        {
            MessageBox.Show("Error al eliminar el registro.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── HELPERS ───────────────────────────────────
    private void LimpiarDetalle()
    {
        _registro = null;
        foreach (var tb in new[] { TxtSalario, TxtPorcVenta, TxtTotalVenta, TxtPorcCobranza,
                                   TxtTotalCobranza, TxtPlus, TxtHorasExtras, TxtBonificacion,
                                   TxtOtrasComisiones, TxtTotalIngresos, TxtAusencias, TxtAdelantos,
                                   TxtIps, TxtCuotas, TxtMultas, TxtOtros, TxtEquis,
                                   TxtTotalEgresos, TxtNeto })
            tb.Text = "—";
        TxtNotaAsignacion.Text = "";
        TxtNotaEgreso.Text     = "";
        BtnEliminar.IsEnabled  = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Delete && BtnEliminar.IsEnabled) OnEliminar(this, new RoutedEventArgs());
        if (e.Key == Key.Escape) Close();
    }

    private void OnCerrar(object s, RoutedEventArgs e) => Close();
}
