using CrediSoft.Core.Services;
using CrediSoft.Core.Interfaces;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace CrediSoft.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Captura todas las excepciones no manejadas para diagnóstico
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            var msg = ex.ExceptionObject?.ToString() ?? "Error desconocido";
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), msg);
            MessageBox.Show("Error crítico:\n\n" + msg[..Math.Min(msg.Length, 800)], "CrediSoft - Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (s, ex) =>
        {
            var msg = ex.Exception?.ToString() ?? "Error desconocido";
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash_ui.log"), msg);
            MessageBox.Show("Error de UI:\n\n" + msg[..Math.Min(msg.Length, 800)], "CrediSoft - Error",
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

        var basePath = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        var connStr = config.GetConnectionString("Default")!;

        // Infraestructura
        services.AddSingleton<IDbConnectionFactory>(new SqlServerConnectionFactory(connStr));

        // Repositorios
        services.AddTransient<IUsuarioRepository, UsuarioRepository>();
        services.AddTransient<ILocalRepository, LocalRepository>();
        services.AddTransient<IClienteRepository, ClienteRepository>();
        services.AddTransient<IArticuloRepository, ArticuloRepository>();
        services.AddTransient<IVentaRepository, VentaRepository>();
        services.AddTransient<ICuotaRepository, CuotaRepository>();
        services.AddTransient<ICajaRepository, CajaRepository>();
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
    }
}
