using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;

namespace Trimmer.Rendering;

/// <summary>
/// Base class that every compiled <c>.cshtml</c> page derives from. The Razor
/// code generator emits calls to <see cref="WriteLiteral"/>, <see cref="Write(object)"/>
/// and the attribute helpers below; this class turns those calls into an HTML string.
/// </summary>
public abstract class TrimmerPage
{
    private readonly StringBuilder _output = new();
    private string? _attributeSuffix;

    /// <summary>The current request context (null when rendered outside a request).</summary>
    public HttpContext? Context { get; set; }

    public HttpRequest? Request => Context?.Request;

    public HttpResponse? Response => Context?.Response;

    public IQueryCollection Query => Context?.Request.Query ?? new QueryCollection();

    /// <summary>Razor entry point implemented by the generated class.</summary>
    public abstract Task ExecuteAsync();

    /// <summary>Runs the page and returns the rendered HTML.</summary>
    public async Task<string> RenderAsync()
    {
        _output.Clear();
        await ExecuteAsync();
        return _output.ToString();
    }

    /// <summary>Marks a string as already-encoded HTML so it is written verbatim.</summary>
    public static RawString Raw(string? value) => new(value ?? string.Empty);

    protected void WriteLiteral(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _output.Append(value);
        }
    }

    protected void Write(object? value)
    {
        switch (value)
        {
            case null:
                return;
            case RawString raw:
                _output.Append(raw.Value);
                return;
            default:
                _output.Append(WebUtility.HtmlEncode(value.ToString()));
                return;
        }
    }

    protected void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _output.Append(WebUtility.HtmlEncode(value));
        }
    }

    // The methods below mirror the signatures emitted by Razor's runtime code
    // generator for attribute rendering (e.g. <a href="@url">).
    protected void BeginWriteAttribute(
        string name,
        string prefix,
        int prefixOffset,
        string suffix,
        int suffixOffset,
        int attributeValuesCount)
    {
        _attributeSuffix = suffix;
        _output.Append(prefix);
    }

    protected void WriteAttributeValue(
        string prefix,
        int prefixOffset,
        object? value,
        int valueOffset,
        int valueLength,
        bool isLiteral)
    {
        _output.Append(prefix);
        if (value is RawString raw)
        {
            _output.Append(raw.Value);
        }
        else if (value is bool b)
        {
            _output.Append(b ? "true" : "false");
        }
        else if (value is not null)
        {
            var text = value.ToString();
            _output.Append(isLiteral ? text : WebUtility.HtmlEncode(text));
        }
    }

    protected void EndWriteAttribute()
    {
        _output.Append(_attributeSuffix);
        _attributeSuffix = null;
    }

    /// <summary>Convenience helper for use inside <c>@code</c> blocks.</summary>
    protected static string HtmlEncode(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);
}

/// <summary>A string value that should be written to the page without HTML encoding.</summary>
public readonly record struct RawString(string Value)
{
    public override string ToString() => Value;
}
