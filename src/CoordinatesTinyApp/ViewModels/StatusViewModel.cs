using CoordinatesTinyApp.Infrastructure;

namespace CoordinatesTinyApp.ViewModels;

internal abstract class StatusViewModel : ObservableObject
{
    private string _statusMessage = "";
    private string _statusColor = "#4B5563";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string StatusColor
    {
        get => _statusColor;
        private set => SetProperty(ref _statusColor, value);
    }

    protected void ShowStatus(string message, string color = "DarkSlateGray")
    {
        StatusMessage = message;
        StatusColor = color;
    }
}
