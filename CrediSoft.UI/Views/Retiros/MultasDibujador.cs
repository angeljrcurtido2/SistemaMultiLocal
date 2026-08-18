namespace CrediSoft.UI.Views.Retiros;

// Fila plana con flag de header — mismo patrón que FilaArqueoDetalle (CajaArqueoDibujador),
// agrupado por funcionario en vez de por cajero.
public class FilaMultaReporte
{
    public bool    EsHeaderFuncionario { get; init; }
    public string  Funcionario         { get; init; } = "";
    public string  LocalEtiqueta       { get; init; } = "";
    public string  FechaStr            { get; init; } = "";
    public decimal Monto               { get; init; }
    public string  Concepto            { get; init; } = "";
    public string  UsuarioCarga        { get; init; } = "";
}

public class MultasReportePagina
{
    public List<FilaMultaReporte> Filas    { get; init; } = new();
    public string Periodo   { get; init; } = ""; // ej. "Agosto 2026"
    public string FiltroFuncionario { get; init; } = ""; // "" = todos
    public string FechaImp  { get; init; } = "";
    public string Usuario   { get; init; } = "";
    public string LogoPath  { get; init; } = "";

    public static string ResolverLogoPath() => CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath();
}

// Mismo look visual que CajaArqueoDetalleDibujador (header logo Credimar, banda azul,
// agrupado — acá por funcionario en vez de cajero) — volumen esperado bajo (multas por mes),
// pero se mantiene la paginación por si algún mes/local acumula muchos registros.
public static class MultasReporteDibujador
{
    // Retrato (no landscape como Arqueo): solo 4 columnas y pocos registros por período,
    // una hoja A4 vertical normal alcanza y es más natural para imprimir/archivar.
    private static readonly int[] Cws         = { 90, 100, 447, 150 }; // Fecha, Monto, Motivo, Cargado por
    private static readonly bool[] RightAlign = { false, true, false, false };

    private const int PgW = 827;
    private const int PgH = 1169;
    private const int Lx  = 20;
    private const int Pw  = PgW - 40;

    public static int ContarPaginas(MultasReportePagina p)
    {
        const int hdrH = 20, rowH = 22, userH = 24, subtotalH = 20;
        int y = 88 + 2 + hdrH + 3;
        int paginas = 1;
        bool hayBloqueAbierto = false;
        foreach (var f in p.Filas)
        {
            int alto = f.EsHeaderFuncionario
                ? userH + hdrH + 3 + (hayBloqueAbierto ? subtotalH : 0)
                : rowH;
            if (y + alto > PgH - 40) { paginas++; y = 88 + 2 + hdrH + 3; }
            y += alto;
            if (f.EsHeaderFuncionario) hayBloqueAbierto = true;
        }
        return paginas;
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        MultasReportePagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        var azul = System.Drawing.Color.FromArgb(21, 79, 110);
        var rojo = System.Drawing.Color.FromArgb(198, 40, 40);

        using var fntHdr  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntRow  = new System.Drawing.Font("Arial", 7f,   System.Drawing.FontStyle.Regular);
        using var fntUser = new System.Drawing.Font("Arial", 8f,   System.Drawing.FontStyle.Bold);
        using var fntFoot = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntPag  = new System.Drawing.Font("Arial", 6.5f, System.Drawing.FontStyle.Regular);

        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawConOpacidad(g, logo, (int)(PgW / 2f - wmW / 2f), (int)(PgH / 2f - wmH / 2f), (int)wmW, (int)wmH, 0.055f);
        }

        // ── Encabezado ────────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 86);
        using var penAzul = new System.Drawing.Pen(azul, 3.5f);
        g.DrawLine(penAzul, 0, 2, PgW, 2);

        if (logo != null)
        {
            float logoH2 = 60f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);

            float sepX = Lx + logoW2 + 12f;
            using var penGris = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 1f);
            g.DrawLine(penGris, sepX, 6, sepX, 76);

            float tx = sepX + 10f, tpw = Pw - (sepX - Lx) - 10f;
            DibujarBandaTitulo(g, azul, tx, tpw);
            DibujarSubtitulos(g, p, tx, tpw);
        }
        else
        {
            DibujarBandaTitulo(g, azul, Lx, Pw);
            DibujarSubtitulos(g, p, Lx, Pw);
        }

        g.DrawLine(penAzul, 0, 78, PgW, 78);
        using var penGris2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 0.5f);
        g.DrawLine(penGris2, 0, 80, PgW, 80);

        int y = 88;

        const int hdrH = 20;
        using var penHdrTop = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
        g.DrawLine(penHdrTop, Lx, y, Lx + Pw, y);
        y += 2;

        string[] hdrs = { "Fecha", "Monto Gs.", "Motivo", "Cargado por" };
        void DibujarHeader(int yh)
        {
            for (int i = 0; i < hdrs.Length; i++)
                Celda(g, hdrs[i], fntHdr, i, yh, hdrH, System.Drawing.Brushes.Black, RightAlign[i]);
        }
        DibujarHeader(y);

        y += hdrH;
        using var penHdrBot = new System.Drawing.Pen(azul, 1.5f);
        g.DrawLine(penHdrBot, Lx, y, Lx + Pw, y);
        y += 3;

        const int rowH = 22, userH = 24, subtotalH = 20;
        bool hayMas = false;
        int contador = 0;
        decimal subtotalActual = 0;
        bool hayBloqueAbierto = false;

        void DibujarSubtotal()
        {
            using var bRojoTxt = new System.Drawing.SolidBrush(rojo);
            using var bFondoSub = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(238, 238, 238));
            g.FillRectangle(bFondoSub, Lx, y, Pw, subtotalH);
            var txt = $"Subtotal: Gs. {subtotalActual:N0}";
            var szSub = g.MeasureString(txt, fntUser);
            g.DrawString(txt, fntUser, bRojoTxt, Lx + Pw - szSub.Width - 8f, y + (subtotalH - szSub.Height) / 2f);
            y += subtotalH;
        }

        while (filaOffset < p.Filas.Count)
        {
            var f = p.Filas[filaOffset];
            int altoExtra = f.EsHeaderFuncionario && hayBloqueAbierto ? subtotalH : 0;
            int alto = (f.EsHeaderFuncionario ? userH : rowH) + altoExtra;
            if (y + alto > PgH - 40) { hayMas = true; break; }
            filaOffset++;

            if (f.EsHeaderFuncionario)
            {
                if (hayBloqueAbierto) DibujarSubtotal();

                using var bRojo = new System.Drawing.SolidBrush(rojo);
                g.FillRectangle(bRojo, Lx, y, Pw, userH);
                var etiqueta = string.IsNullOrEmpty(f.LocalEtiqueta) ? f.Funcionario : $"{f.Funcionario}   {f.LocalEtiqueta}";
                var szU = g.MeasureString(etiqueta, fntUser);
                g.DrawString(etiqueta, fntUser, System.Drawing.Brushes.White, Lx + 8f, y + (userH - szU.Height) / 2f);
                y += userH;

                if (y + hdrH > PgH - 40) { hayMas = true; filaOffset--; break; }
                DibujarHeader(y);
                y += hdrH;
                using (var penHdrBot2 = new System.Drawing.Pen(azul, 1.2f))
                    g.DrawLine(penHdrBot2, Lx, y, Lx + Pw, y);
                y += 3;

                contador = 0;
                subtotalActual = 0;
                hayBloqueAbierto = true;
                continue;
            }

            if (contador % 2 == 1)
            {
                using var bAlt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(244, 248, 250));
                g.FillRectangle(bAlt, Lx, y, Pw, rowH);
            }
            contador++;
            subtotalActual += f.Monto;

            string[] vals = { f.FechaStr, f.Monto.ToString("N0"), f.Concepto, f.UsuarioCarga };
            for (int i = 0; i < vals.Length; i++)
                Celda(g, vals[i], fntRow, i, y, rowH, System.Drawing.Brushes.Black, RightAlign[i]);

            y += rowH;
            using var penSep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 215, 230), 0.5f);
            g.DrawLine(penSep, Lx, y, Lx + Pw, y);
        }

        var bloqueTerminaEnEstaPagina = !hayMas ||
            (filaOffset < p.Filas.Count && p.Filas[filaOffset].EsHeaderFuncionario);
        if (hayBloqueAbierto && bloqueTerminaEnEstaPagina) DibujarSubtotal();

        string pagTxt = $"Página {pageNum} de {totalPages}";
        var szPag = g.MeasureString(pagTxt, fntPag);
        using var bPag = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(120, 120, 140));
        g.DrawString(pagTxt, fntPag, bPag, Lx + Pw - szPag.Width, PgH - 26);

        if (!hayMas)
        {
            y += 4;
            using (var p1 = new System.Drawing.Pen(azul, 1.5f))
                g.DrawLine(p1, Lx, y, Lx + Pw, y);
            y += 6;

            var datos = p.Filas.Where(f => !f.EsHeaderFuncionario).ToList();
            float fhFoot = fntFoot.GetHeight(g);
            float footY = y + Math.Max(0f, (18 - fhFoot) / 2f);
            g.DrawString($"Registros: {datos.Count}", fntFoot, System.Drawing.Brushes.Black, Lx + 2f, footY);

            using var fntTotal = new System.Drawing.Font("Arial", 11f, System.Drawing.FontStyle.Bold);
            using var bAzul = new System.Drawing.SolidBrush(azul);
            DrawCentrado(g, $"Total: Gs. {datos.Sum(f => f.Monto):N0}", fntTotal, bAzul, Lx, y - 2f, Pw, 22f);
        }

        return hayMas;
    }

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, System.Drawing.Color azul, float tx, float tpw)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx, 8, r5 * 2, r5 * 2, 180, 90);
        path.AddArc(tx + tpw - r5 * 2, 8, r5 * 2, r5 * 2, 270, 90);
        path.AddArc(tx + tpw - r5 * 2, 8 + 34 - r5 * 2, r5 * 2, r5 * 2, 0, 90);
        path.AddArc(tx, 8 + 34 - r5 * 2, r5 * 2, r5 * 2, 90, 90);
        path.CloseFigure();
        using var bBanda = new System.Drawing.SolidBrush(azul);
        g.FillPath(bBanda, path);
        using var fntTit = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
        DrawCentrado(g, "MULTAS A FUNCIONARIOS", fntTit, System.Drawing.Brushes.White, tx + 8, 8, tpw - 12, 34);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, MultasReportePagina p, float tx, float tpw)
    {
        var azul = System.Drawing.Color.FromArgb(21, 79, 110);
        using var penAT = new System.Drawing.Pen(azul, 1f);
        g.DrawLine(penAT, tx, 47, tx + tpw, 47);

        using var bSubBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(232, 240, 254));
        g.FillRectangle(bSubBg, tx, 50, tpw, 24f);
        using var penSubBd = new System.Drawing.Pen(System.Drawing.Color.FromArgb(187, 222, 251), 0.8f);
        g.DrawRectangle(penSubBd, tx, 50, tpw, 24f);

        using var fntSub  = new System.Drawing.Font("Arial", 7f, System.Drawing.FontStyle.Regular);
        using var bSubTxt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(13, 71, 161));
        var filtroTxt = string.IsNullOrEmpty(p.FiltroFuncionario) ? "Todos los funcionarios" : p.FiltroFuncionario;
        string linea1 = $"Período: {p.Periodo}   ●   Funcionario: {filtroTxt}";
        DrawCentrado(g, linea1, fntSub, bSubTxt, tx + 4, 50, tpw - 8, 12);
        DrawCentrado(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", fntSub, bSubTxt, tx + 4, 62, tpw - 8, 12);
    }

    private static void Celda(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        int col, int ry, int rh, System.Drawing.Brush br, bool right)
    {
        if (string.IsNullOrEmpty(txt)) return;
        float colX = Lx + Cws.Take(col).Sum();
        float colW = Cws[col];
        float maxW = colW - 4f;
        float fh   = f.GetHeight(g);
        float ty   = ry + Math.Max(0f, (rh - fh) / 2f);
        txt = Truncar(g, txt, f, maxW);
        float tx;
        if (right) { var sz = g.MeasureString(txt, f); tx = colX + colW - sz.Width - 2f; }
        else tx = colX + 2f;
        g.DrawString(txt, f, br, tx, ty);
    }

    private static string Truncar(System.Drawing.Graphics g, string txt, System.Drawing.Font f, float maxW)
    {
        if (g.MeasureString(txt, f).Width <= maxW) return txt;
        const string e = "…";
        float ew = g.MeasureString(e, f).Width;
        while (txt.Length > 0 && g.MeasureString(txt, f).Width + ew > maxW)
            txt = txt[..^1];
        return txt + e;
    }

    private static void DrawCentrado(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float y, float w, float h)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, x + Math.Max(0f, (w - sz.Width) / 2f), y + Math.Max(0f, (h - sz.Height) / 2f));
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
