using CrediSoft.Core.Models;

namespace CrediSoft.Core.Services;

public interface ISessionService
{
    Usuario? UsuarioActual { get; }
    Local? LocalActual { get; }
    bool EstaLogueado { get; }
    void IniciarSesion(Usuario usuario, Local local);
    void CerrarSesion();
    void ActualizarLocalActual(Local local);
}

public class SessionService : ISessionService
{
    private static SessionService? _instance;
    public static SessionService Instance => _instance ??= new SessionService();

    public Usuario? UsuarioActual { get; private set; }
    public Local? LocalActual { get; private set; }
    public bool EstaLogueado => UsuarioActual != null;

    private SessionService() { }

    public void IniciarSesion(Usuario usuario, Local local)
    {
        UsuarioActual = usuario;
        LocalActual = local;
    }

    public void CerrarSesion()
    {
        UsuarioActual = null;
        LocalActual = null;
    }

    // Refresca los datos del local en sesión (ej. teléfono recién editado en Locales /
    // Sucursales) sin re-loguear — solo aplica si es el mismo local que ya estaba en sesión.
    public void ActualizarLocalActual(Local local)
    {
        if (LocalActual != null && LocalActual.IdLocal == local.IdLocal)
            LocalActual = local;
    }
}
