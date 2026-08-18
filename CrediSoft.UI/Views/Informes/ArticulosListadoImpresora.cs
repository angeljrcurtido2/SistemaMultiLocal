using System.Drawing.Printing;
using System.Windows;

namespace CrediSoft.UI.Views.Informes;

public static class ArticulosListadoImpresora
{
    public static async Task ImprimirAsync(ArticulosListadoPagina p, Window? owner = null)
    {
        var doc = new PrintDocument { DocumentName = "Listado de Artículos" };
        doc.DefaultPageSettings.Landscape = false;

        bool esPdf = string.IsNullOrEmpty(p.Impresora)
                  || p.Impresora.Contains("PDF", StringComparison.OrdinalIgnoreCase)
                  || p.Impresora.Contains("XPS", StringComparison.OrdinalIgnoreCase);

        string? pdfRuta = null;
        if (esPdf)
        {
            doc.PrinterSettings.PrinterName = "Microsoft Print to PDF";

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title            = "Guardar PDF",
                Filter           = "Archivo PDF (*.pdf)|*.pdf",
                DefaultExt       = "pdf",
                FileName         = $"ListadoArticulos_{DateTime.Now:yyyyMMdd_HHmm}",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            if (sfd.ShowDialog() != true) return;
            pdfRuta = sfd.FileName;
            doc.PrinterSettings.PrintFileName = pdfRuta;
            doc.PrinterSettings.PrintToFile   = true;
        }
        else
        {
            doc.PrinterSettings.PrinterName = p.Impresora;
        }

        int totalPages = ArticulosListadoDibujador.ContarPaginas(p);
        var dlg = new CrediSoft.UI.Views.Maestros.ImprimiendoDialog(totalPages) { Owner = owner };
        dlg.Show();

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(p.LogoPath))
            try { logo = System.Drawing.Image.FromFile(p.LogoPath); } catch { }

        string? errorMsg = null;

        await Task.Run(() =>
        {
            int filaOffset = 0;
            int pageNum    = 0;

            doc.EndPrint += (_, _) => logo?.Dispose();
            doc.PrintPage += (_, e) =>
            {
                pageNum++;
                dlg.Dispatcher.InvokeAsync(() => dlg.SetPagina(pageNum));

                var g = e.Graphics!;
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
                e.HasMorePages = ArticulosListadoDibujador.DibujarPagina(g, p, logo, ref filaOffset, pageNum, totalPages);
            };

            try   { doc.Print(); }
            catch (Exception ex) { errorMsg = ex.Message; }
        });

        dlg.Close();

        if (errorMsg != null)
            MessageBox.Show($"Error al imprimir:\n{errorMsg}", "Error de impresión",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        else if (pdfRuta is { Length: > 0 })
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfRuta) { UseShellExecute = true }); } catch { }
    }

    public static void Imprimir(ArticulosListadoPagina p, Window? owner = null)
        => _ = ImprimirAsync(p, owner);
}
