namespace CrediSoft.Core.Models;

public class Price
{
    public int IdPrices { get; set; }
    public int IdArt { get; set; }
    public byte IdLocal { get; set; }
    public byte Deletado { get; set; }
    public byte Pr { get; set; }
    public decimal S { get; set; }          // Stock actual
    public decimal Pc { get; set; }         // Precio costo
    public decimal Pventa { get; set; }     // Precio venta crédito
    public decimal Contado { get; set; }    // Precio contado
    public decimal Ppromo { get; set; }     // Precio promoción
    public decimal Pfuturo { get; set; }
    public DateTime? Fcompra { get; set; }
    public DateTime? Fventa { get; set; }
    public DateTime? Fmp { get; set; }
    public DateTime? Fms { get; set; }
    public int? Iducompra { get; set; }
    public int? Iduventa { get; set; }
    public int? Idumodprecio { get; set; }
    public int? Idumodstock { get; set; }
    public DateTime? Inicio { get; set; }   // Inicio promoción
    public DateTime? Fin { get; set; }      // Fin promoción

    public bool EnPromocion => Inicio.HasValue && Fin.HasValue
        && DateTime.Now >= Inicio.Value && DateTime.Now <= Fin.Value
        && Ppromo > 0;

    // Propiedad de navegación (join con LOCALES)
    public string LocalNombre { get; set; } = string.Empty;
}
