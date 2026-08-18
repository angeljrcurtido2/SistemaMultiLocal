using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Transferencias;

// Reemplaza el MessageBox.Show("Cantidad inválida.") genérico del alta de líneas en una
// transferencia — no distinguía si el campo estaba vacío, en cero, con texto no numérico
// o negativo, ni mostraba a qué artículo correspondía el error. Este modal explica la
// causa exacta y qué se esperaba, para no prestarse a confusión sobre por qué no se pudo
// agregar la línea.
public class CantidadInvalidaDialog : Window
{
    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public CantidadInvalidaDialog(string codigoArt, string descripcionArt, string textoIngresado, decimal stockDisponible)
    {
        Title                 = "Cantidad inválida";
        Width                 = 440;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#F5F7FA");
        FontFamily            = new FontFamily("Segoe UI");

        var root = new DockPanel();

        // ── Header ámbar (aviso de validación, no error grave) ─────────────────
        var header = new Border { Background = B("#E65100"), Padding = new Thickness(24, 18, 24, 18) };
        var hDp = new DockPanel();
        var icono = new Border
        {
            Width = 44, Height = 44, CornerRadius = new CornerRadius(22),
            Background = B("#EF6C00"), Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "!", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        DockPanel.SetDock(icono, Dock.Left);
        hDp.Children.Add(icono);
        var hTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hTxt.Children.Add(new TextBlock
        {
            Text = "CANTIDAD INVÁLIDA", Foreground = Brushes.White,
            FontSize = 15, FontWeight = FontWeights.Bold,
        });
        hTxt.Children.Add(new TextBlock
        {
            Text = "No se pudo agregar el artículo al remito",
            Foreground = B("#FFE0B2"), FontSize = 11, Margin = new Thickness(0, 3, 0, 0),
        });
        hDp.Children.Add(hTxt);
        header.Child = hDp;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Cuerpo ───────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(24, 18, 24, 4) };

        // Card del artículo
        var artCard = new Border
        {
            Background = B("#EEF4FB"), BorderBrush = B("#BBDEFB"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 14),
        };
        var artSp = new StackPanel();
        artSp.Children.Add(new TextBlock
        {
            Text = $"{codigoArt}  —  {descripcionArt}", FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            Foreground = B("#0E2F44"), TextWrapping = TextWrapping.Wrap,
        });
        artSp.Children.Add(new TextBlock
        {
            Text = $"Stock disponible en el local origen: {stockDisponible:N0}",
            FontSize = 11, Foreground = B("#5A7D94"), Margin = new Thickness(0, 4, 0, 0),
        });
        artCard.Child = artSp;
        body.Children.Add(artCard);

        // Causa exacta del error
        string causa = DeterminarCausa(textoIngresado, out string sugerencia);

        body.Children.Add(new TextBlock
        {
            Text = "QUÉ PASÓ", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = B("#78909C"), Margin = new Thickness(0, 0, 0, 6),
        });
        var causaCard = new Border
        {
            Background = B("#FFF3E0"), BorderBrush = B("#FFCC80"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 14),
        };
        causaCard.Child = new TextBlock
        {
            Text = causa, FontSize = 12, Foreground = B("#5D4037"), TextWrapping = TextWrapping.Wrap,
        };
        body.Children.Add(causaCard);

        body.Children.Add(new TextBlock
        {
            Text = sugerencia,
            FontSize = 11.5, Foreground = B("#37474F"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        DockPanel.SetDock(body, Dock.Top);
        root.Children.Add(body);

        // ── Footer ───────────────────────────────────────────────────────────
        var footer = new Border
        {
            Background = Brushes.White, BorderBrush = B("#FFCC80"),
            BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(24, 14, 24, 14),
        };
        var btnOk = new Button
        {
            Content = "Entendido", Height = 38, Width = 140,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = B("#0E2F44"), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 13, FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        btnOk.Click += (_, _) => Close();
        footer.Child = btnOk;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.Enter) Close(); };
    }

    private static string DeterminarCausa(string textoIngresado, out string sugerencia)
    {
        var t = textoIngresado?.Trim() ?? "";

        if (string.IsNullOrEmpty(t))
        {
            sugerencia = "Escribí la cantidad de unidades que querés transferir antes de presionar \"Insertar\".";
            return "El campo \"Cantidad\" está vacío.";
        }

        if (!decimal.TryParse(t, out var valor))
        {
            sugerencia = "El campo \"Cantidad\" solo acepta números. Revisá que no haya letras ni símbolos.";
            return $"El texto ingresado (\"{textoIngresado}\") no es un número válido.";
        }

        if (valor == 0)
        {
            sugerencia = "Ingresá una cantidad mayor a cero para poder transferir el artículo.";
            return "La cantidad ingresada es cero.";
        }

        if (valor < 0)
        {
            sugerencia = "Ingresá una cantidad positiva. No se pueden transferir cantidades negativas.";
            return $"La cantidad ingresada ({valor:N0}) es negativa.";
        }

        sugerencia = "Verificá la cantidad ingresada e intentá nuevamente.";
        return "La cantidad ingresada no es válida.";
    }
}
