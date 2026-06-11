namespace CrediSoft.Core.Models;

public class Usuario
{
    public int IdUsuario { get; set; }
    public string CodigoUsuario { get; set; } = string.Empty;
    public string ContrasenaUsuario { get; set; } = string.Empty;
    public int LocalUsuario { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string DireccionUsuario { get; set; } = string.Empty;
    public string TelefonoUsuario { get; set; } = string.Empty;
    public decimal SalarioUsuario { get; set; }
    public string CargoUsuario { get; set; } = string.Empty;
    public string PermisoDescuento { get; set; } = "NO";
    public string PermisoStock { get; set; } = "NO";
    public string PermisoPrecio { get; set; } = "NO";
    public string PermisoEliminar { get; set; } = "NO";
    public string PermisoCobrarCuotas { get; set; } = "NO";
    public string PermisoCompras { get; set; } = "NO";
    public string PermisoEliminarFactura { get; set; } = "NO";
    public string ComisionUsuario { get; set; } = "0";
    public string ComisionCobranza { get; set; } = "0";
    public string ZonaUsuario { get; set; } = string.Empty;
    public string CiUsuario { get; set; } = string.Empty;
    public string LocalNombre { get; set; } = string.Empty;

    public bool EsAdministrador => CargoUsuario.ToUpper() == "ADMINISTRADOR";
    public bool PuedeDescuento => PermisoDescuento == "SI";
    public bool PuedeModificarStock => PermisoStock == "SI";
    public bool PuedeModificarPrecios => PermisoPrecio == "SI";
    public bool PuedeEliminar => PermisoEliminar == "SI";
    public bool PuedeCobrar => PermisoCobrarCuotas == "SI";
    public bool PuedeCompras => PermisoCompras == "SI";
    public bool PuedeEliminarFactura => PermisoEliminarFactura == "SI";
}
