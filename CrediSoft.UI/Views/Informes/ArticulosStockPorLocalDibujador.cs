using CrediSoft.Core.Models;
using CrediSoft.Data.Repositories;

namespace CrediSoft.UI.Views.Informes;

// Una fila del reporte "Stock por Local": header de grupo, artículo, o subtotal de cierre de
// grupo — mismo patrón de tres tipos que FilaListado (ArticulosListadoDibujador), pero acá el
// "Stock" no es un solo valor: es un array con un valor por cada local en _locales, en el
// mismo orden.
public record FilaStockLocal(
    bool EsHeaderGrupo, bool EsSubtotalGrupo,
    string Grupo, string Codigo, string Descripcion,
    decimal[] StockPorLocal, decimal Total);

public class ArticulosStockPorLocalPagina
{
    public List<FilaStockLocal> Filas { get; init; } = new();
    public List<Local> Locales        { get; init; } = new();
    public string FechaImp   { get; init; } = "";
    public string Usuario    { get; init; } = "";
    public string Subtitulo  { get; init; } = "";
    public string Impresora  { get; init; } = "";
    public string LogoPath   { get; init; } = "";
    public int    TotalArticulos { get; init; }
    public decimal[] TotalPorLocal { get; init; } = Array.Empty<decimal>();
    public decimal   TotalGeneral  { get; init; }

    public static string ResolverLogoPath() => CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath();

    public static List<FilaStockLocal> Aplanar(IEnumerable<ArticuloListadoRow> filas, List<Local> locales)
    {
        var result = new List<FilaStockLocal>();
        string? grupoActual = null;
        var subPorLocal = new decimal[locales.Count];
        decimal subTotal = 0;

        void CerrarGrupoActual()
        {
            if (grupoActual == null) return;
            result.Add(new FilaStockLocal(false, true, grupoActual, "", $"Subtotal {grupoActual}",
                (decimal[])subPorLocal.Clone(), subTotal));
        }

        foreach (var f in filas)
        {
            if (grupoActual == null || f.Grupo != grupoActual)
            {
                CerrarGrupoActual();
                grupoActual = f.Grupo;
                subPorLocal = new decimal[locales.Count];
                subTotal = 0;
                result.Add(new FilaStockLocal(true, false, f.Grupo, "", "", Array.Empty<decimal>(), 0));
            }

            var porLocalDicc = f.StockPorLocal.ToDictionary(s => (int)s.IdLocal, s => s.S);
            var valores = new decimal[locales.Count];
            for (int i = 0; i < locales.Count; i++)
            {
                var v = porLocalDicc.TryGetValue(locales[i].IdLocal, out var s) ? s : 0;
                valores[i] = v;
                subPorLocal[i] += v;
            }
            var total = valores.Sum();
            subTotal += total;

            result.Add(new FilaStockLocal(false, false, f.Grupo, f.Codigo, f.D, valores, total));
        }
        CerrarGrupoActual();
        return result;
    }
}

// Reporte apaisado (A4 horizontal) con una columna de stock por cada local — pedido explícito
// para ver de un vistazo cómo se reparte el stock de cada artículo entre las sucursales, en
// vez de un solo número de stock total agregado.
public static class ArticulosStockPorLocalDibujador
{
    // A4 horizontal: 1169×827 (invertido respecto al vertical 827×1169 que usa el resto de
    // los reportes de esta pantalla) — mismo criterio ya usado en ArticulosListadoTablaDibujador
    // para el modo "todos los locales" de la grilla en pantalla.
    private const int PgW = 1169;
    private const int PgH = 827;
    private const int Lx  = 20;
    private const int Pw  = PgW - 40;
    private const int PieAreaY = PgH - 42;
    private const int PieH = 18;

    private const int GrupoH = 28;
    private const int FilaH  = 20;
    private const int HdrH   = 34; // dos líneas: nombre local partido si hace falta
    private const int BodyStartY = 90 + 2;

    private static readonly System.Drawing.Font FntHdr    = new("Arial", 7f,   System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntGrupo  = new("Arial", 10.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow    = new("Arial", 8f,   System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntFoot   = new("Arial", 8f,   System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag    = new("Arial", 8f,   System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntTit    = new("Arial", 13.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub    = new("Arial", 8.5f,  System.Drawing.FontStyle.Regular);

    private static readonly System.Drawing.SolidBrush BAzulOsc  = new(System.Drawing.Color.FromArgb(14, 47, 68));
    private static readonly System.Drawing.SolidBrush BAzulMed  = new(System.Drawing.Color.FromArgb(26, 79, 110));
    private static readonly System.Drawing.SolidBrush BSubTxt   = new(System.Drawing.Color.FromArgb(80, 80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg    = new(System.Drawing.Color.FromArgb(238, 244, 251));
    private static readonly System.Drawing.SolidBrush BPagNum   = new(System.Drawing.Color.FromArgb(120,120,140));
    private static readonly System.Drawing.SolidBrush BAlt      = new(System.Drawing.Color.FromArgb(248,249,252));
    private static readonly System.Drawing.SolidBrush BSubtotal = new(System.Drawing.Color.FromArgb(222,232,243));
    private static readonly System.Drawing.SolidBrush BCeroTxt  = new(System.Drawing.Color.FromArgb(190,190,190));

    private static readonly System.Drawing.Pen PenAzulTop = new(System.Drawing.Color.FromArgb(14, 47, 68), 3.5f);
    private static readonly System.Drawing.Pen PenAzul1   = new(System.Drawing.Color.FromArgb(14, 47, 68), 1.0f);
    private static readonly System.Drawing.Pen PenAzul15  = new(System.Drawing.Color.FromArgb(26, 79, 110), 1.5f);
    private static readonly System.Drawing.Pen PenGris190 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05  = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenHdrTop  = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSep     = new(System.Drawing.Color.FromArgb(220,228,236), 0.8f);
    private static readonly System.Drawing.Pen PenPieLn   = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSubBd   = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);
    private static readonly System.Drawing.Pen PenColSep  = new(System.Drawing.Color.FromArgb(210,215,225), 0.6f);

    // Columnas fijas (Código + Artículo) + N columnas de local + 1 de Total.
    private const int CwCodigo = 65;
    private const int CwTotal  = 55;

    private static (int[] cw, float[] cx, int cwArticulo) CalcularColumnas(int cantLocales)
    {
        // El ancho restante después de Código y Total se reparte entre Artículo (que se
        // lleva una porción fija mayor, ~180px, para no truncar demasiado las descripciones)
        // y las N columnas de local (mínimo 34px cada una, suficiente para "9999" + margen).
        int restante = Pw - CwCodigo - CwTotal;
        int cwArticulo = Math.Min(180, restante / 3);
        int restanteLocales = restante - cwArticulo;
        int cwLocal = Math.Max(34, restanteLocales / Math.Max(1, cantLocales));

        var cw = new List<int> { CwCodigo, cwArticulo };
        for (int i = 0; i < cantLocales; i++) cw.Add(cwLocal);
        cw.Add(CwTotal);

        var cwArr = cw.ToArray();
        var cx = new float[cwArr.Length];
        float acc = Lx;
        for (int i = 0; i < cwArr.Length; i++) { cx[i] = acc; acc += cwArr[i]; }
        return (cwArr, cx, cwArticulo);
    }

    public static int ContarPaginas(ArticulosStockPorLocalPagina p)
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

    private static void SimularPagina(ArticulosStockPorLocalPagina p, ref int filaOffset)
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
        ArticulosStockPorLocalPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        var (cw, cx, _) = CalcularColumnas(p.Locales.Count);

        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 88);
        g.DrawLine(PenAzulTop, 0, 2, PgW, 2);

        float tx = Lx, tpw = Pw;
        if (logo != null)
        {
            float logoH2 = 56f, logoW2 = logo.Width * (logoH2 / logo.Height);
            g.DrawImage(logo, Lx, 8, logoW2, logoH2);
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

        // ── Encabezado de columnas ────────────────────────────────────────────
        g.FillRectangle(BAzulOsc, Lx, y, Pw, HdrH);
        var sfC = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
        var sfL = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Near, LineAlignment = System.Drawing.StringAlignment.Center };
        g.DrawString("Código",  FntHdr, System.Drawing.Brushes.White, new System.Drawing.RectangleF(cx[0]+3, y, cw[0]-4, HdrH), sfL);
        g.DrawString("Artículo", FntHdr, System.Drawing.Brushes.White, new System.Drawing.RectangleF(cx[1]+3, y, cw[1]-4, HdrH), sfL);
        for (int i = 0; i < p.Locales.Count; i++)
        {
            var nombre = p.Locales[i].NombreLocal;
            var texto = $"{p.Locales[i].IdLocal} — {nombre}";
            g.DrawString(texto, FntHdr, System.Drawing.Brushes.White,
                new System.Drawing.RectangleF(cx[2+i]+1, y, cw[2+i]-2, HdrH), sfC);
        }
        g.DrawString("TOTAL", FntHdr, System.Drawing.Brushes.White,
            new System.Drawing.RectangleF(cx[^1]+1, y, cw[^1]-2, HdrH), sfC);
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
                g.FillRectangle(BSubtotal, Lx, y, Pw, FilaH);
                DibujarFilaValores(g, f, cw, cx, y, FntFoot, negrita: true);
                g.DrawLine(PenAzul15, Lx, y + FilaH - 1, Lx + Pw, y + FilaH - 1);
            }
            else
            {
                if (artIdx % 2 == 0)
                    g.FillRectangle(BAlt, Lx, y, Pw, FilaH);
                DibujarFilaValores(g, f, cw, cx, y, FntRow, negrita: false);
                g.DrawLine(PenSep, Lx, y + FilaH - 1, Lx + Pw, y + FilaH - 1);
                artIdx++;
            }

            // Separadores verticales sutiles entre columnas de local — sin esto, con tantas
            // columnas angostas juntas, se hace difícil distinguir a qué local pertenece cada
            // número con solo un vistazo rápido.
            for (int i = 0; i < cw.Length - 1; i++)
                g.DrawLine(PenColSep, cx[i] + cw[i], y, cx[i] + cw[i], y + h);

            y += h;
            filaOffset++;
        }

        g.DrawLine(hayMas ? PenPieLn : PenAzul15, Lx, PieAreaY, Lx + Pw, PieAreaY);

        float fhFoot = FntFoot.GetHeight(g);
        float footY  = PieAreaY + 4 + Math.Max(0f, (PieH - fhFoot) / 2f);

        if (!hayMas)
        {
            var filaTotal = new FilaStockLocal(false, true, "", "", $"TOTAL GENERAL ({p.TotalArticulos:N0} artículos)",
                p.TotalPorLocal, p.TotalGeneral);
            DibujarFilaValores(g, filaTotal, cw, cx, (int)(footY - 2), FntFoot, negrita: true);
        }

        string pagTxt = $"Página {pageNum} de {totalPages}";
        var szPag = g.MeasureString(pagTxt, FntPag);
        g.DrawString(pagTxt, FntPag, BPagNum, Lx + Pw - szPag.Width, PgH - 18);

        return hayMas;
    }

    private static void DibujarFilaValores(
        System.Drawing.Graphics g, FilaStockLocal f,
        int[] cw, float[] cx, int y, System.Drawing.Font fnt, bool negrita)
    {
        var sfL = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Near, Trimming = System.Drawing.StringTrimming.EllipsisCharacter };
        var sfR = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Far };
        float fh = fnt.GetHeight(g), ty = y + Math.Max(0f, (FilaH - fh) / 2f);

        if (!string.IsNullOrEmpty(f.Codigo))
            g.DrawString(f.Codigo, fnt, System.Drawing.Brushes.Black, new System.Drawing.RectangleF(cx[0]+3, ty, cw[0]-4, fh+2), sfL);
        g.DrawString(f.Descripcion, fnt, System.Drawing.Brushes.Black, new System.Drawing.RectangleF(cx[1]+3, ty, cw[1]-4, fh+2), sfL);

        for (int i = 0; i < f.StockPorLocal.Length; i++)
        {
            var v = f.StockPorLocal[i];
            var txt = v == 0 ? "0" : v.ToString("N0");
            var br  = v == 0 && !negrita ? BCeroTxt : (System.Drawing.Brush)System.Drawing.Brushes.Black;
            g.DrawString(txt, fnt, br, new System.Drawing.RectangleF(cx[2+i]+2, ty, cw[2+i]-4, fh+2), sfR);
        }

        g.DrawString(f.Total.ToString("N0"), fnt, System.Drawing.Brushes.Black,
            new System.Drawing.RectangleF(cx[^1]+2, ty, cw[^1]-4, fh+2), sfR);
    }

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, float tx, float tpw)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx,           8,          r5*2, r5*2, 180, 90);
        path.AddArc(tx+tpw-r5*2, 8,          r5*2, r5*2, 270, 90);
        path.AddArc(tx+tpw-r5*2, 8+36-r5*2,  r5*2, r5*2,   0, 90);
        path.AddArc(tx,           8+36-r5*2,  r5*2, r5*2,  90, 90);
        path.CloseFigure();
        g.FillPath(BAzulMed, path);
        g.FillRectangle(BAzulOsc, tx, 8, 4, 36);
        var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
        g.DrawString("STOCK POR LOCAL", FntTit, System.Drawing.Brushes.White, new System.Drawing.RectangleF(tx+8, 8, tpw-12, 36), sf);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, ArticulosStockPorLocalPagina p, float tx, float tpw)
    {
        g.DrawLine(PenAzul1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg,  tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
        g.DrawString(p.Subtitulo, FntSub, BSubTxt, new System.Drawing.RectangleF(tx+4, 53, tpw-8, 12), sf);
        g.DrawString($"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", FntSub, BSubTxt, new System.Drawing.RectangleF(tx+4, 65, tpw-8, 12), sf);
    }
}
