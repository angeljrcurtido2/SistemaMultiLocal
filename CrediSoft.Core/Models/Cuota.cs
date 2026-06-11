namespace CrediSoft.Core.Models;

public class Cuota
{
    public int IdGeneradas { get; set; }
    public int IdCab { get; set; }
    public string Comprobante { get; set; } = string.Empty;
    public byte NCuota { get; set; }
    public decimal Monto { get; set; }
    public DateTime Vto { get; set; }
    public DateTime? FechaCobrado { get; set; }
    public int Mora { get; set; }               // Días de mora
    public decimal Punitorio { get; set; }
    public decimal Reajuste { get; set; }
    public decimal Total { get; set; }
    public byte IdLocal { get; set; }
    public int? IdU { get; set; }
    public string Obs { get; set; } = string.Empty;
    public byte Estado { get; set; }            // 0=pendiente, 1=cobrada
    public decimal Entrega { get; set; }

    // Navegación
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteCi { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
    public string NSolicitud { get; set; } = string.Empty;
    public string VendedorNombre { get; set; } = string.Empty;

    public bool EstaPendiente => Estado == 0 && FechaCobrado == null;
    public bool EstaVencida => EstaPendiente && Vto < DateTime.Today;
    public int DiasDeAtraso => EstaVencida ? (DateTime.Today - Vto).Days : 0;
}
