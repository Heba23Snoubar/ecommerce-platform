using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Module.Application.Common.Results
{
     
    public class ServiceResult
    {
        public bool IsSuccess { get; }
        public string? Error { get; }
        public ServiceErrorType? ErrorType { get; }

        protected ServiceResult(
            bool isSuccess,
            string? error,
            ServiceErrorType? errorType)
        {
            IsSuccess = isSuccess;
            Error = error;
            ErrorType = errorType;
        }

        public static ServiceResult Success()
            => new(true, null, null);

        public static ServiceResult Failure(
            string error,
            ServiceErrorType errorType = ServiceErrorType.Validation)
            => new(false, error, errorType);
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; }

        private ServiceResult(
            bool isSuccess,
            T? data,
            string? error,
            ServiceErrorType? errorType)
            : base(isSuccess, error, errorType)
        {
            Data = data;
        }

        public static ServiceResult<T> Success(T data)
            => new(true, data, null, null);

        public new static ServiceResult<T> Failure(
            string error,
            ServiceErrorType errorType = ServiceErrorType.Validation)
            => new(false, default, error, errorType);
    }
}
