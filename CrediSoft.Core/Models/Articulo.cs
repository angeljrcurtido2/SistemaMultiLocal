namespace CrediSoft.Core.Models;

public class Articulo
{
    public int Id { get; set; }
    public string Ca { get; set; } = string.Empty;      // Código
    public string Serial { get; set; } = string.Empty;
    public string D { get; set; } = string.Empty;       // Descripción
    public string Pres { get; set; } = string.Empty;    // Presentación
    public decimal Smin { get; set; }                   // Stock mínimo
    public int? Ids { get; set; }                       // FK Secciones
    public int? Idpr { get; set; }                      // FK Proveedores
    public int? Idc { get; set; }                       // FK Categorias
    public int? Idsbc { get; set; }                     // FK Subcategorias
    public byte? Idpais { get; set; }                   // FK Paises
    public int? Idm { get; set; }                       // FK Marcas
    public byte? Idmed { get; set; }                    // FK Medidas
    public byte Gra { get; set; }                       // Tipo: genérico/serial
    public decimal Iva { get; set; }
    public byte Maxcuota { get; set; }
    public byte Slc { get; set; }                       // Venta por local
    public byte Vu { get; set; }                        // Unidad de venta
    public DateTime? Vto { get; set; }                  // Vencimiento
    public DateTime? Fcompra { get; set; }
    public int? Iducompra { get; set; }
    public byte Es { get; set; }                        // Estado/habilitado

    // Navegación (join)
    public string SeccionNombre { get; set; } = string.Empty;
    public string CategoriaNombre { get; set; } = string.Empty;
    public string SubcategoriaNombre { get; set; } = string.Empty;
    public string MarcaNombre { get; set; } = string.Empty;
    public string ProveedorNombre { get; set; } = string.Empty;
    public string PaisNombre { get; set; } = string.Empty;
    public string MedidaNombre { get; set; } = string.Empty;

    public string EstadoTexto => Es == 1 ? "Activo" : "Inactivo";
}
