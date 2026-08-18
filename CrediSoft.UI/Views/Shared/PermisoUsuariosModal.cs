using Dapper;
using CrediSoft.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Shared;

public static class PermisoUsuariosModal
{
    public record UsuarioPermiso(int IdUsuario, string Nombre);
    private record FilaUsuario(int Id, string Nombre, string Codigo, string Contrasena);

    private static SolidColorBrush SB(string h) =>
        new((Color)ColorConverter.ConvertFromString(h));

    // Segoe MDL2: E7B3 = View (ojo), E7B4 = Hide (ojo tachado)
    private const string IcoView = "";
    private const string IcoHide = "";
    private const string IcoLock = "";
    private const string IcoUser = "";
    private const string IcoFind = "";
    private const string IcoStar = "";

    public static async Task<UsuarioPermiso?> MostrarAsync(Window owner, IDbConnectionFactory db)
    {
        // ── Datos ─────────────────────────────────────────────────────────
        List<FilaUsuario> usuarios = new();
        try
        {
            using var conn = db.Create();
            // Administradores + excepción puntual (usuario código "67", ver
            // Usuario.PuedeVerTodosLosLocales para el mismo criterio usado en otras pantallas).
            var rows = await conn.QueryAsync<dynamic>(
                "SELECT ID_USUARIO, NOMBRE_USUARIO, CODIGO_USUARIO, CONTRASEÑA_USUARIO FROM USUARIOS " +
                "WHERE CARGO_USUARIO = 'ADMINISTRADOR' OR CODIGO_USUARIO = '67' ORDER BY NOMBRE_USUARIO");
            usuarios = rows.Select(u => new FilaUsuario(
                (int)u.ID_USUARIO,
                (string)u.NOMBRE_USUARIO,
                u.CODIGO_USUARIO?.ToString() ?? "",
                u.CONTRASEÑA_USUARIO?.ToString() ?? "")).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar usuarios: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        FilaUsuario?    usuarioSel = null;
        UsuarioPermiso? resultado  = null;

        // ── Paleta ────────────────────────────────────────────────────────
        var Az0  = SB("#0A2233");
        var Az1  = SB("#0E2F44");
        var Az2  = SB("#1A4F6E");
        var Az3  = SB("#1F6089");
        var AzM  = SB("#7FB3D3");
        var Gris = SB("#546E7A");
        var Fond = SB("#EEF2F6");

        // ── Ventana ───────────────────────────────────────────────────────
        var dlg = new Window
        {
            Title = "Autorización requerida", Width = 430,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner, ResizeMode = ResizeMode.NoResize,
            Background = Fond, FontFamily = new FontFamily("Segoe UI"),
            ShowInTaskbar = false,
        };
        var root = new DockPanel();

        // ── Header ────────────────────────────────────────────────────────
        var hdrBorder = new Border
        {
            Background = Az1, Padding = new Thickness(22, 16, 22, 16),
            BorderBrush = Az0, BorderThickness = new Thickness(0, 0, 0, 3)
        };
        var hdrGrid = new Grid();
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icoCircle = new Border
        {
            Width = 44, Height = 44, CornerRadius = new CornerRadius(22),
            Background = Az3, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
            Child = new TextBlock
            {
                Text = IcoStar, FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 20, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(icoCircle, 0);

        var hdrTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hdrTxt.Children.Add(new TextBlock
        {
            Text = "AUTORIZACIÓN DE ADMINISTRADOR",
            FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White
        });
        hdrTxt.Children.Add(new TextBlock
        {
            Text = "Solo un administrador puede autorizar esta operación",
            FontSize = 11, Foreground = AzM, Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(hdrTxt, 1);
        hdrGrid.Children.Add(icoCircle);
        hdrGrid.Children.Add(hdrTxt);
        hdrBorder.Child = hdrGrid;
        DockPanel.SetDock(hdrBorder, Dock.Top);
        root.Children.Add(hdrBorder);

        // ── Footer ────────────────────────────────────────────────────────
        var footBorder = new Border
        {
            Background = Az2, Padding = new Thickness(20, 12, 20, 12),
            BorderBrush = Az0, BorderThickness = new Thickness(0, 1, 0, 0)
        };
        Button MkBtn(string txt, SolidColorBrush bg, SolidColorBrush hover)
        {
            var b = new Button
            {
                Content = txt, Height = 36, Padding = new Thickness(22, 0, 22, 0),
                Background = bg, Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold, FontSize = 13,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            b.MouseEnter += (_, _) => b.Background = hover;
            b.MouseLeave += (_, _) => b.Background = bg;
            return b;
        }
        var btnAceptar  = MkBtn("✔  Aceptar",  Az3,  SB("#155A80"));
        var btnCancelar = MkBtn("✕  Cancelar", Gris, SB("#37474F"));
        btnCancelar.Margin = new Thickness(8, 0, 0, 0);
        var footRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footRow.Children.Add(btnAceptar);
        footRow.Children.Add(btnCancelar);
        footBorder.Child = footRow;
        DockPanel.SetDock(footBorder, Dock.Bottom);
        root.Children.Add(footBorder);

        // ── Cuerpo ────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(22, 20, 22, 18) };

        TextBlock Sec(string t) => new()
        {
            Text = t, FontSize = 10.5, FontWeight = FontWeights.SemiBold,
            Foreground = SB("#5A7D94"), Margin = new Thickness(0, 0, 0, 6)
        };

        // ── Sección USUARIO ───────────────────────────────────────────────
        body.Children.Add(Sec("USUARIO"));

        var txtIdUsuario = new TextBox
        {
            FontSize = 22, FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = SB("#0E2F44"),
            CaretBrush = SB("#1A4F6E"),
            ToolTip = "Código del administrador — Tab o Enter para confirmar",
            Height = 48,
        };

        var lblIdHint = new TextBlock
        {
            Text = "CÓDIGO USUARIO", FontSize = 9.5, FontWeight = FontWeights.SemiBold,
            Foreground = AzM, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2)
        };

        var leftPane = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        leftPane.Children.Add(lblIdHint);
        leftPane.Children.Add(txtIdUsuario);

        var midDivider = new Border
        {
            Width = 1, Background = SB("#D0DCE8"), Margin = new Thickness(0, 10, 0, 10)
        };

        var txtUsuarioDisplay = new TextBlock
        {
            Text = "Ninguno seleccionado", FontStyle = FontStyles.Italic,
            FontSize = 12.5, Foreground = SB("#8DA9B8"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 2)
        };
        var lblNombreHint = new TextBlock
        {
            Text = "ADMINISTRADOR", FontSize = 9.5, FontWeight = FontWeights.SemiBold,
            Foreground = AzM, Margin = new Thickness(0, 0, 0, 3)
        };
        var nombreStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nombreStack.Children.Add(lblNombreHint);
        nombreStack.Children.Add(txtUsuarioDisplay);

        var btnBuscarUsuario = new Button
        {
            Height = 32, Padding = new Thickness(10, 0, 10, 0),
            Background = Az2, Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 11,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        var btnBuscarContent = new StackPanel { Orientation = Orientation.Horizontal };
        btnBuscarContent.Children.Add(new TextBlock
        {
            Text = IcoFind, FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 13, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0)
        });
        btnBuscarContent.Children.Add(new TextBlock
        {
            Text = "Buscar", FontSize = 11, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        btnBuscarUsuario.Content = btnBuscarContent;
        btnBuscarUsuario.MouseEnter += (_, _) => btnBuscarUsuario.Background = Az3;
        btnBuscarUsuario.MouseLeave += (_, _) => btnBuscarUsuario.Background = Az2;

        var rightPane = new Grid { Margin = new Thickness(14, 10, 12, 10) };
        rightPane.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rightPane.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(nombreStack,      0);
        Grid.SetColumn(btnBuscarUsuario, 1);
        rightPane.Children.Add(nombreStack);
        rightPane.Children.Add(btnBuscarUsuario);

        var userCardGrid = new Grid();
        userCardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        userCardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        userCardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(leftPane,    0);
        Grid.SetColumn(midDivider,  1);
        Grid.SetColumn(rightPane,   2);
        userCardGrid.Children.Add(leftPane);
        userCardGrid.Children.Add(midDivider);
        userCardGrid.Children.Add(rightPane);

        body.Children.Add(new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(6),
            BorderBrush = Az2, BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 16),
            Effect = new DropShadowEffect { ShadowDepth = 1, BlurRadius = 5, Opacity = 0.07, Color = Colors.Black },
            Child = userCardGrid
        });

        txtIdUsuario.TextChanged += (_, _) =>
        {
            txtIdUsuario.Foreground = SB("#0E2F44");
            if (string.IsNullOrWhiteSpace(txtIdUsuario.Text))
            {
                txtUsuarioDisplay.Text       = "Ninguno seleccionado";
                txtUsuarioDisplay.FontStyle  = FontStyles.Italic;
                txtUsuarioDisplay.Foreground = SB("#8DA9B8");
                usuarioSel = null;
            }
        };

        // ── Sección CONTRASEÑA (estilo Login) ─────────────────────────────
        body.Children.Add(new Border { Height = 1, Background = SB("#D8E5EE"), Margin = new Thickness(0, 0, 0, 14) });
        body.Children.Add(Sec("CONTRASEÑA"));

        // Contenedor con fondo azul y borde inferior (idéntico al login)
        var passContainer = new Border
        {
            Background      = Az2,
            BorderBrush     = Az3,
            BorderThickness = new Thickness(0, 0, 0, 2),
            CornerRadius    = new CornerRadius(4, 4, 0, 0),
        };

        var txtPassword = new PasswordBox
        {
            FontSize = 14, Height = 42,
            Padding = new Thickness(12, 0, 44, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            SelectionBrush = SB("#4FC3F7"),
        };
        var txtPasswordVisible = new TextBox
        {
            FontSize = 14, Height = 42,
            Padding = new Thickness(12, 0, 44, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            SelectionBrush = SB("#4FC3F7"),
            Visibility = Visibility.Collapsed,
        };

        // Borde cambia a celeste al enfocar (igual que login)
        txtPassword.GotFocus         += (_, _) => passContainer.BorderBrush = SB("#4FC3F7");
        txtPassword.LostFocus        += (_, _) => passContainer.BorderBrush = Az3;
        txtPasswordVisible.GotFocus  += (_, _) => passContainer.BorderBrush = SB("#4FC3F7");
        txtPasswordVisible.LostFocus += (_, _) => passContainer.BorderBrush = Az3;

        // Ojo superpuesto a la derecha
        bool passVisible = false;
        var eyeIcon = new TextBlock
        {
            Text = IcoView,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 16, Foreground = AzM,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var btnEye = new Button
        {
            Width = 36, Height = 36,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false,
            Content = eyeIcon,
        };
        btnEye.MouseEnter += (_, _) => eyeIcon.Foreground = SB("#4FC3F7");
        btnEye.MouseLeave += (_, _) => eyeIcon.Foreground = AzM;
        btnEye.Click += (_, _) =>
        {
            passVisible = !passVisible;
            if (passVisible)
            {
                txtPasswordVisible.Text       = txtPassword.Password;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtPassword.Visibility        = Visibility.Collapsed;
                eyeIcon.Text = IcoHide;
                txtPasswordVisible.Focus();
                txtPasswordVisible.CaretIndex = txtPasswordVisible.Text.Length;
            }
            else
            {
                txtPassword.Password          = txtPasswordVisible.Text;
                txtPassword.Visibility        = Visibility.Visible;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                eyeIcon.Text = IcoView;
                txtPassword.Focus();
            }
        };

        // Grid interno: PasswordBox y TextBox superpuestos + ojo flotante
        var passInner = new Grid();
        passInner.Children.Add(txtPassword);
        passInner.Children.Add(txtPasswordVisible);
        passInner.Children.Add(btnEye);
        passContainer.Child = passInner;
        body.Children.Add(passContainer);

        // ── Lógica selección por CÓDIGO de usuario ─────────────────────────
        // El campo grande de la izquierda identifica al usuario por CODIGO_USUARIO (lo que el
        // cajero conoce y usa a diario, ej. "67"), NO por ID_USUARIO (la clave interna de la
        // tabla, ej. 2 para ese mismo código) — bug real reportado: el cajero conocía el código
        // 67 de un usuario, pero acá se buscaba/mostraba el ID_USUARIO (2), así que tipear "67"
        // nunca encontraba nada. usuarioSel.Id (ID_USUARIO real) se sigue usando tal cual para
        // todo lo interno (UsuarioPermiso, auditoría) — solo cambia qué campo ve y busca el cajero.
        Action<string> seleccionarPorCodigo = (codigo) =>
        {
            var u = usuarios.FirstOrDefault(x => x.Codigo == codigo);
            if (u != null)
            {
                usuarioSel = u;
                txtIdUsuario.Text            = u.Codigo;
                txtIdUsuario.Foreground      = SB("#1A7A35");
                txtUsuarioDisplay.Text       = u.Nombre;
                txtUsuarioDisplay.FontStyle  = FontStyles.Normal;
                txtUsuarioDisplay.Foreground = SB("#0E2F44");
                txtPassword.Focus();
            }
            else
            {
                txtIdUsuario.Foreground      = SB("#C62828");
                txtUsuarioDisplay.Text       = "Código no encontrado";
                txtUsuarioDisplay.FontStyle  = FontStyles.Italic;
                txtUsuarioDisplay.Foreground = SB("#C62828");
                usuarioSel = null;
            }
        };
        txtIdUsuario.KeyDown   += (_, e) => { if (e.Key == Key.Enter) seleccionarPorCodigo(txtIdUsuario.Text.Trim()); };
        txtIdUsuario.LostFocus += (_, _) => { if (!string.IsNullOrWhiteSpace(txtIdUsuario.Text)) seleccionarPorCodigo(txtIdUsuario.Text.Trim()); };

        // ── Error inline ──────────────────────────────────────────────────
        var errorCard = new Border
        {
            Background = SB("#FFF3F3"), CornerRadius = new CornerRadius(5),
            BorderBrush = SB("#FFCDD2"), BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 10, 0, 0),
            Visibility = Visibility.Collapsed
        };
        var lblError = new TextBlock { FontSize = 12, Foreground = SB("#C62828"), TextWrapping = TextWrapping.Wrap };
        errorCard.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock
                {
                    Text = "⚠ ", FontSize = 13,
                    Foreground = SB("#C62828"),
                    VerticalAlignment = VerticalAlignment.Center
                },
                lblError
            }
        };
        body.Children.Add(errorCard);

        root.Children.Add(body);
        dlg.Content = root;

        // ── Sub-ventana selector ──────────────────────────────────────────
        btnBuscarUsuario.Click += (_, _) =>
        {
            var sel = new Window
            {
                Title = "Seleccionar usuario", Width = 440, Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = dlg, ResizeMode = ResizeMode.NoResize,
                Background = Fond, FontFamily = new FontFamily("Segoe UI"), ShowInTaskbar = false,
            };
            var selRoot = new DockPanel();

            var selHdr = new Border { Background = Az1, Padding = new Thickness(18, 13, 18, 13) };
            var selHdrRow = new StackPanel { Orientation = Orientation.Horizontal };
            selHdrRow.Children.Add(new TextBlock
            {
                Text = IcoUser, FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18, Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0)
            });
            var selHdrTxt = new StackPanel();
            selHdrTxt.Children.Add(new TextBlock { Text = "SELECCIONAR ADMINISTRADOR", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            selHdrTxt.Children.Add(new TextBlock { Text = "Usuarios habilitados para autorizar", FontSize = 10.5, Foreground = AzM, Margin = new Thickness(0, 2, 0, 0) });
            selHdrRow.Children.Add(selHdrTxt);
            selHdr.Child = selHdrRow;
            DockPanel.SetDock(selHdr, Dock.Top);
            selRoot.Children.Add(selHdr);

            var selSearchBorder = new Border { Background = Az2, Padding = new Thickness(14, 9, 14, 9) };
            var selSearchRow = new Grid();
            selSearchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            selSearchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            selSearchRow.Children.Add(new TextBlock
            {
                Text = IcoFind, FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14, Foreground = SB("#A8CFEA"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            });
            var selTxtBusc = new TextBox
            {
                Height = 32, FontSize = 12.5,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = SB("#1F6089"), Foreground = Brushes.White,
                CaretBrush = Brushes.White, BorderThickness = new Thickness(0)
            };
            Grid.SetColumn(selTxtBusc, 1);
            selSearchRow.Children.Add(selTxtBusc);
            selSearchBorder.Child = selSearchRow;
            DockPanel.SetDock(selSearchBorder, Dock.Top);
            selRoot.Children.Add(selSearchBorder);

            var selFoot = new Border { Background = Az2, Padding = new Thickness(14, 10, 14, 10), BorderBrush = Az0, BorderThickness = new Thickness(0, 1, 0, 0) };
            var selBtnSel = MkBtn("✔  Seleccionar", Az3, SB("#155A80"));
            var selBtnCan = MkBtn("✕  Cancelar", Gris, SB("#37474F"));
            selBtnCan.Margin = new Thickness(8, 0, 0, 0);
            var selFootRow2 = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            selFootRow2.Children.Add(selBtnSel);
            selFootRow2.Children.Add(selBtnCan);
            selFoot.Child = selFootRow2;
            DockPanel.SetDock(selFoot, Dock.Bottom);
            selRoot.Children.Add(selFoot);

            var colHdrStyle = new Style(typeof(DataGridColumnHeader));
            colHdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, Az2));
            colHdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            colHdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            colHdrStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            colHdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 9, 12, 9)));
            colHdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
            colHdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty, SB("#155980")));

            var selGrid = new DataGrid
            {
                AutoGenerateColumns = false, IsReadOnly = true, RowHeight = 36,
                FontSize = 13, BorderThickness = new Thickness(0), Background = Brushes.White,
                AlternatingRowBackground = SB("#F4F8FA"),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = SB("#E0E8EE"),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                ColumnHeaderStyle = colHdrStyle,
                SelectionMode = DataGridSelectionMode.Single,
                CanUserAddRows = false, CanUserResizeRows = false,
            };
            selGrid.Columns.Add(new DataGridTextColumn { Header = "Código", Binding = new System.Windows.Data.Binding("Codigo"), Width = new DataGridLength(70, DataGridLengthUnitType.Pixel) });
            selGrid.Columns.Add(new DataGridTextColumn { Header = "Nombre", Binding = new System.Windows.Data.Binding("Nombre"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            selGrid.ItemsSource = usuarios;

            var selGridWrap = new Border
            {
                Padding = new Thickness(10), Background = Fond,
                Child = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    BorderBrush = SB("#C8D8E4"), BorderThickness = new Thickness(1),
                    Effect = new DropShadowEffect { ShadowDepth = 1, BlurRadius = 5, Opacity = 0.07, Color = Colors.Black },
                    Child = selGrid
                }
            };
            selRoot.Children.Add(selGridWrap);
            sel.Content = selRoot;

            selTxtBusc.TextChanged += (_, _) =>
            {
                var t = selTxtBusc.Text.Trim().ToUpperInvariant();
                selGrid.ItemsSource = string.IsNullOrEmpty(t)
                    ? usuarios
                    : usuarios.Where(u => u.Nombre.ToUpperInvariant().Contains(t) || u.Codigo.Contains(t)).ToList();
                if (selGrid.Items.Count > 0) selGrid.SelectedIndex = 0;
            };

            Action confirmarSel = () =>
            {
                if (selGrid.SelectedItem is FilaUsuario f)
                {
                    usuarioSel = f;
                    txtIdUsuario.Text            = f.Codigo;
                    txtIdUsuario.Foreground      = SB("#1A7A35");
                    txtUsuarioDisplay.Text       = f.Nombre;
                    txtUsuarioDisplay.FontStyle  = FontStyles.Normal;
                    txtUsuarioDisplay.Foreground = SB("#0E2F44");
                    sel.DialogResult = true;
                    sel.Close();
                    txtPassword.Focus();
                }
            };

            selBtnSel.Click          += (_, _) => confirmarSel();
            selBtnCan.Click          += (_, _) => sel.Close();
            selGrid.MouseDoubleClick += (_, _) => confirmarSel();
            selGrid.PreviewKeyDown   += (_, e) => { if (e.Key == Key.Enter) { confirmarSel(); e.Handled = true; } };
            sel.Loaded  += (_, _) => { if (usuarios.Count > 0) selGrid.SelectedIndex = 0; selTxtBusc.Focus(); };
            sel.KeyDown += (_, e) => { if (e.Key == Key.Escape) sel.Close(); };
            sel.ShowDialog();
        };

        // ── Lógica confirmación ───────────────────────────────────────────
        void mostrarError(string msg) { lblError.Text = msg; errorCard.Visibility = Visibility.Visible; }

        string GetPassword() => passVisible ? txtPasswordVisible.Text : txtPassword.Password;

        Action confirmar = () =>
        {
            errorCard.Visibility = Visibility.Collapsed;
            if (usuarioSel == null) { mostrarError("Seleccioná un administrador primero."); return; }
            if (GetPassword().Trim() != usuarioSel.Contrasena)
            { mostrarError("Contraseña incorrecta."); txtPassword.Focus(); return; }
            resultado = new UsuarioPermiso(usuarioSel.Id, usuarioSel.Nombre);
            dlg.DialogResult = true;
            dlg.Close();
        };

        btnAceptar.Click           += (_, _) => confirmar();
        btnCancelar.Click          += (_, _) => dlg.Close();
        txtPassword.KeyDown        += (_, e) => { if (e.Key == Key.Enter) confirmar(); };
        txtPasswordVisible.KeyDown += (_, e) => { if (e.Key == Key.Enter) confirmar(); };
        dlg.KeyDown                += (_, e) => { if (e.Key == Key.Escape) dlg.Close(); };
        dlg.Loaded                 += (_, _) => txtIdUsuario.Focus();

        dlg.ShowDialog();
        return resultado;
    }
}
