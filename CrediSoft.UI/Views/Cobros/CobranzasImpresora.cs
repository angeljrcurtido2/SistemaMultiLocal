using System.Drawing.Printing;
using System.IO;
using System.Windows;

namespace CrediSoft.UI.Views.Cobros;

public static class CobranzasImpresora
{
    public static void Imprimir(CobranzasPagina p)
        => _ = ImprimirAsync(p);

    private static async Task ImprimirAsync(CobranzasPagina p)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "Guardar PDF — Historial Cobranzas",
            Filter           = "Archivo PDF (*.pdf)|*.pdf",
            DefaultExt       = "pdf",
            FileName         = $"CobranzasHistorial_{DateTime.Now:yyyyMMdd_HHmm}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (sfd.ShowDialog() != true) return;
        var pdfRuta = sfd.FileName;

        System.Drawing.Image? logo = null;
        if (System.IO.File.Exists(p.LogoPath))
            try { logo = System.Drawing.Image.FromFile(p.LogoPath); } catch { }

        // Landscape A4: 1169 × 827
        const int pgW = 1169, pgH = 827;

        int totalPages = CobranzasDibujador.ContarPaginas(p);
        var bitmaps    = new List<System.Drawing.Bitmap>();

        await Task.Run(() =>
        {
            int filaOffset = 0;
            int pageNum    = 0;
            while (true)
            {
                pageNum++;
                var bmp = new System.Drawing.Bitmap(pgW, pgH);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.Clear(System.Drawing.Color.White);
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                bool hasMore = CobranzasDibujador.DibujarPagina(g, p, logo, ref filaOffset, pageNum, totalPages);
                bitmaps.Add(bmp);
                if (!hasMore || pageNum > 500) break;
            }
        });
        logo?.Dispose();

        var doc = new PrintDocument { DocumentName = "Historial Cobranzas" };
        doc.DefaultPageSettings.Landscape = true;
        doc.PrinterSettings.PrinterName   = "Microsoft Print to PDF";
        doc.PrinterSettings.PrintFileName = pdfRuta;
        doc.PrinterSettings.PrintToFile   = true;

        int idx = 0;
        string? errorMsg = null;

        await Task.Run(() =>
        {
            doc.PrintPage += (_, e) =>
            {
                if (idx >= bitmaps.Count) return;
                var g  = e.Graphics!;
                var pb = e.PageBounds;
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmaps[idx], pb.X, pb.Y, pb.Width, pb.Height);
                idx++;
                e.HasMorePages = idx < bitmaps.Count;
            };
            doc.EndPrint += (_, _) =>
            {
                foreach (var b in bitmaps) b.Dispose();
                bitmaps.Clear();
            };
            try   { doc.Print(); }
            catch (Exception ex) { errorMsg = ex.Message; }
        });

        if (errorMsg != null)
        {
            MessageBox.Show($"Error al imprimir:\n{errorMsg}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try { System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(pdfRuta) { UseShellExecute = true }); } catch { }
    }
}
