using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HotChocolate.Execution;
using HotChocolate.Execution.Processing;
using HotChocolate.Language;
using HotChocolate.Stitching.Utilities;

namespace HotChocolate.Stitching.Client
{
    internal class RequestMergeHelper
    {
        public static IEnumerable<(IQueryRequest, IEnumerable<BufferedRequest>)> MergeRequests(
            IEnumerable<BufferedRequest> requests)
        {
            foreach (var group in requests.GroupBy(t => t.Operation.Operation))
            {
                var rewriter = new MergeRequestRewriter();
                var variableValues = new Dictionary<string, object>();

                var operationName = group
                    .Select(r => r.Request.OperationName)
                    .Where(n => n != null)
                    .Distinct()
                    .FirstOrDefault();

                if (operationName is not null)
                {
                    rewriter.SetOperationName(new NameNode(operationName));
                }

                var i = 0;
                BufferedRequest first = null!;
                foreach (BufferedRequest request in group)
                {
                    first = request;
                    MergeRequest(request, rewriter, variableValues, $"__{i++}_");
                }

                IQueryRequest batch =
                    QueryRequestBuilder.New()
                        .SetQuery(rewriter.Merge())
                        .SetOperation(operationName)
                        .SetVariableValues(variableValues)
                        .TrySetServices(first.Request.Services)
                        .Create();

                yield return (batch, group);
            }
        }

        public static void DispatchResults(
            IQueryResult mergedResult,
            IEnumerable<BufferedRequest> requests)
        {
            var handledErrors = new HashSet<IError>();
            BufferedRequest? current = null;
            QueryResultBuilder? resultBuilder = null;

            foreach (BufferedRequest request in requests)
            {
                if (current is not null && resultBuilder is not null)
                {
                    current.Promise.SetResult(resultBuilder.Create());
                }

                try
                {
                    current = request;
                    resultBuilder = ExtractResult(request.Aliases!, mergedResult, handledErrors);
                }
                catch (Exception ex)
                {
                    current = null;
                    resultBuilder = null;
                    request.Promise.SetException(ex);
                }
            }

            if (current is not null && resultBuilder is not null)
            {
                if (handledErrors.Count < mergedResult.Errors.Count)
                {
                    foreach (IError error in mergedResult.Errors.Except(handledErrors))
                    {
                        resultBuilder.AddError(error);
                    }
                }

                handledErrors.Clear();
                current.Promise.SetResult(resultBuilder.Create());
            }
        }

        private static void MergeRequest(
            BufferedRequest bufferedRequest,
            MergeRequestRewriter rewriter,
            IDictionary<string, object> variableValues,
            NameString requestPrefix)
        {
            MergeVariables(
                bufferedRequest.Request.VariableValues,
                variableValues,
                requestPrefix);

            bufferedRequest.Aliases = rewriter.AddQuery(
                bufferedRequest,
                requestPrefix,
                true);
        }

        private static void MergeVariables(
            IReadOnlyDictionary<string, object?>? original,
            IDictionary<string, object> merged,
            NameString requestPrefix)
        {
            if (original is not null)
            {
                foreach (KeyValuePair<string, object?> item in original)
                {
                    string variableName = MergeUtils.CreateNewName(item.Key, requestPrefix);
                    merged.Add(variableName, item.Value);
                }
            }
        }

        private static QueryResultBuilder ExtractResult(
            IDictionary<string, string> aliases,
            IQueryResult mergedResult,
            ICollection<IError> handledErrors)
        {
            var result = QueryResultBuilder.New();
            var data = new ResultMap();
            data.EnsureCapacity(aliases.Count);
            var i = 0;

            foreach (KeyValuePair<string, string> alias in aliases)
            {
                if (mergedResult.Data.TryGetValue(alias.Key, out object o))
                {
                    data.SetValue(i++, alias.Value, o);
                }
            }

            result.SetData(data);

            if (mergedResult.Errors is not null)
            {
                foreach (IError error in mergedResult.Errors)
                {
                    if (TryResolveField(error, aliases, out string responseName))
                    {
                        handledErrors.Add(error);
                        result.AddError(RewriteError(error, responseName));
                    }
                }
            }

            if (mergedResult.Extensions is not null)
            {
                result.SetExtensions(mergedResult.Extensions);
            }

            if (mergedResult.ContextData is not null)
            {
                foreach (KeyValuePair<string, object?> item in mergedResult.ContextData)
                {
                    result.SetContextData(item.Key, item.Value);
                }
            }

            return result;
        }

        private static IError RewriteError(IError error, string responseName)
        {
            if (error.Path is null)
            {
                return error;
            }

            return error.WithPath(error.Path.Depth == 1
                ? Path.New(responseName)
                : ReplaceRoot(error.Path, responseName));
        }

        private static bool TryResolveField(
            IError error,
            IDictionary<string, string> aliases,
            [NotNullWhen(true)]out string? responseName)
        {
            if (GetRoot(error.Path) is NamePathSegment root &&
                aliases.TryGetValue(root.Name, out string s))
            {
                responseName = s;
                return true;
            }

            responseName = null;
            return false;
        }

        private static Path? GetRoot(Path? path)
        {
            Path? current = path;

            if (path is null || path is RootPathSegment)
            {
                return null;
            }

            while (current.Parent is not null && current.Parent is not RootPathSegment)
            {
                current = current.Parent;
            }

            return current;
        }

        private static Path ReplaceRoot(Path path, string responseName)
        {
            Path[] buffer = ArrayPool<Path>.Shared.Rent(path.Depth);
            Span<Path> paths = buffer.AsSpan().Slice(0, path.Depth);

            try
            {
                int i = path.Depth;
                Path? current = path;

                do
                {
                    paths[--i] = current;
                    current = current.Parent;
                } while (current is not null && current is not RootPathSegment);

                paths = paths.Slice(1);

                current = Path.New(responseName);

                for (i = 0; i < paths.Length; i++)
                {
                    if (paths[i] is IndexerPathSegment index)
                    {
                        current = current.Append(index.Index);
                    }
                    else if (paths[i] is NamePathSegment name)
                    {
                        current = current.Append(name.Name);
                    }
                }

                return current;
            }
            finally
            {
                ArrayPool<Path>.Shared.Return(buffer);
            }
        }
    }
}
