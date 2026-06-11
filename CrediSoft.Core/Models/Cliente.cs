namespace CrediSoft.Core.Models;

public class Cliente
{
    public int IdCliente { get; set; }
    public string RucCliente { get; set; } = string.Empty;
    public string CiCliente { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;
    public string DireccionCliente { get; set; } = string.Empty;
    public string TelefonoCliente { get; set; } = string.Empty;
    public string CiudadCliente { get; set; } = string.Empty;
    public string ZonaCliente { get; set; } = string.Empty;
    public string ReferenciaGeo { get; set; } = string.Empty;
    public string EmailCliente { get; set; } = string.Empty;
    public decimal CredMax { get; set; }
    public byte Estado { get; set; }           // 0=Normal, 1=Moroso
    public byte Inforcom { get; set; }         // 0=Libre, 1=Registrado
    public byte Sexo { get; set; }             // 0=Femenino, 1=Masculino
    public byte Condicion { get; set; }
    public byte Ecv { get; set; }              // Estado civil 0-4
    public string Conyuge { get; set; } = string.Empty;
    public string CiConyuge { get; set; } = string.Empty;
    public string EmpresaLaboral { get; set; } = string.Empty;
    public string TelefonoLaboral { get; set; } = string.Empty;
    public string DireccionLaboral { get; set; } = string.Empty;
    public string Antiguedad { get; set; } = string.Empty;
    public byte Tipo { get; set; }
    public int Local { get; set; }
    public string FotoCliente { get; set; } = string.Empty;
    public DateTime? VencCedula { get; set; }
    public DateTime FechaRegistro { get; set; }

    public string EstadoTexto => Estado == 0 ? "Normal" : "Moroso";
    public string SexoTexto => Sexo == 0 ? "Femenino" : "Masculino";
    public string EstadoCivilTexto => Ecv switch
    {
        0 => "Soltero",
        1 => "Casado",
        2 => "Divorciado",
        3 => "Separado",
        4 => "Viudo",
        _ => "—"
    };
}
