using CoordinatesTinyApp.Infrastructure;

namespace CoordinatesTinyApp.ViewModels;

internal sealed class MatrixCellViewModel : ObservableObject
{
    private string _text = "";

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }
}
