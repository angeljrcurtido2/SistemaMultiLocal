using CrediSoft.Core.Services;
using CrediSoft.Core.Interfaces;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Services;
using CrediSoft.UI.Views.Update;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace CrediSoft.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    // Marca una Window como "no-modal aunque tenga Owner" — asignar a Window.Tag en el
    // constructor de la ventana. Ver comentario en OnStartup sobre el handler global que
    // quita minimizar/maximizar a los modales.
    public static readonly object PermitirMinimizarTag = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        // Captura todas las excepciones no manejadas para diagnóstico
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            var msg = ex.ExceptionObject?.ToString() ?? "Error desconocido";
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), msg);
            MessageBox.Show("Error crítico:\n\n" + msg[..Math.Min(msg.Length, 800)], "ElectroMar - Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (s, ex) =>
        {
            var msg = ex.Exception?.ToString() ?? "Error desconocido";
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash_ui.log"), msg);
            MessageBox.Show("Error de UI:\n\n" + msg[..Math.Min(msg.Length, 800)], "ElectroMar - Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        // Forzar cultura paraguaya: punto como separador de miles, coma como decimal
        var cultura = new CultureInfo("es-PY");
        CultureInfo.DefaultThreadCurrentCulture   = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));

        base.OnStartup(e);

        // Cuando se ejecuta con `dotnet ElectroMar.dll`, Environment.ProcessPath apunta a
        // `dotnet.exe` y no a la carpeta de la app. AppContext.BaseDirectory sí resuelve la
        // carpeta real donde están `appsettings.json` y el resto de binarios, tanto si se
        // lanza con `dotnet` como con el `.exe` nativo.
        var basePath = AppContext.BaseDirectory;
        var profile = ResolveConnectionProfile(e.Args);
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{profile}.json", optional: true)
            .Build();

        var services = new ServiceCollection();
        var connStr = config.GetConnectionString("Default")!;

        // Infraestructura
        services.AddSingleton<IDbConnectionFactory>(DbConnectionFactoryBuilder.Create(connStr));

        // Repositorios
        services.AddTransient<IUsuarioRepository, UsuarioRepository>();
        services.AddTransient<ILocalRepository, LocalRepository>();
        services.AddTransient<IClienteRepository, ClienteRepository>();
        services.AddTransient<IArticuloRepository, ArticuloRepository>();
        services.AddTransient<IVentaRepository, VentaRepository>();
        services.AddTransient<ICuotaRepository, CuotaRepository>();
        services.AddTransient<ICajaRepository, CajaRepository>();
        services.AddTransient<IPagoRepository, PagoRepository>();
        services.AddTransient<IMultaRepository, MultaRepository>();
        services.AddTransient<IMaestrosBancoRepository, MaestrosBancoRepository>();
        services.AddTransient<IMaestrosCategoriaRepository, MaestrosCategoriaRepository>();
        services.AddTransient<IMaestrosSubcategoriaRepository, MaestrosSubcategoriaRepository>();
        services.AddTransient<IMaestrosMarcaRepository, MaestrosMarcaRepository>();
        services.AddTransient<IMaestrosProveedorRepository, MaestrosProveedorRepository>();
        services.AddTransient<IMaestrosSeccionRepository, MaestrosSeccionRepository>();
        services.AddTransient<IMaestrosMedidaRepository, MaestrosMedidaRepository>();
        services.AddTransient<IMaestrosPaisRepository, MaestrosPaisRepository>();
        services.AddTransient<IMaestroGenericoRepository, MaestroGenericoRepository>();

        // Servicios
        services.AddSingleton<ISessionService>(SessionService.Instance);
        services.AddTransient<AuthService>();

        Services = services.BuildServiceProvider();

        // Guardar config para uso del updater
        _config = config;

        // NOTA: hubo un fix acá (quitar minimizar/maximizar a todo modal con Owner, vía
        // EventManager.RegisterClassHandler sobre Window.LoadedEvent) para el bug de Ventas
        // quedando "trabado" cuando un cajero minimizaba un diálogo modal sin cerrarlo. Se
        // revirtió por pedido explícito del cliente: los modales vuelven a tener sus 3
        // botones (minimizar/maximizar/cerrar) normales de WPF. El riesgo original de bloqueo
        // sigue latente si alguien minimiza un ShowDialog() y no lo restaura — los casos
        // puntuales ya migrados a no-modal (DetalleSolicitudWindow, HistorialCrediticioModal,
        // marcados con Tag == PermitirMinimizarTag) no se ven afectados por esto, siguen
        // siendo Show() sin Owner.

        IniciarChequeoDeActualizaciones();
    }

    private static string ResolveConnectionProfile(string[] args)
    {
        static string? ReadProfileValue(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
                return trimmed["--profile=".Length..];

            return null;
        }

        string? profile = null;

        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];
            profile ??= ReadProfileValue(current);

            if (!string.IsNullOrWhiteSpace(profile))
                break;

            if (current.Equals("--profile", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                profile = args[i + 1];
                break;
            }
        }

        profile ??= Environment.GetEnvironmentVariable("CREDISOFT_PROFILE");
        profile ??= "Remote";

        return profile.Trim().Equals("local", StringComparison.OrdinalIgnoreCase)
            ? "Local"
            : "Remote";
    }

    private IConfiguration? _config;

    private async void IniciarChequeoDeActualizaciones()
    {
        // Se dispara desde OnStartup, NO desde OnActivated: OnActivated depende de que la
        // ventana principal gane foco, lo cual no está garantizado (ventana lanzada de
        // fondo por el propio updater, minimizada, detrás de otra app) — se detectó en
        // pruebas reales que una instancia podía quedar corriendo indefinidamente sin
        // disparar ni un solo chequeo porque Activated nunca llegó a ocurrir. OnStartup
        // corre siempre exactamente una vez al iniciar el proceso, sin depender de foco.
        await ChequearYForzarActualizacionAsync();

        // Chequeo periódico mientras la app sigue abierta — sin esto, una sesión que
        // ya estaba abierta cuando se publica una versión nueva recién se enteraría al
        // cerrar y reabrir (puede tardar todo el día en algunas sucursales).
        // Instrucción explícita del cliente: toda actualización debe cerrar/aplicar de
        // inmediato, no solo en el próximo arranque.
        _timerUpdate = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timerUpdate.Tick += async (_, _) => await ChequearYForzarActualizacionAsync();
        _timerUpdate.Start();
    }

    private DispatcherTimer? _timerUpdate;
    private bool _dialogoActualizacionAbierto = false;

    private async Task ChequearYForzarActualizacionAsync()
    {
        // Evita abrir un segundo diálogo si el timer dispara mientras el anterior
        // (obligatorio, no cancelable) sigue en pantalla descargando.
        if (_dialogoActualizacionAbierto) return;

        var (svc, info) = await ObtenerActualizacionPendienteAsync();
        if (info == null) return;

        _dialogoActualizacionAbierto = true;
        try
        {
            var dlg = new UpdateDialog(svc!, info);
            dlg.ShowDialog();
        }
        finally
        {
            _dialogoActualizacionAbierto = false;
        }
    }

    /// <summary>
    /// Consulta si hay una actualización pendiente en el servidor. No muestra ningún diálogo —
    /// el caller decide qué hacer con el resultado (mostrar el diálogo automático al iniciar,
    /// o simplemente resaltar un botón en MainWindow).
    /// </summary>
    public async Task<(UpdateService? Svc, UpdateInfo? Info)> ObtenerActualizacionPendienteAsync()
    {
        try
        {
            if (_config == null) return (null, null);

            var serverPath     = _config["Actualizaciones:ServidorPath"] ?? "";
            // Version real leida del propio ensamblado (ElectroMar.dll), NO de
            // appsettings.json — ese archivo dejo de descargarse en cada actualizacion (para
            // no pisar el InstallId real de cada maquina, ver UpdateService.cs) y quedaba
            // congelado en la version de instalacion original para siempre, haciendo que el
            // updater ofreciera la misma actualizacion en cada apertura aunque ya estuviera
            // aplicada — bug real reportado: "siempre se actualiza". FileVersion se graba en
            // el .dll en cada publish (ver publicar.ps1, -p:FileVersion=$Version) y SI viaja
            // con la descarga real de la actualizacion.
            var versionEnsamblado = System.Diagnostics.FileVersionInfo
                .GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;
            var versionActual  = versionEnsamblado ?? _config["App:Version"] ?? "1.0.0";
            var installId      = _config["Actualizaciones:InstallId"] ?? "";

            if (string.IsNullOrWhiteSpace(serverPath)) return (null, null);

            var svc  = new UpdateService(serverPath, versionActual, installId);
            var info = await svc.VerificarActualizacionAsync();
            return (svc, info);
        }
        catch
        {
            // Nunca bloquear el inicio ni la UI por un error del updater
            return (null, null);
        }
    }
}
