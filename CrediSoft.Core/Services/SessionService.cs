using CrediSoft.Core.Models;

namespace CrediSoft.Core.Services;

public interface ISessionService
{
    Usuario? UsuarioActual { get; }
    Local? LocalActual { get; }
    bool EstaLogueado { get; }
    void IniciarSesion(Usuario usuario, Local local);
    void CerrarSesion();
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
}
