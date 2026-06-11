namespace CrediSoft.Core.Models;

public class CabeceraVenta
{
    public int IdCab { get; set; }
    public string NSolicitud { get; set; } = string.Empty;
    public byte IdLocal { get; set; }
    public int IdUsuario { get; set; }
    public int IdCliente { get; set; }
    public int? IdGarante { get; set; }
    public byte FormaDeVenta { get; set; }      // 1=crédito, 2=contado
    public byte MetodoDeVenta { get; set; }     // 1=efectivo, 2=tarjeta, etc.
    public string NTarjeta { get; set; } = string.Empty;
    public decimal Parcial { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
    public decimal EntregaNormal { get; set; }
    public decimal EntregaLogistica { get; set; }
    public byte Cuotas { get; set; }
    public decimal MontoCuota { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Debe { get; set; }
    public decimal Haber { get; set; }
    public byte Cpha { get; set; }
    public byte Estado { get; set; }            // 1=activo, 0=anulado
    public decimal Tiva { get; set; }
    public int NVenta { get; set; }
    public string NVentaChar { get; set; } = string.Empty;
    public int? IdMaster { get; set; }

    // Navegación
    public string ClienteNombre { get; set; } = string.Empty;
    public string LocalNombre { get; set; } = string.Empty;
    public string VendedorNombre { get; set; } = string.Empty;
    public List<DetalleVenta> Detalles { get; set; } = new();
}

public class DetalleVenta
{
    public int IdDet { get; set; }
    public int IdCab { get; set; }
    public int IdArt { get; set; }
    public decimal Cantidad { get; set; }
    public decimal Pc { get; set; }
    public decimal Pv { get; set; }
    public decimal Iva { get; set; }
    public byte Es { get; set; }
    public string NVentaChar { get; set; } = string.Empty;

    // Navegación
    public string ArticuloCodigo { get; set; } = string.Empty;
    public string ArticuloNombre { get; set; } = string.Empty;
}
