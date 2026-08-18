using CrediSoft.Data.Repositories;
using CrediSoft.Core.Models;

namespace CrediSoft.UI.Views.Informes;

// Reporte impreso del botón "Imprimir" (arriba, imprime lo que se ve en pantalla) — a
// diferencia de ArticulosListadoPagina/Dibujador (agrupado por categoría, 3 columnas fijas
// Código/Artículo/Stock, usado solo por el "Buscar" del bloque Locales/cuadro amarillo),
// esto es una tabla PLANA con las mismas columnas que la grilla en pantalla, en hoja
// horizontal (landscape) para que entren sin amontonarse. TodosLosLocales=true agrega
// Max Cuota/CONTADO/Stock Total/desglose por local (sin ID); false usa las columnas del
// modo "Específico" (ID/Código/Descripción/P.Costo/P.Venta/Stock/PARCIAL) — pedido explícito
// del usuario: el botón Imprimir debe reflejar lo que se ve en pantalla en ambos modos.
public class ArticulosListadoTablaPagina
{
    public List<ArticuloListadoDetalleRow> Filas { get; init; } = new();
    public List<Local> Locales { get; init; } = new();
    public bool TodosLosLocales { get; init; }
    public string FechaImp { get; init; } = "";
    public string Usuario { get; init; } = "";
    public string Subtitulo { get; init; } = "";
    public string Impresora { get; init; } = "";
    public string LogoPath { get; init; } = "";
    public int TotalArticulos { get; init; }

    public static string ResolverLogoPath() => ArticulosListadoPagina.ResolverLogoPath();
}

public static class ArticulosListadoTablaDibujador
{
    // Modo "todos los locales": hoja A4 apaisada (horizontal) porque hay muchas columnas
    // (una por local). Modo "específico": solo 7 columnas fijas, entran cómodas en A4
    // vertical (portrait) — pedido explícito del usuario, no hace falta girar la hoja ahí.
    public static int PgWDe(bool todosLosLocales) => todosLosLocales ? 1169 : 827;
    public static int PgHDe(bool todosLosLocales) => todosLosLocales ? 827 : 1169;
    private const int Lx = 20;
    private static int PwDe(bool todosLosLocales) => PgWDe(todosLosLocales) - 40;
    private static int PieAreaYDe(bool todosLosLocales) => PgHDe(todosLosLocales) - 42;
    private const int PieH = 18;

    private const int FilaH = 22;
    private const int HdrH = 30;
    private const int BodyStartY = 90 + 2;

    private static readonly System.Drawing.Font FntHdr = new("Arial", 7.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow = new("Arial", 8f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntFoot = new("Arial", 9f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag = new("Arial", 8f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntTit = new("Arial", 13.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub = new("Arial", 8.5f, System.Drawing.FontStyle.Regular);

    private static readonly System.Drawing.SolidBrush BAzulOsc = new(System.Drawing.Color.FromArgb(14, 47, 68));
    private static readonly System.Drawing.SolidBrush BAzulMed = new(System.Drawing.Color.FromArgb(26, 79, 110));
    private static readonly System.Drawing.SolidBrush BSubTxt = new(System.Drawing.Color.FromArgb(80, 80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg = new(System.Drawing.Color.FromArgb(238, 244, 251));
    private static readonly System.Drawing.SolidBrush BPagNum = new(System.Drawing.Color.FromArgb(120, 120, 140));
    private static readonly System.Drawing.SolidBrush BAlt = new(System.Drawing.Color.FromArgb(248, 249, 252));

    private static readonly System.Drawing.Pen PenAzulTop = new(System.Drawing.Color.FromArgb(14, 47, 68), 3.5f);
    private static readonly System.Drawing.Pen PenAzul1 = new(System.Drawing.Color.FromArgb(14, 47, 68), 1.0f);
    private static readonly System.Drawing.Pen PenAzul15 = new(System.Drawing.Color.FromArgb(26, 79, 110), 1.5f);
    private static readonly System.Drawing.Pen PenGris190 = new(System.Drawing.Color.FromArgb(190, 190, 190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190, 190, 190), 0.5f);
    private static readonly System.Drawing.Pen PenHdrTop = new(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
    private static readonly System.Drawing.Pen PenSep = new(System.Drawing.Color.FromArgb(220, 228, 236), 0.8f);
    private static readonly System.Drawing.Pen PenPieLn = new(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
    private static readonly System.Drawing.Pen PenSubBd = new(System.Drawing.Color.FromArgb(220, 220, 230), 0.8f);

    // Columnas fijas (+ una por local en modo "todos los locales") — mismo orden que la
    // grilla en pantalla (VerArticulosListadoWindow.AplicarColumnasParaModo). El ancho por
    // local se reduce si hay muchos locales, para que la tabla siga entrando en el ancho
    // disponible de la hoja (Pw) en vez de desbordarse silenciosamente.
    private static (string[] Headers, int[] Widths) Columnas(ArticulosListadoTablaPagina p)
    {
        if (!p.TodosLosLocales)
        {
            // Modo "Específico": mismas columnas que la grilla en pantalla para un local
            // puntual (ID/Código/Descripción/P.Costo/P.Venta/Stock/PARCIAL). PARCIAL aclara
            // su fórmula en una segunda línea, igual que en la grilla en pantalla — mismo
            // motivo: el nombre solo no deja claro que es P.Costo × Stock, no un precio.
            return (
                new[] { "ID", "Código", "Descripción", "P.Costo", "P.Venta", "Stock", "PARCIAL\n(P.Costo × Stock)" },
                new[] { 45, 75, 320, 75, 75, 60, 95 }
            );
        }

        // "Todos los locales": ID sacado a pedido del usuario — libera espacio para el resto.
        var headers = new List<string> { "Código", "Descripción", "P.Costo", "Max Cuota", "CONTADO", "P.Venta", "Stock Total" };
        var fijos = new List<int> { 68, 195, 58, 48, 58, 58, 55 };
        int anchoFijos = fijos.Sum();
        int disponibleLocales = Math.Max(0, PwDe(true) - anchoFijos);
        int anchoLocal = p.Locales.Count > 0
            ? Math.Clamp(disponibleLocales / p.Locales.Count, 34, 60)
            : 0;

        var widths = new List<int>(fijos);
        foreach (var l in p.Locales)
        {
            headers.Add(l.NombreLocal);
            widths.Add(anchoLocal);
        }
        return (headers.ToArray(), widths.ToArray());
    }

    private static string[] ValoresFila(ArticuloListadoDetalleRow f, List<Local> locales, bool todosLosLocales)
    {
        if (!todosLosLocales)
        {
            return new[]
            {
                f.Id.ToString(), f.Codigo, f.Descripcion,
                f.PCosto.ToString("N0"), f.PVenta.ToString("N0"), f.Stock.ToString("N0"),
                f.Parcial.ToString("N0"),
            };
        }

        var vals = new List<string>
        {
            f.Codigo, f.Descripcion,
            f.PCosto.ToString("N0"), f.MaxCuota.ToString(), f.Contado.ToString("N0"),
            f.PVenta.ToString("N0"), f.Stock.ToString("N0"),
        };
        foreach (var l in locales)
        {
            var stockLocal = f.StockPorLocal.FirstOrDefault(s => s.IdLocal == l.IdLocal)?.S ?? 0;
            vals.Add(stockLocal.ToString("N0"));
        }
        return vals.ToArray();
    }

    public static int ContarPaginas(ArticulosListadoTablaPagina p)
    {
        if (p.Filas.Count == 0) return 1;
        var filaOffset = 0;
        var pageNum = 0;
        while (filaOffset < p.Filas.Count)
        {
            pageNum++;
            SimularPagina(p, ref filaOffset);
            if (pageNum > 5000) break;
        }
        return Math.Max(1, pageNum);
    }

    private static void SimularPagina(ArticulosListadoTablaPagina p, ref int filaOffset)
    {
        int y = BodyStartY + HdrH + 3;
        int limY = PieAreaYDe(p.TodosLosLocales) - 4;
        while (filaOffset < p.Filas.Count)
        {
            if (y + FilaH > limY) break;
            y += FilaH;
            filaOffset++;
        }
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        ArticulosListadoTablaPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

        int pgW = PgWDe(p.TodosLosLocales), pgH = PgHDe(p.TodosLosLocales);
        int pw = PwDe(p.TodosLosLocales), pieAreaY = PieAreaYDe(p.TodosLosLocales);

        var (headers, widths) = Columnas(p);
        var colX = ColX(widths);

        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawConOpacidad(g, logo,
                (int)(pgW / 2f - wmW / 2f),
                (int)(pgH / 2f - wmH / 2f + 40f),
                (int)wmW, (int)wmH, 0.05f);
        }

        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, pgW, 88);
        g.DrawLine(PenAzulTop, 0, 2, pgW, 2);

        float tx = Lx, tpw = pw;
        if (logo != null)
        {
            float logoH2 = 52f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);
            float sepX = Lx + logoW2 + 12f;
            g.DrawLine(PenGris190, sepX, 6, sepX, 78);
            tx = sepX + 10f; tpw = pw - (sepX - Lx) - 10f;
        }
        DibujarBandaTitulo(g, tx, tpw, p.TodosLosLocales);
        DibujarSubtitulos(g, p, tx, tpw);

        g.DrawLine(PenAzulTop, 0, 80, pgW, 80);
        g.DrawLine(PenGris05, 0, 82, pgW, 82);

        int y = 90;
        g.DrawLine(PenHdrTop, Lx, y, Lx + pw, y);
        y += 2;

        g.FillRectangle(BAzulOsc, Lx, y, pw, HdrH);
        var aligns = new System.Drawing.StringAlignment[headers.Length];
        // "Todos los locales": Código (0) y Descripción (1) a la izquierda, resto a la
        // derecha. "Específico": ID (0) a la derecha, Código (1) y Descripción (2) a la
        // izquierda, resto a la derecha.
        int[] izquierda = p.TodosLosLocales ? new[] { 0, 1 } : new[] { 1, 2 };
        for (int i = 0; i < aligns.Length; i++)
            aligns[i] = izquierda.Contains(i) ? System.Drawing.StringAlignment.Near : System.Drawing.StringAlignment.Far;
        DibujarCeldas(g, headers, widths, colX, y, FntHdr, System.Drawing.Brushes.White, HdrH, aligns);
        y += HdrH;
        g.DrawLine(PenAzul15, Lx, y, Lx + pw, y);
        y += 3;

        bool hayMas = false;
        int limY = pieAreaY - 4;
        int filaIdx = 0;

        while (filaOffset < p.Filas.Count)
        {
            if (y + FilaH > limY) { hayMas = true; break; }

            var f = p.Filas[filaOffset];
            if (filaIdx % 2 == 0)
                g.FillRectangle(BAlt, Lx, y, pw, FilaH);

            var vals = ValoresFila(f, p.Locales, p.TodosLosLocales);
            DibujarCeldas(g, vals, widths, colX, y, FntRow, System.Drawing.Brushes.Black, FilaH, aligns);
            g.DrawLine(PenSep, Lx, y + FilaH - 1, Lx + pw, y + FilaH - 1);

            y += FilaH;
            filaOffset++;
            filaIdx++;
        }

        g.DrawLine(hayMas ? PenPieLn : PenAzul15, Lx, pieAreaY, Lx + pw, pieAreaY);

        float fhFoot = FntFoot.GetHeight(g);
        float footY = pieAreaY + 4 + Math.Max(0f, (PieH - fhFoot) / 2f);

        if (!hayMas)
            g.DrawString($"Total de artículos: {p.TotalArticulos:N0}", FntFoot, System.Drawing.Brushes.Black, Lx + 2f, footY);

        string pagTxt = $"Página {pageNum} de {totalPages}";
        var szPag = g.MeasureString(pagTxt, FntPag);
        g.DrawString(pagTxt, FntPag, BPagNum, Lx + pw - szPag.Width, pgH - 18);

        return hayMas;
    }

    private static float[] ColX(int[] cws)
    {
        var x = new float[cws.Length];
        float acc = Lx;
        for (int i = 0; i < cws.Length; i++) { x[i] = acc; acc += cws[i]; }
        return x;
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
            float cW = cws[i], maxW = cW - 6f;

            // Header de 2 líneas (ej. "PARCIAL\n(P.Costo × Stock)") — se centra como bloque,
            // cada línea alineada por separado, en vez de tratarlo como texto de una sola
            // línea (que quedaba descentrado verticalmente y mal alineado a la derecha por
            // medir el bloque completo en vez de cada renglón).
            if (t.Contains('\n'))
            {
                var lineas = t.Split('\n');
                float lineH = fnt.GetHeight(g);
                float bloqueH = lineH * lineas.Length;
                float lineY = y + Math.Max(0f, (rowH - bloqueH) / 2f);
                foreach (var linea in lineas)
                {
                    var l = linea;
                    Trunc(g, ref l, fnt, maxW);
                    float lx = cx[i] + 3f;
                    if (aligns[i] == System.Drawing.StringAlignment.Far)
                    {
                        var szL = g.MeasureString(l, fnt);
                        lx = cx[i] + cW - szL.Width - 4f;
                    }
                    else if (aligns[i] == System.Drawing.StringAlignment.Center)
                    {
                        var szL = g.MeasureString(l, fnt);
                        lx = cx[i] + Math.Max(0f, (cW - szL.Width) / 2f);
                    }
                    g.DrawString(l, fnt, br, lx, lineY);
                    lineY += lineH;
                }
                continue;
            }

            Trunc(g, ref t, fnt, maxW);
            float x = cx[i] + 3f;
            if (aligns[i] == System.Drawing.StringAlignment.Far)
            {
                var szT = g.MeasureString(t, fnt);
                x = cx[i] + cW - szT.Width - 4f;
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

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, float tx, float tpw, bool todosLosLocales)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx, 8, r5 * 2, r5 * 2, 180, 90);
        path.AddArc(tx + tpw - r5 * 2, 8, r5 * 2, r5 * 2, 270, 90);
        path.AddArc(tx + tpw - r5 * 2, 8 + 36 - r5 * 2, r5 * 2, r5 * 2, 0, 90);
        path.AddArc(tx, 8 + 36 - r5 * 2, r5 * 2, r5 * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(BAzulMed, path);
        g.FillRectangle(BAzulOsc, tx, 8, 4, 36);
        var titulo = todosLosLocales ? "LISTADO DE ARTÍCULOS — TODOS LOS LOCALES" : "LISTADO DE ARTÍCULOS";
        DrawCentrado(g, titulo, FntTit, System.Drawing.Brushes.White, tx + 8, 8, tpw - 12, 36);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, ArticulosListadoTablaPagina p, float tx, float tpw)
    {
        g.DrawLine(PenAzul1, tx, 50, tx + tpw, 50);
        g.FillRectangle(BSubBg, tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        DrawCentrado(g, p.Subtitulo, FntSub, BSubTxt, tx + 4, 53, tpw - 8, 12);
        DrawCentrado(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", FntSub, BSubTxt, tx + 4, 65, tpw - 8, 12);
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
