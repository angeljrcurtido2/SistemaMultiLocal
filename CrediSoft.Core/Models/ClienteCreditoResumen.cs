namespace CrediSoft.Core.Models;

public class ClienteCreditoResumen
{
    public int IdCliente { get; set; }
    public string CiCliente { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;
    public string TelefonoCliente { get; set; } = string.Empty;
    public string LocalNombre { get; set; } = string.Empty;
    public int CreditosTotales { get; set; }
    public int CreditosActivos { get; set; }
    public int CuotasPendientes { get; set; }
    public int CuotasAtrasadas { get; set; }
    public int DiasAtrasoMax { get; set; }
    public decimal MontoAtraso { get; set; }
    public DateTime? ProximoVencimiento { get; set; }

    public bool TieneAtraso => CuotasAtrasadas > 0;
    public string EstadoTexto => TieneAtraso ? "Con atraso" : "Al día";
    public string ProximoVencimientoTexto => ProximoVencimiento.HasValue
        ? ProximoVencimiento.Value.ToString("dd/MM/yyyy")
        : "—";
}
