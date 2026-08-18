param(
    [string]$Destino = "$PSScriptRoot\CrediSoft.UI\CREDISOFT.db"
)

$sqlServer  = ".\SQLEXPRESS"
$database   = "CREDISOFT"
$sqlitePath = $Destino

Write-Host "MIGRACION SQL SERVER -> SQLITE" -ForegroundColor Cyan
Write-Host "Destino: $sqlitePath" -ForegroundColor Cyan

if (Test-Path $sqlitePath) { Remove-Item $sqlitePath -Force }

# Cargar SQLite
$nuget = "$env:USERPROFILE\.nuget\packages"
Add-Type -Path "$nuget\sqlitepclraw.core\2.1.11\lib\net8.0\SQLitePCLRaw.core.dll"
Add-Type -Path "$nuget\sqlitepclraw.lib.e_sqlite3\2.1.11\runtimes\win-x64\native\e_sqlite3.dll"
Add-Type -Path "$nuget\sqlitepclraw.provider.e_sqlite3\2.1.11\lib\net8.0\win-x64\SQLitePCLRaw.provider.e_sqlite3.dll"
Add-Type -Path "$nuget\microsoft.data.sqlite.core\10.0.9\lib\net8.0\Microsoft.Data.Sqlite.dll"

function Get-SqlTable($query) {
    $conn = New-Object System.Data.SqlClient.SqlConnection "Server=$sqlServer;Database=$database;Integrated Security=True;TrustServerCertificate=True"
    $conn.Open()
    $cmd = $conn.CreateCommand(); $cmd.CommandText = $query; $cmd.CommandTimeout = 300
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null
    $conn.Close()
    return $dt
}

function Map-Type($t) {
    switch -Wildcard ($t.ToLower()) {
        "int*"     { return "INTEGER" }
        "bigint"   { return "INTEGER" }
        "smallint" { return "INTEGER" }
        "tinyint"  { return "INTEGER" }
        "bit"      { return "INTEGER" }
        "decimal*" { return "REAL" }
        "numeric*" { return "REAL" }
        "float*"   { return "REAL" }
        "money*"   { return "REAL" }
        default    { return "TEXT" }
    }
}

$tablas = @(
    "ARTICULOS","AUDITORIA","BANCOS","CAB_BUY_TMP","CAB_BUYS","CAB_CAJA",
    "CAB_REMITO_ACEPTADO","CAB_REMITO_TMP","CAB_SOL_SALES","CABECERA_SALES",
    "CAJA","CAJA_DETALLE","CAJA_MASTER","CATEGORIAS","CLIENTES","CONFIGURACION",
    "CONTADORES","DET_BUY_TMP","DET_BUYS","DET_CAJA","DET_REMITO_ACEPTADO",
    "DET_REMITO_TMP","DET_SOL_SALES","DETALLE_PAGO","DETALLES_NOTA_CREDITO",
    "DETALLES_SALES","DOCUMENTOS","EMPRESA","FOTOS","GARANTES","GENERADAS",
    "GENERAR_PAGOSALARIO","HISTORIAL_COBRO_CUOTAS","HISTORIAL_ENTREGAS_GENERADAS",
    "HISTORIAL_NOTA_CREDITO","HISTORIAL_PAGOS","HISTORIALPAGOFUN","IMPRESORAS",
    "LOCALES","MARCAS","MEDIDAS","MOV_FUNCIONARIOS","MOVART","NumeracionTickets",
    "PAISES","PRICES","PROVEEDORES","REFERENCIAS","RETIRO_LIBRE","SECCIONES",
    "SUBCATEGORIAS","USUARIOS"
)

$liteCon = New-Object Microsoft.Data.Sqlite.SqliteConnection "Data Source=$sqlitePath"
$liteCon.Open()
$pragmaCmd = $liteCon.CreateCommand()
$pragmaCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF; PRAGMA foreign_keys=OFF;"
$pragmaCmd.ExecuteNonQuery() | Out-Null

$i = 0
foreach ($tabla in $tablas) {
    $i++
    Write-Host "[$i/$($tablas.Count)] $tabla..." -NoNewline

    try {
        $cols = Get-SqlTable "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMNPROPERTY(OBJECT_ID('$tabla'), COLUMN_NAME, 'IsIdentity') as IS_ID FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='$tabla' ORDER BY ORDINAL_POSITION"

        $colDefs = @()
        foreach ($col in $cols) {
            $tipo     = Map-Type $col.DATA_TYPE
            $nullable = if ($col.IS_NULLABLE -eq "NO") { " NOT NULL" } else { "" }
            $pk       = if ($col.IS_ID -eq 1) { " PRIMARY KEY AUTOINCREMENT" } else { "" }
            $colDefs += "[$($col.COLUMN_NAME)] $tipo$pk$nullable"
        }
        $createCmd = $liteCon.CreateCommand()
        $createCmd.CommandText = "CREATE TABLE IF NOT EXISTS [$tabla] ($($colDefs -join ', '))"
        $createCmd.ExecuteNonQuery() | Out-Null

        $data  = Get-SqlTable "SELECT * FROM [$tabla]"
        $filas = $data.Rows.Count

        if ($filas -gt 0) {
            $colNames  = ($cols | ForEach-Object { "[$($_.COLUMN_NAME)]" }) -join ","
            $colParams = ($cols | ForEach-Object { "@$($_.COLUMN_NAME)" }) -join ","

            $tx = $liteCon.BeginTransaction()
            $ins = $liteCon.CreateCommand()
            $ins.Transaction = $tx
            $ins.CommandText = "INSERT OR REPLACE INTO [$tabla] ($colNames) VALUES ($colParams)"
            foreach ($col in $cols) {
                $ins.Parameters.Add("@$($col.COLUMN_NAME)", [Microsoft.Data.Sqlite.SqliteType]::Text) | Out-Null
            }
            foreach ($row in $data.Rows) {
                foreach ($col in $cols) {
                    $v = $row[$col.COLUMN_NAME]
                    $ins.Parameters["@$($col.COLUMN_NAME)"].Value = if ($v -is [DBNull]) { [DBNull]::Value } else { $v.ToString() }
                }
                $ins.ExecuteNonQuery() | Out-Null
            }
            $tx.Commit()
        }
        Write-Host " $filas filas OK" -ForegroundColor Green
    } catch {
        Write-Host " ERROR: $_" -ForegroundColor Red
    }
}

$liteCon.Close()
$sizeMB = [math]::Round((Get-Item $sqlitePath).Length / 1MB, 1)
Write-Host ""
Write-Host "COMPLETADO - $sqlitePath ($sizeMB MB)" -ForegroundColor Green
