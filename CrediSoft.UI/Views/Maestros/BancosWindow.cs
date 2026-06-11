using CrediSoft.Core.Models;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace CrediSoft.UI.Views.Maestros;

public class BancosWindow : MaestroSimpleWindow
{
    private readonly IMaestrosBancoRepository _repo;
    public BancosWindow()
    {
        Title = "Bancos";
        _repo = App.Services.GetRequiredService<IMaestrosBancoRepository>();
        Loaded += async (_, _) => await Refrescar();
    }
    protected override async Task<IEnumerable<MaestroItem>> CargarDatosAsync(string? f = null) =>
        await _repo.ListarAsync(f);
    protected override async Task GuardarAsync(string c, string n) => await _repo.InsertarAsync(c, n);
    protected override async Task ActualizarAsync(int id, string c, string n) => await _repo.ActualizarAsync(id, c, n);
    protected override async Task EliminarAsync(int id) => await _repo.EliminarAsync(id);
}

public class CategoriasWindow : MaestroSimpleWindow
{
    private readonly IMaestrosCategoriaRepository _repo;
    public CategoriasWindow()
    {
        Title = "Categorías";
        _repo = App.Services.GetRequiredService<IMaestrosCategoriaRepository>();
        Loaded += async (_, _) => await Refrescar();
    }
    protected override async Task<IEnumerable<MaestroItem>> CargarDatosAsync(string? f = null) => await _repo.ListarAsync(f);
    protected override async Task GuardarAsync(string c, string n) => await _repo.InsertarAsync(c, n);
    protected override async Task ActualizarAsync(int id, string c, string n) => await _repo.ActualizarAsync(id, c, n);
    protected override async Task EliminarAsync(int id) => await _repo.EliminarAsync(id);
}

public class MarcasWindow : MaestroSimpleWindow
{
    private readonly IMaestrosMarcaRepository _repo;
    public MarcasWindow()
    {
        Title = "Marcas";
        _repo = App.Services.GetRequiredService<IMaestrosMarcaRepository>();
        Loaded += async (_, _) => await Refrescar();
    }
    protected override async Task<IEnumerable<MaestroItem>> CargarDatosAsync(string? f = null) => await _repo.ListarAsync(f);
    protected override async Task GuardarAsync(string c, string n) => await _repo.InsertarAsync(c, n);
    protected override async Task ActualizarAsync(int id, string c, string n) => await _repo.ActualizarAsync(id, c, n);
    protected override async Task EliminarAsync(int id) => await _repo.EliminarAsync(id);
}

public class MedidasWindow : MaestroSimpleWindow
{
    public MedidasWindow() { Title = "Unidades de Medida"; Loaded += async (_, _) => await Refrescar(); }
    protected override bool RequiereAdminParaEliminar => true;
    protected override Task<IEnumerable<MaestroItem>> CargarDatosAsync(string? f = null) => GenericList("MEDIDAS", "ID", "CODIGO", "UM", f);
    protected override Task GuardarAsync(string c, string n) => GenericInsert("MEDIDAS", "CODIGO", "UM", c, n);
    protected override Task ActualizarAsync(int id, string c, string n) => GenericUpdate("MEDIDAS", "ID", "CODIGO", "UM", id, c, n);
    protected override Task EliminarAsync(int id) => GenericDelete("MEDIDAS", "ID", id);
}

public class ProcedenciasWindow : MaestroSimpleWindow
{
    public ProcedenciasWindow() { Title = "Procedencias / Países"; Loaded += async (_, _) => await Refrescar(); }
    protected override Task<IEnumerable<MaestroItem>> CargarDatosAsync(string? f = null) => GenericList("PAISES", "ID", "CODIGO", "PAIS", f);
    protected override Task GuardarAsync(string c, string n) => GenericInsert("PAISES", "CODIGO", "PAIS", c, n);
    protected override Task ActualizarAsync(int id, string c, string n) => GenericUpdate("PAISES", "ID", "CODIGO", "PAIS", id, c, n);
    protected override Task EliminarAsync(int id) => GenericDelete("PAISES", "ID", id);
}

public class SeccionesWindow : MaestroSimpleWindow
{
    public SeccionesWindow() { Title = "Secciones"; Loaded += async (_, _) => await Refrescar(); }
    protected override Task<IEnumerable<MaestroItem>> CargarDatosAsync(string? f = null) => GenericList("SECCIONES", "IDSECC", "CODIGO", "SECCION", f);
    protected override Task GuardarAsync(string c, string n) => GenericInsert("SECCIONES", "CODIGO", "SECCION", c, n);
    protected override Task ActualizarAsync(int id, string c, string n) => GenericUpdate("SECCIONES", "IDSECC", "CODIGO", "SECCION", id, c, n);
    protected override Task EliminarAsync(int id) => GenericDelete("SECCIONES", "IDSECC", id);
}
