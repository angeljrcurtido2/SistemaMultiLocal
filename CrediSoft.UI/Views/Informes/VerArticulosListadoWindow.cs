using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CrediSoft.UI.Views.Informes;

// Réplica visual del listado "Ver Artículos" del sistema viejo (VB6): panel de criterios
// lila a la izquierda y grilla de resultados con encabezado naranja a la derecha, ambos
// en la misma ventana — a diferencia del rediseño anterior (tarjeta blanca centrada +
// vista previa aparte), acá se busca la fidelidad visual exacta pedida por el usuario.
public class VerArticulosListadoWindow : Window
{
    private readonly IArticuloRepository _artRepo;
    private readonly ILocalRepository    _localRepo;
    private readonly ISessionService     _session;

    private List<Local> _locales = new();
    private bool         _esAdmin;

    // Paleta del sistema viejo: fondo lila, encabezados de grilla naranjas, fila
    // seleccionada azul.
    private static readonly Color CLila       = Color.FromRgb(0xC9, 0xC3, 0xF0);
    private static readonly Color CLilaOscuro = Color.FromRgb(0xB3, 0xAB, 0xE8);
    private static readonly Color CNaranja    = Color.FromRgb(0xF3, 0xA5, 0x1C);
    private static readonly Color CNaranjaBd  = Color.FromRgb(0xC9, 0x84, 0x0A);
    private static readonly Color CAzulSel    = Color.FromRgb(0x2F, 0x6F, 0xE0);
    private static readonly Color CGrupo      = Color.FromRgb(0x1A, 0x4F, 0x6E);
    private static readonly Color CW          = Colors.White;
    private static readonly Color CBorde      = Color.FromRgb(0x8A, 0x82, 0xC9);
    private static readonly Color CTexto      = Color.FromRgb(20, 20, 30);
    private static readonly Color CAlt        = Color.FromRgb(0xF4, 0xF2, 0xFC);

    private static SolidColorBrush B(Color c) => new(c);

    private TextBox   _txtBuscarArticulo = null!;
    private ComboBox  _cboLocal          = null!;
    private TextBox   _txtBuscarLocal    = null!;
    private TextBox   _txtBuscarLocalEspecifico = null!;
    private TextBox   _txtDesde          = null!;
    private TextBox   _txtHasta          = null!;
    private ComboBox  _cboAgrupar        = null!;
    private RadioButton _rbTodos         = null!;
    private RadioButton _rbEspecifico    = null!;
    private ComboBox  _cboLocalEspecifico = null!;
    private CheckBox  _chkMostrarStock0  = null!;
    private DataGrid  _grid              = null!;
    private TextBlock _lblEstado         = null!;
    private Border    _overlayCargando   = null!;
    private RotateTransform _spinnerRotacion = null!;

    private int    _idLocalSeleccionado = 0;      // 0 = todos los locales
    private string _nombreLocalSeleccionado = "Todos los locales";
    private List<ArticuloListadoDetalleRow> _ultimosResultados = new();

    public VerArticulosListadoWindow()
    {
        _artRepo   = App.Services.GetRequiredService<IArticuloRepository>();
        _localRepo = App.Services.GetRequiredService<ILocalRepository>();
        _session   = App.Services.GetRequiredService<ISessionService>();

        Title  = "Ver Artículos";
        // Ancla real la fija MainWindow.AnclarAAreaDeContenido() — esto es solo el valor de
        // respaldo, igual que en VerArticulosWindow (ver comentario allá).
        WindowStyle = WindowStyle.None;
        ResizeMode  = ResizeMode.NoResize;
        MinWidth = 900; MinHeight = 400;
        var area = SystemParameters.WorkArea;
        Left = area.Left; Top = area.Top;
        Width = area.Width; Height = area.Height;
        Background = B(CLila);
        FontFamily = new FontFamily("Segoe UI");
        Content = Build();
        Loaded += async (_, _) => await CargarLocalesAsync();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2) { Buscar(); e.Handled = true; }
        else if (e.Key == Key.F8) { Imprimir(); e.Handled = true; }
        else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { Close(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
    }

    private UIElement Build()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var panelIzq = BuildPanelCriterios();
        Grid.SetColumn(panelIzq, 0); Grid.SetRow(panelIzq, 0); Grid.SetRowSpan(panelIzq, 2);
        root.Children.Add(panelIzq);

        var panelGrilla = BuildGrilla();
        Grid.SetColumn(panelGrilla, 1); Grid.SetRow(panelGrilla, 0);
        root.Children.Add(panelGrilla);

        var footer = BuildFooter();
        Grid.SetColumn(footer, 1); Grid.SetRow(footer, 1);
        root.Children.Add(footer);

        return root;
    }

    private UIElement BuildPanelCriterios()
    {
        var panel = new Border { Background = B(CLila), Padding = new Thickness(7, 7, 7, 7) };
        var sp = new StackPanel();

        var titulo = new Border
        {
            Background = B(CLilaOscuro), Padding = new Thickness(4, 4, 4, 4),
            Margin = new Thickness(0, 0, 0, 7),
        };
        titulo.Child = new TextBlock
        {
            Text = "CRITERIOS DE BÚSQUEDA", FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = B(CTexto), TextWrapping = TextWrapping.Wrap,
        };
        sp.Children.Add(titulo);

        // Buscador rápido por código o descripción — pedido explícito: además del rango
        // alfabético "Filtrar Descripción" (Desde/Hasta), un campo de texto libre que
        // encuentre el artículo escribiendo cualquier parte del código o del nombre.
        sp.Children.Add(Etiqueta("Buscar artículo (código o descripción)"));
        _txtBuscarArticulo = new TextBox
        {
            FontSize = 9, Padding = new Thickness(3, 2, 3, 2),
            Background = Brushes.White, BorderBrush = B(CBorde), BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 2),
        };
        _txtBuscarArticulo.KeyDown += (_, e) => { if (e.Key == Key.Enter) Buscar(); };
        sp.Children.Add(_txtBuscarArticulo);
        sp.Children.Add(BotonBuscar("Buscar artículo", Buscar, new Thickness(0, 0, 0, 6)));

        sp.Children.Add(Etiqueta("Locales"));
        _cboLocal = ComboEstilizado();
        _cboLocal.SelectionChanged += (_, _) => OnLocalSeleccionado();
        sp.Children.Add(_cboLocal);

        // Pedido explícito: antes solo se podía elegir local desplegando el combo — con 15+
        // locales, encontrar uno puntual a ojo era lento. Este campo filtra el combo en vivo
        // por nombre O por ID de local (ej. escribir "6" muestra "Depósito 1 San Juan Nep").
        sp.Children.Add(new TextBlock {
            Text = "Buscar por nombre o ID de local...", FontSize = 7.5, FontStyle = FontStyles.Italic,
            Foreground = B(Color.FromRgb(0x6A, 0x62, 0x9A)), Margin = new Thickness(0, 2, 0, 1),
        });
        _txtBuscarLocal = new TextBox {
            FontSize = 9, Padding = new Thickness(2, 1, 2, 1), Height = 17,
            Background = Brushes.White, BorderBrush = B(CBorde), BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 1, 0, 0),
        };
        _txtBuscarLocal.TextChanged += (_, _) => FiltrarComboLocal();
        _txtBuscarLocal.KeyDown += (_, e) => {
            if (e.Key != Key.Enter) return;
            if (_cboLocal.Items.Count == 1) { _cboLocal.SelectedIndex = 0; }
            else if (_cboLocal.Items.Count > 1) { _cboLocal.IsDropDownOpen = true; }
        };
        sp.Children.Add(_txtBuscarLocal);

        sp.Children.Add(Etiqueta("Filtrar Descripción", new Thickness(0, 6, 0, 2)));
        var rangoGrid = new Grid();
        rangoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rangoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rangoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rangoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lblDesde = new TextBlock { Text = "Desde", FontSize = 8, Foreground = B(CTexto), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) };
        Grid.SetColumn(lblDesde, 0); rangoGrid.Children.Add(lblDesde);
        _txtDesde = CampoTexto();
        Grid.SetColumn(_txtDesde, 1); rangoGrid.Children.Add(_txtDesde);

        var lblHasta = new TextBlock { Text = "Hasta", FontSize = 8, Foreground = B(CTexto), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 2, 0) };
        Grid.SetColumn(lblHasta, 2); rangoGrid.Children.Add(lblHasta);
        _txtHasta = CampoTexto();
        Grid.SetColumn(_txtHasta, 3); rangoGrid.Children.Add(_txtHasta);

        sp.Children.Add(rangoGrid);
        sp.Children.Add(BotonBuscar("Buscar", Buscar, new Thickness(0, 6, 0, 0)));

        // El glyph de tilde del CheckBox nativo de WPF no se estaba dibujando en este tema/
        // ambiente (bug real confirmado: tildado quedaba visualmente igual al desmarcado) —
        // en vez de seguir peleando con el ControlTemplate por defecto del sistema, se arma
        // el cuadradito a mano: un Border de 18x18 que, tildado, muestra una tilde propia
        // (Path en forma de "V") — así el estado nunca depende de un tema que puede fallar.
        var cajita = new Border
        {
            Width = 12, Height = 12,
            Background = Brushes.White, BorderBrush = B(CBorde), BorderThickness = new Thickness(1.2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var tilde = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 1.5,6 L 4.2,8.5 L 9.5,2.5"),
            Stroke = Brushes.White, StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Visibility = Visibility.Collapsed,
        };
        cajita.Child = tilde;

        _chkMostrarStock0 = new CheckBox
        {
            IsChecked = false, FontSize = 8,
            Foreground = B(CTexto), Background = Brushes.Transparent,
            ToolTip = "Por defecto se ocultan los artículos sin existencia (stock 0 o negativo). Activá esto para incluirlos en la búsqueda.",
        };
        // El CheckBox nativo queda de fondo (para heredar el hit-test y el evento
        // Checked/Unchecked de forma estándar) pero visualmente invisible; la cajita propia
        // es la única señal que ve el usuario.
        _chkMostrarStock0.Template = new ControlTemplate(typeof(CheckBox))
        {
            VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)),
        };

        var filaCheck = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Hand };
        filaCheck.Children.Add(cajita);
        filaCheck.Children.Add(new TextBlock
        {
            Text = "¿Mostrar artículos con Cantidad/Stock 0?",
            TextWrapping = TextWrapping.Wrap, FontSize = 8, Foreground = B(CTexto),
            Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 160,
        });
        filaCheck.MouseLeftButtonUp += (_, _) => _chkMostrarStock0.IsChecked = !(_chkMostrarStock0.IsChecked == true);

        var chkWrap = new Border
        {
            Background = Brushes.White, BorderBrush = B(CBorde), BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 6, 0, 0),
            Child = filaCheck,
        };
        void ActualizarEstiloCheckStock0()
        {
            bool marcado = _chkMostrarStock0.IsChecked == true;
            chkWrap.BorderBrush  = marcado ? B(CNaranjaBd) : B(CBorde);
            chkWrap.BorderThickness = new Thickness(marcado ? 2 : 1);
            cajita.Background = marcado ? B(CNaranja) : Brushes.White;
            cajita.BorderBrush = marcado ? B(CNaranjaBd) : B(CBorde);
            tilde.Visibility = marcado ? Visibility.Visible : Visibility.Collapsed;
        }
        _chkMostrarStock0.Checked   += (_, _) => ActualizarEstiloCheckStock0();
        _chkMostrarStock0.Unchecked += (_, _) => ActualizarEstiloCheckStock0();
        sp.Children.Add(chkWrap);

        // Bloque "Locales: Todos/Específico" — réplica tal cual del viejo, con su propio
        // botón Buscar independiente del filtro de arriba (ver captura de referencia).
        var grpLocales = new GroupBox
        {
            Header = "Locales", FontSize = 8, Foreground = B(CTexto),
            Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(4, 4, 4, 4),
            Background = B(CLila), BorderBrush = B(CBorde),
        };
        var grpVert = new StackPanel();

        // "Agrupar por..." corresponde al reporte que arma ESTE bloque (su botón "Buscar"
        // llama a GenerarReporteAgrupado, que usa _cboAgrupar) — antes estaba arriba, en el
        // bloque de la grilla en pantalla, que no lo usa para nada.
        grpVert.Children.Add(Etiqueta("Agrupar por...", new Thickness(0, 0, 0, 2)));
        _cboAgrupar = ComboEstilizado();
        _cboAgrupar.Items.Add(new ComboBoxItem { Content = "Categorías", Tag = "categorias" });
        _cboAgrupar.Items.Add(new ComboBoxItem { Content = "Proveedores", Tag = "proveedores" });
        _cboAgrupar.SelectedIndex = 0;
        grpVert.Children.Add(_cboAgrupar);

        var grpSp = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        grpSp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grpSp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var radiosSp = new StackPanel();

        _rbTodos = new RadioButton { Content = "Todos", GroupName = "locFiltro", IsChecked = true, FontSize = 8, Foreground = B(CTexto), Margin = new Thickness(0, 1, 0, 3) };
        _rbEspecifico = new RadioButton { Content = "Específico", GroupName = "locFiltro", FontSize = 8, Foreground = B(CTexto), Margin = new Thickness(0, 0, 0, 2) };
        radiosSp.Children.Add(_rbTodos);
        radiosSp.Children.Add(_rbEspecifico);

        // Mismo pedido que el combo de arriba: filtrar por nombre o ID antes de elegir, en
        // vez de desplegar y buscar a ojo entre 15+ locales. Este campo queda SIEMPRE
        // habilitado (antes se bloqueaba con "Todos" seleccionado, lo que impedía justamente
        // el gesto que el usuario pidió: escribir algo ahí para pasar a "Específico" solo).
        // Pedido explícito: este campo quedó demasiado chico tras achicar todo el panel —
        // se lo agranda un poco puntualmente (fuente y alto), sin tocar el resto del bloque.
        _txtBuscarLocalEspecifico = new TextBox {
            FontSize = 10, Padding = new Thickness(3, 2, 3, 2), Height = 21,
            Background = Brushes.White, BorderBrush = B(CBorde), BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 2),
        };
        _txtBuscarLocalEspecifico.TextChanged += (_, _) =>
        {
            // Escribir algo acá pasa el radio a "Específico" automáticamente; borrar todo
            // vuelve solo a "Todos" — pedido explícito, para no tener que tocar el radio a
            // mano antes de poder escribir.
            var conTexto = !string.IsNullOrWhiteSpace(_txtBuscarLocalEspecifico.Text);
            if (conTexto && _rbEspecifico.IsChecked != true) _rbEspecifico.IsChecked = true;
            else if (!conTexto && _rbTodos.IsChecked != true) _rbTodos.IsChecked = true;
            FiltrarComboLocalEspecifico();
        };
        _txtBuscarLocalEspecifico.KeyDown += (_, e) => {
            if (e.Key != Key.Enter) return;
            if (_cboLocalEspecifico.Items.Count == 1) { _cboLocalEspecifico.SelectedIndex = 0; }
            else if (_cboLocalEspecifico.Items.Count > 1) { _cboLocalEspecifico.IsDropDownOpen = true; }
        };
        radiosSp.Children.Add(_txtBuscarLocalEspecifico);

        _cboLocalEspecifico = ComboEstilizado();
        _cboLocalEspecifico.IsEnabled = false;
        radiosSp.Children.Add(_cboLocalEspecifico);
        _rbTodos.Checked       += (_, _) => { _cboLocalEspecifico.IsEnabled = false; };
        _rbEspecifico.Checked  += (_, _) => { _cboLocalEspecifico.IsEnabled = true; };
        // Volver a "Todos" a mano (clic directo en el radio, no solo por vaciar el texto)
        // también limpia el campo de búsqueda, para que ambos controles queden coherentes.
        _rbTodos.Checked       += (_, _) => { if (_txtBuscarLocalEspecifico.Text.Length > 0) _txtBuscarLocalEspecifico.Text = ""; };

        Grid.SetColumn(radiosSp, 0); grpSp.Children.Add(radiosSp);
        var btnBuscar2 = BotonBuscar("Buscar", GenerarReporteAgrupado, new Thickness(8, 0, 0, 0));
        btnBuscar2.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(btnBuscar2, 1); grpSp.Children.Add(btnBuscar2);

        grpVert.Children.Add(grpSp);
        grpLocales.Content = grpVert;
        sp.Children.Add(grpLocales);

        // El panel de criterios sumó campos nuevos (búsqueda de local) y con pantallas de
        // baja altura el contenido dejó de entrar completo, apretando/cortando visualmente el
        // bloque "Locales" de abajo. Con ScrollViewer, si no entra todo se puede desplazar en
        // vez de comprimirse.
        panel.Child = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = sp,
        };
        return panel;
    }

    // Achicado a pedido explícito del usuario (el panel de criterios no entraba completo,
    // pidió más chico dos veces) — fuentes, paddings y alturas reducidas al mínimo legible
    // en todos los controles de este panel.
    private static TextBlock Etiqueta(string txt, Thickness? margin = null) => new TextBlock
    {
        Text = txt, FontSize = 8, Foreground = B(CTexto), Margin = margin ?? new Thickness(0, 0, 0, 2),
    };

    private static ComboBox ComboEstilizado() => new ComboBox
    {
        FontSize = 9, Padding = new Thickness(3, 1, 3, 1),
        Background = Brushes.White, BorderBrush = B(CBorde), BorderThickness = new Thickness(1),
    };

    private TextBox CampoTexto() => new TextBox
    {
        FontSize = 9, Padding = new Thickness(2, 1, 2, 1),
        Background = Brushes.White, BorderBrush = B(CBorde), BorderThickness = new Thickness(1),
    };

    private Border BotonBuscar(string texto, Action accion, Thickness margin)
    {
        var contenido = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        contenido.Children.Add(new TextBlock { Text = "🔍", FontSize = 9, Margin = new Thickness(0, 0, 4, 0) });
        contenido.Children.Add(new TextBlock { Text = texto, FontSize = 9, Foreground = B(CTexto) });

        var btn = new Border
        {
            Background = Brushes.White, BorderBrush = B(CBorde), BorderThickness = new Thickness(1),
            Padding = new Thickness(7, 3, 7, 3), Margin = margin, Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = contenido,
        };
        var bgNormal = Brushes.White;
        var bgHover  = B(CLilaOscuro);
        btn.MouseEnter        += (_, _) => btn.Background = bgHover;
        btn.MouseLeave        += (_, _) => btn.Background = bgNormal;
        btn.MouseLeftButtonUp += (_, _) => accion();
        return btn;
    }

    private UIElement BuildGrilla()
    {
        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = B(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            RowBackground = Brushes.White,
            AlternatingRowBackground = B(CAlt),
            Background = Brushes.White,
            BorderThickness = new Thickness(0),
            CanUserAddRows = false, CanUserDeleteRows = false, CanUserReorderColumns = false,
            SelectionMode = DataGridSelectionMode.Single,
            RowHeaderWidth = 0,
            FontSize = 12.5,
            ColumnHeaderHeight = 40,
            // Modo "Todos los locales" agrega una columna por local (14+): sin esto, el
            // DataGrid comprime todas las columnas para que entren en el ancho visible en vez
            // de habilitar scroll horizontal, y los encabezados/valores quedan truncados a una
            // letra. Con scroll horizontal explícito, cada columna respeta el Width que se le
            // asignó y el usuario se desplaza para ver el resto.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var estiloHeader = new Style(typeof(DataGridColumnHeader));
        estiloHeader.Setters.Add(new Setter(Control.BackgroundProperty, B(CNaranja)));
        estiloHeader.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
        estiloHeader.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        estiloHeader.Setters.Add(new Setter(Control.BorderBrushProperty, B(CNaranjaBd)));
        estiloHeader.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        estiloHeader.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        estiloHeader.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        estiloHeader.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        _grid.ColumnHeaderStyle = estiloHeader;

        var estiloCelda = new Style(typeof(DataGridCell));
        estiloCelda.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        estiloCelda.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        var triggerSel = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        triggerSel.Setters.Add(new Setter(Control.BackgroundProperty, B(CAzulSel)));
        triggerSel.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        estiloCelda.Triggers.Add(triggerSel);
        _grid.CellStyle = estiloCelda;

        AplicarColumnasParaModo(todosLosLocales: true);

        var contenedor = new Grid();
        contenedor.Children.Add(_grid);
        contenedor.Children.Add(BuildOverlayCargando());

        return contenedor;
    }

    // El "Buscando..." de la barra de estado (abajo, chico, al lado de botones) pasaba
    // desapercibido — sobre todo en el modo "todos los locales", donde la búsqueda tarda más
    // por el desglose de stock por 14+ locales. Este overlay tapa la grilla mientras carga,
    // con un spinner animado, para que quede obvio que hay que esperar.
    private UIElement BuildOverlayCargando()
    {
        _overlayCargando = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            Visibility = Visibility.Collapsed,
        };

        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        var spinner = new System.Windows.Shapes.Path
        {
            Width = 40, Height = 40,
            Stroke = B(CAzulSel), StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            Data = System.Windows.Media.Geometry.Parse("M 20,2 A 18,18 0 1 1 4.4,11"),
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        _spinnerRotacion = new RotateTransform(0);
        spinner.RenderTransform = _spinnerRotacion;
        sp.Children.Add(spinner);

        sp.Children.Add(new TextBlock
        {
            Text = "Buscando artículos...", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = B(CTexto), Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        _overlayCargando.Child = sp;
        return _overlayCargando;
    }

    private void MostrarOverlayCargando(bool visible)
    {
        _overlayCargando.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible) { _spinnerRotacion.BeginAnimation(RotateTransform.AngleProperty, null); return; }

        var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(0.9)))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        _spinnerRotacion.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    // "Específico" (un local elegido) y "Todos los locales" necesitan columnas distintas:
    // con un local puntual, P.Promo es un solo número sin ambigüedad. Con todos los locales,
    // cada uno puede tener su propio precio (y promo) distinto — mostrar un solo P.Promo ahí
    // mezclaría 14 locales en un número que no dice de cuál es, así que en ese modo se
    // reemplaza por una columna de Stock por cada local (réplica del listado del sistema
    // viejo), sin columna de promo.
    private void AplicarColumnasParaModo(bool todosLosLocales)
    {
        _grid.Columns.Clear();
        // Los headers de local pueden ocupar 2 líneas (TextWrapping) — con la altura fija de
        // "específico" (una sola línea) el texto de abajo quedaba cortado.
        _grid.ColumnHeaderHeight = todosLosLocales ? 52 : 40;

        var colId = new DataGridTextColumn
        {
            Header = "ID", Binding = new System.Windows.Data.Binding("Id"),
            Width = new DataGridLength(60), CellStyle = CeldaAlineadaDerecha(),
        };
        var colCodigo = new DataGridTextColumn { Header = "Código", Binding = new System.Windows.Data.Binding("Codigo"), Width = new DataGridLength(110) };
        // En modo "todos los locales" hay 14+ columnas más al final — una Descripción a Star
        // ahí competía por espacio contra todas esas columnas y terminaba comprimida a una
        // letra en vez de dejar que el scroll horizontal se encargue. Fija a un ancho legible
        // en ese modo; en "específico" (menos columnas) sigue ocupando el espacio sobrante.
        var colDesc = new DataGridTextColumn
        {
            Header = "Descripción", Binding = new System.Windows.Data.Binding("Descripcion"),
            Width = todosLosLocales ? new DataGridLength(240) : new DataGridLength(1, DataGridLengthUnitType.Star),
        };
        var colPCosto = new DataGridTextColumn
        {
            Header = "P.Costo", Binding = new System.Windows.Data.Binding("PCosto") { StringFormat = "N0" },
            Width = new DataGridLength(110), CellStyle = CeldaAlineadaDerecha(),
        };
        var colPVenta = new DataGridTextColumn
        {
            Header = "P.Venta", Binding = new System.Windows.Data.Binding("PVenta") { StringFormat = "N0" },
            Width = new DataGridLength(110), CellStyle = CeldaAlineadaDerecha(),
        };

        _grid.Columns.Add(colId);
        _grid.Columns.Add(colCodigo);
        _grid.Columns.Add(colDesc);
        _grid.Columns.Add(colPCosto);

        if (!todosLosLocales)
        {
            _grid.Columns.Add(colPVenta);
            // P.Promo se sacó de este modo (pedido explícito) — quedaba redundante junto a
            // P.Venta/Contado para un local puntual; el converter y su lógica de vigencia
            // (PrecioPromoConverter) quedan sin uso acá pero se mantienen para no romper el
            // modo "todos los locales" en caso de que se reincorpore ahí más adelante.
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Stock", Binding = new System.Windows.Data.Binding("Stock") { StringFormat = "N0" },
                Width = new DataGridLength(80), CellStyle = CeldaAlineadaDerecha(),
            });
        }
        else
        {
            // Máximo de cuotas del artículo (ARTICULOS.MAXCUOTA) — mismo valor en todos los
            // locales.
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Max Cuota", Binding = new System.Windows.Data.Binding("MaxCuota"),
                Width = new DataGridLength(80), CellStyle = CeldaAlineadaDerecha(),
            });

            // Orden pedido explícitamente: Descripción → CONTADO → P.Venta (antes P.Venta
            // iba justo después de Descripción/P.Costo, junto con el resto de los modos —
            // acá CONTADO se agrega primero y P.Venta se corre para después de esa columna,
            // en vez de en el bloque común de arriba).
            // Confirmado contra el listado del sistema viejo (comparando ID/artículo/monto
            // real en la base): su columna "CONTADO" coincide con PRICES.CONTADO crudo, no
            // con PVENTA ni PPROMO — se agrega solo acá, en "todos los locales" (que es el
            // modo donde el usuario pidió verla), como columna fija igual que P.Costo/P.Venta
            // (el precio contado no suele variar entre locales, mismo criterio ya usado ahí).
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "CONTADO", Binding = new System.Windows.Data.Binding("Contado") { StringFormat = "N0" },
                Width = new DataGridLength(110), CellStyle = CeldaAlineadaDerecha(),
            });
            _grid.Columns.Add(colPVenta);

            // "Stock" del DTO ya es la suma entre todos los locales (SUM(p2.S) sin filtro de
            // IDLOCAL en ObtenerListadoDetalladoAsync) — se muestra acá como "Stock Total",
            // al lado de CONTADO, además del desglose por local que sigue más a la derecha.
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Stock Total", Binding = new System.Windows.Data.Binding("Stock") { StringFormat = "N0" },
                Width = new DataGridLength(95), CellStyle = CeldaAlineadaDerecha(),
            });

            foreach (var local in _locales)
            {
                // Nombre de local envuelto en 2 líneas en vez de truncado a "C..." — el
                // header base (estiloHeader) no wrappea texto plano de un string, necesita un
                // TextBlock real con TextWrapping, mismo motivo que headerParcial más abajo.
                var headerLocal = new TextBlock
                {
                    Text = local.NombreLocal, TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center, FontSize = 10.5,
                };
                _grid.Columns.Add(new DataGridTextColumn
                {
                    Header = headerLocal,
                    Binding = new System.Windows.Data.Binding(".") { Converter = new StockPorLocalConverter { IdLocal = local.IdLocal } },
                    Width = new DataGridLength(90), CellStyle = CeldaAlineadaDerecha(),
                });
            }
        }

        if (!todosLosLocales)
        {
            // "Parcial" no es un precio: es P.Costo × Stock (el valor a costo de lo que hay
            // en existencia de ese artículo). En "todos los locales" ya hay una columna de
            // Stock por cada local — un PARCIAL único ahí sumaría el costo a través de todos
            // los locales sin decir de cuál, y en la práctica siempre da 0 salvo que el
            // artículo tenga stock en el local elegido en otra búsqueda — se saca en ese modo
            // (pedido explícito del usuario). Header necesita un TextBlock real (no un string
            // plano) para que TextWrapping/salto de línea funcione.
            var headerParcial = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Right,
                Inlines =
                {
                    new System.Windows.Documents.Run("PARCIAL") { FontWeight = FontWeights.Bold },
                    new System.Windows.Documents.LineBreak(),
                    new System.Windows.Documents.Run("(P.Costo × Stock)") { FontSize = 9.5, FontWeight = FontWeights.Normal },
                },
                ToolTip = "Valor a costo de lo que hay en existencia de este artículo (no es un precio promocional).",
            };
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = headerParcial,
                Binding = new System.Windows.Data.Binding("Parcial") { StringFormat = "N0" },
                Width = new DataGridLength(130), CellStyle = CeldaAlineadaDerecha(),
            });
        }
    }

    private Style CeldaAlineadaDerecha()
    {
        var estilo = new Style(typeof(DataGridCell), _grid.CellStyle);
        estilo.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
        return estilo;
    }

    private UIElement BuildFooter()
    {
        var foot = new Border
        {
            Background      = B(CLila),
            BorderBrush     = B(CBorde),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding         = new Thickness(12, 8, 12, 8),
        };
        var dp = new DockPanel();

        var atajos = new StackPanel();
        atajos.Children.Add(new TextBlock { Text = "F2: Buscar     F10: Agrupado", FontSize = 10.5, Foreground = B(CTexto) });
        atajos.Children.Add(new TextBlock { Text = "F8: Imprimir   Ctrl+S: Cerrar", FontSize = 10.5, Foreground = B(CTexto), Margin = new Thickness(0, 2, 0, 0) });
        DockPanel.SetDock(atajos, Dock.Left);
        dp.Children.Add(atajos);

        var lblEstadoWrap = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        _lblEstado = new TextBlock { FontSize = 11, Foreground = B(CTexto), FontWeight = FontWeights.SemiBold };
        lblEstadoWrap.Children.Add(_lblEstado);
        dp.Children.Add(lblEstadoWrap);

        // Pedido explícito: aclarar qué imprime "Imprimir" exactamente — a diferencia del
        // "Buscar" del cuadro amarillo (que arma un reporte agrupado por categoría aparte),
        // este reproduce en papel lo que ya se está viendo en la grilla, tal cual está ahora.
        // Al lado del botón (no arriba) — arriba quedaba tapado por la marca de agua de
        // "Activar Windows" en pantallas más chicas.
        var botones = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        botones.Children.Add(new TextBlock
        {
            // Flecha hacia arriba: apunta a la tabla de la grilla, que queda arriba del
            // footer — pedido explícito para dejar más claro a qué "esta tabla" se refiere.
            Text = "⬆ Imprime esta\nmisma tabla",
            FontSize = 9.5, FontStyle = FontStyles.Italic, TextAlignment = TextAlignment.Right,
            Foreground = B(Color.FromRgb(0x6A, 0x62, 0x9A)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        var btnImprimir = BotonFooter("🖨  Imprimir", Imprimir);
        btnImprimir.ToolTip = "Imprime la misma tabla que estás viendo en pantalla ahora, con las mismas columnas y los mismos artículos.";
        botones.Children.Add(btnImprimir);
        botones.Children.Add(BotonFooter("✕  Cerrar", Close, new Thickness(10, 0, 0, 0)));
        DockPanel.SetDock(botones, Dock.Right);
        dp.Children.Add(botones);

        foot.Child = dp;
        return foot;
    }

    private Border BotonFooter(string texto, Action accion, Thickness? margin = null)
    {
        var txt = new TextBlock
        {
            Text = texto, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 12.5,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        var bg = B(Color.FromRgb(0x64, 0x74, 0x84));
        var bgHover = B(Color.FromRgb(0x4B, 0x5A, 0x69));
        var btn = new Border
        {
            Background = bg, CornerRadius = new CornerRadius(5), Padding = new Thickness(16, 8, 16, 8),
            Margin = margin ?? new Thickness(0), Cursor = Cursors.Hand, Child = txt,
        };
        btn.MouseEnter        += (_, _) => btn.Background = bgHover;
        btn.MouseLeave        += (_, _) => btn.Background = bg;
        btn.MouseLeftButtonUp += (_, _) => accion();
        return btn;
    }

    private async Task CargarLocalesAsync()
    {
        var todos = (await _localRepo.ListarTodosAsync()).ToList();
        _esAdmin  = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;

        _cboLocal.Items.Clear();
        if (_esAdmin)
        {
            // El local del usuario logueado va primero — tanto en el combo como en las
            // columnas de "Stock por local" del modo "todos los locales" (que recorren
            // _locales en este mismo orden), para que sea lo primero que vea sin tener que
            // buscarlo entre los demás.
            var idLocalSesion = _session.LocalActual?.IdLocal;
            _locales = idLocalSesion.HasValue
                ? todos.OrderByDescending(l => l.IdLocal == idLocalSesion.Value).ToList()
                : todos;
            _cboLocal.Items.Add(new ComboBoxItem { Content = "Todos los locales", Tag = 0 });
            foreach (var l in _locales)
                _cboLocal.Items.Add(new ComboBoxItem { Content = l.NombreLocal, Tag = l.IdLocal });
            _cboLocal.SelectedIndex = 0;
            _cboLocal.IsEnabled = true;
        }
        else
        {
            var idLocalSesion = _session.LocalActual?.IdLocal ?? 0;
            var localSesion = todos.FirstOrDefault(l => l.IdLocal == idLocalSesion);
            _locales = localSesion != null ? new List<Local> { localSesion } : new List<Local>();
            var nombre = localSesion?.NombreLocal ?? _session.LocalActual?.NombreLocal ?? "Mi local";
            _cboLocal.Items.Add(new ComboBoxItem { Content = nombre, Tag = idLocalSesion });
            _cboLocal.SelectedIndex = 0;
            _cboLocal.IsEnabled = false;

            _idLocalSeleccionado     = idLocalSesion;
            _nombreLocalSeleccionado = nombre;
        }

        // Bloque "Locales: Todos/Específico" — misma excepción admin/PuedeVerTodosLosLocales
        // que el combo de arriba: un usuario sin ese permiso solo puede generar el reporte
        // agrupado de SU propio local, nunca elegir otro.
        _cboLocalEspecifico.Items.Clear();
        foreach (var l in _locales)
            _cboLocalEspecifico.Items.Add(new ComboBoxItem { Content = l.NombreLocal, Tag = l.IdLocal });
        if (_cboLocalEspecifico.Items.Count > 0)
            _cboLocalEspecifico.SelectedIndex = 0;

        if (!_esAdmin)
        {
            _rbTodos.IsChecked      = false;
            _rbEspecifico.IsChecked = true;
            _rbTodos.IsEnabled      = false;
            _rbEspecifico.IsEnabled = false;
            _cboLocalEspecifico.IsEnabled = false;
            _txtBuscarLocalEspecifico.IsEnabled = false;
        }
    }

    private void OnLocalSeleccionado()
    {
        if (_cboLocal.SelectedItem is not ComboBoxItem item) return;
        _idLocalSeleccionado     = (int)(item.Tag ?? 0);
        _nombreLocalSeleccionado = item.Content?.ToString() ?? "Todos los locales";
    }

    // Filtra _cboLocal en vivo por nombre (contiene) o por ID de local (coincidencia exacta,
    // ej. "6" encuentra el local con IdLocal=6) — reconstruye el combo a partir de _locales
    // (la lista completa ya cargada), preservando "Todos los locales" cuando corresponde.
    private void FiltrarComboLocal()
    {
        var termino = _txtBuscarLocal.Text.Trim();
        var seleccionActual = (_cboLocal.SelectedItem as ComboBoxItem)?.Tag as int?;

        _cboLocal.Items.Clear();
        if (string.IsNullOrEmpty(termino))
        {
            if (_esAdmin) _cboLocal.Items.Add(new ComboBoxItem { Content = "Todos los locales", Tag = 0 });
            foreach (var l in _locales)
                _cboLocal.Items.Add(new ComboBoxItem { Content = l.NombreLocal, Tag = l.IdLocal });

            // Al borrar el filtro, se vuelve directo a "Todos los locales" en vez de
            // conservar el local que estaba elegido — pedido explícito: limpiar la búsqueda
            // tiene que resetear también la selección, no solo repoblar la lista completa.
            if (_esAdmin) _cboLocal.SelectedIndex = 0;
            return;
        }

        var porId = int.TryParse(termino, out var idBuscado);
        var coincidencias = _locales.Where(l =>
            l.NombreLocal.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
            (porId && l.IdLocal == idBuscado));
        foreach (var l in coincidencias)
            _cboLocal.Items.Add(new ComboBoxItem { Content = l.NombreLocal, Tag = l.IdLocal });

        // Mantiene la selección previa si sigue en la lista filtrada.
        foreach (ComboBoxItem it in _cboLocal.Items)
        {
            if ((int?)it.Tag == seleccionActual) { _cboLocal.SelectedItem = it; break; }
        }
        // Si no hay selección previa válida y el filtro dejó una única coincidencia, se
        // selecciona sola — sin esto el combo quedaba vacío/cerrado tras filtrar (aunque
        // tuviera 1 sola opción cargada), dando la impresión de que "no actualizó".
        if (_cboLocal.SelectedItem == null && _cboLocal.Items.Count == 1)
            _cboLocal.SelectedIndex = 0;
    }

    // Mismo filtrado que FiltrarComboLocal, aplicado al combo del bloque "Locales:
    // Todos/Específico" — no tiene opción "Todos los locales" (ese bloque siempre elige un
    // local puntual cuando está en modo "Específico").
    private void FiltrarComboLocalEspecifico()
    {
        var termino = _txtBuscarLocalEspecifico.Text.Trim();
        var seleccionActual = (_cboLocalEspecifico.SelectedItem as ComboBoxItem)?.Tag as int?;

        _cboLocalEspecifico.Items.Clear();
        IEnumerable<Local> fuente = _locales;
        if (!string.IsNullOrEmpty(termino))
        {
            var porId = int.TryParse(termino, out var idBuscado);
            fuente = _locales.Where(l =>
                l.NombreLocal.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                (porId && l.IdLocal == idBuscado));
        }
        foreach (var l in fuente)
            _cboLocalEspecifico.Items.Add(new ComboBoxItem { Content = l.NombreLocal, Tag = l.IdLocal });

        foreach (ComboBoxItem it in _cboLocalEspecifico.Items)
        {
            if ((int?)it.Tag == seleccionActual) { _cboLocalEspecifico.SelectedItem = it; break; }
        }
        if (_cboLocalEspecifico.SelectedItem == null && _cboLocalEspecifico.Items.Count > 0)
            _cboLocalEspecifico.SelectedIndex = 0;
    }

    private void Buscar()
    {
        var agruparPor = (_cboAgrupar.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "categorias";
        var desde = _txtDesde.Text.Trim();
        var hasta = _txtHasta.Text.Trim();
        var idLocal = _idLocalSeleccionado;
        var termino = _txtBuscarArticulo.Text.Trim();

        MostrarEstado("Buscando...");
        _ = BuscarAsync(agruparPor, desde, hasta, idLocal, termino);
    }

    private void MostrarEstado(string texto) => _lblEstado.Text = texto;

    private async Task BuscarAsync(string agruparPor, string desde, string hasta, int idLocal, string? termino = null)
    {
        MostrarOverlayCargando(true);
        try
        {
            var filas = (await _artRepo.ObtenerListadoDetalladoAsync(agruparPor, desde, hasta, idLocal, termino)).ToList();
            // Tildado ("Mostrar artículos con Cantidad/Stock 0") no filtra nada acá — incluye
            // stock en 0 Y negativo, tal cual viene de la base. Destildado excluye ambos
            // (solo Stock > 0). No hay ningún otro filtro de stock aplicado en otro lado.
            if (_chkMostrarStock0.IsChecked != true)
                filas = filas.Where(f => f.Stock > 0).ToList();

            // Las columnas cambian según el modo (una por local vs. P.Promo+Stock), así que
            // se reconstruyen antes de asignar ItemsSource — si no, StockIndexConverter
            // quedaría leyendo índices de locales de una búsqueda anterior.
            AplicarColumnasParaModo(todosLosLocales: idLocal == 0);

            _ultimosResultados = filas;
            _grid.ItemsSource = filas;
            MostrarEstado(filas.Count == 0
                ? "No se encontraron artículos con los criterios indicados."
                : $"{filas.Count:N0} artículos encontrados.");
        }
        catch (Exception ex)
        {
            MostrarEstado("Ocurrió un error al buscar.");
            MessageBox.Show($"Error al buscar artículos:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            MostrarOverlayCargando(false);
        }
    }

    private void Imprimir()
    {
        if (_ultimosResultados.Count == 0)
        {
            MessageBox.Show("No hay resultados para imprimir. Realice una búsqueda primero.",
                "Ver Artículos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // El botón "Imprimir" (arriba) refleja lo que se ve en pantalla — misma tabla plana
        // que la grilla, en ambos modos (Específico y Todos los locales), en vez del reporte
        // agrupado por categoría. Ese reporte agrupado queda EXCLUSIVO del botón "Buscar" del
        // cuadro amarillo (GenerarReporteAgrupado) — pedido explícito del usuario, no se toca.
        MostrarEstado("Generando impresión...");
        _ = ImprimirTablaAsync();
    }

    private async Task ImprimirTablaAsync()
    {
        try
        {
            bool todosLosLocales = _idLocalSeleccionado == 0;
            var (impresoraReporte, _) = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerImpresoraAsync("reporte");

            var pagina = new ArticulosListadoTablaPagina
            {
                Filas           = _ultimosResultados,
                Locales         = todosLosLocales ? _locales : new List<Local>(),
                TodosLosLocales = todosLosLocales,
                FechaImp        = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Usuario         = _session.UsuarioActual?.NombreUsuario ?? "",
                Subtitulo       = todosLosLocales
                    ? "Lista de artículos — todos los locales"
                    : $"Lista de artículos — local {_nombreLocalSeleccionado}",
                Impresora       = impresoraReporte,
                LogoPath        = ArticulosListadoTablaPagina.ResolverLogoPath(),
                TotalArticulos  = _ultimosResultados.Count,
            };

            MostrarEstado($"{_ultimosResultados.Count:N0} artículos encontrados.");
            new ArticulosListadoTablaPreviewWindow(pagina) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            MostrarEstado("Ocurrió un error al generar la impresión.");
            MessageBox.Show($"Error al generar el listado:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Botón "Buscar" del bloque "Locales: Todos/Específico" — a diferencia del botón de
    // arriba (que llena la grilla en pantalla), este genera directamente el reporte
    // impreso agrupado por categoría/proveedor, usando el local elegido en ESTE bloque
    // (independiente del combo "Locales" de arriba) — réplica exacta del comportamiento
    // del sistema viejo (ver captura de referencia: "Lista de artículos... filtrado por
    // local 4" con encabezados de categoría en rojo).
    private void GenerarReporteAgrupado()
    {
        var agruparPor = (_cboAgrupar.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "categorias";
        var desde = _txtDesde.Text.Trim();
        var hasta = _txtHasta.Text.Trim();

        // "Todos los locales": pedido explícito 2026-08-11 — en vez del listado con una sola
        // columna de stock (agregado de los 15 locales), se genera el reporte "Stock por
        // Local" con una columna de stock POR CADA local, para ver de un vistazo cómo se
        // reparte el inventario entre sucursales. "Específico" sigue igual que antes.
        if (_rbTodos.IsChecked == true)
        {
            MostrarEstado("Generando reporte...");
            _ = ImprimirStockPorLocalAsync(agruparPor, desde, hasta);
            return;
        }

        int idLocal = 0;
        string nombreLocal = "todos los locales";
        if (_rbEspecifico.IsChecked == true && _cboLocalEspecifico.SelectedItem is ComboBoxItem item)
        {
            idLocal = (int)(item.Tag ?? 0);
            nombreLocal = item.Content?.ToString() ?? $"local {idLocal}";
        }

        MostrarEstado("Generando reporte...");
        _ = ImprimirAsync(agruparPor, desde, hasta, idLocal, nombreLocal);
    }

    private async Task ImprimirStockPorLocalAsync(string agruparPor, string desde, string hasta, string? termino = null)
    {
        try
        {
            var filasAgrupadas = (await _artRepo.ObtenerListadoAgrupadoConStockPorLocalAsync(agruparPor, desde, hasta, termino)).ToList();
            if (_chkMostrarStock0.IsChecked != true)
                filasAgrupadas = filasAgrupadas.Where(f => f.Stock > 0).ToList();

            var agruparTxt = agruparPor == "proveedores" ? "proveedores" : "categorías";
            var subtitulo = $"Artículos agrupados por {agruparTxt} · unidades en existencia en cada local";

            var (impresoraReporte, _) = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerImpresoraAsync("reporte");

            var totalPorLocal = new decimal[_locales.Count];
            foreach (var f in filasAgrupadas)
            {
                var porLocalDicc = f.StockPorLocal.ToDictionary(s => (int)s.IdLocal, s => s.S);
                for (int i = 0; i < _locales.Count; i++)
                    totalPorLocal[i] += porLocalDicc.TryGetValue(_locales[i].IdLocal, out var s) ? s : 0;
            }
            var totalGeneral = totalPorLocal.Sum();

            var pagina = new ArticulosStockPorLocalPagina
            {
                Filas          = ArticulosStockPorLocalPagina.Aplanar(filasAgrupadas, _locales),
                Locales        = _locales,
                FechaImp       = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Usuario        = _session.UsuarioActual?.NombreUsuario ?? "",
                Subtitulo      = subtitulo,
                Impresora      = impresoraReporte,
                LogoPath       = ArticulosStockPorLocalPagina.ResolverLogoPath(),
                TotalArticulos = filasAgrupadas.Count,
                TotalPorLocal  = totalPorLocal,
                TotalGeneral   = totalGeneral,
            };

            MostrarEstado($"{filasAgrupadas.Count:N0} artículos encontrados.");
            new ArticulosStockPorLocalPreviewWindow(pagina) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            MostrarEstado("Ocurrió un error al generar la impresión.");
            MessageBox.Show($"Error al generar el listado:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task ImprimirAsync(string agruparPor, string desde, string hasta, int idLocal, string nombreLocal, string? termino = null)
    {
        try
        {
            var filasAgrupadas = (await _artRepo.ObtenerListadoAgrupadoAsync(agruparPor, desde, hasta, idLocal, termino)).ToList();
            if (_chkMostrarStock0.IsChecked != true)
                filasAgrupadas = filasAgrupadas.Where(f => f.Stock > 0).ToList();

            var agruparTxt = agruparPor == "proveedores" ? "proveedores" : "categorías";
            var localTxt = idLocal > 0 ? $"local {nombreLocal}" : "todos los locales";
            var subtitulo = $"Lista de artículos agrupado por {agruparTxt} y filtrado por {localTxt}";

            // Sin esto, Impresora quedaba en "" siempre — ArticulosListadoImpresora.ImprimirAsync
            // trata cualquier Impresora vacía como "no hay impresora configurada" y fuerza el
            // diálogo "Guardar PDF" (esPdf = string.IsNullOrEmpty(p.Impresora) || ...), aunque
            // el usuario ya hubiera configurado una impresora física para "reportes" en esta PC
            // (Herramientas > Impresoras, tabla IMPRESORAS.NOMBRE_REPORTE) — bug real reportado:
            // "ya seleccioné la impresora para reportes y al generar el reporte me vuelve a
            // pedir guardar PDF".
            var (impresoraReporte, _) = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerImpresoraAsync("reporte");

            // Totales del listado (no de una sola página) — suma de Stock y del valor de
            // inventario a costo y a venta (Stock × Precio) de todos los artículos filtrados.
            var totalStock = filasAgrupadas.Sum(f => f.Stock);
            var totalCosto = filasAgrupadas.Sum(f => f.Stock * f.PCosto);
            var totalVenta = filasAgrupadas.Sum(f => f.Stock * f.PVenta);

            var pagina = new ArticulosListadoPagina
            {
                Filas          = ArticulosListadoPagina.Aplanar(filasAgrupadas),
                FechaImp       = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Usuario        = _session.UsuarioActual?.NombreUsuario ?? "",
                Subtitulo      = subtitulo,
                Impresora      = impresoraReporte,
                LogoPath       = ArticulosListadoPagina.ResolverLogoPath(),
                TotalArticulos = filasAgrupadas.Count,
                TotalStock     = totalStock,
                TotalCosto     = totalCosto,
                TotalVenta     = totalVenta,
            };

            MostrarEstado($"{filasAgrupadas.Count:N0} artículos encontrados.");
            new ArticulosListadoPreviewWindow(pagina) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            MostrarEstado("Ocurrió un error al generar la impresión.");
            MessageBox.Show($"Error al generar el listado:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

// PPromo llega en 0 cuando el artículo no tiene promoción vigente (ver filtro por
// INICIO/FIN en ArticuloRepository.ObtenerListadoDetalladoAsync) — se muestra "—" en vez
// de "0" para no leerse como un precio promocional real.
public sealed class PrecioPromoConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is decimal d && d > 0 ? d.ToString("N0", culture) : "—";

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}

// Columna dinámica de "Stock por local" en modo "Todos los locales": una instancia de este
// converter por local (IdLocal fijo), busca ese local puntual dentro de
// ArticuloListadoDetalleRow.StockPorLocal en vez de asumir una posición fija de array —
// StockPorLocal solo trae los locales que tienen fila en PRICES para ese artículo, así que
// un local sin stock cargado ahí se muestra como 0, no se cae el binding.
public sealed class StockPorLocalConverter : System.Windows.Data.IValueConverter
{
    public int IdLocal { get; set; }

    public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not ArticuloListadoDetalleRow row) return "0";
        var stock = row.StockPorLocal.FirstOrDefault(s => s.IdLocal == IdLocal)?.S ?? 0;
        return stock.ToString("N0", culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
