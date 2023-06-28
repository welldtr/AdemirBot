using System.Text.RegularExpressions;

using Microsoft.ClearScript.V8;

using HtmlAgilityPack;

/// <summary>
/// A custom Http handler for Cloudflare protected servers
/// </summary>
public partial class CloudflareHttpHandler : HttpClientHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var task = base.SendAsync(request, cancellationToken);

        var response = task.Result;

        if (base.CookieContainer.GetCookieHeader(request.RequestUri).Contains("cf_clearance"))
            return task;

        IEnumerable<string> values;

        if (response.Headers.TryGetValues("refresh", out values) && values.FirstOrDefault().Contains("URL=/cdn-cgi/") && response.Headers.Server.ToString() == "cloudflare-nginx")
        {
            Console.WriteLine("Solving cloudflare challenge . . . ");

            string content = response.Content.ReadAsStringAsync().Result;

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);

            var jschl_vc = htmlDocument.DocumentNode.SelectSingleNode(@".//input[@name=""jschl_vc""]").Attributes["value"].Value;
            var pass = htmlDocument.DocumentNode.SelectSingleNode(@".//input[@name=""pass""]").Attributes["value"].Value;

            var script = htmlDocument.DocumentNode.SelectSingleNode(@".//script").InnerText;

            var regex = new string[3] {
                @"setTimeout\(function\(\){(.+)},\s*\d*\s*\)\s*;",
                @"^\n*\s*(var\s+.*?;)",
                @"(?<=\s+;)(.+t.length;)"
            };

            string function, vars, calc;

            function = Regex.Match(script, regex[0], RegexOptions.Singleline).Value;
            vars = Regex.Match(function, regex[1], RegexOptions.Multiline).Value;
            calc = Regex.Match(function, regex[2], RegexOptions.Singleline).Value
                .Replace("a.value", "var result")
                .Replace("t.length", (request.RequestUri.Host.Length).ToString()); ;

            object result;
            using (var engine = new V8ScriptEngine())
                result = engine.Evaluate("function getAnswer() {" + vars + calc + "return result;" + "} getAnswer();");

            Thread.Sleep(5000);

            var requestUri = request.RequestUri;

            request.RequestUri = new Uri(requestUri, String.Format("cdn-cgi/l/chk_jschl?jschl_vc={0}&pass={1}&jschl_answer={2}", jschl_vc, pass, result.ToString()));

            base.SendAsync(request, cancellationToken).Wait();

            request.RequestUri = requestUri;

            return base.SendAsync(request, cancellationToken);
        }

        return task;
    }
}