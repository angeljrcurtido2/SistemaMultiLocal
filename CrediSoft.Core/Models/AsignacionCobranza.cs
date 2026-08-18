namespace CrediSoft.Core.Models;

public class AsignacionCobranza
{
    public int IdAsignacion { get; set; }
    public int? IdCab { get; set; }
    public int? IdGeneradas { get; set; }
    public int? IdCliente { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteCi { get; set; } = string.Empty;
    public string LocalNombre { get; set; } = string.Empty;
    public string NSolicitud { get; set; } = string.Empty;
    public byte? NCuota { get; set; }
    public DateTime? Vto { get; set; }
    public byte? EstadoCuota { get; set; }
    public decimal? MontoCuota { get; set; }
    public int IdCobrador { get; set; }
    public string CobradorNombre { get; set; } = string.Empty;
    public int IdUsuarioRegistro { get; set; }
    public string UsuarioRegistroNombre { get; set; } = string.Empty;
    public DateTime FechaAsignacion { get; set; }
    public bool Activo { get; set; }
    public string Nivel => IdGeneradas.HasValue ? "Cuota" : "Crédito";
    public string Referencia => IdGeneradas.HasValue
        ? (NCuota.HasValue ? $"Cuota {NCuota}" : $"Cuota #{IdGeneradas}")
        : IdCab.HasValue ? $"Crédito #{IdCab}" : string.Empty;
}
