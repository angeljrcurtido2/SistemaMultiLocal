using CrediSoft.Data.Repositories;

namespace CrediSoft.UI.Views.Informes;

// Una fila "aplanada" del reporte: puede ser un header de grupo, un artículo dentro de él, o
// el subtotal de cierre de ese grupo (EsSubtotalGrupo) — se inserta justo después del último
// artículo de cada categoría/proveedor, antes de pasar al header del siguiente grupo.
public record FilaListado(bool EsHeaderGrupo, string Grupo, string Codigo, string Descripcion,
    string Stock, string PCosto = "", string PVenta = "", bool EsSubtotalGrupo = false);

public class ArticulosListadoPagina
{
    public List<FilaListado> Filas   { get; init; } = new();
    public string FechaImp   { get; init; } = "";
    public string Usuario    { get; init; } = "";
    public string Subtitulo  { get; init; } = "";
    public string Impresora  { get; init; } = "";
    public string LogoPath   { get; init; } = "";
    public int    TotalArticulos { get; init; }
    // Totales generales del listado (suma de Stock y de Stock×PCosto / Stock×PVenta de todos
    // los artículos) — se dibujan como fila propia, alineados bajo cada columna respectiva
    // (Stock, P.Costo, P.Venta), no como texto corrido.
    public decimal TotalStock { get; init; }
    public decimal TotalCosto { get; init; }
    public decimal TotalVenta { get; init; }

    public static string ResolverLogoPath() => CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath();

    public static List<FilaListado> Aplanar(IEnumerable<ArticuloListadoRow> filas)
    {
        var result = new List<FilaListado>();
        string? grupoActual = null;
        decimal subStock = 0, subCosto = 0, subVenta = 0;

        void CerrarGrupoActual()
        {
            if (grupoActual == null) return;
            result.Add(new FilaListado(false, grupoActual, "", $"Subtotal {grupoActual}",
                subStock.ToString("N0"), subCosto.ToString("N0"), subVenta.ToString("N0"), EsSubtotalGrupo: true));
        }

        foreach (var f in filas)
        {
            if (grupoActual == null || f.Grupo != grupoActual)
            {
                CerrarGrupoActual();
                grupoActual = f.Grupo;
                subStock = subCosto = subVenta = 0;
                result.Add(new FilaListado(true, f.Grupo, "", "", ""));
            }
            result.Add(new FilaListado(false, f.Grupo, f.Codigo, f.D, f.Stock.ToString("N0"),
                f.PCosto.ToString("N0"), f.PVenta.ToString("N0")));
            subStock += f.Stock;
            subCosto += f.Stock * f.PCosto;
            subVenta += f.Stock * f.PVenta;
        }
        CerrarGrupoActual();
        return result;
    }
}

public static class ArticulosListadoDibujador
{
    private const int PgW      = 827;
    private const int PgH      = 1169;
    private const int Lx       = 20;
    private const int Pw       = PgW - 40;
    private const int PieAreaY = PgH - 42;
    private const int PieH     = 18;

    // Tamaños ampliados a pedido del usuario — la versión original (FntRow 8pt, FilaH 20px)
    // resultaba ilegible en pantalla/impreso con listados largos. Se prioriza legibilidad
    // sobre densidad, aunque implique más páginas totales para el mismo listado.
    private const int GrupoH   = 32;
    private const int FilaH    = 26;
    private const int HdrH     = 26;
    private const int BodyStartY = 90 + 2;

    // Columnas: Código | Artículo/producto | P.Costo | P.Venta | Stock
    // Stock ya no ocupa el ancho completo pegado al borde derecho — queda con un ancho fijo
    // más chico e inmediatamente después de P.Venta, mismo criterio de columnas angostas para
    // valores numéricos que ya usa el resto de la app (pedido explícito: se veía "muy al
    // extremo" con el ancho completo restante de antes).
    private static readonly int[] Cw = { 100, 367, 105, 105, 110 };
    private static readonly float[] Cx = ColX(Cw);

    private static float[] ColX(int[] cws)
    {
        var x = new float[cws.Length];
        float acc = Lx;
        for (int i = 0; i < cws.Length; i++) { x[i] = acc; acc += cws[i]; }
        return x;
    }

    private static readonly System.Drawing.Font FntHdr    = new("Arial", 9f,   System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntGrupo  = new("Arial", 11.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow    = new("Arial", 10.5f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntFoot   = new("Arial", 9f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag    = new("Arial", 8f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntTit    = new("Arial", 13.5f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub    = new("Arial", 8.5f,   System.Drawing.FontStyle.Regular);

    // Paleta azul corporativa (misma que el resto de módulos nuevos)
    private static readonly System.Drawing.SolidBrush BAzulOsc  = new(System.Drawing.Color.FromArgb(14, 47, 68));    // #0E2F44
    private static readonly System.Drawing.SolidBrush BAzulMed  = new(System.Drawing.Color.FromArgb(26, 79, 110));   // #1A4F6E
    private static readonly System.Drawing.SolidBrush BAzulHielo = new(System.Drawing.Color.FromArgb(238, 244, 251)); // #EEF4FB
    private static readonly System.Drawing.SolidBrush BSubTxt  = new(System.Drawing.Color.FromArgb( 80, 80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg   = new(System.Drawing.Color.FromArgb(238, 244, 251));
    private static readonly System.Drawing.SolidBrush BPagNum  = new(System.Drawing.Color.FromArgb(120,120, 140));
    private static readonly System.Drawing.SolidBrush BAlt     = new(System.Drawing.Color.FromArgb(248,249, 252));
    private static readonly System.Drawing.SolidBrush BSubtotal = new(System.Drawing.Color.FromArgb(222,232,243));

    private static readonly System.Drawing.Pen PenAzulTop = new(System.Drawing.Color.FromArgb(14, 47, 68), 3.5f);
    private static readonly System.Drawing.Pen PenAzul1   = new(System.Drawing.Color.FromArgb(14, 47, 68), 1.0f);
    private static readonly System.Drawing.Pen PenAzul15  = new(System.Drawing.Color.FromArgb(26, 79, 110), 1.5f);
    private static readonly System.Drawing.Pen PenGris190 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05  = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenHdrTop  = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSep     = new(System.Drawing.Color.FromArgb(220,228,236), 0.8f);
    private static readonly System.Drawing.Pen PenPieLn   = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSubBd   = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);

    public static int ContarPaginas(ArticulosListadoPagina p)
    {
        if (p.Filas.Count == 0) return 1;
        var filaOffset = 0;
        var pageNum = 0;
        while (filaOffset < p.Filas.Count)
        {
            pageNum++;
            SimularPagina(p, ref filaOffset);
            if (pageNum > 2000) break;
        }
        return Math.Max(1, pageNum);
    }

    private static void SimularPagina(ArticulosListadoPagina p, ref int filaOffset)
    {
        int y = BodyStartY + HdrH + 3;
        int limY = PieAreaY - 4;
        while (filaOffset < p.Filas.Count)
        {
            var f = p.Filas[filaOffset];
            int h = f.EsHeaderGrupo ? GrupoH : FilaH;
            if (y + h > limY) break;
            y += h;
            filaOffset++;
        }
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        ArticulosListadoPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawConOpacidad(g, logo,
                (int)(PgW / 2f - wmW / 2f),
                (int)(PgH / 2f - wmH / 2f + 40f),
                (int)wmW, (int)wmH, 0.055f);
        }

        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 88);
        g.DrawLine(PenAzulTop, 0, 2, PgW, 2);

        float tx = Lx, tpw = Pw;
        if (logo != null)
        {
            float logoH2 = 62f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);
            float sepX = Lx + logoW2 + 12f;
            g.DrawLine(PenGris190, sepX, 6, sepX, 78);
            tx = sepX + 10f; tpw = Pw - (sepX - Lx) - 10f;
        }
        DibujarBandaTitulo(g, tx, tpw);
        DibujarSubtitulos(g, p, tx, tpw);

        g.DrawLine(PenAzulTop, 0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);

        int y = 90;
        g.DrawLine(PenHdrTop, Lx, y, Lx + Pw, y);
        y += 2;

        g.FillRectangle(BAzulOsc, Lx, y, Pw, HdrH);
        string[] hdrs = { "Código", "Artículo / Producto", "P. Costo", "P. Venta", "Stock" };
        DibujarCeldas(g, hdrs, Cw, Cx, y, FntHdr, System.Drawing.Brushes.White, HdrH,
            new[] { System.Drawing.StringAlignment.Near, System.Drawing.StringAlignment.Near,
                    System.Drawing.StringAlignment.Far,  System.Drawing.StringAlignment.Far, System.Drawing.StringAlignment.Far });
        y += HdrH;
        g.DrawLine(PenAzul15, Lx, y, Lx + Pw, y);
        y += 3;

        bool hayMas = false;
        int  limY   = PieAreaY - 4;
        int  artIdx = 0;

        while (filaOffset < p.Filas.Count)
        {
            var f = p.Filas[filaOffset];
            int h = f.EsHeaderGrupo ? GrupoH : FilaH;
            if (y + h > limY) { hayMas = true; break; }

            if (f.EsHeaderGrupo)
            {
                g.FillRectangle(BAzulMed, Lx, y, Pw, GrupoH);
                var szG = g.MeasureString(f.Grupo.ToUpperInvariant(), FntGrupo);
                g.DrawString(f.Grupo.ToUpperInvariant(), FntGrupo, System.Drawing.Brushes.White,
                    Lx + 10f, y + Math.Max(0f, (GrupoH - szG.Height) / 2f));
                artIdx = 0;
            }
            else if (f.EsSubtotalGrupo)
            {
                // Subtotal de cierre del grupo — fondo distintivo y texto en negrita, para que
                // se note como un corte, no como un artículo más de la lista.
                g.FillRectangle(BSubtotal, Lx, y, Pw, FilaH);
                string[] vals = { "", f.Descripcion, f.PCosto, f.PVenta, f.Stock };
                DibujarCeldas(g, vals, Cw, Cx, y, FntFoot, System.Drawing.Brushes.Black, FilaH,
                    new[] { System.Drawing.StringAlignment.Near, System.Drawing.StringAlignment.Near,
                            System.Drawing.StringAlignment.Far,  System.Drawing.StringAlignment.Far, System.Drawing.StringAlignment.Far });
                g.DrawLine(PenAzul15, Lx, y + FilaH - 1, Lx + Pw, y + FilaH - 1);
            }
            else
            {
                if (artIdx % 2 == 0)
                    g.FillRectangle(BAlt, Lx, y, Pw, FilaH);

                string[] vals = { f.Codigo, f.Descripcion, f.PCosto, f.PVenta, f.Stock };
                DibujarCeldas(g, vals, Cw, Cx, y, FntRow, System.Drawing.Brushes.Black, FilaH,
                    new[] { System.Drawing.StringAlignment.Near, System.Drawing.StringAlignment.Near,
                            System.Drawing.StringAlignment.Far,  System.Drawing.StringAlignment.Far, System.Drawing.StringAlignment.Far });
                g.DrawLine(PenSep, Lx, y + FilaH - 1, Lx + Pw, y + FilaH - 1);
                artIdx++;
            }

            y += h;
            filaOffset++;
        }

        g.DrawLine(hayMas ? PenPieLn : PenAzul15, Lx, PieAreaY, Lx + Pw, PieAreaY);

        float fhFoot = FntFoot.GetHeight(g);
        float footY  = PieAreaY + 4 + Math.Max(0f, (PieH - fhFoot) / 2f);

        if (!hayMas)
        {
            // Fila de totales alineada por columna (bajo Stock, P.Costo y P.Venta), en vez de
            // un texto corrido — pedido explícito para que cada total quede debajo de su
            // divisoria correspondiente, igual que el resto de la tabla.
            string[] totVals = { "", $"TOTALES ({p.TotalArticulos:N0} artículos):",
                p.TotalCosto.ToString("N0"), p.TotalVenta.ToString("N0"), p.TotalStock.ToString("N0") };
            DibujarCeldas(g, totVals, Cw, Cx, (int)(footY - 2), FntFoot, System.Drawing.Brushes.Black, PieH,
                new[] { System.Drawing.StringAlignment.Near, System.Drawing.StringAlignment.Near,
                        System.Drawing.StringAlignment.Far,  System.Drawing.StringAlignment.Far, System.Drawing.StringAlignment.Far });
        }

        string pagTxt = $"Página {pageNum} de {totalPages}";
        var szPag = g.MeasureString(pagTxt, FntPag);
        g.DrawString(pagTxt, FntPag, BPagNum, Lx + Pw - szPag.Width, PgH - 18);

        return hayMas;
    }

    private static void DibujarCeldas(
        System.Drawing.Graphics g, string[] txts,
        int[] cws, float[] cx, int y,
        System.Drawing.Font fnt, System.Drawing.Brush br,
        int rowH, System.Drawing.StringAlignment[] aligns)
    {
        float fh = fnt.GetHeight(g), ty = y + Math.Max(0f, (rowH - fh) / 2f);
        for (int i = 0; i < txts.Length; i++)
        {
            string t = txts[i];
            float cW = cws[i], maxW = cW - 8f;
            Trunc(g, ref t, fnt, maxW);
            float x = cx[i] + 4f;
            if (aligns[i] == System.Drawing.StringAlignment.Far)
            {
                var szT = g.MeasureString(t, fnt);
                x = cx[i] + cW - szT.Width - 6f;
            }
            g.DrawString(t, fnt, br, x, ty);
        }
    }

    private static void Trunc(System.Drawing.Graphics g, ref string t, System.Drawing.Font f, float maxW)
    {
        if (string.IsNullOrEmpty(t) || g.MeasureString(t, f).Width <= maxW) return;
        float elW = g.MeasureString("…", f).Width;
        while (t.Length > 0 && g.MeasureString(t, f).Width + elW > maxW)
            t = t[..^1];
        t += "…";
    }

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, float tx, float tpw)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx,              8,              r5*2, r5*2, 180, 90);
        path.AddArc(tx+tpw-r5*2,    8,              r5*2, r5*2, 270, 90);
        path.AddArc(tx+tpw-r5*2,    8+36-r5*2,      r5*2, r5*2,   0, 90);
        path.AddArc(tx,              8+36-r5*2,      r5*2, r5*2,  90, 90);
        path.CloseFigure();
        g.FillPath(BAzulMed, path);
        g.FillRectangle(BAzulOsc, tx, 8, 4, 36);
        DrawCentrado(g, "LISTADO DE ARTÍCULOS", FntTit, System.Drawing.Brushes.White, tx+8, 8, tpw-12, 36);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, ArticulosListadoPagina p, float tx, float tpw)
    {
        g.DrawLine(PenAzul1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg,  tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        DrawCentrado(g, p.Subtitulo,                                       FntSub, BSubTxt, tx+4, 53, tpw-8, 12);
        DrawCentrado(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", FntSub, BSubTxt, tx+4, 65, tpw-8, 12);
    }

    private static void DrawCentrado(
        System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float y, float w, float h)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br,
            x + Math.Max(0f, (w - sz.Width) / 2f),
            y + Math.Max(0f, (h - sz.Height) / 2f));
    }

    private static void DrawConOpacidad(
        System.Drawing.Graphics g, System.Drawing.Image img,
        int x, int y, int w, int h, float opacity)
    {
        var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity };
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
        g.DrawImage(img, new System.Drawing.Rectangle(x, y, w, h),
            0, 0, img.Width, img.Height, System.Drawing.GraphicsUnit.Pixel, ia);
    }
}
