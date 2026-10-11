using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace LoDb.Testing;

/// <summary>
/// Disposes the rate limiters held by the request pipelines of a factory's hosts, once the
/// hosts are gone.
/// </summary>
/// <remarks>
/// ASP.NET Core's rate limiting middleware creates its endpoint limiter in its constructor and
/// never disposes it. The limiter's heartbeat timer stays in the process-wide timer queue and
/// holds the middleware, the rest of the pipeline and every service of the host. A process
/// with one host never notices; a test run that starts hundreds of hosts kept all of them, up
/// to 30 GB. The middleware is internal: it is reached by walking the built pipeline, from each
/// delegate to its target, through every field of the closures that wrap a middleware, and
/// through the fields of a middleware that hold a delegate, a limiter or compiled constants.
/// </remarks>
internal sealed class PipelineRateLimiters : IStartupFilter
{
    private const BindingFlags DeclaredInstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    private readonly Lock _gate = new();
    private readonly List<RequestDelegate> _pipelines = [];

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(pipeline =>
            {
                lock (_gate)
                {
                    _pipelines.Add(pipeline);
                }

                return pipeline;
            });
            next(app);
        };

    /// <summary>Disposes the limiters of every pipeline built so far, then forgets them.</summary>
    public void Release()
    {
        RequestDelegate[] pipelines;
        lock (_gate)
        {
            pipelines = [.. _pipelines];
            _pipelines.Clear();
        }

        foreach (var limiter in pipelines.SelectMany(LimitersOf))
        {
            limiter.Dispose();
        }
    }

    private static List<PartitionedRateLimiter<HttpContext>> LimitersOf(RequestDelegate pipeline)
    {
        var limiters = new List<PartitionedRateLimiter<HttpContext>>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>([pipeline]);
        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            if (current is PartitionedRateLimiter<HttpContext> limiter)
            {
                limiters.Add(limiter);
                continue;
            }

            foreach (var next in Successors(current))
            {
                pending.Push(next);
            }
        }

        return limiters;
    }

    private static IEnumerable<object> Successors(object current) =>
        current switch
        {
            Delegate step => step.Target is { } target ? [target] : [],
            object?[] constants => constants.OfType<object>(),
            _ => FieldsOf(current.GetType())
                .Where(field => IsClosure(current.GetType())
                    ? !field.FieldType.IsValueType
                    : IsWalked(field.FieldType))
                .Select(field => field.GetValue(current))
                .OfType<object>(),
        };

    // A middleware may keep its next step in a private field of its base class.
    private static IEnumerable<FieldInfo> FieldsOf(Type type)
    {
        for (var declaring = type; declaring is not null; declaring = declaring.BaseType)
        {
            foreach (var field in declaring.GetFields(DeclaredInstanceFields))
            {
                yield return field;
            }
        }
    }

    private static bool IsClosure(Type type) =>
        type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    private static bool IsWalked(Type type) =>
        typeof(Delegate).IsAssignableFrom(type)
        || typeof(PartitionedRateLimiter<HttpContext>).IsAssignableFrom(type)
        || type == typeof(object[]);
}
