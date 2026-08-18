using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Cobros;

// ─────────────────────────────────────────────────────────────────────────────
// Se muestra cuando un usuario NO administrador intenta cobrar una cuota que
// "Asignaciones de cobranza" ya asignó a otro cobrador. En vez de solo bloquear,
// ofrece aplicar automáticamente la misma función de "Cambiar vendedor" que ya
// existe (SeleccionarVendedorCobradorDialog) pero sin pedir clave — la propia
// asignación ya es la autorización — para que el usuario logueado pueda registrar
// el cobro a nombre del cobrador asignado sin cambiar de sesión ni de caja.
// ─────────────────────────────────────────────────────────────────────────────
public class CuotaAsignadaOtroDialog : Window
{
    public bool UsarComoVendedor { get; private set; }

    private static SolidColorBrush B(string h) =>
        new((Color)ColorConverter.ConvertFromString(h));

    public CuotaAsignadaOtroDialog(string cobradorNombre)
    {
        Title = "Cuota asignada a otro cobrador";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");

        var root = new DockPanel();

        var hdr = new Border { Background = B("#0E2F44"), Padding = new Thickness(16, 12, 16, 12) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock
        {
            Text = "CUOTA ASIGNADA A OTRO COBRADOR",
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.Bold
        });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top);
        root.Children.Add(hdr);

        var pie = new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            BorderBrush = B("#E0E0E0"),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        var pieSp = new StackPanel();

        var btnUsar = new Button
        {
            Content = $"✔ Cobrar a nombre de {cobradorNombre}",
            Height = 36,
            Padding = new Thickness(12, 0, 12, 0),
            Background = B("#1B5E20"),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 8)
        };
        btnUsar.Click += (_, _) => { UsarComoVendedor = true; DialogResult = true; Close(); };

        var btnCancelar = new Button
        {
            Content = "Cancelar",
            Height = 32,
            Padding = new Thickness(12, 0, 12, 0),
            Background = B("#546E7A"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        btnCancelar.Click += (_, _) => { DialogResult = false; Close(); };

        pieSp.Children.Add(btnUsar);
        pieSp.Children.Add(btnCancelar);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom);
        root.Children.Add(pie);

        var body = new StackPanel { Margin = new Thickness(18, 18, 18, 18) };
        body.Children.Add(new TextBlock
        {
            Text = $"Esta cuota está asignada a {cobradorNombre}.",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = B("#37474F"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });
        body.Children.Add(new TextBlock
        {
            Text = $"¿Deseás usar la función de \"Cambiar vendedor\" para registrar este cobro a nombre de {cobradorNombre}? " +
                   "No hace falta salir de tu sesión ni de la caja — solo se atribuye la comisión de esta cuota a su nombre.",
            FontSize = 11.5,
            Foreground = B("#546E7A"),
            TextWrapping = TextWrapping.Wrap
        });
        root.Children.Add(body);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
    }
}
