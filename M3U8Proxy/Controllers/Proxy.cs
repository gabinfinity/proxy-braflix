using System.Net.Http.Headers;
using System.Web;
using AspNetCore.Proxy;
using AspNetCore.Proxy.Options;
using M3U8Proxy.RequestHandler;
using M3U8Proxy.RequestHandler.AfterReceive;
using M3U8Proxy.RequestHandler.BeforeSend;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace M3U8Proxy.Controllers;

[EnableCors("corsPolicy")]
[ApiController]
[Route("[controller]")]
public partial class Proxy : Controller
{
    private readonly ReqHandler _reqHandler = new();
    private readonly HttpClientHandler _handler = new() { AllowAutoRedirect = false };
    private const int RequestTimeoutMinutes = 15;

    [HttpHead]
    [HttpGet]
    [Route("{url}/{headers?}/{type?}")]
    public async Task<IActionResult> GetProxy(string url, string? headers = "{}", string? forcedHeadersProxy = "{}")
    {
        try
        {
            var decodedUrl = HttpUtility.UrlDecode(url);
            var headersDictionary = DeserializeHeaders(headers);
            var forcedHeadersProxyDictionary = DeserializeHeaders(forcedHeadersProxy);

            var options = BuildProxyOptions(headersDictionary, forcedHeadersProxyDictionary);
            await this.HttpProxyAsync(decodedUrl, options);
            return Ok();
        }
        catch (Exception e)
        {
            return HandleExceptionResponse(e);
        }
    }

    [Route("grabRedirect/{url}/{headers?}")]
    public async Task<IActionResult> GrabRedirect(string url, string? headers = "{}")
    {
        try
        {
            var decodedUrl = HttpUtility.UrlDecode(url);
            var headersDictionary = DeserializeHeaders(headers);

            var redirectedUrl = await GetRedirectedUrl(decodedUrl, headersDictionary);
            var result = new { url = redirectedUrl != null ? $"{_baseUrl}{redirectedUrl}" : null };
            return Ok(result);
        }
        catch (Exception e)
        {
            return BadRequest(JsonConvert.SerializeObject(e));
        }
    }

    private HttpProxyOptions BuildProxyOptions(Dictionary<string, string>? headersDictionary, Dictionary<string, string>? forcedHeadersProxyDictionary)
    {
        return HttpProxyOptionsBuilder.Instance
            .WithShouldAddForwardedHeaders(false)
            .WithBeforeSend((_, hrm) =>
            {
                if (headersDictionary != null)
                {
                    BeforeSend.RemoveHeaders(hrm);
                    BeforeSend.AddHeaders(headersDictionary, hrm);
                    hrm.Headers.Remove("Host");
                }

                return Task.CompletedTask;
            })
            .WithHandleFailure(async (context, e) =>
            {
                context.Response.StatusCode = context.Response.StatusCode;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(e));
            })
            .WithAfterReceive((_, hrm) =>
            {
                AfterReceive.RemoveHeaders(hrm);
                if (forcedHeadersProxyDictionary != null)
                {
                    AfterReceive.AddForcedHeaders(forcedHeadersProxyDictionary, hrm);
                }
                hrm.Headers.Remove("Cross-Origin-Resource-Policy");
                hrm.Headers.Add("Cross-Origin-Resource-Policy", "*");
                return Task.CompletedTask;
            })
            .Build();
    }

    private Dictionary<string, string>? DeserializeHeaders(string? headersJson)
    {
        return string.IsNullOrWhiteSpace(headersJson) ? null : JsonConvert.DeserializeObject<Dictionary<string, string>>(headersJson!);
    }

    private async Task<string?> GetRedirectedUrl(string url, Dictionary<string, string>? headersDictionary)
    {
        using var client = new HttpClient(_handler) { Timeout = TimeSpan.FromMinutes(RequestTimeoutMinutes) };
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (headersDictionary != null)
        {
            foreach (var header in headersDictionary)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }

        var response = await client.SendAsync(request);
        return response.Headers.Location?.AbsoluteUri;
    }

    private IActionResult HandleExceptionResponse(Exception e)
    {
        HttpContext.Response.StatusCode = 400;
        HttpContext.Response.ContentType = "application/json";
        return BadRequest(JsonConvert.SerializeObject(e));
    }
}
