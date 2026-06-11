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

    public async Task<LoginResultado> LoginAsync(string codigo, string contrasena, int idLocal)
    {
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(contrasena))
            return LoginResultado.Fallo("Ingrese código y contraseña.");

        var usuario = await _usuarios.BuscarPorCodigoAsync(codigo);
        if (usuario == null)
            return LoginResultado.Fallo("Usuario no encontrado.");

        // Comparación directa (texto plano como en el sistema original)
        if (usuario.ContrasenaUsuario != contrasena)
            return LoginResultado.Fallo("Contraseña incorrecta.");

        if (usuario.LocalUsuario != idLocal && !usuario.EsAdministrador)
            return LoginResultado.Fallo("El local no corresponde al usuario.");

        var local = await _locales.ObtenerPorIdAsync(idLocal);
        if (local == null)
            return LoginResultado.Fallo("Local no encontrado.");

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
