namespace DayvpnBotWebApi.Shared
{
    public class ServiceResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }

        protected ServiceResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public static ServiceResult Success(string message = "")
            => new ServiceResult(true, message);

        public static ServiceResult Failed(string message = "")
            => new ServiceResult(false, message);

        public bool IsFailed => !IsSuccess;
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; }
        private ServiceResult(bool isSuccess, string message, T? data = default)
            : base(isSuccess, message)
        {
            Data = data;
        }

        public static ServiceResult<T> Success(T data, string message = "")
            => new ServiceResult<T>(true, message, data);

        public static ServiceResult<T> Failed(T data, string message = "")
            => new ServiceResult<T>(false, message, data);

        public static ServiceResult<T> Success(string message = "")
            => new ServiceResult<T>(true, message);

        public static new ServiceResult<T> Failed(string message = "")
            => new ServiceResult<T>(false, message);
    }
}
