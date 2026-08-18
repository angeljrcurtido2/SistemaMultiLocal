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

    // Excepción puntual pedida por el cliente: el usuario código "67" (Aida Acosta, cargo
    // Vendedor) debe poder consultar los movimientos/arqueos de TODOS los locales en las
    // pantallas de Caja (Historial, Gastos, Explorador, Arqueo), igual que un administrador,
    // aunque no lo sea. No es un permiso general de "vendedor" — es específico de este
    // usuario. Si se necesita extender a más usuarios, agregar sus códigos a este arreglo.
    private static readonly string[] _codigosConAccesoTotalLocales = { "67" };
    public bool PuedeVerTodosLosLocales => EsAdministrador || Array.IndexOf(_codigosConAccesoTotalLocales, CodigoUsuario) >= 0;

    // Pedido explícito: "Asignar cobrador" (CobranzaAsignacionesWindow) solo debe estar
    // disponible para el administrador o el usuario código "67" — mismo patrón que
    // PuedeVerTodosLosLocales. Si se necesita extender a más usuarios, agregar sus códigos acá.
    private static readonly string[] _codigosConAccesoAsignarCobradores = { "67" };
    public bool PuedeAsignarCobradores => EsAdministrador || Array.IndexOf(_codigosConAccesoAsignarCobradores, CodigoUsuario) >= 0;

    // Pantalla Locales / Sucursales: dato delicado (afecta comprobantes impresos de las 14
    // sucursales), por lo que se separa en dos niveles distintos de PuedeVerTodosLosLocales:
    //  - Administrador: único que puede crear locales y editar Nombre/Dirección/Ciudad/Teléfono.
    //  - Todos los que cumplen PuedeVerTodosLosLocales (admin + código 67): pueden ver los 14
    //    locales y editar el Teléfono de cualquiera, pero NO crear ni tocar los demás campos.
    public bool PuedeEditarTodosLosDatosDeLocales => EsAdministrador;

    // Descuentos por Nota de Crédito aplicados a una cuota puntual (ver DescuentoCuotaWindow):
    // solo Administrador o el usuario código 67 pueden crearlos — mismo patrón que
    // PuedeVerTodosLosLocales/PuedeAsignarCobradores. El descuento queda guardado en la cuota
    // y lo ve/aplica automáticamente cualquier cajero de cualquier local al cobrarla.
    private static readonly string[] _codigosConAccesoDescuentosCuota = { "67" };
    public bool PuedeCrearDescuentosCuota => EsAdministrador || Array.IndexOf(_codigosConAccesoDescuentosCuota, CodigoUsuario) >= 0;
    public bool PuedeDescuento => PermisoDescuento == "SI";
    public bool PuedeModificarStock => PermisoStock == "SI";
    public bool PuedeModificarPrecios => PermisoPrecio == "SI";
    public bool PuedeEliminar => PermisoEliminar == "SI";
    public bool PuedeCobrar => PermisoCobrarCuotas == "SI";
    public bool PuedeCompras => PermisoCompras == "SI";
    public bool PuedeEliminarFactura => PermisoEliminarFactura == "SI";
}
