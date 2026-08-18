using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Microsoft.Extensions.Configuration;

namespace CrediSoft.UITests;

/// <summary>
/// Lanza la app una sola vez para toda la suite y expone helpers reutilizables.
/// </summary>
public sealed class AppFixture : IDisposable
{
    public Application    App        { get; }
    public UIA3Automation Auto       { get; }
    public Window         MainWindow { get; private set; } = null!;

    public string Usuario  { get; }
    public string Password { get; }
    public string IdLocal  { get; }

    private bool _loginDone;

    public AppFixture()
    {
        var cfg = new ConfigurationBuilder()
            .AddJsonFile("testsettings.json", optional: false)
            .Build();

        Usuario  = cfg["Usuario"]!;
        Password = cfg["Password"]!;
        IdLocal  = cfg["IdLocal"]!;

        Auto = new UIA3Automation();
        var psi = new System.Diagnostics.ProcessStartInfo(cfg["AppPath"]!, "--profile=Local")
        {
            WorkingDirectory = System.IO.Path.GetDirectoryName(cfg["AppPath"]!)
        };
        App  = Application.Launch(psi);
        MainWindow = App.GetMainWindow(Auto, TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Hace login. Idempotente: solo actúa si no lo hizo antes en esta sesión de tests.
    /// El local ya NO se elige a mano: se autocompleta desde USUARIOS.LOCAL_USUARIO
    /// apenas se escribe un código de usuario válido (ver LoginWindow.xaml, BtnLocal
    /// pasó a ser solo informativo).
    /// </summary>
    public Window Login()
    {
        if (_loginDone) return MainWindow;

        // Escribir usuario
        TypeInto(MainWindow, "TxtUsuario", Usuario);
        Thread.Sleep(400); // deja tiempo a que se autocomplete el local desde la BD

        // PasswordBox: usar ValuePattern vía SendKeys
        var pwBox = MainWindow.FindFirstDescendant(c => c.ByAutomationId("TxtContrasena"))
                    ?? throw new Exception("No se encontró TxtContrasena");
        pwBox.Click();
        Thread.Sleep(100);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(Password);

        ClickButton(MainWindow, "BtnIngresar");

        // Esperar que el MainWindow MDI cargue
        Thread.Sleep(2500);
        MainWindow = App.GetMainWindow(Auto, TimeSpan.FromSeconds(10));
        _loginDone = true;
        return MainWindow;
    }

    /// <summary>
    /// Captura la pantalla completa (o solo la ventana dada, si se pasa) y la guarda
    /// como PNG en la carpeta indicada. Usado para armar la guía visual paso a paso.
    /// </summary>
    public string Capture(string outDir, string stepName, Window? window = null)
    {
        System.IO.Directory.CreateDirectory(outDir);
        var path = System.IO.Path.Combine(outDir, stepName + ".png");
        // No forzar Focus(): robarle el foco a un diálogo modal recién abierto puede
        // hacer que Windows lo cierre (comportamiento típico de ventanas "no-activables"
        // al perder foco). Solo se captura la pantalla tal cual está.
        using var bmp = FlaUI.Core.Capturing.Capture.Screen();
        bmp.ToFile(path);
        return path;
    }

    // ─── Navegación ──────────────────────────────────────────────────────────

    /// <summary>
    /// Abre Movimientos > Solicitudes > Ingresar solicitud.
    /// El módulo se embebe en el TabControl del MainWindow (no es ventana top-level).
    /// Retorna el MainWindow con el tab activo.
    /// </summary>
    public Window AbrirNuevaSolicitud()
    {
        NavegaMenu("Movimientos", "Solicitudes", "Ingresar solicitud");
        Thread.Sleep(2000);
        return MainWindow;
    }

    /// <summary>Abre Movimientos > Solicitudes > Visor solicitudes.</summary>
    public Window AbrirVisorSolicitudes()
    {
        NavegaMenu("Movimientos", "Solicitudes", "Visor solicitudes");
        Thread.Sleep(2500);
        return MainWindow;
    }

    /// <summary>
    /// Navega un menú de 3 niveles.
    /// Cierra cualquier menú abierto primero, luego hace click secuencial
    /// buscando cada nivel en todos los elementos visibles del árbol.
    /// </summary>
    private void NavegaMenu(string raiz, string sub, string item)
    {
        var main = MainWindow;

        // Cerrar cualquier menú abierto antes de empezar
        main.Focus();
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        Thread.Sleep(200);

        // Nivel 1 — ítems con submenú usan ExpandCollapse, no Invoke
        var m1 = main.FindFirstDescendant(c => c.ByName(raiz).And(c.ByControlType(ControlType.MenuItem)))
                 ?? throw new Exception($"Menú raíz no encontrado: '{raiz}'");
        m1.Click();
        Thread.Sleep(600);

        // Nivel 2 — buscar tras expansión del popup (puede ser ventana separada en UIA)
        var m2 = BuscarEnTodosLosElementos(sub, ControlType.MenuItem)
                 ?? throw new Exception($"Submenú no encontrado: '{sub}' (tras abrir '{raiz}')");
        m2.Click();
        Thread.Sleep(600);

        // Nivel 3 — ítem hoja: soporta Invoke O Click
        var m3 = BuscarEnTodosLosElementos(item, ControlType.MenuItem)
                 ?? throw new Exception($"Ítem no encontrado: '{item}' (tras abrir '{sub}')");
        m3.Click();
        Thread.Sleep(300);
    }

    /// <summary>Busca un elemento por nombre y tipo en todas las ventanas top-level y en el MainWindow.</summary>
    private AutomationElement? BuscarEnTodosLosElementos(string name, ControlType tipo)
    {
        // Primero en el MainWindow
        var en = MainWindow.FindFirstDescendant(c => c.ByName(name).And(c.ByControlType(tipo)));
        if (en != null) return en;

        // Luego en otras ventanas top-level (popups de menú WPF)
        foreach (var w in App.GetAllTopLevelWindows(Auto))
        {
            en = w.FindFirstDescendant(c => c.ByName(name).And(c.ByControlType(tipo)));
            if (en != null) return en;
        }
        return null;
    }

    // ─── Helpers de UI ───────────────────────────────────────────────────────

    public static void TypeInto(AutomationElement root, string automationId, string value)
    {
        var el = root.FindFirstDescendant(c => c.ByAutomationId(automationId))
                 ?? throw new Exception($"Elemento no encontrado: AutomationId='{automationId}'");
        el.AsTextBox().Enter(value);
    }

    public static void ClickButton(AutomationElement root, string automationId, int timeoutMs = 4000)
    {
        // El contenido de las pestañas del TabControl (ContentPresenter con
        // ContentSource="SelectedContent") tarda un instante en materializarse en el
        // árbol UIA tras un cambio de layout — reintenta con polling en vez de fallar
        // al primer intento.
        var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        AutomationElement? el = null;
        while (DateTime.Now < deadline)
        {
            el = root.FindFirstDescendant(c => c.ByAutomationId(automationId));
            if (el != null) break;
            Thread.Sleep(200);
        }
        if (el == null) throw new Exception($"Botón no encontrado: AutomationId='{automationId}'");
        el.AsButton().Click();
        Thread.Sleep(200);
    }

    /// <summary>
    /// Lee el texto de un elemento por AutomationId.
    /// Funciona para TextBox (ValuePattern), TextBlock (Name) y Label.
    /// </summary>
    public static string ReadText(AutomationElement root, string automationId)
    {
        var el = root.FindFirstDescendant(c => c.ByAutomationId(automationId))
                 ?? throw new Exception($"Elemento no encontrado: AutomationId='{automationId}'");
        // TextBox tiene ValuePattern; TextBlock solo tiene Name
        try { return el.AsTextBox().Text ?? ""; }
        catch { return el.Name ?? ""; }
    }

    public Window WaitForWindow(string titleContains, int timeoutMs = 6000)
    {
        var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < deadline)
        {
            // App.GetAllTopLevelWindows() a veces no ve ventanas ShowInTaskbar="False"
            // recién creadas (ventanas modales propias de esta app) — se busca también
            // por el Desktop completo de UIA, que sí las enumera de forma confiable.
            var wins = App.GetAllTopLevelWindows(Auto);
            var match = wins.FirstOrDefault(w =>
                w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            var desktop = Auto.GetDesktop();
            var fromDesktop = desktop.FindAllChildren(c => c.ByControlType(ControlType.Window));
            var matchDesktop = fromDesktop.FirstOrDefault(w =>
                (w.Name ?? "").Contains(titleContains, StringComparison.OrdinalIgnoreCase));
            if (matchDesktop != null) return matchDesktop.AsWindow();

            Thread.Sleep(200);
        }
        throw new Exception($"Ventana con título que contiene '{titleContains}' no apareció en {timeoutMs}ms");
    }

    public Window? TryFindWindow(string titleContains, int timeoutMs = 3000)
    {
        try { return WaitForWindow(titleContains, timeoutMs); }
        catch { return null; }
    }

    /// <summary>
    /// Enumera, vía Win32 puro (EnumWindows), las ventanas visibles del proceso de la
    /// app cuyo título contiene el texto dado. Bypass de App.GetAllTopLevelWindows(),
    /// que en este proceso no refleja ventanas secundarias (ej. "Solicitud / Venta a
    /// Crédito" o modales como "Seleccionar Local") aunque están abiertas y visibles —
    /// EnumWindows sí las ve siempre, de forma confiable.
    /// </summary>
    public Window? FindWindowByTitleWin32(string titleContains)
    {
        var pid = (uint)App.ProcessId;
        Window? found = null;
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var winPid);
            if (winPid != pid || !NativeMethods.IsWindowVisible(hwnd)) return true;
            var el = Auto.FromHandle(hwnd);
            if (el != null && (el.Name ?? "").Contains(titleContains, StringComparison.OrdinalIgnoreCase))
            {
                found = el.AsWindow();
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public Window WaitForWindowWin32(string titleContains, int timeoutMs = 6000)
    {
        var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < deadline)
        {
            var w = FindWindowByTitleWin32(titleContains);
            if (w != null) return w;
            Thread.Sleep(200);
        }
        throw new Exception($"Ventana (Win32) con título que contiene '{titleContains}' no apareció en {timeoutMs}ms");
    }

    /// <summary>Cierra un MessageBox buscando el botón Aceptar/OK/Sí.</summary>
    public void CloseDialog(string? titleContains = null, int timeoutMs = 3000)
    {
        Window? dlg = null;
        if (titleContains != null)
            dlg = TryFindWindow(titleContains, timeoutMs);
        else
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < deadline)
            {
                var wins = App.GetAllTopLevelWindows(Auto);
                dlg = wins.FirstOrDefault(w => w != MainWindow);
                if (dlg != null) break;
                Thread.Sleep(200);
            }
        }
        if (dlg == null) return;

        var btn = dlg.FindFirstDescendant(c => c.ByName("Aceptar"))
               ?? dlg.FindFirstDescendant(c => c.ByName("OK"))
               ?? dlg.FindFirstDescendant(c => c.ByName("Sí"));
        btn?.AsButton().Click();
        Thread.Sleep(300);
    }

    public void Dispose()
    {
        try { App.Close(); } catch { /* ignorar */ }
        Auto.Dispose();
    }
}

internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
}
