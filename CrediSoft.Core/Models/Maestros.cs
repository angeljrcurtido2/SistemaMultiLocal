namespace CrediSoft.Core.Models;

// Tipo compartido para grillas de maestros simples (usado por Data y UI)
public record MaestroItem(int Id, string Codigo, string Nombre);

public class Banco
{
    public int IdBanco { get; set; }
    public string CodigoBanco { get; set; } = string.Empty;
    public string NombreBanco { get; set; } = string.Empty;
}

public class Categoria
{
    public int IdCategoria { get; set; }
    public string CodigoCategoria { get; set; } = string.Empty;
    public string NombreCategoria { get; set; } = string.Empty;
}

public class Subcategoria
{
    public int IdSubcategoria { get; set; }
    public int IdCategoria { get; set; }
    public string CodigoSubcategoria { get; set; } = string.Empty;
    public string NombreSubcategoria { get; set; } = string.Empty;
    public string CategoriaNombre { get; set; } = string.Empty;
}

public class Marca
{
    public int IdMarca { get; set; }
    public string CodigoMarca { get; set; } = string.Empty;
    public string NombreMarca { get; set; } = string.Empty;
}

public class Proveedor
{
    public int IdProveedor { get; set; }
    public string CodigoProveedor { get; set; } = string.Empty;
    public string RucProveedor { get; set; } = string.Empty;
    public DateTime? FechaRegistro { get; set; }
    public string NombreProveedor { get; set; } = string.Empty;
    public string DireccionProveedor { get; set; } = string.Empty;
    public string TelefonoProveedor { get; set; } = string.Empty;
    public string CiudadProveedor { get; set; } = string.Empty;
    public string EmailProveedor { get; set; } = string.Empty;
    public string WebProveedor { get; set; } = string.Empty;
    public string ContactoNombre { get; set; } = string.Empty;
    public string ContactoCargo { get; set; } = string.Empty;
    public string ContactoCelular { get; set; } = string.Empty;
    public string ContactoCorreo { get; set; } = string.Empty;
}

public class Medida
{
    public int IdMedida { get; set; }
    public string CodigoMedida { get; set; } = string.Empty;
    public string NombreMedida { get; set; } = string.Empty;
}

public class Pais
{
    public int IdPais { get; set; }
    public string CodigoPais { get; set; } = string.Empty;
    public string NombrePais { get; set; } = string.Empty;
}

public class Seccion
{
    public int IdSeccion { get; set; }
    public string CodigoSeccion { get; set; } = string.Empty;
    public string NombreSeccion { get; set; } = string.Empty;
}
