namespace CrediSoft.UI.Views.Informes;

public record FilaTransfImp(
    string NumeroRem,
    string Origen,
    string Destino,
    decimal Total,
    string Estado,
    string Fecha
);

public class TransferenciasPagina
{
    public List<FilaTransfImp> Filas    { get; init; } = new();
    public string FechaImp   { get; init; } = "";
    public string Usuario    { get; init; } = "";
    public string LogoPath   { get; init; } = "";
    public string LocalFiltro { get; init; } = "Todos los locales";
    public string Desde      { get; init; } = "";
    public string Hasta      { get; init; } = "";

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

public static class TransferenciasDibujador
{
    // Portrait A4: 827 × 1169
    // N°Remito | Origen | Destino | Total Gs. | Estado | Fecha
    // widths: 90 | 0(fill) | 0(fill) | 110 | 100 | 90
    // Origen y Destino comparten el espacio dinámico al 50%

    private const int PgW = 827;
    private const int PgH = 1169;
    private const int Lx  = 20;
    private const int Pw  = PgW - 40; // 787

    private static readonly int WNum    = 90;
    private static readonly int WTotal  = 110;
    private static readonly int WEstado = 100;
    private static readonly int WFecha  = 90;
    private static int WDyn => (Pw - WNum - WTotal - WEstado - WFecha) / 2;  // ~198 cada uno

    private static readonly System.Drawing.Color Azul   = System.Drawing.Color.FromArgb(13,  71, 161);   // #0D47A1
    private static readonly System.Drawing.Color AzulM  = System.Drawing.Color.FromArgb(21, 101, 192);   // #1565C0
    private static readonly System.Drawing.Color Rojo   = System.Drawing.Color.FromArgb(183,  28,  28);  // #B71C1C
    private static readonly System.Drawing.Color Verde  = System.Drawing.Color.FromArgb(27,   94,  32);  // #1B5E20
    private static readonly System.Drawing.Color Gris   = System.Drawing.Color.FromArgb(117, 117, 117);  // #757575
    private static readonly System.Drawing.Color Naranja = System.Drawing.Color.FromArgb(230, 81,  0);   // #E65100

    private static readonly bool[] RightAlign = { false, false, false, true, false, false };

    public static int ContarPaginas(TransferenciasPagina p)
    {
        const int hdrH = 20, rowH = 26;
        int y = 100 + 2 + hdrH + 3;
        int paginas = 1;
        foreach (var _ in p.Filas)
        {
            if (y > PgH - 60) { paginas++; y = 100 + 2 + hdrH + 3; }
            y += rowH;
        }
        return paginas;
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        TransferenciasPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        using var fntHdr  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntRow  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Regular);
        using var fntFoot = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntPag  = new System.Drawing.Font("Arial", 6.5f, System.Drawing.FontStyle.Regular);

        int dynW = WDyn;

        // ── Marca de agua ────────────────────────────────────────────────────
        if (logo != null)
            DrawConOpacidad(g, logo,
                (int)(PgW / 2f - 250f), (int)(PgH / 2f - logo.Height * 500f / logo.Width / 2f),
                500, (int)(logo.Height * 500f / logo.Width), 0.055f);

        // ── Encabezado ───────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 98);
        using var penRojo = new System.Drawing.Pen(Rojo, 3.5f);
        g.DrawLine(penRojo, 0, 2, PgW, 2);

        if (logo != null)
        {
            float logoH2 = 62f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);
            float sepX = Lx + logoW2 + 12f;
            using var penGris = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 1f);
            g.DrawLine(penGris, sepX, 6, sepX, 90);
            float tx = sepX + 10f, tpw = Pw - (sepX - Lx) - 10f;
            DibujarBandaTitulo(g, tx, tpw);
            DibujarSubtitulos(g, p, tx, tpw);
        }
        else
        {
            DibujarBandaTitulo(g, Lx, Pw);
            DibujarSubtitulos(g, p, Lx, Pw);
        }

        g.DrawLine(penRojo, 0, 92, PgW, 92);
        int y = 100;

        // ── Cabecera columnas ────────────────────────────────────────────────
        const int hdrH = 20;
        using var penHdrTop = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
        g.DrawLine(penHdrTop, Lx, y, Lx + Pw, y);
        y += 2;

        string[] hdrs = { "N° Remito", "Origen", "Destino", "Total Gs.", "Estado", "Fecha" };
        for (int i = 0; i < hdrs.Length; i++)
            Celda(g, hdrs[i], fntHdr, i, y, hdrH, System.Drawing.Brushes.Black, RightAlign[i], dynW);

        y += hdrH;
        using var penHdrBot = new System.Drawing.Pen(Azul, 1.5f);
        g.DrawLine(penHdrBot, Lx, y, Lx + Pw, y);
        y += 3;

        // ── Filas ────────────────────────────────────────────────────────────
        const int rowH = 26;
        bool hayMas = false;
        int altIdx = 0;

        while (filaOffset < p.Filas.Count)
        {
            if (y > PgH - 60) { hayMas = true; break; }

            var f = p.Filas[filaOffset++];

            if (altIdx++ % 2 == 1)
            {
                using var bAlt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(245, 247, 252));
                g.FillRectangle(bAlt, Lx, y, Pw, rowH);
            }

            System.Drawing.Color estadoColor = f.Estado switch
            {
                "Aceptado" => Verde,
                "Anulado"  => Rojo,
                _          => Naranja
            };

            string[] vals = { f.NumeroRem, f.Origen, f.Destino, f.Total.ToString("N0"), f.Estado, f.Fecha };

            for (int i = 0; i < vals.Length; i++)
            {
                System.Drawing.Color fg = i == 4 ? estadoColor
                                        : i == 3 ? AzulM
                                        : System.Drawing.Color.Black;
                using var bFg = new System.Drawing.SolidBrush(fg);
                Celda(g, vals[i], fntRow, i, y, rowH, bFg, RightAlign[i], dynW);
            }

            y += rowH;
            using var penSep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 210, 225), 0.4f);
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
            using (var pAzul = new System.Drawing.Pen(Azul, 1.5f))
                g.DrawLine(pAzul, Lx, y, Lx + Pw, y);
            y += 6;

            const int pieH = 18;
            float fhFoot = fntFoot.GetHeight(g);
            float footY  = y + Math.Max(0f, (pieH - fhFoot) / 2f);

            int pendientes = p.Filas.Count(f => f.Estado == "Pendiente");
            int aceptados  = p.Filas.Count(f => f.Estado == "Aceptado");
            int anulados   = p.Filas.Count(f => f.Estado == "Anulado");

            float partW = Pw / 4f;
            using var bVerde  = new System.Drawing.SolidBrush(Verde);
            using var bRojo   = new System.Drawing.SolidBrush(Rojo);
            using var bNar    = new System.Drawing.SolidBrush(Naranja);

            g.DrawString($"Total: {p.Filas.Count}", fntFoot, System.Drawing.Brushes.Black, Lx + 2f, footY);
            DrawCentradoH(g, $"Pendientes: {pendientes}", fntFoot, bNar,   Lx + partW,     partW, footY);
            DrawCentradoH(g, $"Aceptados: {aceptados}",  fntFoot, bVerde, Lx + partW * 2, partW, footY);
            DrawCentradoH(g, $"Anulados: {anulados}",    fntFoot, bRojo,  Lx + partW * 3, partW, footY);

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
        path.AddArc(tx,           8,          r5*2, r5*2, 180, 90);
        path.AddArc(tx+tpw-r5*2, 8,          r5*2, r5*2, 270, 90);
        path.AddArc(tx+tpw-r5*2, 8+34-r5*2, r5*2, r5*2, 0,   90);
        path.AddArc(tx,           8+34-r5*2, r5*2, r5*2, 90,  90);
        path.CloseFigure();
        using var bBanda = new System.Drawing.SolidBrush(Azul);
        g.FillPath(bBanda, path);
        using var fntTit = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
        DrawCentradoBox(g, "HISTORIAL DE TRANSFERENCIAS", fntTit, System.Drawing.Brushes.White, tx+8, 8, tpw-12, 34);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, TransferenciasPagina p, float tx, float tpw)
    {
        using var penAT = new System.Drawing.Pen(Azul, 1f);
        g.DrawLine(penAT, tx, 47, tx + tpw, 47);

        using var bSubBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(227, 242, 253));
        g.FillRectangle(bSubBg, tx, 50, tpw, 38f);
        using var penSubBd = new System.Drawing.Pen(System.Drawing.Color.FromArgb(187, 222, 251), 0.8f);
        g.DrawRectangle(penSubBd, tx, 50, tpw, 38f);

        using var fntSub  = new System.Drawing.Font("Arial", 7f, System.Drawing.FontStyle.Regular);
        using var bSubTxt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(13, 71, 161));
        DrawCentradoBox(g, $"Local: {p.LocalFiltro}   ●   Período: {p.Desde} — {p.Hasta}", fntSub, bSubTxt, tx+4, 50, tpw-8, 19);
        DrawCentradoBox(g, $"Fecha de impresión: {p.FechaImp}   ●   Usuario: {p.Usuario}",  fntSub, bSubTxt, tx+4, 69, tpw-8, 19);
    }

    private static void Celda(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        int col, int ry, int rh, System.Drawing.Brush br, bool right, int dynW)
    {
        if (string.IsNullOrEmpty(txt)) return;
        float colX = col switch
        {
            0 => Lx,
            1 => Lx + WNum,
            2 => Lx + WNum + dynW,
            3 => Lx + WNum + dynW * 2,
            4 => Lx + WNum + dynW * 2 + WTotal,
            _ => Lx + WNum + dynW * 2 + WTotal + WEstado,
        };
        float colW = col switch
        {
            0 => WNum,
            1 => dynW,
            2 => dynW,
            3 => WTotal,
            4 => WEstado,
            _ => WFecha,
        };

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
