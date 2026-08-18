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
    // GENERADAS.REAJUSTE mezcla en un solo campo el cargo automático de Inforconf y
    // cualquier ajuste manual del cajero — este flag (persistido aparte, GENERADAS.
    // INFORCOM_APLICADO) es la única forma de saber, después de cobrada, si el reajuste
    // correspondía a Inforconf, sin asumirlo por el monto. Ver CobrosWindow (pantalla
    // "CUOTA COBRADA") para dónde se usa.
    public bool InforcomAplicado { get; set; }

    // Navegación
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteCi { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
    public string NSolicitud { get; set; } = string.Empty;
    public string VendedorNombre { get; set; } = string.Empty;
    public string LocalNombre { get; set; } = string.Empty;
    public string LocalTexto => string.IsNullOrEmpty(LocalNombre) ? IdLocal.ToString() : $"{IdLocal} - {LocalNombre}";
    // De CABECERA_SALES
    public decimal CabTotal { get; set; }   // Total original del crédito
    public decimal CabHaber { get; set; }   // Ya cobrado
    // MAX(NCUOTA) real entre las filas de GENERADAS de este crédito — no se usa
    // CABECERA_SALES.CUOTAS porque no siempre refleja la cantidad real (reajustes,
    // cuotas agregadas después de la venta original quedan desincronizados con ese campo).
    public byte TotalCuotasCredito { get; set; }

    // El sistema viejo inicializa FECHACOBRADO = VTO al crear cuotas — no indica cobro real.
    // La única fuente de verdad es ESTADO: 0 = pendiente, 1 = cobrada.
    public bool EstaPendiente => Estado == 0;
    public bool EstaVencida   => Estado == 0 && Vto.Date < DateTime.Today;
    // Período de gracia de 5 días (confirmado por el usuario 2026-07-30; antes era 3)
    public int DiasDeAtraso   => EstaVencida ? Math.Max(0, (DateTime.Today - Vto.Date).Days - 5) : 0;
    public decimal TotalCuota => Monto + Punitorio;
    public string EstadoTextoCorto => Estado == 0 ? (EstaVencida ? "Vencida" : "Pendiente") : "Cobrada";

    // El SP legado (AGREGAR_GENERADAS_CS) graba la ENTREGA inicial de la venta como
    // NCUOTA=1 en GENERADAS — no es una cuota real del plan de pago pactado con el
    // cliente, sino el pago inicial ya cobrado al confirmar la venta. Todas las cuotas
    // reales (las que el cliente sigue pagando mes a mes) quedan corridas +1: la
    // "primera cuota" del cliente se graba como NCUOTA=2, la segunda como NCUOTA=3, etc.
    // NCuotaVisible/TotalCuotasVisible restan ese desfase para que lo que se muestra al
    // cliente (pantalla, diálogos de cobro, ticket impreso) coincida con lo pactado en la
    // Proforma (que sí numera 1..N solo con cuotas reales) — sin tocar NCuota, que se
    // sigue necesitando intacto para filtros/joins contra GENERADAS.
    public int NCuotaVisible => Math.Max(0, NCuota - 1);
    public int TotalCuotasVisible => Math.Max(0, TotalCuotasCredito - 1);
    // Para la grilla de "Cuotas del crédito" (Cobrar Cuota) se usa el NCUOTA real de
    // GENERADAS, sin renumerar — pedido explícito para que coincida exactamente con el
    // número que ve el cajero en el sistema viejo (ej. "2 / 3"), sin la resta de NCuotaVisible
    // que sí se usa en la Proforma/ticket para no contar la entrega como cuota pactada.
    // La entrega (NCuota=1) se marca como "Cuota 1 (Entrega)" en vez de "1 / N" — pedido
    // explícito para dejar claro que es la cuota 1 del plan, no una fila aparte.
    public string NCuotaTexto => NCuota == 1
        ? "Cuota 1 (Entrega)"
        : TotalCuotasCredito > 0 ? $"{NCuota} / {TotalCuotasCredito}" : NCuota.ToString();
}
