namespace PaintMixer.ViewModels
{
    public class ApiErrorModel
    {
        public string ResponseType { get; set; } = "Error";
        public List<string> ErrorMessages { get; set; } = new();
    }
}
