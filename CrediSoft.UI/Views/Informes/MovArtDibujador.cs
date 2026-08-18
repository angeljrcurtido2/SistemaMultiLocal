namespace CrediSoft.UI.Views.Informes;

public record FilaMovArtImp(
    string  Movimiento,
    string  Modo,
    string  Fecha,
    string  Local,
    string  Destino,
    string  Codigo,
    string  Nombre,
    int     StAnterior,
    decimal Cantidad,
    decimal PCosto,
    string  Usuario
);

public class MovArtPagina
{
    public List<FilaMovArtImp> Filas   { get; init; } = new();
    public string Desde    { get; init; } = "";
    public string Hasta    { get; init; } = "";
    public string FechaImp { get; init; } = "";
    public string Usuario  { get; init; } = "";
    public string LogoPath { get; init; } = "";

    // totales para el pie
    public int    TotalBD   { get; init; }
    public int    Entradas  { get; init; }
    public int    Salidas   { get; init; }
    public int    Otros     { get; init; }

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

public static class MovArtDibujador
{
    // Landscape A4: 1169 × 827
    // Tipo | Modo | Fecha | Local | Destino | Código | Artículo | St.Ant | Cant. | P.Costo | Usuario
    private static readonly int[] Cws         = { 70, 95, 90, 105, 105, 75, 0, 58, 60, 90, 115 };
    // Col 6 (Artículo) se calcula dinámicamente para llenar el ancho
    private static readonly bool[] RightAlign = { false, false, false, false, false, false, false, true, true, true, false };

    private const int PgW = 1169;
    private const int PgH = 827;
    private const int Lx  = 20;
    private const int Pw  = PgW - 40; // 1129

    // El ancho de la columna Artículo = Pw - suma del resto
    private static int ArticuloW => Pw - Cws.Where((_, i) => i != 6).Sum(); // ~266

    private static readonly System.Drawing.Color Naranja  = System.Drawing.Color.FromArgb(255, 140, 0);   // #FF8C00
    private static readonly System.Drawing.Color NaranjaD = System.Drawing.Color.FromArgb(204, 85, 0);    // #CC5500
    private static readonly System.Drawing.Color Verde    = System.Drawing.Color.FromArgb(27, 94, 32);    // #1B5E20
    private static readonly System.Drawing.Color Rojo     = System.Drawing.Color.FromArgb(183, 28, 28);   // #B71C1C
    private static readonly System.Drawing.Color Azul     = System.Drawing.Color.FromArgb(21, 101, 192);  // #1565C0

    public static int ContarPaginas(MovArtPagina p)
    {
        const int hdrH = 20, rowH = 22;
        int y = 88 + 2 + hdrH + 3;
        int paginas = 1;
        foreach (var _ in p.Filas)
        {
            if (y > PgH - 52) { paginas++; y = 88 + 2 + hdrH + 3; }
            y += rowH;
        }
        return paginas;
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        MovArtPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        using var fntHdr  = new System.Drawing.Font("Arial", 7f,   System.Drawing.FontStyle.Bold);
        using var fntRow  = new System.Drawing.Font("Arial", 6.8f, System.Drawing.FontStyle.Regular);
        using var fntFoot = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntPag  = new System.Drawing.Font("Arial", 6.5f, System.Drawing.FontStyle.Regular);

        int artW = ArticuloW;

        // ── Marca de agua ────────────────────────────────────────────────────
        if (logo != null)
            DrawConOpacidad(g, logo,
                (int)(PgW / 2f - 250f), (int)(PgH / 2f - logo.Height * 500f / logo.Width / 2f),
                500, (int)(logo.Height * 500f / logo.Width), 0.055f);

        // ── Encabezado ───────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 86);
        using var penNar = new System.Drawing.Pen(Naranja, 3.5f);
        g.DrawLine(penNar, 0, 2, PgW, 2);

        if (logo != null)
        {
            float logoH2 = 60f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);
            float sepX = Lx + logoW2 + 12f;
            using var penGris = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 1f);
            g.DrawLine(penGris, sepX, 6, sepX, 76);
            float tx = sepX + 10f, tpw = Pw - (sepX - Lx) - 10f;
            DibujarBandaTitulo(g, tx, tpw);
            DibujarSubtitulos(g, p, tx, tpw);
        }
        else
        {
            DibujarBandaTitulo(g, Lx, Pw);
            DibujarSubtitulos(g, p, Lx, Pw);
        }

        g.DrawLine(penNar, 0, 78, PgW, 78);
        int y = 88;

        // ── Cabecera columnas ────────────────────────────────────────────────
        const int hdrH = 20;
        using var penHdrTop = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
        g.DrawLine(penHdrTop, Lx, y, Lx + Pw, y);
        y += 2;

        string[] hdrs = { "Tipo", "Modo", "Fecha", "Local", "Destino", "Código", "Artículo", "St.Ant", "Cant.", "P.Costo", "Usuario" };
        for (int i = 0; i < hdrs.Length; i++)
            Celda(g, hdrs[i], fntHdr, i, y, hdrH, System.Drawing.Brushes.Black, RightAlign[i], artW);

        y += hdrH;
        using var penHdrBot = new System.Drawing.Pen(Naranja, 1.5f);
        g.DrawLine(penHdrBot, Lx, y, Lx + Pw, y);
        y += 3;

        // ── Filas ────────────────────────────────────────────────────────────
        const int rowH = 22;
        bool hayMas = false;

        while (filaOffset < p.Filas.Count)
        {
            if (y > PgH - 52) { hayMas = true; break; }

            var f = p.Filas[filaOffset++];

            // fondo por tipo
            System.Drawing.Color bgColor = f.Movimiento == "ENTRADA"
                ? System.Drawing.Color.FromArgb(241, 248, 233)
                : f.Movimiento == "SALIDA"
                    ? System.Drawing.Color.FromArgb(255, 243, 243)
                    : System.Drawing.Color.FromArgb(232, 240, 254);
            using (var bBg = new System.Drawing.SolidBrush(bgColor))
                g.FillRectangle(bBg, Lx, y, Pw, rowH);

            // color de texto por tipo
            System.Drawing.Color fgColor = f.Movimiento == "ENTRADA" ? Verde
                : f.Movimiento == "SALIDA" ? Rojo : Azul;
            using var bFg = new System.Drawing.SolidBrush(fgColor);

            string[] vals =
            {
                f.Movimiento, f.Modo, f.Fecha, f.Local, f.Destino,
                f.Codigo, f.Nombre,
                f.StAnterior.ToString("N0"),
                f.Cantidad.ToString("N2"),
                f.PCosto.ToString("N0"),
                f.Usuario
            };

            for (int i = 0; i < vals.Length; i++)
                Celda(g, vals[i], fntRow, i, y, rowH, bFg, RightAlign[i], artW);

            y += rowH;
            using var penSep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 220, 220), 0.4f);
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
            using (var pNar = new System.Drawing.Pen(Naranja, 1.5f))
                g.DrawLine(pNar, Lx, y, Lx + Pw, y);
            y += 6;

            const int pieH = 18;
            float partW  = Pw / 4f;
            float fhFoot = fntFoot.GetHeight(g);
            float footY  = y + Math.Max(0f, (pieH - fhFoot) / 2f);

            using var bVerde = new System.Drawing.SolidBrush(Verde);
            using var bRojo  = new System.Drawing.SolidBrush(Rojo);
            using var bAzul  = new System.Drawing.SolidBrush(Azul);

            g.DrawString($"Mostrando: {p.Filas.Count} / {p.TotalBD}", fntFoot, System.Drawing.Brushes.Black, Lx + 2f, footY);
            DrawCentradoH(g, $"✔ Entradas: {p.Entradas}", fntFoot, bVerde, Lx + partW,     partW, footY);
            DrawCentradoH(g, $"▼ Salidas: {p.Salidas}",   fntFoot, bRojo,  Lx + partW * 2, partW, footY);
            DrawCentradoH(g, $"⚙ Otros: {p.Otros}",       fntFoot, bAzul,  Lx + partW * 3, partW, footY);

            using var penPD = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
            for (int i = 1; i < 4; i++)
                g.DrawLine(penPD, Lx + i * partW, y + 2, Lx + i * partW, y + pieH - 2);
        }

        return hayMas;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, float tx, float tpw)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx,            8,              r5*2, r5*2, 180, 90);
        path.AddArc(tx+tpw-r5*2,  8,              r5*2, r5*2, 270, 90);
        path.AddArc(tx+tpw-r5*2,  8+34-r5*2,     r5*2, r5*2, 0,   90);
        path.AddArc(tx,            8+34-r5*2,     r5*2, r5*2, 90,  90);
        path.CloseFigure();
        using var bBanda = new System.Drawing.SolidBrush(Naranja);
        g.FillPath(bBanda, path);
        using var fntTit = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
        DrawCentrado(g, "MOVIMIENTO DE ARTÍCULOS", fntTit, System.Drawing.Brushes.White, tx+8, 8, tpw-12, 34);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, MovArtPagina p, float tx, float tpw)
    {
        using var penAT = new System.Drawing.Pen(Naranja, 1f);
        g.DrawLine(penAT, tx, 47, tx + tpw, 47);

        using var bSubBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 243, 224));
        g.FillRectangle(bSubBg, tx, 50, tpw, 24f);
        using var penSubBd = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 204, 128), 0.8f);
        g.DrawRectangle(penSubBd, tx, 50, tpw, 24f);

        using var fntSub  = new System.Drawing.Font("Arial", 7f, System.Drawing.FontStyle.Regular);
        using var bSubTxt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(130, 70, 0));

        string linea1 = string.IsNullOrEmpty(p.Desde)
            ? "Sin filtro de fecha"
            : $"Período: {p.Desde} al {p.Hasta}";
        DrawCentrado(g, linea1,                                             fntSub, bSubTxt, tx+4, 50, tpw-8, 12);
        DrawCentrado(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", fntSub, bSubTxt, tx+4, 62, tpw-8, 12);
    }

    private static void Celda(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        int col, int ry, int rh, System.Drawing.Brush br, bool right, int artW)
    {
        if (string.IsNullOrEmpty(txt)) return;
        float colX = Lx + Cws.Take(col).Sum() + (col > 6 ? artW - Cws[6] : 0);
        float colW = col == 6 ? artW : Cws[col];
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

    private static void DrawCentrado(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float y, float w, float h)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, x + Math.Max(0f, (w - sz.Width) / 2f), y + Math.Max(0f, (h - sz.Height) / 2f));
    }

    private static void DrawCentradoH(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
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
