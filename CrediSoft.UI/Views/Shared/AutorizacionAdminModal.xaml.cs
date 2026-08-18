using Dapper;
using CrediSoft.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Shared;

public partial class AutorizacionAdminModal : Window
{
    private readonly string _permiso;

    public AutorizacionAdminModal(string accion, string permiso = "PERMISO_ELIMINAR_FACTURA")
    {
        InitializeComponent();
        TxtAccion.Text = accion;
        _permiso = permiso;
        Loaded += (_, _) => TxtUsuario.Focus();
    }

    private async void OnAutorizar(object s, RoutedEventArgs e)
    {
        var usuario = TxtUsuario.Text.Trim();
        var clave   = TxtPassword.Password;

        if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(clave))
        {
            MostrarError("Ingresá usuario y contraseña.");
            return;
        }

        try
        {
            using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();

            // Acepta nombre de usuario O código de usuario (CODIGO_USUARIO, lo que el cajero
            // conoce y usa a diario, ej. "67") — NO el ID_USUARIO interno (la clave de tabla,
            // ej. 2 para ese mismo código). Mismo bug ya documentado y corregido en
            // PermisoUsuariosModal: comparar contra ID_USUARIO hacía que tipear "67" nunca
            // encontrara a Aida Acosta (ID_USUARIO=2, CODIGO_USUARIO='67').
            var esCodigo = int.TryParse(usuario, out _);
            var sql = esCodigo
                ? $@"SELECT COUNT(1) FROM USUARIOS
                     WHERE CODIGO_USUARIO = @Usuario
                       AND CONTRASEÑA_USUARIO = @Clave
                       AND {_permiso} = 'SI'"
                : $@"SELECT COUNT(1) FROM USUARIOS
                     WHERE NOMBRE_USUARIO = @Usuario
                       AND CONTRASEÑA_USUARIO = @Clave
                       AND {_permiso} = 'SI'";

            var ok = await conn.ExecuteScalarAsync<int>(sql,
                new { Usuario = usuario, Clave = clave });

            if (ok > 0)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MostrarError("Usuario o contraseña incorrectos,\no sin permiso para esta acción.");
                TxtPassword.Password = "";
                TxtPassword.Focus();
            }
        }
        catch (Exception ex)
        {
            MostrarError($"Error al verificar: {ex.Message}");
        }
    }

    private void MostrarError(string msg)
    {
        TxtError.Text       = msg;
        TxtError.Visibility = Visibility.Visible;
    }

    private void OnKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnAutorizar(s, new RoutedEventArgs());
        if (e.Key == Key.Escape) Close();
    }

    private void OnCancelar(object s, RoutedEventArgs e) => Close();
}
