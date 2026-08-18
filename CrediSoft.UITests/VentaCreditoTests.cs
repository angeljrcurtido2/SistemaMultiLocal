using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace CrediSoft.UITests;

/// <summary>
/// Tests 2–7 — Flujo completo de Venta a Crédito:
///   T02 Nueva solicitud → ESTADO = NUEVO
///   T03 Sin local → advertencia al pulsar Código
///   T04 Seleccionar local → buscador artículos abre
///   T05 Buscar artículo, ingresar al carrito
///   T06 Eliminar artículo del carrito
///   T07 Guardar solicitud completa
///   T08 Visor solicitudes → solicitud guardada aparece en estado Pendiente
/// </summary>
[Collection("App")]
public class VentaCreditoTests
{
    private readonly AppFixture _fx;
    public VentaCreditoTests(AppFixture fx) => _fx = fx;

    // ── T02 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void T02_NuevaSolicitud_EstadoMuestraNUEVO()
    {
        _fx.Login();
        var win = _fx.AbrirNuevaSolicitud();

        var txtEstado = win.FindFirstDescendant(c => c.ByAutomationId("TxtEstado"));
        Assert.NotNull(txtEstado);

        var texto = txtEstado.AsLabel().Text ?? txtEstado.Name ?? "";
        Assert.Equal("NUEVO", texto);

        win.Close();
        Thread.Sleep(800);
    }

    // ── T03 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void T03_SinLocal_BtnCodigo_MuestraAdvertencia()
    {
        _fx.Login();
        var win = _fx.AbrirNuevaSolicitud();

        // Pulsar "Código" sin haber seleccionado local
        AppFixture.ClickButton(win, "BtnCodigo");

        // Debe aparecer un MessageBox con "Local requerido"
        var dlg = _fx.TryFindWindow("Local requerido", 3000)
               ?? _fx.TryFindWindow("requerido", 2000);
        Assert.NotNull(dlg);
        _fx.CloseDialog();

        win.Close();
        Thread.Sleep(800);
    }

    // ── T04 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void T04_ConLocal_BtnCodigo_AbreBuscadorArticulo()
    {
        _fx.Login();
        var win = _fx.AbrirNuevaSolicitud();

        SeleccionarLocal(win);

        // Verificar que TxtLocal tiene valor
        var txtLocal = AppFixture.ReadText(win, "TxtLocal");
        Assert.False(string.IsNullOrWhiteSpace(txtLocal), "TxtLocal debería tener un local seleccionado");

        // Pulsar Código — debe abrir buscador de artículos, NO un MessageBox
        AppFixture.ClickButton(win, "BtnCodigo");
        Thread.Sleep(1500);

        var dlgArts = _fx.TryFindWindow("Artículo", 5000)
                   ?? _fx.TryFindWindow("Buscar", 3000);
        Assert.NotNull(dlgArts);

        dlgArts.Close();
        win.Close();
        Thread.Sleep(800);
    }

    // ── T05 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void T05_SeleccionarArticulo_SeAgregaAlCarrito()
    {
        _fx.Login();
        var win = _fx.AbrirNuevaSolicitud();

        SeleccionarLocal(win);

        // Abrir buscador de artículos
        AppFixture.ClickButton(win, "BtnCodigo");
        Thread.Sleep(1500);

        var dlgArts = _fx.TryFindWindow("Artículo", 5000)
                   ?? _fx.TryFindWindow("Buscar", 3000);
        Assert.NotNull(dlgArts);

        // Seleccionar el primer artículo de la lista (doble clic o Enter)
        var grid = dlgArts.FindFirstDescendant(c => c.ByControlType(FlaUI.Core.Definitions.ControlType.DataGrid));
        if (grid != null)
        {
            // Enfocar la grilla y seleccionar la primera fila con flecha abajo + Enter
            grid.Click();
            Thread.Sleep(200);
            Keyboard.Press(VirtualKeyShort.DOWN);
            Thread.Sleep(100);
            Keyboard.Press(VirtualKeyShort.RETURN);
        }
        else
        {
            // Fallback: Enter en la ventana del buscador
            dlgArts.Focus();
            Keyboard.Press(VirtualKeyShort.RETURN);
        }
        Thread.Sleep(1000);

        // El artículo debe haberse cargado en TxtCodigoArticulo
        var codigo = AppFixture.ReadText(win, "TxtCodigoArticulo");
        Assert.False(string.IsNullOrWhiteSpace(codigo), "TxtCodigoArticulo debe tener el código del artículo seleccionado");

        // Verificar que no hay notación científica
        Assert.DoesNotContain("E+", codigo, StringComparison.OrdinalIgnoreCase);

        // Pulsar "▶ Ingresar" para agregar al carrito
        AppFixture.ClickButton(win, "BtnIngresarArticulo");
        Thread.Sleep(500);

        // El carrito (GridDetalle) debe tener al menos 1 fila
        var gridDetalle = win.FindFirstDescendant(c => c.ByAutomationId("GridDetalle"));
        Assert.NotNull(gridDetalle);
        var filas = gridDetalle.FindAllDescendants(c => c.ByControlType(FlaUI.Core.Definitions.ControlType.DataItem));
        Assert.True(filas.Length >= 1, "GridDetalle debe tener al menos 1 artículo");

        // El total debe ser > 0
        var total = AppFixture.ReadText(win, "TxtTotal");
        Assert.False(string.IsNullOrWhiteSpace(total));
        Assert.NotEqual("0", total.Trim());

        win.Close();
        Thread.Sleep(800);
    }

    // ── T06 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void T06_EliminarArticulo_DelCarrito()
    {
        _fx.Login();
        var win = _fx.AbrirNuevaSolicitud();

        SeleccionarLocal(win);
        AgregarPrimerArticulo(win);

        // Verificar que el carrito tiene 1 artículo
        var gridDetalle = win.FindFirstDescendant(c => c.ByAutomationId("GridDetalle"));
        Assert.NotNull(gridDetalle);

        var filasAntes = gridDetalle.FindAllDescendants(c => c.ByControlType(FlaUI.Core.Definitions.ControlType.DataItem));
        Assert.True(filasAntes.Length >= 1, "Debe haber al menos 1 artículo antes de eliminar");

        // Seleccionar la primera fila y pulsar BtnEliminar
        gridDetalle.Click();
        Thread.Sleep(200);
        Keyboard.Press(VirtualKeyShort.DOWN);
        Thread.Sleep(200);

        AppFixture.ClickButton(win, "BtnEliminar");
        Thread.Sleep(500);

        // El carrito debe estar vacío
        var filasDespues = gridDetalle.FindAllDescendants(c => c.ByControlType(FlaUI.Core.Definitions.ControlType.DataItem));
        Assert.True(filasDespues.Length == 0, "El carrito debe estar vacío después de eliminar");

        win.Close();
        Thread.Sleep(800);
    }

    // ── T07 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void T07_GuardarSolicitud_Completa()
    {
        _fx.Login();
        var win = _fx.AbrirNuevaSolicitud();

        // Leer el número de solicitud asignado
        var nroSolicitud = AppFixture.ReadText(win, "TxtNroSolicitud");
        Assert.False(string.IsNullOrWhiteSpace(nroSolicitud));

        SeleccionarLocal(win);
        SeleccionarCliente(win);
        AgregarPrimerArticulo(win);

        // Guardar
        AppFixture.ClickButton(win, "BtnGuardar");
        Thread.Sleep(800);

        // Confirmar el MessageBox "¿Guardar solicitud de crédito para ...?"
        var dlgConfirm = _fx.TryFindWindow("Confirmar solicitud", 4000);
        if (dlgConfirm != null)
        {
            var btnSi = dlgConfirm.FindFirstDescendant(c => c.ByName("Sí"))
                     ?? dlgConfirm.FindFirstDescendant(c => c.ByName("Si"));
            btnSi?.AsButton().Click();
            Thread.Sleep(2000);
        }

        // Debe aparecer MessageBox "Solicitud enviada" o "guardada"
        var dlgOk = _fx.TryFindWindow("Solicitud enviada", 5000)
                 ?? _fx.TryFindWindow("guardada", 4000);
        Assert.NotNull(dlgOk);
        _fx.CloseDialog();
        Thread.Sleep(500);

        // Tras guardar, el form se resetea: ESTADO debe volver a NUEVO y Nro cambia
        var nuevoNro = AppFixture.ReadText(win, "TxtNroSolicitud");
        Assert.NotEqual(nroSolicitud, nuevoNro);

        win.Close();
        Thread.Sleep(800);
    }

    // ── T08 ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void T08_VisorSolicitudes_MuestraSolicitudGuardada()
    {
        _fx.Login();

        // Primero guardar una solicitud (depende de T07 haberse ejecutado, pero creamos una fresca)
        var winForm = _fx.AbrirNuevaSolicitud();
        SeleccionarLocal(winForm);
        SeleccionarCliente(winForm);
        AgregarPrimerArticulo(winForm);
        AppFixture.ClickButton(winForm, "BtnGuardar");
        Thread.Sleep(600);
        var dlgConfirm = _fx.TryFindWindow("Confirmar", 3000);
        if (dlgConfirm != null)
        {
            var btnSi = dlgConfirm.FindFirstDescendant(c => c.ByName("Sí"))
                     ?? dlgConfirm.FindFirstDescendant(c => c.ByName("Si"));
            btnSi?.AsButton().Click();
            Thread.Sleep(2000);
        }
        _fx.CloseDialog(timeoutMs: 3000);
        winForm.Close();
        Thread.Sleep(800);

        // Abrir el Visor
        var visor = _fx.AbrirVisorSolicitudes();

        // Debe haber al menos 1 fila en el DataGrid del visor
        var grid = visor.FindFirstDescendant(c => c.ByControlType(FlaUI.Core.Definitions.ControlType.DataGrid));
        Assert.NotNull(grid);
        var filas = grid.FindAllDescendants(c => c.ByControlType(FlaUI.Core.Definitions.ControlType.DataItem));
        Assert.True(filas.Length >= 1, "El visor debe mostrar al menos 1 solicitud pendiente");

        visor.Close();
        Thread.Sleep(800);
    }

    // ─── Helpers privados ────────────────────────────────────────────────────────

    private void SeleccionarLocal(Window win)
    {
        AppFixture.ClickButton(win, "BtnLocal");
        Thread.Sleep(1200);

        // Modal de locales: seleccionar el primero
        var dlg = _fx.TryFindWindow("Local", 4000);
        if (dlg != null)
        {
            var lb = dlg.FindFirstDescendant(c => c.ByControlType(FlaUI.Core.Definitions.ControlType.List));
            if (lb != null)
            {
                lb.Click();
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.DOWN);
                Thread.Sleep(150);
            }
            // Pulsar Aceptar
            var btnOk = dlg.FindFirstDescendant(c => c.ByName("Aceptar"));
            if (btnOk != null)
                btnOk.AsButton().Click();
            else
                Keyboard.Press(VirtualKeyShort.RETURN);
            Thread.Sleep(600);
        }
    }

    private void SeleccionarCliente(Window win)
    {
        AppFixture.ClickButton(win, "BtnBuscarCliente");
        Thread.Sleep(1500);

        var dlg = _fx.TryFindWindow("liente", 5000);
        if (dlg != null)
        {
            var grid = dlg.FindFirstDescendant(c => c.ByControlType(FlaUI.Core.Definitions.ControlType.DataGrid));
            if (grid != null)
            {
                grid.Click();
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.DOWN);
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.RETURN);
            }
            else
            {
                dlg.Focus();
                Keyboard.Press(VirtualKeyShort.RETURN);
            }
            Thread.Sleep(800);
        }
    }

    private void AgregarPrimerArticulo(Window win)
    {
        AppFixture.ClickButton(win, "BtnCodigo");
        Thread.Sleep(1500);

        var dlgArts = _fx.TryFindWindow("Artículo", 5000)
                   ?? _fx.TryFindWindow("Buscar", 3000);
        if (dlgArts != null)
        {
            var grid = dlgArts.FindFirstDescendant(c => c.ByControlType(FlaUI.Core.Definitions.ControlType.DataGrid));
            if (grid != null)
            {
                grid.Click();
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.DOWN);
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.RETURN);
            }
            else
            {
                dlgArts.Focus();
                Keyboard.Press(VirtualKeyShort.RETURN);
            }
            Thread.Sleep(800);
        }

        AppFixture.ClickButton(win, "BtnIngresarArticulo");
        Thread.Sleep(400);
    }
}
