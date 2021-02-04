using Vostok.Tracing.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;
using Vostok.ZooKeeper.Client.Operations;

namespace Vostok.ZooKeeper.Client.Helpers
{
    internal static class TracerExtensions
    {
        public static ISpanBuilder CreateSpan<TRequest, TResult>(this ITracer tracer, BaseOperation<TRequest, TResult> operation)
            where TRequest : ZooKeeperRequest
            where TResult : ZooKeeperResult =>
            tracer.BeginCustomRequestClientSpan(operation.GetType().Name.Replace("Operation", ""));

        public static void SetRequestDetails<TRequest>(this ISpanBuilder builder, TRequest request)
            where TRequest : ZooKeeperRequest
        {
            builder.SetCustomAnnotation("request.path", request.Path);

            switch (request)
            {
                case SetDataRequest setDataRequest:
                    builder.SetRequestDetails((long?)setDataRequest.Data?.Length);
                    break;
                case CreateRequest createRequest:
                    builder.SetRequestDetails((long?)createRequest.Data?.Length);
                    break;
            }

            if (request is GetRequest getRequest)
                builder.SetCustomAnnotation("request.watcher", getRequest.Watcher != null);
        }

        public static void SetResponseDetails<TResult>(this ISpanBuilder builder, TResult result)
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

        #region TracingExtensions

        private static ISpanBuilder BeginCustomRequestClientSpan(this ITracer tracer, string operation)
        {
            var span = tracer.BeginSpan();

            span.SetAnnotation(WellKnownAnnotations.Common.Kind, WellKnownSpanKinds.Custom.Client);
            span.SetAnnotation(WellKnownAnnotations.Common.Operation, operation);

            return span;
        }

        private static void SetRequestDetails(this ISpanBuilder builder, long? size)
        {
            if (size.HasValue)
                builder.SetAnnotation(WellKnownAnnotations.Custom.Request.Size, size.Value);
        }

        private static void SetResponseDetails(this ISpanBuilder builder, string customStatus, string wellKnownStatus, long? size)
        {
            if (customStatus != null)
                builder.SetAnnotation(WellKnownAnnotations.Custom.Response.Status, customStatus);

            if (wellKnownStatus != null)
                builder.SetAnnotation(WellKnownAnnotations.Common.Status, wellKnownStatus);

            if (size.HasValue)
                builder.SetAnnotation(WellKnownAnnotations.Custom.Response.Size, size.Value);
        }

        public static void SetTargetDetails(this ISpanBuilder builder, string targetService, string targetEnvironment)
        {
            if (targetService != null)
                builder.SetAnnotation(WellKnownAnnotations.Custom.Request.TargetService, targetService);

            if (targetEnvironment != null)
                builder.SetAnnotation(WellKnownAnnotations.Custom.Request.TargetEnvironment, targetEnvironment);
        }

        public static void SetReplica(this ISpanBuilder builder, string replica) =>
            builder.SetAnnotation(WellKnownAnnotations.Custom.Request.Replica, replica);

        private static void SetCustomAnnotation(this ISpanBuilder builder, string key, object value, bool allowOverwrite = true) =>
            builder.SetAnnotation($"custom.{key}", value, allowOverwrite);

        #endregion
    }
}