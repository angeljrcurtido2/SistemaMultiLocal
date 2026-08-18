using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrediSoft.UI.Services;

public record UpdateInfo(
    [property: JsonPropertyName("version")]     string Version,
    [property: JsonPropertyName("notas")]       string Notas,
    [property: JsonPropertyName("exe_path")]    string ExePath,
    [property: JsonPropertyName("fecha")]       string Fecha,
    [property: JsonPropertyName("obligatoria")] bool   Obligatoria = false);

public class UpdateService
{
    private readonly string _baseUrl;
    private readonly string _versionActual;
    private readonly string _exeActual;

    // Timeout corto: solo para VerificarActualizacionAsync (version.json, unos pocos KB),
    // que corre en cada apertura de la app (App.xaml.cs OnActivated) y no debe demorar
    // visiblemente el login si el tunel Cloudflare esta caido/lento — antes con 30s el
    // usuario podia ver la ventana sin reaccionar por medio minuto, sin ningun aviso.
    private static readonly HttpClient _httpCheck = new() { Timeout = TimeSpan.FromSeconds(6) };

    // Timeout largo: para DescargarYAplicarAsync, que baja ElectroMar.dll (varios MB) y
    // puede necesitar mas tiempo en conexiones lentas de sucursal — aqui SI vale la pena
    // esperar, porque el usuario ya vio el dialogo y decidio actualizar ahora.
    private static readonly HttpClient _httpDownload = new() { Timeout = TimeSpan.FromSeconds(120) };

    private readonly string _installId;

    public UpdateService(string baseUrl, string versionActual, string installId = "")
    {
        _baseUrl       = baseUrl.TrimEnd('/');
        _versionActual = versionActual;
        _exeActual     = Environment.ProcessPath ?? "";
        _installId     = installId;
    }

    public async Task<UpdateInfo?> VerificarActualizacionAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_baseUrl)) return null;

            // InstallId identifica esta copia especifica ante el servidor de
            // actualizaciones (ver servidor-updates.ps1 / emitidos.json). El servidor
            // niega la actualizacion a cualquier Id no reconocido — protege contra
            // copias del .exe distribuidas sin autorizacion (filtradas/robadas), aunque
            // esa copia tenga una contraseña de base de datos valida parcheada adentro:
            // el bloqueo no depende de la contraseña, depende de este Id.
            var url = $"{_baseUrl}/version.json?installId={Uri.EscapeDataString(_installId)}";
            var json = await _httpCheck.GetStringAsync(url);
            var info = JsonSerializer.Deserialize<UpdateInfo>(json);
            if (info == null) return null;

            // Toda actualización es obligatoria: se fuerza acá, en el cliente, sin
            // depender de que quien publique recuerde marcar "obligatoria": true en
            // version.json. Instrucción explícita del cliente — ninguna copia debe
            // seguir corriendo una versión vieja ni un momento más de lo necesario.
            info = info with { Obligatoria = true };

            return EsNuevaVersion(info.Version, _versionActual) ? info : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DescargarYAplicarAsync(UpdateInfo info, IProgress<int>? progreso = null)
    {
        try
        {
            var dirLocal  = Path.GetDirectoryName(_exeActual) ?? AppContext.BaseDirectory;
            var dirNueva  = Path.Combine(Path.GetTempPath(), "ElectroMarUpdate");

            // Copiar toda la instalacion actual a la carpeta nueva
            if (Directory.Exists(dirNueva)) Directory.Delete(dirNueva, recursive: true);
            CopiarDirectorio(dirLocal, dirNueva);

            // Descargar archivos actualizados sobre la copia.
            // ElectroMar.dll es el ensamblado real (contiene todo el codigo + XAML compilado/BAML);
            // ElectroMar.exe es solo el apphost nativo que lo carga. Sin el .dll, el exe
            // arranca pero sigue ejecutando la logica/ventanas de la version anterior.
            //
            // appsettings.json DELIBERADAMENTE NO esta en esta lista. Cada maquina tiene su
            // propio InstallId grabado ahi desde su instalacion original — sobreescribirlo con
            // el appsettings.json publicado (que trae el InstallId de la ULTIMA vez que se
            // corrio publicar.ps1, no el de esta maquina) le borraba su propio Id autorizado en
            // cada actualizacion real, dejandola con un Id ajeno que ademas ni siquiera estaba
            // en emitidos.json salvo que se hubiera vuelto a publicar despues. Bug real
            // reportado: "necesito que actualice el de los 14 locales de una, no tiene sentido
            // el sistema de actualizacion asi". La ConnectionString/ServidorPath/InstallId de
            // cada maquina son fijos desde la instalacion y no deben cambiar por una
            // actualizacion de version del codigo.
            var archivos = new[] { "ElectroMar.exe", "ElectroMar.dll", "CrediSoft.Core.dll", "CrediSoft.Data.dll" };
            int total = archivos.Length, hecho = 0;
            var logDescarga = Path.Combine(Path.GetTempPath(), "_electromar_download.log");
            var logLineas = new List<string> { $"INICIO DESCARGA {DateTime.Now:HH:mm:ss} baseUrl={_baseUrl} dirNueva={dirNueva}" };

            foreach (var archivo in archivos)
            {
                try
                {
                    // Cache-busting: un query string distinto por version fuerza a Cloudflare
                    // (que expone este servidor via tunel) a tratarlo como una URL nueva, sin
                    // cache previa — un release anterior quedo cacheado en el CDN pese a que el
                    // origen ya servia el archivo correcto, causando una actualizacion rota real
                    // (las 14 maquinas hubieran recibido un .exe viejo con runtime incompatible).
                    var bytes = await _httpDownload.GetByteArrayAsync(
                        $"{_baseUrl}/{archivo}?v={Uri.EscapeDataString(info.Version)}&installId={Uri.EscapeDataString(_installId)}");
                    var destino = Path.Combine(dirNueva, archivo);
                    await File.WriteAllBytesAsync(destino, bytes);
                    logLineas.Add($"OK  {archivo}  {bytes.Length} bytes  ->  {destino}");
                }
                catch (Exception ex)
                {
                    logLineas.Add($"FALLO  {archivo}  {ex.GetType().Name}: {ex.Message}");
                }

                hecho++;
                progreso?.Report((hecho * 100) / total);
            }

            try { await File.WriteAllLinesAsync(logDescarga, logLineas); } catch { }

            // Batch: espera activamente a que el PID actual muera (no timeout fijo),
            // y SIEMPRE relanza desde la carpeta local (para que accesos directos/taskbar
            // sigan apuntando ahi).
            //
            // IMPORTANTE — por que robocopy y no xcopy:
            // Cuando este batch se lanza desde .NET con Process.Start(UseShellExecute=false,
            // CreateNoWindow=true) (necesario para que no parpadee una consola visible), los
            // streams estandar del proceso quedan completamente redirigidos. xcopy en ese modo
            // intenta abrir un canal de entrada interactivo (para el prompt de sobrescritura,
            // pese a /Y) y aborta de inmediato con "ERROR: No es compatible la redireccion de
            // entradas" — SIN copiar nada — incluso en el primer intento (verificado con test
            // controlado). robocopy no tiene ese problema y ademas reintenta archivos bloqueados
            // nativamente via /R (reintentos) y /W (espera entre reintentos), sin necesitar
            // logica manual de reintento en el batch.
            var pidActual = Environment.ProcessId;
            var batch     = Path.Combine(Path.GetTempPath(), "_electromar_update.bat");
            var log       = Path.Combine(Path.GetTempPath(), "_electromar_update.log");
            var exeLocal  = Path.Combine(dirLocal, "ElectroMar.exe");
            var lines = new[]
            {
                "@echo off",
                "setlocal",
                $"echo INICIO %DATE% %TIME% > \"{log}\"",
                "",
                ":esperando",
                $"tasklist /FI \"PID eq {pidActual}\" 2>nul | find \"{pidActual}\" >nul",
                "if errorlevel 1 goto copiar",
                $"echo esperando PID {pidActual} todavia vivo >> \"{log}\"",
                "timeout /t 1 /nobreak >nul",
                "goto esperando",
                "",
                ":copiar",
                $"echo PID {pidActual} termino, copiando >> \"{log}\"",
                $"robocopy \"{dirNueva}\" \"{dirLocal}\" /E /R:10 /W:1 >> \"{log}\" 2>&1",
                $"echo ROBOCOPY EXIT CODE %ERRORLEVEL% >> \"{log}\"",
                "",
                $"echo arrancando exe local >> \"{log}\"",
                $"start \"\" \"{exeLocal}\"",
                $"rmdir /S /Q \"{dirNueva}\" >nul 2>&1",
                $"echo FIN >> \"{log}\"",
                "del \"%~f0\""
            };
            await File.WriteAllLinesAsync(batch, lines);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CopiarDirectorio(string origen, string destino)
    {
        Directory.CreateDirectory(destino);
        foreach (var archivo in Directory.GetFiles(origen))
            File.Copy(archivo, Path.Combine(destino, Path.GetFileName(archivo)), overwrite: true);
        foreach (var sub in Directory.GetDirectories(origen))
            CopiarDirectorio(sub, Path.Combine(destino, Path.GetFileName(sub)));
    }

    public void ReiniciarApp()
    {
        var batch = Path.Combine(Path.GetTempPath(), "_electromar_update.bat");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "cmd.exe",
            Arguments       = $"/c \"\"{batch}\"\"",
            UseShellExecute = false,
            CreateNoWindow  = true,
            WindowStyle     = System.Diagnostics.ProcessWindowStyle.Hidden
        });

        System.Windows.Application.Current.Shutdown();
    }

    private static bool EsNuevaVersion(string remota, string local)
    {
        if (!Version.TryParse(remota, out var r)) return false;
        if (!Version.TryParse(local,  out var l)) return false;
        return r > l;
    }
}
