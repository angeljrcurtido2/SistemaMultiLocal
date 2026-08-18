using CrediSoft.Core.Models;

namespace CrediSoft.UI.Views.Maestros;

public class LocalesPagina
{
    public List<Local> Filas    { get; init; } = new();
    public string      Filtro   { get; init; } = "";
    public string      FechaImp { get; init; } = "";
    public string      Usuario  { get; init; } = "";
    public string      LogoPath { get; init; } = "";
}

public static class LocalesDibujador
{
    private const int PgW      = 827;
    private const int PgH      = 1169;
    private const int Lx       = 20;
    private const int Pw       = PgW - 40;   // 787
    private const int PieAreaY = PgH - 42;
    private const int PieH     = 18;
    private const int FilaH    = 26;
    private const int HdrH     = 24;
    private const int BodyY    = 92 + HdrH + 3;

    // ID | Nombre | Dirección | Ciudad | Teléfono
    // 44 + 195 + 220 + 195 + 133 = 787
    private static readonly int[]    Cw  = {  44, 195, 220, 195, 133 };
    private static readonly float[]  Cx  = { 20f, 64f, 259f, 479f, 674f };
    private static readonly string[] Hdr = { "ID", "Nombre", "Dirección", "Ciudad", "Teléfono" };

    private static readonly System.Drawing.Font FntTit  = new("Arial", 12f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub  = new("Arial",  7f,  System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntHdr  = new("Arial",  8f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow  = new("Arial",  8.5f,System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntFoot = new("Arial",  7.5f,System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag  = new("Arial",  6.5f,System.Drawing.FontStyle.Regular);

    private static readonly System.Drawing.SolidBrush BNaranja = new(System.Drawing.Color.FromArgb(230, 81,   0));
    private static readonly System.Drawing.SolidBrush BRojo    = new(System.Drawing.Color.FromArgb(210,  0,   0));
    private static readonly System.Drawing.SolidBrush BSubTxt  = new(System.Drawing.Color.FromArgb( 80, 80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg   = new(System.Drawing.Color.FromArgb(245,245, 248));
    private static readonly System.Drawing.SolidBrush BPagNum  = new(System.Drawing.Color.FromArgb(120,120, 140));
    private static readonly System.Drawing.SolidBrush BAlt     = new(System.Drawing.Color.FromArgb(248,249, 252));
    private static readonly System.Drawing.SolidBrush BHdr1    = new(System.Drawing.Color.FromArgb( 13, 71, 161));

    private static readonly System.Drawing.Pen PenRojo   = new(System.Drawing.Color.FromArgb(210, 0,  0), 3.5f);
    private static readonly System.Drawing.Pen PenRojo1  = new(System.Drawing.Color.FromArgb(210, 0,  0), 1.0f);
    private static readonly System.Drawing.Pen PenGris19 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenAzul15 = new(System.Drawing.Color.FromArgb( 13, 71,161), 1.5f);
    private static readonly System.Drawing.Pen PenSep    = new(System.Drawing.Color.FromArgb(200,210,225), 0.5f);
    private static readonly System.Drawing.Pen PenHdrTop = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSubBd  = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);
    private static readonly System.Drawing.Pen PenPie    = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);

    public static int ContarPaginas(LocalesPagina p)
    {
        if (p.Filas.Count == 0) return 1;
        int area      = PieAreaY - BodyY;
        int porPagina = area / FilaH;
        return (p.Filas.Count + porPagina - 1) / porPagina;
    }

    public static void DibujarPagina(
        System.Drawing.Graphics g,
        LocalesPagina p,
        System.Drawing.Image? logo,
        int pageOffset,
        int pageNum,
        int totalPages)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        // Marca de agua
        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawImg(g, logo, (int)(PgW/2f-wmW/2f), (int)(PgH/2f-wmH/2f+40f), (int)wmW, (int)wmH, 0.055f);
        }

        // Encabezado
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 88);
        g.DrawLine(PenRojo, 0, 2, PgW, 2);

        float tx = Lx, tpw = Pw;
        if (logo != null)
        {
            float lh = 62f, lw = logo.Width * (lh / logo.Height);
            DrawImg(g, logo, Lx, 8, (int)lw, (int)lh, 1f);
            float sx = Lx + lw + 12f;
            g.DrawLine(PenGris19, sx, 6, sx, 78);
            tx = sx + 10f; tpw = Pw - (sx - Lx) - 10f;
        }

        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            const float r = 5f;
            path.AddArc(tx,          8,        r*2, r*2, 180, 90);
            path.AddArc(tx+tpw-r*2,  8,        r*2, r*2, 270, 90);
            path.AddArc(tx+tpw-r*2,  8+36-r*2, r*2, r*2, 0,   90);
            path.AddArc(tx,          8+36-r*2, r*2, r*2, 90,  90);
            path.CloseFigure();
            g.FillPath(BNaranja, path);
        }
        g.FillRectangle(BRojo, tx, 8, 4, 36);
        const string titulo = "LISTADO DE LOCALES / SUCURSALES";
        var szT = g.MeasureString(titulo, FntTit);
        g.DrawString(titulo, FntTit, System.Drawing.Brushes.White,
            tx + 8 + (tpw - 12 - szT.Width) / 2f, 8 + (36 - szT.Height) / 2f);

        g.DrawLine(PenRojo1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg, tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        var sub = string.IsNullOrEmpty(p.Filtro) ? "Todos los locales / sucursales" : p.Filtro;
        DrawC(g, sub,                                               FntSub, BSubTxt, tx+4, 53, tpw-8, 12);
        DrawC(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", FntSub, BSubTxt, tx+4, 65, tpw-8, 12);

        g.DrawLine(PenRojo,   0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);

        int y = 90;

        // Cabecera columnas
        g.DrawLine(PenHdrTop, Lx, y, Lx+Pw, y); y += 2;
        g.FillRectangle(BHdr1, Lx, y, Pw, HdrH);
        float fhH = FntHdr.GetHeight(g), tyH = HdrH/2f - fhH/2f;
        for (int i = 0; i < Hdr.Length; i++)
            g.DrawString(Hdr[i], FntHdr, System.Drawing.Brushes.White, Cx[i]+4f, y+tyH);
        y += HdrH;
        g.DrawLine(PenAzul15, Lx, y, Lx+Pw, y); y += 3;

        // Filas de esta página
        float fhR = FntRow.GetHeight(g), tyR = Math.Max(0f, (FilaH - fhR) / 2f);
        int area      = PieAreaY - BodyY;
        int porPagina = area / FilaH;
        int start     = pageOffset;
        int end       = Math.Min(start + porPagina, p.Filas.Count);

        for (int i = start; i < end; i++)
        {
            var f = p.Filas[i];
            if (i % 2 == 1) g.FillRectangle(BAlt, Lx, y, Pw, FilaH);

            float ty1 = y + tyR;
            DC(g, f.IdLocal.ToString(),  0, ty1);
            DC(g, f.NombreLocal,         1, ty1);
            DC(g, f.DireccionLocal,      2, ty1);
            DC(g, f.CiudadLocal,         3, ty1);
            DC(g, f.TelefonoLocal,       4, ty1);

            y += FilaH;
            g.DrawLine(PenSep, Lx, y, Lx+Pw, y);
        }

        // Pie
        bool esUltima = end >= p.Filas.Count;
        g.DrawLine(esUltima ? PenAzul15 : PenPie, Lx, PieAreaY, Lx+Pw, PieAreaY);
        if (esUltima)
        {
            float fhF = FntFoot.GetHeight(g), fy = PieAreaY+4+Math.Max(0f,(PieH-fhF)/2f);
            g.DrawString($"Total registros: {p.Filas.Count}", FntFoot, System.Drawing.Brushes.Black, Lx+2f, fy);
        }
        var pag = $"Página {pageNum} de {totalPages}";
        var szP = g.MeasureString(pag, FntPag);
        g.DrawString(pag, FntPag, BPagNum, Lx+Pw-szP.Width, PgH-18);
    }

    private static void DC(System.Drawing.Graphics g, string txt, int col, float ty)
    {
        var t = Trunc(g, txt, FntRow, Cw[col] - 8f);
        g.DrawString(t, FntRow, System.Drawing.Brushes.Black, Cx[col]+4f, ty);
    }

    private static string Trunc(System.Drawing.Graphics g, string t, System.Drawing.Font f, float mw)
    {
        if (string.IsNullOrEmpty(t) || g.MeasureString(t, f).Width <= mw) return t;
        float ew = g.MeasureString("…", f).Width;
        while (t.Length > 0 && g.MeasureString(t, f).Width + ew > mw) t = t[..^1];
        return t + "…";
    }

    private static void DrawC(System.Drawing.Graphics g, string t, System.Drawing.Font f,
        System.Drawing.Brush b, float x, float y, float w, float h)
    {
        var s = g.MeasureString(t, f);
        g.DrawString(t, f, b, x+Math.Max(0f,(w-s.Width)/2f), y+Math.Max(0f,(h-s.Height)/2f));
    }

    private static void DrawImg(System.Drawing.Graphics g, System.Drawing.Image img,
        int x, int y, int w, int h, float op)
    {
        var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = op };
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default,
            System.Drawing.Imaging.ColorAdjustType.Bitmap);
        g.DrawImage(img, new System.Drawing.Rectangle(x, y, w, h),
            0, 0, img.Width, img.Height, System.Drawing.GraphicsUnit.Pixel, ia);
    }
}
