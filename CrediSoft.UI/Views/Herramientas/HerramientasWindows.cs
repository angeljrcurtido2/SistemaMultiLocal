using CrediSoft.Core.Services;
using CrediSoft.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Herramientas;

// ══════════════════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN DE PUNITORIO
// ══════════════════════════════════════════════════════════════════════════════
public class PunitorioWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtValorPunit = null!;
    private TextBox   _txtValorInf   = null!;
    private int       _idConfig;

    public PunitorioWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Configuración de Punitorio"; Width = 400; Height = 250;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new StackPanel { Margin = new Thickness(20) };

        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF8C00")!,
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 16)
        };
        hdr.Child = new TextBlock { Text = "Configuración de Punitorio", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold };
        root.Children.Add(hdr);

        root.Children.Add(new TextBlock { Text = "Valor Punitorio (%):", Margin = new Thickness(0, 8, 0, 2), Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
        _txtValorPunit = new TextBox { Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 0, 0, 8) };
        root.Children.Add(_txtValorPunit);

        root.Children.Add(new TextBlock { Text = "Valor Informconf (%):", Margin = new Thickness(0, 0, 0, 2), Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
        _txtValorInf = new TextBox { Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 0, 0, 16) };
        root.Children.Add(_txtValorInf);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnGuardar = new Button { Content = "✔ Guardar", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#27AE60")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnGuardar.Click += async (_, _) => await Guardar();
        var btnCerrar = new Button { Content = "Cerrar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        btnRow.Children.Add(btnGuardar); btnRow.Children.Add(btnCerrar);
        root.Children.Add(btnRow);

        Content = root;
    }

    private async Task Cargar()
    {
        using var conn = _db.Create();
        var p = new DynamicParameters();
        p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        var row = await conn.QueryFirstOrDefaultAsync<dynamic>("CARGAR_PUNITORIO_CS", p, commandType: CommandType.StoredProcedure);
        if (row != null)
        {
            _idConfig = (int)row.ID_CONFIG;
            _txtValorPunit.Text = ((decimal)row.VALOR_PUNITORIO).ToString("F2");
            _txtValorInf.Text   = ((decimal)row.VALOR_INFORCONF).ToString("F2");
        }
    }

    private async Task Guardar()
    {
        if (!decimal.TryParse(_txtValorPunit.Text, out var vPunit) ||
            !decimal.TryParse(_txtValorInf.Text,   out var vInf))
        {
            MessageBox.Show("Ingrese valores numéricos válidos.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            using var conn = _db.Create();
            // ACTUALIZAR_PUNITORIO_CS(@Id, @Valor, @msg) — actualizar VALOR_PUNITORIO
            var p1 = new DynamicParameters();
            p1.Add("@Id",    (byte)_idConfig);
            p1.Add("@Valor", vPunit);
            p1.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("ACTUALIZAR_PUNITORIO_CS", p1, commandType: CommandType.StoredProcedure);

            // Actualizar VALOR_INFORCONF directamente
            await conn.ExecuteAsync(
                "UPDATE CONFIGURACION SET VALOR_INFORCONF=@v WHERE ID_CONFIG=@id",
                new { v = vInf, id = _idConfig });

            MessageBox.Show("Configuración actualizada correctamente.", "Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  NOTA DE CRÉDITO (Herramientas)
// ══════════════════════════════════════════════════════════════════════════════
public class NotaCreditoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtIdCab   = null!;
    private DataGrid  _grid       = null!;
    private TextBlock _lblInfo    = null!;

    public NotaCreditoWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Nota de Crédito"; Width = 900; Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#C0392B")!,
            Padding = new Thickness(12, 6, 12, 6)
        };
        hdr.Child = new TextBlock { Text = "Nota de Crédito — Cuotas Generadas", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 15, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnC = new Button { Content = "Cerrar", Height = 28, Padding = new Thickness(10, 0, 10, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close(); bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        var filterBar = new DockPanel { Margin = new Thickness(8) };
        filterBar.Children.Add(new TextBlock { Text = "N° Cabecera Venta:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _txtIdCab = new TextBox { Padding = new Thickness(4, 3, 4, 3), Width = 100, Margin = new Thickness(0, 0, 6, 0) };
        _txtIdCab.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Cargar(); };
        var btnB = new Button { Content = "Buscar", Height = 28, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF8C00")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnB.Click += async (_, _) => await Cargar();
        filterBar.Children.Add(_txtIdCab); filterBar.Children.Add(btnB);
        _lblInfo = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Foreground = System.Windows.Media.Brushes.Gray };
        filterBar.Children.Add(_lblInfo);
        DockPanel.SetDock(filterBar, Dock.Top); root.Children.Add(filterBar);

        _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.MistyRose,
            Margin = new Thickness(8, 0, 8, 0) };
        _grid.Columns.Add(new DataGridTextColumn { Header = "N° Sol.",    Binding = new System.Windows.Data.Binding("NSOLICITUD"),  Width = 90 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Cliente",    Binding = new System.Windows.Data.Binding("CLIENTE"),     Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Comprobante",Binding = new System.Windows.Data.Binding("COMPROBANTE"), Width = 110 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "N°Cuota",    Binding = new System.Windows.Data.Binding("NCUOTA"),      Width = 65 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Monto",      Binding = new System.Windows.Data.Binding("MONTO"),       Width = 80 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Entrega",    Binding = new System.Windows.Data.Binding("ENTREGA"),     Width = 70 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Vence",      Binding = new System.Windows.Data.Binding("VTO"),         Width = 90 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Estado",     Binding = new System.Windows.Data.Binding("ESTADO"),      Width = 90 });
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar()
    {
        if (!int.TryParse(_txtIdCab.Text.Trim(), out var idCab)) { MessageBox.Show("Ingrese un N° de cabecera válido."); return; }
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@Idcab", idCab);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var rows = (await conn.QueryAsync<dynamic>("BUSCAR_G_NOTA_CREDITO_CS", p, commandType: CommandType.StoredProcedure)).ToList();
            _grid.ItemsSource = rows;
            _lblInfo.Text = $"{rows.Count} cuotas";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  GENERAR PAGOS  (genera cuotas para una venta a crédito existente)
// ══════════════════════════════════════════════════════════════════════════════
public class GenerarPagosWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtIdCab = null!, _txtMonto = null!, _txtEntrega = null!, _txtCuotas = null!;
    private DatePicker _dtInicio = null!;

    public GenerarPagosWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Generar Pagos / Cuotas"; Width = 420; Height = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new StackPanel { Margin = new Thickness(20) };
        var hdr = MakeSmHdr("Generar Pagos (Cuotas)", "#16A085");
        root.Children.Add(hdr);

        void AddRow(string lbl, UIElement ctrl) {
            root.Children.Add(new TextBlock { Text = lbl, Margin = new Thickness(0, 6, 0, 1), Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
            root.Children.Add(ctrl);
        }

        _txtIdCab  = new TextBox { Padding = new Thickness(4, 3, 4, 3) }; AddRow("N° Cabecera venta:", _txtIdCab);
        _txtMonto  = new TextBox { Padding = new Thickness(4, 3, 4, 3) }; AddRow("Monto total a financiar:", _txtMonto);
        _txtEntrega= new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = "0" }; AddRow("Entrega normal:", _txtEntrega);
        _txtCuotas = new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = "6" }; AddRow("Número de cuotas:", _txtCuotas);
        _dtInicio  = new DatePicker { SelectedDate = DateTime.Today }; AddRow("Fecha de inicio:", _dtInicio);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var btnG = MakeSmBtn("✔ Generar", "#27AE60"); btnG.Click += async (_, _) => await Generar();
        var btnC = MakeSmBtn("Cerrar", "#757575"); btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnG); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = root;
    }

    private async Task Generar()
    {
        if (!int.TryParse(_txtIdCab.Text.Trim(), out var idCab)) { MessageBox.Show("N° Cabecera inválido."); return; }
        if (!decimal.TryParse(_txtMonto.Text, out var monto) || monto <= 0) { MessageBox.Show("Monto inválido."); return; }
        if (!decimal.TryParse(_txtEntrega.Text, out var entrega)) entrega = 0;
        if (!int.TryParse(_txtCuotas.Text, out var cuotas) || cuotas <= 0) { MessageBox.Show("Cuotas inválidas."); return; }
        var fechaInicio = _dtInicio.SelectedDate ?? DateTime.Today;
        var sesion = SessionService.Instance;
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@NumeroDeFechas",  cuotas);
            p.Add("@monto",           monto);
            p.Add("@ENTREGANORMAL",   entrega);
            p.Add("@ID_LOCAL",        (byte)(sesion.LocalActual?.IdLocal ?? 1));
            p.Add("@ID_USU",          (byte)(sesion.UsuarioActual?.IdUsuario ?? 1));
            p.Add("@FechaInicioExterna", fechaInicio);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("AGREGAR_GENERADAS_CS", p, commandType: CommandType.StoredProcedure);
            var msg = p.Get<string>("@msg");
            MessageBox.Show($"Pagos generados. Resultado: {msg}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private static Border MakeSmHdr(string t, string hex) {
        var b = new Border { Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 12) };
        b.Child = new TextBlock { Text = t, Foreground = System.Windows.Media.Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold };
        return b;
    }
    private static Button MakeSmBtn(string t, string hex) => new Button { Content = t, Height = 30, Width = 90, Margin = new Thickness(0, 0, 8, 0),
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
        Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
}

// ══════════════════════════════════════════════════════════════════════════════
//  EDITAR PAGOS
// ══════════════════════════════════════════════════════════════════════════════
public class EditarPagosWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox  _txtIdCab = null!;
    private DataGrid _grid     = null!;

    public EditarPagosWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Editar Pagos Generados"; Width = 820; Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        var hdrB = new Border { Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2980B9")!, Padding = new Thickness(12, 6, 12, 6) };
        hdrB.Child = new TextBlock { Text = "Editar Pagos Generados", Foreground = System.Windows.Media.Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(hdrB, Dock.Top); root.Children.Add(hdrB);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnC = new Button { Content = "Cerrar", Height = 28, Padding = new Thickness(10, 0, 10, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close(); bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        var filterBar = new DockPanel { Margin = new Thickness(8) };
        filterBar.Children.Add(new TextBlock { Text = "N° Cabecera venta:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _txtIdCab = new TextBox { Padding = new Thickness(4, 3, 4, 3), Width = 100, Margin = new Thickness(0, 0, 6, 0) };
        _txtIdCab.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await Cargar(); };
        var btnB = new Button { Content = "Cargar cuotas", Height = 28, Padding = new Thickness(10, 0, 10, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF8C00")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnB.Click += async (_, _) => await Cargar();
        filterBar.Children.Add(_txtIdCab); filterBar.Children.Add(btnB);
        DockPanel.SetDock(filterBar, Dock.Top); root.Children.Add(filterBar);

        _grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
            AlternatingRowBackground = System.Windows.Media.Brushes.AliceBlue, Margin = new Thickness(8, 0, 8, 0) };
        _grid.Columns.Add(new DataGridTextColumn { Header = "ID",      Binding = new System.Windows.Data.Binding("IDGENERADAS"), Width = 60 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "N°Cuota", Binding = new System.Windows.Data.Binding("NCUOTA"),      Width = 70 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Monto",   Binding = new System.Windows.Data.Binding("MONTO"),       Width = 80 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Entrega", Binding = new System.Windows.Data.Binding("ENTREGA"),     Width = 70 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Vence",   Binding = new System.Windows.Data.Binding("VTO"),         Width = 100 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Estado",  Binding = new System.Windows.Data.Binding("ESTADO"),      Width = 90 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Cliente", Binding = new System.Windows.Data.Binding("CLIENTE"),     Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar()
    {
        if (!int.TryParse(_txtIdCab.Text.Trim(), out var idCab)) { MessageBox.Show("N° Cabecera inválido."); return; }
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@IdCab", idCab);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            var rows = (await conn.QueryAsync<dynamic>("CARGAR_CUOTAS_GENERADAS_CS", p, commandType: CommandType.StoredProcedure)).ToList();
            _grid.ItemsSource = rows;
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  ELIMINAR VENTA AL CONTADO
// ══════════════════════════════════════════════════════════════════════════════
public class EliminarVentaContadoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox _txtIdCab = null!;

    public EliminarVentaContadoWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Eliminar Venta al Contado"; Width = 400; Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new StackPanel { Margin = new Thickness(20) };
        var hdrE = new Border { Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#C0392B")!, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 12) };
        hdrE.Child = new TextBlock { Text = "Eliminar Venta al Contado", Foreground = System.Windows.Media.Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold };
        root.Children.Add(hdrE);
        root.Children.Add(new TextBlock { Text = "⚠ Esta acción es irreversible.", Foreground = System.Windows.Media.Brushes.Red, Margin = new Thickness(0, 0, 0, 8) });
        root.Children.Add(new TextBlock { Text = "N° Cabecera venta (IDCAB):", Margin = new Thickness(0, 0, 0, 2), Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
        _txtIdCab = new TextBox { Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 0, 0, 16) };
        root.Children.Add(_txtIdCab);
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnE = new Button { Content = "Eliminar", Width = 80, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#C0392B")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnE.Click += async (_, _) => await Eliminar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnE); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = root;
    }

    private async Task Eliminar()
    {
        if (!int.TryParse(_txtIdCab.Text.Trim(), out var idCab)) { MessageBox.Show("N° Cabecera inválido."); return; }
        var conf = MessageBox.Show($"¿Eliminar la venta al contado N° {idCab}?\nEsta acción no puede deshacerse.",
            "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (conf != MessageBoxResult.Yes) return;
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@Id",  idCab);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("ELIMINAR_VENTA_CONTADO_CS", p, commandType: CommandType.StoredProcedure);
            MessageBox.Show("Venta eliminada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  FINALIZAR PROMOCIÓN
// ══════════════════════════════════════════════════════════════════════════════
public class FinalizarPromoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox _txtIdArt = null!;

    public FinalizarPromoWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Finalizar Promoción"; Width = 420; Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new StackPanel { Margin = new Thickness(20) };
        var hdrP = new Border { Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#8E44AD")!, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 12) };
        hdrP.Child = new TextBlock { Text = "Finalizar Promoción de Artículo", Foreground = System.Windows.Media.Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold };
        root.Children.Add(hdrP);
        root.Children.Add(new TextBlock { Text = "ID Artículo (IDART):", Margin = new Thickness(0, 0, 0, 2), Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
        _txtIdArt = new TextBox { Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 0, 0, 16) };
        root.Children.Add(_txtIdArt);
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnF = new Button { Content = "Finalizar", Width = 80, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#8E44AD")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnF.Click += async (_, _) => await Finalizar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnF); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = root;
    }

    private async Task Finalizar()
    {
        if (!int.TryParse(_txtIdArt.Text.Trim(), out var idArt)) { MessageBox.Show("ID de artículo inválido."); return; }
        var conf = MessageBox.Show($"¿Finalizar la promoción del artículo ID {idArt}?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (conf != MessageBoxResult.Yes) return;
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@Idart",   idArt);
            p.Add("@Enpromo", (byte)0);
            p.Add("@ppromo",  0m);
            for (int i = 1; i <= 6; i++) { p.Add($"@P{i}", (byte)0); p.Add($"@L{i}", (byte)0); }
            p.Add("@Result", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("TERMINAR_PROMOCION_LOCALES_CS", p, commandType: CommandType.StoredProcedure);
            MessageBox.Show("Promoción finalizada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  RETIRO LIBRE
// ══════════════════════════════════════════════════════════════════════════════
public class RetiroLibreWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox   _txtMonto    = null!;
    private TextBox   _txtHoras    = null!;
    private TextBox   _txtConcepto = null!;
    private TextBox   _txtNota     = null!;
    private ComboBox  _cboTipo     = null!;

    public RetiroLibreWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Retiro Libre"; Width = 440; Height = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new StackPanel { Margin = new Thickness(20) };
        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1A5276")!,
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 14)
        };
        hdr.Child = new TextBlock { Text = "Registrar Retiro Libre", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold };
        root.Children.Add(hdr);

        void Row(string lbl, UIElement ctrl) {
            root.Children.Add(new TextBlock { Text = lbl, Margin = new Thickness(0, 6, 0, 1),
                Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
            root.Children.Add(ctrl);
        }

        _cboTipo = new ComboBox { Padding = new Thickness(4, 3, 4, 3) };
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Retiro en efectivo",   Tag = (byte)1 });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Adelanto de sueldo",   Tag = (byte)2 });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Devolución proveedor", Tag = (byte)3 });
        _cboTipo.Items.Add(new ComboBoxItem { Content = "Otro",                 Tag = (byte)4 });
        _cboTipo.SelectedIndex = 0;
        Row("Tipo de retiro:", _cboTipo);

        _txtMonto    = new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = "0" };
        _txtHoras    = new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = "0" };
        _txtConcepto = new TextBox { Padding = new Thickness(4, 3, 4, 3) };
        _txtNota     = new TextBox { Padding = new Thickness(4, 3, 4, 3), Height = 50,
            TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        Row("Monto:",    _txtMonto);
        Row("Horas:",    _txtHoras);
        Row("Concepto:", _txtConcepto);
        Row("Nota:",     _txtNota);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var btnG = new Button { Content = "✔ Registrar", Width = 100, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1A5276")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnG.Click += async (_, _) => await Registrar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnG); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = root;
    }

    private async Task Registrar()
    {
        if (!decimal.TryParse(_txtMonto.Text, out var monto) || monto < 0) { MessageBox.Show("Monto inválido."); return; }
        if (!decimal.TryParse(_txtHoras.Text, out var horas)) horas = 0;
        if (string.IsNullOrWhiteSpace(_txtConcepto.Text)) { MessageBox.Show("Ingrese un concepto."); return; }
        var sesion = SessionService.Instance;
        byte tipo = _cboTipo.SelectedItem is ComboBoxItem ci && ci.Tag is byte b ? b : (byte)1;
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@ID",        0);
            p.Add("@MOVIMIENTO",(byte)2);
            p.Add("@TIPO",      tipo);
            p.Add("@MONTO",     monto);
            p.Add("@HORAS",     horas);
            p.Add("@CONCEPTO",  _txtConcepto.Text.Trim());
            p.Add("@IDU",       (byte)(sesion.UsuarioActual?.IdUsuario ?? 1));
            p.Add("@NOMBRE",    sesion.UsuarioActual?.NombreUsuario ?? "");
            p.Add("@NOTA",      _txtNota.Text.Trim());
            p.Add("@ID_LOCAL",  (byte)(sesion.LocalActual?.IdLocal ?? 1));
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("AGREGAR_RETIRO_CS", p, commandType: CommandType.StoredProcedure);
            var msg = p.Get<string>("@msg");
            MessageBox.Show($"Retiro registrado. {msg}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error al registrar retiro: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  PROMOCIÓN — Crear / activar promoción de un artículo
// ══════════════════════════════════════════════════════════════════════════════
public class PromocionWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox    _txtIdArt   = null!;
    private TextBox    _txtPrecio  = null!;
    private DatePicker _dtInicio   = null!;
    private DatePicker _dtFin      = null!;
    private CheckBox[] _chkLocales = null!;
    private TextBox[]  _txtPases   = null!;

    public PromocionWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Crear / Activar Promoción"; Width = 520; Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root   = new StackPanel { Margin = new Thickness(20) };
        scroll.Content = root;

        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#6C3483")!,
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 14)
        };
        hdr.Child = new TextBlock { Text = "Promoción de Artículo", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold };
        root.Children.Add(hdr);

        void Row(string lbl, UIElement ctrl) {
            root.Children.Add(new TextBlock { Text = lbl, Margin = new Thickness(0, 6, 0, 1),
                Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
            root.Children.Add(ctrl);
        }

        _txtIdArt  = new TextBox { Padding = new Thickness(4, 3, 4, 3) };
        _txtPrecio = new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = "0" };
        _dtInicio  = new DatePicker { SelectedDate = DateTime.Today };
        _dtFin     = new DatePicker { SelectedDate = DateTime.Today.AddMonths(1) };
        Row("ID Artículo (IDART):", _txtIdArt);
        Row("Precio de promoción:", _txtPrecio);
        Row("Fecha inicio:", _dtInicio);
        Row("Fecha fin:",    _dtFin);

        root.Children.Add(new TextBlock { Text = "Locales participantes:",
            Margin = new Thickness(0, 10, 0, 4), FontWeight = FontWeights.SemiBold });

        _chkLocales = new CheckBox[6];
        _txtPases   = new TextBox[6];
        for (int i = 0; i < 6; i++)
        {
            var rowDp = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
            _chkLocales[i] = new CheckBox { Content = $"Local {i + 1}", Width = 90, VerticalAlignment = VerticalAlignment.Center };
            _txtPases[i]   = new TextBox  { Padding = new Thickness(4, 2, 4, 2), Text = "0", Width = 100 };
            DockPanel.SetDock(_chkLocales[i], Dock.Left); rowDp.Children.Add(_chkLocales[i]);
            rowDp.Children.Add(new TextBlock { Text = "  Precio: ", VerticalAlignment = VerticalAlignment.Center });
            rowDp.Children.Add(_txtPases[i]);
            root.Children.Add(rowDp);
        }

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var btnG = new Button { Content = "✔ Guardar", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#6C3483")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnG.Click += async (_, _) => await Guardar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnG); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = scroll;
    }

    private async Task Guardar()
    {
        if (!int.TryParse(_txtIdArt.Text.Trim(), out var idArt)) { MessageBox.Show("ID de artículo inválido."); return; }
        if (!decimal.TryParse(_txtPrecio.Text, out var precio) || precio < 0) { MessageBox.Show("Precio inválido."); return; }
        var inicio = _dtInicio.SelectedDate ?? DateTime.Today;
        var fin    = _dtFin.SelectedDate    ?? DateTime.Today.AddMonths(1);
        if (fin <= inicio) { MessageBox.Show("La fecha de fin debe ser posterior al inicio."); return; }
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@idart",       idArt);
            p.Add("@enpromo",     (byte)1);
            p.Add("@preciopromo", precio);
            for (int i = 0; i < 6; i++)
            {
                bool activo = _chkLocales[i].IsChecked == true;
                decimal.TryParse(_txtPases[i].Text, out var pLocal);
                p.Add($"@Pase{i + 1}", activo ? pLocal : 0m);
                p.Add($"@L{i + 1}",   activo ? (byte)1 : (byte)0);
            }
            p.Add("@inicio", inicio);
            p.Add("@fin",    fin);
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("GUARDAR_PROMOCIONAR_CS", p, commandType: CommandType.StoredProcedure);
            var msg = p.Get<string>("@msg");
            MessageBox.Show($"Promoción guardada. {msg}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error al guardar promoción: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  PAGO DE REMUNERACIONES
// ══════════════════════════════════════════════════════════════════════════════
public class PagoRemuneracionesWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox _txtSalario      = null!, _txtVenta         = null!, _txtPorcVenta    = null!;
    private TextBox _txtCobranza     = null!, _txtPorcCobranza  = null!, _txtPlus         = null!;
    private TextBox _txtHorasExtra   = null!, _txtBonificacion  = null!, _txtOtrasComis   = null!;
    private TextBox _txtAusencias    = null!, _txtAdelantos     = null!, _txtIps          = null!;
    private TextBox _txtCuotas       = null!, _txtMultas        = null!, _txtOtros        = null!;
    private TextBox _txtEquis        = null!, _txtNombre        = null!, _txtNotaAsig     = null!;
    private TextBox _txtNotaEgr      = null!;
    private DatePicker _dtFecha           = null!;
    private TextBlock  _lblTotalIngresos  = null!, _lblTotalEgresos = null!;

    public PagoRemuneracionesWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Pago de Remuneraciones"; Width = 640; Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root   = new StackPanel { Margin = new Thickness(20) };
        scroll.Content = root;

        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#117A65")!,
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 14)
        };
        hdr.Child = new TextBlock { Text = "Planilla de Pago de Remuneraciones",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold };
        root.Children.Add(hdr);

        void Row(string lbl, UIElement ctrl) {
            root.Children.Add(new TextBlock { Text = lbl, Margin = new Thickness(0, 5, 0, 1),
                Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
            root.Children.Add(ctrl);
        }
        TextBox MkTxt(string def = "0") => new TextBox { Padding = new Thickness(4, 3, 4, 3), Text = def };

        _txtNombre = MkTxt(""); Row("Nombre del funcionario:", _txtNombre);
        _dtFecha   = new DatePicker { SelectedDate = DateTime.Today }; Row("Fecha:", _dtFecha);

        root.Children.Add(new TextBlock { Text = "── INGRESOS ──────────────────",
            Margin = new Thickness(0, 10, 0, 2), FontWeight = FontWeights.Bold,
            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#117A65")! });

        _txtSalario      = MkTxt(); Row("Salario base:", _txtSalario);
        _txtVenta        = MkTxt(); Row("Venta (monto):", _txtVenta);
        _txtPorcVenta    = MkTxt(); Row("% Comisión por venta:", _txtPorcVenta);
        _txtCobranza     = MkTxt(); Row("Cobranza (monto):", _txtCobranza);
        _txtPorcCobranza = MkTxt(); Row("% Comisión por cobranza:", _txtPorcCobranza);
        _txtPlus         = MkTxt(); Row("Plus:", _txtPlus);
        _txtHorasExtra   = MkTxt(); Row("Horas extras:", _txtHorasExtra);
        _txtBonificacion = MkTxt(); Row("Bonificación:", _txtBonificacion);
        _txtOtrasComis   = MkTxt(); Row("Otras comisiones:", _txtOtrasComis);

        _lblTotalIngresos = new TextBlock { Margin = new Thickness(0, 4, 0, 0),
            FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.DarkGreen };
        root.Children.Add(_lblTotalIngresos);

        root.Children.Add(new TextBlock { Text = "── EGRESOS ───────────────────",
            Margin = new Thickness(0, 10, 0, 2), FontWeight = FontWeights.Bold,
            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#C0392B")! });

        _txtAusencias = MkTxt(); Row("Ausencias:", _txtAusencias);
        _txtAdelantos = MkTxt(); Row("Adelantos:", _txtAdelantos);
        _txtIps       = MkTxt(); Row("IPS:", _txtIps);
        _txtCuotas    = MkTxt(); Row("Cuotas:", _txtCuotas);
        _txtMultas    = MkTxt(); Row("Multas:", _txtMultas);
        _txtOtros     = MkTxt(); Row("Otros:", _txtOtros);
        _txtEquis     = MkTxt(); Row("Equis:", _txtEquis);

        _lblTotalEgresos = new TextBlock { Margin = new Thickness(0, 4, 0, 0),
            FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.DarkRed };
        root.Children.Add(_lblTotalEgresos);

        _txtNotaAsig = new TextBox { Padding = new Thickness(4, 3, 4, 3), Height = 40,
            TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        _txtNotaEgr  = new TextBox { Padding = new Thickness(4, 3, 4, 3), Height = 40,
            TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        Row("Nota asignación:", _txtNotaAsig);
        Row("Nota egreso:",     _txtNotaEgr);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var btnCalc = new Button { Content = "Calcular", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#2E86C1")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnCalc.Click += (_, _) => Calcular();
        var btnG = new Button { Content = "✔ Generar", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#117A65")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnG.Click += async (_, _) => await Generar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnCalc); btnRow.Children.Add(btnG); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = scroll;

        foreach (var tb in new[] { _txtSalario, _txtVenta, _txtPorcVenta, _txtCobranza, _txtPorcCobranza,
                                   _txtPlus, _txtHorasExtra, _txtBonificacion, _txtOtrasComis,
                                   _txtAusencias, _txtAdelantos, _txtIps, _txtCuotas, _txtMultas, _txtOtros, _txtEquis })
            tb.TextChanged += (_, _) => Calcular();
    }

    private (decimal ti, decimal tv, decimal tc, decimal te) GetTotals()
    {
        decimal D(TextBox t) => decimal.TryParse(t.Text, out var v) ? v : 0;
        var tv = D(_txtVenta) * D(_txtPorcVenta) / 100m;
        var tc = D(_txtCobranza) * D(_txtPorcCobranza) / 100m;
        var ti = D(_txtSalario) + tv + tc + D(_txtPlus) + D(_txtHorasExtra) + D(_txtBonificacion) + D(_txtOtrasComis);
        var te = D(_txtAusencias) + D(_txtAdelantos) + D(_txtIps) + D(_txtCuotas) + D(_txtMultas) + D(_txtOtros) + D(_txtEquis);
        return (ti, tv, tc, te);
    }

    private void Calcular()
    {
        var (ti, _, _, te) = GetTotals();
        _lblTotalIngresos.Text = $"Total ingresos: {ti:N2}";
        _lblTotalEgresos.Text  = $"Total egresos: {te:N2}   →  Neto: {(ti - te):N2}";
    }

    private async Task Generar()
    {
        if (string.IsNullOrWhiteSpace(_txtNombre.Text)) { MessageBox.Show("Ingrese el nombre del funcionario."); return; }
        decimal D(TextBox t) => decimal.TryParse(t.Text, out var v) ? v : 0;
        var (ti, tv, tc, te) = GetTotals();
        var sesion = SessionService.Instance;
        try
        {
            using var conn = _db.Create();
            var p = new DynamicParameters();
            p.Add("@IDGPS",           0);
            p.Add("@IDU",             (byte)(sesion.UsuarioActual?.IdUsuario ?? 1));
            p.Add("@IDLOCAL",         (byte)(sesion.LocalActual?.IdLocal ?? 1));
            p.Add("@SALARIO",         D(_txtSalario));
            p.Add("@VENTA",           D(_txtVenta));
            p.Add("@PORCVENTA",       D(_txtPorcVenta));
            p.Add("@TOTALVENTA",      tv);
            p.Add("@COBRANZA",        D(_txtCobranza));
            p.Add("@PORCCOBRANZA",    D(_txtPorcCobranza));
            p.Add("@TOTALCOBRANZA",   tc);
            p.Add("@PLUS",            D(_txtPlus));
            p.Add("@HORASEXTRAS",     D(_txtHorasExtra));
            p.Add("@BONIFICACION",    D(_txtBonificacion));
            p.Add("@OTRASCOMISIONES", D(_txtOtrasComis));
            p.Add("@TOTALINGRESOS",   ti);
            p.Add("@AUSENCIAS",       D(_txtAusencias));
            p.Add("@ADELANTOS",       D(_txtAdelantos));
            p.Add("@IPS",             D(_txtIps));
            p.Add("@CUOTAS",          D(_txtCuotas));
            p.Add("@MULTAS",          D(_txtMultas));
            p.Add("@OTROS",           D(_txtOtros));
            p.Add("@EQUIS",           D(_txtEquis));
            p.Add("@TOTALEGRESOS",    te);
            p.Add("@NOMBRE",          _txtNombre.Text.Trim());
            p.Add("@FECHA",           _dtFecha.SelectedDate ?? DateTime.Today);
            p.Add("@NOTAASIGNACION",  _txtNotaAsig.Text.Trim());
            p.Add("@NOTAEGRESO",      _txtNotaEgr.Text.Trim());
            p.Add("@msg", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            await conn.ExecuteAsync("GENERAR_PAGOSALARIO_CS", p, commandType: CommandType.StoredProcedure);
            var msg = p.Get<string>("@msg");
            MessageBox.Show($"Planilla generada. {msg}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error al generar planilla: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BLOQUEAR TRANSFERENCIAS
// ══════════════════════════════════════════════════════════════════════════════
public class BloquearTransfWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private DataGrid _grid = null!;

    public BloquearTransfWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Bloquear / Desbloquear Transferencias"; Width = 860; Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
        Loaded += async (_, _) => await Cargar();
    }

    private void BuildUI()
    {
        var root = new DockPanel();
        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#784212")!,
            Padding = new Thickness(12, 6, 12, 6)
        };
        hdr.Child = new TextBlock { Text = "Bloquear / Desbloquear Transferencias Pendientes",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var btnBlq = new Button { Content = "Bloquear selección", Height = 28, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#C0392B")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnBlq.Click += async (_, _) => await CambiarEstado(true);
        var btnDesblq = new Button { Content = "Desbloquear", Height = 28, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#27AE60")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnDesblq.Click += async (_, _) => await CambiarEstado(false);
        var btnC = new Button { Content = "Cerrar", Height = 28, Padding = new Thickness(10, 0, 10, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        bottom.Children.Add(btnBlq); bottom.Children.Add(btnDesblq); bottom.Children.Add(btnC);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);

        _grid = new DataGrid { AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Extended,
            IsReadOnly = true, AlternatingRowBackground = System.Windows.Media.Brushes.AntiqueWhite, Margin = new Thickness(8) };
        _grid.Columns.Add(new DataGridTextColumn { Header = "ID",          Binding = new System.Windows.Data.Binding("ID_REMITO_TMP"),  Width = 60 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "N° Remito",   Binding = new System.Windows.Data.Binding("NUMERO_REM_TMP"), Width = 90 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Origen",      Binding = new System.Windows.Data.Binding("ORIGEN"),         Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Destino",     Binding = new System.Windows.Data.Binding("DESTINO"),        Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Total Costo", Binding = new System.Windows.Data.Binding("TOTALCOSTO"),     Width = 100 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Estado",      Binding = new System.Windows.Data.Binding("ESTADOTXT"),      Width = 90 });
        root.Children.Add(_grid);
        Content = root;
    }

    private async Task Cargar()
    {
        try
        {
            using var conn = _db.Create();
            var rows = (await conn.QueryAsync<dynamic>(
                @"SELECT r.ID_REMITO_TMP, r.NUMERO_REM_TMP,
                         lo.NOMBRE_LOCAL AS ORIGEN, ld.NOMBRE_LOCAL AS DESTINO,
                         r.TOTALCOSTO,
                         CASE r.ESTADO WHEN 0 THEN 'Pendiente' WHEN 3 THEN 'Bloqueado'
                         ELSE CAST(r.ESTADO AS NVARCHAR) END AS ESTADOTXT
                  FROM CAB_REMITO_TMP r
                  LEFT JOIN LOCALES lo ON lo.ID_LOCAL = r.IDORIGENTMP
                  LEFT JOIN LOCALES ld ON ld.ID_LOCAL = r.IDDESTINOTMP
                  WHERE r.ESTADO IN (0, 3)
                  ORDER BY r.ID_REMITO_TMP DESC")).ToList();
            _grid.ItemsSource = rows;
        }
        catch (Exception ex) { MessageBox.Show($"Error al cargar transferencias: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task CambiarEstado(bool bloquear)
    {
        if (_grid.SelectedItem == null) { MessageBox.Show("Seleccione un remito."); return; }
        dynamic row = _grid.SelectedItem;
        try
        {
            using var conn = _db.Create();
            int idRemito = (int)row.ID_REMITO_TMP;
            byte nuevoEstado = bloquear ? (byte)3 : (byte)0;
            await conn.ExecuteAsync(
                "UPDATE CAB_REMITO_TMP SET ESTADO=@e WHERE ID_REMITO_TMP=@id",
                new { e = nuevoEstado, id = idRemito });
            await Cargar();
        }
        catch (Exception ex) { MessageBox.Show($"Error al cambiar estado: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  EDITAR CUOTA PAGADA
// ══════════════════════════════════════════════════════════════════════════════
public class EditarCuotaWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox    _txtIdCuota = null!;
    private TextBox    _txtMonto   = null!;
    private TextBox    _txtEntrega = null!;
    private DatePicker _dtVto      = null!;
    private TextBlock  _lblInfo    = null!;

    public EditarCuotaWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Editar Cuota Pagada"; Width = 420; Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new StackPanel { Margin = new Thickness(20) };
        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#D35400")!,
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 14)
        };
        hdr.Child = new TextBlock { Text = "Editar Cuota Pagada", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold };
        root.Children.Add(hdr);
        root.Children.Add(new TextBlock { Text = "⚠ Modifica datos de pago registrado.",
            Foreground = System.Windows.Media.Brushes.OrangeRed, Margin = new Thickness(0, 0, 0, 6), FontSize = 11 });

        var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        searchRow.Children.Add(new TextBlock { Text = "ID Cuota (IDGENERADAS):",
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _txtIdCuota = new TextBox { Padding = new Thickness(4, 3, 4, 3), Width = 80, Margin = new Thickness(0, 0, 6, 0) };
        var btnB = new Button { Content = "Cargar", Height = 26, Padding = new Thickness(8, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF8C00")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnB.Click += async (_, _) => await Cargar();
        searchRow.Children.Add(_txtIdCuota); searchRow.Children.Add(btnB);
        root.Children.Add(searchRow);

        _lblInfo = new TextBlock { Foreground = System.Windows.Media.Brushes.SteelBlue,
            Margin = new Thickness(0, 0, 0, 6), FontSize = 11 };
        root.Children.Add(_lblInfo);

        void Row(string lbl, UIElement ctrl) {
            root.Children.Add(new TextBlock { Text = lbl, Margin = new Thickness(0, 5, 0, 1),
                Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
            root.Children.Add(ctrl);
        }
        _txtMonto   = new TextBox { Padding = new Thickness(4, 3, 4, 3) }; Row("Monto:", _txtMonto);
        _txtEntrega = new TextBox { Padding = new Thickness(4, 3, 4, 3) }; Row("Entrega:", _txtEntrega);
        _dtVto      = new DatePicker(); Row("Vencimiento:", _dtVto);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var btnG = new Button { Content = "✔ Guardar", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#D35400")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnG.Click += async (_, _) => await Guardar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnG); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = root;
    }

    private async Task Cargar()
    {
        if (!int.TryParse(_txtIdCuota.Text.Trim(), out var idCuota)) { MessageBox.Show("ID inválido."); return; }
        try
        {
            using var conn = _db.Create();
            var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT IDGENERADAS, NCUOTA, MONTO, ENTREGA, VTO, ESTADO FROM GENERADAS WHERE IDGENERADAS=@id",
                new { id = idCuota });
            if (row == null) { MessageBox.Show("Cuota no encontrada."); return; }
            _lblInfo.Text       = $"Cuota N°{row.NCUOTA} — Estado: {row.ESTADO}";
            _txtMonto.Text      = ((decimal)row.MONTO).ToString("F2");
            _txtEntrega.Text    = ((decimal)row.ENTREGA).ToString("F2");
            _dtVto.SelectedDate = row.VTO is DateTime d ? d : (DateTime?)null;
        }
        catch (Exception ex) { MessageBox.Show($"Error al cargar cuota: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task Guardar()
    {
        if (!int.TryParse(_txtIdCuota.Text.Trim(), out var idCuota)) { MessageBox.Show("ID inválido."); return; }
        if (!decimal.TryParse(_txtMonto.Text,   out var monto))   { MessageBox.Show("Monto inválido."); return; }
        if (!decimal.TryParse(_txtEntrega.Text, out var entrega)) entrega = 0;
        var vto = _dtVto.SelectedDate;
        if (vto == null) { MessageBox.Show("Seleccione fecha de vencimiento."); return; }
        if (MessageBox.Show("¿Confirmar modificación?", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            using var conn = _db.Create();
            await conn.ExecuteAsync(
                "UPDATE GENERADAS SET MONTO=@m, ENTREGA=@e, VTO=@v WHERE IDGENERADAS=@id",
                new { m = monto, e = entrega, v = vto.Value, id = idCuota });
            MessageBox.Show("Cuota actualizada.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error al guardar cuota: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  ELIMINAR PAGO GENERADO
// ══════════════════════════════════════════════════════════════════════════════
public class EliminarPagoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private TextBox _txtIdCuota = null!;

    public EliminarPagoWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Eliminar Pago Generado"; Width = 400; Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new StackPanel { Margin = new Thickness(20) };
        var hdr = new Border {
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#922B21")!,
            Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(-20, -20, -20, 12)
        };
        hdr.Child = new TextBlock { Text = "Eliminar Pago Generado", Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13, FontWeight = FontWeights.Bold };
        root.Children.Add(hdr);
        root.Children.Add(new TextBlock { Text = "⚠ Acción irreversible.",
            Foreground = System.Windows.Media.Brushes.Red, Margin = new Thickness(0, 0, 0, 8), FontSize = 11 });
        root.Children.Add(new TextBlock { Text = "ID Cuota generada (IDGENERADAS):",
            Margin = new Thickness(0, 0, 0, 2), Foreground = System.Windows.Media.Brushes.DimGray, FontSize = 11 });
        _txtIdCuota = new TextBox { Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(0, 0, 0, 16) };
        root.Children.Add(_txtIdCuota);
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnE = new Button { Content = "Eliminar", Width = 80, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#922B21")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnE.Click += async (_, _) => await Eliminar();
        var btnC = new Button { Content = "Cancelar", Width = 80, Height = 30,
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#757575")!,
            Foreground = System.Windows.Media.Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        btnC.Click += (_, _) => Close();
        btnRow.Children.Add(btnE); btnRow.Children.Add(btnC);
        root.Children.Add(btnRow);
        Content = root;
    }

    private async Task Eliminar()
    {
        if (!int.TryParse(_txtIdCuota.Text.Trim(), out var idCuota)) { MessageBox.Show("ID inválido."); return; }
        if (MessageBox.Show($"¿Eliminar la cuota generada ID {idCuota}?", "Confirmar eliminación",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            using var conn = _db.Create();
            await conn.ExecuteAsync("DELETE FROM GENERADAS WHERE IDGENERADAS=@id", new { id = idCuota });
            MessageBox.Show("Pago eliminado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show($"Error al eliminar pago: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

