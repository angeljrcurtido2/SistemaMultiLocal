namespace CrediSoft.UITests;

/// <summary>
/// No es un test de verificación — es un recorrido guiado que toma una captura de
/// pantalla real en cada paso del flujo de Solicitud de Crédito, para armar la
/// guía visual de uso. Corre solo, no en la suite normal de CI.
///
/// Nota de diagnóstico (dejar documentado): App.GetAllTopLevelWindows() de FlaUI NO
/// detecta las ventanas secundarias de esta app (ni "Solicitud / Venta a Crédito" ni
/// los modales como "Seleccionar Local"), aunque están abiertas y visibles — se
/// confirmó con EnumWindows (Win32 puro) que si existen y pertenecen al mismo
/// proceso. Por eso todo este archivo navega ventanas con
/// AppFixture.WaitForWindowWin32(...) en vez de TryFindWindow(...).
/// </summary>
[Collection("App")]
public class CapturaGuiaTests
{
    private readonly AppFixture _fx;
    private static readonly string OutDir = System.IO.Path.Combine(
        AppContext.BaseDirectory, "capturas_guia");

    public CapturaGuiaTests(AppFixture fx) => _fx = fx;

    [Fact]
    public void RecorrerFlujoYCapturar()
    {
        var n = 0;
        void Foto(string nombre, Window? w = null) => _fx.Capture(OutDir, $"{++n:D2}_{nombre}", w);

        _fx.Login();
        Foto("panel_control");

        _fx.AbrirNuevaSolicitud();
        Thread.Sleep(800);
        var win = _fx.WaitForWindowWin32("Solicitud / Venta a Cr", 6000);
        Foto("solicitud_vacia", win);

        // Local
        AppFixture.ClickButton(win, "BtnSeleccionarLocal");
        Thread.Sleep(1500);
        var dlgLocal = _fx.WaitForWindowWin32("Seleccionar Local", 6000);
        Foto("modal_local", dlgLocal);
        var lbLocal = dlgLocal.FindFirstDescendant(c => c.ByControlType(ControlType.List));
        lbLocal?.Click();
        Thread.Sleep(200);
        Keyboard.Press(VirtualKeyShort.DOWN);
        Thread.Sleep(200);
        var btnSelLocal = dlgLocal.FindFirstDescendant(c => c.ByName("Seleccionar"));
        if (btnSelLocal != null) btnSelLocal.AsButton().Click(); else Keyboard.Press(VirtualKeyShort.RETURN);
        Thread.Sleep(1000);
        // Refrescar la referencia: tras cerrarse el modal, el AutomationElement viejo
        // de 'win' puede quedar "stale" (encuentra la ventana pero no sus hijos).
        win = _fx.WaitForWindowWin32("Solicitud / Venta a Cr", 4000);
        Foto("local_elegido", win);

        // Diagnóstico final: buscar por AutomationId, por Name parcial, y contar
        // TODOS los descendientes (de cualquier tipo) para ver si el árbol está vacío.
        var todosLosDescendientes = win.FindAllDescendants();
        Console.WriteLine($"DIAG: win tiene {todosLosDescendientes.Length} descendientes en total.");
        var porId = win.FindFirstDescendant(c => c.ByAutomationId("BtnBuscarCliente"));
        Console.WriteLine($"DIAG: por AutomationId 'BtnBuscarCliente' -> {(porId == null ? "NULL" : "encontrado")}");
        var porNombreParcial = todosLosDescendientes.FirstOrDefault(e =>
        {
            try { return (e.Name ?? "").Contains("Buscar cliente", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        });
        Console.WriteLine($"DIAG: por Name conteniendo 'Buscar cliente' -> {(porNombreParcial == null ? "NULL" : "encontrado: " + porNombreParcial.ControlType)}");

        // Cliente
        AppFixture.ClickButton(win, "BtnBuscarCliente");
        Thread.Sleep(1200);
        var dlgCliente = _fx.FindWindowByTitleWin32("liente");
        if (dlgCliente != null)
        {
            Foto("modal_buscar_cliente", dlgCliente);
            var grid = dlgCliente.FindFirstDescendant(c => c.ByControlType(ControlType.DataGrid));
            if (grid != null)
            {
                grid.Click();
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.DOWN);
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.RETURN);
            }
            Thread.Sleep(800);
            win = _fx.WaitForWindowWin32("Solicitud / Venta a Cr", 4000); // refrescar tras cerrar modal
        }
        Foto("pestana_cliente_cargada", win);

        // Garante
        var tabGarante = win.FindFirstDescendant(c => c.ByName("Garante"));
        tabGarante?.Click();
        Thread.Sleep(500);
        Foto("pestana_garante", win);

        // Referencias
        var tabRef = win.FindFirstDescendant(c => c.ByName("Referencias"));
        tabRef?.Click();
        Thread.Sleep(500);
        Foto("pestana_referencias", win);

        // Mercaderías
        var tabMerc = win.FindFirstDescendant(c => c.ByName("Mercaderías"));
        tabMerc?.Click();
        Thread.Sleep(500);
        Foto("pestana_mercaderias_vacia", win);

        AppFixture.ClickButton(win, "BtnCodigo");
        Thread.Sleep(1200);
        var dlgArt = _fx.FindWindowByTitleWin32("rtículo") ?? _fx.FindWindowByTitleWin32("Buscar");
        if (dlgArt != null)
        {
            Foto("modal_buscar_articulo", dlgArt);
            var grid = dlgArt.FindFirstDescendant(c => c.ByControlType(ControlType.DataGrid));
            if (grid != null)
            {
                grid.Click();
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.DOWN);
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.RETURN);
            }
            Thread.Sleep(600);
            win = _fx.WaitForWindowWin32("Solicitud / Venta a Cr", 4000); // refrescar tras cerrar modal
        }
        AppFixture.ClickButton(win, "BtnIngresarArticulo");
        Thread.Sleep(500);
        Foto("articulo_agregado", win);

        // Vendedor
        AppFixture.ClickButton(win, "BtnBuscarVendedor");
        Thread.Sleep(1200);
        var dlgVend = _fx.FindWindowByTitleWin32("endedor");
        if (dlgVend != null)
        {
            Foto("modal_vendedor", dlgVend);
            var grid = dlgVend.FindFirstDescendant(c => c.ByControlType(ControlType.DataGrid))
                    ?? dlgVend.FindFirstDescendant(c => c.ByControlType(ControlType.List));
            if (grid != null)
            {
                grid.Click();
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.DOWN);
                Thread.Sleep(150);
            }
            var btnSel = dlgVend.FindFirstDescendant(c => c.ByName("Seleccionar"));
            if (btnSel != null) btnSel.AsButton().Click(); else Keyboard.Press(VirtualKeyShort.RETURN);
            Thread.Sleep(800);
            win = _fx.WaitForWindowWin32("Solicitud / Venta a Cr", 4000); // refrescar tras cerrar modal
        }
        Foto("listo_para_guardar", win);

        // NOTA: se corta ACÁ a propósito — no se pulsa Guardar para no crear
        // una solicitud de prueba real en la base.

        win.Close();
        Thread.Sleep(500);

        Console.WriteLine($"Capturas guardadas en: {OutDir}");
    }
}
