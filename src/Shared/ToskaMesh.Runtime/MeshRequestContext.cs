using Microsoft.AspNetCore.Http;

namespace ToskaMesh.Runtime;

/// <summary>
/// Lightweight request context abstraction to avoid leaking HttpContext.
/// </summary>
public sealed class MeshRequestContext
{
    private readonly HttpContext _context;

    internal MeshRequestContext(HttpContext context)
    {
        _context = context;
    }

    public string Path => _context.Request.Path;
    public string Method => _context.Request.Method;
    public IServiceProvider Services => _context.RequestServices;
    public CancellationToken RequestAborted => _context.RequestAborted;

    public void SetHeader(string key, string value) => _context.Response.Headers.Append(key, value);
}
