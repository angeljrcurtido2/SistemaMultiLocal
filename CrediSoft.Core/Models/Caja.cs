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
    // Desglose de lo que el cajero contó/declaró al cerrar, por medio de pago — MontoGierreReal
    // sigue siendo la suma de estos 5 y se mantiene igual (la lee también el sistema legado),
    // pero el desglose permite comparar cada medio contra su propio neto en vez de mezclarlos.
    public decimal? MontoCierreEfectivo { get; set; }
    public decimal? MontoCierreTarjeta { get; set; }
    public decimal? MontoCierreTransf { get; set; }
    public decimal? MontoCierreQr { get; set; }
    public decimal? MontoCierreCheque { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    // Comprobante de depósito del efectivo de este cierre — el dueño del negocio pidió poder
    // exigirlo, pero SIN bloquear el cierre en sí: se puede cargar en el momento (si el cajero
    // ya hizo el depósito) o completarlo después, con 48hs de plazo desde FechaCierre.
    public string? NroComprobanteDeposito { get; set; }
    public bool TieneFotoComprobante { get; set; }
    // Caja donde se registró al menos un pago de salario (SUBTIPO 'PAGO' en CAJA_DETALLE): el
    // efectivo salió como sueldo, no hay nada que depositar en el banco. No se condiciona al
    // signo del neto de la caja (TOT_INGRESOS/TOT_EGRESOS pueden no reflejarlo fielmente) —
    // basta con que exista el movimiento de pago. Sin esto, estas cajas quedaban marcadas como
    // "comprobante pendiente/vencido" sin que exista jamás un comprobante real que cargar.
    public bool DesfinanciadaPorPagoSalario { get; set; }

    public bool EstaAbierta => Estado == 'A';
    public string LocalNombre { get; set; } = string.Empty;
    public string UsuarioAperturanombre { get; set; } = string.Empty;
    public string NombreCajero { get; set; } = string.Empty;

    public bool ComprobanteCompleto =>
        !string.IsNullOrWhiteSpace(NroComprobanteDeposito) && TieneFotoComprobante;
    // Ventana de 48hs contadas desde el cierre — pasado ese plazo, ya no se puede cargar/editar.
    public bool DentroDePlazoComprobante =>
        FechaCierre.HasValue && (DateTime.Now - FechaCierre.Value).TotalHours <= 48;
    // Por ahora el único caso conocido de "no aplica comprobante" es el pago de salario que
    // desfinancia la caja. Otros motivos (ej. transferencias entre locales) se evaluarán más
    // adelante si aparecen.
    public bool AplicaComprobanteDeposito => !DesfinanciadaPorPagoSalario;
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
