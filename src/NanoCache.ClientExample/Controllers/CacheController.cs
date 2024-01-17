namespace NanoCache.ClientExample.Controllers;

[Route("v1")]
public class CacheController : ControllerBase
{
    private readonly AppCache _appCache;

    public CacheController(AppCache appCache)
    {
        _appCache = appCache;
    }

    [HttpGet("cache/single")]
    public async Task<ActionResult> SingleAsync()
    {
        var a01 = _appCache.GetFromMemoryCache();
        var a02 = await _appCache.GetFromDistributedCacheAsync();

        return this.Ok(new
        {
            Memo = a01,
            Dist = a02,
        });
    }

    [HttpGet("cache/speed")]
    public async Task<ActionResult> SpeedAsync(int limit = 100)
    {
        _appCache.GetFromMemoryCache();
        await _appCache.GetFromDistributedCacheAsync();

        var sw01 = Stopwatch.StartNew();
        for (var i = 0; i < limit; i++) _appCache.GetFromMemoryCache();
        sw01.Stop();

        var sw02 = Stopwatch.StartNew();
        for (var i = 0; i < limit; i++) await _appCache.GetFromDistributedCacheAsync();
        sw02.Stop();

        return this.Ok(new
        {
            Memo = sw01.Elapsed,
            Dist = sw02.Elapsed,
        });
    }
}
