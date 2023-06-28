using System.Net.Http.Headers;

public static class HttpClientExtensions
{
    public static HttpClient SetHeader(this HttpClient cl, string key, string value)
    {
        cl.DefaultRequestHeaders.Remove(key);
        cl.DefaultRequestHeaders.Add(key, value);
        return cl;
    }
    public static HttpClient AddHeader(this HttpClient cl, string key, string value)
    {
        cl.DefaultRequestHeaders.Add(key, value);
        return cl;
    }
}
