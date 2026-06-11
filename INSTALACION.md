# Instalación y Compilación — CrediSoft WPF

## Requisitos

1. **Windows 10/11** (64-bit)
2. **.NET 8 SDK** — descargar en: https://dotnet.microsoft.com/download/dotnet/8.0
   - Elegir: ".NET 8.0 SDK (Windows x64)"
3. **SQL Server Express** ya instalado con la base de datos CREDISOFT (ya existe)
4. **Visual Studio 2022** (opcional, para desarrollo) o solo el SDK para compilar

---

## Pasos para compilar y ejecutar

### Opción A — Solo con el SDK (sin Visual Studio)

```powershell
# 1. Abrir PowerShell como Administrador
# 2. Navegar al directorio del proyecto
cd "c:\SIS_VENTAS_NEXT\CrediSoftWPF"

# 3. Restaurar paquetes NuGet
dotnet restore

# 4. Compilar
dotnet build --configuration Release

# 5. Ejecutar
dotnet run --project CrediSoft.UI
```

### Opción B — Con Visual Studio 2022

1. Abrir Visual Studio 2022
2. File → Open → Solution → seleccionar `CrediSoft.sln`
3. Esperar que restaure paquetes NuGet (automático)
4. Presionar F5 para compilar y ejecutar

---

## Configuración de base de datos

Editar `CrediSoft.UI\appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=.\\SQLEXPRESS;Database=CREDISOFT;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```

Si el servidor SQL está en otra máquina:
```json
"Default": "Server=192.168.1.100\\SQLEXPRESS;Database=CREDISOFT;User Id=sa;Password=tu_pass;TrustServerCertificate=True;"
```

---

## Generar ejecutable standalone (.exe)

```powershell
cd "c:\SIS_VENTAS_NEXT\CrediSoftWPF"
dotnet publish CrediSoft.UI -c Release -r win-x64 --self-contained false -o publish\
```

El .exe quedará en `publish\CrediSoft.exe` (~15 MB).  
Requiere .NET 8 Runtime instalado en la PC destino (se instala una vez, gratis).

Para un ejecutable completamente independiente (sin necesidad de instalar .NET):
```powershell
dotnet publish CrediSoft.UI -c Release -r win-x64 --self-contained true -o publish_standalone\
```
(~60-80 MB, no requiere instalar nada adicional)

---

## Estado actual de módulos

| Módulo | Estado |
|---|---|
| Login / Autenticación | ✅ Completo |
| Ventana MDI principal + menús + toolbar | ✅ Completo |
| Estilos / Tema naranja Credimar | ✅ Completo |
| Clientes (CRUD completo) | ✅ Completo |
| Bancos, Categorías, Marcas, Medidas, Procedencias, Secciones | ✅ Completo |
| Cobrar cuota (con cálculo de punitorio) | ✅ Completo |
| Caja — Apertura | ✅ Completo |
| Caja — Cierre | ✅ Completo |
| Informe Atrasos / Morosos | ✅ Completo |
| Artículos (CRUD completo + Precios + Stock) | ✅ Completo |
| Ventas a Crédito (flujo 4 pasos) | ✅ Completo |
| Ventas al Contado | 🔄 En desarrollo |
| Compras a Proveedores | 🔄 En desarrollo |
| Transferencias inter-locales | 🔄 En desarrollo |
| Informes H.Ventas, H.Créditos, etc. | 🔄 En desarrollo |
| Funcionarios | 🔄 En desarrollo |
| Proveedores | 🔄 En desarrollo |

---

## Arquitectura

```
CrediSoftWPF/
├── CrediSoft.UI/          ← Aplicación WPF (pantallas XAML + code-behind)
│   ├── Views/Login/       ← Pantalla de login
│   ├── Views/Main/        ← Ventana principal MDI
│   ├── Views/Maestros/    ← Clientes, Artículos, Bancos, etc.
│   ├── Views/Ventas/      ← Ventas crédito/contado
│   ├── Views/Cobros/      ← Cobrar cuotas
│   ├── Views/Caja/        ← Apertura/Cierre/Arqueo
│   ├── Views/Informes/    ← Atrasos, H.Ventas, etc.
│   ├── Controls/          ← Controles reutilizables (MaestroSimpleWindow)
│   └── Styles/            ← Tema naranja Credimar
├── CrediSoft.Core/        ← Modelos C# + Servicios (sin dependencias externas)
│   ├── Models/            ← Cliente, Artículo, Cuota, Venta, Caja, etc.
│   ├── Services/          ← AuthService, SessionService
│   └── Interfaces/        ← IRepositories
└── CrediSoft.Data/        ← Acceso a SQL Server con Dapper
    └── Repositories/      ← Llaman a los SPs existentes de CREDISOFT
```

**Principio de datos:** El nuevo frontend llama a los mismos ~280 stored procedures  
que usaba el VB6 original. La base de datos CREDISOFT no se modifica.
