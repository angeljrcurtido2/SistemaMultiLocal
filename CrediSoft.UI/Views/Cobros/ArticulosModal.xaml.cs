using System.Windows;

namespace CrediSoft.UI.Views.Cobros;

public record ArticuloVenta(string Descripcion, decimal Cantidad, decimal PVenta)
{
    public decimal Subtotal => Cantidad * PVenta;
}

public partial class ArticulosModal : Window
{
    public ArticulosModal(int idCab, IEnumerable<ArticuloVenta> articulos)
    {
        InitializeComponent();

        var lista = articulos.ToList();

        TxtTitulo.Text    = "ARTÍCULOS DEL CRÉDITO";
        TxtSubtitulo.Text = $"Crédito #{idCab}  —  {lista.Count} artículo(s)";

        GridArticulos.ItemsSource = lista;

        TxtCantItems.Text = lista.Count.ToString();
        TxtTotal.Text     = "Gs. " + lista.Sum(a => a.Subtotal).ToString("N0");
    }

    private void OnCerrar(object s, RoutedEventArgs e) => Close();
}
