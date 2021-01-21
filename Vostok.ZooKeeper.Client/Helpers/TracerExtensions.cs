using Vostok.Tracing.Abstractions;
using Vostok.Tracing.Extensions.Custom;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;
using Vostok.ZooKeeper.Client.Operations;

namespace Vostok.ZooKeeper.Client.Helpers
{
    internal static class TracerExtensions
    {
        public static ICustomRequestClientSpanBuilder CreateSpan<TRequest, TResult>(this ITracer tracer, BaseOperation<TRequest, TResult> operation)
            where TRequest : ZooKeeperRequest
            where TResult : ZooKeeperResult =>
            tracer.BeginCustomRequestClientSpan(operation.GetType().Name.Replace("Operation", ""));

        public static void SetRequestDetails<TRequest>(this ICustomRequestClientSpanBuilder builder, TRequest request)
            where TRequest : ZooKeeperRequest
        {
            builder.SetCustomAnnotation("request.path", request.Path);

            switch (request)
            {
                case SetDataRequest setDataRequest:
                    builder.SetRequestDetails(setDataRequest.Data?.Length);
                    break;
                case CreateRequest createRequest:
                    builder.SetRequestDetails(createRequest.Data?.Length);
                    break;
            }

            if (request is GetRequest getRequest)
                builder.SetCustomAnnotation("request.watcher", getRequest.Watcher != null);
        }

        public static void SetResponseDetails<TResult>(this ICustomRequestClientSpanBuilder builder, TResult result)
            where TResult : ZooKeeperResult
        {
            var wellKnownStatus = result.IsSuccessful
                ? WellKnownStatuses.Success
                : result.Status.IsMundaneError()
                    ? WellKnownStatuses.Warning
                    : WellKnownStatuses.Error;

            long? size = null;
            if (result.IsSuccessful && result is GetDataResult getDataResult)
                size = getDataResult.Data?.Length;

            builder.SetResponseDetails(result.Status.ToString(), wellKnownStatus, size);

            builder.Dispose();
        }
    }
}