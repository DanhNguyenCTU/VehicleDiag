using Microsoft.AspNetCore.Mvc;
using VehicleDiag.Api.Services;

namespace VehicleDiag.Api.Controllers
{
    [ApiController]
    [Route("api/geocode")]
    public class GeocodeController : ControllerBase
    {
        private readonly OsmGeocodingService _geo;

        public GeocodeController(OsmGeocodingService geo)
        {
            _geo = geo;
        }

        [HttpGet]
        public async Task<IActionResult> Reverse(double lat, double lng)
        {
            var address = await _geo.GetAddressAsync(lat, lng);

            return Ok(new
            {
                latitude = lat,
                longitude = lng,
                address = address
            });
        }
    }
}