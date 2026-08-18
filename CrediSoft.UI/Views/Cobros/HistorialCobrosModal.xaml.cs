using System.Windows;

namespace CrediSoft.UI.Views.Cobros;

// Historial por CUOTA (una fila por cuota). Fecha de pago y Días vto.→pago vienen de
// GENERADAS.FECHACOBRADO — ver comentario en ICuotaRepository.ObtenerHistorialAsync.
public record CuotaHistorialDetallada(
    byte     NCuota,
    decimal  Monto,
    string   Vto,
    string   Estado,
    int      Mora,
    string   Obs,
    string?  FechaPago,
    int?     DiasVtoAPago)
{
    public bool EstadoVencida =>
        Estado == "Pendiente" &&
        DateTime.TryParse(Vto, out var d) &&
        d.Date < DateTime.Today;

    // NCUOTA=1 en GENERADAS es siempre la ENTREGA inicial de la venta, no una cuota real
    // del plan de pago — ver comentario en Cuota.NCuotaVisible. Acá se conserva la fila
    // (es historial real: el cliente sí pagó ese monto) pero se etiqueta aparte y las
    // cuotas reales se renumeran 1..N para coincidir con lo pactado en la Proforma.
    public bool EsEntrega => NCuota == 1;
    public string NCuotaTexto => EsEntrega ? "Entrega" : (NCuota - 1).ToString();
}

public partial class HistorialCobrosModal : Window
{
    public HistorialCobrosModal(string clienteNombre, int idCab, IEnumerable<CuotaHistorialDetallada> cuotas)
    {
        InitializeComponent();

        var lista = cuotas.ToList();

        TxtTitulo.Text    = "HISTORIAL DE COBROS";
        TxtSubtitulo.Text = $"{clienteNombre}  —  Crédito #{idCab}";

        GridHistorial.ItemsSource = lista;

        var cuotasReales = lista.Where(c => !c.EsEntrega).ToList();
        TxtTotCuotas.Text     = cuotasReales.Count.ToString();
        TxtTotCobradas.Text   = cuotasReales.Count(c => c.Estado == "Cancelado").ToString();
        TxtTotPendientes.Text = cuotasReales.Count(c => c.Estado == "Pendiente").ToString();
        var maxMora = cuotasReales.Where(c => c.Estado == "Pendiente").Select(c => c.Mora).DefaultIfEmpty(0).Max();
        TxtTotMora.Text = maxMora > 0 ? maxMora.ToString() : "0";
    }

    private void OnCerrar(object s, RoutedEventArgs e) => Close();
}
