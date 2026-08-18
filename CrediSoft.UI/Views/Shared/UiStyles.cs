using System.Windows;
using System.Windows.Controls;

namespace CrediSoft.UI.Views.Shared;

// ─────────────────────────────────────────────────────────────────────────────
// Estilos reutilizables construidos en código (para ventanas 100% C#, sin XAML
// declarativo) — evita duplicar el mismo ControlTemplate en cada pantalla que
// lo necesita.
// ─────────────────────────────────────────────────────────────────────────────
public static class UiStyles
{
    // DatePicker moderno propio: el control nativo de WPF trae un chrome anticuado
    // (botón gris cuadrado estilo Windows XP) que ModernDP (GlobalStyles.xaml) no resuelve
    // del todo — ese estilo directamente OCULTA el botón de calendario en vez de rediseñarlo.
    // Este ControlTemplate respeta el contrato oficial de partes de DatePicker (PART_Root,
    // PART_TextBox, PART_Button, PART_Popup — ver Microsoft Learn "DatePicker Styles and
    // Templates") pero con un Border blanco redondeado + ícono de calendario Segoe MDL2
    // Assets en vez del botón nativo. XamlReader.Parse en vez de FrameworkElementFactory:
    // un ControlTemplate con VisualStateManager/Popup completo es mucho más confiable en
    // XAML literal que armado a mano factory por factory.
    public static Style ModernDatePickerStyle()
    {
        const string xaml = @"
<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
       xmlns:sys=""clr-namespace:System;assembly=mscorlib""
       TargetType=""DatePicker"">
    <Setter Property=""Background"" Value=""White""/>
    <Setter Property=""BorderBrush"" Value=""#BBDEFB""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""Height"" Value=""32""/>
    <Setter Property=""Template"">
        <Setter.Value>
            <ControlTemplate TargetType=""DatePicker"">
                <Grid x:Name=""PART_Root"">
                    <Border x:Name=""Bd"" Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""4"">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""*""/>
                                <ColumnDefinition Width=""Auto""/>
                            </Grid.ColumnDefinitions>
                            <DatePickerTextBox x:Name=""PART_TextBox"" Grid.Column=""0""
                                                Background=""Transparent"" BorderThickness=""0""
                                                Padding=""8,0,4,0"" VerticalContentAlignment=""Center""
                                                Focusable=""True""/>
                            <Button x:Name=""PART_Button"" Grid.Column=""1"" Width=""28""
                                    Background=""Transparent"" BorderThickness=""0"" Cursor=""Hand"" Focusable=""False"">
                                <TextBlock Text=""&#xE787;"" FontFamily=""Segoe MDL2 Assets""
                                           FontSize=""14"" Foreground=""#1565C0""
                                           HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                                <Button.Template>
                                    <ControlTemplate TargetType=""Button"">
                                        <Border Background=""{TemplateBinding Background}"">
                                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                                        </Border>
                                    </ControlTemplate>
                                </Button.Template>
                            </Button>
                        </Grid>
                    </Border>
                    <Popup x:Name=""PART_Popup"" Placement=""Bottom"" StaysOpen=""False""
                           AllowsTransparency=""True""/>
                    <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name=""CommonStates"">
                            <VisualState x:Name=""Normal""/>
                            <VisualState x:Name=""Disabled"">
                                <Storyboard>
                                    <DoubleAnimation Storyboard.TargetName=""Bd""
                                                     Storyboard.TargetProperty=""Opacity""
                                                     To=""0.5"" Duration=""0""/>
                                </Storyboard>
                            </VisualState>
                        </VisualStateGroup>
                        <VisualStateGroup x:Name=""ValidationStates"">
                            <VisualState x:Name=""Valid""/>
                            <VisualState x:Name=""InvalidFocused""/>
                            <VisualState x:Name=""InvalidUnfocused""/>
                        </VisualStateGroup>
                    </VisualStateManager.VisualStateGroups>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
    <Style.Triggers>
        <Trigger Property=""IsMouseOver"" Value=""True"">
            <Setter Property=""BorderBrush"" Value=""#1565C0""/>
        </Trigger>
    </Style.Triggers>
</Style>";
        return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
    }
}
