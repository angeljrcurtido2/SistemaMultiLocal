namespace CrediSoft.Core.Models;

public class CajaMaster
{
    public int IdMaster { get; set; }
    public byte IdLocal { get; set; }
    public int IdCajaFisica { get; set; }
    public char Estado { get; set; }            // 'A'=abierta, 'C'=cerrada
    public DateTime FechaApertura { get; set; }
    public int IdUsuarioApe { get; set; }
    public decimal MontoBase { get; set; }
    public DateTime? FechaCierre { get; set; }
    public int? IdUsuarioCie { get; set; }
    public decimal TotEfectivo { get; set; }
    public decimal TotCheque { get; set; }
    public decimal TotTarjeta { get; set; }
    public decimal TotTransf { get; set; }
    public decimal TotOtro { get; set; }
    public decimal TotIngresos { get; set; }
    public decimal TotEgresos { get; set; }
    public decimal MontoGierreReal { get; set; }
    public string Observaciones { get; set; } = string.Empty;

    public bool EstaAbierta => Estado == 'A';
    public string LocalNombre { get; set; } = string.Empty;
    public string UsuarioAperturanombre { get; set; } = string.Empty;
}

public class CajaDetalle
{
    public long IdDetalle { get; set; }
    public int IdMaster { get; set; }
    public int? IdVenta { get; set; }
    public byte IdLocal { get; set; }
    public DateTime FechaHora { get; set; }
    public char Tipo { get; set; }              // 'I'=ingreso, 'E'=egreso
    public string Subtipo { get; set; } = string.Empty;
    public string FormaPago { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int IdCajero { get; set; }
    public int? IdEntidad { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public char EstadoReg { get; set; }         // 'V'=vigente, 'A'=anulado
}
