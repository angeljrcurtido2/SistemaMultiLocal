using CrediSoft.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Cobros;

// ─────────────────────────────────────────────────────────────────────────────
// Registra el cobro a nombre de un vendedor DISTINTO al usuario logueado — sin
// tocar SessionService (la sesión/caja siguen siendo las de quien está logueado).
// Mismo patrón que CajaCredencialesDialog (AdicionalesWindows.cs): valida código+
// contraseña de OTRO usuario y expone el ID resultante como propiedad pública.
// Existe para que la comisión de cobranza de fin de mes se calcule a nombre de
// quien realmente cobró la cuota, no de quien tiene la sesión/caja abierta.
// ─────────────────────────────────────────────────────────────────────────────
public class SeleccionarVendedorCobradorDialog : Window
{
    public int    VendedorId     { get; private set; }
    public string VendedorNombre { get; private set; } = "";

    private readonly IDbConnectionFactory _db;
    private TextBox     _txtCodigo = null!;
    private PasswordBox _txtClave  = null!;

    private static SolidColorBrush B(string h) =>
        new((Color)ColorConverter.ConvertFromString(h));

    public SeleccionarVendedorCobradorDialog()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Cobrado por otro vendedor";
        Width = 380; Height = 280;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        var hdr = new Border { Background = B("#0E2F44"), Padding = new Thickness(14, 10, 14, 10) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = "COBRADO POR OTRO VENDEDOR",
            Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold });
        hdrSp.Children.Add(new TextBlock {
            Text = "Ingrese el código y la contraseña del vendedor que realmente cobra",
            Foreground = B("#90A4AE"), FontSize = 10.5, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0) });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var pie = new Border { Padding = new Thickness(12, 8, 12, 8),
            BorderBrush = B("#E0E0E0"), BorderThickness = new Thickness(0, 1, 0, 0) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnAcep = new Button { Content = "✔ Aceptar", Height = 32, Padding = new Thickness(16, 0, 16, 0),
            Background = B("#1B5E20"), Foreground = Brushes.White, FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0) };
        var btnCan = new Button { Content = "Cancelar", Height = 32, Padding = new Thickness(12, 0, 12, 0),
            Background = B("#546E7A"), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnAcep.Click += async (_, _) => await Validar();
        btnCan.Click  += (_, _) => { DialogResult = false; Close(); };
        pieSp.Children.Add(btnAcep); pieSp.Children.Add(btnCan);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        var form = new Grid { Margin = new Thickness(20, 18, 20, 0) };
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        void AddRow(string label, UIElement ctrl, int row)
        {
            var lbl = new TextBlock { Text = label, FontWeight = FontWeights.SemiBold,
                Foreground = B("#37474F"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 14) };
            Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0); form.Children.Add(lbl);
            Grid.SetRow(ctrl, row); Grid.SetColumn(ctrl, 1);
            if (ctrl is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 0, 14);
            form.Children.Add(ctrl);
        }

        _txtCodigo = new TextBox { Padding = new Thickness(8, 6, 8, 6) };
        _txtClave  = new PasswordBox { Padding = new Thickness(8, 6, 8, 6) };
        _txtClave.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Validar(); };

        AddRow("CÓDIGO",      _txtCodigo, 0);
        AddRow("CONTRASEÑA",  _txtClave,  1);

        root.Children.Add(form);
        Content = root;
        Loaded += (_, _) => _txtCodigo.Focus();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
    }

    private async System.Threading.Tasks.Task Validar()
    {
        var codigo = _txtCodigo.Text.Trim();
        var clave  = _txtClave.Password;
        if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(clave))
        {
            MessageBox.Show("Ingrese código y contraseña.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            using var conn = _db.Create();
            var user = await conn.QueryFirstOrDefaultAsync<(int Id, string Nombre)>(
                @"SELECT ID_USUARIO, ISNULL(NOMBRE_USUARIO,'') FROM USUARIOS
                  WHERE CODIGO_USUARIO = @cod AND CONTRASEÑA_USUARIO = @clave",
                new { cod = codigo, clave });
            if (user.Id == 0)
            {
                MessageBox.Show("Código o contraseña incorrectos.", "Acceso denegado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _txtClave.Password = "";
                _txtClave.Focus();
                return;
            }
            VendedorId     = user.Id;
            VendedorNombre = user.Nombre;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al validar: " + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
