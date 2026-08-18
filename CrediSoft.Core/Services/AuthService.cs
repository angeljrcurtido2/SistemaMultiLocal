using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;

namespace CrediSoft.Core.Services;

public class AuthService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ILocalRepository _locales;
    private readonly ISessionService _session;

    public AuthService(IUsuarioRepository usuarios, ILocalRepository locales, ISessionService session)
    {
        _usuarios = usuarios;
        _locales = locales;
        _session = session;
    }

    // El local ya no se recibe como parametro externo: se toma siempre de
    // USUARIOS.LOCAL_USUARIO (via usuario.LocalUsuario), tal como esta asignado en la base
    // de datos. Antes la UI del login permitia elegir manualmente el local con un selector,
    // lo que dejaba abierta la posibilidad de loguearse con un local distinto al asignado;
    // ahora queda fijo por el dato real de la BD, sin intervencion de la UI.
    public async Task<LoginResultado> LoginAsync(string codigo, string contrasena)
    {
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(contrasena))
            return LoginResultado.Fallo("Ingrese código y contraseña.");

        var usuario = await _usuarios.BuscarPorCodigoAsync(codigo);
        if (usuario == null)
            return LoginResultado.Fallo("Usuario no encontrado.");

        // Comparación directa (texto plano como en el sistema original)
        if (usuario.ContrasenaUsuario != contrasena)
            return LoginResultado.Fallo("Contraseña incorrecta.");

        var local = await _locales.ObtenerPorIdAsync(usuario.LocalUsuario);
        if (local == null)
            return LoginResultado.Fallo("El local asignado a este usuario no existe. Contacte al administrador.");

        _session.IniciarSesion(usuario, local);
        return LoginResultado.Ok(usuario, local);
    }
}

public class LoginResultado
{
    public bool Exitoso { get; private set; }
    public string MensajeError { get; private set; } = string.Empty;
    public Usuario? Usuario { get; private set; }
    public Local? Local { get; private set; }

    public static LoginResultado Ok(Usuario u, Local l) =>
        new() { Exitoso = true, Usuario = u, Local = l };

    public static LoginResultado Fallo(string msg) =>
        new() { Exitoso = false, MensajeError = msg };
}
