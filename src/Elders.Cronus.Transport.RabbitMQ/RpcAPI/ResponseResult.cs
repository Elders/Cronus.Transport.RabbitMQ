using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Elders.Cronus.Transport.RabbitMQ.RpcAPI
{
    //public interface IRpcResponse<out TResult> where TResult : class, IRpcResult
    //{
    //    [DataMember(Order = 1)]
    //    public IEnumerable<string> Errors { get; set; }

    //    [DataMember(Order = 2)]
    //    public bool IsSuccess { get { return Errors == null || !Errors.Any(); } }

    //    [DataMember(Order = 3)]
    //    public TResult Result { get; }
    //}

    //[DataContract(Name = "c86a9393-d89a-48d7-acaf-c1090d4b4219")]
    //public class ResponseResult<TResult> : IRpcResponse<TResult> where TResult : class, IRpcResult
    //{
    //    public ResponseResult() { }

    //    public ResponseResult(TResult result)
    //    {
    //        Result = result;
    //    }

    //    public ResponseResult(TResult result, params string[] errors)
    //    {
    //        Result = result;
    //        Errors = errors;
    //    }

    //    public ResponseResult(params string[] errors)
    //    {
    //        Errors = errors;
    //    }

    //    [DataMember(Order = 1)]
    //    public IEnumerable<string> Errors { get; set; }

    //    [DataMember(Order = 2)]
    //    public bool IsSuccess { get { return Errors == null || !Errors.Any(); } }

    //    [DataMember(Order = 3)]
    //    public TResult Result { get; }
    //}

    //public interface IRpcResponse
    //{
    //    [DataMember(Order = 1)]
    //    public IEnumerable<string> Errors { get; set; }

    //    [DataMember(Order = 2)]
    //    public bool IsSuccess { get { return Errors == null || !Errors.Any(); } }

    //    [DataMember(Order = 3)]
    //    public IRpcResult Result { get; set; }
    //}

    //[DataContract(Name = "c86a9393-d89a-48d7-acaf-c1090d4b4219")]
    //public class ResponseResult : IRpcResponse
    //{
    //    public ResponseResult() { }

    //    public ResponseResult(IRpcResult result)
    //    {
    //        Result = result;
    //    }

    //    public ResponseResult(IRpcResult result, params string[] errors)
    //    {
    //        Result = result;
    //        Errors = errors;
    //    }

    //    public ResponseResult(params string[] errors)
    //    {
    //        Errors = errors;
    //    }

    //    [DataMember(Order = 1)]
    //    public IEnumerable<string> Errors { get; set; }

    //    [DataMember(Order = 2)]
    //    public bool IsSuccess { get { return Errors == null || !Errors.Any(); } }

    //    [DataMember(Order = 3)]
    //    public IRpcResult Result { get; set; }
    //}
}
