namespace DayvpnBotWebApi.Shared
{
    public class ServiceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;

        private ServiceResult(bool success, string message)
        {
            IsSuccess = success;
            Message = message;
        }

        public static ServiceResult Success(string message = "")
        {
            return new ServiceResult(true, message);
        }

        public static ServiceResult Failed(string message = "")
        {
            return new ServiceResult(false, message);
        }
    }
}
