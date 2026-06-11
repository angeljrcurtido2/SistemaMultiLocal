using CrediSoft.Core.Models;

namespace CrediSoft.Core.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> BuscarPorCodigoAsync(string codigo);
    Task<IEnumerable<Usuario>> ListarTodosAsync();
    Task<bool> GuardarAsync(Usuario usuario);
    Task<bool> ActualizarAsync(Usuario usuario);
    Task<bool> EliminarAsync(int idUsuario);
}

public interface ILocalRepository
{
    Task<Local?> ObtenerPorIdAsync(int id);
    Task<IEnumerable<Local>> ListarTodosAsync();
    Task<bool> GuardarAsync(Local local);
    Task<bool> ActualizarAsync(Local local);
    Task<bool> EliminarAsync(int id);
}
