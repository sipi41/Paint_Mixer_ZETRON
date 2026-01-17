namespace PaintMixer.ViewModels
{
    public class ApiResponseModel
    {
        public string Type { get; set; } = ApiResponseTypes.Success.ToString();
        public int Code { get; set; } 
        public string Description { get; set; }
    }

    public enum ApiResponseTypes
    {
        Success,
        Error,
        Warning,
    }

}
