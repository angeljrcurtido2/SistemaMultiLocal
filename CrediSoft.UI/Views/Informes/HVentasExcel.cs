using System.Windows;
using ClosedXML.Excel;

namespace CrediSoft.UI.Views.Informes;

public static class HVentasExcel
{
    // ── Colores corporativos ───────────────────────────────────────────────
    private static readonly XLColor CAzul   = XLColor.FromArgb( 26,  82, 118);
    private static readonly XLColor CAzulCl = XLColor.FromArgb(214, 234, 248);
    private static readonly XLColor CSbtBg  = XLColor.FromArgb(232, 245, 253);
    private static readonly XLColor CSbtFg  = XLColor.FromArgb( 21,  67,  96);
    private static readonly XLColor CRojo   = XLColor.FromArgb(192,  57,  43);
    private static readonly XLColor CGris   = XLColor.FromArgb(245, 245, 248);
    private static readonly XLColor CAlt    = XLColor.FromArgb(235, 245, 255);

    // ── Exportar Detalle ──────────────────────────────────────────────────
    public static void ExportarDetalle(HVentasPagina p)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "Exportar Excel — Historial de Ventas (Detalle)",
            Filter           = "Excel (*.xlsx)|*.xlsx",
            DefaultExt       = "xlsx",
            FileName         = $"HVentas_Detalle_{DateTime.Now:yyyyMMdd_HHmm}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (sfd.ShowDialog() != true) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Detalle");

            // Anchos de columna (A-I)
            string[] hdrs = { "Vendedor", "Nº Sol.", "Tipo", "Cliente", "Total Gs.", "Entrega Gs.", "Saldo Gs.", "Estado", "Fecha" };
            int[]    wds  = { 28, 14, 12, 36, 18, 18, 18, 12, 12 };
            for (int c = 0; c < wds.Length; c++) ws.Column(c + 1).Width = wds[c];

            int row = EscribirEncabezado(ws, p, 9,
                "HISTORIAL DE VENTAS — DETALLE POR LOCAL",
                $"Generado: {p.FechaImp}   |   Usuario: {p.Usuario}   |   {p.Cantidad} ventas   |   Total: Gs. {p.SumTotal:N0}");

            // ── Grupos por local ──────────────────────────────────────────
            var grupos = p.Detalle.GroupBy(f => f.Local, StringComparer.OrdinalIgnoreCase);
            foreach (var grp in grupos)
            {
                var filas = grp.ToList();

                // Encabezado de grupo
                ws.Cell(row, 1).Value = $"  LOCAL:  {grp.Key.ToUpper()}";
                ws.Cell(row, 9).Value = $"{filas.Count} venta(s)";
                ws.Range(row, 1, row, 8).Merge();
                ws.Range(row, 1, row, 9).Style
                    .Font.SetBold(true).Font.SetFontSize(10).Font.SetFontColor(CAzul)
                    .Fill.SetBackgroundColor(CAzulCl)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                ws.Cell(row, 9).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                ws.Row(row).Height = 16;
                row++;

                // Cabecera de columnas
                for (int c = 0; c < hdrs.Length; c++)
                {
                    ws.Cell(row, c + 1).Value = hdrs[c];
                    ws.Cell(row, c + 1).Style
                        .Font.SetBold(true).Font.SetFontSize(9).Font.SetFontColor(XLColor.White)
                        .Fill.SetBackgroundColor(CAzul)
                        .Alignment.SetHorizontal(c >= 4 && c <= 6
                            ? XLAlignmentHorizontalValues.Right
                            : XLAlignmentHorizontalValues.Left);
                }
                ws.Row(row).Height = 15;
                row++;

                // Filas de datos
                decimal subTotal = 0, subEntrega = 0, subSaldo = 0;
                int z = 0;
                foreach (var f in filas)
                {
                    bool alt  = z++ % 2 == 1;
                    bool pend = f.Estado == "Pendiente";

                    ws.Cell(row, 1).Value = f.Vendedor;
                    ws.Cell(row, 2).Value = ShortSol(f.Solicitud);
                    ws.Cell(row, 3).Value = f.Tipo;
                    ws.Cell(row, 4).Value = f.Cliente;
                    ws.Cell(row, 5).Value = f.Total;
                    ws.Cell(row, 6).Value = f.Entrega;
                    ws.Cell(row, 7).Value = f.Saldo;
                    ws.Cell(row, 8).Value = f.Estado;
                    ws.Cell(row, 9).Value = f.Fecha;

                    ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                    ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                    ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";

                    ws.Range(row, 1, row, 9).Style
                        .Font.SetFontSize(9)
                        .Fill.SetBackgroundColor(alt ? CAlt : XLColor.White);
                    ws.Range(row, 5, row, 7).Style
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

                    if (pend)
                    {
                        ws.Cell(row, 7).Style.Font.SetFontColor(CRojo).Font.SetBold(true);
                        ws.Cell(row, 8).Style.Font.SetFontColor(CRojo).Font.SetBold(true);
                    }

                    ws.Row(row).Height = 14;
                    subTotal += f.Total; subEntrega += f.Entrega; subSaldo += f.Saldo;
                    row++;
                }

                // Subtotal del local
                ws.Cell(row, 1).Value = $"  Subtotal  {grp.Key}  ({filas.Count} ventas)";
                ws.Cell(row, 5).Value = subTotal;
                ws.Cell(row, 6).Value = subEntrega;
                ws.Cell(row, 7).Value = subSaldo;
                ws.Range(row, 1, row, 4).Merge();
                ws.Range(row, 1, row, 9).Style
                    .Font.SetBold(true).Font.SetFontSize(9).Font.SetFontColor(CSbtFg)
                    .Fill.SetBackgroundColor(CSbtBg);
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
                ws.Range(row, 5, row, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                ws.Row(row).Height = 14;
                row++;
                row++; // espacio entre grupos
            }

            // Total General
            ws.Cell(row, 1).Value = $"  TOTAL GENERAL  ({p.Cantidad} ventas)";
            ws.Cell(row, 5).Value = p.SumTotal;
            ws.Cell(row, 6).Value = p.SumEntrega;
            ws.Cell(row, 7).Value = p.SumSaldo;
            ws.Range(row, 1, row, 4).Merge();
            ws.Range(row, 1, row, 9).Style
                .Font.SetBold(true).Font.SetFontSize(10).Font.SetFontColor(XLColor.White)
                .Fill.SetBackgroundColor(CAzul);
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
            ws.Range(row, 5, row, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            ws.Row(row).Height = 16;

            // Inmovilizar encabezado
            ws.SheetView.FreezeRows(7);

            // Insertar logo (después de todo, para no afectar row counter)
            InsertarLogo(ws, p.LogoPath, totalCols: 9);

            wb.SaveAs(sfd.FileName);
            AbrirArchivo(sfd.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Exportar Resumen ──────────────────────────────────────────────────
    public static void ExportarResumen(HVentasPagina p)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "Exportar Excel — Historial de Ventas (Resumen)",
            Filter           = "Excel (*.xlsx)|*.xlsx",
            DefaultExt       = "xlsx",
            FileName         = $"HVentas_Resumen_{DateTime.Now:yyyyMMdd_HHmm}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (sfd.ShowDialog() != true) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Resumen");

            string[] hdrs = { "Vendedor", "Nº Sol.", "Total Gs.", "Entrega Gs.", "Debe Gs.", "Haber Gs.", "Saldo Gs." };
            int[]    wds  = { 30, 14, 18, 18, 18, 18, 18 };
            for (int c = 0; c < wds.Length; c++) ws.Column(c + 1).Width = wds[c];

            int row = EscribirEncabezado(ws, p, 7,
                "HISTORIAL DE VENTAS — RESUMEN POR LOCAL",
                $"Generado: {p.FechaImp}   |   Usuario: {p.Usuario}   |   {p.Cantidad} ventas");

            decimal genTotal = 0, genEntrega = 0, genDebe = 0, genHaber = 0, genSaldo = 0;

            var grupos = p.Resumen.GroupBy(f => f.Local, StringComparer.OrdinalIgnoreCase);
            foreach (var grp in grupos)
            {
                var filas = grp.ToList();

                // Encabezado de grupo
                ws.Cell(row, 1).Value = $"  LOCAL:  {grp.Key.ToUpper()}";
                ws.Cell(row, 7).Value = $"{filas.Count} venta(s)";
                ws.Range(row, 1, row, 6).Merge();
                ws.Range(row, 1, row, 7).Style
                    .Font.SetBold(true).Font.SetFontSize(10).Font.SetFontColor(CAzul)
                    .Fill.SetBackgroundColor(CAzulCl)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                ws.Cell(row, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                ws.Row(row).Height = 16;
                row++;

                // Cabecera de columnas
                for (int c = 0; c < hdrs.Length; c++)
                {
                    ws.Cell(row, c + 1).Value = hdrs[c];
                    ws.Cell(row, c + 1).Style
                        .Font.SetBold(true).Font.SetFontSize(9).Font.SetFontColor(XLColor.White)
                        .Fill.SetBackgroundColor(CAzul)
                        .Alignment.SetHorizontal(c >= 2
                            ? XLAlignmentHorizontalValues.Right
                            : XLAlignmentHorizontalValues.Left);
                }
                ws.Row(row).Height = 15;
                row++;

                // Filas
                decimal subTotal = 0, subEntrega = 0, subDebe = 0, subHaber = 0, subSaldo = 0;
                int z = 0;
                foreach (var f in filas)
                {
                    bool alt = z++ % 2 == 1;
                    ws.Cell(row, 1).Value = f.Vendedor;
                    ws.Cell(row, 2).Value = ShortSol(f.Solicitud);
                    ws.Cell(row, 3).Value = f.Total;
                    ws.Cell(row, 4).Value = f.Entrega;
                    ws.Cell(row, 5).Value = f.Debe;
                    ws.Cell(row, 6).Value = f.Haber;
                    ws.Cell(row, 7).Value = f.Saldo;

                    for (int c = 3; c <= 7; c++)
                        ws.Cell(row, c).Style.NumberFormat.Format = "#,##0";

                    ws.Range(row, 1, row, 7).Style
                        .Font.SetFontSize(9)
                        .Fill.SetBackgroundColor(alt ? CAlt : XLColor.White);
                    ws.Range(row, 3, row, 7).Style
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

                    if (f.Saldo > 0)
                        ws.Cell(row, 7).Style.Font.SetFontColor(CRojo).Font.SetBold(true);

                    ws.Row(row).Height = 14;
                    subTotal += f.Total; subEntrega += f.Entrega;
                    subDebe  += f.Debe;  subHaber   += f.Haber; subSaldo += f.Saldo;
                    row++;
                }

                // Subtotal del local
                ws.Cell(row, 1).Value = $"  Subtotal  {grp.Key}  ({filas.Count} ventas)";
                ws.Cell(row, 3).Value = subTotal;
                ws.Cell(row, 4).Value = subEntrega;
                ws.Cell(row, 5).Value = subDebe;
                ws.Cell(row, 6).Value = subHaber;
                ws.Cell(row, 7).Value = subSaldo;
                ws.Range(row, 1, row, 2).Merge();
                ws.Range(row, 1, row, 7).Style
                    .Font.SetBold(true).Font.SetFontSize(9).Font.SetFontColor(CSbtFg)
                    .Fill.SetBackgroundColor(CSbtBg);
                for (int c = 3; c <= 7; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "#,##0";
                ws.Range(row, 3, row, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                if (subSaldo > 0)
                    ws.Cell(row, 7).Style.Font.SetFontColor(CRojo);
                ws.Row(row).Height = 14;

                genTotal   += subTotal;   genEntrega += subEntrega;
                genDebe    += subDebe;    genHaber   += subHaber;
                genSaldo   += subSaldo;
                row++;
                row++; // espacio
            }

            // Total General
            ws.Cell(row, 1).Value = $"  TOTAL GENERAL  ({p.Cantidad} ventas)";
            ws.Cell(row, 3).Value = genTotal;
            ws.Cell(row, 4).Value = genEntrega;
            ws.Cell(row, 5).Value = genDebe;
            ws.Cell(row, 6).Value = genHaber;
            ws.Cell(row, 7).Value = genSaldo;
            ws.Range(row, 1, row, 2).Merge();
            ws.Range(row, 1, row, 7).Style
                .Font.SetBold(true).Font.SetFontSize(10).Font.SetFontColor(XLColor.White)
                .Fill.SetBackgroundColor(CAzul);
            for (int c = 3; c <= 7; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0";
            ws.Range(row, 3, row, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            ws.Row(row).Height = 16;

            ws.SheetView.FreezeRows(7);

            InsertarLogo(ws, p.LogoPath, totalCols: 7);

            wb.SaveAs(sfd.FileName);
            AbrirArchivo(sfd.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Encabezado común (filas 1-6) ─────────────────────────────────────
    // Devuelve la fila siguiente al encabezado (7).
    // Estructura:
    //   Fila 1 (alto 50): logo en A1:B4 (imagen flotante), cols C-fin → título azul
    //   Fila 2 (alto 16): cols C-fin → subtítulo filtro
    //   Fila 3 (alto 14): cols C-fin → info generado/usuario/total
    //   Fila 4 (alto 14): cols A-fin → segunda línea info (filtro largo)
    //   Filas 5-6: vacías (separador visual)
    private static int EscribirEncabezado(IXLWorksheet ws, HVentasPagina p,
        int totalCols, string titulo, string infoLinea)
    {
        // Fila 1 — alto grande para acomodar el logo
        ws.Row(1).Height = 52;
        ws.Row(2).Height = 16;
        ws.Row(3).Height = 14;
        ws.Row(4).Height = 12;
        ws.Row(5).Height = 6;  // separador

        // Cols A-B: blanco para el logo. Col C en adelante: azul corporativo con título.
        ws.Range(1, 1, 1, 2).Style.Fill.SetBackgroundColor(XLColor.White);

        // Título — cols 3 a totalCols de la fila 1
        ws.Cell(1, 3).Value = titulo;
        ws.Range(1, 3, 1, totalCols).Merge();
        ws.Range(1, 3, 1, totalCols).Style
            .Font.SetBold(true).Font.SetFontSize(15).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(CAzul)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

        // Fila 2 — filtro
        ws.Cell(2, 1).Value = string.IsNullOrEmpty(p.Filtro) ? "Todas las ventas" : p.Filtro;
        ws.Range(2, 1, 2, totalCols).Merge();
        ws.Range(2, 1, 2, totalCols).Style
            .Font.SetItalic(true).Font.SetFontSize(9).Font.SetFontColor(XLColor.FromArgb(80,80,100))
            .Fill.SetBackgroundColor(CGris)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetIndent(1);

        // Fila 3 — info generado/usuario
        ws.Cell(3, 1).Value = infoLinea;
        ws.Range(3, 1, 3, totalCols).Merge();
        ws.Range(3, 1, 3, totalCols).Style
            .Font.SetFontSize(9).Font.SetFontColor(XLColor.FromArgb(80,80,100))
            .Fill.SetBackgroundColor(CGris)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetIndent(1);

        // Fila 4 — vacía con fondo gris (separador suave)
        ws.Range(4, 1, 4, totalCols).Style.Fill.SetBackgroundColor(CGris);

        // Fila 5 — separador blanco
        // (sin estilo extra)

        return 6; // los datos empiezan en la fila 6
    }

    // ── Insertar logo en A1 ───────────────────────────────────────────────
    private static void InsertarLogo(IXLWorksheet ws, string logoPath, int totalCols)
    {
        if (string.IsNullOrEmpty(logoPath) || !System.IO.File.Exists(logoPath)) return;
        try
        {
            // Cargar imagen para calcular aspect ratio
            using var bmp = System.Drawing.Image.FromFile(logoPath);
            float aspect = (float)bmp.Width / bmp.Height;

            // Alto deseado en píxeles (equivale ~50px de altura de fila 1 ≈ 37.5pt)
            // Excel usa unidades EMU pero ClosedXML acepta píxeles directamente
            int logoH = 48; // px
            int logoW = (int)(logoH * aspect);

            var pic = ws.AddPicture(logoPath);
            pic.MoveTo(ws.Cell(1, 1), new System.Drawing.Point(4, 4));
            pic.Width  = logoW;
            pic.Height = logoH;
        }
        catch { /* si falla la imagen, el resto del Excel ya está guardado */ }
    }

    private static string ShortSol(string s)
    {
        var tr = s.TrimStart('0');
        return string.IsNullOrEmpty(tr) ? s : "#" + tr;
    }

    private static void AbrirArchivo(string ruta)
    {
        try { System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true }); }
        catch { }
    }
}
