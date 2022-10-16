using Microsoft.AspNetCore.Mvc;
using NanoCache.WebApiExample;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NanoExchange.PublicRestApi.Controllers
{
    [Route("api/v1")]
    public class CacheController : ControllerBase
    {
        private readonly AppCache _appCache;

        public CacheController(AppCache appCache)
        {
            _appCache = appCache;
        }

        [HttpGet("cache/single")]
        public async Task<ActionResult> Single()
        {
            var a01 = _appCache.GetFromMemoryCache();
            var a02 = await _appCache.GetFromDistributedCache();

            return this.Ok(new
            {
                Memo = a01,
                Dist = a02,
            });
        }

        [HttpGet("cache/speed")]
        public async Task<ActionResult> Speed(int limit=100)
        {
            var a01 = _appCache.GetFromMemoryCache();
            var a02 = await _appCache.GetFromDistributedCache();

            var sw01 = Stopwatch.StartNew();
            for (var i = 0; i < limit; i++) a01 = _appCache.GetFromMemoryCache();
            sw01.Stop();

            var sw02 = Stopwatch.StartNew();
            for (var i = 0; i < limit; i++) a02 = await _appCache.GetFromDistributedCache();
            sw02.Stop();

            return this.Ok(new
            {
                Memo = sw01.Elapsed,
                Dist = sw02.Elapsed,
            });
        }
    }
}
