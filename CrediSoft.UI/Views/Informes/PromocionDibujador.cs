namespace CrediSoft.UI.Views.Informes;

public record FilaPromoImp(
    string NombreLocal,
    string Estado,
    decimal PProm,
    decimal Pventa,
    string Inicio,
    string Fin
);

public class PromocionPagina
{
    public List<FilaPromoImp> Filas       { get; init; } = new();
    public string Articulo   { get; init; } = "";
    public string Codigo     { get; init; } = "";
    public string FechaImp   { get; init; } = "";
    public string Usuario    { get; init; } = "";
    public string LogoPath   { get; init; } = "";

    public static string ResolverLogoPath()
    {
        const string nombre = "logotipocredimar2.png";
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidatos = new[]
        {
            System.IO.Path.Combine(baseDir, nombre),
            System.IO.Path.Combine(baseDir, "..", nombre),
            System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "..", nombre),
        };
        return candidatos.FirstOrDefault(System.IO.File.Exists) ?? System.IO.Path.Combine(baseDir, nombre);
    }
}

public static class PromocionDibujador
{
    // Portrait A4: 827 × 1169
    // Local | Estado | P.Promo Gs. | P.Venta Gs. | Inicio | Fin
    private static readonly int[] Cws         = { 0, 110, 115, 115, 100, 100 };
    // Col 0 (Local) se calcula para llenar
    private static readonly bool[] RightAlign = { false, false, true, true, false, false };

    private const int PgW = 827;
    private const int PgH = 1169;
    private const int Lx  = 20;
    private const int Pw  = PgW - 40; // 787

    private static int LocalW => Pw - Cws.Where((_, i) => i != 0).Sum(); // ~247

    private static readonly System.Drawing.Color Violeta  = System.Drawing.Color.FromArgb(69, 39, 160);   // #4527A0
    private static readonly System.Drawing.Color VioletaD = System.Drawing.Color.FromArgb(49, 27, 146);   // #311B92
    private static readonly System.Drawing.Color Verde    = System.Drawing.Color.FromArgb(27, 94, 32);    // #1B5E20
    private static readonly System.Drawing.Color Gris     = System.Drawing.Color.FromArgb(117, 117, 117); // #757575

    public static int ContarPaginas(PromocionPagina p)
    {
        const int hdrH = 20, rowH = 26;
        int y = 110 + 2 + hdrH + 3;
        int paginas = 1;
        foreach (var _ in p.Filas)
        {
            if (y > PgH - 52) { paginas++; y = 110 + 2 + hdrH + 3; }
            y += rowH;
        }
        return paginas;
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        PromocionPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        using var fntHdr  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntRow  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Regular);
        using var fntFoot = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntPag  = new System.Drawing.Font("Arial", 6.5f, System.Drawing.FontStyle.Regular);

        int localW = LocalW;

        // ── Marca de agua ────────────────────────────────────────────────────
        if (logo != null)
            DrawConOpacidad(g, logo,
                (int)(PgW / 2f - 250f), (int)(PgH / 2f - logo.Height * 500f / logo.Width / 2f),
                500, (int)(logo.Height * 500f / logo.Width), 0.055f);

        // ── Encabezado ───────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 108);
        using var penViol = new System.Drawing.Pen(Violeta, 3.5f);
        g.DrawLine(penViol, 0, 2, PgW, 2);

        if (logo != null)
        {
            float logoH2 = 62f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);
            float sepX = Lx + logoW2 + 12f;
            using var penGris = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 1f);
            g.DrawLine(penGris, sepX, 6, sepX, 98);
            float tx = sepX + 10f, tpw = Pw - (sepX - Lx) - 10f;
            DibujarBandaTitulo(g, tx, tpw);
            DibujarSubtitulos(g, p, tx, tpw);
        }
        else
        {
            DibujarBandaTitulo(g, Lx, Pw);
            DibujarSubtitulos(g, p, Lx, Pw);
        }

        g.DrawLine(penViol, 0, 100, PgW, 100);
        int y = 110;

        // ── Cabecera columnas ────────────────────────────────────────────────
        const int hdrH = 20;
        using var penHdrTop = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
        g.DrawLine(penHdrTop, Lx, y, Lx + Pw, y);
        y += 2;

        string[] hdrs = { "Local", "Estado", "P. Promo Gs.", "P. Venta Gs.", "Inicio", "Fin" };
        for (int i = 0; i < hdrs.Length; i++)
            Celda(g, hdrs[i], fntHdr, i, y, hdrH, System.Drawing.Brushes.Black, RightAlign[i], localW);

        y += hdrH;
        using var penHdrBot = new System.Drawing.Pen(Violeta, 1.5f);
        g.DrawLine(penHdrBot, Lx, y, Lx + Pw, y);
        y += 3;

        // ── Filas ────────────────────────────────────────────────────────────
        const int rowH = 26;
        bool hayMas = false;

        while (filaOffset < p.Filas.Count)
        {
            if (y > PgH - 52) { hayMas = true; break; }

            var f = p.Filas[filaOffset++];
            bool enPromo = f.Estado == "ACTIVA";

            if (enPromo)
            {
                using var bAct = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(232, 245, 233));
                g.FillRectangle(bAct, Lx, y, Pw, rowH);
            }

            System.Drawing.Brush bFg = enPromo
                ? new System.Drawing.SolidBrush(Verde)
                : new System.Drawing.SolidBrush(Gris);

            string[] vals =
            {
                f.NombreLocal,
                f.Estado,
                enPromo ? f.PProm.ToString("N0") : "—",
                f.Pventa.ToString("N0"),
                enPromo ? f.Inicio : "—",
                enPromo ? f.Fin    : "—",
            };

            for (int i = 0; i < vals.Length; i++)
                Celda(g, vals[i], fntRow, i, y, rowH, bFg, RightAlign[i], localW);

            bFg.Dispose();
            y += rowH;
            using var penSep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(215, 210, 230), 0.5f);
            g.DrawLine(penSep, Lx, y, Lx + Pw, y);
        }

        // ── Pie ──────────────────────────────────────────────────────────────
        string pagTxt = $"Página {pageNum} de {totalPages}";
        var szPag = g.MeasureString(pagTxt, fntPag);
        using var bPag = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(120, 120, 140));
        g.DrawString(pagTxt, fntPag, bPag, Lx + Pw - szPag.Width, PgH - 26);

        if (!hayMas)
        {
            y += 4;
            using (var pViol = new System.Drawing.Pen(Violeta, 1.5f))
                g.DrawLine(pViol, Lx, y, Lx + Pw, y);
            y += 6;

            const int pieH = 18;
            float partW  = Pw / 3f;
            float fhFoot = fntFoot.GetHeight(g);
            float footY  = y + Math.Max(0f, (pieH - fhFoot) / 2f);

            int activas = p.Filas.Count(f => f.Estado == "ACTIVA");

            using var bVerde = new System.Drawing.SolidBrush(Verde);
            g.DrawString($"Locales: {p.Filas.Count}", fntFoot, System.Drawing.Brushes.Black, Lx + 2f, footY);
            DrawCentrado(g, $"Con promo activa: {activas}", fntFoot, bVerde, Lx + partW, partW, footY);
            var s3 = $"Sin promo: {p.Filas.Count - activas}";
            var sz3 = g.MeasureString(s3, fntFoot);
            g.DrawString(s3, fntFoot, System.Drawing.Brushes.Black, Lx + Pw - sz3.Width - 2f, footY);

            using var penPD = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
            for (int i = 1; i < 3; i++)
                g.DrawLine(penPD, Lx + i * partW, y + 2, Lx + i * partW, y + pieH - 2);
        }

        return hayMas;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, float tx, float tpw)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx,            8,           r5*2, r5*2, 180, 90);
        path.AddArc(tx+tpw-r5*2,  8,           r5*2, r5*2, 270, 90);
        path.AddArc(tx+tpw-r5*2,  8+34-r5*2,  r5*2, r5*2, 0,   90);
        path.AddArc(tx,            8+34-r5*2,  r5*2, r5*2, 90,  90);
        path.CloseFigure();
        using var bBanda = new System.Drawing.SolidBrush(Violeta);
        g.FillPath(bBanda, path);
        using var fntTit = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
        DrawCentradoBox(g, "GESTIÓN DE PROMOCIONES", fntTit, System.Drawing.Brushes.White, tx+8, 8, tpw-12, 34);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, PromocionPagina p, float tx, float tpw)
    {
        using var penAT = new System.Drawing.Pen(Violeta, 1f);
        g.DrawLine(penAT, tx, 47, tx + tpw, 47);

        using var bSubBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(237, 231, 246));
        g.FillRectangle(bSubBg, tx, 50, tpw, 44f);
        using var penSubBd = new System.Drawing.Pen(System.Drawing.Color.FromArgb(209, 196, 233), 0.8f);
        g.DrawRectangle(penSubBd, tx, 50, tpw, 44f);

        using var fntSub  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Regular);
        using var fntArt  = new System.Drawing.Font("Arial", 8f,   System.Drawing.FontStyle.Bold);
        using var bSubTxt = new System.Drawing.SolidBrush(VioletaD);

        DrawCentradoBox(g, $"Artículo: {p.Codigo}  —  {p.Articulo}", fntArt, bSubTxt, tx+4, 50, tpw-8, 22);
        DrawCentradoBox(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", fntSub, bSubTxt, tx+4, 72, tpw-8, 22);
    }

    private static void Celda(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        int col, int ry, int rh, System.Drawing.Brush br, bool right, int localW)
    {
        if (string.IsNullOrEmpty(txt)) return;
        float colX = col == 0 ? Lx : Lx + localW + Cws.Skip(1).Take(col - 1).Sum();
        float colW = col == 0 ? localW : Cws[col];
        float maxW = colW - 4f;
        float fh   = f.GetHeight(g);
        float ty   = ry + Math.Max(0f, (rh - fh) / 2f);
        txt = Truncar(g, txt, f, maxW);
        float tx2 = right ? colX + colW - g.MeasureString(txt, f).Width - 2f : colX + 2f;
        g.DrawString(txt, f, br, tx2, ty);
    }

    private static string Truncar(System.Drawing.Graphics g, string txt, System.Drawing.Font f, float maxW)
    {
        if (g.MeasureString(txt, f).Width <= maxW) return txt;
        const string e = "…";
        float ew = g.MeasureString(e, f).Width;
        while (txt.Length > 0 && g.MeasureString(txt, f).Width + ew > maxW) txt = txt[..^1];
        return txt + e;
    }

    private static void DrawCentradoBox(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float y, float w, float h)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, x + Math.Max(0f, (w - sz.Width) / 2f), y + Math.Max(0f, (h - sz.Height) / 2f));
    }

    private static void DrawCentrado(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float w, float y)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, x + Math.Max(0f, (w - sz.Width) / 2f), y);
    }

    private static void DrawConOpacidad(System.Drawing.Graphics g, System.Drawing.Image img,
        int x, int y, int w, int h, float opacity)
    {
        var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity };
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
        g.DrawImage(img, new System.Drawing.Rectangle(x, y, w, h),
            0, 0, img.Width, img.Height, System.Drawing.GraphicsUnit.Pixel, ia);
    }
}
