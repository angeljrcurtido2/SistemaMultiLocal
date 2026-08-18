using CrediSoft.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CrediSoft.UI.Views.Shared;

/// <summary>
/// Modales reutilizables de selección (Local, Vendedor) con buscador y paleta #124E78.
/// </summary>
public static class SelectorModal
{
    // ── Paleta ───────────────────────────────────────────────────────────────
    static readonly SolidColorBrush BrPrimary = new(Color.FromRgb(18,  78, 120));  // #124E78
    static readonly SolidColorBrush BrAzul    = new(Color.FromRgb(31, 119, 180));  // #1F77B4
    static readonly SolidColorBrush BrFondo   = new(Color.FromRgb(238, 242, 247)); // #EEF2F7
    static readonly SolidColorBrush BrBorde   = new(Color.FromRgb(208, 218, 232)); // #D0DAE8
    static readonly SolidColorBrush BrCard    = Brushes.White;
    static readonly SolidColorBrush BrGris    = new(Color.FromRgb(90,  107, 124)); // #5A6B7C
    static readonly SolidColorBrush BrHover   = new(Color.FromRgb(240, 246, 252));
    static readonly SolidColorBrush BrSelBg   = new(Color.FromRgb(210, 235, 252)); // #D2EBFC
    static readonly SolidColorBrush BrSelFg   = BrPrimary;
    static readonly SolidColorBrush BrVerde   = new(Color.FromRgb(30,  110,  66));
    static readonly SolidColorBrush BrRojo    = new(Color.FromRgb(192,  40,  27));

    // ═══════════════════════════════════════════════════════════════════════
    //  SELECTOR DE LOCAL
    // ═══════════════════════════════════════════════════════════════════════
    public static Local? MostrarLocales(Window owner, List<Local> locales, bool obligatorio)
    {
        Local? resultado = null;

        var win = new Window {
            Title = "Seleccionar Local",
            Width = 420, SizeToContent = SizeToContent.Height,
            MaxHeight = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner, ResizeMode = ResizeMode.NoResize,
            Background = BrFondo
        };

        // Header
        var header = BuildHeader("SELECCIONAR LOCAL",
            obligatorio ? "Debe seleccionar el local de operación para continuar" : "Elija el local de esta solicitud",
            "📍");

        // Buscador
        var (searchLocales, txtBuscarLocales) = BuildSearchBox("Buscar local...");

        // Lista
        var lb = BuildListBox();
        void Poblar(string filtro)
        {
            lb.Items.Clear();
            foreach (var l in locales.Where(x =>
                string.IsNullOrEmpty(filtro) ||
                x.NombreLocal.ToUpper().Contains(filtro.ToUpper()) ||
                x.IdLocal.ToString().Contains(filtro)))
            {
                lb.Items.Add(BuildItem(
                    $"{l.IdLocal}",
                    l.NombreLocal,
                    "📍", l));
            }
            if (lb.Items.Count > 0) lb.SelectedIndex = 0;
        }
        Poblar("");
        txtBuscarLocales.TextChanged += (_, _) => Poblar(txtBuscarLocales.Text);

        // Contador
        var lblConteo = new TextBlock {
            FontSize = 10, Foreground = BrGris,
            Margin = new Thickness(16, 4, 16, 0)
        };
        void ActualizarConteo() => lblConteo.Text = $"{lb.Items.Count} local(es)";
        lb.Items.CurrentChanged  += (_, _) => ActualizarConteo();
        txtBuscarLocales.TextChanged += (_, _) => ActualizarConteo();
        ActualizarConteo();

        // Footer
        var btnOk = BuildBtn("✔  Seleccionar", BrVerde);
        var btnCx = BuildBtn("Cancelar", BrGris);
        btnCx.Visibility = obligatorio ? Visibility.Collapsed : Visibility.Visible;

        void Confirmar()
        {
            if (lb.SelectedItem is Border b && b.Tag is Local loc)
            {
                resultado = loc;
                win.DialogResult = true;
                win.Close();
            }
            else MessageBox.Show("Seleccione un local de la lista.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        btnOk.Click += (_, _) => Confirmar();
        btnCx.Click += (_, _) => win.Close();
        lb.MouseDoubleClick += (_, _) => Confirmar();
        win.KeyDown += (_, e) => { if (e.Key == Key.Enter) Confirmar(); else if (e.Key == Key.Escape && !obligatorio) win.Close(); };

        var footer = BuildFooter(btnCx, btnOk);
        var body   = BuildBody(searchLocales, lblConteo, lb);
        win.Content = BuildRoot(header, body, footer);
        win.ShowDialog();
        return resultado;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SELECTOR DE VENDEDOR
    // ═══════════════════════════════════════════════════════════════════════
    public static Usuario? MostrarVendedores(Window owner, List<Usuario> usuarios)
    {
        Usuario? resultado = null;

        var win = new Window {
            Title = "Seleccionar Vendedor",
            Width = 440, SizeToContent = SizeToContent.Height,
            MaxHeight = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner, ResizeMode = ResizeMode.NoResize,
            Background = BrFondo
        };

        var header  = BuildHeader("SELECCIONAR VENDEDOR", "Elija el vendedor asignado a esta solicitud", "👤");
        var (searchVend, txtBuscarVend) = BuildSearchBox("Buscar por nombre o código...");
        var lb = BuildListBox();

        void Poblar(string filtro)
        {
            lb.Items.Clear();
            foreach (var u in usuarios.Where(x =>
                string.IsNullOrEmpty(filtro) ||
                x.NombreUsuario.ToUpper().Contains(filtro.ToUpper()) ||
                x.CodigoUsuario.ToString().Contains(filtro)))
            {
                lb.Items.Add(BuildItem(
                    $"{u.CodigoUsuario}",
                    u.NombreUsuario,
                    "👤", u));
            }
            if (lb.Items.Count > 0) lb.SelectedIndex = 0;
        }
        Poblar("");
        txtBuscarVend.TextChanged += (_, _) => Poblar(txtBuscarVend.Text);

        var lblConteo = new TextBlock {
            FontSize = 10, Foreground = BrGris,
            Margin = new Thickness(16, 4, 16, 0)
        };
        void ActualizarConteo() => lblConteo.Text = $"{lb.Items.Count} vendedor(es)";
        lb.Items.CurrentChanged  += (_, _) => ActualizarConteo();
        txtBuscarVend.TextChanged += (_, _) => ActualizarConteo();
        ActualizarConteo();

        var btnOk = BuildBtn("✔  Seleccionar", BrVerde);
        var btnCx = BuildBtn("Cancelar", BrGris);

        void Confirmar()
        {
            if (lb.SelectedItem is Border b && b.Tag is Usuario u)
            {
                resultado = u;
                win.DialogResult = true;
                win.Close();
            }
            else MessageBox.Show("Seleccione un vendedor de la lista.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        btnOk.Click += (_, _) => Confirmar();
        btnCx.Click += (_, _) => win.Close();
        lb.MouseDoubleClick += (_, _) => Confirmar();
        win.KeyDown += (_, e) => { if (e.Key == Key.Enter) Confirmar(); else if (e.Key == Key.Escape) win.Close(); };

        var footer = BuildFooter(btnCx, btnOk);
        var body   = BuildBody(searchVend, lblConteo, lb);
        win.Content = BuildRoot(header, body, footer);
        win.ShowDialog();
        return resultado;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HELPERS PRIVADOS
    // ═══════════════════════════════════════════════════════════════════════

    static Border BuildHeader(string titulo, string subtitulo, string icono)
    {
        var g = new Grid { Margin = new Thickness(20, 0, 20, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var accentBar = new Border {
            Width = 4, CornerRadius = new CornerRadius(2),
            Background = BrAzul, Margin = new Thickness(0, 14, 12, 14)
        };
        var titleSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 14, 0, 14) };
        titleSp.Children.Add(new TextBlock {
            Text = titulo, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White
        });
        titleSp.Children.Add(new TextBlock {
            Text = subtitulo, FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(170, 204, 232)),
            Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(accentBar, 0); g.Children.Add(accentBar);
        Grid.SetColumn(titleSp,   1); g.Children.Add(titleSp);

        return new Border { Background = BrPrimary, Child = g };
    }

    // Devuelve (contenedor con ícono, referencia al TextBox para suscribir TextChanged)
    static (UIElement container, TextBox input) BuildSearchBox(string placeholder)
    {
        var txt = new TextBox {
            Height = 36, FontSize = 13,
            Padding = new Thickness(36, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Background = BrCard
        };

        var wrapper = new Grid { Margin = new Thickness(16, 12, 16, 0) };
        wrapper.Children.Add(txt);
        wrapper.Children.Add(new TextBlock {
            Text = "🔍", FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(10, 0, 0, 0),
            IsHitTestVisible = false
        });

        return (wrapper, txt);
    }

    static ListBox BuildListBox()
    {
        var lb = new ListBox {
            Margin = new Thickness(16, 8, 16, 0),
            MinHeight = 200, MaxHeight = 380,
            BorderBrush = BrBorde, BorderThickness = new Thickness(1),
            Background = BrCard, Padding = new Thickness(0)
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(lb, ScrollBarVisibility.Disabled);
        return lb;
    }

    static Border BuildItem(string codigo, string nombre, string icono, object tag)
    {
        var g = new Grid { Margin = new Thickness(0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Badge código
        var badge = new Border {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(18),
            Background = BrSelBg, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock {
                Text = codigo, FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = BrPrimary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };
        Grid.SetColumn(badge, 0); g.Children.Add(badge);

        var nameTb = new TextBlock {
            Text = nombre, FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 31, 48)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(nameTb, 1); g.Children.Add(nameTb);

        var item = new Border {
            Padding = new Thickness(10, 8, 10, 8),
            Tag = tag, Child = g,
            Background = Brushes.Transparent
        };

        // Hover effect
        item.MouseEnter += (_, _) => { if (!IsSelected(item)) item.Background = BrHover; };
        item.MouseLeave += (_, _) => { if (!IsSelected(item)) item.Background = Brushes.Transparent; };

        return item;
    }

    static bool IsSelected(Border b) =>
        b.Parent is ListBoxItem lbi && lbi.IsSelected;

    static Border BuildFooter(Button btnCancelar, Button btnOk)
    {
        var sp = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        sp.Children.Add(btnCancelar);
        sp.Children.Add(btnOk);
        return new Border {
            Background = BrCard, BorderBrush = BrBorde,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 12, 16, 12),
            Child = sp
        };
    }

    static Button BuildBtn(string texto, SolidColorBrush bg) => new Button {
        Content = texto, Height = 36, Padding = new Thickness(20, 0, 20, 0),
        Margin = new Thickness(0, 0, 8, 0),
        Background = bg, Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        FontWeight = FontWeights.SemiBold, FontSize = 13,
        Cursor = Cursors.Hand
    };

    static StackPanel BuildBody(UIElement searchContainer, TextBlock lblConteo, ListBox lb)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
        sp.Children.Add(searchContainer);
        sp.Children.Add(lblConteo);
        sp.Children.Add(lb);
        return sp;
    }

    static StackPanel BuildRoot(Border header, StackPanel body, Border footer)
    {
        var sp = new StackPanel();
        sp.Children.Add(header);
        sp.Children.Add(body);
        sp.Children.Add(footer);
        return sp;
    }
}
