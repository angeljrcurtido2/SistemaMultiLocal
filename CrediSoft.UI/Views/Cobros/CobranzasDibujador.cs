namespace CrediSoft.UI.Views.Cobros;

public record FilaCobranzaImp(
    string  FechaCobrado,
    string  Cliente,
    string  Telefono,
    string  Vendedor,
    string  Cobrador,
    string  Local,
    string  Cuota,
    string  Solicitud,
    int     Mora,
    decimal Monto,
    decimal Punitorio,
    decimal Total
);

public class CobranzasPagina
{
    public List<FilaCobranzaImp> Filas     { get; init; } = new();
    public string Desde                    { get; init; } = "";
    public string Hasta                    { get; init; } = "";
    public string FechaImp                 { get; init; } = "";
    public string Usuario                  { get; init; } = "";
    public string Impresora                { get; init; } = "";
    public string LogoPath                 { get; init; } = "";

    // Busca el logo con fallbacks: baseDir, un nivel arriba, raíz de solución
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

public static class CobranzasDibujador
{
    // Landscape A4: 1169 × 827
    // Fecha | Cliente | Teléfono | Vendedor | Cobrador | Local | Cuota | Solicitud | Mora | Monto | Punit. | Total
    private static readonly int[] Cws         = { 80, 175, 95, 105, 105, 90, 46, 62, 42, 95, 80, 95 };
    private static readonly bool[] RightAlign = { false, false, false, false, false, false, true, true, true, true, true, true };

    private const int PgW = 1169;   // landscape
    private const int PgH = 827;
    private const int Lx  = 20;
    private const int Pw  = PgW - 40; // 1129

    // Cuenta cuántas páginas necesita sin dibujar nada
    public static int ContarPaginas(CobranzasPagina p)
    {
        const int hdrH = 20, rowH = 28;
        int y      = 90 + 2 + hdrH + 3; // y inicial después de cabecera columnas
        int paginas = 1;
        foreach (var _ in p.Filas)
        {
            if (y > PgH - 55) { paginas++; y = 90 + 2 + hdrH + 3; }
            y += rowH;
        }
        return paginas;
    }

    // Dibuja una página. Devuelve true si hay más páginas.
    // filaOffset se actualiza al avanzar.
    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        CobranzasPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        var azul = System.Drawing.Color.FromArgb(13, 71, 161);
        var rojo = System.Drawing.Color.FromArgb(210, 0, 0);

        using var fntHdr  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntRow  = new System.Drawing.Font("Arial", 7f,   System.Drawing.FontStyle.Regular);
        using var fntFoot = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntPag  = new System.Drawing.Font("Arial", 6.5f, System.Drawing.FontStyle.Regular);

        // ── Marca de agua ────────────────────────────────────────────────────
        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawConOpacidad(g, logo,
                (int)(PgW / 2f - wmW / 2f),
                (int)(PgH / 2f - wmH / 2f + 40f),
                (int)wmW, (int)wmH, 0.055f);
        }

        // ── Encabezado ───────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 88);
        using var penRojo = new System.Drawing.Pen(rojo, 3.5f);
        g.DrawLine(penRojo, 0, 2, PgW, 2);

        if (logo != null)
        {
            float logoH2 = 62f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);

            float sepX = Lx + logoW2 + 12f;
            using var penGris = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 1f);
            g.DrawLine(penGris, sepX, 6, sepX, 78);

            float tx = sepX + 10f, tpw = Pw - (sepX - Lx) - 10f;
            DibujarBandaTitulo(g, azul, rojo, tx, tpw);
            DibujarSubtitulos(g, p, tx, tpw);
        }
        else
        {
            // Sin logo: banda azul ocupa todo el ancho
            DibujarBandaTitulo(g, azul, rojo, Lx, Pw);
            DibujarSubtitulos(g, p, Lx, Pw);
        }

        g.DrawLine(penRojo, 0, 80, PgW, 80);
        using var penGris2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 0.5f);
        g.DrawLine(penGris2, 0, 82, PgW, 82);

        int y = 90;

        // ── Cabecera columnas ────────────────────────────────────────────────
        const int hdrH = 20;
        using var penHdrTop = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
        g.DrawLine(penHdrTop, Lx, y, Lx + Pw, y);
        y += 2;

        string[] hdrs = { "Fecha", "Cliente", "Teléfono", "Vendedor", "Cobrador", "Local", "Cuota", "Solicitud", "Mora", "Monto Gs.", "Punit. Gs.", "Total Gs." };
        for (int i = 0; i < hdrs.Length; i++)
            Celda(g, hdrs[i], fntHdr, i, y, hdrH, System.Drawing.Brushes.Black, RightAlign[i]);

        y += hdrH;
        using var penHdrBot = new System.Drawing.Pen(azul, 1.5f);
        g.DrawLine(penHdrBot, Lx, y, Lx + Pw, y);
        y += 3;

        // ── Filas ────────────────────────────────────────────────────────────
        const int rowH = 28;
        bool hayMas  = false;

        while (filaOffset < p.Filas.Count)
        {
            if (y > PgH - 55) { hayMas = true; break; }

            var f = p.Filas[filaOffset++];

            string mora = f.Mora > 0 ? $"{f.Mora}d" : "—";
            string[] vals =
            {
                f.FechaCobrado, f.Cliente, f.Telefono, f.Vendedor, f.Cobrador, f.Local,
                f.Cuota, SolCorta(f.Solicitud), mora,
                f.Monto.ToString("N0"), f.Punitorio.ToString("N0"), f.Total.ToString("N0")
            };

            for (int i = 0; i < vals.Length; i++)
            {
                System.Drawing.Brush bTxt = System.Drawing.Brushes.Black;
                bool disp = false;
                if (i == 8 && f.Mora > 0)   // columna Mora (índice 8 con las nuevas cols)
                    { bTxt = new System.Drawing.SolidBrush(rojo); disp = true; }
                else if (i >= 9)             // Monto, Punit., Total (índices 9,10,11)
                {
                    decimal v = i == 9 ? f.Monto : i == 10 ? f.Punitorio : f.Total;
                    if (v > 0) { bTxt = new System.Drawing.SolidBrush(azul); disp = true; }
                }
                Celda(g, vals[i], fntRow, i, y, rowH, bTxt, RightAlign[i]);
                if (disp) bTxt.Dispose();
            }

            y += rowH;
            using var penSep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 210, 225), 0.5f);
            g.DrawLine(penSep, Lx, y, Lx + Pw, y);
        }

        // ── Pie: totales (solo última página) + número de página (siempre) ──
        int pieY = PgH - 40;

        // Número de página
        string pagTxt = $"Página {pageNum} de {totalPages}";
        var szPag = g.MeasureString(pagTxt, fntPag);
        using var bPag = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(120, 120, 140));
        g.DrawString(pagTxt, fntPag, bPag, Lx + Pw - szPag.Width, pieY + 14);

        if (!hayMas)
        {
            // Línea separadora
            y += 4;
            using (var p1 = new System.Drawing.Pen(azul, 1.5f))
                g.DrawLine(p1, Lx, y, Lx + Pw, y);
            y += 6;

            const int pieH = 18;
            float partW  = Pw / 4f;
            float fhFoot = fntFoot.GetHeight(g);
            float footY  = y + Math.Max(0f, (pieH - fhFoot) / 2f);

            // Totales
            g.DrawString($"Registros: {p.Filas.Count}", fntFoot, System.Drawing.Brushes.Black, Lx + 2f, footY);
            DrawDerecha(g, $"Monto: Gs. {p.Filas.Sum(f => f.Monto):N0}",      fntFoot, System.Drawing.Brushes.Black, Lx + partW,     partW, footY);
            DrawDerecha(g, $"Punit.: Gs. {p.Filas.Sum(f => f.Punitorio):N0}", fntFoot, System.Drawing.Brushes.Black, Lx + 2 * partW, partW, footY);
            DrawDerecha(g, $"Total: Gs. {p.Filas.Sum(f => f.Total):N0}",      fntFoot, System.Drawing.Brushes.Black, Lx + 3 * partW, partW, footY);

            using var penPD = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
            for (int i = 1; i < 4; i++)
                g.DrawLine(penPD, Lx + i * partW, y + 2, Lx + i * partW, y + pieH - 2);
        }

        return hayMas;
    }

    // ── Helpers de dibujo ────────────────────────────────────────────────────

    private static void DibujarBandaTitulo(
        System.Drawing.Graphics g,
        System.Drawing.Color azul,
        System.Drawing.Color rojo,
        float tx, float tpw)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx,           8,          r5 * 2, r5 * 2, 180, 90);
        path.AddArc(tx + tpw - r5 * 2, 8,          r5 * 2, r5 * 2, 270, 90);
        path.AddArc(tx + tpw - r5 * 2, 8 + 36 - r5 * 2, r5 * 2, r5 * 2, 0,   90);
        path.AddArc(tx,           8 + 36 - r5 * 2, r5 * 2, r5 * 2, 90,  90);
        path.CloseFigure();
        using var bBanda = new System.Drawing.SolidBrush(azul);
        g.FillPath(bBanda, path);
        using var bRojoA = new System.Drawing.SolidBrush(rojo);
        g.FillRectangle(bRojoA, tx, 8, 4, 36);
        using var fntTit = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
        DrawCentrado(g, "HISTORIAL DE COBRANZAS", fntTit, System.Drawing.Brushes.White, tx + 8, 8, tpw - 12, 36);
    }

    private static void DibujarSubtitulos(
        System.Drawing.Graphics g,
        CobranzasPagina p,
        float tx, float tpw)
    {
        var rojo = System.Drawing.Color.FromArgb(210, 0, 0);
        using var penRT = new System.Drawing.Pen(rojo, 1f);
        g.DrawLine(penRT, tx, 50, tx + tpw, 50);

        using var bSubBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(245, 245, 248));
        g.FillRectangle(bSubBg, tx, 53, tpw, 24f);
        using var penSubBd = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 220, 230), 0.8f);
        g.DrawRectangle(penSubBd, tx, 53, tpw, 24f);

        using var fntSub  = new System.Drawing.Font("Arial", 7f, System.Drawing.FontStyle.Regular);
        using var bSubTxt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(80, 80, 100));
        DrawCentrado(g, $"Período: {p.Desde} al {p.Hasta}",                          fntSub, bSubTxt, tx + 4, 53, tpw - 8, 12);
        DrawCentrado(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", fntSub, bSubTxt, tx + 4, 65, tpw - 8, 12);
    }

    private static void Celda(
        System.Drawing.Graphics g,
        string txt,
        System.Drawing.Font f,
        int col, int ry, int rh,
        System.Drawing.Brush br,
        bool right)
    {
        if (string.IsNullOrEmpty(txt)) return;
        float colX  = Lx + Cws.Take(col).Sum();
        float colW  = Cws[col];
        float maxW  = colW - 4f;
        float fh    = f.GetHeight(g);
        float ty    = ry + Math.Max(0f, (rh - fh) / 2f);

        // Truncar con "…" si el texto supera el ancho disponible
        txt = Truncar(g, txt, f, maxW);

        float tx;
        if (right)
        {
            var sz = g.MeasureString(txt, f);
            tx = colX + colW - sz.Width - 2f;
        }
        else
        {
            tx = colX + 2f;
        }
        g.DrawString(txt, f, br, tx, ty);
    }

    private static string Truncar(System.Drawing.Graphics g, string txt, System.Drawing.Font f, float maxW)
    {
        if (g.MeasureString(txt, f).Width <= maxW) return txt;
        const string elipsis = "…";
        float elW = g.MeasureString(elipsis, f).Width;
        while (txt.Length > 0 && g.MeasureString(txt, f).Width + elW > maxW)
            txt = txt[..^1];
        return txt + elipsis;
    }

    private static void DrawCentrado(
        System.Drawing.Graphics g,
        string txt,
        System.Drawing.Font f,
        System.Drawing.Brush br,
        float x, float y, float w, float h)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br,
            x + Math.Max(0f, (w - sz.Width)  / 2f),
            y + Math.Max(0f, (h - sz.Height) / 2f));
    }

    private static void DrawDerecha(
        System.Drawing.Graphics g,
        string txt,
        System.Drawing.Font f,
        System.Drawing.Brush br,
        float x, float w, float y)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, x + w - sz.Width - 2f, y);
    }

    private static void DrawConOpacidad(
        System.Drawing.Graphics g,
        System.Drawing.Image img,
        int x, int y, int w, int h,
        float opacity)
    {
        var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity };
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
        g.DrawImage(img,
            new System.Drawing.Rectangle(x, y, w, h),
            0, 0, img.Width, img.Height,
            System.Drawing.GraphicsUnit.Pixel, ia);
    }

    private static string SolCorta(string s)
    {
        var raw = s.TrimStart('#').TrimStart('0');
        return "#" + (raw.Length == 0 ? "0" : raw);
    }
}
